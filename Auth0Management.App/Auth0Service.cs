using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Auth0Management.App
{
    internal sealed class Auth0Service : IDisposable
    {
        private readonly Auth0Options _options;
        private readonly ILogger<Auth0Service> _logger;
        private readonly SemaphoreSlim _rateLimiter;
        private readonly int _intervalMs;
        private DateTime _lastRequestTime;

        private string? _cachedToken;
        private DateTime _tokenExpiration;

        public Auth0Service(IOptions<Auth0Options> options, ILogger<Auth0Service> logger)
        {
            _options = options.Value;
            _logger = logger;
            _rateLimiter = new SemaphoreSlim(_options.RequestsPerSecond, _options.RequestsPerSecond);
            _intervalMs = 1000 / Math.Max(1, _options.RequestsPerSecond);
            _lastRequestTime = DateTime.MinValue;
        }

        private async Task RateLimitAsync(CancellationToken cancellationToken)
        {
            if (_options.RequestsPerSecond <= 0)
            {
                throw new InvalidOperationException("RequestsPerSecond must be greater than zero.");
            }

            await _rateLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastRequestTime).TotalMilliseconds;
                if (elapsed < _intervalMs)
                {
                    _logger.LogDebug("Rate limiting: delaying for {DelayMs}ms", _intervalMs - elapsed);
                    await Task.Delay(_intervalMs - (int)elapsed, cancellationToken).ConfigureAwait(false);
                }
                _lastRequestTime = DateTime.UtcNow;
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        private async Task<T> WithRateLimitAsync<T>(Func<Task<T>> func, CancellationToken cancellationToken)
        {
            await RateLimitAsync(cancellationToken).ConfigureAwait(false);
            return await func().ConfigureAwait(false);
        }

        private async Task WithRateLimitAsync(Func<Task> func, CancellationToken cancellationToken)
        {
            await RateLimitAsync(cancellationToken).ConfigureAwait(false);
            await func().ConfigureAwait(false);
        }

        private string Domain => _options.Domain.Replace("https://", "");
        private string ClientId => _options.ClientId;
        private string ClientSecret => _options.ClientSecret;

        private async Task<string> GetManagementApiAccessToken(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiration)
            {
                return _cachedToken;
            }

            return await WithRateLimitAsync(async () =>
            {
                var client = new AuthenticationApiClient(new Uri($"https://{Domain}"));
                var tokenRequest = new ClientCredentialsTokenRequest
                {
                    ClientId = ClientId,
                    ClientSecret = ClientSecret,
                    Audience = $"https://{Domain}/api/v2/"
                };
                var tokenResponse = await client.GetTokenAsync(tokenRequest, cancellationToken).ConfigureAwait(false);
                _cachedToken = tokenResponse.AccessToken;
                _tokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60); // Buffer for safety
                return _cachedToken;
            }, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ManagementApiClient> GetManagementClientAsync(CancellationToken cancellationToken)
        {
            var token = await GetManagementApiAccessToken(cancellationToken).ConfigureAwait(false);
            return new ManagementApiClient(token, new Uri($"https://{Domain}/api/v2"));
        }

        public async Task ListUsersAsync(CancellationToken cancellationToken)
        {
            await WithRateLimitAsync(async () =>
            {
                try
                {
                    var client = await GetManagementClientAsync(cancellationToken).ConfigureAwait(false);
                    int page = 0;
                    int pageSize = 10;
                    while (true)
                    {
                        var users = await client.Users.GetAllAsync(new GetUsersRequest(), new Auth0.ManagementApi.Paging.PaginationInfo(page, pageSize, true), cancellationToken).ConfigureAwait(false);
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
            }, cancellationToken).ConfigureAwait(false);
        }

        public async Task CreateUserInteractiveAsync(CancellationToken cancellationToken)
        {
            await WithRateLimitAsync(async () =>
            {
                try
                {
                    var client = await GetManagementClientAsync(cancellationToken).ConfigureAwait(false);
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
                    var user = await client.Users.CreateAsync(request, cancellationToken).ConfigureAwait(false);
                    Console.WriteLine($"User created with ID: {user.UserId}");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error creating user.");
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        public async Task UpdateUserInteractiveAsync(CancellationToken cancellationToken)
        {
            await WithRateLimitAsync(async () =>
            {
                try
                {
                    var client = await GetManagementClientAsync(cancellationToken).ConfigureAwait(false);
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
                    var updatedUser = await client.Users.UpdateAsync(userId, request, cancellationToken).ConfigureAwait(false);
                    Console.WriteLine($"User updated: {updatedUser.Email}");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error updating user.");
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _rateLimiter?.Dispose();
        }
    }
}
