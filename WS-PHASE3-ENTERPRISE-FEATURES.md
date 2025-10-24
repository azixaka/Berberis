# Agent Brief: Phase 3+ Enterprise Features for Berberis CrossBar

**Role:** Enterprise Features Development Agent
**Duration:** Phase 3 completion + Phase 4 enterprise features + optional enhancements
**Status:** not_started

---

## ⚠️ CHECKPOINT PROTOCOL - MANDATORY - READ THIS FIRST ⚠️

**STEP 1: CHECK FOR EXISTING CHECKPOINT (DO THIS BEFORE ANYTHING ELSE)**

```bash
cat .checkpoints/phase3-enterprise-checkpoint.md
```

**If the file EXISTS (you see checkpoint content):**
- ✅ **READ IT COMPLETELY** - Review all completed [x] and pending [ ] tasks
- ✅ **ADD ALL PENDING TASKS TO YOUR TODO LIST** - Not just the first one, ALL tasks marked with [ ]
- ✅ **CONTINUE FROM WHERE YOU LEFT OFF** - Resume work, don't restart from scratch
- ✅ **DO NOT OVERWRITE IT** - Never recreate the checkpoint if file exists
- ✅ **DO NOT START OVER** - You're resuming work, not starting fresh

**If the file DOES NOT EXIST (you see "No such file or directory"):**
- ✅ **CREATE THE DIRECTORY AND FILE NOW:**
```bash
mkdir -p .checkpoints
cat > .checkpoints/phase3-enterprise-checkpoint.md << 'EOF'
# Phase 3+ Enterprise Features Checkpoint

**Workstream:** Enterprise Features Development for Berberis CrossBar
**Status:** IN_PROGRESS
**Last Updated:** [timestamp]

## Tasks

### Phase 3: Enhanced Features (Priority: High Value)

#### 1. Configuration System (3 days)
- [ ] 1.1. Create CrossBarOptions.cs class with all configuration properties
- [ ] 1.2. Add DefaultBufferCapacity, DefaultHandlerTimeout, DefaultSlowConsumerStrategy
- [ ] 1.3. Add MaxChannels, MaxChannelNameLength, EnableMessageTracing, EnablePublishLogging
- [ ] 1.4. Add DefaultConflationInterval, SystemChannelPrefix, SystemChannelBufferCapacity
- [ ] 1.5. Modify CrossBar constructor to accept CrossBarOptions parameter (nullable, default to new())
- [ ] 1.6. Apply defaults in Subscribe() methods when parameters are null
- [ ] 1.7. Apply defaults in Publish() methods where applicable
- [ ] 1.8. Add validation for options (e.g., MaxChannels > 0 if set, etc.)
- [ ] 1.9. Create CrossBarOptionsTests.cs test file
- [ ] 1.10. Test defaults application in various scenarios
- [ ] 1.11. Test ASP.NET Core IOptions<CrossBarOptions> integration
- [ ] 1.12. Add XML documentation to CrossBarOptions class and all properties
- [ ] 1.13. Update README with configuration examples
- [ ] 1.14. Create configuration usage samples in SampleApp

#### 2. Health Check Support (2 days)
**Note:** Berberis already has extensive per-subscription and per-channel statistics. This task adds global/aggregate statistics at the CrossBar level for health monitoring.

- [ ] 2.1. Create ICrossBarHealthCheck.cs interface
- [ ] 2.2. Create HealthCheckResult.cs class with properties:
  - [ ] IsHealthy, Status, ActiveChannels, ActiveSubscriptions, FailedSubscriptions
  - [ ] TotalMessagesPublished, TotalMessagesProcessed, TotalTimeouts, TotalSkippedMessages
  - [ ] Diagnostics (Dictionary<string,string>), Warnings (List<string>)
- [ ] 2.3. Implement ICrossBarHealthCheck in CrossBar partial class
- [ ] 2.4. Add CheckHealth() method implementation
- [ ] 2.5. Add health criteria logic (e.g., < 10% failed subscriptions = healthy)
- [ ] 2.6. Add warning generation for high channel count, failed subs, timeouts
- [ ] 2.7. Add global/aggregate statistics tracking (CrossBar-level atomic counters)
  - [ ] _totalPublished - total messages published across all channels
  - [ ] _totalSkipped - total messages dropped/skipped system-wide
  - [ ] Aggregate timeouts/failures can be computed from existing per-subscription stats
- [ ] 2.8. Create CrossBarHealthCheck.cs for ASP.NET Core integration
- [ ] 2.9. Implement IHealthCheck interface wrapping ICrossBarHealthCheck
- [ ] 2.10. Create HealthCheckTests.cs test file
- [ ] 2.11. Test health check in various states (healthy, degraded, unhealthy)
- [ ] 2.12. Test ASP.NET Core health check endpoint integration
- [ ] 2.13. Add XML documentation to health check types
- [ ] 2.14. Update README with health check examples

#### 3. Batch Processing Support (3 days)
- [ ] 3.1. Add SubscribeBatch<TBody> method to ICrossBar interface
- [ ] 3.2. Define batch handler delegate: Func<IReadOnlyList<Message<TBody>>, ValueTask>
- [ ] 3.3. Add batchSize parameter (int, required)
- [ ] 3.4. Add batchTimeout parameter (TimeSpan, required)
- [ ] 3.5. Implement SubscribeBatch in CrossBar.cs
- [ ] 3.6. Create batch accumulation logic with thread-safe List + SemaphoreSlim
- [ ] 3.7. Implement batch flush on size threshold
- [ ] 3.8. Implement batch flush on timeout via Timer or PeriodicTimer
- [ ] 3.9. Handle edge cases (disposal during batch, cancellation, etc.)
- [ ] 3.10. Create BatchProcessingTests.cs test file
- [ ] 3.11. Test batch size triggers
- [ ] 3.12. Test batch timeout triggers
- [ ] 3.13. Test mixed size/timeout scenarios
- [ ] 3.14. Test concurrent batch subscriptions
- [ ] 3.15. Create BatchProcessingBenchmarks.cs benchmark file
- [ ] 3.16. Benchmark batch vs individual message throughput
- [ ] 3.17. Add XML documentation to batch methods
- [ ] 3.18. Update README with batch processing examples

**DESIGN NOTE: Allocation-Free Batch Processing Options**

🚨 **CRITICAL:** Berberis's core value proposition is the allocation-free hot path. Batch processing MUST maintain this guarantee.

**Problem:** Naive implementation allocates on every flush:
```csharp
// ❌ ALLOCATES - destroys performance!
private readonly List<Message<TBody>> _batch = new();
private async ValueTask FlushBatch()
{
    var batchToProcess = _batch.ToArray();  // NEW ARRAY EVERY FLUSH!
    _batch.Clear();
    await _batchHandler(batchToProcess);
}
```

**SOLUTION OPTIONS (Choose one or hybrid):**

**Option 1: ArrayPool + Custom BatchView Struct**
- Uses `ArrayPool<Message<TBody>>.Shared` - rent once, reuse forever
- Custom `BatchView<T>` struct with struct enumerator (no boxing)
- Zero allocations in Berberis core
- Async compatible
- Medium complexity

```csharp
// API
ISubscription SubscribeBatch<TBody>(
    string channel,
    int batchSize,
    TimeSpan batchTimeout,
    Func<BatchView<Message<TBody>>, ValueTask> handler);

