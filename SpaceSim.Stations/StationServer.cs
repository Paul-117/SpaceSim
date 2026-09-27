using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SpaceSim.Stations.Armarium;

namespace SpaceSim.Stations;

/// <summary>
/// Lightweight HTTP and WebSocket host for browser-based ship stations. It owns no simulation state;
/// callers publish immutable station snapshots and consume buffered command intents on their own thread.
/// </summary>
public sealed class StationServer : IDisposable
{
    private const int MaximumHeaderBytes = 16 * 1024;
    private const int MaximumMessageBytes = 8 * 1024;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly StationServerOptions _options;
    private readonly IReadOnlyDictionary<string, string> _assets;
    private readonly ArmariumCommandBuffer _armariumCommands;
    private readonly Action<string>? _log;
    private readonly ConcurrentDictionary<int, StationConnection> _connections = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly TcpListener _listener;
    private ArmariumState _latestState = new(false, 0f, 0f, false, false, 0);
    private Task? _acceptTask;
    private Task? _broadcastTask;
    private int _nextConnectionId;
    private long _sequence;
    private bool _started;

    public StationServer(StationServerOptions options, IReadOnlyDictionary<string, string> assets,
        ArmariumCommandBuffer armariumCommands, Action<string>? log = null)
    {
        _options = options;
        _options.Validate();
        _assets = assets;
        _armariumCommands = armariumCommands;
        _log = log;
        _listener = new TcpListener(IPAddress.Any, options.Port);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public string ArmariumUrl => $"http://127.0.0.1:{Port}/armarium/";

    public void Start()
    {
        if (_started) return;
        _listener.Start();
        _started = true;
        _acceptTask = AcceptLoopAsync(_stopping.Token);
        _broadcastTask = BroadcastLoopAsync(_stopping.Token);
        _log?.Invoke($"Station server listening on port {Port}.");
    }

    public void UpdateState(ArmariumState state) => Volatile.Write(ref _latestState, state);

    public void Dispose()
    {
        if (!_started) return;
        _stopping.Cancel();
        _listener.Stop();
        foreach (StationConnection connection in _connections.Values) connection.Dispose();
        _connections.Clear();
        try { Task.WaitAll([_acceptTask!, _broadcastTask!], TimeSpan.FromSeconds(1)); }
        catch (AggregateException) { }
        _stopping.Dispose();
        _started = false;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception exception) { _log?.Invoke($"Station accept error: {exception.Message}"); }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        await using NetworkStream stream = client.GetStream();
        int connectionId = Interlocked.Increment(ref _nextConnectionId);
        StationConnection? connection = null;
        try
        {
            HttpRequest request = await ReadHttpRequestAsync(stream, cancellationToken);
            if (request.Path == "/station" && request.IsWebSocket)
            {
                if (!request.Headers.TryGetValue("sec-websocket-key", out string? key))
                {
                    await WriteHttpAsync(stream, "400 Bad Request", "Missing WebSocket key", "text/plain", cancellationToken);
                    return;
                }
                await WriteWebSocketHandshakeAsync(stream, key, cancellationToken);
                connection = new StationConnection(connectionId, client, stream);
                _connections.TryAdd(connectionId, connection);
                await ReceiveMessagesAsync(connection, cancellationToken);
                return;
            }
            await ServeAssetAsync(stream, request.Path, cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (Exception exception) { _log?.Invoke($"Station connection error: {exception.Message}"); }
        finally
        {
            if (connection is not null)
            {
                _connections.TryRemove(connection.Id, out _);
                _armariumCommands.ClearSteering();
                connection.Dispose();
            }
            client.Dispose();
        }
    }

    private async Task ReceiveMessagesAsync(StationConnection connection, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? message = await ReadWebSocketTextAsync(connection.Stream, cancellationToken);
            if (message is null) return;
            StationClientMessage? command;
            try { command = JsonSerializer.Deserialize<StationClientMessage>(message, Json); }
            catch (JsonException)
            {
                await connection.SendJsonAsync(new StationError("invalid_message", "Invalid JSON message."), cancellationToken);
                continue;
            }
            if (command is null) continue;

            if (!connection.IsAccepted)
            {
                if (command.Type != "hello" || !string.Equals(command.Station, StationProtocol.ArmariumStation,
                    StringComparison.OrdinalIgnoreCase) || command.ProtocolVersion != StationProtocol.Version)
                {
                    await connection.SendJsonAsync(new StationError("protocol_mismatch",
                        $"Expected ARMARIUM protocol {StationProtocol.Version}."), cancellationToken);
                    return;
                }
                connection.IsAccepted = true;
                await connection.SendJsonAsync(new StationWelcome(StationProtocol.ArmariumStation, StationProtocol.Version), cancellationToken);
                continue;
            }

            if (!Volatile.Read(ref _latestState).ArmariumControlsActive) continue;
            if (command.Type == "fire_lance") _armariumCommands.RequestFire();
            else if (command.Type == "yaw" && command.Active is bool active)
            {
                int direction = string.Equals(command.Direction, "left", StringComparison.OrdinalIgnoreCase) ? -1 :
                    string.Equals(command.Direction, "right", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                _armariumCommands.SetYawDirection(active ? direction : 0);
            }
        }
    }

    private async Task BroadcastLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            TimeSpan period = TimeSpan.FromSeconds(1d / _options.StateUpdatesPerSecond);
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(period, cancellationToken);
                ArmariumState state = Volatile.Read(ref _latestState);
                var message = new ArmariumStateMessage(state, Interlocked.Increment(ref _sequence));
                foreach (StationConnection connection in _connections.Values.Where(connection => connection.IsAccepted))
                {
                    try { await connection.SendJsonAsync(message, cancellationToken); }
                    catch (IOException) { connection.Dispose(); _connections.TryRemove(connection.Id, out _); }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task ServeAssetAsync(NetworkStream stream, string path, CancellationToken cancellationToken)
    {
        if (path is "/" or "/armarium")
        {
            byte[] empty = Array.Empty<byte>();
            string redirect = "HTTP/1.1 302 Found\r\nLocation: /armarium/\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(redirect), cancellationToken);
            return;
        }
        string asset = path == "/armarium/" ? "index.html" : path.StartsWith("/armarium/", StringComparison.Ordinal)
            ? path[10..] : string.Empty;
        if (!_assets.TryGetValue(asset, out string? content))
        {
            await WriteHttpAsync(stream, "404 Not Found", "Not found", "text/plain", cancellationToken);
            return;
        }
        string type = asset.EndsWith(".css", StringComparison.Ordinal) ? "text/css; charset=utf-8" :
            asset.EndsWith(".js", StringComparison.Ordinal) ? "text/javascript; charset=utf-8" : "text/html; charset=utf-8";
        await WriteHttpAsync(stream, "200 OK", content, type, cancellationToken);
    }

    private static async Task<HttpRequest> ReadHttpRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        var one = new byte[1];
        while (bytes.Count < MaximumHeaderBytes)
        {
            int count = await stream.ReadAsync(one, cancellationToken);
            if (count == 0) throw new IOException("Client closed before HTTP request.");
            bytes.Add(one[0]);
            int n = bytes.Count;
            if (n >= 4 && bytes[n - 4] == 13 && bytes[n - 3] == 10 && bytes[n - 2] == 13 && bytes[n - 1] == 10) break;
        }
        string[] lines = Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        string[] requestLine = lines.FirstOrDefault()?.Split(' ', 3) ?? [];
        if (requestLine.Length < 2 || requestLine[0] != "GET") throw new IOException("Only HTTP GET is supported.");
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines.Skip(1))
        {
            int separator = line.IndexOf(':');
            if (separator > 0) headers[line[..separator].Trim().ToLowerInvariant()] = line[(separator + 1)..].Trim();
        }
        string path = requestLine[1].Split('?', 2)[0];
        bool webSocket = headers.TryGetValue("upgrade", out string? upgrade) &&
            upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase);
        return new HttpRequest(path, headers, webSocket);
    }

