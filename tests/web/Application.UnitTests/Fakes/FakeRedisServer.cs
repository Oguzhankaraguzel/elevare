using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Application.UnitTests.Fakes;

/// <summary>
/// A minimal in-process RESP2 server, enough for StackExchange.Redis to complete
/// its handshake and run the handful of commands <c>RedisCacheService</c> uses
/// (GET/SET/DEL/DBSIZE/FLUSHDB/PING).
/// <para>
/// This exists because the cache's most important behaviours — that it round-trips
/// values over a real socket, and that it degrades quietly the moment the socket
/// goes away — cannot be covered by a hand-written mock of
/// <c>IConnectionMultiplexer</c>: the interesting failures live in the transport,
/// not in our own call sites. It is deliberately NOT a Redis implementation; it
/// proves our client wiring is correct, not that upstream Redis behaves a certain
/// way.
/// </para>
/// </summary>
internal sealed class FakeRedisServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, string> _store = new(StringComparer.Ordinal);

    public FakeRedisServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = AcceptLoopAsync();
    }

    public int Port { get; }

    public string ConnectionString =>
        $"127.0.0.1:{Port},connectTimeout=2000,syncTimeout=2000,connectRetry=1,allowAdmin=true";

    /// <summary>Keys currently held, so tests can assert on the server side too.</summary>
    public int KeyCount => _store.Count;

    /// <summary>Every command received, in order — for diagnosing handshake issues.</summary>
    public IReadOnlyList<string> CommandLog
    {
        get { lock (_log) { return [.. _log]; } }
    }

    private readonly List<string> _log = [];

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = HandleClientAsync(client);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            NetworkStream stream = client.GetStream();
            RespReader reader = new(stream);
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    List<string>? command = await reader.ReadCommandAsync(_cts.Token);
                    if (command is null || command.Count == 0)
                        return;

                    byte[] reply = Respond(command);
                    await stream.WriteAsync(reply, _cts.Token);
                    await stream.FlushAsync(_cts.Token);
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException or ObjectDisposedException)
            {
                // Client went away (or we're shutting down) — nothing to do.
            }
        }
    }

    private byte[] Respond(List<string> command)
    {
        lock (_log) { _log.Add(string.Join(' ', command)); }
        string verb = command[0].ToUpperInvariant();
        switch (verb)
        {
            case "PING":
                return Simple("PONG");

            case "ECHO":
                return Bulk(command.Count > 1 ? command[1] : "");

            // Answering HELLO with an error is what a pre-RESP3 server does, and
            // makes the client settle on RESP2 — which is all we implement.
            case "HELLO":
                return Error("ERR unknown command 'HELLO'");

            // CLIENT ID answers with an integer; the rest are acknowledgements.
            // Reply TYPE matters here — a simple-string where the client expects an
            // integer desynchronises the handshake and the connection never becomes
            // usable (which is exactly how this fake failed the first time).
            case "CLIENT":
                return command.Count > 1 && command[1].Equals("ID", StringComparison.OrdinalIgnoreCase)
                    ? Integer(1)
                    : Simple("OK");

            case "SELECT":
            case "AUTH":
                return Simple("OK");

            // CONFIG GET <name> → flat [name, value] array.
            case "CONFIG":
                {
                    if (command.Count < 3 || !command[1].Equals("GET", StringComparison.OrdinalIgnoreCase))
                        return Simple("OK");

                    string name = command[2];
                    string setting = name switch
                    {
                        "databases" => "16",
                        "slave-read-only" or "replica-read-only" => "no",
                        "timeout" => "0",
                        _ => "",
                    };
                    return Array2(name, setting);
                }

            case "INFO":
                {
                    string section = command.Count > 1 ? command[1].ToUpperInvariant() : "ALL";
                    return Bulk(section switch
                    {
                        "REPLICATION" => "# Replication\r\nrole:master\r\nconnected_slaves:0\r\n",
                        "SERVER" => "# Server\r\nredis_version:7.0.0\r\nredis_mode:standalone\r\nrun_id:fake\r\n",
                        _ => "# Server\r\nredis_version:7.0.0\r\nredis_mode:standalone\r\n# Replication\r\nrole:master\r\nconnected_slaves:0\r\n",
                    });
                }

            // A standalone server reports no cluster topology.
            case "CLUSTER":
                return Bulk("");

            // No sentinels are watching this "server".
            case "SENTINEL":
                return EmptyArray();

            case "SUBSCRIBE":
            case "UNSUBSCRIBE":
                {
                    string channel = command.Count > 1 ? command[1] : "";
                    string kind = verb == "SUBSCRIBE" ? "subscribe" : "unsubscribe";
                    string reply = string.Create(CultureInfo.InvariantCulture,
                        $"*3\r\n${kind.Length}\r\n{kind}\r\n${channel.Length}\r\n{channel}\r\n:1\r\n");
                    return Encoding.Latin1.GetBytes(reply);
                }

            case "GET":
                return _store.TryGetValue(command[1], out string? value) ? Bulk(value) : NullBulk();

            case "SET":
                _store[command[1]] = command[2];
                return Simple("OK");

            case "SETEX":
                _store[command[1]] = command[3];
                return Simple("OK");

            case "DEL":
            case "UNLINK":
                {
                    int removed = 0;
                    for (int i = 1; i < command.Count; i++)
                    {
                        if (_store.TryRemove(command[i], out _))
                            removed++;
                    }
                    return Integer(removed);
                }

            case "EXISTS":
                return Integer(_store.ContainsKey(command[1]) ? 1 : 0);

            case "DBSIZE":
                return Integer(_store.Count);

            case "FLUSHDB":
            case "FLUSHALL":
                _store.Clear();
                return Simple("OK");

            default:
                // An error reply is the safe default for anything unanticipated: the
                // client treats it as "server doesn't support this" and carries on,
                // whereas a wrongly-typed success would corrupt the reply stream.
                return Error($"ERR unknown command '{command[0]}'");
        }
    }

    private static byte[] Simple(string s) => Encoding.Latin1.GetBytes($"+{s}\r\n");
    private static byte[] Error(string s) => Encoding.Latin1.GetBytes($"-{s}\r\n");
    private static byte[] Integer(long n) => Encoding.Latin1.GetBytes($":{n}\r\n");
    private static byte[] NullBulk() => Encoding.Latin1.GetBytes("$-1\r\n");
    private static byte[] EmptyArray() => Encoding.Latin1.GetBytes("*0\r\n");

    /// <summary>A two-element array of bulk strings, e.g. a CONFIG GET reply.</summary>
    private static byte[] Array2(string first, string second)
    {
        string reply = string.Create(CultureInfo.InvariantCulture,
            $"*2\r\n${first.Length}\r\n{first}\r\n${second.Length}\r\n{second}\r\n");
        return Encoding.Latin1.GetBytes(reply);
    }

    private static byte[] Bulk(string s)
    {
        byte[] payload = Encoding.Latin1.GetBytes(s);
        byte[] prefix = Encoding.Latin1.GetBytes($"${payload.Length}\r\n");
        byte[] suffix = Encoding.Latin1.GetBytes("\r\n");
        byte[] result = new byte[prefix.Length + payload.Length + suffix.Length];
        prefix.CopyTo(result, 0);
        payload.CopyTo(result, prefix.Length);
        suffix.CopyTo(result, prefix.Length + payload.Length);
        return result;
    }

    /// <summary>Stops listening and drops connections, simulating a Redis outage.</summary>
    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _cts.Dispose();
    }
}
