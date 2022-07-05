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
                writer.WriteString(useMnemonics ? "Tp" : "MessageBodyType", channel.BodyType.FullName);
                writer.WriteString(useMnemonics ? "PubBy" : "LastPublishedBy", channel.LastPublishedBy);
                writer.WriteString(useMnemonics ? "PubAt" : "LastPublishedAt", channel.LastPublishedAt.ToString(DateTimeFormat));

                var channelStats = channel.Statistics.GetStats();
                WriteFloatingPointNumber(writer, useMnemonics ? "InMs" : "IntervalMs", channelStats.IntervalMs, 2);
                WriteFloatingPointNumber(writer, useMnemonics ? "PRtIn" : "PublishRateInterval", channelStats.PublishRateInterval, 2);
                WriteFloatingPointNumber(writer, useMnemonics ? "PRtLt" : "PublishRateLongTerm", channelStats.PublishRateLongTerm, 2);
                writer.WriteNumber(useMnemonics ? "TMsg" : "TotalMessages", channelStats.TotalMessages);

                writer.WritePropertyName(useMnemonics ? "Sbs" : "Subscriptions");
                writer.WriteStartArray();

                foreach (var subscription in crossBar.GetChannelSubscriptions(channel.Name))
                {
                    writer.WriteStartObject();
                    writer.WriteString(useMnemonics ? "Nm" : "Name", subscription.Name);
                    writer.WriteString(useMnemonics ? "SubOn" : "SubscribedOn", subscription.SubscribedOn.ToString(DateTimeFormat));
                    writer.WriteString(useMnemonics ? "CfIn" : "ConflationInterval", subscription.ConflationInterval.ToString());

                    var stats = subscription.Statistics.GetStats();

                    WriteFloatingPointNumber(writer, useMnemonics ? "CfRatLt" : "ConflationRatioLongTerm", stats.ConflationRatioLongTerm);
                    WriteFloatingPointNumber(writer, useMnemonics ? "LatRspRatLt" : "LatencyToResponseTimeRatioLongTerm", stats.LatencyToResponseTimeRatioLongTerm);

                    WriteFloatingPointNumber(writer, useMnemonics ? "InMs" : "IntervalMs", stats.IntervalMs, 2);
                    WriteFloatingPointNumber(writer, useMnemonics ? "EqRtIn" : "EnqueueRateInterval", stats.EnqueueRateInterval, 2);
                    WriteFloatingPointNumber(writer, useMnemonics ? "DqRtIn" : "DequeueRateInterval", stats.DequeueRateInterval, 2);
                    WriteFloatingPointNumber(writer, useMnemonics ? "DqRtLt" : "DequeueRateLongTerm", stats.DequeueRateLongTerm, 2);
                    WriteFloatingPointNumber(writer, useMnemonics ? "PcRtIn" : "ProcessRateInterval", stats.ProcessRateInterval, 2);
                    WriteFloatingPointNumber(writer, useMnemonics ? "PcRtLt" : "ProcessRateLongTerm", stats.ProcessRateLongTerm, 2);
                    WriteFloatingPointNumber(writer, useMnemonics ? "EstAvgAMsg" : "EstimatedAvgActiveMessages", stats.EstimatedAvgActiveMessages);
                    writer.WriteNumber(useMnemonics ? "TEqMsg" : "TotalEnqueuedMessages", stats.TotalEnqueuedMessages);
                    writer.WriteNumber(useMnemonics ? "TDqMsg" : "TotalDequeuedMessages", stats.TotalDequeuedMessages);
                    writer.WriteNumber(useMnemonics ? "TPcMsg" : "TotalProcessedMessages", stats.TotalProcessedMessages);
                    writer.WriteNumber(useMnemonics ? "QLn" : "QueueLength", stats.QueueLength);
                    WriteFloatingPointNumber(writer, useMnemonics ? "AvgLatIn" : "AvgLatencyTimeMsInterval", stats.AvgLatencyTimeMsInterval);
                    WriteFloatingPointNumber(writer, useMnemonics ? "AvgLatLt" : "AvgLatencyTimeMsLongTerm", stats.AvgLatencyTimeMsLongTerm);
                    WriteFloatingPointNumber(writer, useMnemonics ? "AvgSvcIn" : "AvgServiceTimeMsInterval", stats.AvgServiceTimeMsInterval);
                    WriteFloatingPointNumber(writer, useMnemonics ? "AvgSvcLt" : "AvgServiceTimeMsLongTerm", stats.AvgServiceTimeMsLongTerm);

                    writer.WriteEndObject();
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
}