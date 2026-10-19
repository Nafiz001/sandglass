package game

import (
	"context"
	"encoding/json"
	"sync"
	"testing"
	"time"

	"github.com/Nafiz001/sandglass/server/internal/arena"
	"github.com/Nafiz001/sandglass/server/internal/geom"
)

var (
	worldOnce sync.Once
	world     *arena.World
)

func testWorld(t *testing.T) *arena.World {
	t.Helper()
	worldOnce.Do(func() {
		w, err := arena.Load()
		if err != nil {
			panic(err)
		}
		world = w
	})
	return world
}

// inbox records every message a fake client receives.
type inbox struct {
	mu   sync.Mutex
	msgs []map[string]any
}

func (b *inbox) send(raw []byte) {
	var m map[string]any
	_ = json.Unmarshal(raw, &m)
	b.mu.Lock()
	b.msgs = append(b.msgs, m)
	b.mu.Unlock()
}

func (b *inbox) last(kind string) map[string]any {
	b.mu.Lock()
	defer b.mu.Unlock()
	for i := len(b.msgs) - 1; i >= 0; i-- {
		if b.msgs[i]["t"] == kind {
			return b.msgs[i]
		}
	}
	return nil
}

func liveMatch(t *testing.T) (*Match, *inbox, *inbox) {
	t.Helper()
	a, b := &inbox{}, &inbox{}
	m := newMatch(testWorld(t), "", &Player{Name: "A", send: a.send}, &Player{Name: "B", send: b.send}, 1)
	m.start()
	for m.state != StLive {
		m.step()
	}
	return m, a, b
}

// place puts both players in the open between the south spawn and the colonnade.
func place(m *Match) (*Player, *Player) {
	p, q := m.P[0], m.P[1]
	p.Pos, q.Pos = geom.V(0, 0, -20), geom.V(0, 0, -14)
	for i := 0; i < histLen; i++ {
		m.step()
	}
	return p, q
}

func aim(p *Player, at geom.Vec3) geom.Vec3 { return at.Sub(p.Eye()).Norm() }

func TestWallsBlockShots(t *testing.T) {
	m, _, b := liveMatch(t)
	p, q := m.P[0], m.P[1]
	m.shoot(p, p.Eye(), aim(p, q.Pos.Add(geom.V(0, 1, 0))), Rifle, m.tick)
	if q.HP != MaxHP {
		t.Fatalf("shot through the temple did %d damage", MaxHP-q.HP)
	}
	if s := b.last("shot"); s == nil || s["hit"].(float64) != -1 {
		t.Fatalf("expected a miss event, got %v", s)
	}
}

func TestBodyAndHeadDamage(t *testing.T) {
	m, _, _ := liveMatch(t)
	p, q := place(m)
	if !m.World.LineOfSight(p.Eye(), q.Eye()) {
		t.Fatal("test positions should see each other")
	}
	m.shoot(p, p.Eye(), aim(p, q.Pos.Add(geom.V(0, 0.9, 0))), Rifle, m.tick)
	if q.HP != MaxHP-Weapons[Rifle].Body {
		t.Fatalf("body shot: hp %d", q.HP)
	}
	m.now += 1
	m.shoot(p, p.Eye(), aim(p, q.Pos.Add(geom.V(0, 1.6, 0))), Rifle, m.tick)
	if q.HP != MaxHP-Weapons[Rifle].Body-Weapons[Rifle].Head {
		t.Fatalf("head shot: hp %d", q.HP)
	}
}

func TestFireRateAndAmmoAreEnforced(t *testing.T) {
	m, _, _ := liveMatch(t)
	p, q := place(m)
	dir := aim(p, q.Pos.Add(geom.V(0, 0.9, 0)))
	for i := 0; i < 5; i++ { // five shots in one instant
		m.shoot(p, p.Eye(), dir, Rifle, m.tick)
	}
	if got := Weapons[Rifle].Mag - p.Mag[Rifle]; got != 1 {
		t.Fatalf("%d shots accepted in the same instant", got)
	}
	p.Mag[Rifle] = 0
	m.now += 1
	m.shoot(p, p.Eye(), dir, Rifle, m.tick)
	if q.HP != MaxHP-Weapons[Rifle].Body {
		t.Fatal("fired with an empty magazine")
	}
}

func TestShotFromWrongPlaceIsRejected(t *testing.T) {
	m, _, _ := liveMatch(t)
	p, q := place(m)
	fake := q.Eye().Add(geom.V(0, 0, -2)) // pretend to fire from beside the target
	m.shoot(p, fake, aim(p, q.Eye()), Rifle, m.tick)
	if p.Mag[Rifle] != Weapons[Rifle].Mag {
		t.Fatal("shot with a spoofed origin was accepted")
	}
}

