# MessageStore Optimization Reversion Summary

## Action Taken

Reverted the MessageStore implementation from `ConcurrentDictionary` back to `Dictionary + lock` based on comprehensive benchmark results.

## Commit Details

**Reverted:** Partial revert of commit `39de538` ("Tackle MessageStore high traffic contention")

**What was reverted:**
- `Berberis.Messaging/CrossBar.MessageStore.cs` - Reverted from ConcurrentDictionary to Dictionary+lock

**What was kept (good improvements):**
- `Berberis.Messaging/CrossBar.cs` - New custom exception types usage
- `Berberis.Messaging/Exceptions/ChannelTypeMismatchException.cs` - Better type mismatch exceptions
- `Berberis.Messaging/Exceptions/InvalidChannelNameException.cs` - Better validation exceptions
- All test updates - Using new exception types
- `tests/Berberis.Messaging.Tests/Performance/MessageStorePerformanceTests.cs` - Performance tests

## Benchmark Results (Why Reversion Was Necessary)

### Expected vs Actual Performance

| Scenario | Expected | Actual | Result |
|----------|----------|--------|--------|
| SameKeys @ 8 publishers | 3-5x faster | **1.6x SLOWER** | ❌ Regression |
| SameKeys @ 16 publishers | 3-5x faster | ~same | ❌ No improvement |
| MixedReads @ 8 publishers | 2-4x faster | **1.6x SLOWER** | ❌ Regression |
| MixedReads @ 16 publishers | 2-4x faster | **2.0x SLOWER** | ❌ Severe regression |

### Only Improvement Found

- DifferentKeys @ 16 publishers: 6% faster (not worth the other regressions)

## Root Cause

The ConcurrentDictionary "optimization" was actually slower because:

1. **Indexer Overhead**: `_state[key] = value` uses complex lock-free algorithms (compare-and-swap, memory barriers, retry loops)
2. **GetState() Contention**: `ConcurrentDictionary.Values.ToArray()` still takes locks internally
3. **Lock Was Already Efficient**: .NET's lock is highly optimized for:
   - Low contention scenarios (our actual workload)
   - Short critical sections (8-120 ns operations)
   - Predictable access patterns
4. **Wrong Assumptions**: We overestimated lock contention in the original Dictionary+lock implementation

## Key Learning

> **Lock-free ≠ Faster**
>
> ConcurrentDictionary has overhead. It only wins under heavy, sustained contention. Our workload doesn't have that pattern.

## Test Results After Reversion

```
✅ All 193 tests pass
✅ Build succeeds
✅ Performance tests work with both implementations
✅ New exception types preserved
```

## Performance Characteristics (After Reversion)

Back to baseline performance:
- Low overhead for sequential operations
- Good scaling up to 16 concurrent publishers
- Simple, maintainable code
- Well-understood lock behavior

## References

- **Benchmark Comparison**: `benchmarks/PRELIMINARY-COMPARISON.md`
- **Root Cause Analysis**: `benchmarks/REGRESSION-ANALYSIS.md`
- **Baseline Results**: `benchmarks/results/baselines/before-hardening/`
- **Regression Results**: `benchmarks/Berberis.Messaging.Benchmarks/BenchmarkDotNet.Artifacts/results/`

---

**Date:** 2025-10-23
**Decision:** Revert MessageStore to Dictionary+lock
**Reason:** Benchmark data shows ConcurrentDictionary is 1.6-2x slower
