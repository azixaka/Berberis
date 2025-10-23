# Berberis CrossBar - Performance Benchmarks

## Overview

This directory contains performance benchmarks for Berberis CrossBar messaging library, built with BenchmarkDotNet.

## Why Manual Benchmarks?

**Performance benchmarks should be run manually on dedicated hardware, NOT in CI/CD.**

**Reasons:**
- ❌ CI runners are virtualized and shared (noisy neighbors)
- ❌ Results vary ±20-50% between runs in CI
- ❌ Makes regression detection unreliable
- ❌ Wastes CI time (10-30 minutes for full suite)

**Instead:**
- ✅ Run benchmarks on consistent, dedicated hardware
- ✅ Compare results from same machine
- ✅ Commit baseline results to repository
- ✅ CI only does quick "smoke test" (verifies benchmarks compile/run)

## Running Benchmarks

### Prerequisites

- .NET 8.0 SDK
- Dedicated hardware (recommended)
- No other processes running during benchmark

### ⚡ QUICK MODE (Recommended for Development)

```bash
cd benchmarks/Berberis.Messaging.Benchmarks
dotnet run -c Release -- --quick --filter '*'
```

**Use this for:**
- Initial development
- Quick performance checks
- Rapid iteration
- Smoke testing changes

**Stats:**
- **Time:** ~2-3 minutes for all 60 benchmarks
- **Accuracy:** Approximate (1 warmup + 3 iterations)
- **NOT for:** Official baselines, regression detection

### Run All Benchmarks (Full Statistical Analysis)

```bash
cd benchmarks/Berberis.Messaging.Benchmarks
dotnet run -c Release
```

**Use this for:**
- Official performance baselines
- Regression detection
- Production metrics
- Release benchmarks

**Stats:**
- **Time:** ~20-30 minutes for all 60 benchmarks
- **Accuracy:** High (auto-determined iterations for statistical significance)

This will:
- Run all benchmark classes
- Generate results in `BenchmarkDotNet.Artifacts/results/`

### Run Specific Benchmark

```bash
# QUICK MODE - single benchmark category
dotnet run -c Release -- --quick --filter '*PublishSubscribe*'

# STANDARD MODE - single benchmark category
dotnet run -c Release -- --filter '*Throughput*'

# Run only allocation benchmarks
dotnet run -c Release -- --filter '*Allocation*'
```

## Benchmark Structure

```
benchmarks/Berberis.Messaging.Benchmarks/
├── Core/
│   ├── PublishSubscribeBenchmarks.cs    - Basic pub/sub operations
│   └── ThroughputBenchmarks.cs          - Sustained throughput tests
├── Latency/
│   ├── LatencyBenchmarks.cs             - End-to-end latency
│   └── HandlerTimeoutBenchmarks.cs      - Timeout enforcement (Tasks 4-6)
├── Memory/
│   └── AllocationBenchmarks.cs          - Memory allocation tests
├── Stateful/
│   ├── StatefulChannelBenchmarks.cs     - State management
│   └── StateInitializationBenchmarks.cs - State send race (Task 1)
├── Wildcards/
│   ├── WildcardBenchmarks.cs            - Pattern matching
│   └── WildcardConcurrencyBenchmarks.cs - Wildcard race (Task 3)
├── Conflation/
│   └── ConflationBenchmarks.cs          - Message conflation
├── Concurrency/
│   ├── ConcurrencyBenchmarks.cs         - Concurrent operations
│   └── StatefulConcurrencyBenchmarks.cs - MessageStore optimization (Tasks 7-9)
└── Helpers/
    └── BenchmarkHelpers.cs              - Shared utilities
```

## What Each Benchmark Measures

### Core Operations
- **PublishSubscribeBenchmarks:** Basic publish/subscribe latency
- **ThroughputBenchmarks:** Messages per second under sustained load

### Latency
- **LatencyBenchmarks:** Time from publish to handler execution
  - Includes p50, p90, p95, p99 percentiles
- **HandlerTimeoutBenchmarks:** ⚠️ **CRITICAL FOR PRODUCTION**
  - Tests handler timeout enforcement (Tasks 4-6 from hardening work)
  - Demonstrates deadlock prevention
  - Measures timeout overhead and callback performance
  - **Note:** Some benchmarks require Task 4-6 implementation

### Memory
- **AllocationBenchmarks:** Heap allocations per operation
  - Verifies "allocation-free" claims for hot paths

### Stateful
- **StatefulChannelBenchmarks:** State storage and retrieval performance
- **StateInitializationBenchmarks:** Tests state send race condition (Task 1)
  - Validates sequence tracking prevents out-of-order messages
  - Tests concurrent subscription with state fetch
  - Measures state initialization overhead

### Wildcards
- **WildcardBenchmarks:** Pattern matching overhead
- **WildcardConcurrencyBenchmarks:** Tests wildcard subscription race (Task 3)
  - Demonstrates eventual consistency model
  - Tests concurrent subscribe/publish scenarios
  - Validates FindMatchingChannels behavior

### Conflation
- **ConflationBenchmarks:** Conflation effectiveness and overhead

### Concurrency
- **ConcurrencyBenchmarks:** Thread-safety and concurrent scalability
- **StatefulConcurrencyBenchmarks:** ⚡ **SHOWS 3-5x IMPROVEMENT**
  - Tests MessageStore optimization (Tasks 7-9)
  - Measures lock contention with current Dictionary+lock implementation
  - **Will show dramatic improvement** after ConcurrentDictionary migration
  - Tests concurrent state updates, reads, deletes, and initialization

