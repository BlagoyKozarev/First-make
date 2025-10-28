#!/bin/bash
# Production Deployment Validation Script
# Run this before deploying to production

set -e

echo "=================================="
echo "FirstMake Agent - Pre-Deployment Validation"
echo "=================================="
echo ""

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

ERRORS=0
WARNINGS=0

# Check function
check() {
  local name="$1"
  local status="$2"
  
  if [ "$status" == "PASS" ]; then
    echo -e "✅ ${GREEN}PASS${NC} - $name"
  elif [ "$status" == "WARN" ]; then
    echo -e "⚠️  ${YELLOW}WARN${NC} - $name"
    ((WARNINGS++))
  else
    echo -e "❌ ${RED}FAIL${NC} - $name"
    ((ERRORS++))
  fi
}

echo "1. Checking Required Files..."
echo "--------------------------------"

[ -f "deployment/docker-compose.prod.yml" ] && check "docker-compose.prod.yml exists" "PASS" || check "docker-compose.prod.yml exists" "FAIL"
[ -f "deployment/.env.example" ] && check ".env.example exists" "PASS" || check ".env.example exists" "FAIL"
[ -f "Dockerfile" ] && check "Dockerfile exists" "PASS" || check "Dockerfile exists" "FAIL"
[ -f "src/UI/Dockerfile" ] && check "UI Dockerfile exists" "PASS" || check "UI Dockerfile exists" "FAIL"
[ -f "docs/SECURITY.md" ] && check "SECURITY.md exists" "PASS" || check "SECURITY.md exists" "FAIL"

echo ""
echo "2. Checking Environment Configuration..."
echo "--------------------------------"

if [ -f "deployment/.env" ]; then
  check ".env file exists" "PASS"
  
  # Check required env vars
  source deployment/.env
  
  [ -n "$VERSION" ] && check "VERSION is set" "PASS" || check "VERSION is set" "WARN"
  [ -n "$BGGPT_USERNAME" ] && [ "$BGGPT_USERNAME" != "your-username-here" ] && check "BGGPT_USERNAME configured" "PASS" || check "BGGPT_USERNAME configured" "FAIL"
  [ -n "$BGGPT_PASSWORD" ] && [ "$BGGPT_PASSWORD" != "your-strong-password-here" ] && check "BGGPT_PASSWORD configured" "PASS" || check "BGGPT_PASSWORD configured" "FAIL"
  [ -n "$BGGPT_API_KEY" ] && [ "$BGGPT_API_KEY" != "your-api-key-here" ] && check "BGGPT_API_KEY configured" "PASS" || check "BGGPT_API_KEY configured" "FAIL"
else
  check ".env file exists" "FAIL"
  echo "   Run: cp deployment/.env.example deployment/.env"
fi

echo ""
echo "3. Checking Git Repository..."
echo "--------------------------------"

if command -v git &> /dev/null; then
  # Check for uncommitted changes
  if [ -z "$(git status --porcelain)" ]; then
    check "No uncommitted changes" "PASS"
  else
    check "No uncommitted changes" "WARN"
  fi
  
  # Check current tag
  CURRENT_TAG=$(git describe --tags --exact-match 2>/dev/null || echo "none")
  if [ "$CURRENT_TAG" != "none" ]; then
    check "On tagged release: $CURRENT_TAG" "PASS"
  else
    check "On tagged release" "WARN"
    echo "   Consider: git tag -a v1.0.1 -m 'Release v1.0.1' && git push origin v1.0.1"
  fi
else
  check "Git available" "WARN"
fi

echo ""
echo "4. Checking Secret Hygiene..."
echo "--------------------------------"

# Check if sensitive files are ignored
if [ -f ".gitignore" ]; then
  grep -q "appsettings.json" .gitignore && check "appsettings.json in .gitignore" "PASS" || check "appsettings.json in .gitignore" "FAIL"
  grep -q ".env" .gitignore && check ".env in .gitignore" "PASS" || check ".env in .gitignore" "FAIL"
else
  check ".gitignore exists" "FAIL"
fi

# Check for hardcoded secrets in appsettings.json
if [ -f "src/AiGateway/appsettings.json" ]; then
  if grep -q "CHANGE_ME" src/AiGateway/appsettings.json; then
    check "appsettings.json sanitized" "PASS"
  elif grep -q "R@icommerce23" src/AiGateway/appsettings.json; then
    check "appsettings.json sanitized" "FAIL"
    echo "   Old credentials still present!"
  else
    check "appsettings.json sanitized" "WARN"
  fi
fi

echo ""
echo "5. Checking Docker Configuration..."
echo "--------------------------------"

if command -v docker &> /dev/null; then
  check "Docker installed" "PASS"
  
  # Check if Docker daemon is running
  if docker info &> /dev/null; then
    check "Docker daemon running" "PASS"
  else
    check "Docker daemon running" "FAIL"
  fi
else
  check "Docker installed" "WARN"
  echo "   Install: https://docs.docker.com/get-docker/"
fi

# Check Dockerfile syntax
if command -v docker &> /dev/null; then
  if docker build --target api-runtime -t firstmake-api:validation-test -f Dockerfile . &> /dev/null; then
    check "Dockerfile builds successfully" "PASS"
    docker rmi firstmake-api:validation-test &> /dev/null
  else
    check "Dockerfile builds successfully" "FAIL"
  fi
fi

echo ""
echo "6. Checking Network & Ports..."
echo "--------------------------------"

# Check if ports are available
if command -v netstat &> /dev/null || command -v ss &> /dev/null; then
  PORT_TOOL="netstat"
  command -v ss &> /dev/null && PORT_TOOL="ss"
  
  for PORT in 80 443 5000 5001; do
    if $PORT_TOOL -tuln 2>/dev/null | grep -q ":$PORT "; then
      check "Port $PORT available" "WARN"
      echo "   Port $PORT already in use"
    else
      check "Port $PORT available" "PASS"
    fi
  done
else
  check "Network utilities available" "WARN"
fi

echo ""
echo "7. Checking System Resources..."
echo "--------------------------------"

# Disk space
DISK_AVAIL=$(df -h . | tail -1 | awk '{print $4}')
check "Disk space: $DISK_AVAIL available" "PASS"

# Memory
if command -v free &> /dev/null; then
  MEM_AVAIL=$(free -h | grep Mem | awk '{print $7}')
  check "Memory: $MEM_AVAIL available" "PASS"
fi

echo ""
echo "=================================="
echo "Validation Summary"
echo "=================================="
echo ""

if [ $ERRORS -eq 0 ] && [ $WARNINGS -eq 0 ]; then
  echo -e "${GREEN}✅ ALL CHECKS PASSED${NC}"
  echo ""
  echo "Ready to deploy! 🚀"
  echo ""
  echo "Next steps:"
  echo "  1. cd deployment"
  echo "  2. docker-compose -f docker-compose.prod.yml pull"
  echo "  3. docker-compose -f docker-compose.prod.yml up -d"
  echo "  4. docker-compose -f docker-compose.prod.yml logs -f"
  exit 0
elif [ $ERRORS -eq 0 ]; then
  echo -e "${YELLOW}⚠️  CHECKS PASSED WITH $WARNINGS WARNINGS${NC}"
  echo ""
  echo "Review warnings before deploying."
  exit 0
else
  echo -e "${RED}❌ VALIDATION FAILED${NC}"
  echo ""
  echo "Errors: $ERRORS"
  echo "Warnings: $WARNINGS"
  echo ""
  echo "Fix all errors before deploying!"
  exit 1
fi
