using Berberis.Messaging;
using Berberis.Messaging.Recorder;
using System.IO.Pipelines;

namespace Berberis.Recorder;

/// <summary>
/// Utility methods for manipulating recording files (merge, split, filter, convert).
/// </summary>
public static class RecordingUtilities
{
    /// <summary>
    /// Converts SerializerVersion to ushort for storage in metadata.
    /// </summary>
    private static ushort ToUInt16(SerializerVersion version) =>
        (ushort)((version.Major << 8) | version.Minor);

    /// <summary>
    /// Writes a message to a PipeWriter (non-async helper to avoid ref in async iterators).
    /// </summary>
    private static void WriteMessage<TBody>(
        PipeWriter writer,
        IMessageBodySerializer<TBody> serializer,
        Message<TBody> message)
    {
        var mutableMsg = message;
        var messageLengthSpan = MessageCodec.WriteChannelMessageHeader(writer, serializer.Version, ref mutableMsg);

        if (mutableMsg.MessageType == MessageType.ChannelUpdate)
        {
            serializer.Serialize(mutableMsg.Body!, writer);
        }

        MessageCodec.WriteMessageLengthPrefixAndSuffix(writer, messageLengthSpan);
    }

    /// <summary>
    /// Strategy for handling duplicate message IDs when merging recordings.
    /// </summary>
    public enum DuplicateStrategy
    {
        /// <summary>Keep the first occurrence of a duplicate message ID.</summary>
        KeepFirst,
        /// <summary>Keep the last occurrence of a duplicate message ID.</summary>
        KeepLast,
        /// <summary>Keep all messages, including duplicates.</summary>
        KeepAll
    }

    /// <summary>
    /// Criteria for splitting recordings.
    /// </summary>
    public enum SplitBy
    {
        /// <summary>Split by message count.</summary>
        MessageCount,
        /// <summary>Split by time duration (ticks).</summary>
        TimeDuration,
        /// <summary>Split by file size (approximate, in bytes).</summary>
        FileSize
    }

