package game

import (
	"context"
	"math"
	"math/rand/v2"
	"time"

	"github.com/Nafiz001/sandglass/server/internal/arena"
	"github.com/Nafiz001/sandglass/server/internal/geom"
)

const (
	TickRate      = 30
	dt            = 1.0 / TickRate
	RoundsToWin   = 3 // best of 5
	countdownTime = 3.0
	roundTime     = 100.0
	roundOverTime = 4.0
	matchEndTime  = 5.0

	maxSpeed    = 7.5 // sprint speed, m/s
	maxRewind   = 9   // ticks of lag compensation (300 ms)
	originSlack = 1.0 // metres a shot may start from the server's eye
)

// Match states, as sent to clients.
const (
	StCountdown = "countdown"
	StLive      = "live"
	StOver      = "over"
	StEnded     = "ended"
)

type inbound struct {
	slot int
	msg  In
	quit bool
}

// Match runs one 1v1 game on its own goroutine at TickRate.
type Match struct {
	Room  string
	World *arena.World
	P     [2]*Player
	bot   *Bot

	inbox chan inbound
	tick  uint32
	now   float64
	rng   *rand.Rand

	state      string
	stateUntil float64
	round      int
	score      [2]int
	finished   bool
	onFinish   func(*Match)
}

func newMatch(w *arena.World, room string, a, b *Player, seed uint64) *Match {
	m := &Match{
		Room: room, World: w, P: [2]*Player{a, b},
		inbox: make(chan inbound, 256),
		rng:   rand.New(rand.NewPCG(seed, seed^0x9e3779b97f4a7c15)),
	}
	a.Slot, b.Slot = 0, 1
	if b.Bot {
		m.bot = newBot(m, b)
	}
	return m
}

// Deliver queues a message from a player's connection. It never blocks.
func (m *Match) Deliver(slot int, msg In) {
	select {
	case m.inbox <- inbound{slot: slot, msg: msg}:
	default:
	}
}

// Quit reports that a player's connection closed.
func (m *Match) Quit(slot int) {
	select {
	case m.inbox <- inbound{slot: slot, quit: true}:
	default:
		// The inbox is full; the match will still notice on its next send.
		go func() { m.inbox <- inbound{slot: slot, quit: true} }()
	}
}

func (m *Match) Run(ctx context.Context) {
	m.start()
	t := time.NewTicker(time.Second / TickRate)
	defer t.Stop()
	for !m.finished {
		select {
		case <-ctx.Done():
			m.finish(-1, "server shutting down")
		case in := <-m.inbox:
			m.handle(in)
		case <-t.C:
			m.step()
		}
	}
	if m.onFinish != nil {
		m.onFinish(m)
	}
}

func (m *Match) start() {
	for _, p := range m.P {
		p.Send(encode(msgMatch{
			T: "match", You: p.Slot, Room: m.Room,
			Names: [2]string{m.P[0].Name, m.P[1].Name},
			Bots:  [2]bool{m.P[0].Bot, m.P[1].Bot},
		}))
	}
	m.startRound()
}

func (m *Match) broadcast(b []byte) {
	for _, p := range m.P {
		p.Send(b)
	}
}

func (m *Match) setState(s string, d float64) {
	m.state, m.stateUntil = s, m.now+d
}

func (m *Match) startRound() {
	m.round++
	for i, p := range m.P {
		sp := m.World.Spawns[i]
		p.reset(sp.Pos, sp.Yaw)
		p.lastMoveAt = m.now
		p.Send(encode(msgCorrect{T: "correct", P: arr(sp.Pos), Yaw: sp.Yaw}))
	}
	if m.bot != nil {
		m.bot.reset()
	}
	m.setState(StCountdown, countdownTime)
	m.broadcast(encode(msgRound{T: "round", State: StCountdown, Round: m.round, Score: m.score, Winner: -1}))
}

func (m *Match) endRound(winner int, reason string) {
	if winner >= 0 {
		m.score[winner]++
	}
	m.setState(StOver, roundOverTime)
	m.broadcast(encode(msgRound{T: "round", State: StOver, Round: m.round, Score: m.score, Winner: winner, Reason: reason}))
	if winner >= 0 && m.score[winner] >= RoundsToWin {
		m.setState(StEnded, matchEndTime)
		m.broadcast(encode(msgEnd{T: "end", Winner: winner, Score: m.score}))
	}
}

// finish ends the match at once (forfeit or shutdown).
func (m *Match) finish(winner int, reason string) {
	if m.finished {
		return
	}
	if m.state != StEnded {
		m.broadcast(encode(msgEnd{T: "end", Winner: winner, Score: m.score, Reason: reason}))
	}
	m.finished = true
}

// step advances the simulation by one tick.
func (m *Match) step() {
	m.tick++
	m.now += dt

	for _, p := range m.P {
		if p.reloadUntil > 0 && m.now >= p.reloadUntil {
			w := Weapons[p.Weapon]
			n := min(w.Mag-p.Mag[p.Weapon], p.Reserve[p.Weapon])
			p.Mag[p.Weapon] += n
			p.Reserve[p.Weapon] -= n
			p.reloadUntil = 0
		}
		if p.strikes > 0 && m.tick%TickRate == 0 {
			p.strikes--
		}
	}

	switch m.state {
	case StCountdown:
		if m.now >= m.stateUntil {
			m.setState(StLive, roundTime)
			m.broadcast(encode(msgRound{T: "round", State: StLive, Round: m.round, Score: m.score, Winner: -1}))
		}
	case StLive:
		if m.bot != nil {
			m.bot.think()
		}
		if m.now >= m.stateUntil {
			a, b := m.P[0].HP, m.P[1].HP
			switch {
			case a > b:
				m.endRound(0, "time")
			case b > a:
				m.endRound(1, "time")
			default:
				m.endRound(-1, "draw")
			}
		}
	case StOver:
		if m.now >= m.stateUntil {
			m.startRound()
		}
	case StEnded:
		if m.now >= m.stateUntil {
			m.finished = true
		}
	}

	for _, p := range m.P {
		p.record(m.tick)
	}
	m.sendSnapshots()
}

