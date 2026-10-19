package game

import (
	"context"
	crand "crypto/rand"
	"strings"
	"sync"
	"time"
	"unicode"

	mrand "math/rand/v2"

	"github.com/Nafiz001/sandglass/server/internal/arena"
)

var botNames = []string{"Sirocco", "Haboob", "Khamsin", "Simoom", "Harmattan"}

const roomAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"

// Session is one connected client as the lobby sees it.
type Session struct {
	Name string
	Send func([]byte)

	mu    sync.Mutex
	match *Match
	slot  int
	queue string // mode while waiting: "quick" or "private"
	room  string
}

// Lobby pairs sessions into matches: quick match (with a bot stepping in
// if nobody else is waiting), straight-to-bot, and private room codes.
type Lobby struct {
	World       *arena.World
	BotFallback time.Duration
	ctx         context.Context

	mu      sync.Mutex
	waiting *Session            // quick-match queue (one is enough for 1v1)
	rooms   map[string]*Session // private room code -> host
	matches map[*Match]struct{}
	online  int
}

func NewLobby(ctx context.Context, w *arena.World) *Lobby {
	return &Lobby{World: w, BotFallback: 12 * time.Second, ctx: ctx,
		rooms: map[string]*Session{}, matches: map[*Match]struct{}{}}
}

type Stats struct {
	Online  int `json:"online"`
	Matches int `json:"matches"`
	Waiting int `json:"waiting"`
	Rooms   int `json:"rooms"`
}

func (l *Lobby) Stats() Stats {
	l.mu.Lock()
	defer l.mu.Unlock()
	s := Stats{Online: l.online, Matches: len(l.matches), Rooms: len(l.rooms)}
	if l.waiting != nil {
		s.Waiting = 1
	}
	return s
}

func (l *Lobby) Connected()    { l.mu.Lock(); l.online++; l.mu.Unlock() }
func (l *Lobby) Disconnected() { l.mu.Lock(); l.online--; l.mu.Unlock() }

// CleanName keeps names short and printable.
func CleanName(s string) string {
	var b strings.Builder
	for _, r := range strings.TrimSpace(s) {
		if unicode.IsLetter(r) || unicode.IsDigit(r) || r == ' ' || r == '_' || r == '-' {
			b.WriteRune(r)
		}
		if b.Len() >= 16 {
			break
		}
	}
	if b.Len() == 0 {
		return "Wanderer"
	}
	return b.String()
}

// Handle routes one message from a session.
func (l *Lobby) Handle(s *Session, msg In) {
	s.mu.Lock()
	m, slot := s.match, s.slot
	s.mu.Unlock()
	if m != nil {
		m.Deliver(slot, msg)
		return
	}
	if msg.T != "hello" {
		return
	}
	s.Name = CleanName(msg.Name)
	switch msg.Mode {
	case "bot":
		l.startBotMatch(s, "")
	case "private":
		l.joinPrivate(s, strings.ToUpper(strings.TrimSpace(msg.Room)))
	default:
		l.joinQuick(s)
	}
}

// Leave is called when a session's connection closes.
func (l *Lobby) Leave(s *Session) {
	s.mu.Lock()
	m, slot, room := s.match, s.slot, s.room
	s.match = nil
	s.mu.Unlock()
	if m != nil {
		m.Quit(slot)
		return
	}
	l.mu.Lock()
	if l.waiting == s {
		l.waiting = nil
	}
	if room != "" && l.rooms[room] == s {
		delete(l.rooms, room)
	}
	l.mu.Unlock()
}

func (l *Lobby) joinQuick(s *Session) {
	l.mu.Lock()
	if other := l.waiting; other != nil && other != s {
		l.waiting = nil
		l.mu.Unlock()
		l.startMatch(other, s, nil, "", "")
		return
	}
	l.waiting = s
	l.mu.Unlock()
	s.Send(encode(msgQueue{T: "queue", Mode: "quick"}))
	time.AfterFunc(l.BotFallback, func() {
		l.mu.Lock()
		still := l.waiting == s
		if still {
			l.waiting = nil
		}
		l.mu.Unlock()
		if still {
			l.startBotMatch(s, "No one else is online right now, so a bot stepped in.")
		}
	})
}

func (l *Lobby) joinPrivate(s *Session, code string) {
	l.mu.Lock()
	if code != "" {
		host, ok := l.rooms[code]
		if !ok {
			l.mu.Unlock()
			s.Send(encode(msgError{T: "error", Msg: "No room with code " + code + "."}))
			return
		}
		delete(l.rooms, code)
		l.mu.Unlock()
		l.startMatch(host, s, nil, code, "")
		return
	}
	for {
		code = newRoomCode()
		if _, taken := l.rooms[code]; !taken {
			break
		}
	}
	l.rooms[code] = s
	l.mu.Unlock()
	s.mu.Lock()
	s.room = code
	s.mu.Unlock()
	s.Send(encode(msgQueue{T: "queue", Mode: "private", Room: code}))
}

func (l *Lobby) startBotMatch(s *Session, note string) {
	bot := &Player{Name: botNames[mrand.IntN(len(botNames))], Bot: true}
	l.startMatch(s, nil, bot, "", note)
}

// startMatch pairs a and b (or a and a bot) and runs the match.
func (l *Lobby) startMatch(a, b *Session, bot *Player, room, note string) {
	pa := &Player{Name: a.Name, send: a.Send}
	pb := bot
	if b != nil {
		pb = &Player{Name: b.Name, send: b.Send}
	}
	m := newMatch(l.World, room, pa, pb, mrand.Uint64())
	if note != "" {
		a.Send(encode(msgError{T: "note", Msg: note}))
	}
	for i, s := range []*Session{a, b} {
		if s == nil {
			continue
		}
		s.mu.Lock()
		s.match, s.slot, s.room = m, i, ""
		s.mu.Unlock()
	}
	m.onFinish = func(m *Match) {
		for _, s := range []*Session{a, b} {
			if s == nil {
				continue
			}
			s.mu.Lock()
			if s.match == m {
				s.match = nil
			}
			s.mu.Unlock()
		}
		l.mu.Lock()
		delete(l.matches, m)
		l.mu.Unlock()
	}
	l.mu.Lock()
	l.matches[m] = struct{}{}
	l.mu.Unlock()
	go m.Run(l.ctx)
}

func newRoomCode() string {
	var b [4]byte
	_, _ = crand.Read(b[:])
	for i := range b {
		b[i] = roomAlphabet[int(b[i])%len(roomAlphabet)]
	}
	return string(b[:])
}