    private static async Task WriteHttpAsync(NetworkStream stream, string status, string content, string type,
        CancellationToken cancellationToken)
    {
        byte[] body = Encoding.UTF8.GetBytes(content);
        string header = $"HTTP/1.1 {status}\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header), cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
    }

    private static async Task WriteWebSocketHandshakeAsync(NetworkStream stream, string key, CancellationToken cancellationToken)
    {
        string accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        string response = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
            $"Sec-WebSocket-Accept: {accept}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
    }

    private static async Task<string?> ReadWebSocketTextAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        int first = await ReadByteAsync(stream, cancellationToken);
        if (first < 0) return null;
        int second = await ReadByteAsync(stream, cancellationToken);
        if (second < 0) return null;
        int opcode = first & 0x0f;
        if (opcode == 0x8) return null;
        if (opcode != 0x1 || (second & 0x80) == 0) throw new IOException("Expected a masked WebSocket text frame.");
        ulong length = (uint)(second & 0x7f);
        if (length == 126) length = BitConverter.ToUInt16((await ReadExactAsync(stream, 2, cancellationToken)).Reverse().ToArray());
        else if (length == 127) length = BitConverter.ToUInt64((await ReadExactAsync(stream, 8, cancellationToken)).Reverse().ToArray());
        if (length > MaximumMessageBytes) throw new IOException("WebSocket message is too large.");
        byte[] mask = await ReadExactAsync(stream, 4, cancellationToken);
        byte[] data = await ReadExactAsync(stream, (int)length, cancellationToken);
        for (int i = 0; i < data.Length; i++) data[i] ^= mask[i % 4];
        return Encoding.UTF8.GetString(data);
    }

    private static async Task<int> ReadByteAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        byte[] value = await ReadExactAsync(stream, 1, cancellationToken);
        return value.Length == 0 ? -1 : value[0];
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
    {
        var bytes = new byte[length];
        int offset = 0;
        while (offset < length)
        {
            int read = await stream.ReadAsync(bytes.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0) return Array.Empty<byte>();
            offset += read;
        }
        return bytes;
    }

    private sealed class StationConnection(int id, TcpClient client, NetworkStream stream) : IDisposable
    {
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        public int Id { get; } = id;
        public TcpClient Client { get; } = client;
        public NetworkStream Stream { get; } = stream;
        public bool IsAccepted { get; set; }

        public async Task SendJsonAsync<T>(T message, CancellationToken cancellationToken)
        {
            byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, Json));
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                var frame = new List<byte> { 0x81 };
                if (body.Length < 126) frame.Add((byte)body.Length);
                else { frame.Add(126); frame.Add((byte)(body.Length >> 8)); frame.Add((byte)body.Length); }
                frame.AddRange(body);
                await Stream.WriteAsync(frame.ToArray(), cancellationToken);
            }
            finally { _writeLock.Release(); }
        }

        public void Dispose()
        {
            Client.Dispose();
            _writeLock.Dispose();
        }
    }

    private sealed record HttpRequest(string Path, IReadOnlyDictionary<string, string> Headers, bool IsWebSocket);
    private sealed record StationClientMessage(string? Type, string? Station, int? ProtocolVersion,
        string? Direction, bool? Active);
    private sealed record StationWelcome(string Station, int ProtocolVersion) { public string Type { get; } = "welcome"; }
    private sealed record StationError(string Code, string Message) { public string Type { get; } = "error"; }
    private sealed record ArmariumStateMessage(ArmariumState State, long ConnectionSequence)
    {
        public string Type { get; } = "armarium_state";
        public bool TargetAvailable => State.TargetAvailable;
        public float TargetBearingDegrees => State.TargetBearingDegrees;
        public float LanceCharge => State.LanceCharge;
        public bool LanceReady => State.LanceReady;
        public bool ArmariumControlsActive => State.ArmariumControlsActive;
        public long SimulationTick => State.SimulationTick;
    }
}

public sealed record StationServerOptions
{
    public const int DefaultPort = 47870;
    public const int DefaultStateUpdatesPerSecond = 25;
    public int Port { get; init; } = DefaultPort;
    public int StateUpdatesPerSecond { get; init; } = DefaultStateUpdatesPerSecond;

    internal void Validate()
    {
        if (Port < 0 || Port > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
        if (StateUpdatesPerSecond is < 1 or > 60) throw new ArgumentOutOfRangeException(nameof(StateUpdatesPerSecond));
    }
}
