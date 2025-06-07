using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Auth0Management.App;

internal sealed class Program
{
    private static ILogger<Program>? _logger;

    // LoggerMessage delegate for improved logging performance
    private static readonly Action<ILogger, string, Exception> _logAppError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, nameof(LogAppError)),
            "{Message}");

    private static void LogAppError(ILogger logger, Exception ex, string message)
    {
        _logAppError(logger, message, ex);
    }

    private static readonly Action<ILogger, string, Exception?> _logOperationCanceled =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, nameof(LogOperationCanceled)),
            "{Message}");

    private static void LogOperationCanceled(ILogger logger, string message)
    {
        _logOperationCanceled(logger, message, null);
    }

    public static async Task Main(string[] args)
    {
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, builder) =>
            {
                builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddOptions<Auth0Options>()
                        .Bind(context.Configuration.GetSection("Auth0"))
                        .ValidateDataAnnotations();
                services.AddSingleton<Auth0Service>();
            })
            .Build();

        _logger = host.Services.GetRequiredService<ILogger<Program>>();
        var auth0Service = host.Services.GetRequiredService<Auth0Service>();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await ShowMenuAsync(auth0Service, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            LogOperationCanceled(_logger!, "Operation was canceled by the user.");
        }
        catch (Exception ex)
        {
            LogAppError(_logger!, ex, "An error occurred in the application.");
            throw; // Rethrow to allow higher-level handlers or crash reporting
        }
    }

    private static async Task ShowMenuAsync(Auth0Service auth0Service, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine(Resources.SelectActionPrompt);
            Console.WriteLine(Resources.ListUsersOption);
            Console.WriteLine(Resources.CreateUserOption);
            Console.WriteLine(Resources.UpdateUserOption);
            Console.WriteLine(Resources.ExitOption);
            Console.Write(Resources.EnterChoicePrompt);
            var choice = Console.ReadLine();
            switch (choice)
            {
                case "1":
                    await auth0Service.ListUsersAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case "2":
                    await auth0Service.CreateUserInteractiveAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case "3":
                    await auth0Service.UpdateUserInteractiveAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case "4":
                    return;
                default:
                    Console.WriteLine(Resources.InvalidChoice);
                    break;
            }
        }
    }
    // For future: Add unit tests for input validation and service logic.
}