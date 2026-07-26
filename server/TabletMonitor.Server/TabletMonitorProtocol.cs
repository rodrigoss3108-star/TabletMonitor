using System.Buffers.Binary;
using System.Text;

namespace TabletMonitor.Server;

internal static class TabletMonitorProtocol
{
    private const byte ProtocolVersion = 1;
    private const int HeaderSize = 10;

    public static async Task WriteHeaderAsync(
        Stream output,
        int width,
        int height,
        int framesPerSecond,
        CancellationToken cancellationToken)
    {
        var header = new byte[HeaderSize];
        Encoding.ASCII.GetBytes("TMON", header);
        header[4] = ProtocolVersion;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(5, 2), checked((ushort)width));
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(7, 2), checked((ushort)height));
        header[9] = checked((byte)framesPerSecond);

        await output.WriteAsync(header, cancellationToken);
    }

    public static async Task WriteFrameAsync(
        Stream output,
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken)
    {
        var frameHeader = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(frameHeader, frame.Length);

        await output.WriteAsync(frameHeader, cancellationToken);
        await output.WriteAsync(frame, cancellationToken);
    }
}

