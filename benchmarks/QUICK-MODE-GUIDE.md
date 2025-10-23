# Berberis CrossBar - Quick Mode Benchmark Guide

## 🎯 Problem Solved

**Original Issue:** Running all 60 benchmarks took **20-30 minutes** due to BenchmarkDotNet running 15-100 iterations per benchmark for statistical accuracy.

**Solution:** Added `--quick` flag that reduces iterations to **1 warmup + 3 actual iterations**, completing all benchmarks in **~2-3 minutes**.

---

## ⚡ Quick Mode Usage

### Run ALL Benchmarks (Fast)

```bash
cd benchmarks/Berberis.Messaging.Benchmarks
dotnet run -c Release -- --quick --filter '*'
```

**Time:** ~2-3 minutes for all 60 benchmarks

### Run Specific Category (Fast)

```bash
# Just allocation benchmarks
dotnet run -c Release -- --quick --filter '*Allocation*'

# Just concurrency benchmarks
dotnet run -c Release -- --quick --filter '*Concurrency*'

# Just latency benchmarks
dotnet run -c Release -- --quick --filter '*Latency*'
```

---

## 📊 Mode Comparison

| Mode | Command | Time (60 benchmarks) | Iterations | Use Case |
|------|---------|----------------------|------------|----------|
| **Quick** | `--quick --filter '*'` | ~2-3 minutes | 1 warmup + 3 actual | Development, quick checks |
| **Standard** | `--filter '*'` | ~20-30 minutes | Auto (15-100 iterations) | Official baselines, releases |

---

## 🔍 When to Use Each Mode

### ⚡ Use QUICK MODE for:

- ✅ **Development iteration** - Quick feedback during code changes
- ✅ **Smoke testing** - Verify benchmarks still run after refactoring
- ✅ **Rough performance checks** - "Is this faster or slower?"
- ✅ **Finding performance cliffs** - Detect major regressions (>2x slower)
- ✅ **Exploratory analysis** - Which benchmarks are worth investigating?

### 📈 Use STANDARD MODE for:

- ✅ **Official baselines** - Track performance over time
- ✅ **Release benchmarks** - Before major releases
- ✅ **Regression detection** - Precise comparison (±5-10% accuracy)
- ✅ **Performance optimization** - Measuring exact speedup from changes
- ✅ **Documentation** - Publishing official performance numbers

---

## 💡 Understanding the Speedup

### Why is Quick Mode ~10x Faster?

**Standard Mode (per benchmark):**
```
Pilot iterations:  ~11 iterations (determine operation count)
Warmup:            6-15 iterations (warm up JIT, GC)
Actual:            15-100 iterations (statistical significance)
───────────────────────────────────────────────────────
Total:             ~30-120 iterations per benchmark
```

**Quick Mode (per benchmark):**
```
Pilot iterations:  ~11 iterations (still needed)
Warmup:            1 iteration (minimal warmup)
Actual:            3 iterations (approximate results)
───────────────────────────────────────────────────────
Total:             ~15 iterations per benchmark
```

**Speedup Math:**
- Standard: 60 benchmarks × ~60 avg iterations = 3,600 total iterations
- Quick: 60 benchmarks × ~15 avg iterations = 900 total iterations
- **Result:** ~4x fewer iterations = ~4-10x faster (depending on benchmark complexity)

---

## 📝 Interpreting Quick Mode Results

### Accuracy Expectations

**Quick Mode Results:**
- **Mean time:** ±10-20% accuracy
- **Percentiles (p50, p90, p95):** Less reliable (only 3 samples)
- **Allocations:** Accurate (allocation detection is deterministic)
- **Relative comparisons:** Good for "2x slower" but not "10% slower"

**Example:**
```
Quick Mode:   Publish_SingleMessage = 45.2ns ± 8ns (high variance)
Standard Mode: Publish_SingleMessage = 42.7ns ± 0.4ns (low variance)

Conclusion: Both show ~45ns, but standard mode is precise
```

---

## 🛠️ How It Works (Technical Details)

Quick mode is implemented in `Program.cs`:

