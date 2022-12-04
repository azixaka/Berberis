using System.Text.Json;

namespace Berberis.Messaging;

public static class CrossBarExtensions
{
    private const string DateTimeFormat = "dd/MM/yyyy HH:mm:ss.fff";

    public static void MetricsToJson(this ICrossBar crossBar, Utf8JsonWriter writer, bool useMnemonics = false)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(useMnemonics ? "Chs" : "Channels");
        writer.WriteStartArray();

        var visitedSubs = new HashSet<string>();

        foreach (var channel in crossBar.GetChannels())
        {
            writer.WriteStartObject();
            writer.WriteString(useMnemonics ? "Ch" : "Channel", channel.Name);
            writer.WriteString(useMnemonics ? "Tp" : "MessageBodyType", channel.BodyType.FullName);
            writer.WriteString(useMnemonics ? "PubBy" : "LastPublishedBy", channel.LastPublishedBy);
            writer.WriteString(useMnemonics ? "PubAt" : "LastPublishedAt", channel.LastPublishedAt.ToString(DateTimeFormat));

            var channelStats = channel.Statistics.GetStats();
            WriteFloatingPointNumber(writer, useMnemonics ? "InMs" : "IntervalMs", channelStats.IntervalMs, 2);
            WriteFloatingPointNumber(writer, useMnemonics ? "Rt" : "PublishRate", channelStats.PublishRate, 2);
            writer.WriteNumber(useMnemonics ? "TMsg" : "TotalMessages", channelStats.TotalMessages);

            writer.WritePropertyName(useMnemonics ? "Sbs" : "Subscriptions");
            writer.WriteStartArray();

            foreach (var subscription in crossBar.GetChannelSubscriptions(channel.Name))
            {
                if (!visitedSubs.Contains(subscription.Name))
                {
                    visitedSubs.Add(subscription.Name);

                    writer.WriteStartObject();
                    writer.WriteString(useMnemonics ? "Nm" : "Name", subscription.Name);
                    writer.WriteString(useMnemonics ? "SubOn" : "SubscribedOn", subscription.SubscribedOn.ToString(DateTimeFormat));

                    if (subscription.ConflationInterval == TimeSpan.Zero)
                        writer.WriteNull(useMnemonics ? "CfIn" : "ConflationInterval");
                    else
                        writer.WriteString(useMnemonics ? "CfIn" : "ConflationInterval", subscription.ConflationInterval.ToString());

                    writer.WriteString(useMnemonics ? "ChNm" : "ChannelName", subscription.ChannelName);
                    writer.WriteBoolean(useMnemonics ? "IsWld" : "IsWildcard", subscription.IsWildcard);

                    var stats = subscription.Statistics.GetStats();

                    WriteFloatingPointNumber(writer, useMnemonics ? "StPct" : "StatsPercentile", subscription.Statistics.StatsOptions.Percentile * 100, 0);

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
                    WriteFloatingPointNumber(writer, useMnemonics ? "PctLat" : "PctLatencyTimeMs", stats.PercentileLatencyTimeMs);
                    WriteFloatingPointNumber(writer, useMnemonics ? "PctSvc" : "PctServiceTimeMs", stats.PercentileServiceTimeMs);

                    writer.WriteEndObject();
                }
            }

            writer.WriteEndArray();
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