    /// <summary>
    /// Merges multiple recordings into a single recording, ordered by timestamp.
    /// </summary>
    /// <typeparam name="TBody">The message body type.</typeparam>
    /// <param name="inputPaths">Paths to the input recording files to merge.</param>
    /// <param name="outputPath">Path to the output merged recording file.</param>
    /// <param name="serializer">The message body serializer.</param>
    /// <param name="duplicateStrategy">Strategy for handling duplicate message IDs (default: KeepFirst).</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The merged recording metadata.</returns>
    public static async Task<RecordingMetadata> MergeAsync<TBody>(
        string[] inputPaths,
        string outputPath,
        IMessageBodySerializer<TBody> serializer,
        DuplicateStrategy duplicateStrategy = DuplicateStrategy.KeepFirst,
        IProgress<RecordingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (inputPaths == null || inputPaths.Length == 0)
            throw new ArgumentException("At least one input file is required", nameof(inputPaths));

        // Read metadata from all input files
        var metadataList = new List<RecordingMetadata?>();
        foreach (var path in inputPaths)
        {
            var metadataPath = RecordingMetadata.GetMetadataPath(path);
            var metadata = await RecordingMetadata.ReadAsync(metadataPath, cancellationToken);
            metadataList.Add(metadata);
        }

        // Open all input files and create players
        var streams = inputPaths.Select(p => File.OpenRead(p)).ToArray();
        var players = streams.Select(s => Player<TBody>.Create(s, serializer)).ToArray();
        var enumerators = players.Select(p => p.MessagesAsync(cancellationToken).GetAsyncEnumerator(cancellationToken)).ToArray();

        // Initialize by reading first message from each file
        var currentMessages = new (Message<TBody>? message, int sourceIndex)[enumerators.Length];
        for (int i = 0; i < enumerators.Length; i++)
        {
            currentMessages[i] = (await enumerators[i].MoveNextAsync() ? enumerators[i].Current : null, i);
        }

        // Track seen message IDs for duplicate handling
        var seenIds = duplicateStrategy == DuplicateStrategy.KeepAll ? null : new HashSet<long>();

        long totalMessages = 0;
        long firstTimestamp = 0;
        long lastTimestamp = 0;
        long bytesWritten = 0;

        try
        {
            await using var outputStream = File.Create(outputPath);
            var pipe = new Pipe();

            // Start pipe reader task
            var pipeReaderTask = Task.Run(async () =>
            {
                while (true)
                {
                    var result = await pipe.Reader.ReadAsync(cancellationToken);
                    var buffer = result.Buffer;

                    if (buffer.Length > 0)
                    {
                        foreach (var segment in buffer)
                        {
                            await outputStream.WriteAsync(segment, cancellationToken);
                            bytesWritten += segment.Length;
                        }
                    }

                    pipe.Reader.AdvanceTo(buffer.End);

                    if (result.IsCompleted)
                        break;
                }

                pipe.Reader.Complete();
            }, cancellationToken);

            // Merge messages in timestamp order
            while (currentMessages.Any(cm => cm.message != null))
            {
                // Find message with smallest timestamp
                var minIndex = -1;
                long minTimestamp = long.MaxValue;

                for (int i = 0; i < currentMessages.Length; i++)
                {
                    var currentMessage = currentMessages[i].message;
                    if (currentMessage.HasValue && currentMessage.Value.Timestamp < minTimestamp)
                    {
                        minTimestamp = currentMessage.Value.Timestamp;
                        minIndex = i;
                    }
                }

                if (minIndex == -1)
                    break;

                var msg = currentMessages[minIndex].message!.Value;

                // Handle duplicates
                bool shouldWrite = true;
                if (seenIds != null)
                {
                    if (seenIds.Contains(msg.Id))
                    {
                        shouldWrite = duplicateStrategy == DuplicateStrategy.KeepLast;
                        if (shouldWrite)
                            seenIds.Remove(msg.Id); // Will re-add below
                    }

                    if (shouldWrite)
                        seenIds.Add(msg.Id);
                }

                // Write message to output
                if (shouldWrite)
                {
                    WriteMessage(pipe.Writer, serializer, msg);
                    await pipe.Writer.FlushAsync(cancellationToken);

                    totalMessages++;

                    if (totalMessages == 1)
                        firstTimestamp = msg.Timestamp;
                    lastTimestamp = msg.Timestamp;

                    // Report progress
                    if (progress != null && totalMessages % 1000 == 0)
                    {
                        progress.Report(new RecordingProgress
                        {
                            MessagesProcessed = totalMessages,
                            BytesProcessed = bytesWritten
                        });
                    }
                }

                // Read next message from this file
                if (await enumerators[minIndex].MoveNextAsync())
                {
                    currentMessages[minIndex] = (enumerators[minIndex].Current, minIndex);
                }
                else
                {
                    currentMessages[minIndex] = (null, minIndex);
                }
            }

            // Complete pipe and wait for writer
            pipe.Writer.Complete();
            await pipeReaderTask;
        }
        finally
        {
            // Cleanup
            foreach (var enumerator in enumerators)
            {
                await enumerator.DisposeAsync();
            }
            foreach (var player in players)
            {
                player.Dispose();
            }
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }

        // Create merged metadata
        var mergedMetadata = new RecordingMetadata
        {
            CreatedUtc = DateTime.UtcNow,
            Channel = metadataList.FirstOrDefault(m => m?.Channel != null)?.Channel,
            SerializerType = metadataList.FirstOrDefault(m => m?.SerializerType != null)?.SerializerType,
            SerializerVersion = ToUInt16(serializer.Version),
            MessageType = metadataList.FirstOrDefault(m => m?.MessageType != null)?.MessageType,
            MessageCount = totalMessages,
            FirstMessageTicks = firstTimestamp,
            LastMessageTicks = lastTimestamp,
            DurationMs = totalMessages > 0 ? TimeSpan.FromTicks(lastTimestamp - firstTimestamp).Milliseconds : 0,
            Custom = new Dictionary<string, string>
            {
                ["mergedFrom"] = string.Join(", ", inputPaths.Select(Path.GetFileName)),
                ["duplicateStrategy"] = duplicateStrategy.ToString()
            }
        };

        // Write merged metadata
        var outputMetadataPath = RecordingMetadata.GetMetadataPath(outputPath);
        await RecordingMetadata.WriteAsync(mergedMetadata, outputMetadataPath, cancellationToken);

        return mergedMetadata;
    }

