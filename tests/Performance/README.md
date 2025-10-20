# Performance Testing Suite

Load testing, benchmarks, and performance profiling for FirstMake Agent.

## 📊 Test Categories

### 1. Load Testing
- API endpoint stress tests
- Concurrent user simulation
- File upload performance
- Database query performance

### 2. Benchmarks
- Core.Engine operations
- Fuzzy matching performance
- LP optimization speed
- Excel export throughput

### 3. Memory Profiling
- Memory leak detection
- Resource usage monitoring
- Garbage collection analysis

## 🛠️ Tools

### Load Testing
- **k6** - Modern load testing tool
- **Apache Bench (ab)** - HTTP server benchmarking
- **wrk** - HTTP benchmarking tool

### Profiling
- **dotnet-trace** - .NET performance traces
- **dotnet-counters** - Real-time metrics
- **BenchmarkDotNet** - .NET benchmarking library

## 🚀 Running Tests

### Quick Start

```bash
# Install dependencies
sudo apt-get install -y apache2-utils wrk

# Install k6
wget -q -O - https://dl.k6.io/key.gpg | sudo apt-key add -
echo "deb https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6

# Run all tests
./run-performance-tests.sh
```

### Individual Tests

```bash
# API load test
k6 run --vus 10 --duration 30s load-tests/api-endpoints.js

# Fuzzy matcher benchmark
cd /workspaces/First-make
dotnet run --project tests/Performance/PerformanceBench mark.csproj -c Release

# Memory profiling
dotnet-trace collect --process-id $(pgrep -f "Api.dll") --profile cpu-sampling
```

## 📈 Performance Targets

### API Endpoints

| Endpoint | Target Response Time (p95) | Throughput (req/s) |
|----------|----------------------------|-------------------|
| /healthz | < 10ms | 1000+ |
| /parse | < 500ms | 20+ |
| /extract | < 2s | 5+ |
| /match | < 100ms | 50+ |
| /optimize | < 500ms | 10+ |
| /export | < 300ms | 20+ |

### Core Operations

| Operation | Target Time | Notes |
|-----------|-------------|-------|
| FuzzyMatcher.Match (100 items) | < 100ms | Cached: < 5ms |
| LpOptimizer.Optimize (100 items) | < 500ms | OR-Tools GLOP |
| ExcelExport.Generate (100 items) | < 300ms | EPPlus |
| TextNormalizer.Normalize | < 1ms | Per string |

### Resource Limits

- **Memory**: < 500MB per service (idle)
- **CPU**: < 50% utilization (normal load)
- **Disk I/O**: < 10MB/s sustained

## 📝 Test Results

### Latest Benchmark Results

```
// Run benchmarks to populate
dotnet run --project tests/Performance/PerformanceBenchmark.csproj -c Release
```

Results are saved to `BenchmarkDotNet.Artifacts/results/`.

### Load Test Reports

K6 generates HTML reports in `load-tests/reports/`.

## 🔧 Optimization Tips

### Database
- Enable WAL mode for SQLite
- Create indexes on frequently queried columns
- VACUUM periodically

### Caching
- Enable response caching for static endpoints
- Use distributed cache for multi-instance deployments
- Configure cache expiration appropriately

### OR-Tools
- Pre-warm solver on startup
- Reuse solver instances where possible
- Tune iteration limits for speed vs accuracy trade-off

### Memory
- Use object pooling for large allocations
- Dispose resources promptly
- Configure GC modes (Server GC for high throughput)

## 📊 Monitoring

### Real-time Metrics

```bash
# .NET Counters
dotnet-counters monitor --process-id $(pgrep -f "Api.dll") \
    System.Runtime \
    Microsoft.AspNetCore.Hosting

# Custom metrics endpoint
curl http://localhost:5000/observations/metrics | jq
```

### Profiling

```bash
# CPU profiling
dotnet-trace collect --process-id $(pgrep -f "Api.dll") \
    --profile cpu-sampling \
    --duration 00:01:00

# Memory profiling
dotnet-gcdump collect --process-id $(pgrep -f "Api.dll")

# Analyze traces
dotnet-trace analyze trace.nettrace
```

## 🎯 Continuous Monitoring

### GitHub Actions Integration

Performance tests run automatically on:
- Pull requests to `main`
- Nightly scheduled builds
- Manual workflow dispatch

Results are published as workflow artifacts.

### Regression Detection

Tests fail if performance degrades by more than 10% compared to baseline.

## 📚 References

- [k6 Documentation](https://k6.io/docs/)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)
- [.NET Performance Tools](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/)
- [Apache Bench Guide](https://httpd.apache.org/docs/2.4/programs/ab.html)

---

**Last Updated:** October 20, 2025
