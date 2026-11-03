using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Orbital.Core.Net
{
    public enum TransportState
    {
        Idle,
        Connecting,
        Open,
        /// <summary>Connection failed or dropped; <see cref="ITransport.Error"/> says why. A new transport is made for the next attempt.</summary>
        Closed,
    }

    /// <summary>
    /// A text message link to the server (stage 2c.5). Polled from one thread (the game loop): <see cref="Send"/> queues, <see cref="TryReceive"/>
    /// takes what arrived. The network runs on background tasks. One instance = one connection attempt.
    /// </summary>
    public interface ITransport : IDisposable
    {
        TransportState State { get; }
        string Error { get; }
        void Connect(string url);
        void Send(string text);
        bool TryReceive(out string text);
    }

    /// <summary><see cref="ITransport"/> over <see cref="ClientWebSocket"/>: keep-alive 15 s, a receive loop and a send loop in the background.</summary>
    public sealed class WsTransport : ITransport
    {
        public static readonly TimeSpan KeepAlive = TimeSpan.FromSeconds(15);
        public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

        private readonly ClientWebSocket _ws = new ClientWebSocket();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ConcurrentQueue<string> _in = new ConcurrentQueue<string>();
        private readonly BlockingCollection<string> _out = new BlockingCollection<string>(new ConcurrentQueue<string>());
        private volatile TransportState _state = TransportState.Idle;
        private volatile string _error;
        private int _disposed;

        public TransportState State => _state;
        public string Error => _error;

        public WsTransport()
        {
            _ws.Options.KeepAliveInterval = KeepAlive;
        }

        public void Connect(string url)
        {
            if (_state != TransportState.Idle) throw new InvalidOperationException("a transport connects once");
            _state = TransportState.Connecting;
            Task.Run(() => RunAsync(new Uri(url)));
        }

        public void Send(string text)
        {
            if (_state == TransportState.Closed || text == null) return;
            try { _out.Add(text); }
            catch (InvalidOperationException) { } // completed: closing
        }

        public bool TryReceive(out string text) => _in.TryDequeue(out text);

        private async Task RunAsync(Uri uri)
        {
            try
            {
                using (CancellationTokenSource c = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
                {
                    c.CancelAfter(ConnectTimeout);
                    await _ws.ConnectAsync(uri, c.Token).ConfigureAwait(false);
                }
                _state = TransportState.Open;
                Task sender = Task.Run(SendLoop);
                await ReceiveLoop().ConfigureAwait(false);
                await sender.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Fail(e is OperationCanceledException && !_cts.IsCancellationRequested ? "connect timeout" : e.Message);
            }
        }

        private async Task ReceiveLoop()
        {
            byte[] buf = new byte[64 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                while (!_cts.IsCancellationRequested)
                {
                    WebSocketReceiveResult r = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), _cts.Token).ConfigureAwait(false);
                    if (r.MessageType == WebSocketMessageType.Close)
                    {
                        Fail("closed by server" + (r.CloseStatusDescription != null ? ": " + r.CloseStatusDescription : ""));
                        return;
                    }
                    ms.Write(buf, 0, r.Count);
                    if (!r.EndOfMessage) continue;
                    _in.Enqueue(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int) ms.Length));
                    ms.SetLength(0);
                }
            }
        }

        private void SendLoop()
        {
            try
            {
                foreach (string text in _out.GetConsumingEnumerable(_cts.Token))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(text);
                    _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token).GetAwaiter().GetResult();
                }
            }
            catch (Exception e)
            {
                Fail(e.Message);
            }
        }

        private void Fail(string why)
        {
            if (_state == TransportState.Closed) return;
            _error = why;
            _state = TransportState.Closed;
            try { _cts.Cancel(); } catch (ObjectDisposedException) { }
            _out.CompleteAdding();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (_state != TransportState.Closed) Fail("disposed");
            try
            {
                if (_ws.State == WebSocketState.Open)
                    _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).Wait(500);
            }
            catch (Exception) { }
            _ws.Dispose();
        }
    }
}
