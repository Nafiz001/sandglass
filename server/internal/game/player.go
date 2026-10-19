package game

import (
	"github.com/Nafiz001/sandglass/server/internal/geom"
)

const histLen = 32 // ~1 s of positions at 30 Hz for lag compensation

type histEntry struct {
	tick uint32
	pos  geom.Vec3
}

// Player is one side of a match. The match goroutine owns all of its state.
type Player struct {
	Slot int
	Name string
	Bot  bool
	send func([]byte) // nil for bots

	Pos        geom.Vec3
	Yaw, Pitch float64
	HP         int
	Alive      bool
	Weapon     int
	Mag        [2]int
	Reserve    [2]int

	reloadUntil float64 // 0 when not reloading
	nextFire    float64
	lastMoveAt  float64
	strikes     int // rejected moves, decays over time

	hist [histLen]histEntry
}

func (p *Player) Send(b []byte) {
	if p.send != nil {
		p.send(b)
	}
}

func (p *Player) Eye() geom.Vec3 { return p.Pos.Add(geom.V(0, EyeHeight, 0)) }

func (p *Player) reset(pos geom.Vec3, yaw float64) {
	p.Pos, p.Yaw, p.Pitch = pos, yaw, 0
	p.HP, p.Alive = MaxHP, true
	p.Weapon = Rifle
	for i, w := range Weapons {
		p.Mag[i], p.Reserve[i] = w.Mag, w.Reserve
	}
	p.reloadUntil, p.nextFire, p.strikes = 0, 0, 0
	for i := range p.hist {
		p.hist[i] = histEntry{}
	}
}

func (p *Player) record(tick uint32) {
	p.hist[tick%histLen] = histEntry{tick: tick, pos: p.Pos}
}

// PosAt returns where the player was at a past tick, if still in history.
func (p *Player) PosAt(tick uint32) geom.Vec3 {
	if e := p.hist[tick%histLen]; e.tick == tick {
		return e.pos
	}
	return p.Pos
}

// Hitboxes: a body capsule and a head sphere, relative to the feet.
func hitHead(feet geom.Vec3) geom.Sphere {
	return geom.Sphere{C: feet.Add(geom.V(0, 1.6, 0)), R: 0.24}
}

func hitBody(feet geom.Vec3) geom.Capsule {
	return geom.Capsule{A: feet.Add(geom.V(0, 0.4, 0)), B: feet.Add(geom.V(0, 1.22, 0)), R: 0.38}
}
