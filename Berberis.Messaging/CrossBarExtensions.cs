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

                writer.WritePropertyName("Subscriptions");
                writer.WriteStartArray();

                foreach (var subscription in crossBar.GetChannelSubscriptions(channel.Name))
                {
                    writer.WriteStartObject();
                    writer.WriteString("Name", subscription.Name);
                    writer.WriteString("SubscribedOn", subscription.SubscribedOn.ToString(DateTimeFormat));
                    writer.WriteString("ConflationInterval", subscription.ConflationInterval.ToString());

                    var stats = subscription.Statistics.GetStats();

                    var queueLen = stats.TotalMessagesIn - stats.TotalMessagesOut;

                    WriteNumber(writer, "IMs", stats.IntervalMs);
                    WriteNumber(writer, "MpsIn", stats.MessagesPerSecondIn);
                    WriteNumber(writer, "MpsOut", stats.MessagesPerSecondOut);
                    WriteNumber(writer, "MpsProcess", stats.MessagesPerSecondProcessed);
                    WriteNumber(writer, "MsgsIn", stats.TotalMessagesIn);
                    WriteNumber(writer, "MsgsOut", stats.TotalMessagesOut);
                    WriteNumber(writer, "MsgsProcess", stats.TotalMessagesProcessed);
                    WriteNumber(writer, "Queue", queueLen);
                    WriteNumber(writer, "AvgLat", stats.AvgLatencyTime);
                    WriteNumber(writer, "AvgSvc", stats.AvgServiceTime);
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