func TestLagCompensation(t *testing.T) {
	m, _, _ := liveMatch(t)
	p, q := place(m)
	seen := m.tick
	old := q.Pos
	q.Pos = q.Pos.Add(geom.V(3, 0, 0)) // target moved after the shooter's frame
	for i := 0; i < 4; i++ {
		m.step()
	}
	m.shoot(p, p.Eye(), aim(p, old.Add(geom.V(0, 0.9, 0))), Rifle, seen)
	if q.HP == MaxHP {
		t.Fatal("shot at where the target was 4 ticks ago should hit")
	}

	// Beyond the rewind window the old position no longer counts.
	q.HP = MaxHP
	seen = m.tick
	old = q.Pos
	q.Pos = q.Pos.Add(geom.V(-3, 0, 0))
	for i := 0; i < maxRewind+6; i++ {
		m.step()
	}
	m.now += 1
	m.shoot(p, p.Eye(), aim(p, old.Add(geom.V(0, 0.9, 0))), Rifle, seen)
	if q.HP != MaxHP {
		t.Fatal("rewind beyond the limit should not hit")
	}
}

func TestImpossibleMovesAreCorrected(t *testing.T) {
	m, a, _ := liveMatch(t)
	p := m.P[0]
	start := p.Pos
	m.move(p, In{T: "move", P: [3]float64{start.X + 20, 0, start.Z}})
	if p.Pos != start {
		t.Fatal("teleport accepted")
	}
	if a.last("correct") == nil {
		t.Fatal("client was not corrected")
	}
	m.step()
	m.move(p, In{T: "move", P: [3]float64{start.X, 0, start.Z + 0.2}})
	if p.Pos.Z != start.Z+0.2 {
		t.Fatal("normal step rejected")
	}
	// Walking into the spawn cover wall is refused.
	m.step()
	m.move(p, In{T: "move", P: [3]float64{0, 0, -24.5}})
	if p.Pos.Z == -24.5 {
		t.Fatal("moved inside a wall")
	}
}

func TestBestOfFive(t *testing.T) {
	m, a, _ := liveMatch(t)
	for r := 0; r < RoundsToWin; r++ {
		for m.state != StLive {
			m.step()
		}
		p, q := place(m)
		for q.Alive {
			m.now += 1
			m.shoot(p, p.Eye(), aim(p, q.Pos.Add(geom.V(0, 1.6, 0))), Rifle, m.tick)
		}
	}
	if m.score != [2]int{3, 0} || m.state != StEnded {
		t.Fatalf("score %v state %s", m.score, m.state)
	}
	if e := a.last("end"); e == nil || e["winner"].(float64) != 0 {
		t.Fatalf("end message %v", e)
	}
	for !m.finished {
		m.step()
	}
}

func TestBotHuntsAndKillsIdlePlayer(t *testing.T) {
	a := &inbox{}
	m := newMatch(testWorld(t), "", &Player{Name: "Idle", send: a.send}, &Player{Name: "Bot", Bot: true}, 42)
	m.start()
	for i := 0; i < TickRate*90 && m.score[1] == 0; i++ {
		m.step()
	}
	if m.score[1] != 1 {
		t.Fatalf("bot failed to find and kill a stationary player in 90 s (bot at %+v)", m.P[1].Pos)
	}
}

func TestLobbyBotMatch(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()
	l := NewLobby(ctx, testWorld(t))
	got := make(chan []byte, 64)
	s := &Session{Send: func(b []byte) {
		select {
		case got <- b:
		default:
		}
	}}
	l.Handle(s, In{T: "hello", Name: "  Nafiz<script>", Mode: "bot"})
	deadline := time.After(2 * time.Second)
	for {
		select {
		case raw := <-got:
			var m msgMatch
			_ = json.Unmarshal(raw, &m)
			if m.T == "match" {
				if m.Names[0] != "Nafizscript" || !m.Bots[1] {
					t.Fatalf("match %+v", m)
				}
				return
			}
		case <-deadline:
			t.Fatal("no match message")
		}
	}
}

func TestPrivateRoom(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()
	l := NewLobby(ctx, testWorld(t))
	host, guest := &inbox{}, &inbox{}
	hs, gs := &Session{Send: host.send}, &Session{Send: guest.send}
	l.Handle(hs, In{T: "hello", Name: "Host", Mode: "private"})
	q := host.last("queue")
	if q == nil || len(q["room"].(string)) != 4 {
		t.Fatalf("no room code: %v", q)
	}
	l.Handle(gs, In{T: "hello", Name: "Guest", Mode: "private", Room: "zzzz"})
	if guest.last("error") == nil {
		t.Fatal("bad code accepted")
	}
	l.Handle(gs, In{T: "hello", Name: "Guest", Mode: "private", Room: q["room"].(string)})
	if l.Stats().Matches != 1 {
		t.Fatal("room did not start a match")
	}
}
