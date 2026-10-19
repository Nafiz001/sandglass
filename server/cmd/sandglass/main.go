// Command sandglass is the authoritative match server for Sandglass, a 1v1
// arena shooter. Clients connect over WebSocket at /ws.
package main

import (
	"context"
	"encoding/json"
	"errors"
	"flag"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"sync/atomic"
	"syscall"
	"time"

	"github.com/coder/websocket"

	"github.com/Nafiz001/sandglass/server/internal/arena"
	"github.com/Nafiz001/sandglass/server/internal/game"
)

const (
	maxClients    = 200
	maxMsgSize    = 2048
	maxMsgsPerSec = 120
	sendBuffer    = 128
)

func main() {
	addr := flag.String("addr", envOr("SANDGLASS_ADDR", ":7350"), "listen address")
	flag.Parse()

	w, err := arena.Load()
	if err != nil {
		slog.Error("load arena", "err", err)
		os.Exit(1)
	}
	slog.Info("arena loaded", "boxes", len(w.Boxes), "capsules", len(w.Capsules), "walkable", w.Nav.WalkableCount())

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()
	lobby := game.NewLobby(ctx, w)

	mux := http.NewServeMux()
	mux.HandleFunc("GET /healthz", func(w http.ResponseWriter, _ *http.Request) { w.Write([]byte("ok")) })
	mux.HandleFunc("GET /stats", func(rw http.ResponseWriter, _ *http.Request) {
		rw.Header().Set("Content-Type", "application/json")
		rw.Header().Set("Access-Control-Allow-Origin", "*")
		json.NewEncoder(rw).Encode(lobby.Stats())
	})
	var clients atomic.Int64
	mux.HandleFunc("GET /ws", func(rw http.ResponseWriter, r *http.Request) {
		if clients.Load() >= maxClients {
			http.Error(rw, "server full", http.StatusServiceUnavailable)
			return
		}
		c, err := websocket.Accept(rw, r, &websocket.AcceptOptions{
			// The WebGL client is served from another origin (Vercel).
			InsecureSkipVerify: true,
		})
		if err != nil {
			return
		}
		clients.Add(1)
		defer clients.Add(-1)
		serve(ctx, lobby, c)
	})

	srv := &http.Server{Addr: *addr, Handler: mux, ReadHeaderTimeout: 5 * time.Second}
	go func() {
		<-ctx.Done()
		shutdown, cancel := context.WithTimeout(context.Background(), 3*time.Second)
		defer cancel()
		srv.Shutdown(shutdown)
	}()
	slog.Info("listening", "addr", *addr)
	if err := srv.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
		slog.Error("serve", "err", err)
		os.Exit(1)
	}
}

// serve pumps one client's messages between its socket and the lobby.
func serve(ctx context.Context, lobby *game.Lobby, c *websocket.Conn) {
	ctx, cancel := context.WithCancel(ctx)
	defer cancel()
	c.SetReadLimit(maxMsgSize)

	out := make(chan []byte, sendBuffer)
	s := &game.Session{Send: func(b []byte) {
		select {
		case out <- b:
		default:
			cancel() // a client this far behind is gone; drop it
		}
	}}
	lobby.Connected()
	defer lobby.Disconnected()
	defer lobby.Leave(s)

	go func() {
		defer cancel()
		for {
			select {
			case <-ctx.Done():
				return
			case b := <-out:
				wctx, wcancel := context.WithTimeout(ctx, 5*time.Second)
				err := c.Write(wctx, websocket.MessageText, b)
				wcancel()
				if err != nil {
					return
				}
			}
		}
	}()

	window, count := time.Now(), 0
	for {
		_, data, err := c.Read(ctx)
		if err != nil {
			c.CloseNow()
			return
		}
		if now := time.Now(); now.Sub(window) >= time.Second {
			window, count = now, 0
		}
		if count++; count > maxMsgsPerSec {
			c.Close(websocket.StatusPolicyViolation, "too many messages")
			return
		}
		var msg game.In
		if json.Unmarshal(data, &msg) != nil {
			continue
		}
		lobby.Handle(s, msg)
	}
}

func envOr(key, def string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return def
}
