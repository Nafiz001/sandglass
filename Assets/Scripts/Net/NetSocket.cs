using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sandglass.Net
{
    /// <summary>
    /// A text WebSocket that is polled from the main thread. WebGL uses the
    /// browser's WebSocket through SandglassSocket.jslib; everything else
    /// uses System.Net.WebSockets.
    /// </summary>
    public sealed class NetSocket
    {
        public enum Status { Idle, Connecting, Open, Closed }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SgSocketConnect(string url);
        [DllImport("__Internal")] static extern void SgSocketSend(string msg);
        [DllImport("__Internal")] static extern void SgSocketClose();
        [DllImport("__Internal")] static extern int SgSocketState();
        [DllImport("__Internal")] static extern string SgSocketPoll();
        [DllImport("__Internal")] static extern string SgPageQuery(string key);

        bool started;

        public Status State
        {
            get
            {
                if (!started) return Status.Idle;
                switch (SgSocketState())
                {
                    case 0: return Status.Connecting;
                    case 1: return Status.Open;
                    default: return Status.Closed;
                }
            }
        }

        public void Connect(string url) { started = true; SgSocketConnect(url); }
        public void Send(string msg) => SgSocketSend(msg);
        public void Close() { if (started) SgSocketClose(); }

        public bool TryReceive(out string msg)
        {
            msg = SgSocketPoll();
            return msg != null;
        }

        public static string PageQuery(string key) => SgPageQuery(key);
#else
        System.Net.WebSockets.ClientWebSocket ws;
        CancellationTokenSource cts;
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> outbox = new ConcurrentQueue<string>();
        readonly SemaphoreSlim outSignal = new SemaphoreSlim(0);
        volatile Status state = Status.Idle;

        public Status State => state;

        public void Connect(string url)
        {
            Close();
            while (inbox.TryDequeue(out _)) { }
            while (outbox.TryDequeue(out _)) { }
            state = Status.Connecting;
            cts = new CancellationTokenSource();
            ws = new System.Net.WebSockets.ClientWebSocket();
            var socket = ws;
            var token = cts.Token;
            Task.Run(async () =>
            {
                try
                {
                    await socket.ConnectAsync(new Uri(url), token);
                    state = Status.Open;
                    _ = SendLoop(socket, token);
                    await ReceiveLoop(socket, token);
                }
                catch (Exception) { }
                state = Status.Closed;
            });
        }

        async Task ReceiveLoop(System.Net.WebSockets.ClientWebSocket socket, CancellationToken token)
        {
            var buffer = new byte[16 * 1024];
            var sb = new StringBuilder();
            while (!token.IsCancellationRequested && socket.State == System.Net.WebSockets.WebSocketState.Open)
            {
                var r = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (r.MessageType == System.Net.WebSockets.WebSocketMessageType.Close) break;
                sb.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
                if (r.EndOfMessage)
                {
                    inbox.Enqueue(sb.ToString());
                    sb.Clear();
                }
            }
        }

        async Task SendLoop(System.Net.WebSockets.ClientWebSocket socket, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await outSignal.WaitAsync(token);
                    while (outbox.TryDequeue(out var msg))
                    {
                        var bytes = Encoding.UTF8.GetBytes(msg);
                        await socket.SendAsync(new ArraySegment<byte>(bytes), System.Net.WebSockets.WebSocketMessageType.Text, true, token);
                    }
                }
            }
            catch (Exception) { state = Status.Closed; }
        }

        public void Send(string msg)
        {
            if (state != Status.Open) return;
            outbox.Enqueue(msg);
            outSignal.Release();
        }

        public void Close()
        {
            if (cts == null) return;
            cts.Cancel();
            try { ws?.Abort(); } catch (Exception) { }
            ws = null;
            cts = null;
            state = Status.Closed;
        }

        public bool TryReceive(out string msg) => inbox.TryDequeue(out msg);

        public static string PageQuery(string key) => "";
#endif
    }
}
