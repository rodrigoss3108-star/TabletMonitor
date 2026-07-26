using System.Diagnostics;

namespace TabletMonitor.Server;

internal sealed class FfmpegDesktopCapture : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task errorReader;

    private FfmpegDesktopCapture(Process process)
    {
        this.process = process;
        VideoStream = process.StandardOutput.BaseStream;
        errorReader = ReadErrorsAsync(process.StandardError);
    }

    public Stream VideoStream { get; }

    public static FfmpegDesktopCapture Start(ServerOptions options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.FfmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        AddArgument(startInfo, "-hide_banner");
        AddArgument(startInfo, "-loglevel", "warning");
        AddArgument(startInfo, "-f", "gdigrab");
        AddArgument(startInfo, "-framerate", options.FramesPerSecond.ToString());
        AddArgument(startInfo, "-draw_mouse", "1");
        AddArgument(startInfo, "-rtbufsize", "256M");
        AddArgument(startInfo, "-i", "desktop");
        AddArgument(startInfo, "-an");
        AddArgument(
            startInfo,
            "-vf",
            $"scale={options.Width}:{options.Height}:" +
                "force_original_aspect_ratio=decrease," +
                $"pad={options.Width}:{options.Height}:(ow-iw)/2:(oh-ih)/2"
        );
        AddArgument(startInfo, "-c:v", "libx264");
        AddArgument(startInfo, "-preset", "ultrafast");
        AddArgument(startInfo, "-tune", "zerolatency");
        AddArgument(startInfo, "-profile:v", "baseline");
        AddArgument(startInfo, "-level", "4.2");
        AddArgument(startInfo, "-pix_fmt", "yuv420p");
        AddArgument(startInfo, "-b:v", $"{options.BitrateKbps}k");
        AddArgument(startInfo, "-maxrate", $"{options.BitrateKbps}k");
        AddArgument(startInfo, "-bufsize", $"{options.BitrateKbps / 2}k");
        AddArgument(startInfo, "-g", options.FramesPerSecond.ToString());
        AddArgument(startInfo, "-keyint_min", options.FramesPerSecond.ToString());
        AddArgument(startInfo, "-sc_threshold", "0");
        AddArgument(startInfo, "-x264-params", "aud=1:repeat-headers=1");
        AddArgument(startInfo, "-f", "h264");
        AddArgument(startInfo, "pipe:1");

        Process? process;

        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                $"Não foi possível iniciar o FFmpeg em '{options.FfmpegPath}'.",
                error
            );
        }

        if (process is null)
        {
            throw new InvalidOperationException("O FFmpeg não foi iniciado.");
        }

        return new FfmpegDesktopCapture(process);
    }

    public async Task EnsureSuccessfulExitAsync(CancellationToken cancellationToken)
    {
        await process.WaitForExitAsync(cancellationToken);
        await errorReader;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"O FFmpeg terminou com o código {process.ExitCode}."
            );
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // O processo já foi encerrado.
        }

        try
        {
            await process.WaitForExitAsync();
        }
        catch (InvalidOperationException)
        {
            // O processo não chegou a ser iniciado completamente.
        }

        await errorReader;
        process.Dispose();
    }

    private static void AddArgument(
        ProcessStartInfo startInfo,
        params string[] arguments)
    {
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private static async Task ReadErrorsAsync(StreamReader errorOutput)
    {
        while (await errorOutput.ReadLineAsync() is { } line)
        {
            Console.Error.WriteLine($"FFmpeg: {line}");
        }
    }
}

