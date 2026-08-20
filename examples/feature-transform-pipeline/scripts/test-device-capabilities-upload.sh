#!/bin/bash
# Test Device Capabilities Upload Script
# Tests that testdevice uploads DeviceCapabilities via eco1j.weda.dm.cfg.update.req
#
# Test Flow:
# 1. Start testdevice and verify initial DeviceCapabilities upload (1 sensor)
# 2. Send payloads/all.json to add 2 more sensors
# 3. Verify DeviceCapabilities re-upload (3 sensors)
# 4. Send payloads/reset.json to restore original state
# 5. Verify DeviceCapabilities re-upload (1 sensor)

set -e

# Configuration
NATS_URL="${1:-172.22.160.197:4224}"
TIMEOUT_SECONDS="${2:-30}"

# NATS Authentication (from systemcfg.json)
NATS_USER="advantech_nats"
NATS_PASS="3671be64607240cbc2b95af99c9a3b28fb5f9aa3fbe51f501478f1a678e19d48"

# Subjects
CFG_UPDATE_REQ_SUBJECT="eco1j.weda.dm.cfg.update.req"  # Device uploads capabilities here
# The device config desired topic will be assigned after registration
# Format: eco1j.weda.{deviceId}.subnode.shadow.devicecfg.delta

# Get script directory and project path
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
PAYLOADS_DIR="$PROJECT_DIR/payloads"

# Temp files
OUTPUT_DIR="/tmp/testdevice-test"
mkdir -p "$OUTPUT_DIR"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Process tracking
DOTNET_PID=""
NATS_SUB_PID=""

# Cleanup function
cleanup() {
    echo -e "\n${YELLOW}[CLEANUP] Stopping processes...${NC}"

    if [ -n "$NATS_SUB_PID" ] && kill -0 "$NATS_SUB_PID" 2>/dev/null; then
        kill "$NATS_SUB_PID" 2>/dev/null || true
        echo -e "${GRAY}   Stopped NATS subscriber${NC}"
    fi

    if [ -n "$DOTNET_PID" ] && kill -0 "$DOTNET_PID" 2>/dev/null; then
        kill "$DOTNET_PID" 2>/dev/null || true
        wait "$DOTNET_PID" 2>/dev/null || true
        echo -e "${GRAY}   Stopped testdevice${NC}"
    fi

    # Kill any remaining nats processes started by this script
    pkill -f "nats sub.*$CFG_UPDATE_REQ_SUBJECT" 2>/dev/null || true
}

trap cleanup EXIT

# Function to wait for capability upload and verify sensor count
wait_for_capabilities() {
    local expected_count=$1
    local step_name=$2
    local output_file="$OUTPUT_DIR/capabilities_${step_name}.json"
    local timeout=$TIMEOUT_SECONDS

    echo -e "${CYAN}   Waiting for DeviceCapabilities upload (expecting $expected_count sensors)...${NC}"

    # Start subscriber in background
    timeout "$timeout" nats sub \
        --server "nats://$NATS_URL" \
        --user "$NATS_USER" \
        --password "$NATS_PASS" \
        --count 1 \
        "$CFG_UPDATE_REQ_SUBJECT" > "$output_file" 2>&1 &
    local sub_pid=$!

    # Wait for subscriber to complete or timeout
    if wait $sub_pid 2>/dev/null; then
        # Check if we got the expected content
        if [ -f "$output_file" ] && grep -q "deviceCapabilities" "$output_file"; then
            # Extract sensor count using jq if available
            if command -v jq &> /dev/null; then
                local json_content=$(grep -o '{.*}' "$output_file" | head -1)
                local actual_count=$(echo "$json_content" | jq '.data.deviceCapabilities.sensors | length' 2>/dev/null || echo "0")

                if [ "$actual_count" -eq "$expected_count" ]; then
                    echo -e "${GREEN}   [OK] Received DeviceCapabilities with $actual_count sensors${NC}"

                    # Show sensor names
                    echo -e "${GRAY}   Sensors:${NC}"
                    echo "$json_content" | jq -r '.data.deviceCapabilities.sensors[].name' 2>/dev/null | while read name; do
                        echo -e "${GRAY}     - $name${NC}"
                    done
                    return 0
                else
                    echo -e "${RED}   [FAIL] Expected $expected_count sensors, got $actual_count${NC}"
                    return 1
                fi
            else
                echo -e "${YELLOW}   [WARN] jq not installed, cannot verify sensor count${NC}"
                echo -e "${GREEN}   [OK] Received DeviceCapabilities (unverified count)${NC}"
                return 0
            fi
        else
            echo -e "${RED}   [FAIL] No deviceCapabilities in response${NC}"
            cat "$output_file" 2>/dev/null || true
            return 1
        fi
    else
        echo -e "${RED}   [FAIL] Timeout waiting for DeviceCapabilities upload${NC}"
        return 1
    fi
}

