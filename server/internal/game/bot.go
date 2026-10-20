package game

import (
	"math"

	"github.com/Nafiz001/sandglass/server/internal/geom"
)

const (
	botSpeed       = 4.6
	botTurnRate    = 220.0 // degrees per second
	botReaction    = 0.7   // seconds from first sight to first shot
	botAimSigma    = 4.0   // degrees of aim error (one standard deviation)
	botRepathEvery = 1.0
	botPushRange   = 13.0 // beyond this the bot closes in while it fires
)

// Bot drives a Player on the server. It plays by the same rules as a human:
// it moves at a capped speed, needs line of sight, and its shots go through
// Match.shoot with the same validation, so it cannot hit through walls.
type Bot struct {
	m *Match
	p *Player

	path      []geom.Vec3
	repathAt  float64
	seenSince float64 // -1 while the target is not visible
	lastSeen  geom.Vec3
	lastSeenT float64
	strafe    geom.Vec3
	strafeAt  float64
	burstLeft int
	pauseTill float64
}

func newBot(m *Match, p *Player) *Bot { return &Bot{m: m, p: p} }

func (b *Bot) reset() {
	b.path, b.repathAt, b.seenSince, b.lastSeenT = nil, 0, -1, -100
	b.strafeAt, b.burstLeft, b.pauseTill = 0, 0, 0
}

func (b *Bot) think() {
	m, p := b.m, b.p
	if !p.Alive {
		return
	}
	t := m.P[1-p.Slot]
	visible, aimAt := false, geom.Vec3{}
	if t.Alive {
		chest, head := t.Pos.Add(geom.V(0, 1.1, 0)), t.Pos.Add(geom.V(0, 1.6, 0))
		switch {
		case m.World.LineOfSight(p.Eye(), chest):
			visible, aimAt = true, chest
		case m.World.LineOfSight(p.Eye(), head):
			visible, aimAt = true, head
		}
	}

	if visible {
		if b.seenSince < 0 {
			b.seenSince = m.now
			b.burstLeft = 3 + m.rng.IntN(3)
		}
		b.lastSeen, b.lastSeenT = t.Pos, m.now
		b.turnTowards(aimAt)
		b.fight(t, aimAt, aimAt.Y-t.Pos.Y < 1.5)
		return
	}

	b.seenSince = -1
	if p.Mag[p.Weapon] < Weapons[p.Weapon].Mag/2 {
		m.reload(p)
	}
	if m.now >= b.repathAt || len(b.path) == 0 {
		goal := t.Pos // hunt: head for the opponent
		if m.now-b.lastSeenT < 4 {
			goal = b.lastSeen // check where they were last seen first
		}
		b.path = m.World.Nav.Path(p.Pos, goal)
		b.repathAt = m.now + botRepathEvery
	}
	if len(b.path) > 0 {
		next := b.path[0]
		if b.walk(next) {
			b.path = b.path[1:]
		}
		look := next
		if len(b.path) > 1 {
			look = b.path[1]
		}
		b.turnTowards(look.Add(geom.V(0, EyeHeight, 0)))
	}
}

// fight fires controlled bursts. Up close with the target's body in view
// it strafes between nearby cells; when the target is far off or only its
// head shows over cover, it keeps walking in so the fight cannot stall.
func (b *Bot) fight(t *Player, aimAt geom.Vec3, bodyVisible bool) {
	m, p := b.m, b.p
	if !bodyVisible || p.Pos.HorizDist(t.Pos) > botPushRange {
		if m.now >= b.repathAt || len(b.path) == 0 {
			b.path = m.World.Nav.Path(p.Pos, t.Pos)
			b.repathAt = m.now + botRepathEvery
		}
		if len(b.path) > 0 && b.walk(b.path[0]) {
			b.path = b.path[1:]
		}
	} else if m.now >= b.strafeAt || len(b.path) > 0 {
		b.path = nil
		b.strafe = m.World.Nav.RandomWalkable(p.Pos, 2.5, m.rng)
		knee := geom.V(0, 0.5, 0)
		if !m.World.LineOfSight(p.Pos.Add(knee), b.strafe.Add(knee)) {
			b.strafe = p.Pos // would walk through cover: hold still instead
		}
		b.strafeAt = m.now + 0.6 + m.rng.Float64()*0.6
	}
	if len(b.path) == 0 {
		b.walk(b.strafe)
	}

	if p.Mag[p.Weapon] == 0 {
		if p.Reserve[p.Weapon] == 0 && p.Weapon == Rifle {
			m.switchWeapon(p, Pistol)
		} else {
			m.reload(p)
		}
		return
	}
	if m.now-b.seenSince < botReaction || m.now < b.pauseTill || m.now < p.nextFire {
		return
	}
	if m.rng.Float64() < 0.2 {
		aimAt = t.Pos.Add(geom.V(0, 1.6, 0))
	}
	dir := aimAt.Sub(p.Eye()).Norm()
	dir = jitter(dir, botAimSigma*math.Pi/180, m.rng.NormFloat64(), m.rng.NormFloat64())
	m.shoot(p, p.Eye(), dir, p.Weapon, m.tick)
	b.burstLeft--
	if b.burstLeft <= 0 {
		b.pauseTill = m.now + 0.5 + m.rng.Float64()*0.4
		b.burstLeft = 3 + m.rng.IntN(3)
	}
}

// walk moves towards target at bot speed and reports arrival.
func (b *Bot) walk(target geom.Vec3) bool {
	p := b.p
	d := geom.V(target.X-p.Pos.X, 0, target.Z-p.Pos.Z)
	dist := d.Len()
	stepLen := botSpeed * dt
	if dist <= stepLen {
		p.Pos = target
		return true
	}
	next := p.Pos.Add(d.Scale(stepLen / dist))
	// Ease height towards the target cell's floor (stairs, ledges).
	next.Y = p.Pos.Y + (target.Y-p.Pos.Y)*math.Min(1, stepLen/dist*1.5)
	p.Pos = next
	return false
}

func (b *Bot) turnTowards(point geom.Vec3) {
	p := b.p
	eye := p.Eye()
	wantYaw := geom.YawTo(eye, point)
	flat := math.Hypot(point.X-eye.X, point.Z-eye.Z)
	wantPitch := -math.Atan2(point.Y-eye.Y, flat) * 180 / math.Pi
	maxStep := botTurnRate * dt
	diff := math.Mod(wantYaw-p.Yaw+540, 360) - 180
	p.Yaw += math.Max(-maxStep, math.Min(maxStep, diff))
	p.Pitch += math.Max(-maxStep, math.Min(maxStep, wantPitch-p.Pitch))
}

// jitter rotates dir by a small random angle (n1, n2 standard normals).
func jitter(dir geom.Vec3, sigma, n1, n2 float64) geom.Vec3 {
	up := geom.V(0, 1, 0)
	right := up.Cross(dir).Norm()
	if right.Len() == 0 {
		right = geom.V(1, 0, 0)
	}
	realUp := dir.Cross(right).Norm()
	return dir.Add(right.Scale(math.Tan(sigma * n1))).Add(realUp.Scale(math.Tan(sigma * n2))).Norm()
}
