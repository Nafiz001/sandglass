// Package geom has the small amount of 3D maths the match server needs:
// vectors, quaternions, and ray/point queries against boxes, capsules and
// spheres. Coordinates follow Unity: left-handed, Y up, metres.
package geom

import "math"

type Vec3 struct{ X, Y, Z float64 }

func V(x, y, z float64) Vec3 { return Vec3{x, y, z} }

func (a Vec3) Add(b Vec3) Vec3             { return Vec3{a.X + b.X, a.Y + b.Y, a.Z + b.Z} }
func (a Vec3) Sub(b Vec3) Vec3             { return Vec3{a.X - b.X, a.Y - b.Y, a.Z - b.Z} }
func (a Vec3) Scale(s float64) Vec3        { return Vec3{a.X * s, a.Y * s, a.Z * s} }
func (a Vec3) Dot(b Vec3) float64          { return a.X*b.X + a.Y*b.Y + a.Z*b.Z }
func (a Vec3) Len() float64                { return math.Sqrt(a.Dot(a)) }
func (a Vec3) Dist(b Vec3) float64         { return a.Sub(b).Len() }
func (a Vec3) Lerp(b Vec3, t float64) Vec3 { return a.Add(b.Sub(a).Scale(t)) }

func (a Vec3) Cross(b Vec3) Vec3 {
	return Vec3{a.Y*b.Z - a.Z*b.Y, a.Z*b.X - a.X*b.Z, a.X*b.Y - a.Y*b.X}
}

func (a Vec3) Norm() Vec3 {
	l := a.Len()
	if l < 1e-9 {
		return Vec3{}
	}
	return a.Scale(1 / l)
}

func (a Vec3) Finite() bool {
	for _, f := range [3]float64{a.X, a.Y, a.Z} {
		if math.IsNaN(f) || math.IsInf(f, 0) {
			return false
		}
	}
	return true
}

// HorizDist is the distance ignoring height.
func (a Vec3) HorizDist(b Vec3) float64 { return math.Hypot(a.X-b.X, a.Z-b.Z) }

type Quat struct{ X, Y, Z, W float64 }

var Identity = Quat{0, 0, 0, 1}

func (q Quat) Conj() Quat { return Quat{-q.X, -q.Y, -q.Z, q.W} }

func (q Quat) Normalized() Quat {
	l := math.Sqrt(q.X*q.X + q.Y*q.Y + q.Z*q.Z + q.W*q.W)
	if l < 1e-9 {
		return Identity
	}
	return Quat{q.X / l, q.Y / l, q.Z / l, q.W / l}
}

// Rotate applies q to v (v' = q v q*).
func (q Quat) Rotate(v Vec3) Vec3 {
	u := Vec3{q.X, q.Y, q.Z}
	t := u.Cross(v).Scale(2)
	return v.Add(t.Scale(q.W)).Add(u.Cross(t))
}

// DirFromAngles converts Unity yaw/pitch in degrees (pitch positive looks
// down, as Transform.eulerAngles.x does) to a unit forward vector.
func DirFromAngles(yaw, pitch float64) Vec3 {
	y, p := yaw*math.Pi/180, pitch*math.Pi/180
	return Vec3{math.Sin(y) * math.Cos(p), -math.Sin(p), math.Cos(y) * math.Cos(p)}
}

// YawTo returns the Unity yaw in degrees that faces from a towards b.
func YawTo(a, b Vec3) float64 {
	return math.Atan2(b.X-a.X, b.Z-a.Z) * 180 / math.Pi
}

// OBB is an oriented box.
type OBB struct {
	Center Vec3
	Half   Vec3
	Rot    Quat
}

func (b OBB) toLocal(p Vec3) Vec3 { return b.Rot.Conj().Rotate(p.Sub(b.Center)) }

