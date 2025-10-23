# Performance Comparison Guidelines

## Purpose

This document describes how to compare performance benchmarks between versions, commits, or after code changes to detect regressions or improvements.

## When to Run Benchmarks

### Always Run
- Before major releases
- After performance-critical code changes
- When modifying hot paths (Publish, Subscribe, MessageStore)
- After optimizations to verify improvements

### Consider Running
- After refactoring core components
- When adding new features that touch critical paths
- When changing concurrency patterns

### Don't Need to Run
- After documentation changes
- After test-only changes
- After non-critical feature additions

## Running Comparison Benchmarks

### 1. Establish Baseline (One-Time)

```bash
# On dedicated hardware
git checkout master  # or your baseline branch
cd benchmarks/Berberis.Messaging.Benchmarks
dotnet run -c Release -- --filter '*' --exporters json

# Save baseline
mkdir -p results/baselines
cp BenchmarkDotNet.Artifacts/results/*.json results/baselines/baseline-v1.1.30.json
git add results/baselines/
git commit -m "Add v1.1.30 performance baseline"
```

### 2. Run Comparison After Changes

```bash
# After making changes
git checkout feature-branch
cd benchmarks/Berberis.Messaging.Benchmarks
dotnet run -c Release -- --filter '*' --exporters json

# Save comparison results
cp BenchmarkDotNet.Artifacts/results/*.json results/comparison-$(date +%Y%m%d).json
```

### 3. Analyze Differences

**Manual Comparison:**
1. Open both HTML reports
2. Compare key metrics:
   - Mean execution time
   - Allocated memory
   - Standard deviation (check for variance)
3. Look for significant changes (>20% difference)

**Key Metrics to Monitor:**

| Metric | Regression Threshold | Critical Threshold |
|--------|---------------------|-------------------|
| Publish Latency (p50) | +20% | +50% |
| Allocations per Publish | +10% | +50% |
| Throughput | -20% | -50% |
| Latency p99 | +30% | +100% |

## Interpreting Results

### When to Investigate Immediately

**Critical Regressions (Stop Ship):**
- >50% increase in publish latency
- New heap allocations in hot paths (was 0B, now >0B)
- >50% throughput decrease
- Any memory leaks detected

**Example:**
```
Baseline: Publish_SingleMessage = 45ns, 0B allocated
Current:  Publish_SingleMessage = 120ns, 24B allocated
Action:   INVESTIGATE IMMEDIATELY - 2.6x slower + new allocations
```

### When to Investigate Soon

**Moderate Regressions (Before Merge):**
- 20-50% latency increase
- 20-50% throughput decrease
- Increased allocation volume (even if not in hot path)

**Example:**
```
Baseline: Throughput = 5.2M msg/sec
Current:  Throughput = 3.8M msg/sec
Action:   INVESTIGATE - 27% slower, impacts high-throughput scenarios
```

### When to Monitor

**Minor Changes (<20%):**
- Could be measurement noise
- Could be legitimate impact
- Run benchmarks multiple times to verify
- Document but don't block on minor variations

**Example:**
```
Baseline: Latency = 45ns ± 2ns
Current:  Latency = 52ns ± 3ns
Action:   MONITOR - 15% increase, within noise threshold but watch for trend
```

### When to Celebrate

**Improvements:**
- Verify improvements are real (run multiple times)
- Ensure no trade-offs (check allocations, latency, throughput together)
- Document the optimization

**Example:**
```
Baseline: Allocations_100Publishes = 2400B
Current:  Allocations_100Publishes = 0B
Action:   DOCUMENT - Successful allocation-free optimization!
```

## Common Regression Causes

### 1. Lock Contention
**Symptoms:**
- Concurrent benchmarks much slower
- Single-threaded unchanged

**Example:**
```csharp
// Before: Lock-free
var dict = new ConcurrentDictionary<string, T>();

// After: Lock added (regression!)
lock (_sync) { dict[key] = value; }
```

### 2. New Allocations
**Symptoms:**
- Allocated memory increased
- Often in LINQ or closures

**Example:**
```csharp
// Before: Allocation-free
var count = list.Count;

// After: Allocates enumerator (regression!)
var count = list.Where(x => x.IsActive).Count();
```

### 3. Unnecessary Async
**Symptoms:**
- Increased latency
- More allocations (async state machine)

**Example:**
```csharp
// Before: Synchronous
return ValueTask.CompletedTask;

// After: Async state machine (regression!)
return await SomeAsyncOperation();
```

### 4. Boxing
**Symptoms:**
- Increased allocations
- Slightly higher latency