// Implementation
internal class BatchSubscription<TBody>
{
    private readonly ArrayPool<Message<TBody>> _pool = ArrayPool<Message<TBody>>.Shared;
    private Message<TBody>[] _buffer;  // Rented once, reused
    private int _count;

    public BatchSubscription(int batchSize)
    {
        _buffer = _pool.Rent(batchSize);  // Rent once
    }

    private void FlushBatch()
    {
        // ✅ Zero allocations - struct wrapper
        var batch = new BatchView<Message<TBody>>(_buffer, _count);
        _batchHandler(batch);
        _count = 0;
    }

    public void Dispose() => _pool.Return(_buffer);
}

// Custom struct (no allocations!)
public readonly struct BatchView<T> : IReadOnlyList<T>
{
    private readonly T[] _buffer;
    private readonly int _count;

    internal BatchView(T[] buffer, int count)
    {
        _buffer = buffer;
        _count = count;
    }

    public T this[int index] => _buffer[index];
    public int Count => _count;

    // Struct enumerator - allocation-free foreach!
    public Enumerator GetEnumerator() => new Enumerator(_buffer, _count);

    public struct Enumerator
    {
        private readonly T[] _buffer;
        private readonly int _count;
        private int _index;

        internal Enumerator(T[] buffer, int count)
        {
            _buffer = buffer;
            _count = count;
            _index = -1;
        }

        public bool MoveNext() => ++_index < _count;
        public T Current => _buffer[_index];
    }
}
```

**Option 2: ReadOnlySpan<T> (Sync Only - Fastest)**
- Uses `ReadOnlySpan<T>` - true zero-allocation stack reference
- Fastest possible approach (no heap at all)
- **Limitation:** Handler MUST be synchronous (Span can't cross await)
- Low complexity

```csharp
// API (sync only!)
ISubscription SubscribeBatch<TBody>(
    string channel,
    int batchSize,
    TimeSpan batchTimeout,
    Action<ReadOnlySpan<Message<TBody>>> handler);

// Implementation
private void FlushBatch()
{
    // ✅ Zero allocations - span over existing buffer
    var batch = new ReadOnlySpan<Message<TBody>>(_buffer, 0, _count);
    _handler(batch);
    _count = 0;
}

// Usage
crossBar.SubscribeBatch<Order>("orders", 1000, TimeSpan.FromSeconds(1),
    batch =>  // ReadOnlySpan<Message<Order>>
    {
        foreach (var msg in batch)
            cache.Add(msg.Body);  // Must be sync!
    });
