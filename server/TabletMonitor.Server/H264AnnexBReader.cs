using System.Runtime.CompilerServices;

namespace TabletMonitor.Server;

internal static class H264AnnexBReader
{
    private const int InitialBufferSize = 1024 * 1024;
    private const int MaximumBufferSize = 16 * 1024 * 1024;
    private const int MaximumAccessUnitSize = 8 * 1024 * 1024;
    private const int AccessUnitDelimiterNalType = 9;

    public static async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAccessUnitsAsync(
        Stream input,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var currentAccessUnit = new MemoryStream();
        using var prefix = new MemoryStream();
        var foundAccessUnitDelimiter = false;

        await foreach (
            var nalUnit in ReadNalUnitsAsync(input, cancellationToken)
        )
        {
            var nalType = GetNalType(nalUnit.Span);

            if (nalType == AccessUnitDelimiterNalType)
            {
                if (foundAccessUnitDelimiter && currentAccessUnit.Length > 0)
                {
                    yield return currentAccessUnit.ToArray();
                    currentAccessUnit.SetLength(0);
                }

                if (!foundAccessUnitDelimiter && prefix.Length > 0)
                {
                    prefix.Position = 0;
                    await prefix.CopyToAsync(currentAccessUnit, cancellationToken);
                    prefix.SetLength(0);
                }

                foundAccessUnitDelimiter = true;
            }

            var destination = foundAccessUnitDelimiter ? currentAccessUnit : prefix;
            await destination.WriteAsync(nalUnit, cancellationToken);

            if (destination.Length > MaximumAccessUnitSize)
            {
                throw new InvalidDataException(
                    "O FFmpeg produziu uma unidade H.264 maior que 8 MB."
                );
            }
        }

        if (!foundAccessUnitDelimiter)
        {
            throw new InvalidDataException(
                "O fluxo H.264 não contém delimitadores de unidade de acesso."
            );
        }

        if (currentAccessUnit.Length > 0)
        {
            yield return currentAccessUnit.ToArray();
        }
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadNalUnitsAsync(
        Stream input,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new byte[InitialBufferSize];
        var count = 0;

        while (true)
        {
            if (count == buffer.Length)
            {
                if (buffer.Length >= MaximumBufferSize)
                {
                    throw new InvalidDataException(
                        "NAL H.264 maior que o limite de 16 MB."
                    );
                }

                Array.Resize(
                    ref buffer,
                    Math.Min(buffer.Length * 2, MaximumBufferSize)
                );
            }

            var bytesRead = await input.ReadAsync(
                buffer.AsMemory(count, buffer.Length - count),
                cancellationToken
            );

            if (bytesRead == 0)
            {
                break;
            }

            count += bytesRead;

            var firstStartCode = FindStartCode(buffer, 0, count);

            if (firstStartCode < 0)
            {
                PreserveStartCodePrefix(buffer, ref count);
                continue;
            }

            if (firstStartCode > 0)
            {
                ShiftLeft(buffer, ref count, firstStartCode);
            }

            while (true)
            {
                var firstStartCodeLength = GetStartCodeLength(buffer, 0, count);

                if (firstStartCodeLength == 0)
                {
                    break;
                }

                var nextStartCode = FindStartCode(
                    buffer,
                    firstStartCodeLength,
                    count
                );

                if (nextStartCode < 0)
                {
                    break;
                }

                var nalUnit = new byte[nextStartCode];
                Buffer.BlockCopy(buffer, 0, nalUnit, 0, nextStartCode);
                ShiftLeft(buffer, ref count, nextStartCode);
                yield return nalUnit;
            }
        }

        if (count > 0 && FindStartCode(buffer, 0, count) == 0)
        {
            var finalNalUnit = new byte[count];
            Buffer.BlockCopy(buffer, 0, finalNalUnit, 0, count);
            yield return finalNalUnit;
        }
    }

    private static int GetNalType(ReadOnlySpan<byte> nalUnit)
    {
        var startCodeLength = GetStartCodeLength(nalUnit);

        if (startCodeLength == 0 || nalUnit.Length <= startCodeLength)
        {
            throw new InvalidDataException("NAL H.264 inválida.");
        }

        return nalUnit[startCodeLength] & 0x1F;
    }

    private static int FindStartCode(
        ReadOnlySpan<byte> data,
        int start,
        int length)
    {
        for (var index = start; index <= length - 3; index++)
        {
            if (data[index] != 0 || data[index + 1] != 0)
            {
                continue;
            }

            if (data[index + 2] == 1)
            {
                return index;
            }

            if (index <= length - 4 && data[index + 2] == 0 && data[index + 3] == 1)
            {
                return index;
            }
        }

        return -1;
    }

    private static int GetStartCodeLength(
        ReadOnlySpan<byte> data,
        int start = 0,
        int length = -1)
    {
        var actualLength = length < 0 ? data.Length : length;

        if (
            start + 3 <= actualLength &&
            data[start] == 0 &&
            data[start + 1] == 0 &&
            data[start + 2] == 1
        )
        {
            return 3;
        }

        if (
            start + 4 <= actualLength &&
            data[start] == 0 &&
            data[start + 1] == 0 &&
            data[start + 2] == 0 &&
            data[start + 3] == 1
        )
        {
            return 4;
        }

        return 0;
    }

    private static void ShiftLeft(byte[] buffer, ref int count, int amount)
    {
        Buffer.BlockCopy(buffer, amount, buffer, 0, count - amount);
        count -= amount;
    }

    private static void PreserveStartCodePrefix(byte[] buffer, ref int count)
    {
        var bytesToPreserve = Math.Min(count, 3);

        if (bytesToPreserve > 0)
        {
            Buffer.BlockCopy(
                buffer,
                count - bytesToPreserve,
                buffer,
                0,
                bytesToPreserve
            );
        }

        count = bytesToPreserve;
    }
}