**Example:**
```csharp
// Before: No boxing
var msg = new Message<int> { Body = 42 };

// After: Boxing (regression!)
object body = 42;
var msg = new Message<object> { Body = body };
```

## Hardware Considerations

### Why Dedicated Hardware Matters

**❌ CI/CD Runners (Unreliable):**
```
Run 1: 45ns ± 5ns
Run 2: 68ns ± 12ns
Run 3: 52ns ± 8ns
Conclusion: ±40% variance = useless for regression detection
```

**✅ Dedicated Hardware (Reliable):**
```
Run 1: 45ns ± 2ns
Run 2: 46ns ± 2ns
Run 3: 45ns ± 2ns
Conclusion: ±4% variance = meaningful comparisons possible
```

### Recommended Benchmark Environment

- **Physical machine** (not VM if possible)
- **No other processes** running
- **Consistent power settings** (disable CPU throttling)
- **Same hardware** for all comparisons
- **Room temperature** (thermal throttling affects results)

## Version-to-Version Comparison

### Major Release Checklist

Before releasing v1.2.0:

1. ✅ Run full benchmark suite on master
2. ✅ Save as `results/baselines/baseline-v1.2.0.json`
3. ✅ Compare with v1.1.30 baseline
4. ✅ Document any performance changes in CHANGELOG
5. ✅ Update BENCHMARK-RESULTS.md with new numbers
6. ✅ Commit baseline to repository

### Tracking Performance Over Time

Create a performance log:

```markdown
# Performance History

## v1.2.0 (2025-01-15)
- Publish latency: 42ns (was 45ns in v1.1.30) - 7% improvement
- Allocations: 0B (unchanged) - maintained allocation-free design
- Throughput: 5.8M msg/sec (was 5.2M) - 11% improvement
- Change: Optimized MessageStore with ConcurrentDictionary

## v1.1.30 (2024-12-20)
- Publish latency: 45ns
- Allocations: 0B
- Throughput: 5.2M msg/sec
- Baseline version
```

## CI/CD Smoke Tests (Optional)

While full benchmarks shouldn't run in CI, you can add a quick smoke test:

```yaml
# .github/workflows/benchmark-smoke-test.yml
name: Benchmark Smoke Test

on:
  pull_request:
    branches: [ master ]

jobs:
  smoke-test:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x

    - name: Build Benchmarks
      run: |
        cd benchmarks/Berberis.Messaging.Benchmarks
        dotnet build -c Release

    - name: Smoke Test - Verify Benchmarks Run
      run: |
        cd benchmarks/Berberis.Messaging.Benchmarks
        # Quick run, just verify no crashes
        dotnet run -c Release -- --job short --filter '*PublishSubscribeBenchmarks.Publish_SingleMessage'

    # Do NOT compare results - CI environment too noisy
```

**Purpose:** Only verifies benchmarks compile and run without crashing. Does NOT check performance.

## Best Practices

1. **Always run on same hardware** - Different CPUs = incomparable results
2. **Run multiple times** - Verify consistency (should be within ±5%)
3. **Document hardware specs** - Include in baseline files
4. **Commit baselines to repo** - Track performance history
5. **Don't over-optimize** - 20% improvement that adds complexity may not be worth it
6. **Test real scenarios** - Synthetic benchmarks don't always reflect production
7. **Profile before optimizing** - Use dotTrace/PerfView to understand bottlenecks

## Tools for Analysis

### BenchmarkDotNet Features
- Built-in statistical analysis
- Memory diagnoser
- HTML/JSON/CSV exporters
- Baseline comparison

### External Tools
- **dotTrace** (JetBrains) - CPU profiling
- **dotMemory** (JetBrains) - Memory profiling
- **PerfView** (Microsoft) - Performance investigation
- **Visual Studio Profiler** - Built-in profiling

## Questions to Ask When Reviewing Results

1. **Is the change significant?** (>20% or allocations added)
2. **Is it reproducible?** (Run 3 times, same result?)
3. **Does it affect critical paths?** (Publish/Subscribe hot paths)
4. **Are there trade-offs?** (Faster but more allocations?)
5. **Is the variance high?** (High StdDev = unreliable)
6. **Does it match expectations?** (Optimization should improve, not regress)

## Getting Help

If benchmark results are confusing:
1. Run benchmarks multiple times - verify consistency
2. Check hardware was idle during run
3. Review recent code changes for obvious issues
4. Profile with dotTrace to understand what changed
5. Compare with documented baselines in `results/baselines/`
