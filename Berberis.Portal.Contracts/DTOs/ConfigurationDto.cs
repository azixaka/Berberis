namespace Berberis.Portal.Contracts.DTOs;

public class ConfigurationDto
{
    public CrossBarConfigDto? CrossBarOptions { get; set; }
    public SubscriptionOptionsDto? DefaultSubscriptionOptions { get; set; }
    public StatsOptionsDto? DefaultStatsOptions { get; set; }
}

public class CrossBarConfigDto
{
    public int? DefaultBufferCapacity { get; set; }
    public string? DefaultSlowConsumerStrategy { get; set; }
    public string? DefaultConflationInterval { get; set; }
    public int? MaxChannels { get; set; }
    public int MaxChannelNameLength { get; set; }
    public bool EnableMessageTracing { get; set; }
    public bool EnableLifecycleTracking { get; set; }
    public bool EnablePublishLogging { get; set; }
    public string SystemChannelPrefix { get; set; } = "$";
    public int SystemChannelBufferCapacity { get; set; }
}

public class SubscriptionOptionsDto
{
    public string? HandlerTimeout { get; set; }
    public bool HasTimeoutCallback { get; set; }
}

public class StatsOptionsDto
{
    public float? Percentile { get; set; }
    public float Alpha { get; set; }
    public float Delta { get; set; }
    public int EwmaWindowSize { get; set; }
    public bool PercentileEnabled { get; set; }
}