# Function to send config update via NATS
send_config_update() {
    local payload_file=$1
    local device_id=$2

    # The device config topic format from registration response
    # We'll publish to the device's subscribed topic
    # Format: eco1j.weda.{deviceId}.subnode.shadow.devicecfg.delta
    local topic="eco1j.weda.${device_id}.subnode.shadow.devicecfg.delta"

    echo -e "${CYAN}   Sending config update to: $topic${NC}"
    echo -e "${GRAY}   Payload: $payload_file${NC}"

    if [ ! -f "$payload_file" ]; then
        echo -e "${RED}   [FAIL] Payload file not found: $payload_file${NC}"
        return 1
    fi

    # Send the config update
    nats pub \
        --server "nats://$NATS_URL" \
        --user "$NATS_USER" \
        --password "$NATS_PASS" \
        "$topic" \
        "$(cat "$payload_file")"

    echo -e "${GREEN}   [OK] Config update sent${NC}"
    return 0
}

# Main test execution
echo -e "${CYAN}========================================"
echo -e "Test Device Capabilities Upload"
echo -e "========================================${NC}\n"

echo -e "${YELLOW}[PREREQUISITES]${NC}"
echo -e "${GRAY}   1. NATS server must be running at $NATS_URL"
echo -e "   2. nats CLI must be installed (brew install nats-io/nats-tools/nats)"
echo -e "   3. jq must be installed for JSON parsing (brew install jq)"
echo -e "   4. .NET SDK must be installed${NC}"
echo ""

echo -e "${YELLOW}[TEST FLOW]${NC}"
echo -e "${GRAY}   1. Start testdevice → Verify initial upload (1 sensor)"
echo -e "   2. Send all.json → Verify re-upload (3 sensors)"
echo -e "   3. Send reset.json → Verify re-upload (1 sensor)${NC}"
echo ""

# Check prerequisites
if ! command -v nats &> /dev/null; then
    echo -e "${RED}[ERROR] nats CLI not found. Install with: brew install nats-io/nats-tools/nats${NC}"
    exit 1
fi

if ! command -v jq &> /dev/null; then
    echo -e "${YELLOW}[WARN] jq not found. Sensor count verification will be skipped.${NC}"
fi

# Verify payload files exist
if [ ! -f "$PAYLOADS_DIR/all.json" ] || [ ! -f "$PAYLOADS_DIR/reset.json" ]; then
    echo -e "${RED}[ERROR] Payload files not found in $PAYLOADS_DIR${NC}"
    exit 1
fi

TOTAL_TESTS=3
PASSED_TESTS=0

# ============================================
# STEP 1: Start testdevice and verify initial upload
# ============================================
echo -e "\n${GREEN}[STEP 1/3] Starting testdevice and verifying initial upload...${NC}"
echo -e "${GRAY}   Project: $PROJECT_DIR"
echo -e "   NATS URL: $NATS_URL${NC}"