```

**Option 3: ReadOnlyMemory<T> (Async Compatible - Recommended)**
- Uses `ReadOnlyMemory<T>` - allocation-free wrapper
- Async compatible (can cross await boundaries)
- Built-in .NET type (no custom structs)
- Low complexity

```csharp
// API
ISubscription SubscribeBatch<TBody>(
    string channel,
    int batchSize,
    TimeSpan batchTimeout,
    Func<ReadOnlyMemory<Message<TBody>>, ValueTask> handler);

// Implementation
private async ValueTask FlushBatch()
{
    // ✅ Zero allocations - Memory<T> wraps buffer without copying
    var batch = new ReadOnlyMemory<Message<TBody>>(_buffer, 0, _count);
    await _handler(batch);
    _count = 0;
}

// Usage
crossBar.SubscribeBatch<Order>("orders", 1000, TimeSpan.FromSeconds(1),
    async batch =>  // ReadOnlyMemory<Message<Order>>
    {
        foreach (var msg in batch.Span)  // Span for iteration
            ProcessOrder(msg.Body);

        // Can await!
        await database.BulkInsert(batch.ToArray());  // Only allocates here if needed
    });
```

**RECOMMENDED APPROACH: Hybrid (Best of All Worlds)**

Provide BOTH sync and async variants:

```csharp
public interface ICrossBar
{
    // Sync variant - fastest (uses Span)
    ISubscription SubscribeBatch<TBody>(
        string channel,
        int batchSize,
        TimeSpan batchTimeout,
        Action<ReadOnlySpan<Message<TBody>>> handler,
        string? subscriptionName = null,
        CancellationToken token = default);

