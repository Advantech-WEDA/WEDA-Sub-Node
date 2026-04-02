#!/bin/bash
# Build and quick-verify a SubNode project
# Usage: verify-project.sh <project-directory>
# Returns: exit 0 = all checks pass, exit 1 = failure
#
# Steps:
#   1. dotnet build
#   2. Run for 10 seconds, capture logs
#   3. Check for success/error markers in output

set -euo pipefail

PROJECT_DIR="${1:?Usage: verify-project.sh <project-directory>}"

if [ ! -d "$PROJECT_DIR" ]; then
    echo "FAIL: Directory not found: $PROJECT_DIR"
    exit 1
fi

CSPROJ=$(find "$PROJECT_DIR" -maxdepth 1 -name "*.csproj" | head -1)
if [ -z "$CSPROJ" ]; then
    echo "FAIL: No .csproj file found in $PROJECT_DIR"
    exit 1
fi

PROJECT_NAME=$(basename "$CSPROJ" .csproj)
LOG_FILE=$(mktemp /tmp/subnode-verify-XXXXXX.log)
ERRORS=0

echo "=== SubNode Project Verification ==="
echo "Project:   $PROJECT_NAME"
echo "Directory: $PROJECT_DIR"
echo "Log file:  $LOG_FILE"
echo ""

# Step 1: Build
echo "[1/3] Building project..."
if dotnet build "$PROJECT_DIR" --nologo -v q 2>&1 | tee -a "$LOG_FILE"; then
    echo "  PASS: Build succeeded"
else
    echo "  FAIL: Build failed"
    echo ""
    echo "Build output saved to: $LOG_FILE"
    exit 1
fi

# Step 2: Run for a few seconds
echo "[2/3] Running project (10 seconds)..."
RUN_LOG=$(mktemp /tmp/subnode-run-XXXXXX.log)

dotnet run --project "$PROJECT_DIR" --no-build -- --Serilog:MinimumLevel:Default=Information 2>&1 > "$RUN_LOG" &
RUN_PID=$!

sleep 10
kill "$RUN_PID" 2>/dev/null || true
wait "$RUN_PID" 2>/dev/null || true

cat "$RUN_LOG" >> "$LOG_FILE"

# Step 3: Check log markers
echo "[3/3] Analyzing runtime output..."

# Success markers
check_marker() {
    local pattern="$1"
    local label="$2"
    if grep -qi "$pattern" "$RUN_LOG" 2>/dev/null; then
        echo "  PASS: $label"
        return 0
    else
        echo "  WARN: $label not detected"
        return 1
    fi
}

# Check for critical errors
if grep -qiE "(fatal|unhandled exception|crash)" "$RUN_LOG" 2>/dev/null; then
    echo "  FAIL: Critical error detected in logs"
    grep -iE "(fatal|unhandled exception|crash)" "$RUN_LOG" | head -3 | sed 's/^/    /'
    ERRORS=$((ERRORS + 1))
fi

# Check expected markers
check_marker "simulator.*started\|TcpModbusSimulator" "Simulator started" || true
check_marker "mock cloud\|MOCK CLOUD" "Mock cloud active" || true
check_marker "device.*connect\|polling\|data received" "Device communication" || true
check_marker "sensor\|telemetry\|report" "Sensor reporting" || true

echo ""
echo "--- Last 20 lines of runtime output ---"
tail -20 "$RUN_LOG" | sed 's/^/  /'
echo "--- End of output ---"

# Cleanup
rm -f "$RUN_LOG"

echo ""
if [ "$ERRORS" -gt 0 ]; then
    echo "RESULT: Verification failed ($ERRORS error(s))"
    echo "Full log: $LOG_FILE"
    exit 1
else
    echo "RESULT: Verification passed"
    rm -f "$LOG_FILE"
    exit 0
fi
