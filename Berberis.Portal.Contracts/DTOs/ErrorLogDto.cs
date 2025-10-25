namespace Berberis.Portal.Contracts.DTOs;

public class ErrorLogDto
{
    public List<ErrorInfoDto> Errors { get; set; } = new();
    public int TotalErrors { get; set; }
    public ErrorStatisticsDto Statistics { get; set; } = new();
}

public class ErrorInfoDto
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ErrorType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? ChannelName { get; set; }
    public string? SubscriptionId { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class ErrorStatisticsDto
{
    public int TotalTimeouts { get; set; }
    public int TotalPublishFailures { get; set; }
    public int TotalTypeMismatches { get; set; }
    public int TotalInvalidOperations { get; set; }
    public int TotalOtherErrors { get; set; }
}