    /// <summary>
    /// Splits a recording into multiple smaller recordings based on specified criteria.
    /// </summary>
    /// <typeparam name="TBody">The message body type.</typeparam>
    /// <param name="inputPath">Path to the input recording file to split.</param>
    /// <param name="outputPathPattern">Pattern for output files (e.g., "recording_{0}.rec" where {0} is chunk number).</param>
    /// <param name="serializer">The message body serializer.</param>
    /// <param name="splitBy">Criteria for splitting.</param>
    /// <param name="splitValue">Value for split criteria (message count, ticks for duration, or bytes for file size).</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of metadata for each output chunk.</returns>
    public static async Task<RecordingMetadata[]> SplitAsync<TBody>(
        string inputPath,
        string outputPathPattern,
        IMessageBodySerializer<TBody> serializer,
        SplitBy splitBy,
        long splitValue,
        IProgress<RecordingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (splitValue <= 0)
            throw new ArgumentException("Split value must be positive", nameof(splitValue));

        // Read input metadata
        var inputMetadataPath = RecordingMetadata.GetMetadataPath(inputPath);
        var inputMetadata = await RecordingMetadata.ReadAsync(inputMetadataPath, cancellationToken);

        var chunks = new List<RecordingMetadata>();

        await using var inputStream = File.OpenRead(inputPath);
        var player = Player<TBody>.Create(inputStream, serializer);

        int chunkIndex = 0;
        Stream? currentOutputStream = null;
        Pipe? currentPipe = null;
        Task? currentPipeReaderTask = null;
        long currentChunkMessages = 0;
        long currentChunkBytes = 0;
        long currentChunkFirstTimestamp = 0;
        long currentChunkLastTimestamp = 0;
        long chunkStartTimestamp = 0;
        long totalProcessed = 0;

        try
        {
            await foreach (var msg in player.MessagesAsync(cancellationToken))
            {
                // Check if we need to start a new chunk
                bool needNewChunk = currentOutputStream == null;

                if (!needNewChunk)
                {
                    needNewChunk = splitBy switch
                    {
                        SplitBy.MessageCount => currentChunkMessages >= splitValue,
                        SplitBy.TimeDuration => (msg.Timestamp - chunkStartTimestamp) >= splitValue,
                        SplitBy.FileSize => currentChunkBytes >= splitValue,
                        _ => false
                    };
                }

                // Finalize previous chunk if needed
                if (needNewChunk && currentOutputStream != null)
                {
                    currentPipe!.Writer.Complete();
                    await currentPipeReaderTask!;

                    // Create metadata for this chunk
                    var chunkMetadata = CreateChunkMetadata(
                        inputMetadata,
                        ToUInt16(serializer.Version),
                        chunkIndex,
                        currentChunkMessages,
                        currentChunkFirstTimestamp,
                        currentChunkLastTimestamp);

                    chunks.Add(chunkMetadata);

                    // Write chunk metadata
                    var chunkPath = string.Format(outputPathPattern, chunkIndex);
                    var chunkMetadataPath = RecordingMetadata.GetMetadataPath(chunkPath);
                    await RecordingMetadata.WriteAsync(chunkMetadata, chunkMetadataPath, cancellationToken);

                    await currentOutputStream.DisposeAsync();
                    currentOutputStream = null;
                    currentPipe = null;
                    currentPipeReaderTask = null;
                    chunkIndex++;
                }

                // Start new chunk if needed
                if (currentOutputStream == null)
                {
                    var chunkPath = string.Format(outputPathPattern, chunkIndex);
                    currentOutputStream = File.Create(chunkPath);
                    currentPipe = new Pipe();
                    currentChunkMessages = 0;
                    currentChunkBytes = 0;
                    currentChunkFirstTimestamp = 0;
                    chunkStartTimestamp = msg.Timestamp;

                    var outputStream = currentOutputStream;
                    var pipe = currentPipe;

                    currentPipeReaderTask = Task.Run(async () =>
                    {
                        while (true)
                        {
                            var result = await pipe.Reader.ReadAsync(cancellationToken);
                            var buffer = result.Buffer;

                            if (buffer.Length > 0)
                            {
                                foreach (var segment in buffer)
                                {
                                    await outputStream.WriteAsync(segment, cancellationToken);
                                }
                            }

                            pipe.Reader.AdvanceTo(buffer.End);

                            if (result.IsCompleted)
                                break;
                        }

                        pipe.Reader.Complete();
                    }, cancellationToken);
                }

                // Write message to current chunk
                WriteMessage(currentPipe!.Writer, serializer, msg);
                await currentPipe.Writer.FlushAsync(cancellationToken);

                currentChunkMessages++;
                currentChunkBytes = currentPipe.Writer.UnflushedBytes;

                if (currentChunkMessages == 1)
                    currentChunkFirstTimestamp = msg.Timestamp;
                currentChunkLastTimestamp = msg.Timestamp;

                totalProcessed++;

                // Report progress
                if (progress != null && totalProcessed % 1000 == 0)
                {
                    progress.Report(new RecordingProgress
                    {
                        MessagesProcessed = totalProcessed,
                        BytesProcessed = inputStream.Position
                    });
                }
            }

            // Finalize last chunk
            if (currentOutputStream != null)
            {
                currentPipe!.Writer.Complete();
                await currentPipeReaderTask!;

                var chunkMetadata = CreateChunkMetadata(
                    inputMetadata,
                    ToUInt16(serializer.Version),
                    chunkIndex,
                    currentChunkMessages,
                    currentChunkFirstTimestamp,
                    currentChunkLastTimestamp);

                chunks.Add(chunkMetadata);

                var chunkPath = string.Format(outputPathPattern, chunkIndex);
                var chunkMetadataPath = RecordingMetadata.GetMetadataPath(chunkPath);
                await RecordingMetadata.WriteAsync(chunkMetadata, chunkMetadataPath, cancellationToken);

                await currentOutputStream.DisposeAsync();
            }
        }
        finally
        {
            player.Dispose();
        }

        return chunks.ToArray();
    }

