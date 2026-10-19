// Package arena holds the map geometry exported from Unity (see
// Assets/Editor/ArenaExporter.cs) and answers ray, line-of-sight and
// navigation queries against it.
package arena

import (
	_ "embed"
	"encoding/json"
	"fmt"
	"math"

	"github.com/Nafiz001/sandglass/server/internal/geom"
)

//go:embed arena.json
var arenaJSON []byte

type Spawn struct {
	Pos geom.Vec3
	Yaw float64
}

// World is the static collision geometry plus an infinite ground at y=0.
type World struct {
	Boxes    []geom.OBB
	Capsules []geom.Capsule
	Spawns   []Spawn
	Bounds   float64 // play area is |x|,|z| <= Bounds
	Nav      *NavGrid
}

type fileFormat struct {
	Boxes []struct {
		C [3]float64 `json:"c"`
		H [3]float64 `json:"h"`
		Q [4]float64 `json:"q"`
	} `json:"boxes"`
	Capsules []struct {
		A [3]float64 `json:"a"`
		B [3]float64 `json:"b"`
		R float64    `json:"r"`
	} `json:"capsules"`
	Spawns []struct {
		P   [3]float64 `json:"p"`
		Yaw float64    `json:"yaw"`
	} `json:"spawns"`
	Bounds float64 `json:"bounds"`
}

func v3(a [3]float64) geom.Vec3 { return geom.V(a[0], a[1], a[2]) }

// Load parses the embedded arena and builds its navigation grid.
func Load() (*World, error) { return Parse(arenaJSON) }

func Parse(data []byte) (*World, error) {
	var f fileFormat
	if err := json.Unmarshal(data, &f); err != nil {
		return nil, fmt.Errorf("arena: %w", err)
	}
	if len(f.Spawns) < 2 {
		return nil, fmt.Errorf("arena: need 2 spawns, have %d", len(f.Spawns))
	}
	w := &World{Bounds: f.Bounds}
	for _, b := range f.Boxes {
		w.Boxes = append(w.Boxes, geom.OBB{
			Center: v3(b.C), Half: v3(b.H),
			Rot: geom.Quat{X: b.Q[0], Y: b.Q[1], Z: b.Q[2], W: b.Q[3]}.Normalized(),
		})
	}
	for _, c := range f.Capsules {
		w.Capsules = append(w.Capsules, geom.Capsule{A: v3(c.A), B: v3(c.B), R: c.R})
	}
	for _, s := range f.Spawns {
		w.Spawns = append(w.Spawns, Spawn{Pos: v3(s.P), Yaw: s.Yaw})
	}
	w.Nav = buildNav(w)
	return w, nil
}

// Raycast returns the distance to the first static surface hit by the ray
// (unit dir), or max if nothing is hit.
func (w *World) Raycast(o, d geom.Vec3, max float64) float64 {
	best := max
	if d.Y < -1e-9 && o.Y > 0 {
		if t := -o.Y / d.Y; t < best {
			best = t
		}
	}
	for i := range w.Boxes {
		if t, ok := w.Boxes[i].Raycast(o, d, best); ok && t < best {
			best = t
		}
	}
	for i := range w.Capsules {
		if t, ok := w.Capsules[i].Raycast(o, d, best); ok && t < best {
			best = t
		}
	}
	return best
}

// LineOfSight reports whether nothing static blocks the segment a-b.
func (w *World) LineOfSight(a, b geom.Vec3) bool {
	dist := a.Dist(b)
	if dist < 1e-6 {
		return true
	}
	return w.Raycast(a, b.Sub(a).Scale(1/dist), dist) >= dist-1e-3
}

// Clearance is the distance from p to the nearest static collider.
func (w *World) Clearance(p geom.Vec3) float64 {
	best := p.Y // ground
	for i := range w.Boxes {
		best = math.Min(best, w.Boxes[i].Dist(p))
	}
	for i := range w.Capsules {
		best = math.Min(best, w.Capsules[i].Dist(p))
	}
	return best
}

// FloorAt returns the height a player standing at (x, z) would rest on,
// probing down from probeTop.
func (w *World) FloorAt(x, z, probeTop float64) float64 {
	o := geom.V(x, probeTop, z)
	t := w.Raycast(o, geom.V(0, -1, 0), probeTop+1)
	return probeTop - t
}

func (w *World) InBounds(p geom.Vec3) bool {
	return math.Abs(p.X) <= w.Bounds && math.Abs(p.Z) <= w.Bounds && p.Y > -2 && p.Y < 12
}