```csharp
if (args.Contains("--quick"))
{
    var quickJob = Job.Default
        .WithWarmupCount(1)        // Minimal warmup
        .WithIterationCount(3)     // Minimal iterations
        .WithGcServer(true)
        .WithGcConcurrent(true);

    config = DefaultConfig.Instance
        .AddDiagnoser(MemoryDiagnoser.Default)
        .AddJob(quickJob);
}
```

**Key Changes:**
- `WithWarmupCount(1)` - Down from 6-15 (default)
- `WithIterationCount(3)` - Down from 15-100 (auto-determined)
- Still includes MemoryDiagnoser for allocation tracking

---

## 📂 Result Files

Both modes generate the same output formats:

```
BenchmarkDotNet.Artifacts/results/
├── *.html       - Human-readable reports
├── *.json       - Programmatic access
├── *.csv        - Spreadsheet analysis
└── *.md         - GitHub-flavored markdown
```

**Quick mode results include a note:**
```
Job Configuration: IterationCount=3, WarmupCount=1
```

---

## 🚀 Workflow Recommendations

### Development Cycle

```bash
# 1. Make code change
vim Berberis.Messaging/CrossBar.cs

# 2. Quick check (~30 seconds for one category)
dotnet run -c Release -- --quick --filter '*PublishSubscribe*'

# 3. If results look good, run all quick benchmarks (~2-3 min)
dotnet run -c Release -- --quick --filter '*'

# 4. Before committing, run full benchmarks on affected areas
dotnet run -c Release -- --filter '*PublishSubscribe*'
```

### Release Cycle

```bash
# 1. Run full benchmark suite (20-30 min)
dotnet run -c Release -- --filter '*'

# 2. Save as baseline
cp BenchmarkDotNet.Artifacts/results/*.json results/baselines/baseline-v1.2.0.json

# 3. Compare with previous release
# (Manual comparison of JSON files)

# 4. Document performance changes in CHANGELOG
```

---

## ⚠️ Limitations of Quick Mode

**DO NOT use quick mode for:**

1. **Official Documentation** - Publish standard mode results only
2. **Regression Detection** - Variance too high to detect <20% changes
3. **Performance Claims** - "Our system does X ops/sec" needs precision
4. **Comparative Analysis** - Comparing different algorithms/approaches
5. **Production Tuning** - Fine-tuning requires accurate measurements

**Quick mode is for iteration speed, not measurement precision.**

---

## 📊 Example: Quick vs Standard Results

### Same Benchmark, Different Modes

**Quick Mode:**
```
| Method                | Mean    | StdDev | Allocated |
|-----------------------|---------|--------|-----------|
| Publish_SingleMessage | 45.2 ns | 8.1 ns | 0 B       |
```

**Standard Mode:**
```
| Method                | Mean    | StdDev | Allocated |
|-----------------------|---------|--------|-----------|
| Publish_SingleMessage | 42.7 ns | 0.4 ns | 0 B       |
```

**Analysis:**
- Both show ~45ns performance
- Standard mode has 20x lower variance (0.4ns vs 8.1ns)
- Allocation results identical (deterministic)
- **Use quick for "is it fast enough?"**
- **Use standard for "exactly how fast is it?"**

---

## 🎓 Best Practices

1. **Start with quick mode** - Fast feedback loop
2. **Validate with standard mode** - Before commits/releases
3. **Track official baselines** - Use standard mode only
4. **Document your mode** - Note which mode was used in results
5. **Use dedicated hardware** - Even quick mode benefits from consistent environment

---

## 📚 Additional Resources

- **BenchmarkDotNet Docs:** https://benchmarkdotnet.org
- **README.md:** General benchmark usage and structure
- **PERFORMANCE-COMPARISON.md:** How to compare results across versions
- **BENCHMARK-RESULTS.md:** Baseline performance numbers

---

## ✅ Summary

**Quick mode makes benchmark iteration practical during development.**

- **Before:** 20-30 minutes = painful, rarely run
- **After:** 2-3 minutes = quick feedback, run often

**Use it for fast iteration. Use standard mode for official metrics.**