// Raycast returns the distance along unit dir d to the first hit, if any
// within max. A ray starting inside the box reports no hit.
func (b OBB) Raycast(o, d Vec3, max float64) (float64, bool) {
	lo := b.toLocal(o)
	ld := b.Rot.Conj().Rotate(d)
	tmin, tmax := 0.0, max
	for i := 0; i < 3; i++ {
		var oc, dc, h float64
		switch i {
		case 0:
			oc, dc, h = lo.X, ld.X, b.Half.X
		case 1:
			oc, dc, h = lo.Y, ld.Y, b.Half.Y
		default:
			oc, dc, h = lo.Z, ld.Z, b.Half.Z
		}
		if math.Abs(dc) < 1e-12 {
			if oc < -h || oc > h {
				return 0, false
			}
			continue
		}
		t1, t2 := (-h-oc)/dc, (h-oc)/dc
		if t1 > t2 {
			t1, t2 = t2, t1
		}
		tmin = math.Max(tmin, t1)
		tmax = math.Min(tmax, t2)
		if tmin > tmax {
			return 0, false
		}
	}
	if tmin <= 0 {
		return 0, false
	}
	return tmin, true
}

// Dist is the distance from p to the box surface (0 inside).
func (b OBB) Dist(p Vec3) float64 {
	l := b.toLocal(p)
	dx := math.Max(math.Abs(l.X)-b.Half.X, 0)
	dy := math.Max(math.Abs(l.Y)-b.Half.Y, 0)
	dz := math.Max(math.Abs(l.Z)-b.Half.Z, 0)
	return math.Sqrt(dx*dx + dy*dy + dz*dz)
}

// Capsule is a segment A-B swept by radius R.
type Capsule struct {
	A, B Vec3
	R    float64
}

// Raycast intersects a ray with the capsule (after Inigo Quilez).
func (c Capsule) Raycast(o, d Vec3, max float64) (float64, bool) {
	ba := c.B.Sub(c.A)
	oa := o.Sub(c.A)
	baba := ba.Dot(ba)
	if baba < 1e-12 {
		return Sphere{c.A, c.R}.Raycast(o, d, max)
	}
	bard := ba.Dot(d)
	baoa := ba.Dot(oa)
	rdoa := d.Dot(oa)
	oaoa := oa.Dot(oa)
	a := baba - bard*bard
	if a > 1e-9 {
		b := baba*rdoa - baoa*bard
		cc := baba*oaoa - baoa*baoa - c.R*c.R*baba
		h := b*b - a*cc
		if h < 0 {
			return 0, false
		}
		t := (-b - math.Sqrt(h)) / a
		y := baoa + t*bard
		if y > 0 && y < baba {
			if t > 0 && t <= max {
				return t, true
			}
			return 0, false
		}
	}
	// End caps: nearest of the two spheres.
	t1, ok1 := Sphere{c.A, c.R}.Raycast(o, d, max)
	t2, ok2 := Sphere{c.B, c.R}.Raycast(o, d, max)
	switch {
	case ok1 && ok2:
		return math.Min(t1, t2), true
	case ok1:
		return t1, true
	case ok2:
		return t2, true
	}
	return 0, false
}

func (c Capsule) Dist(p Vec3) float64 {
	ba := c.B.Sub(c.A)
	t := 0.0
	if l := ba.Dot(ba); l > 1e-12 {
		t = math.Max(0, math.Min(1, p.Sub(c.A).Dot(ba)/l))
	}
	return math.Max(p.Dist(c.A.Add(ba.Scale(t)))-c.R, 0)
}

type Sphere struct {
	C Vec3
	R float64
}

func (s Sphere) Raycast(o, d Vec3, max float64) (float64, bool) {
	oc := o.Sub(s.C)
	b := oc.Dot(d)
	c := oc.Dot(oc) - s.R*s.R
	h := b*b - c
	if h < 0 {
		return 0, false
	}
	t := -b - math.Sqrt(h)
	if t <= 0 || t > max {
		return 0, false
	}
	return t, true
}
