package arena

import (
	"container/heap"
	"math"
	"math/rand/v2"

	"github.com/Nafiz001/sandglass/server/internal/geom"
)

const (
	cellSize = 1.0
	maxStep  = 0.5 // highest rise a walker climbs between cells
	maxDrop  = 1.3 // highest ledge it steps off
)

// NavGrid is a 1 m walkability grid over the play area, built once from
// the colliders. Each walkable cell knows its floor height.
type NavGrid struct {
	N      int // cells per side
	Origin float64
	Walk   []bool
	Floor  []float64
}

func buildNav(w *World) *NavGrid {
	half := math.Floor(w.Bounds - 0.5)
	n := int(half*2/cellSize) + 1
	g := &NavGrid{N: n, Origin: -half, Walk: make([]bool, n*n), Floor: make([]float64, n*n)}
	for iz := 0; iz < n; iz++ {
		for ix := 0; ix < n; ix++ {
			x, z := g.Origin+float64(ix)*cellSize, g.Origin+float64(iz)*cellSize
			floor := w.FloorAt(x, z, 1.4)
			i := iz*n + ix
			g.Floor[i] = floor
			if floor > 1.15 {
				continue
			}
			g.Walk[i] = w.Clearance(geom.V(x, floor+0.75, z)) >= 0.3 &&
				w.Clearance(geom.V(x, floor+1.3, z)) >= 0.4 &&
				w.Clearance(geom.V(x, floor+1.75, z)) >= 0.4
		}
	}
	return g
}

func (g *NavGrid) pos(i int) geom.Vec3 {
	return geom.V(g.Origin+float64(i%g.N)*cellSize, g.Floor[i], g.Origin+float64(i/g.N)*cellSize)
}

func (g *NavGrid) index(x, z float64) (int, bool) {
	ix := int(math.Round((x - g.Origin) / cellSize))
	iz := int(math.Round((z - g.Origin) / cellSize))
	if ix < 0 || iz < 0 || ix >= g.N || iz >= g.N {
		return 0, false
	}
	return iz*g.N + ix, true
}

// Nearest returns the walkable cell closest to p, searching a few cells out.
func (g *NavGrid) Nearest(p geom.Vec3) (int, bool) {
	best, bestD := -1, math.MaxFloat64
	ix := int(math.Round((p.X - g.Origin) / cellSize))
	iz := int(math.Round((p.Z - g.Origin) / cellSize))
	for dz := -4; dz <= 4; dz++ {
		for dx := -4; dx <= 4; dx++ {
			x, z := ix+dx, iz+dz
			if x < 0 || z < 0 || x >= g.N || z >= g.N {
				continue
			}
			i := z*g.N + x
			if !g.Walk[i] {
				continue
			}
			if d := g.pos(i).Dist(p); d < bestD {
				best, bestD = i, d
			}
		}
	}
	return best, best >= 0
}

func (g *NavGrid) canMove(a, b int) bool {
	if !g.Walk[b] {
		return false
	}
	dh := g.Floor[b] - g.Floor[a]
	return dh <= maxStep && -dh <= maxDrop
}

var neighbours = [8][2]int{{1, 0}, {-1, 0}, {0, 1}, {0, -1}, {1, 1}, {1, -1}, {-1, 1}, {-1, -1}}

func (g *NavGrid) each(i int, fn func(j int, cost float64)) {
	x, z := i%g.N, i/g.N
	for _, d := range neighbours {
		nx, nz := x+d[0], z+d[1]
		if nx < 0 || nz < 0 || nx >= g.N || nz >= g.N {
			continue
		}
		j := nz*g.N + nx
		if !g.canMove(i, j) {
			continue
		}
		cost := 1.0
		if d[0] != 0 && d[1] != 0 {
			// No corner cutting past blocked cells.
			if !g.canMove(i, z*g.N+nx) || !g.canMove(i, nz*g.N+x) {
				continue
			}
			cost = math.Sqrt2
		}
		fn(j, cost)
	}
}

// Path returns waypoints (cell centres at floor height) from a to b, or nil
// if b is unreachable.
func (g *NavGrid) Path(a, b geom.Vec3) []geom.Vec3 {
	start, ok1 := g.Nearest(a)
	goal, ok2 := g.Nearest(b)
	if !ok1 || !ok2 {
		return nil
	}
	if start == goal {
		return []geom.Vec3{g.pos(goal)}
	}
	gs := map[int]float64{start: 0}
	from := map[int]int{}
	gp := g.pos(goal)
	open := &pq{{start, gp.HorizDist(g.pos(start))}}
	closed := map[int]bool{}
	for open.Len() > 0 {
		cur := heap.Pop(open).(item).i
		if cur == goal {
			var path []geom.Vec3
			for c := goal; c != start; c = from[c] {
				path = append(path, g.pos(c))
			}
			for l, r := 0, len(path)-1; l < r; l, r = l+1, r-1 {
				path[l], path[r] = path[r], path[l]
			}
			return path
		}
		if closed[cur] {
			continue
		}
		closed[cur] = true
		g.each(cur, func(j int, cost float64) {
			ng := gs[cur] + cost
			if old, seen := gs[j]; seen && ng >= old {
				return
			}
			gs[j] = ng
			from[j] = cur
			heap.Push(open, item{j, ng + gp.HorizDist(g.pos(j))})
		})
	}
	return nil
}

// RandomWalkable picks a walkable cell within r of p (for strafing and
// wandering), falling back to p's nearest cell.
func (g *NavGrid) RandomWalkable(p geom.Vec3, r float64, rng *rand.Rand) geom.Vec3 {
	for range 24 {
		q := geom.V(p.X+(rng.Float64()*2-1)*r, p.Y, p.Z+(rng.Float64()*2-1)*r)
		if i, ok := g.index(q.X, q.Z); ok && g.Walk[i] && math.Abs(g.Floor[i]-p.Y) <= maxStep {
			return g.pos(i)
		}
	}
	if i, ok := g.Nearest(p); ok {
		return g.pos(i)
	}
	return p
}

// WalkableCount is used by tests and the stats endpoint.
func (g *NavGrid) WalkableCount() int {
	n := 0
	for _, w := range g.Walk {
		if w {
			n++
		}
	}
	return n
}

type item struct {
	i int
	f float64
}
type pq []item

func (q pq) Len() int           { return len(q) }
func (q pq) Less(a, b int) bool { return q[a].f < q[b].f }
func (q pq) Swap(a, b int)      { q[a], q[b] = q[b], q[a] }
func (q *pq) Push(x any)        { *q = append(*q, x.(item)) }
func (q *pq) Pop() any          { o := *q; it := o[len(o)-1]; *q = o[:len(o)-1]; return it }
