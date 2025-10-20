# Performance Testing Guide

Comprehensive guide for running and analyzing performance tests for FirstMake Agent.

## Table of Contents

1. [Overview](#overview)
2. [Test Types](#test-types)
3. [Quick Start](#quick-start)
4. [Benchmarks](#benchmarks)
5. [Load Testing](#load-testing)
6. [Profiling](#profiling)
7. [CI/CD Integration](#cicd-integration)
8. [Analyzing Results](#analyzing-results)
9. [Performance Targets](#performance-targets)
10. [Optimization Tips](#optimization-tips)

## Overview

The performance testing suite validates that FirstMake Agent meets performance requirements and detects regressions.

**Test Infrastructure:**
- **Benchmarks**: BenchmarkDotNet for .NET micro-benchmarks
- **Load Tests**: k6 for HTTP endpoint load testing
- **Profiling**: dotnet-trace/dotnet-counters for runtime analysis
- **CI/CD**: Automated performance testing on every PR

## Test Types

### 1. Micro-Benchmarks
- **Purpose**: Measure individual component performance
- **Tool**: BenchmarkDotNet
- **Scope**: FuzzyMatcher, TextNormalizer, LpOptimizer
- **Duration**: 5-10 minutes
- **Frequency**: Every PR, nightly

### 2. Load Tests
- **Purpose**: Test system under realistic load
- **Tool**: k6
- **Scope**: API endpoints, concurrent users
- **Duration**: 3-5 minutes
- **Frequency**: Every PR to main

### 3. Stress Tests
- **Purpose**: Find breaking points
- **Tool**: k6
- **Scope**: High concurrency, large payloads
- **Duration**: 11 minutes
- **Frequency**: Weekly, manual

### 4. Profiling
- **Purpose**: Find bottlenecks and memory leaks
- **Tool**: dotnet-trace, dotnet-counters
- **Scope**: CPU, memory, allocations
- **Duration**: Variable (30s - 10min)
- **Frequency**: On-demand, when investigating issues

## Quick Start

### Prerequisites

```bash
# Install k6 (load testing)
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg \
  --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | \
  sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6

# Install .NET profiling tools
dotnet tool install --global dotnet-trace
dotnet tool install --global dotnet-counters
dotnet tool install --global dotnet-gcdump
```

### Run All Tests

```bash
cd /workspaces/First-make/tests/Performance

# Ensure services are running
docker-compose up -d  # or start services manually

# Run complete test suite
./run-performance-tests.sh
```

## Benchmarks

### Running Benchmarks

```bash
cd /workspaces/First-make/tests/Performance

# Run all benchmarks
dotnet run -c Release -- --filter "*"

# Run specific benchmark
dotnet run -c Release -- --filter "*FuzzyMatcher*"

# Run with custom configuration
dotnet run -c Release -- \
  --filter "*FuzzyMatcher*" \
  --job short \
  --exporters json html csv
```

### Available Benchmarks

1. **FuzzyMatcherBenchmarks**
   - `Match_100_Items`: Match 100 BOQ items against price base
   - `Match_Single_Item_Against_1000`: Single item vs 1000 entries
   - `Match_Single_Item_Against_100`: Single item vs 100 entries
   - `Match_With_Caching`: Test caching effectiveness

2. **TextNormalizerBenchmarks**
   - `Normalize_Short_Text`: Short strings (10-20 chars)
   - `Normalize_Medium_Text`: Medium strings (50-100 chars)
   - `Normalize_Long_Text`: Long strings (100+ chars)
   - `Normalize_100_Strings`: Batch normalization
   - `Normalize_With_Numbers`: Text with numbers
   - `Normalize_With_Special_Chars`: Text with special characters

### Interpreting Results

```
| Method                      | Mean      | Error    | StdDev   | Allocated |
|-----------------------------|-----------|----------|----------|-----------|
| Match_100_Items             | 85.42 ms  | 1.21 ms  | 1.07 ms  | 15.2 MB   |
| Match_Single_Item_Against_1000 | 12.34 ms | 0.18 ms | 0.16 ms | 2.5 MB    |
```

- **Mean**: Average execution time
- **Error**: 99.9% confidence interval
- **StdDev**: Standard deviation
- **Allocated**: Memory allocated on managed heap

## Load Testing

### Running Load Tests

```bash
cd /workspaces/First-make/tests/Performance

# Set API URL
export API_URL=http://localhost:5000

# Basic load test (3 minutes)
k6 run load-tests/api-endpoints.js

# Stress test (11 minutes)
k6 run load-tests/stress-test.js

# Custom configuration
k6 run --vus 50 --duration 5m load-tests/api-endpoints.js

# With output
k6 run --out json=results.json load-tests/api-endpoints.js
```

### Load Test Scenarios

**api-endpoints.js** - Normal load:
- Ramp: 0 → 5 → 10 → 20 users over 3.5 minutes
- Duration: 3 minutes total
- Tests: health, parse, extract, match, optimize, metrics

**stress-test.js** - High load:
- Ramp: 0 → 50 → 100 → 200 users over 11 minutes
- Duration: 11 minutes total
- Tests: Heavy match operations with large datasets

### Custom Test Scenarios

Create `load-tests/custom.js`:

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 10,
  duration: '30s',
};

export default function () {
  const res = http.get('http://localhost:5000/healthz');
  check(res, { 'status is 200': (r) => r.status === 200 });
  sleep(1);
}
```

Run: `k6 run load-tests/custom.js`

## Profiling

### CPU Profiling

```bash
# Find API process
API_PID=$(pgrep -f 'Api.dll')

# Collect 60-second CPU trace
dotnet-trace collect \
  --process-id $API_PID \
  --profile cpu-sampling \
  --duration 00:01:00 \
  --output api-cpu-trace.nettrace

# Analyze with PerfView (Windows) or speedscope (Web)
# Upload to https://www.speedscope.app/
```

### Memory Profiling

```bash
# Memory dump
dotnet-gcdump collect --process-id $API_PID --output api-memory.gcdump

# Analyze with Visual Studio or dotMemory
```

### Real-time Monitoring

```bash
# System runtime counters
dotnet-counters monitor --process-id $API_PID System.Runtime

# ASP.NET counters
dotnet-counters monitor --process-id $API_PID Microsoft.AspNetCore.Hosting

# Custom metrics
dotnet-counters monitor --process-id $API_PID \
  System.Runtime \
  Microsoft.AspNetCore.Hosting \
  --format json
```

### Key Metrics

- **CPU Usage**: < 50% under normal load
- **Memory**: < 500MB working set
- **GC**: Gen0 < 100/sec, Gen2 < 1/sec
- **Request Rate**: > 100 req/sec for simple endpoints
- **Exceptions**: < 1% error rate

## CI/CD Integration

### GitHub Actions Workflow

Performance tests run automatically:

1. **On PR to main**: Benchmarks + load tests
2. **Weekly (Sunday 2 AM)**: Full suite including stress tests
3. **Manual**: Via workflow_dispatch

### Viewing Results

```bash
# GitHub Actions UI
# 1. Go to Actions tab
# 2. Select "Performance Testing" workflow
# 3. Download artifacts:
#    - benchmark-results
#    - load-test-results

# Artifacts contain:
# - BenchmarkDotNet HTML reports
# - k6 JSON results
# - Performance comparison vs baseline
```

### Regression Detection

PRs fail if:
- Benchmarks are >10% slower than baseline
- Load test thresholds not met:
  - P95 response time > 2s
  - Error rate > 10%

## Analyzing Results

### Benchmark Reports

Open `BenchmarkDotNet.Artifacts/results/*.html` in browser:

- **Summary**: All benchmarks with statistics
- **Charts**: Performance visualizations
- **Outliers**: Anomalous measurements removed
- **Comparison**: Side-by-side comparison (if baseline exists)

### Load Test Reports

k6 outputs JSON with detailed metrics:

```json
{
  "metrics": {
    "http_req_duration": {
      "avg": 145.23,
      "min": 45.12,
      "max": 987.34,
      "p(90)": 234.56,
      "p(95)": 345.67,
      "p(99)": 654.32
    },
    "http_req_failed": {
      "rate": 0.02
    }
  }
}
```

Key metrics:
- **http_req_duration**: Response times
- **http_req_failed**: Error rate
- **http_reqs**: Total requests
- **vus**: Virtual users

### Profiling Traces

1. **Upload .nettrace to speedscope.app**
2. **Analyze flame graph**:
   - Hot path (slowest code paths)
   - Time distribution
   - Call stacks

3. **Common issues**:
   - Synchronous database calls
   - Excessive allocations
   - Inefficient LINQ
   - Missing caching

## Performance Targets

### API Endpoints (P95 Response Time)

| Endpoint | Target | Acceptable | Unacceptable |
|----------|--------|------------|--------------|
| /healthz | < 10ms | < 50ms | > 100ms |
| /parse | < 500ms | < 1s | > 2s |
| /extract | < 2s | < 5s | > 10s |
| /match | < 100ms | < 500ms | > 1s |
| /optimize | < 500ms | < 1s | > 2s |
| /export | < 300ms | < 1s | > 2s |

### Core Operations

| Operation | Target | Notes |
|-----------|--------|-------|
| FuzzyMatcher (100 items) | < 100ms | First call |
| FuzzyMatcher (cached) | < 5ms | Subsequent calls |
| LpOptimizer (100 items) | < 500ms | OR-Tools GLOP solver |
| ExcelExport (100 items) | < 300ms | EPPlus library |
| TextNormalizer | < 1ms | Per string |

### Resource Limits

- **Memory (idle)**: < 200MB per service
- **Memory (loaded)**: < 500MB per service
- **CPU (normal)**: < 50% utilization
- **CPU (peak)**: < 80% utilization
- **Disk I/O**: < 10MB/s sustained

### Throughput

- **Parse**: 20+ requests/second
- **Match**: 50+ requests/second
- **Optimize**: 10+ requests/second
- **Health**: 1000+ requests/second

## Optimization Tips

### Database

```csharp
// Enable WAL mode for SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(connectionString, sqliteOptions =>
    {
        sqliteOptions.CommandTimeout(30);
    });
});

// In DbContext
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder.UseSqlite("Data Source=app.db;Mode=ReadWriteCreate;Cache=Shared");
}

// Create indexes
CREATE INDEX idx_observations_timestamp ON observations(timestamp);
CREATE INDEX idx_observations_operation ON observations(operation);
```

### Caching

```csharp
// Response caching
builder.Services.AddResponseCaching();
app.UseResponseCaching();

// In-memory caching
builder.Services.AddMemoryCache();

// Distributed caching (for multi-instance)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});
```

### OR-Tools Optimizer

```csharp
// Pre-warm solver on startup
public class OptimizerWarmer : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var optimizer = new LpOptimizer();
        // Run dummy optimization to load solver
        optimizer.Optimize(new OptimizationRequest { /* ... */ });
        return Task.CompletedTask;
    }
}

// Tune solver parameters
solver.SetSolverSpecificParametersAsString("max_time_in_seconds:5.0");
```

### Memory

```csharp
// Use object pooling
builder.Services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();

// Configure GC
// In .csproj:
<ServerGarbageCollection>true</ServerGarbageCollection>
<ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>

// Use ArrayPool for large arrays
var buffer = ArrayPool<byte>.Shared.Rent(size);
try { /* use buffer */ }
finally { ArrayPool<byte>.Shared.Return(buffer); }
```

### Async/Await

```csharp
// Always await database calls
var result = await dbContext.Observations.ToListAsync();

// Use ConfigureAwait(false) in libraries
var data = await FetchDataAsync().ConfigureAwait(false);

// Parallel processing
await Parallel.ForEachAsync(items, async (item, ct) =>
{
    await ProcessAsync(item, ct);
});
```

## Troubleshooting

### Benchmarks Taking Too Long

```bash
# Use shorter job
dotnet run -c Release -- --job short --filter "*"

# Reduce iterations
dotnet run -c Release -- --iterationCount 3 --filter "*"
```

### k6 Tests Failing

```bash
# Check services are running
curl http://localhost:5000/healthz
curl http://localhost:5001/healthz

# Increase thresholds temporarily
# Edit load-tests/api-endpoints.js:
thresholds: {
  http_req_duration: ['p(95)<5000'], // Was 2000
}

# Reduce load
k6 run --vus 5 --duration 30s load-tests/api-endpoints.js
```

### High Memory Usage

```bash
# Check for memory leaks
dotnet-gcdump collect --process-id $API_PID
# Analyze in Visual Studio or dotMemory

# Monitor GC
dotnet-counters monitor --process-id $API_PID System.Runtime

# Force GC
curl -X POST http://localhost:5000/gc  # If endpoint exists
```

### Slow Performance

```bash
# Profile CPU
dotnet-trace collect --process-id $API_PID --profile cpu-sampling --duration 00:01:00

# Check database queries
# Enable query logging in appsettings.Development.json:
"Logging": {
  "LogLevel": {
    "Microsoft.EntityFrameworkCore.Database.Command": "Information"
  }
}

# Check OR-Tools
# Add logging to LpOptimizer.cs
Console.WriteLine($"Solver time: {solver.WallTime()}ms");
```

## References

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [k6 Documentation](https://k6.io/docs/)
- [.NET Performance Tools](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/)
- [Speedscope](https://www.speedscope.app/) - Trace visualization
- [PerfView](https://github.com/microsoft/perfview) - Windows profiler

---

**Last Updated:** October 20, 2025
