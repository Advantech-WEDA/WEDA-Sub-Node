#!/bin/bash
# Test Modbus TCP device connectivity
# Usage: test-edge-connection.sh <host> [port]
# Returns: exit 0 = reachable, exit 1 = failed
#
# Examples:
#   ./test-edge-connection.sh 192.168.1.100
#   ./test-edge-connection.sh 192.168.1.100 502

set -euo pipefail

HOST="${1:?Usage: test-edge-connection.sh <host> [port]}"
PORT="${2:-502}"

echo "=== Modbus TCP Connection Test ==="
echo "Host: $HOST"
echo "Port: $PORT"
echo ""

# Step 1: Ping check
echo "[1/3] Testing host reachability..."
if ping -c 1 -W 2 "$HOST" &>/dev/null; then
    echo "  PASS: $HOST responds to ping"
else
    echo "  WARN: $HOST does not respond to ping (may still work if ICMP blocked)"
fi

# Step 2: TCP port check
echo "[2/3] Testing TCP port $PORT..."
if nc -z -w 3 "$HOST" "$PORT" 2>/dev/null; then
    echo "  PASS: $HOST:$PORT is open"
else
    echo "  FAIL: Cannot connect to $HOST:$PORT"
    echo ""
    echo "Possible causes:"
    echo "  - Device is powered off or unreachable"
    echo "  - Modbus TCP not enabled on the device"
    echo "  - Firewall blocking port $PORT"
    echo "  - Wrong IP address or port"
    echo ""
    echo "RECOMMENDATION: Use built-in simulator (127.0.0.1:5020) for development"
    exit 1
fi

# Step 3: Summary
echo "[3/3] Connection summary"
echo "  PASS: Device at $HOST:$PORT is reachable"
echo ""
echo "Suggested devicecfg.json DeviceCommunication:"
echo "{"
echo "  \"Host\": \"$HOST\","
echo "  \"Port\": $PORT"
echo "}"
exit 0
