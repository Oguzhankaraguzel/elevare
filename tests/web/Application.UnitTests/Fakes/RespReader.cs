using System.Text;

namespace Application.UnitTests.Fakes;

/// <summary>
/// Reads RESP2 client requests — always an array of bulk strings — off a stream,
/// one command at a time. Inline (telnet-style) commands are not supported; the
/// StackExchange.Redis client never sends them.
/// <para>
/// Payloads are decoded with Latin1, not UTF-8, on purpose: Latin1 maps bytes
/// 0-255 one-to-one onto characters, so arbitrary binary survives the
/// byte-to-string round trip intact. That matters because the client's handshake
/// ECHO carries random bytes and is rejected unless echoed back verbatim — UTF-8
/// would replace every invalid sequence with U+FFFD and quietly corrupt it.
/// </para>
/// </summary>
internal sealed class RespReader(Stream stream)
{
    private readonly byte[] _buffer = new byte[1];

    public async Task<List<string>?> ReadCommandAsync(CancellationToken cancellationToken)
    {
        string? header = await ReadLineAsync(cancellationToken);
        if (header is null)
            return null;

        if (!header.StartsWith('*') || !int.TryParse(header[1..], out int argCount) || argCount <= 0)
            return [];

        var args = new List<string>(argCount);
        for (int i = 0; i < argCount; i++)
        {
            string? lengthLine = await ReadLineAsync(cancellationToken);
            if (lengthLine is null)
                return null;
            if (!lengthLine.StartsWith('$') || !int.TryParse(lengthLine[1..], out int length))
                return [];

            if (length < 0)
            {
                args.Add("");
                continue;
            }

            byte[] payload = new byte[length];
            int read = 0;
            while (read < length)
            {
                int n = await stream.ReadAsync(payload.AsMemory(read, length - read), cancellationToken);
                if (n == 0)
                    return null;
                read += n;
            }

            // Consume the trailing CRLF.
            await ReadLineAsync(cancellationToken);
            args.Add(Encoding.Latin1.GetString(payload));
        }

        return args;
    }

    private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        StringBuilder sb = new();
        while (true)
        {
            int n = await stream.ReadAsync(_buffer.AsMemory(0, 1), cancellationToken);
            if (n == 0)
                return sb.Length == 0 ? null : sb.ToString();

            if (_buffer[0] == (byte)'\r')
                continue;
            if (_buffer[0] == (byte)'\n')
                return sb.ToString();

            sb.Append((char)_buffer[0]);
        }
    }
}
