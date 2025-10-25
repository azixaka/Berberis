# Berberis Recorder CLI

Command-line tool for working with Berberis message recordings.

## Features

The CLI provides type-agnostic operations on recording files without requiring compile-time knowledge of message body types:

- **info** - Display recording metadata and statistics
- **build-index** - Create index files for fast seeking
- **verify** - Check recording integrity

## Installation

Build the CLI tool:

```bash
dotnet build tools/Berberis.Recorder.Cli/Berberis.Recorder.Cli.csproj
```

Or run directly:

```bash
dotnet run --project tools/Berberis.Recorder.Cli/Berberis.Recorder.Cli.csproj -- [command] [options]
```

## Commands

### info

Display information about a recording file.

```bash
berberis-recorder info <recording-file>
```

**Examples:**

```bash
# Show metadata for a recording
berberis-recorder info recording.bin

# If metadata file exists (recording.bin.meta.json), displays:
Recording: recording.bin
Created: 2025-10-25 18:00:00 UTC
Channel: trade.prices
Serializer: JsonSerializer v1
Message Type: StockPrice
Message Count: 1,000,000
Duration: 01:30:00

# If no metadata, scans the recording:
Recording: recording.bin
No metadata file found (.meta.json)

File size: 524,288,000 bytes (500.00 MB)
Last modified: 2025-10-25 18:00:00

Scanning recording (without metadata)...
Message Count: 1,000,000
Duration: 01:30:00
```

### build-index

Build an index file for fast seeking in large recordings.

```bash
berberis-recorder build-index <recording-file> <index-file> [--interval N]
```

**Options:**

- `--interval N` - Index every Nth message (default: 1000)

**Examples:**

```bash
# Build index with default interval (every 1000th message)
berberis-recorder build-index recording.bin recording.bin.idx

# Build index with custom interval (every 5000th message)
berberis-recorder build-index recording.bin recording.bin.idx --interval 5000

# Output:
Building index for: recording.bin
Index file: recording.bin.idx
Interval: 1000 messages

Progress: 100%
Indexed 1,000 entries from 1,000,000 messages

Index built successfully!
```

### verify

Verify recording integrity by checking all messages can be read.

```bash
berberis-recorder verify <recording-file>
```

**Examples:**

```bash
# Verify recording integrity
berberis-recorder verify recording.bin

# Output:
Verifying recording: recording.bin

Verified 1,000,000 messages successfully (524,288,000 bytes)

Recording is valid!

# If corruption detected:
Verifying recording: corrupted.bin

Corruption detected at message #45,231, byte offset 123456789
Error: Recording corrupted: Invalid message framing
```

## Implementation Notes

### Type-Agnostic Operations

The CLI operates directly on the binary recording format without requiring knowledge of the message body type (`TBody`). This is achieved by:

1. **MessageChunkReader** - Reads message headers and raw body bytes without deserialization
2. **Binary format parsing** - Extracts metadata (timestamp, key, from) from message headers
3. **Stream-based processing** - Works with recording streams without loading into memory

### Limitations

Operations that require deserializing message bodies (e.g., filtering by body content, transforming messages) cannot be performed by the CLI. For these operations, use the Berberis.Messaging API directly in your application code.

### Performance

- Zero-allocation message reading (uses `ArrayPool<byte>`)
- Streaming processing for large files
- Progress reporting every 1000 messages

## Related Documentation

- [Recorder Documentation](../../docs/Recorder.md) - Complete guide to the Berberis recording system
- [README](../../README.md) - Main Berberis documentation
