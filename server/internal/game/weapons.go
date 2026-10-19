package game

// Weapon stats are server-owned; the client only mirrors them for feel.
type Weapon struct {
	Name     string
	Body     int
	Head     int
	Interval float64 // seconds between shots
	Mag      int
	Reserve  int
	Reload   float64 // seconds
	Range    float64
}

const (
	Rifle  = 0
	Pistol = 1
)

var Weapons = [2]Weapon{
	Rifle:  {Name: "rifle", Body: 24, Head: 60, Interval: 0.1, Mag: 30, Reserve: 90, Reload: 2.2, Range: 180},
	Pistol: {Name: "pistol", Body: 32, Head: 80, Interval: 0.22, Mag: 12, Reserve: 48, Reload: 1.5, Range: 120},
}

const (
	MaxHP       = 100
	EyeHeight   = 1.6
	switchDelay = 0.3
)