    // Async variant - flexible (uses Memory)
    ISubscription SubscribeBatch<TBody>(
        string channel,
        int batchSize,
        TimeSpan batchTimeout,
        Func<ReadOnlyMemory<Message<TBody>>, ValueTask> handler,
        string? subscriptionName = null,
        CancellationToken token = default);
}
```

**Performance Comparison:**

| Approach | Allocations | Async Support | Complexity | Performance |
|----------|-------------|---------------|------------|-------------|
| List + ToArray() | ❌ Every flush | ✅ Yes | Low | Slow |
| ArrayPool + BatchView | ✅ Zero | ✅ Yes | Medium | Fast |
| ReadOnlySpan<T> | ✅ Zero (stack) | ❌ Sync only | Low | Fastest |
| ReadOnlyMemory<T> | ✅ Zero | ✅ Yes | Low | Fast |
| **Hybrid (Span + Memory)** | ✅ Zero | ✅ Both | Medium | Best |

**Implementation Priority:**
1. Start with ReadOnlyMemory<T> (async, simple, allocation-free)
2. Add ReadOnlySpan<T> overload for sync hot paths
3. Benchmark both to prove allocation-free guarantee

**Buffer Management:**
- Use `ArrayPool<Message<TBody>>.Shared.Rent(batchSize)` once per subscription
- Reuse same buffer for all flushes
- Return to pool on disposal
- Clear buffer after flush (Array.Clear) to prevent memory leaks

**Key Insight:** User handler can still allocate (ToArray, ToList, etc.) if needed, but Berberis core remains allocation-free!

#### 4. Request-Reply Pattern (3 days)
- [ ] 4.1. Add Request<TRequest, TResponse> method to ICrossBar interface
- [ ] 4.2. Add Respond<TRequest, TResponse> method to ICrossBar interface
- [ ] 4.3. Implement Request method in CrossBar.cs
- [ ] 4.4. Create temporary reply channel ($reply.{correlationId})
- [ ] 4.5. Subscribe to reply channel with TaskCompletionSource
- [ ] 4.6. Publish request with correlationId
- [ ] 4.7. Implement timeout handling with CancellationTokenSource
- [ ] 4.8. Clean up reply subscription after completion/timeout
- [ ] 4.9. Implement Respond method in CrossBar.cs
- [ ] 4.10. Extract correlationId from request message
- [ ] 4.11. Publish response to $reply.{correlationId} channel
- [ ] 4.12. Create RequestReplyTests.cs test file
- [ ] 4.13. Test successful request-reply roundtrip
- [ ] 4.14. Test request timeout scenarios
- [ ] 4.15. Test multiple concurrent requests
- [ ] 4.16. Test responder failure handling
- [ ] 4.17. Test no responder scenarios
- [ ] 4.18. Create RequestReplyBenchmarks.cs benchmark file
- [ ] 4.19. Benchmark request-reply latency
- [ ] 4.20. Add XML documentation to request-reply methods
- [ ] 4.21. Update README with request-reply examples

#### 5. Dead Letter Queue Support (4 days)
- [ ] 5.1. Add DeadLetterQueue property to SubscriptionOptions
- [ ] 5.2. Add MaxRetries property to SubscriptionOptions (default: 3)
- [ ] 5.3. Add RetryDelay property to SubscriptionOptions (default: 1s)
- [ ] 5.4. Add RetryBackoffMultiplier property to SubscriptionOptions (default: 2.0)
- [ ] 5.5. Create DeadLetterMessage<TBody> class with properties:
  - [ ] OriginalMessage, FailureReason, FailureException (type name)
  - [ ] AttemptCount, FailedAt (DateTime), SubscriptionName, ChannelName
- [ ] 5.6. Modify Subscription.ProcessMessage to support retry logic
- [ ] 5.7. Create ProcessMessageWithRetry method
- [ ] 5.8. Implement retry loop with exponential backoff
- [ ] 5.9. Track retry attempts per message
- [ ] 5.10. Publish to DLQ after max retries exhausted
- [ ] 5.11. Add DLQ statistics tracking (messages sent to DLQ counter)
- [ ] 5.12. Create DeadLetterQueueTests.cs test file
- [ ] 5.13. Test successful retry after transient failure
- [ ] 5.14. Test max retries exhausted → DLQ
- [ ] 5.15. Test exponential backoff timing
- [ ] 5.16. Test DLQ message structure
- [ ] 5.17. Test DLQ subscription and monitoring
- [ ] 5.18. Test interaction with handler timeout
- [ ] 5.19. Add XML documentation to DLQ types
- [ ] 5.20. Update README with DLQ examples and best practices

### Phase 4: Enterprise Features (Priority: Advanced Capabilities)

#### 6. Circuit Breaker Pattern (1 week)
- [ ] 6.1. Create CircuitBreakerOptions class
- [ ] 6.2. Add FailureThreshold, ResetTimeout, HalfOpenMaxAttempts properties
- [ ] 6.3. Add OnCircuitOpen, OnCircuitClosed, OnCircuitHalfOpen callbacks
- [ ] 6.4. Create CircuitBreakerState enum (Closed, Open, HalfOpen)
- [ ] 6.5. Implement circuit breaker logic in Subscription
- [ ] 6.6. Track failure count and state transitions
- [ ] 6.7. Implement automatic open → half-open → closed transitions
- [ ] 6.8. Add circuit breaker state to subscription statistics
- [ ] 6.9. Create CircuitBreakerTests.cs test file
- [ ] 6.10. Test state transitions under various failure scenarios
- [ ] 6.11. Test automatic recovery
- [ ] 6.12. Add XML documentation
- [ ] 6.13. Update README with circuit breaker examples

#### 7. Message Priority Queues (1-2 weeks)
- [ ] 7.1. Create MessagePriority enum (Low, Normal, High, Critical)
- [ ] 7.2. Add Priority property to Message<TBody> struct
- [ ] 7.3. Add SupportPriority option to SubscriptionOptions
- [ ] 7.4. Modify Subscription to use PriorityQueue when SupportPriority = true
- [ ] 7.5. Maintain ordering within priority levels
- [ ] 7.6. Update Publish methods to accept priority parameter
- [ ] 7.7. Create PriorityQueueTests.cs test file
- [ ] 7.8. Test priority-based delivery order
- [ ] 7.9. Test within-priority FIFO ordering
- [ ] 7.10. Benchmark priority queue overhead
- [ ] 7.11. Add XML documentation
- [ ] 7.12. Update README with priority queue examples

#### 8. Message Compression (1 week)
- [ ] 8.1. Create CompressionOptions class
- [ ] 8.2. Add CompressionAlgorithm enum (None, Gzip, LZ4, Brotli)
- [ ] 8.3. Add MinSize threshold property
- [ ] 8.4. Add CompressionLevel property
- [ ] 8.5. Create IMessageCompressor interface
- [ ] 8.6. Implement GzipMessageCompressor
- [ ] 8.7. Implement LZ4MessageCompressor (if adding dependency is acceptable)
- [ ] 8.8. Add compression to Recorder/Player (primary use case)
- [ ] 8.9. Add compression support to message serialization
- [ ] 8.10. Create CompressionTests.cs test file
- [ ] 8.11. Test compression/decompression roundtrip
- [ ] 8.12. Test threshold-based compression
- [ ] 8.13. Benchmark compression overhead vs size savings
- [ ] 8.14. Add XML documentation
- [ ] 8.15. Update README with compression examples

#### 9. Message Persistence & Durability (3-4 weeks)
- [ ] 9.1. Design persistence architecture (WAL, snapshots, retention)
- [ ] 9.2. Create IPersistenceProvider interface
- [ ] 9.3. Create PersistenceOptions class
- [ ] 9.4. Implement FilePersistence provider
- [ ] 9.5. Add write-ahead log (WAL) implementation
- [ ] 9.6. Add snapshot support for stateful channels
- [ ] 9.7. Implement retention policies (time-based, size-based)
- [ ] 9.8. Add ReplayOnStartup option
- [ ] 9.9. Modify CrossBar to integrate persistence provider
- [ ] 9.10. Add persistence hooks to Publish
- [ ] 9.11. Implement replay logic on startup
- [ ] 9.12. Create PersistenceTests.cs test file
- [ ] 9.13. Test crash recovery scenarios
- [ ] 9.14. Test retention policy enforcement
- [ ] 9.15. Test snapshot creation and restoration
- [ ] 9.16. Benchmark persistence overhead
- [ ] 9.17. (Optional) Implement SQLite persistence provider
- [ ] 9.18. (Optional) Implement RocksDB persistence provider
- [ ] 9.19. Add XML documentation
- [ ] 9.20. Update README with persistence examples

#### 10. Distributed Messaging (Multi-Process) (2-3 months)
- [ ] 10.1. Design distributed architecture (cluster membership, leader election)
- [ ] 10.2. Create DistributedCrossBar class extending CrossBar
- [ ] 10.3. Create DistributedOptions class
- [ ] 10.4. Implement ITransport interface (abstraction for TCP/gRPC/etc.)
- [ ] 10.5. Implement TcpTransport
- [ ] 10.6. Implement gRPC transport (preferred for production)
- [ ] 10.7. Create IMessageSerializer interface
- [ ] 10.8. Implement MessagePackSerializer
- [ ] 10.9. Implement Protobuf serializer
- [ ] 10.10. Implement gossip protocol for cluster membership
- [ ] 10.11. Implement leader election for stateful channels
- [ ] 10.12. Add replication logic (ReplicationFactor)
- [ ] 10.13. Add partition tolerance handling
- [ ] 10.14. Create DistributedTests.cs test file
- [ ] 10.15. Test multi-node pub/sub
- [ ] 10.16. Test node failure scenarios
- [ ] 10.17. Test leader election
- [ ] 10.18. Test partition tolerance
- [ ] 10.19. Benchmark distributed latency vs local
- [ ] 10.20. Add XML documentation
- [ ] 10.21. Create comprehensive distributed deployment guide

### Phase 5: Optional Enhancements (Nice to Have)

#### 11. OpenTelemetry Integration (2 weeks)
- [ ] 11.1. Add OpenTelemetry.Api dependency
- [ ] 11.2. Create OpenTelemetryOptions class
- [ ] 11.3. Add EnableTracing, EnableMetrics, ServiceName properties
- [ ] 11.4. Implement distributed tracing spans for Publish/Subscribe
- [ ] 11.5. Add trace context propagation via Message metadata
- [ ] 11.6. Implement metrics export (counters, histograms)
- [ ] 11.7. Add metrics for: messages published, processed, latency, errors
- [ ] 11.8. Create OpenTelemetryTests.cs test file
- [ ] 11.9. Test trace creation and propagation
- [ ] 11.10. Test metrics collection
- [ ] 11.11. Add XML documentation
- [ ] 11.12. Create OpenTelemetry integration guide

#### 12. Schema Registry & Versioning (2-3 weeks)
- [ ] 12.1. Create ISchemaRegistry interface
- [ ] 12.2. Create SchemaVersion class
- [ ] 12.3. Add RegisterSchema method to ICrossBar
- [ ] 12.4. Implement schema storage (in-memory initially)
- [ ] 12.5. Add schema validation on Publish
- [ ] 12.6. Implement automatic schema migration logic
- [ ] 12.7. Add schema version to Message metadata
- [ ] 12.8. Create SchemaRegistryTests.cs test file
- [ ] 12.9. Test schema registration
- [ ] 12.10. Test version migration
- [ ] 12.11. Test backward/forward compatibility
- [ ] 12.12. Add XML documentation
- [ ] 12.13. Update README

#### 13. Admin Dashboard (1-2 months)
- [ ] 13.1. Design dashboard architecture (Blazor or React/Vue)
- [ ] 13.2. Create API endpoints for dashboard data
- [ ] 13.3. Implement real-time channel statistics endpoint
- [ ] 13.4. Implement subscription health endpoint
- [ ] 13.5. Implement message flow visualization data endpoint
- [ ] 13.6. Build frontend UI for channel list
- [ ] 13.7. Build frontend UI for subscription details
- [ ] 13.8. Build frontend UI for performance graphs
- [ ] 13.9. Build frontend UI for configuration management
- [ ] 13.10. Add authentication/authorization to dashboard
- [ ] 13.11. Create dashboard deployment guide
- [ ] 13.12. Add screenshot documentation

#### 14. Security & Authorization (3-4 weeks)
- [ ] 14.1. Design security model (RBAC, claims-based, etc.)
- [ ] 14.2. Create IChannelPolicy interface
- [ ] 14.3. Create ChannelPolicy class
- [ ] 14.4. Add SetChannelPolicy method to ICrossBar
- [ ] 14.5. Implement publisher authorization checks
- [ ] 14.6. Implement subscriber authorization checks
- [ ] 14.7. Add optional message encryption (AES-256)
- [ ] 14.8. Add optional message signing (HMAC)
- [ ] 14.9. Implement audit logging for sensitive channels
- [ ] 14.10. Create SecurityTests.cs test file
- [ ] 14.11. Test authorization enforcement
- [ ] 14.12. Test encryption/decryption
- [ ] 14.13. Test audit log generation
- [ ] 14.14. Add XML documentation
- [ ] 14.15. Create security best practices guide

### Phase 6: Out of Scope (Consider Only If Explicitly Requested)

**⚠️ WARNING: These features are considered OUT OF SCOPE for core CrossBar development.**

These features either:
- Overlap with existing mechanisms (Rate Limiting vs Conflation/SlowConsumerStrategy)
- Mix concerns/responsibilities (Message Scheduling belongs in a job scheduler, not a message bus)
- Can be trivially implemented in user code

**Only implement if users explicitly request AND provide compelling use cases.**

#### 16. Rate Limiting (3-5 days) - OUT OF SCOPE
**Reason:** Redundant with existing conflation and SlowConsumerStrategy mechanisms. For external API rate limits, users can implement throttling in their handlers or use existing rate limiting libraries.

- [ ] 16.1. Create RateLimiterOptions class
- [ ] 16.2. Add MaxMessagesPerSecond, BurstSize properties
- [ ] 16.3. Implement token bucket algorithm
- [ ] 16.4. Add rate limiter to SubscriptionOptions
- [ ] 16.5. Integrate rate limiting in Subscription message processing
- [ ] 16.6. Create RateLimitingTests.cs test file
- [ ] 16.7. Test rate limit enforcement
- [ ] 16.8. Test burst handling
- [ ] 16.9. Add XML documentation
- [ ] 16.10. Update README

#### 17. Message Scheduling (1 week) - OUT OF SCOPE
**Reason:** Job scheduling is a separate concern from message bus operations. Users should use Task.Delay(), Quartz.NET, Hangfire, or similar scheduling libraries. Adding this to CrossBar mixes responsibilities and creates unnecessary complexity.

**Alternative:** Create separate `Berberis.Scheduling` package if demand exists.

- [ ] 17.1. Add PublishDelayed method to ICrossBar
- [ ] 17.2. Add PublishScheduled method with specific DateTime
- [ ] 17.3. Create DelayedMessageScheduler internal service
- [ ] 17.4. Use Timer/PeriodicTimer for scheduled message delivery
- [ ] 17.5. Handle cancellation of scheduled messages
- [ ] 17.6. Create SchedulingTests.cs test file
- [ ] 17.7. Test delayed message delivery
- [ ] 17.8. Test scheduled message accuracy
- [ ] 17.9. Test cancellation scenarios
- [ ] 17.10. Add XML documentation
- [ ] 17.11. Update README

### Validation & Quality

#### Continuous Testing & Quality Assurance
- [ ] 18.1. Maintain >80% line coverage as new features are added
- [ ] 18.2. Maintain >70% branch coverage
- [ ] 18.3. Zero build warnings (--warnaserror)
- [ ] 18.4. All public APIs have XML documentation
- [ ] 18.5. Create integration tests for feature combinations
- [ ] 18.6. Performance benchmarks for all new features
- [ ] 18.7. Memory leak detection tests for long-running scenarios
- [ ] 18.8. Load testing for high-throughput scenarios
- [ ] 18.9. Chaos testing for resilience features (circuit breaker, DLQ, etc.)

#### Documentation
- [ ] 19.1. Update main README.md with all new features
- [ ] 19.2. Create feature-specific documentation files
- [ ] 19.3. Create migration guides for breaking changes
- [ ] 19.4. Create performance tuning guide
- [ ] 19.5. Create troubleshooting guide
- [ ] 19.6. Create architectural decision records (ADRs)
- [ ] 19.7. Create API reference documentation (via Docfx or similar)
- [ ] 19.8. Create sample applications for each major feature
- [ ] 19.9. Create video tutorials (optional)

## Files Created/Modified
[List will be populated as work progresses]

## Status Summary
- Phase 3 items: 0/86 complete
- Phase 4 items: 0/70 complete
- Phase 5 items: 0/57 complete
- Phase 6 (Out of Scope): 0/21 complete
- Quality/Docs: 0/23 complete
- Total progress: 0/257 tasks (0/236 excluding Phase 6)
- Build status: N/A
- Test status: N/A

## Next Steps
Review and prioritize Phase 3 features. Recommend starting with Configuration System as it provides foundation for other features.

## Blockers / Notes
None - Phase 1-2 successfully completed, ready to begin Phase 3.

## Dependencies Between Features
- **Configuration System** should be done FIRST - provides foundation for all other features
- **Health Check Support** depends on Configuration System (uses options pattern)
- **DLQ** works well with **Circuit Breaker** (complementary resilience features)
- **Request-Reply** has built-in timeout handling via CancellationToken
- **Distributed Messaging** requires **Message Compression** and **Schema Registry** for efficiency
- **Admin Dashboard** requires **Health Check** and **OpenTelemetry** for data
- **Security** should be implemented before **Distributed Messaging** goes to production

## Recommended Implementation Order

### Phase 3 (Core - 15 days)
1. Configuration System (3d) - foundation
2. Health Check Support (2d) - monitoring
3. Batch Processing (3d) - performance
4. Request-Reply (3d) - common pattern
5. Dead Letter Queue (4d) - resilience

### Phase 4 (Advanced - depends on requirements)
6. Circuit Breaker (1w) - pairs well with DLQ
7. Message Priority (1-2w) - if needed for use case
8. Message Compression (1w) - for recorder/persistence
9. Persistence (3-4w) - if durability required
10. Distributed (2-3mo) - if multi-process needed

### Phase 5 (Optional - based on user feedback)
11-17. Implement as needed based on user requests

EOF
```

