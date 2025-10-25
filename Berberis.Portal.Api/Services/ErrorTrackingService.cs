using Berberis.Portal.Contracts.DTOs;
using System.Collections.Concurrent;

namespace Berberis.Portal.Api.Services;

public class ErrorTrackingService
{
    private readonly ConcurrentQueue<ErrorInfoDto> _errors = new();
    private long _errorIdCounter;
    private readonly int _maxErrors;
    private readonly ILogger<ErrorTrackingService> _logger;

    public ErrorTrackingService(ILogger<ErrorTrackingService> logger, int maxErrors = 1000)
    {
        _logger = logger;
        _maxErrors = maxErrors;
    }

    public void TrackError(
        string errorType,
        string severity,
        string errorMessage,
        string? channelName = null,
        string? subscriptionId = null,
        string? stackTrace = null,
        Dictionary<string, string>? metadata = null)
    {
        var error = new ErrorInfoDto
        {
            Id = Interlocked.Increment(ref _errorIdCounter),
            Timestamp = DateTime.UtcNow,
            ErrorType = errorType,
            Severity = severity,
            ChannelName = channelName,
            SubscriptionId = subscriptionId,
            ErrorMessage = errorMessage,
            StackTrace = stackTrace,
            Metadata = metadata
        };

        _errors.Enqueue(error);

        while (_errors.Count > _maxErrors && _errors.TryDequeue(out _))
        {
        }

        _logger.LogDebug("Tracked error: {ErrorType} - {Message}", errorType, errorMessage);
    }

    public void TrackHandlerTimeout(string subscriptionId, string channelName, TimeSpan timeout, string handlerInfo)
    {
        TrackError(
            errorType: "HandlerTimeout",
            severity: "Warning",
            errorMessage: $"Handler exceeded timeout of {timeout.TotalMilliseconds}ms",
            channelName: channelName,
            subscriptionId: subscriptionId,
            metadata: new Dictionary<string, string>
            {
                ["TimeoutMs"] = timeout.TotalMilliseconds.ToString(),
                ["HandlerInfo"] = handlerInfo
            }
        );
    }

    public void TrackPublishFailure(string channelName, string reason, string? messageKey = null)
    {
        TrackError(
            errorType: "PublishFailure",
            severity: "Error",
            errorMessage: reason,
            channelName: channelName,
            metadata: messageKey != null ? new Dictionary<string, string> { ["MessageKey"] = messageKey } : null
        );
    }

    public void TrackTypeMismatch(string channelName, string expectedType, string actualType)
    {
        TrackError(
            errorType: "TypeMismatch",
            severity: "Error",
            errorMessage: $"Type mismatch: expected {expectedType}, got {actualType}",
            channelName: channelName,
            metadata: new Dictionary<string, string>
            {
                ["ExpectedType"] = expectedType,
                ["ActualType"] = actualType
            }
        );
    }

    public void TrackInvalidOperation(string operation, string reason, string? channelName = null, string? subscriptionId = null)
    {
        TrackError(
            errorType: "InvalidOperation",
            severity: "Warning",
            errorMessage: $"Invalid operation '{operation}': {reason}",
            channelName: channelName,
            subscriptionId: subscriptionId
        );
    }

    public ErrorLogDto GetErrors(string? errorTypeFilter = null, string? searchTerm = null, int limit = 100)
    {
        var errors = _errors.ToArray().Reverse().ToList();

        if (!string.IsNullOrWhiteSpace(errorTypeFilter))
        {
            errors = errors.Where(e => e.ErrorType.Equals(errorTypeFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            errors = errors.Where(e =>
                e.ErrorMessage.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (e.ChannelName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.SubscriptionId?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        var limitedErrors = errors.Take(limit).ToList();

        var allErrors = _errors.ToArray();
        var statistics = new ErrorStatisticsDto
        {
            TotalTimeouts = allErrors.Count(e => e.ErrorType == "HandlerTimeout"),
            TotalPublishFailures = allErrors.Count(e => e.ErrorType == "PublishFailure"),
            TotalTypeMismatches = allErrors.Count(e => e.ErrorType == "TypeMismatch"),
            TotalInvalidOperations = allErrors.Count(e => e.ErrorType == "InvalidOperation"),
            TotalOtherErrors = allErrors.Count(e =>
                e.ErrorType != "HandlerTimeout" &&
                e.ErrorType != "PublishFailure" &&
                e.ErrorType != "TypeMismatch" &&
                e.ErrorType != "InvalidOperation")
        };

        return new ErrorLogDto
        {
            Errors = limitedErrors,
            TotalErrors = errors.Count,
            Statistics = statistics
        };
    }

    public void ClearErrors()
    {
        while (_errors.TryDequeue(out _))
        {
        }

        _logger.LogInformation("Cleared all error logs");
    }

    public int GetErrorCount() => _errors.Count;
}
