using Microsoft.Extensions.Logging;

internal static class ProgramLog
{
    private static readonly Action<ILogger, string, Exception> _logAppError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, nameof(LogAppError)),
            "{Message}");

    private static readonly Action<ILogger, string, Exception?> _logOperationCanceled =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, nameof(LogOperationCanceled)),
            "{Message}");

    private static readonly Action<ILogger, Exception?> _logListUsersError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(10, nameof(LogListUsersError)),
            "Error listing users.");

    private static readonly Action<ILogger, Exception?> _logCreateUserError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(11, nameof(LogCreateUserError)),
            "Error creating user.");

    private static readonly Action<ILogger, Exception?> _logUpdateUserError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(12, nameof(LogUpdateUserError)),
            "Error updating user.");

    private static readonly Action<ILogger, double, Exception?> _logRateLimitDelay =
        LoggerMessage.Define<double>(
            LogLevel.Debug,
            new EventId(20, nameof(LogRateLimitDelay)),
            "Rate limiting: delaying for {DelayMs}ms");

    public static void LogAppError(ILogger logger, Exception ex, string message)
    {
        _logAppError(logger, message, ex);
    }

    public static void LogOperationCanceled(ILogger logger, string message)
    {
        _logOperationCanceled(logger, message, null);
    }

    public static void LogListUsersError(ILogger logger, Exception ex)
    {
        _logListUsersError(logger, ex);
    }

    public static void LogCreateUserError(ILogger logger, Exception ex)
    {
        _logCreateUserError(logger, ex);
    }

    public static void LogUpdateUserError(ILogger logger, Exception ex)
    {
        _logUpdateUserError(logger, ex);
    }

    public static void LogRateLimitDelay(ILogger logger, double delayMs)
    {
        _logRateLimitDelay(logger, delayMs, null);
    }
}
