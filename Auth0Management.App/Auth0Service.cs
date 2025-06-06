using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Diagnostics;

namespace Auth0Management.App
{
    public class Auth0Service
    {
        private readonly Auth0Options _options;
        private readonly ILogger<Auth0Service> _logger;
        private readonly SemaphoreSlim _rateLimiter;
        private readonly int _intervalMs;
        private DateTime _lastRequestTime;

        public Auth0Service(IOptions<Auth0Options> options, ILogger<Auth0Service> logger)
        {
            _options = options.Value;
            _logger = logger;
            _rateLimiter = new SemaphoreSlim(_options.RequestsPerSecond, _options.RequestsPerSecond);
            _intervalMs = 1000 / Math.Max(1, _options.RequestsPerSecond);
            _lastRequestTime = DateTime.MinValue;
        }

        private async Task RateLimitAsync()
        {
            await _rateLimiter.WaitAsync();
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastRequestTime).TotalMilliseconds;
            if (elapsed < _intervalMs)
            {
                await Task.Delay(_intervalMs - (int)elapsed);
            }
            _lastRequestTime = DateTime.UtcNow;
            _rateLimiter.Release();
        }

        private async Task<T> WithRateLimitAsync<T>(Func<Task<T>> func)
        {
            await RateLimitAsync();
            return await func();
        }

        private async Task WithRateLimitAsync(Func<Task> func)
        {
            await RateLimitAsync();
            await func();
        }

        private string Domain => _options.Domain.Replace("https://", "");
        private string ClientId => _options.ClientId;
        private string ClientSecret => _options.ClientSecret;

        private async Task<string> GetManagementApiAccessToken()
        {
            return await WithRateLimitAsync(async () =>
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
            });
        }

        public async Task<ManagementApiClient> GetManagementClientAsync()
        {
            var token = await GetManagementApiAccessToken();
            return new ManagementApiClient(token, new Uri($"https://{Domain}/api/v2"));
        }

        public async Task ListUsersAsync(CancellationToken cancellationToken)
        {
            await WithRateLimitAsync(async () =>
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
            });
        }

        public async Task CreateUserInteractiveAsync(CancellationToken cancellationToken)
        {
            await WithRateLimitAsync(async () =>
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
                        Connection = _options.ConnectionName,
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
            });
        }

        public async Task UpdateUserInteractiveAsync(CancellationToken cancellationToken)
        {
            await WithRateLimitAsync(async () =>
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
            });
        }
    }
}
