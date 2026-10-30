using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AetherSerpent.Network
{
    /// <summary>
    /// Enterprise-grade, cross-platform WebSocket client for Unity.
    /// Employs:
    /// - Native asynchronous .NET ClientWebSocket
    /// - ConcurrentQueue for thread-safe main-thread dispatching
    /// - Automatic reconnection with exponential backoff
    /// - Heartbeat ping-pong latency metering (RTT ms)
    /// </summary>
    public class WebSocketClient
    {
        public enum ConnectionState
        {
            Disconnected,
            Connecting,
            Connected,
            Reconnecting
        }

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public int PingMs { get; private set; } = 0;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnMessageReceived;
        public event Action<string> OnError;
        public event Action<int> OnPingUpdated;

        private ClientWebSocket webSocket;
        private CancellationTokenSource cancellationTokenSource;
        private readonly ConcurrentQueue<string> incomingMessages = new ConcurrentQueue<string>();

        private string targetUri;
        private bool isIntentionalDisconnect = false;
        private int reconnectAttempts = 0;
        private const int MaxReconnectAttempts = 6;
        private long lastPingSentTimestamp;

        public async Task ConnectAsync(string uri)
        {
            targetUri = uri;
            isIntentionalDisconnect = false;
            reconnectAttempts = 0;

            await EstablishConnection();
        }

        private async Task EstablishConnection()
        {
            if (State == ConnectionState.Connected || State == ConnectionState.Connecting) return;

            State = reconnectAttempts > 0 ? ConnectionState.Reconnecting : ConnectionState.Connecting;
            cancellationTokenSource = new CancellationTokenSource();
            webSocket = new ClientWebSocket();

            try
            {
                Debug.Log($"[WebSocketClient] Connecting to: {targetUri} (Attempt {reconnectAttempts + 1})");
                Uri serverUri = new Uri(targetUri);
                await webSocket.ConnectAsync(serverUri, cancellationTokenSource.Token);

                State = ConnectionState.Connected;
                reconnectAttempts = 0;
                OnConnected?.Invoke();
                Debug.Log("[WebSocketClient] WebSocket connection established successfully.");

                // Start receiving background loop
                _ = ReceiveLoop(cancellationTokenSource.Token);

                // Start periodic ping loop
                _ = PingLoop(cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                State = ConnectionState.Disconnected;
                OnError?.Invoke(ex.Message);
                Debug.LogWarning($"[WebSocketClient] Connection failed: {ex.Message}");

                if (!isIntentionalDisconnect)
                {
                    ScheduleReconnect();
                }
            }
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            byte[] buffer = new byte[8192];

            while (webSocket != null && webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                try
                {
                    StringBuilder messageBuilder = new StringBuilder();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                            HandleDisconnect();
                            return;
                        }

                        string chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        messageBuilder.Append(chunk);
                    } while (!result.EndOfMessage);

                    string fullMessage = messageBuilder.ToString();
                    incomingMessages.Enqueue(fullMessage);
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        Debug.LogWarning($"[WebSocketClient] Error in ReceiveLoop: {ex.Message}");
                        HandleDisconnect();
                    }
                    return;
                }
            }
        }

        private async Task PingLoop(CancellationToken token)
        {
            while (webSocket != null && webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                await Task.Delay(3000, token);
                if (webSocket.State == WebSocketState.Open)
                {
                    lastPingSentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    string pingJson = $"{{\"type\":\"PING\",\"timestamp\":{lastPingSentTimestamp}}}";
                    await SendAsync(pingJson);
                }
            }
        }

        public async Task SendAsync(string message)
        {
            if (webSocket == null || webSocket.State != WebSocketState.Open) return;

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    cancellationTokenSource.Token
                );
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WebSocketClient] Send failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Must be invoked from Unity's main thread Update() loop.
        /// </summary>
        public void DispatchIncomingMessages()
        {
            while (incomingMessages.TryDequeue(out string message))
            {
                if (message.Contains("\"type\":\"PONG\""))
                {
                    long rtt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastPingSentTimestamp;
                    PingMs = (int)Math.Max(0, rtt);
                    OnPingUpdated?.Invoke(PingMs);
                }
                else
                {
                    OnMessageReceived?.Invoke(message);
                }
            }
        }

        private void HandleDisconnect()
        {
            State = ConnectionState.Disconnected;
            OnDisconnected?.Invoke();

            if (!isIntentionalDisconnect)
            {
                ScheduleReconnect();
            }
        }

        private void ScheduleReconnect()
        {
            if (reconnectAttempts >= MaxReconnectAttempts)
            {
                Debug.LogWarning("[WebSocketClient] Max reconnection attempts reached.");
                return;
            }

            reconnectAttempts++;
            float delay = Mathf.Min(Mathf.Pow(2f, reconnectAttempts), 16f);
            Debug.Log($"[WebSocketClient] Reconnecting in {delay:0.0}s (Attempt {reconnectAttempts}/{MaxReconnectAttempts})...");

            Task.Delay((int)(delay * 1000)).ContinueWith(_ =>
            {
                if (!isIntentionalDisconnect)
                {
                    _ = EstablishConnection();
                }
            });
        }

        public async Task DisconnectAsync()
        {
            isIntentionalDisconnect = true;
            cancellationTokenSource?.Cancel();

            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                }
                catch { }
            }

            webSocket?.Dispose();
            webSocket = null;
            State = ConnectionState.Disconnected;
            OnDisconnected?.Invoke();
        }
    }
}
