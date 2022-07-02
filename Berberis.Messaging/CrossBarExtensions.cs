using System.Text.Json;

namespace Berberis.Messaging
{
    public static class CrossBarExtensions
    {
        private const string DateTimeFormat = "dd/MM/yyyy HH:mm:ss.fff";

        public static void MetricsToJson(this ICrossBar crossBar, Utf8JsonWriter writer, bool useMnemonics = false)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(useMnemonics ? "Chs" : "Channels");
            writer.WriteStartArray();

            foreach (var channel in crossBar.GetChannels())
            {
                writer.WriteStartObject();
                writer.WriteString(useMnemonics ? "Ch" : "Channel", channel.Name);
                writer.WriteString(useMnemonics ? "Tp" : "MessageBodyType", channel.BodyType.Name);
                writer.WriteString(useMnemonics ? "PubBy" : "LastPublishedBy", channel.LastPublishedBy);
                writer.WriteString(useMnemonics ? "PubAt" : "LastPublishedAt", channel.LastPublishedAt.ToString(DateTimeFormat));

                var channelStats = channel.Statistics.GetStats();
                WriteNumber(writer, useMnemonics ? "InMs" : "IntervalMs", channelStats.IntervalMs);
                WriteNumber(writer, useMnemonics ? "PRtIn" : "PublishRateInterval", channelStats.PublishRateInterval);
                WriteNumber(writer, useMnemonics ? "PRtLt" : "PublishRateLongTerm", channelStats.PublishRateLongTerm);
                WriteNumber(writer, useMnemonics ? "TMsg" : "TotalMessages", channelStats.TotalMessages);

                writer.WritePropertyName(useMnemonics ? "Sbs" : "Subscriptions");
                writer.WriteStartArray();

                foreach (var subscription in crossBar.GetChannelSubscriptions(channel.Name))
                {
                    writer.WriteStartObject();
                    writer.WriteString(useMnemonics ? "Nm" : "Name", subscription.Name);
                    writer.WriteString(useMnemonics ? "SubOn" : "SubscribedOn", subscription.SubscribedOn.ToString(DateTimeFormat));
                    writer.WriteString(useMnemonics ? "CfIn" : "ConflationInterval", subscription.ConflationInterval.ToString());

                    var stats = subscription.Statistics.GetStats();

                    WriteNumber(writer, useMnemonics ? "CfRtLt" : "ConflationRateLongTerm", stats.ConflationRateLongTerm);
                    WriteNumber(writer, useMnemonics ? "LatRspRatLt" : "LatencyToResponseTimeRatioLongTerm", stats.LatencyToResponseTimeRatioLongTerm);

                    WriteNumber(writer, useMnemonics ? "InMs" : "IntervalMs", stats.IntervalMs);
                    WriteNumber(writer, useMnemonics ? "EqRtIn" : "EnqueueRateInterval", stats.EnqueueRateInterval);
                    WriteNumber(writer, useMnemonics ? "DqRtIn" : "DequeueRateInterval", stats.DequeueRateInterval);
                    WriteNumber(writer, useMnemonics ? "DqRtLt" : "DequeueRateLongTerm", stats.DequeueRateLongTerm);
                    WriteNumber(writer, useMnemonics ? "PcRtIn" : "ProcessRateInterval", stats.ProcessRateInterval);
                    WriteNumber(writer, useMnemonics ? "PcRtLt" : "ProcessRateLongTerm", stats.ProcessRateLongTerm);
                    WriteNumber(writer, useMnemonics ? "EstAvgAMsg" : "EstimatedAvgActiveMessages", stats.EstimatedAvgActiveMessages);
                    WriteNumber(writer, useMnemonics ? "TEqMsg" : "TotalEnqueuedMessages", stats.TotalEnqueuedMessages);
                    WriteNumber(writer, useMnemonics ? "TDqMsg" : "TotalDequeuedMessages", stats.TotalDequeuedMessages);
                    WriteNumber(writer, useMnemonics ? "TPcMsg" : "TotalProcessedMessages", stats.TotalProcessedMessages);
                    WriteNumber(writer, useMnemonics ? "QLn" : "QueueLength", stats.QueueLength);
                    WriteNumber(writer, useMnemonics ? "AvgLatIn" : "AvgLatencyTimeMsInterval", stats.AvgLatencyTimeMsInterval);
                    WriteNumber(writer, useMnemonics ? "AvgLatLt" : "AvgLatencyTimeMsLongTerm", stats.AvgLatencyTimeMsLongTerm);
                    WriteNumber(writer, useMnemonics ? "AvgSvcIn" : "AvgServiceTimeMsInterval", stats.AvgServiceTimeMsInterval);
                    WriteNumber(writer, useMnemonics ? "AvgSvcLt" : "AvgServiceTimeMsLongTerm", stats.AvgServiceTimeMsLongTerm);

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