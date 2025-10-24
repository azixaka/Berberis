using System.Text.Json;

namespace Berberis.Messaging;

/// <summary>CrossBar extension methods.</summary>
public static class CrossBarExtensions
{
    private const string DateTimeFormat = "dd/MM/yyyy HH:mm:ss.fff";

    /// <summary>Writes metrics to JSON.</summary>
    public static void MetricsToJson(this ICrossBar crossBar, Utf8JsonWriter writer, bool useMnemonics = false, bool resetStats = true)
    {
        writer.WriteStartObject();

        // Channels array
        writer.WritePropertyName(useMnemonics ? "Chs" : "Channels");
        writer.WriteStartArray();

        var subscriptions = new Dictionary<string, (CrossBar.SubscriptionInfo subscription, List<string> channels)>();

        foreach (var channel in crossBar.GetChannels())
        {
            writer.WriteStartObject();

            writer.WriteString(useMnemonics ? "Ch" : "Channel", channel.Name);
            writer.WriteString(useMnemonics ? "Tp" : "MessageBodyType", channel.BodyType.FullName);
            writer.WriteString(useMnemonics ? "PubBy" : "LastPublishedBy", channel.LastPublishedBy);
            writer.WriteString(useMnemonics ? "PubAt" : "LastPublishedAt", channel.LastPublishedAt.ToString(DateTimeFormat));

            var channelStats = channel.Statistics.GetStats(resetStats);
            WriteFloatingPointNumber(writer, useMnemonics ? "InMs" : "IntervalMs", channelStats.IntervalMs, 2);
            WriteFloatingPointNumber(writer, useMnemonics ? "Rt" : "PublishRate", channelStats.PublishRate, 2);
            writer.WriteNumber(useMnemonics ? "TMsg" : "TotalMessages", channelStats.TotalMessages);
            writer.WriteNumber(useMnemonics ? "StCnt" : "StoredMessageCount", channel.StoredMessageCount);

            writer.WriteEndObject();

            foreach (var subscription in crossBar.GetChannelSubscriptions(channel.Name) ?? [])
            {
                if (!subscriptions.ContainsKey(subscription.Name))
                {
                    var channels = new List<string>
                    {
                        channel.Name
                    };

                    subscriptions[subscription.Name] = (subscription, channels);
                }
                else
                {
                    var subPair = subscriptions[subscription.Name];
                    subPair.channels.Add(channel.Name);
                }
            }
        }

        writer.WriteEndArray();

        // Subscriptions array
        writer.WritePropertyName(useMnemonics ? "Sbs" : "Subscriptions");
        writer.WriteStartArray();

        foreach (var (subName, (subscription, channels)) in subscriptions)
        {
            writer.WriteStartObject();
            writer.WriteString(useMnemonics ? "Nm" : "Name", subscription.Name);
            writer.WriteString(useMnemonics ? "SubAt" : "SubscribedAt", subscription.SubscribedOn.ToString(DateTimeFormat));

            writer.WritePropertyName(useMnemonics ? "Sbs" : "Subscriptions");
            writer.WriteStartArray();

            foreach (var channelName in channels)
            {
                writer.WriteStringValue(channelName);
            }

            writer.WriteEndArray();

            if (subscription.ConflationInterval != TimeSpan.Zero)
                writer.WriteString(useMnemonics ? "CfIn" : "ConflationInterval", subscription.ConflationInterval.ToString());

            if (subscription.IsWildcard)
                writer.WriteString(useMnemonics ? "Exp" : "Expression", subscription.ChannelName);

            var stats = subscription.Statistics.GetStats(resetStats);

            if (subscription.ConflationInterval != TimeSpan.Zero)
                WriteFloatingPointNumber(writer, useMnemonics ? "CfRat" : "ConflationRatio", stats.ConflationRatio);

            WriteFloatingPointNumber(writer, useMnemonics ? "LatRsp" : "LatencyToResponseTimeRatio", stats.LatencyToResponseTimeRatio);

            WriteFloatingPointNumber(writer, useMnemonics ? "InMs" : "IntervalMs", stats.IntervalMs, 2);
            WriteFloatingPointNumber(writer, useMnemonics ? "DqRt" : "DequeueRate", stats.DequeueRate, 2);
            WriteFloatingPointNumber(writer, useMnemonics ? "PcRt" : "ProcessRate", stats.ProcessRate, 2);
            WriteFloatingPointNumber(writer, useMnemonics ? "EstAvgAMsg" : "EstimatedAvgActiveMessages", stats.EstimatedAvgActiveMessages);
            writer.WriteNumber(useMnemonics ? "TEqMsg" : "TotalEnqueuedMessages", stats.TotalEnqueuedMessages);
            writer.WriteNumber(useMnemonics ? "TDqMsg" : "TotalDequeuedMessages", stats.TotalDequeuedMessages);
            writer.WriteNumber(useMnemonics ? "TPcMsg" : "TotalProcessedMessages", stats.TotalProcessedMessages);
            writer.WriteNumber(useMnemonics ? "QLn" : "QueueLength", stats.QueueLength);
            WriteFloatingPointNumber(writer, useMnemonics ? "AvgLat" : "AvgLatencyTimeMs", stats.AvgLatencyTimeMs);
            WriteFloatingPointNumber(writer, useMnemonics ? "MinLat" : "MinLatencyTimeMs", stats.MinLatencyTimeMs);
            WriteFloatingPointNumber(writer, useMnemonics ? "MaxLat" : "MaxLatencyTimeMs", stats.MaxLatencyTimeMs);
            WriteFloatingPointNumber(writer, useMnemonics ? "AvgSvc" : "AvgServiceTimeMs", stats.AvgServiceTimeMs);
            WriteFloatingPointNumber(writer, useMnemonics ? "MinSvc" : "MinServiceTimeMs", stats.MinServiceTimeMs);
            WriteFloatingPointNumber(writer, useMnemonics ? "MaxSvc" : "MaxServiceTimeMs", stats.MaxServiceTimeMs);
            WriteFloatingPointNumber(writer, useMnemonics ? "AvgRsp" : "AvgResponseTimeMs", stats.AvgResponseTime);

            var pct = subscription.Statistics.StatsOptions.Percentile;

            if (pct.HasValue)
            {
                WriteFloatingPointNumber(writer, useMnemonics ? "StPct" : "StatsPercentile", pct.Value * 100, 0);
                WriteFloatingPointNumber(writer, useMnemonics ? "PctLat" : "PctLatencyTimeMs", stats.PercentileLatencyTimeMs);
                WriteFloatingPointNumber(writer, useMnemonics ? "PctSvc" : "PctServiceTimeMs", stats.PercentileServiceTimeMs);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteEndObject();

        static void WriteFloatingPointNumber(Utf8JsonWriter writer, string name, float value, int roundDigits = 6)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                writer.WriteNull(name);
            else
                writer.WriteNumber(name, Math.Round(value, roundDigits));
        }
    }
}