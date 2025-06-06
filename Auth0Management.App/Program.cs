using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Auth0Management.App;

internal class Program
{
    private static ILogger<Program>? _logger;

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

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await ShowMenuAsync(auth0Service, cts.Token);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "An error occurred in the application.");
        }
    }

    private static async Task ShowMenuAsync(Auth0Service auth0Service, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("\nSelect an action:");
            Console.WriteLine("1. List users");
            Console.WriteLine("2. Create user");
            Console.WriteLine("3. Update user");
            Console.WriteLine("4. Exit");
            Console.Write("Enter choice: ");
            var choice = Console.ReadLine();
            switch (choice)
            {
                case "1":
                    await auth0Service.ListUsersAsync(cancellationToken);
                    break;
                case "2":
                    await auth0Service.CreateUserInteractiveAsync(cancellationToken);
                    break;
                case "3":
                    await auth0Service.UpdateUserInteractiveAsync(cancellationToken);
                    break;
                case "4":
                    return;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }
        }
    }
    // For future: Add unit tests for input validation and service logic.
}