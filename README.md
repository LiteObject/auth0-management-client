# Auth0 Management Client

This is a .NET console application for managing Auth0 users via the Auth0 Management API.

## Features
- List users
- Create user
- Update user

## Prerequisites
- .NET 9.0 SDK or later
- Auth0 account and Management API credentials

## Configuration

1. Copy `appsettings.template.json` to `appsettings.json` in the `Auth0Management.App` directory.
2. Fill in your Auth0 credentials in `appsettings.json`:

```json
{
  "Auth0": {
    "Domain": "YOUR_AUTH0_DOMAIN",
    "ClientId": "YOUR_AUTH0_CLIENT_ID",
    "ClientSecret": "YOUR_AUTH0_CLIENT_SECRET",
    "RequestsPerSecond": 10,
    "CircuitBreaker": {
      "Threshold": 5,
      "TimeoutMinutes": 1
    },
    "Retry": {
      "MaxAttempts": 3,
      "BaseDelayMs": 500
    }
  }
}
```

> **Note:** The application will fail to start if any required Auth0 configuration is missing. The `appsettings.json` file is automatically copied to the output directory on build.

## Build and Run

```sh
cd Auth0Management.App
# Build
 dotnet build
# Run
 dotnet run
```

## Options Validation
- The application uses data annotation attributes (e.g., `[Required]`) on `Auth0Options`.
- Validation is enforced at startup using `.AddOptions<T>().Bind(...).ValidateDataAnnotations()`.
- If a required value is missing, a clear error message is shown and the app will not start.

## Project Structure
- `Auth0Options.cs`: Strongly-typed configuration options with validation attributes.
- `Program.cs`: Main entry point, DI setup, and menu logic.
- `appsettings.json`: Configuration file (copied to output directory on build).

## Troubleshooting
- If you see an error about missing `appsettings.json`, ensure the file exists in the project root and is set to copy to output (see `.csproj`).
- If you see validation errors, check that all required Auth0 values are present in your configuration.

---

For more details, see the [Microsoft Docs on Options pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/options).