---

**STEP 2: UPDATE CHECKPOINT AFTER EVERY TODO ITEM - THIS IS MANDATORY**

After completing each task:
1. Mark it as [x] in the checkpoint
2. Update "Last Updated" timestamp
3. Update status summary counters
4. Add files modified to "Files Created/Modified"
5. Save the checkpoint file

**DO NOT** batch updates - update after EVERY completed task.

Example update:
```bash
# After completing task 1.1
sed -i 's/\[ \] 1.1. Create CrossBarOptions.cs/[x] 1.1. Create CrossBarOptions.cs/' .checkpoints/phase3-enterprise-checkpoint.md
# Update timestamp
sed -i "s/Last Updated:.*/Last Updated: $(date)/" .checkpoints/phase3-enterprise-checkpoint.md
# Commit checkpoint
git add .checkpoints/phase3-enterprise-checkpoint.md
git commit -m "Checkpoint: Completed CrossBarOptions.cs creation"
```

---

## Mission Objectives

Your mission is to transform Berberis CrossBar from a production-ready messaging library into a **premier enterprise-grade messaging platform** that can compete with RabbitMQ, MediatR, and other established solutions.

### Success Criteria

**Phase 3 Complete (v2.5):**
- ✅ Configuration system implemented and tested
- ✅ Health checks integrated with ASP.NET Core
- ✅ Batch processing support with benchmarks
- ✅ Request-reply pattern with timeout handling
- ✅ Dead letter queue with retry logic
- ✅ All features >80% test coverage
- ✅ Comprehensive documentation and examples

