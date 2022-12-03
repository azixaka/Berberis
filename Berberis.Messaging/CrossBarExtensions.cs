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
                    writer.WriteString(useMnemonics ? "CfIn" : "ConflationInterval", subscription.ConflationInterval.ToString());

                    var stats = subscription.Statistics.GetStats();

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
                    WriteFloatingPointNumber(writer, useMnemonics ? "AvgSvc" : "AvgServiceTimeMs", stats.AvgServiceTimeMs);
                    WriteFloatingPointNumber(writer, useMnemonics ? "AvgRsp" : "AvgResponseTimeMs", stats.AvgResponseTime);
                    WriteFloatingPointNumber(writer, useMnemonics ? "P90Lat" : "P90LatencyTimeMs", stats.P90LatencyTimeMs);
                    WriteFloatingPointNumber(writer, useMnemonics ? "P90Lat" : "P90ServiceTimeMs", stats.P90ServiceTimeMs);

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