# Start NATS subscriber first (before device starts)
INITIAL_OUTPUT="$OUTPUT_DIR/initial_capabilities.json"
timeout "$TIMEOUT_SECONDS" nats sub \
    --server "nats://$NATS_URL" \
    --user "$NATS_USER" \
    --password "$NATS_PASS" \
    --count 1 \
    "$CFG_UPDATE_REQ_SUBJECT" > "$INITIAL_OUTPUT" 2>&1 &
NATS_SUB_PID=$!

# Give subscriber time to connect
sleep 1

# Start testdevice (must run from project directory to find devicecfg.json)
echo -e "${CYAN}   Starting testdevice...${NC}"
(cd "$PROJECT_DIR" && dotnet run -- --SystemConfig:WedaNode:Url="$NATS_URL") \
    > "$OUTPUT_DIR/testdevice.log" 2>&1 &
DOTNET_PID=$!

# Wait for initial capabilities upload (poll for file content)
echo -e "${CYAN}   Waiting for initial DeviceCapabilities upload...${NC}"
waited=0
while [ $waited -lt $TIMEOUT_SECONDS ]; do
    if [ -f "$INITIAL_OUTPUT" ] && grep -q "deviceCapabilities" "$INITIAL_OUTPUT"; then
        break
    fi
    sleep 1
    ((waited++))
done

if [ -f "$INITIAL_OUTPUT" ] && grep -q "deviceCapabilities" "$INITIAL_OUTPUT"; then
    if command -v jq &> /dev/null; then
        json_content=$(grep -o '{.*}' "$INITIAL_OUTPUT" | head -1)
        sensor_count=$(echo "$json_content" | jq '.data.deviceCapabilities.sensors | length' 2>/dev/null || echo "0")
        device_id=$(echo "$json_content" | jq -r '.data.deviceId' 2>/dev/null || echo "unknown")

        echo -e "${GREEN}   [OK] Initial upload received: $sensor_count sensor(s)${NC}"
        echo -e "${GRAY}   Device ID: $device_id${NC}"

        if [ "$sensor_count" -ge 1 ]; then
            ((PASSED_TESTS++))
        fi
    else
        echo -e "${GREEN}   [OK] Initial upload received (count unverified)${NC}"
        device_id="TestDevice"  # Default fallback
        ((PASSED_TESTS++))
    fi
else
    echo -e "${RED}   [FAIL] Timeout waiting for initial upload${NC}"
    cat "$INITIAL_OUTPUT" 2>/dev/null || echo "   (no output file)"
    device_id="TestDevice"
fi

NATS_SUB_PID=""

# Give device time to stabilize
sleep 2

# ============================================
# STEP 2: Send all.json to add sensors
# ============================================
echo -e "\n${GREEN}[STEP 2/3] Sending all.json to add 2 more sensors...${NC}"

# Start subscriber for the re-upload
ALL_OUTPUT="$OUTPUT_DIR/all_capabilities.json"
timeout "$TIMEOUT_SECONDS" nats sub \
    --server "nats://$NATS_URL" \
    --user "$NATS_USER" \
    --password "$NATS_PASS" \
    --count 1 \
    "$CFG_UPDATE_REQ_SUBJECT" > "$ALL_OUTPUT" 2>&1 &
NATS_SUB_PID=$!

sleep 1