**Phase 4 Complete (v3.0):**
- ✅ Circuit breaker resilience pattern
- ✅ Message priority queues
- ✅ Message compression for efficiency
- ✅ Optional: Persistence for durability
- ✅ Optional: Distributed messaging for multi-process

**Phase 5 Complete (v3.5+):**
- ✅ Rate limiting
- ✅ Filtering & transformation
- ✅ Message scheduling
- ✅ OpenTelemetry integration
- ✅ Schema registry
- ✅ Admin dashboard
- ✅ Security & authorization

---

## Guidelines & Best Practices

### Code Quality Standards

1. **Test Coverage:** Every feature must have >80% line coverage, >70% branch coverage
2. **Benchmarks:** Every performance-sensitive feature must have BenchmarkDotNet benchmarks
3. **Documentation:** Every public API must have complete XML documentation
4. **Zero Warnings:** Build must pass with --warnaserror
5. **No Regressions:** All existing tests must continue to pass

### Performance Standards

1. **Allocation-Free:** Hot paths should remain allocation-free where possible
2. **Benchmark First:** Before optimizing, benchmark to establish baseline
3. **Validate Changes:** All "optimizations" must be proven via benchmarks
4. **Memory Efficiency:** Monitor memory usage for long-running scenarios
5. **Latency Targets:** Maintain <100ns publish latency, >10M msg/sec throughput

