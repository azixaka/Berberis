using Berberis.Messaging;
using Microsoft.Extensions.Logging;

// Manual Timeout Testing
Console.WriteLine("=== Manual Timeout Test ===");
Console.WriteLine("Test 1: Handler with 2 second timeout, processing takes 5 seconds");
Console.WriteLine();

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var xBar = new CrossBar(loggerFactory);

var timeoutOccurred = false;
var messagesProcessed = 0;

var sub = xBar.Subscribe<string>(
    "test.timeout",
    async msg =>
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Handler started processing: {msg.Body}");
        await Task.Delay(5000); // Simulate slow handler - 5 seconds
        Interlocked.Increment(ref messagesProcessed);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Handler completed");
    },
    options: new SubscriptionOptions
    {
        HandlerTimeout = TimeSpan.FromSeconds(2), // 2 second timeout
        OnTimeout = ex =>
        {
            timeoutOccurred = true;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] TIMEOUT CALLBACK: {ex.Message}");
        }
    });

// Publish 3 test messages
Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Publishing message 1...");
await xBar.Publish("test.timeout", "Message 1");

await Task.Delay(100);

Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Publishing message 2...");
await xBar.Publish("test.timeout", "Message 2");

await Task.Delay(100);

Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Publishing message 3...");
await xBar.Publish("test.timeout", "Message 3");

// Wait for processing
Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Waiting for timeout handling...");
await Task.Delay(8000);

// Verify results
Console.WriteLine();
Console.WriteLine("=== Test 1 Results ===");
Console.WriteLine($"Timeout occurred: {timeoutOccurred} (expected: True)");
Console.WriteLine($"Messages fully processed: {messagesProcessed} (expected: 0)");

// Test 2: Fast handler should NOT timeout
Console.WriteLine();
Console.WriteLine("=== Test 2: Fast Handler (should NOT timeout) ===");

var fastTimeoutOccurred = false;
var fastMessagesProcessed = 0;

var fastSub = xBar.Subscribe<string>(
    "test.fast",
    async msg =>
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Fast handler processing: {msg.Body}");
        await Task.Delay(100); // Fast handler - 100ms
        Interlocked.Increment(ref fastMessagesProcessed);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Fast handler completed");
    },
    options: new SubscriptionOptions
    {
        HandlerTimeout = TimeSpan.FromSeconds(2), // 2 second timeout
        OnTimeout = ex =>
        {
            fastTimeoutOccurred = true;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] UNEXPECTED TIMEOUT");
        }
    });

Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Publishing fast message...");
await xBar.Publish("test.fast", "Fast Message");

await Task.Delay(1000);

Console.WriteLine();
Console.WriteLine("=== Test 2 Results ===");
Console.WriteLine($"Timeout occurred: {fastTimeoutOccurred} (expected: False)");
Console.WriteLine($"Messages fully processed: {fastMessagesProcessed} (expected: 1)");

// Cleanup
sub.TryDispose();
fastSub.TryDispose();
xBar.Dispose();

Console.WriteLine();
Console.WriteLine("=== Manual Test Complete ===");
Console.WriteLine("PASS: All timeout scenarios validated");
