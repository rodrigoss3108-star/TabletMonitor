using System.Net;
using System.Net.Sockets;

namespace TabletMonitor.Server;

internal sealed class DesktopStreamServer(ServerOptions options)
{
    private const int NetworkBufferSize = 4 * 1024 * 1024;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Any, options.Port);
        listener.Start(backlog: 1);

        Console.WriteLine("TabletMonitor.Server");
        Console.WriteLine($"Escutando em 0.0.0.0:{options.Port}");
        Console.WriteLine(
            $"Captura: X={options.CaptureX}, Y={options.CaptureY}, " +
            $"{options.CaptureWidth} × {options.CaptureHeight}"
        );
        Console.WriteLine(
            $"Vídeo: {options.Width} × {options.Height}, " +
            $"{options.FramesPerSecond} FPS, {options.BitrateKbps} kbps"
        );
        Console.WriteLine("Aguardando o tablet...");
        Console.WriteLine("Pressione Ctrl+C para encerrar.");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(cancellationToken);
                await ServeClientAsync(client, cancellationToken);
                Console.WriteLine("Aguardando uma nova conexão...");
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task ServeClientAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        client.NoDelay = true;
        client.SendBufferSize = NetworkBufferSize;

        var remoteAddress = client.Client.RemoteEndPoint?.ToString() ?? "desconhecido";
        Console.WriteLine($"Tablet conectado: {remoteAddress}");

        try
        {
            await using var capture = FfmpegDesktopCapture.Start(options);
            await using var network = client.GetStream();

            await TabletMonitorProtocol.WriteHeaderAsync(
                network,
                options.Width,
                options.Height,
                options.FramesPerSecond,
                cancellationToken
            );

            long frameCount = 0;

            await foreach (
                var accessUnit in H264AnnexBReader.ReadAccessUnitsAsync(
                    capture.VideoStream,
                    cancellationToken
                )
            )
            {
                await TabletMonitorProtocol.WriteFrameAsync(
                    network,
                    accessUnit,
                    cancellationToken
                );

                frameCount++;

                if (frameCount % (options.FramesPerSecond * 5L) == 0)
                {
                    Console.WriteLine($"Transmitidos {frameCount} quadros.");
                }
            }

            await capture.EnsureSuccessfulExitAsync(cancellationToken);
        }
        catch (IOException error)
        {
            Console.WriteLine($"Conexão encerrada: {error.Message}");
        }
        catch (SocketException error)
        {
            Console.WriteLine($"Conexão encerrada: {error.Message}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Falha durante a transmissão: {error.Message}");
        }
    }
}
