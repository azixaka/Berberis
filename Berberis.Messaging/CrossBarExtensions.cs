using System.Text.Json;

namespace Berberis.Messaging
{
    public static class CrossBarExtensions
    {
        private const string DateTimeFormat = "dd/MM/yyyy HH:mm:ss.fff";

        public static void MetricsToJson(this ICrossBar crossBar, Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Channels");
            writer.WriteStartArray();

            foreach (var channel in crossBar.GetChannels())
            {
                writer.WriteStartObject();
                writer.WriteString("Channel", channel.Name);
                writer.WriteString("Type", channel.BodyType.Name);
                writer.WriteString("LastBy", channel.LastPublishedBy);
                writer.WriteString("LastAt", channel.LastPublishedAt.ToString(DateTimeFormat));

                var channelStats = channel.Statistics.GetStats();
                WriteNumber(writer, "IMs", channelStats.IntervalMs);
                WriteNumber(writer, "Mps", channelStats.MessagesPerSecond);
                WriteNumber(writer, "Msgs", channelStats.TotalMessages);
                WriteNumber(writer, "AvgMsgs", channelStats.PublishRate);

                writer.WritePropertyName("Subscriptions");
                writer.WriteStartArray();

                foreach (var subscription in crossBar.GetChannelSubscriptions(channel.Name))
                {
                    writer.WriteStartObject();
                    writer.WriteString("Name", subscription.Name);
                    writer.WriteString("SubscribedOn", subscription.SubscribedOn.ToString(DateTimeFormat));
                    writer.WriteString("ConflationInterval", subscription.ConflationInterval.ToString());

                    var stats = subscription.Statistics.GetStats();

                    WriteNumber(writer, "ConflationRatio", stats.ConflationRate);
                    WriteNumber(writer, "LatencyToResponseRatio", stats.LatencyToResponseTimeRatio);

                    WriteNumber(writer, "IMs", stats.IntervalMs);
                    WriteNumber(writer, "MpsIn", stats.MessagesPerSecondEnqueue);
                    WriteNumber(writer, "MpsOut", stats.MessagesPerSecondDequeued);
                    WriteNumber(writer, "DequeueRate", stats.DequeueRate);
                    WriteNumber(writer, "EstimatedAvgActiveMessagesDequeue", stats.EstimatedAvgActiveMessagesDequeue);

                    WriteNumber(writer, "MpsProcess", stats.MessagesPerSecondProcessed);
                    WriteNumber(writer, "ProcessRate", stats.ProcessRate);
                    WriteNumber(writer, "EstimatedAvgActiveMessagesProcess", stats.EstimatedAvgActiveMessagesProcess);
                    WriteNumber(writer, "MsgsIn", stats.TotalEnqueuedMessages);
                    WriteNumber(writer, "MsgsOut", stats.TotalDequeuedMessages);
                    WriteNumber(writer, "MsgsProcess", stats.TotalProcessedMessages);
                    WriteNumber(writer, "Queue", stats.QueueLength);
                    WriteNumber(writer, "AvgLat", stats.AvgLatencyTimeMs);
                    WriteNumber(writer, "AvgAllLat", stats.AvgAllLatencyTimeMs);
                    WriteNumber(writer, "AvgSvc", stats.AvgServiceTimeMs);
                    WriteNumber(writer, "AvgAllSvc", stats.AvgAllServiceTimeMs);

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();

            static void WriteNumber(Utf8JsonWriter writer, string name, float value)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    writer.WriteNull(name);
                else
                    writer.WriteNumber(name, value);
            }
        }
    }
}