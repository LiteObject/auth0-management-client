using System.ComponentModel.DataAnnotations;

namespace Auth0Management.App
{
    internal class Auth0Options
    {
        [Required(ErrorMessage = "Auth0 Domain is required. Please set 'Auth0:Domain' in configuration.")]
        public string Domain { get; set; } = null!;
        [Required(ErrorMessage = "Auth0 ClientId is required. Please set 'Auth0:ClientId' in configuration.")]
        public string ClientId { get; set; } = null!;
        [Required(ErrorMessage = "Auth0 ClientSecret is required. Please set 'Auth0:ClientSecret' in configuration.")]
        public string ClientSecret { get; set; } = null!;
        [Required(ErrorMessage = "Auth0 ConnectionName is required. Please set 'Auth0:ConnectionName' in configuration.")]
        public string ConnectionName { get; set; } = null!;
        public int RequestsPerSecond { get; set; }
        public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
        public RetryOptions Retry { get; set; } = new();

        internal class CircuitBreakerOptions
        {
            public int Threshold { get; set; }
            public int TimeoutMinutes { get; set; }
        }

        internal class RetryOptions
        {
            public int MaxAttempts { get; set; }
            public int BaseDelayMs { get; set; }
        }
    }
}
