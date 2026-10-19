package game

import (
	"encoding/json"

	"github.com/Nafiz001/sandglass/server/internal/geom"
)

// In is every client-to-server message; T selects which fields matter.
//
//	hello  {name, mode: "quick"|"bot"|"private", room}
//	move   {seq, p, yaw, pitch}        ~30 Hz while alive
//	shoot  {o, d, w, tick}             tick = server tick the client was rendering
//	reload {}
//	switch {w}
//	leave  {}
type In struct {
	T     string     `json:"t"`
	Name  string     `json:"name,omitempty"`
	Mode  string     `json:"mode,omitempty"`
	Room  string     `json:"room,omitempty"`
	Seq   uint32     `json:"seq,omitempty"`
	P     [3]float64 `json:"p"`
	Yaw   float64    `json:"yaw"`
	Pitch float64    `json:"pitch"`
	W     int        `json:"w"`
	O     [3]float64 `json:"o"`
	D     [3]float64 `json:"d"`
	Tick  uint32     `json:"tick"`
}

func vec(a [3]float64) geom.Vec3 { return geom.V(a[0], a[1], a[2]) }

// arr rounds to centimetres to keep messages short.
func arr(v geom.Vec3) [3]float64 {
	return [3]float64{round2(v.X), round2(v.Y), round2(v.Z)}
}

func round2(f float64) float64 {
	if f < 0 {
		return float64(int64(f*100-0.5)) / 100
	}
	return float64(int64(f*100+0.5)) / 100
}

// Server-to-client messages.

type msgQueue struct {
	T    string `json:"t"` // "queue"
	Mode string `json:"mode"`
	Room string `json:"room,omitempty"`
}

type msgMatch struct {
	T     string    `json:"t"` // "match"
	You   int       `json:"you"`
	Names [2]string `json:"names"`
	Bots  [2]bool   `json:"bots"`
	Room  string    `json:"room,omitempty"`
	Note  string    `json:"note,omitempty"`
}

type msgRound struct {
	T      string `json:"t"` // "round"
	State  string `json:"state"`
	Round  int    `json:"round"`
	Score  [2]int `json:"score"`
	Winner int    `json:"winner"` // slot, -1 for none/draw
	Reason string `json:"reason,omitempty"`
}

type msgEnd struct {
	T      string `json:"t"` // "end"
	Winner int    `json:"winner"`
	Score  [2]int `json:"score"`
	Reason string `json:"reason,omitempty"`
}

type msgCorrect struct {
	T   string     `json:"t"` // "correct"
	P   [3]float64 `json:"p"`
	Yaw float64    `json:"yaw"`
}

type msgShot struct {
	T    string     `json:"t"` // "shot"
	By   int        `json:"by"`
	W    int        `json:"w"`
	O    [3]float64 `json:"o"`
	E    [3]float64 `json:"e"`
	Hit  int        `json:"hit"` // slot hit, -1 for none
	Head bool       `json:"head,omitempty"`
	Dmg  int        `json:"dmg,omitempty"`
}

type snapPlayer struct {
	P     [3]float64 `json:"p"`
	Yaw   float64    `json:"yaw"`
	Pitch float64    `json:"pitch"`
	HP    int        `json:"hp"`
	W     int        `json:"w"`
	Alive bool       `json:"alive"`
	Rl    bool       `json:"rl,omitempty"`
}

type msgSnap struct {
	T       string        `json:"t"` // "snap"
	Tick    uint32        `json:"tick"`
	State   string        `json:"st"`
	Left    float64       `json:"left"` // seconds left in the current state
	Players [2]snapPlayer `json:"pl"`
	Mag     int           `json:"mag"`
	Res     int           `json:"res"`
}

type msgError struct {
	T   string `json:"t"` // "error"
	Msg string `json:"msg"`
}

func encode(v any) []byte {
	b, _ := json.Marshal(v)
	return b
}