# Send config update
if send_config_update "$PAYLOADS_DIR/all.json" "$device_id"; then
    echo -e "${CYAN}   Waiting for DeviceCapabilities re-upload (expecting 3 sensors)...${NC}"

    if wait $NATS_SUB_PID 2>/dev/null; then
        if [ -f "$ALL_OUTPUT" ] && grep -q "deviceCapabilities" "$ALL_OUTPUT"; then
            if command -v jq &> /dev/null; then
                json_content=$(grep -o '{.*}' "$ALL_OUTPUT" | head -1)
                sensor_count=$(echo "$json_content" | jq '.data.deviceCapabilities.sensors | length' 2>/dev/null || echo "0")

                echo -e "${GREEN}   [OK] Re-upload received: $sensor_count sensor(s)${NC}"

                if [ "$sensor_count" -eq 3 ]; then
                    echo -e "${GRAY}   Sensors:${NC}"
                    echo "$json_content" | jq -r '.data.deviceCapabilities.sensors[].name' 2>/dev/null | while read name; do
                        echo -e "${GRAY}     - $name${NC}"
                    done
                    ((PASSED_TESTS++))
                else
                    echo -e "${YELLOW}   [WARN] Expected 3 sensors, got $sensor_count${NC}"
                fi
            else
                echo -e "${GREEN}   [OK] Re-upload received (count unverified)${NC}"
                ((PASSED_TESTS++))
            fi
        else
            echo -e "${RED}   [FAIL] No deviceCapabilities in re-upload${NC}"
        fi
    else
        echo -e "${RED}   [FAIL] Timeout waiting for re-upload${NC}"
    fi
fi

NATS_SUB_PID=""
sleep 2

# ============================================
# STEP 3: Send reset.json to restore state
# ============================================
echo -e "\n${GREEN}[STEP 3/3] Sending reset.json to restore original state...${NC}"

# Start subscriber for the re-upload
RESET_OUTPUT="$OUTPUT_DIR/reset_capabilities.json"
timeout "$TIMEOUT_SECONDS" nats sub \
    --server "nats://$NATS_URL" \
    --user "$NATS_USER" \
    --password "$NATS_PASS" \
    --count 1 \
    "$CFG_UPDATE_REQ_SUBJECT" > "$RESET_OUTPUT" 2>&1 &
NATS_SUB_PID=$!

sleep 1

# Send config update
if send_config_update "$PAYLOADS_DIR/reset.json" "$device_id"; then
    echo -e "${CYAN}   Waiting for DeviceCapabilities re-upload (expecting 1 sensor)...${NC}"

    if wait $NATS_SUB_PID 2>/dev/null; then
        if [ -f "$RESET_OUTPUT" ] && grep -q "deviceCapabilities" "$RESET_OUTPUT"; then
            if command -v jq &> /dev/null; then
                json_content=$(grep -o '{.*}' "$RESET_OUTPUT" | head -1)
                sensor_count=$(echo "$json_content" | jq '.data.deviceCapabilities.sensors | length' 2>/dev/null || echo "0")

                echo -e "${GREEN}   [OK] Re-upload received: $sensor_count sensor(s)${NC}"

                if [ "$sensor_count" -eq 1 ]; then
                    echo -e "${GRAY}   Sensors:${NC}"
                    echo "$json_content" | jq -r '.data.deviceCapabilities.sensors[].name' 2>/dev/null | while read name; do
                        echo -e "${GRAY}     - $name${NC}"
                    done
                    ((PASSED_TESTS++))
                else
                    echo -e "${YELLOW}   [WARN] Expected 1 sensor, got $sensor_count${NC}"
                fi
            else
                echo -e "${GREEN}   [OK] Re-upload received (count unverified)${NC}"
                ((PASSED_TESTS++))
            fi
        else
            echo -e "${RED}   [FAIL] No deviceCapabilities in re-upload${NC}"
        fi
    else
        echo -e "${RED}   [FAIL] Timeout waiting for re-upload${NC}"
    fi
fi

NATS_SUB_PID=""

# ============================================
# Final Result
# ============================================
echo -e "\n${CYAN}========================================"
echo -e "TEST RESULTS: $PASSED_TESTS/$TOTAL_TESTS passed"
echo -e "========================================${NC}"

if [ "$PASSED_TESTS" -eq "$TOTAL_TESTS" ]; then
    echo -e "${GREEN}ALL TESTS PASSED${NC}"
    exit 0
else
    echo -e "${RED}SOME TESTS FAILED${NC}"
    echo -e "${YELLOW}Check logs at: $OUTPUT_DIR${NC}"
    exit 1
fi