    /// <summary>
    /// Filters messages from a recording based on a predicate.
    /// </summary>
    /// <typeparam name="TBody">The message body type.</typeparam>
    /// <param name="inputPath">Path to the input recording file.</param>
    /// <param name="outputPath">Path to the output filtered recording file.</param>
    /// <param name="serializer">The message body serializer.</param>
    /// <param name="predicate">Predicate to test each message (return true to include).</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The filtered recording metadata.</returns>
    public static async Task<RecordingMetadata> FilterAsync<TBody>(
        string inputPath,
        string outputPath,
        IMessageBodySerializer<TBody> serializer,
        Func<Message<TBody>, bool> predicate,
        IProgress<RecordingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Read input metadata
        var inputMetadataPath = RecordingMetadata.GetMetadataPath(inputPath);
        var inputMetadata = await RecordingMetadata.ReadAsync(inputMetadataPath, cancellationToken);

        long totalMessages = 0;
        long firstTimestamp = 0;
        long lastTimestamp = 0;
        long bytesProcessed = 0;
        long totalProcessed = 0;

        await using var inputStream = File.OpenRead(inputPath);
        await using var outputStream = File.Create(outputPath);

        var player = Player<TBody>.Create(inputStream, serializer);
        var pipe = new Pipe();

        // Start pipe reader task
        var pipeReaderTask = Task.Run(async () =>
        {
            while (true)
            {
                var result = await pipe.Reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (buffer.Length > 0)
                {
                    foreach (var segment in buffer)
                    {
                        await outputStream.WriteAsync(segment, cancellationToken);
                        bytesProcessed += segment.Length;
                    }
                }

                pipe.Reader.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                    break;
            }

            pipe.Reader.Complete();
        }, cancellationToken);

        try
        {
            await foreach (var msg in player.MessagesAsync(cancellationToken))
            {
                totalProcessed++;

                // Test predicate
                if (predicate(msg))
                {
                    // Write message to output
                    WriteMessage(pipe.Writer, serializer, msg);
                    await pipe.Writer.FlushAsync(cancellationToken);

                    totalMessages++;

                    if (totalMessages == 1)
                        firstTimestamp = msg.Timestamp;
                    lastTimestamp = msg.Timestamp;
                }

                // Report progress
                if (progress != null && totalProcessed % 1000 == 0)
                {
                    progress.Report(new RecordingProgress
                    {
                        MessagesProcessed = totalProcessed,
                        BytesProcessed = inputStream.Position
                    });
                }
            }

            // Complete pipe and wait for writer
            pipe.Writer.Complete();
            await pipeReaderTask;
        }
        finally
        {
            player.Dispose();
        }

        // Create filtered metadata
        var filteredMetadata = new RecordingMetadata
        {
            CreatedUtc = DateTime.UtcNow,
            Channel = inputMetadata?.Channel,
            SerializerType = inputMetadata?.SerializerType,
            SerializerVersion = ToUInt16(serializer.Version),
            MessageType = inputMetadata?.MessageType,
            MessageCount = totalMessages,
            FirstMessageTicks = firstTimestamp,
            LastMessageTicks = lastTimestamp,
            DurationMs = totalMessages > 0 ? TimeSpan.FromTicks(lastTimestamp - firstTimestamp).Milliseconds : 0,
            Custom = new Dictionary<string, string>
            {
                ["filteredFrom"] = Path.GetFileName(inputPath),
                ["totalInputMessages"] = totalProcessed.ToString(),
                ["filteredMessages"] = totalMessages.ToString()
            }
        };

        // Write filtered metadata
        var outputMetadataPath = RecordingMetadata.GetMetadataPath(outputPath);
        await RecordingMetadata.WriteAsync(filteredMetadata, outputMetadataPath, cancellationToken);

        return filteredMetadata;
    }

