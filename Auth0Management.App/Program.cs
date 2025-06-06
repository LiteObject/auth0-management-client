using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Auth0Management.App;

internal class Program
{
    private static ILogger<Program>? _logger;
    private static Auth0Options? _auth0Options;
    private const string ConnectionName = "Username-Password-Authentication";

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
            })
            .Build();

        _logger = host.Services.GetRequiredService<ILogger<Program>>();
        _auth0Options = host.Services.GetRequiredService<IOptions<Auth0Options>>().Value;

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await ShowMenuAsync(cts.Token);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "An error occurred in the application.");
        }
    }

    private static string Domain => _auth0Options?.Domain?.Replace("https://", "") ?? string.Empty;
    private static string ClientId => _auth0Options?.ClientId ?? string.Empty;
    private static string ClientSecret => _auth0Options?.ClientSecret ?? string.Empty;

    private static async Task ShowMenuAsync(CancellationToken cancellationToken)
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
                    await ListUsersAsync(cancellationToken);
                    break;
                case "2":
                    await CreateUserInteractiveAsync(cancellationToken);
                    break;
                case "3":
                    await UpdateUserInteractiveAsync(cancellationToken);
                    break;
                case "4":
                    return;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }
        }
    }

    private static async Task<string> GetManagementApiAccessToken()
    {
        var client = new AuthenticationApiClient(new Uri($"https://{Domain}"));
        var tokenRequest = new ClientCredentialsTokenRequest
        {
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            Audience = $"https://{Domain}/api/v2/"
        };
        var tokenResponse = await client.GetTokenAsync(tokenRequest);
        return tokenResponse.AccessToken;
    }

    private static async Task<ManagementApiClient> GetManagementClientAsync()
    {
        var token = await GetManagementApiAccessToken();
        return new ManagementApiClient(token, new Uri($"https://{Domain}/api/v2"));
    }

    private static async Task ListUsersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = await GetManagementClientAsync();
            int page = 0;
            int pageSize = 10;
            while (true)
            {
                var users = await client.Users.GetAllAsync(new GetUsersRequest(), new Auth0.ManagementApi.Paging.PaginationInfo(page, pageSize, true), cancellationToken);
                if (users.Count == 0)
                {
                    Console.WriteLine("No more users.");
                    break;
                }
                Console.WriteLine("\n| {0,-24} | {1,-30} | {2,-20} |", "User ID", "Email", "Name");
                Console.WriteLine(new string('-', 82));
                foreach (var user in users)
                {
                    Console.WriteLine("| {0,-24} | {1,-30} | {2,-20} |", user.UserId, user.Email, $"{user.FirstName} {user.LastName}");
                }
                Console.WriteLine($"-- Page {page + 1} --");
                Console.Write("n=next, p=prev, q=quit: ");
                var nav = Console.ReadLine();
                if (nav == "n") page++;
                else if (nav == "p" && page > 0) page--;
                else break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error listing users.");
        }
    }

    private static async Task CreateUserInteractiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = await GetManagementClientAsync();
            Console.Write("Email: ");
            var email = Console.ReadLine();
            Console.Write("Password: ");
            var password = Console.ReadLine();
            Console.Write("First Name: ");
            var firstName = Console.ReadLine();
            Console.Write("Last Name: ");
            var lastName = Console.ReadLine();
            var request = new UserCreateRequest
            {
                Email = email,
                Password = password,
                Connection = ConnectionName,
                FirstName = firstName,
                LastName = lastName,
                EmailVerified = false
            };
            var user = await client.Users.CreateAsync(request, cancellationToken);
            Console.WriteLine($"User created with ID: {user.UserId}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating user.");
        }
    }

    private static async Task UpdateUserInteractiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = await GetManagementClientAsync();
            Console.Write("User ID to update: ");
            var userId = Console.ReadLine();
            Console.Write("New Email (leave blank to skip): ");
            var email = Console.ReadLine();
            Console.Write("New First Name (leave blank to skip): ");
            var firstName = Console.ReadLine();
            Console.Write("New Last Name (leave blank to skip): ");
            var lastName = Console.ReadLine();
            var request = new UserUpdateRequest();
            if (!string.IsNullOrWhiteSpace(email)) request.Email = email;
            if (!string.IsNullOrWhiteSpace(firstName)) request.FirstName = firstName;
            if (!string.IsNullOrWhiteSpace(lastName)) request.LastName = lastName;
            var updatedUser = await client.Users.UpdateAsync(userId, request, cancellationToken);
            Console.WriteLine($"User updated: {updatedUser.Email}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating user.");
        }
    }
    // For future: Add unit tests for input validation and service logic.
}