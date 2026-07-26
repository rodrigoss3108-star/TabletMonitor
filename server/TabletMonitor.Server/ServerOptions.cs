namespace TabletMonitor.Server;

internal sealed record ServerOptions(
    int Port,
    int Width,
    int Height,
    int FramesPerSecond,
    int BitrateKbps,
    string FfmpegPath)
{
    public static string HelpText =>
        """
        TabletMonitor.Server

        Uso:
          TabletMonitor.Server [opções]

        Opções:
          --port <número>       Porta TCP. Padrão: 5000
          --width <pixels>      Largura transmitida. Padrão: 1920
          --height <pixels>     Altura transmitida. Padrão: 1200
          --fps <número>        Quadros por segundo. Padrão: 30
          --bitrate <kbps>      Bitrate do H.264. Padrão: 12000
          --ffmpeg <caminho>    Caminho completo do ffmpeg.exe
          --help                Exibe esta ajuda
        """;

    public static ServerOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index += 2)
        {
            var name = args[index];

            if (!name.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Opção inválida: {name}");
            }

            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Falta o valor de {name}");
            }

            values[name[2..]] = args[index + 1];
        }

        var port = ReadInteger(values, "port", 5000, 1, 65535);
        var width = ReadInteger(values, "width", 1920, 320, 7680);
        var height = ReadInteger(values, "height", 1200, 240, 4320);
        var framesPerSecond = ReadInteger(values, "fps", 30, 1, 120);
        var bitrateKbps = ReadInteger(values, "bitrate", 12000, 500, 100000);
        var ffmpegPath = ReadString(values, "ffmpeg", "ffmpeg.exe");

        var supported = new HashSet<string>(
            ["port", "width", "height", "fps", "bitrate", "ffmpeg"],
            StringComparer.OrdinalIgnoreCase
        );

        var unsupported = values.Keys.FirstOrDefault(key => !supported.Contains(key));

        if (unsupported is not null)
        {
            throw new ArgumentException($"Opção desconhecida: --{unsupported}");
        }

        return new ServerOptions(
            port,
            width,
            height,
            framesPerSecond,
            bitrateKbps,
            ffmpegPath
        );
    }

    private static int ReadInteger(
        IReadOnlyDictionary<string, string> values,
        string name,
        int defaultValue,
        int minimum,
        int maximum)
    {
        if (!values.TryGetValue(name, out var rawValue))
        {
            return defaultValue;
        }

        if (!int.TryParse(rawValue, out var value) || value < minimum || value > maximum)
        {
            throw new ArgumentException(
                $"--{name} deve estar entre {minimum} e {maximum}"
            );
        }

        return value;
    }

    private static string ReadString(
        IReadOnlyDictionary<string, string> values,
        string name,
        string defaultValue)
    {
        if (!values.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim();
    }
}