## Hardening Validation Benchmarks

Several benchmarks are specifically designed to validate fixes from the Phase 1-2 Production Hardening work (see `WS-PHASE1-2-HARDENING.md`):

### Before vs After Comparisons

| Benchmark | Tests | Expected Improvement | Hardening Tasks |
|-----------|-------|---------------------|-----------------|
| **StatefulConcurrencyBenchmarks** | MessageStore lock contention | **3-5x throughput** under concurrent writes | Tasks 7-9 |
| **MessageStoreInitializationBenchmarks** | Channel initialization race | Thread-safe creation, no double-init | Task 2 |
| **StateInitializationBenchmarks** | State send race condition | No duplicate/out-of-order messages | Task 1 |
| **WildcardConcurrencyBenchmarks** | Wildcard subscription race | Documented eventual consistency | Task 3 |
| **HandlerTimeoutBenchmarks** | Deadlock prevention | Timeout enforcement, statistics | Tasks 4-6 |

### Running Hardening Benchmarks

```bash
# Test MessageStore optimization impact (Tasks 7-9)
dotnet run -c Release -- --filter '*StatefulConcurrency*'

# Test state initialization fixes (Task 1)
dotnet run -c Release -- --filter '*StateInitialization*'

# Test wildcard race scenarios (Task 3)
dotnet run -c Release -- --filter '*WildcardConcurrency*'

# Test timeout functionality (Tasks 4-6)
# Note: Some benchmarks require timeout implementation
dotnet run -c Release -- --filter '*HandlerTimeout*'

# Run all hardening-related benchmarks
dotnet run -c Release -- --filter '*Stateful*|*Wildcard*|*Timeout*'
```

### Critical Findings

1. **StatefulConcurrencyBenchmarks.Stateful_ConcurrentUpdates_SameKeys**
   - **Current**: Lock contention bottleneck with Dictionary+lock
   - **After Task 7-9**: 3-5x improvement with ConcurrentDictionary
   - **Why**: Eliminates lock contention on MessageStore operations

2. **HandlerTimeoutBenchmarks.Production_DatabaseHandlerTimeout**
   - **Critical**: Without Tasks 4-6, this benchmark hangs indefinitely
   - **After Tasks 4-6**: Completes with timeout enforcement
   - **Why**: Prevents single slow handler from deadlocking message bus

3. **StateInitializationBenchmarks.State_RapidPublishingDuringStateInit**
   - **Current**: Potential for out-of-order message delivery
   - **After Task 1**: Monotonically increasing message IDs, no duplicates
   - **Why**: Sequence tracking prevents race condition

## Viewing Results

Results are saved in multiple formats:

```bash
cd BenchmarkDotNet.Artifacts/results/

# HTML (human-readable)
open *.html

# JSON (programmatic)
cat *.json

# CSV (spreadsheet)
open *.csv
```

## Establishing Baselines

After running benchmarks:

```bash
# Save as baseline
mkdir -p results/baselines
cp BenchmarkDotNet.Artifacts/results/*.json results/baselines/baseline-v1.1.30.json

# Commit to repository
git add results/baselines/
git commit -m "Add v1.1.30 performance baseline"
```

## Comparing Performance

See [PERFORMANCE-COMPARISON.md](./PERFORMANCE-COMPARISON.md) for detailed guidance on:
- When to run benchmarks
- How to compare results
- Regression thresholds
- Common regression causes

## CI/CD Integration

**Smoke Test Only:**
- CI runs a quick smoke test (30 seconds)
- Verifies benchmarks compile and execute
- Does NOT measure performance or detect regressions
- See `.github/workflows/benchmark-smoke-test.yml`

**No Performance Testing in CI:**
- CI environments too noisy for reliable measurements
- Results vary ±20-50% between runs
- False positives would block valid PRs

## Key Performance Indicators

Target performance characteristics:

| Metric | Target | Status |
|--------|--------|--------|
| Publish Latency (p50) | <50ns | [Measure] |
| Publish Latency (p99) | <100ns | [Measure] |
| Allocations (hot path) | 0 bytes | [Measure] |
| Throughput (single ch) | >5M msg/sec | [Measure] |
| Concurrent scaling | Linear to 8 threads | [Measure] |

## Documentation

- **BENCHMARK-RESULTS.md** - Baseline results and analysis
- **PERFORMANCE-COMPARISON.md** - How to compare and detect regressions

## Troubleshooting

### High Variance in Results

**Symptom:** StdDev >10% of mean

**Solutions:**
- Close all other applications
- Disable CPU throttling
- Run on dedicated hardware
- Check for thermal throttling

### Unexpected Regressions

**Check:**
1. Did code change affect hot paths?
2. Were benchmarks run on same hardware?
3. Is variance high (unreliable results)?
4. Run multiple times to verify

### Benchmarks Won't Build

**Check:**
- .NET 8.0 SDK installed
- Project reference correct
- Dependencies restored (`dotnet restore`)

## Best Practices

1. **Always run in Release mode** - Debug mode ~10x slower
2. **Run on dedicated hardware** - Consistent environment required
3. **Run multiple times** - Verify results are reproducible
4. **Document hardware** - Include specs with baseline results
5. **Commit baselines** - Track performance over time
6. **Compare on same machine** - Different CPUs = incomparable

## Questions?

- Review [PERFORMANCE-COMPARISON.md](./PERFORMANCE-COMPARISON.md)
- Review [BENCHMARK-RESULTS.md](./BENCHMARK-RESULTS.md)
- Check BenchmarkDotNet docs: https://benchmarkdotnet.org
