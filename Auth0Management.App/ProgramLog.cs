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

    public static void LogAppError(ILogger logger, Exception ex, string message)
    {
        _logAppError(logger, message, ex);
    }

    public static void LogOperationCanceled(ILogger logger, string message)
    {
        _logOperationCanceled(logger, message, null);
    }
}
