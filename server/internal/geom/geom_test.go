package geom

import (
	"math"
	"testing"
)

func near(a, b float64) bool { return math.Abs(a-b) < 1e-6 }

func TestRotateMatchesUnityYaw(t *testing.T) {
	// 90 degrees about Y turns +Z into +X in Unity's left-handed frame.
	s := math.Sin(math.Pi / 4)
	q := Quat{0, s, 0, s}
	got := q.Rotate(V(0, 0, 1))
	if !near(got.X, 1) || !near(got.Z, 0) {
		t.Fatalf("rotate: %+v", got)
	}
	d := DirFromAngles(90, 0)
	if !near(d.X, 1) || !near(d.Z, 0) {
		t.Fatalf("dir: %+v", d)
	}
	if d := DirFromAngles(0, 90); !near(d.Y, -1) {
		t.Fatalf("pitch down: %+v", d)
	}
	if y := YawTo(V(0, 0, 0), V(1, 0, 0)); !near(y, 90) {
		t.Fatalf("yawto: %v", y)
	}
}

func TestOBBRaycast(t *testing.T) {
	b := OBB{Center: V(0, 0, 5), Half: V(1, 1, 1), Rot: Identity}
	if tt, ok := b.Raycast(V(0, 0, 0), V(0, 0, 1), 100); !ok || !near(tt, 4) {
		t.Fatalf("hit: %v %v", tt, ok)
	}
	if _, ok := b.Raycast(V(0, 2, 0), V(0, 0, 1), 100); ok {
		t.Fatal("ray above box should miss")
	}
	if _, ok := b.Raycast(V(0, 0, 0), V(0, 0, 1), 3); ok {
		t.Fatal("hit beyond max")
	}
	// Rotated 45 degrees: the corner now sticks out to sqrt(2).
	s := math.Sin(math.Pi / 8)
	r := OBB{Center: V(0, 0, 5), Half: V(1, 1, 1), Rot: Quat{0, s, 0, math.Cos(math.Pi / 8)}}
	if tt, ok := r.Raycast(V(0, 0, 0), V(0, 0, 1), 100); !ok || !near(tt, 5-math.Sqrt2) {
		t.Fatalf("rotated hit: %v %v", tt, ok)
	}
	if d := b.Dist(V(0, 0, 2)); !near(d, 2) {
		t.Fatalf("dist %v", d)
	}
}

func TestCapsuleRaycast(t *testing.T) {
	c := Capsule{A: V(0, 0, 5), B: V(0, 2, 5), R: 0.5}
	if tt, ok := c.Raycast(V(0, 1, 0), V(0, 0, 1), 100); !ok || !near(tt, 4.5) {
		t.Fatalf("body: %v %v", tt, ok)
	}
	// Straight down onto the top cap.
	if tt, ok := c.Raycast(V(0, 10, 5), V(0, -1, 0), 100); !ok || !near(tt, 7.5) {
		t.Fatalf("cap: %v %v", tt, ok)
	}
	if _, ok := c.Raycast(V(1, 1, 0), V(0, 0, 1), 100); ok {
		t.Fatal("should miss to the side")
	}
	if d := c.Dist(V(2, 1, 5)); !near(d, 1.5) {
		t.Fatalf("dist %v", d)
	}
}

func TestSphereRaycast(t *testing.T) {
	s := Sphere{V(0, 0, 10), 1}
	if tt, ok := s.Raycast(V(0, 0, 0), V(0, 0, 1), 100); !ok || !near(tt, 9) {
		t.Fatalf("%v %v", tt, ok)
	}
}