    /// <summary>
    /// Converts a recording from one serializer version to another.
    /// </summary>
    /// <typeparam name="TBody">The message body type.</typeparam>
    /// <param name="inputPath">Path to the input recording file.</param>
    /// <param name="outputPath">Path to the output converted recording file.</param>
    /// <param name="oldSerializer">The old serializer to read messages.</param>
    /// <param name="newSerializer">The new serializer to write messages.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The converted recording metadata.</returns>
    public static async Task<RecordingMetadata> ConvertAsync<TBody>(
        string inputPath,
        string outputPath,
        IMessageBodySerializer<TBody> oldSerializer,
        IMessageBodySerializer<TBody> newSerializer,
        IProgress<RecordingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Read input metadata
        var inputMetadataPath = RecordingMetadata.GetMetadataPath(inputPath);
        var inputMetadata = await RecordingMetadata.ReadAsync(inputMetadataPath, cancellationToken);

        long totalMessages = 0;
        long firstTimestamp = 0;
        long lastTimestamp = 0;
        long bytesProcessed = 0;

        await using var inputStream = File.OpenRead(inputPath);
        await using var outputStream = File.Create(outputPath);

        var player = Player<TBody>.Create(inputStream, oldSerializer);
        var pipe = new Pipe();

        // Start pipe reader task
        var pipeReaderTask = Task.Run(async () =>
        {
            while (true)
            {
                var result = await pipe.Reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (buffer.Length > 0)
                {
                    foreach (var segment in buffer)
                    {
                        await outputStream.WriteAsync(segment, cancellationToken);
                        bytesProcessed += segment.Length;
                    }
                }

                pipe.Reader.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                    break;
            }

            pipe.Reader.Complete();
        }, cancellationToken);

        try
        {
            await foreach (var msg in player.MessagesAsync(cancellationToken))
            {
                // Write message with new serializer
                WriteMessage(pipe.Writer, newSerializer, msg);
                await pipe.Writer.FlushAsync(cancellationToken);

                totalMessages++;

                if (totalMessages == 1)
                    firstTimestamp = msg.Timestamp;
                lastTimestamp = msg.Timestamp;

                // Report progress
                if (progress != null && totalMessages % 1000 == 0)
                {
                    progress.Report(new RecordingProgress
                    {
                        MessagesProcessed = totalMessages,
                        BytesProcessed = inputStream.Position
                    });
                }
            }

            // Complete pipe and wait for writer
            pipe.Writer.Complete();
            await pipeReaderTask;
        }
        finally
        {
            player.Dispose();
        }

        // Create converted metadata
        var convertedMetadata = new RecordingMetadata
        {
            CreatedUtc = DateTime.UtcNow,
            Channel = inputMetadata?.Channel,
            SerializerType = newSerializer.GetType().Name,
            SerializerVersion = ToUInt16(newSerializer.Version),
            MessageType = inputMetadata?.MessageType,
            MessageCount = totalMessages,
            FirstMessageTicks = firstTimestamp,
            LastMessageTicks = lastTimestamp,
            DurationMs = totalMessages > 0 ? TimeSpan.FromTicks(lastTimestamp - firstTimestamp).Milliseconds : 0,
            Custom = new Dictionary<string, string>
            {
                ["convertedFrom"] = Path.GetFileName(inputPath),
                ["oldSerializerVersion"] = ToUInt16(oldSerializer.Version).ToString(),
                ["newSerializerVersion"] = ToUInt16(newSerializer.Version).ToString()
            }
        };

        // Write converted metadata
        var outputMetadataPath = RecordingMetadata.GetMetadataPath(outputPath);
        await RecordingMetadata.WriteAsync(convertedMetadata, outputMetadataPath, cancellationToken);

        return convertedMetadata;
    }

    private static RecordingMetadata CreateChunkMetadata(
        RecordingMetadata? sourceMetadata,
        ushort serializerVersion,
        int chunkIndex,
        long messageCount,
        long firstTimestamp,
        long lastTimestamp)
    {
        return new RecordingMetadata
        {
            CreatedUtc = DateTime.UtcNow,
            Channel = sourceMetadata?.Channel,
            SerializerType = sourceMetadata?.SerializerType,
            SerializerVersion = serializerVersion,
            MessageType = sourceMetadata?.MessageType,
            MessageCount = messageCount,
            FirstMessageTicks = firstTimestamp,
            LastMessageTicks = lastTimestamp,
            DurationMs = messageCount > 0 ? TimeSpan.FromTicks(lastTimestamp - firstTimestamp).Milliseconds : 0,
            Custom = new Dictionary<string, string>
            {
                ["chunkIndex"] = chunkIndex.ToString(),
                ["splitFrom"] = sourceMetadata?.Custom?.GetValueOrDefault("originalFile") ?? "unknown"
            }
        };
    }
}