### Architecture Standards

1. **Interface-First:** Define interfaces before implementation
2. **SOLID Principles:** Follow SOLID design principles
3. **Minimal Dependencies:** Only add dependencies when absolutely necessary
4. **Async All The Way:** Use async/await consistently
5. **Cancellation Support:** All long-running operations support CancellationToken

### Documentation Standards

1. **XML Comments:** Complete XML documentation on all public APIs
2. **Examples:** Every feature must have working code examples
3. **README Updates:** Update main README.md for each feature
4. **Migration Guides:** Document breaking changes with migration path
5. **Performance Notes:** Document performance characteristics and trade-offs

### Testing Standards

1. **Unit Tests:** Test individual components in isolation
2. **Integration Tests:** Test feature interactions
3. **Performance Tests:** Benchmark performance-critical paths
4. **Edge Cases:** Test failure scenarios, edge cases, boundary conditions
5. **Concurrency Tests:** Test thread-safety and race conditions

---

## Feature Prioritization Matrix

| Feature | Value | Complexity | Effort | Priority | Version |
|---------|-------|------------|--------|----------|---------|
| Configuration System | HIGH | LOW | 3d | P0 | v2.5 |
| Health Checks | HIGH | LOW | 2d | P0 | v2.5 |
| Batch Processing | HIGH | MED | 3d | P0 | v2.5 |
| Request-Reply | HIGH | MED | 3d | P0 | v2.5 |
| Dead Letter Queue | HIGH | MED | 4d | P0 | v2.5 |
| Circuit Breaker | MED | MED | 1w | P1 | v3.0 |
| Message Priority | MED | MED | 1-2w | P1 | v3.0 |
| Compression | MED | LOW | 1w | P1 | v3.0 |
| Persistence | HIGH | HIGH | 3-4w | P2 | v3.0+ |
| Distributed | HIGH | VERY HIGH | 2-3mo | P2 | v4.0 |
| Rate Limiting | LOW | LOW | 3-5d | P3 | v3.5+ |
| Filtering/Transform | LOW | LOW | 3-5d | P3 | v3.5+ |
| Scheduling | MED | MED | 1w | P3 | v3.5+ |
| OpenTelemetry | MED | MED | 2w | P3 | v3.5+ |
| Schema Registry | LOW | HIGH | 2-3w | P4 | v4.0+ |
| Admin Dashboard | MED | HIGH | 1-2mo | P4 | v4.0+ |
| Security | HIGH | HIGH | 3-4w | P2 | v3.0+ |

