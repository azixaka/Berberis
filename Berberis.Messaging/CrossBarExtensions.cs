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
                writer.WriteString("MessageBodyType", channel.BodyType.Name);
                writer.WriteString("LastPublishedBy", channel.LastPublishedBy);
                writer.WriteString("LastPublishedAt", channel.LastPublishedAt.ToString(DateTimeFormat));

                var channelStats = channel.Statistics.GetStats();
                WriteNumber(writer, "IntervalMs", channelStats.IntervalMs);
                WriteNumber(writer, "PublishRateInterval", channelStats.PublishRateInterval);
                WriteNumber(writer, "PublishRateLongTerm", channelStats.PublishRateLongTerm);
                WriteNumber(writer, "TotalMessages", channelStats.TotalMessages);

                writer.WritePropertyName("Subscriptions");
                writer.WriteStartArray();

                foreach (var subscription in crossBar.GetChannelSubscriptions(channel.Name))
                {
                    writer.WriteStartObject();
                    writer.WriteString("Name", subscription.Name);
                    writer.WriteString("SubscribedOn", subscription.SubscribedOn.ToString(DateTimeFormat));
                    writer.WriteString("ConflationInterval", subscription.ConflationInterval.ToString());

                    var stats = subscription.Statistics.GetStats();

                    WriteNumber(writer, "ConflationRateLongTerm", stats.ConflationRateLongTerm);
                    WriteNumber(writer, "LatencyToResponseTimeRatioLongTerm", stats.LatencyToResponseTimeRatioLongTerm);

                    WriteNumber(writer, "IntervalMs", stats.IntervalMs);
                    WriteNumber(writer, "EnqueueRateInterval", stats.EnqueueRateInterval);
                    WriteNumber(writer, "DequeueRateInterval", stats.DequeueRateInterval);
                    WriteNumber(writer, "DequeueRateLongTerm", stats.DequeueRateLongTerm);
                    WriteNumber(writer, "ProcessRateInterval", stats.ProcessRateInterval);
                    WriteNumber(writer, "ProcessRateLongTerm", stats.ProcessRateLongTerm);
                    WriteNumber(writer, "EstimatedAvgActiveMessages", stats.EstimatedAvgActiveMessages);
                    WriteNumber(writer, "TotalEnqueuedMessages", stats.TotalEnqueuedMessages);
                    WriteNumber(writer, "TotalDequeuedMessages", stats.TotalDequeuedMessages);
                    WriteNumber(writer, "TotalProcessedMessages", stats.TotalProcessedMessages);
                    WriteNumber(writer, "QueueLength", stats.QueueLength);
                    WriteNumber(writer, "AvgLatencyTimeMsInterval", stats.AvgLatencyTimeMsInterval);
                    WriteNumber(writer, "AvgLatencyTimeMsLongTerm", stats.AvgLatencyTimeMsLongTerm);
                    WriteNumber(writer, "AvgServiceTimeMsInterval", stats.AvgServiceTimeMsInterval);
                    WriteNumber(writer, "AvgLatencyTimeMsLongTerm", stats.AvgLatencyTimeMsLongTerm);

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