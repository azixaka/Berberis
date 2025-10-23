# Benchmark Comparison: MessageStore Optimization Impact

## Overview

This document compares performance **before** and **after** the MessageStore optimization (Dictionary+lock → ConcurrentDictionary) from the Phase 1-2 Production Hardening work.

## Baseline Metrics (Before Optimization)

**Test Configuration:**
- Implementation: Dictionary with lock-based synchronization
- Measurement: Mean execution time in microseconds (μs)
- Hardware: [Same machine for both tests]

### StatefulConcurrencyBenchmarks - BASELINE

| Benchmark | Publishers | Mean Time (μs) | Notes |
|-----------|------------|----------------|-------|
| **Stateful_ConcurrentUpdates_SameKeys** | 2 | 59.17 | Multiple threads updating SAME keys |
| | 4 | 63.29 | |
| | 8 | 156.20 | **Heavy lock contention** |
| | 16 | 442.28 | **Severe contention bottleneck** |
| **Stateful_ConcurrentUpdates_DifferentKeys** | 2 | 56.42 | Multiple threads updating DIFFERENT keys |
| | 4 | 68.53 | |
| | 8 | 159.00 | Still locks unnecessarily |
| | 16 | 461.92 | Lock overhead even for different keys |
| **Stateful_MixedReadsAndWrites** | 2 | 67.04 | Concurrent state updates + fetches |
| | 4 | 129.80 | |
| | 8 | 315.12 | **Read locks block writes** |
| | 16 | 581.15 | **Worst case scenario** |

### Key Observations from Baseline

1. **Lock Contention Scales Poorly**
   - 2 → 4 publishers: +7% slower (SameKeys)
   - 4 → 8 publishers: +147% slower (SameKeys)
   - 8 → 16 publishers: +183% slower (SameKeys)
   - **Non-linear degradation** indicates lock contention

2. **Even Different Keys Suffer**
   - DifferentKeys benchmark still shows degradation
   - 16 publishers: 461.92 μs (should be near-linear scaling)
   - Lock is acquired for ALL operations, even when touching different keys

3. **Mixed Reads/Writes Worst Hit**
   - 16 publishers: 581.15 μs
   - Readers and writers block each other
   - Classic reader-writer lock problem

## Expected Improvements (Per HARDENING-BENCHMARKS.md)

| Scenario | Expected Improvement | Reason |
|----------|---------------------|---------|
| **SameKeys** (8-16 publishers) | **3-5x faster** | Lock-free ConcurrentDictionary updates |
| **DifferentKeys** (all) | **2-3x faster** | No lock needed for different keys |
| **MixedReadsAndWrites** | **2-4x faster** | Lock-free reads and writes |

### Predicted Current Results

Based on expected 3-5x improvement for high contention:

| Benchmark | Publishers | Baseline | Predicted | Improvement |
|-----------|------------|----------|-----------|-------------|
| SameKeys | 8 | 156.20 μs | 31-52 μs | 3-5x |
| SameKeys | 16 | 442.28 μs | 88-147 μs | 3-5x |
| MixedReads | 8 | 315.12 μs | 79-157 μs | 2-4x |
| MixedReads | 16 | 581.15 μs | 145-290 μs | 2-4x |

## Current Results (After Optimization)

**Status:** 🔄 Benchmarks currently running...

Running: `dotnet run -c Release -- -j short --filter '*StatefulConcurrency*'`

Estimated completion: ~10-15 minutes

### Partial Results (Being Collected)

From preliminary benchmark output:
- Stateful_ConcurrentUpdates_SameKeys (2 publishers): 71.36 μs (vs baseline 59.17 μs)

**Note:** Low publisher counts (2-4) may show similar or slightly slower performance due to:
- ConcurrentDictionary overhead for low contention
- The optimization targets high-contention scenarios (8+ publishers)

## Performance Analysis

### When to Expect Improvement

The MessageStore optimization using ConcurrentDictionary is specifically designed for **high-concurrency scenarios**:

✅ **Significant gains expected:**
- 8+ concurrent publishers
- Heavy concurrent state updates
- Production workloads with many simultaneous operations

❌ **Minimal gains (or overhead) expected:**
- 1-4 concurrent publishers
- Low-contention scenarios
- Single-threaded or sequential access

### Critical Metrics to Watch

1. **Lock Contention Reduction** (8-16 publishers)
   - Should see 3-5x improvement in SameKeys benchmark
   - Validates Dictionary+lock → ConcurrentDictionary migration

2. **Scalability** (scaling with publisher count)
   - Current: Non-linear degradation (bad scaling)
   - Expected: Near-linear scaling (good scaling)

3. **Read/Write Concurrency**
   - Current: Read locks block writes
   - Expected: True concurrent reads and writes

## Validation Checklist

- [ ] 8-publisher SameKeys: 3-5x faster than baseline (156.20 μs → ~31-52 μs)
- [ ] 16-publisher SameKeys: 3-5x faster than baseline (442.28 μs → ~88-147 μs)
- [ ] MixedReadsAndWrites: 2-4x faster at 8-16 publishers
- [ ] No major regressions in other benchmarks
- [ ] Linear scaling maintained at high concurrency

## Next Steps

1. ⏳ Wait for benchmark completion (~10 min remaining)
2. 📊 Extract final metrics from JSON results
3. 📈 Compare against baseline and expected improvements
4. ✅ Validate success criteria met
5. 📝 Document final findings

---

**Last Updated:** 2025-10-23 (Preliminary - Awaiting Final Results)
