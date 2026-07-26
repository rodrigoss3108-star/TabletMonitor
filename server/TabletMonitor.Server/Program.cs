using System.Text;
using TabletMonitor.Server;

Console.OutputEncoding = Encoding.UTF8;

if (args.Any(argument => argument is "--help" or "-h" or "/?"))
{
    Console.WriteLine(ServerOptions.HelpText);
    return;
}

ServerOptions options;

try
{
    options = ServerOptions.Parse(args);
}
catch (ArgumentException error)
{
    Console.Error.WriteLine($"Erro: {error.Message}");
    Console.Error.WriteLine();
    Console.Error.WriteLine(ServerOptions.HelpText);
    Environment.ExitCode = 2;
    return;
}

using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var server = new DesktopStreamServer(options);

try
{
    await server.RunAsync(cancellation.Token);
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.WriteLine();
    Console.WriteLine("Servidor encerrado.");
}
catch (Exception error)
{
    Console.Error.WriteLine($"Falha fatal: {error.Message}");
    Environment.ExitCode = 1;
}

