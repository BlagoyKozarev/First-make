#!/bin/bash

###############################################################################
# FirstMake Performance Testing Suite
# 
# Runs benchmarks, load tests, and generates performance reports
###############################################################################

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPORTS_DIR="${SCRIPT_DIR}/reports"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

# API URL
API_URL="${API_URL:-http://localhost:5000}"
AI_GATEWAY_URL="${AI_GATEWAY_URL:-http://localhost:5001}"

###############################################################################
# Functions
###############################################################################

print_header() {
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

check_prerequisites() {
    print_header "Checking Prerequisites"

    # Check if services are running
    if ! curl -s "${API_URL}/healthz" > /dev/null 2>&1; then
        print_error "API service not running at ${API_URL}"
        print_info "Please start the API service first"
        exit 1
    fi
    print_success "API service is running"

    if ! curl -s "${AI_GATEWAY_URL}/healthz" > /dev/null 2>&1; then
        print_warning "AI Gateway service not running at ${AI_GATEWAY_URL}"
        print_info "Some tests may be skipped"
    else
        print_success "AI Gateway service is running"
    fi

    # Check for required tools
    if ! command -v dotnet &> /dev/null; then
        print_error "dotnet CLI not found"
        exit 1
    fi
    print_success "dotnet CLI found"

    if command -v k6 &> /dev/null; then
        print_success "k6 found"
    else
        print_warning "k6 not found - load tests will be skipped"
        print_info "Install k6 from https://k6.io/docs/getting-started/installation/"
    fi

    echo ""
}

run_benchmarks() {
    print_header "Running .NET Benchmarks"

    cd "${SCRIPT_DIR}"
    
    print_info "Building benchmark project..."
    dotnet build -c Release --nologo

    print_info "Running benchmarks (this may take several minutes)..."
    dotnet run -c Release --no-build -- --filter "*" \
        --exporters json html \
        --artifacts "${REPORTS_DIR}/benchmarks"

    if [ $? -eq 0 ]; then
        print_success "Benchmarks completed successfully"
        print_info "Reports saved to: ${REPORTS_DIR}/benchmarks"
    else
        print_error "Benchmarks failed"
        return 1
    fi

    echo ""
}

run_load_tests() {
    print_header "Running Load Tests"

    if ! command -v k6 &> /dev/null; then
        print_warning "Skipping load tests - k6 not installed"
        return 0
    fi

    mkdir -p "${REPORTS_DIR}/load-tests"

    # API endpoints test
    print_info "Running API endpoints load test..."
    API_URL="${API_URL}" k6 run \
        --out json="${REPORTS_DIR}/load-tests/api-endpoints_${TIMESTAMP}.json" \
        "${SCRIPT_DIR}/load-tests/api-endpoints.js"

    if [ $? -eq 0 ]; then
        print_success "API load test completed"
    else
        print_error "API load test failed"
    fi

    # Stress test
    print_info "Running stress test (this will take ~11 minutes)..."
    read -p "Do you want to run the stress test? (y/N) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        API_URL="${API_URL}" k6 run \
            --out json="${REPORTS_DIR}/load-tests/stress-test_${TIMESTAMP}.json" \
            "${SCRIPT_DIR}/load-tests/stress-test.js"

        if [ $? -eq 0 ]; then
            print_success "Stress test completed"
        else
            print_error "Stress test failed"
        fi
    else
        print_warning "Stress test skipped"
    fi

    echo ""
}

run_profiling() {
    print_header "Performance Profiling"

    print_info "To profile the API service, run these commands:"
    echo ""
    echo "  # CPU profiling (60 seconds)"
    echo "  dotnet-trace collect --process-id \$(pgrep -f 'Api.dll') \\"
    echo "      --profile cpu-sampling \\"
    echo "      --duration 00:01:00 \\"
    echo "      --output ${REPORTS_DIR}/api-cpu-trace.nettrace"
    echo ""
    echo "  # Memory dump"
    echo "  dotnet-gcdump collect --process-id \$(pgrep -f 'Api.dll') \\"
    echo "      --output ${REPORTS_DIR}/api-memory.gcdump"
    echo ""
    echo "  # Real-time counters"
    echo "  dotnet-counters monitor --process-id \$(pgrep -f 'Api.dll') \\"
    echo "      System.Runtime Microsoft.AspNetCore.Hosting"
    echo ""

    read -p "Do you want to collect CPU trace now? (y/N) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        if command -v dotnet-trace &> /dev/null; then
            mkdir -p "${REPORTS_DIR}/profiling"
            print_info "Collecting 60-second CPU trace..."
            
            API_PID=$(pgrep -f 'Api.dll' | head -n 1)
            if [ -z "$API_PID" ]; then
                print_error "Could not find API process"
            else
                dotnet-trace collect \
                    --process-id $API_PID \
                    --profile cpu-sampling \
                    --duration 00:01:00 \
                    --output "${REPORTS_DIR}/profiling/api-cpu-trace_${TIMESTAMP}.nettrace"
                
                print_success "CPU trace saved to ${REPORTS_DIR}/profiling/"
            fi
        else
            print_warning "dotnet-trace not found"
            print_info "Install with: dotnet tool install --global dotnet-trace"
        fi
    fi

    echo ""
}

generate_summary() {
    print_header "Performance Test Summary"

    echo "Test run completed at: $(date)"
    echo ""
    echo "Reports location: ${REPORTS_DIR}"
    echo ""

    if [ -d "${REPORTS_DIR}/benchmarks" ]; then
        echo "Benchmarks:"
        echo "  - HTML report: ${REPORTS_DIR}/benchmarks/results/*.html"
        echo "  - JSON data: ${REPORTS_DIR}/benchmarks/results/*.json"
    fi

    if [ -d "${REPORTS_DIR}/load-tests" ]; then
        echo "Load tests:"
        ls -1 "${REPORTS_DIR}/load-tests"/*.json 2>/dev/null | while read file; do
            echo "  - $(basename $file)"
        done
    fi

    if [ -d "${REPORTS_DIR}/profiling" ]; then
        echo "Profiling data:"
        ls -1 "${REPORTS_DIR}/profiling"/* 2>/dev/null | while read file; do
            echo "  - $(basename $file)"
        done
    fi

    echo ""
    print_success "All performance tests completed!"
}

###############################################################################
# Main
###############################################################################

main() {
    print_header "FirstMake Performance Testing Suite"
    echo "Timestamp: ${TIMESTAMP}"
    echo "API URL: ${API_URL}"
    echo ""

    # Create reports directory
    mkdir -p "${REPORTS_DIR}"

    # Run tests
    check_prerequisites
    run_benchmarks
    run_load_tests
    run_profiling
    generate_summary
}

main "$@"
