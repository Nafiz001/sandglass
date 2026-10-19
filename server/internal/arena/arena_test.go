package arena

import (
	"testing"

	"github.com/Nafiz001/sandglass/server/internal/geom"
)

func load(t *testing.T) *World {
	t.Helper()
	w, err := Load()
	if err != nil {
		t.Fatal(err)
	}
	return w
}

func TestLoad(t *testing.T) {
	w := load(t)
	if len(w.Boxes) < 100 || len(w.Capsules) < 10 {
		t.Fatalf("too little geometry: %d boxes %d capsules", len(w.Boxes), len(w.Capsules))
	}
	if w.Spawns[0].Pos.Z > 0 || w.Spawns[1].Pos.Z < 0 {
		t.Fatalf("spawns not sorted south to north: %+v", w.Spawns)
	}
}

func TestTempleBlocksSightBetweenSpawns(t *testing.T) {
	w := load(t)
	eye := geom.V(0, 1.6, 0)
	a, b := w.Spawns[0].Pos.Add(eye), w.Spawns[1].Pos.Add(eye)
	if w.LineOfSight(a, b) {
		t.Fatal("spawns should not see each other through the temple")
	}
	// The temple platform top is 1 m up.
	if f := w.FloorAt(0, 3, 1.4); f < 0.99 || f > 1.1 {
		t.Fatalf("platform floor %v", f)
	}
}

func TestNavReachesEverywhereThatMatters(t *testing.T) {
	w := load(t)
	g := w.Nav
	if c := g.WalkableCount(); c < 1500 {
		t.Fatalf("only %d walkable cells", c)
	}
	south, north := w.Spawns[0].Pos, w.Spawns[1].Pos
	if p := g.Path(south, north); len(p) == 0 {
		t.Fatal("no path between spawns")
	}
	top := g.Path(south, geom.V(0, 1, 3))
	if len(top) == 0 {
		t.Fatal("no path onto the temple platform")
	}
	if last := top[len(top)-1]; last.Y < 0.9 {
		t.Fatalf("path ends at floor %v, not on the platform", last.Y)
	}
}