**Priority Levels:**
- **P0:** Must-have for v2.5 (Phase 3)
- **P1:** Should-have for v3.0 (Phase 4 core)
- **P2:** Important for enterprise but can be later
- **P3:** Nice-to-have enhancements
- **P4:** Future considerations

---

## Breaking Changes Policy

**IMPORTANT:** Berberis is already at v1.1.30 and should be production-ready. Minimize breaking changes.

**Allowed Breaking Changes (with major version bump):**
- Adding required parameters to interfaces (IFF absolutely necessary)
- Changing default behavior (document in CHANGELOG)
- Removing deprecated APIs (after 2+ minor versions of deprecation)

**Preferred Approach:**
- Add new interfaces (e.g., ICrossBarExtended) rather than modify existing
- Use optional parameters for new features
- Provide adapter/wrapper classes for compatibility
- Use [Obsolete] attribute for deprecations

---

## Release Strategy

### Version 2.5 (Phase 3 Complete)
**Target:** 3 weeks from start
**Focus:** High-value enterprise features
**Includes:** Configuration, Health Checks, Batch, Request-Reply, DLQ

### Version 3.0 (Phase 4 Core)
**Target:** 2-3 months from start
**Focus:** Advanced resilience and performance
**Includes:** Circuit Breaker, Priority, Compression, optionally Persistence

### Version 3.5+ (Phase 5 Optional)
**Target:** User-driven roadmap
**Focus:** Nice-to-have enhancements based on feedback
**Includes:** Selected features from Phase 5 based on demand

### Version 4.0 (Distributed)
**Target:** 6-12 months from start
**Focus:** Multi-process distributed messaging
**Includes:** Distributed CrossBar, Schema Registry, Security, Admin Dashboard

---

## Known Risks & Mitigation

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Feature creep extends timeline | HIGH | MED | Strict prioritization, phase-based delivery |
| Breaking changes anger users | HIGH | LOW | Careful API design, deprecation policy |
| Performance regressions | HIGH | MED | Mandatory benchmarks before merge |
| Complexity increases bugs | MED | HIGH | High test coverage requirement |
| Distributed adds major complexity | VERY HIGH | HIGH | Make optional, separate package |
| Dependencies bloat library | MED | MED | Minimize dependencies, make optional |

---

## Communication & Checkpoints

**Daily Checkpoints:**
- Update checkpoint file after each task completion
- Commit checkpoint to git for safety
- Update todo list to reflect current progress

**Weekly Reviews:**
- Review completed features
- Run full test suite + benchmarks
- Update documentation
- Plan next week's tasks

**Phase Completions:**
- Full regression testing
- Performance validation
- Documentation review
- Create release notes
- Tag release in git

---

## Getting Started

**Step 1:** Read checkpoint file (if exists) or create it (if doesn't exist)

**Step 2:** Start with Phase 3, Task 1.1 (Create CrossBarOptions.cs)

**Step 3:** Follow TDD approach:
1. Write tests first
2. Implement feature
3. Verify tests pass
4. Benchmark if performance-sensitive
5. Document
6. Update checkpoint

**Step 4:** Commit frequently with descriptive messages

**Step 5:** Update README as features complete

---

## Questions to Resolve Before Starting

1. **Distributed Messaging:** Is multi-process support a requirement, or can it be deferred to v4.0?
2. **Persistence:** Is message durability (survive restarts) required for your use cases?
3. **Dependencies:** Is it acceptable to add dependencies for compression (LZ4), serialization (MessagePack), telemetry (OpenTelemetry)?
4. **Breaking Changes:** Are any breaking changes acceptable for v2.5, or must we maintain 100% backward compatibility?
5. **Timeline:** What's the target timeline for Phase 3 completion?

---

**Ready to begin? Start by checking for the checkpoint file!**