func (m *Match) sendSnapshots() {
	s := msgSnap{T: "snap", Tick: m.tick, State: m.state, Left: round2(math.Max(m.stateUntil-m.now, 0))}
	for i, p := range m.P {
		s.Players[i] = snapPlayer{
			P: arr(p.Pos), Yaw: round2(p.Yaw), Pitch: round2(p.Pitch),
			HP: p.HP, W: p.Weapon, Alive: p.Alive, Rl: p.reloadUntil > 0,
		}
	}
	for _, p := range m.P {
		if p.Bot {
			continue
		}
		s.Mag, s.Res = p.Mag[p.Weapon], p.Reserve[p.Weapon]
		p.Send(encode(s))
	}
}

func (m *Match) handle(in inbound) {
	if in.slot < 0 || in.slot > 1 {
		return
	}
	p := m.P[in.slot]
	if in.quit {
		m.finish(1-in.slot, "opponent left")
		return
	}
	switch in.msg.T {
	case "move":
		m.move(p, in.msg)
	case "shoot":
		m.shoot(p, vec(in.msg.O), vec(in.msg.D), in.msg.W, in.msg.Tick)
	case "reload":
		m.reload(p)
	case "switch":
		m.switchWeapon(p, in.msg.W)
	case "leave":
		m.finish(1-in.slot, "opponent left")
	}
}

// move accepts a client-simulated position if it is physically plausible;
// otherwise it snaps the client back to its last accepted position.
func (m *Match) move(p *Player, msg In) {
	if m.state != StLive || !p.Alive {
		return
	}
	pos := vec(msg.P)
	elapsed := math.Min(math.Max(m.now-p.lastMoveAt, dt), 0.5)
	ok := pos.Finite() && m.World.InBounds(pos) &&
		pos.HorizDist(p.Pos) <= maxSpeed*elapsed*1.35+0.35 &&
		math.Abs(pos.Y-p.Pos.Y) <= 14*elapsed+0.6 &&
		m.World.Clearance(pos.Add(geom.V(0, 1.0, 0))) > 0.12
	if !ok {
		p.strikes++
		p.Send(encode(msgCorrect{T: "correct", P: arr(p.Pos), Yaw: p.Yaw}))
		return
	}
	p.Pos = pos
	p.Yaw = math.Mod(msg.Yaw, 360)
	p.Pitch = math.Max(-89, math.Min(89, msg.Pitch))
	p.lastMoveAt = m.now
}

func (m *Match) reload(p *Player) {
	w := Weapons[p.Weapon]
	if !p.Alive || p.reloadUntil > 0 || p.Mag[p.Weapon] >= w.Mag || p.Reserve[p.Weapon] == 0 {
		return
	}
	p.reloadUntil = m.now + w.Reload
}

func (m *Match) switchWeapon(p *Player, w int) {
	if w < 0 || w >= len(Weapons) || w == p.Weapon || !p.Alive {
		return
	}
	p.Weapon = w
	p.reloadUntil = 0
	p.nextFire = math.Max(p.nextFire, m.now+switchDelay)
}

// shoot validates and resolves a hitscan shot. rewindTick is the tick the
// shooter was looking at; the target is rewound there (bounded) so shots
// that were on target on the shooter's screen still land.
func (m *Match) shoot(p *Player, origin, dir geom.Vec3, weapon int, rewindTick uint32) {
	if m.state != StLive || !p.Alive || weapon != p.Weapon || p.reloadUntil > 0 {
		return
	}
	w := Weapons[p.Weapon]
	// Allow a little network jitter, but never more than the weapon's rate
	// over time: nextFire advances from the later of now and its old value.
	if m.now < p.nextFire-0.06 || p.Mag[p.Weapon] <= 0 {
		return
	}
	if !origin.Finite() || !dir.Finite() || origin.Dist(p.Eye()) > originSlack {
		return
	}
	dir = dir.Norm()
	if dir.Len() == 0 {
		return
	}
	p.nextFire = math.Max(p.nextFire, m.now) + w.Interval
	p.Mag[p.Weapon]--

	if rewindTick > m.tick || m.tick-rewindTick > maxRewind {
		rewindTick = m.tick - min(m.tick, maxRewind)
	}

	dist := m.World.Raycast(origin, dir, w.Range)
	target := m.P[1-p.Slot]
	hit, head := -1, false
	if target.Alive {
		feet := target.PosAt(rewindTick)
		th, okH := hitHead(feet).Raycast(origin, dir, dist)
		tb, okB := hitBody(feet).Raycast(origin, dir, dist)
		switch {
		case okH && (!okB || th <= tb):
			hit, head, dist = target.Slot, true, th
		case okB:
			hit, dist = target.Slot, tb
		}
	}
	ev := msgShot{T: "shot", By: p.Slot, W: p.Weapon, O: arr(origin), E: arr(origin.Add(dir.Scale(dist))), Hit: hit, Head: head}
	if hit >= 0 {
		ev.Dmg = w.Body
		if head {
			ev.Dmg = w.Head
		}
		target.HP = max(target.HP-ev.Dmg, 0)
	}
	m.broadcast(encode(ev))
	if hit >= 0 && target.HP == 0 {
		target.Alive = false
		m.endRound(p.Slot, "kill")
	}
}
