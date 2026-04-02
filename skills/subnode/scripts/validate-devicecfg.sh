#!/bin/bash
# Validate devicecfg.json for common configuration errors
# Usage: validate-devicecfg.sh <path-to-devicecfg.json>
# Returns: exit 0 = valid, exit 1 = errors found
#
# Checks:
#   - Valid JSON syntax
#   - Required fields present (SubNode, DeviceConfigs)
#   - Sensor register address overlap detection
#   - RegisterCount vs DataType consistency
#   - No duplicate sensor names within a device

set -euo pipefail

CFG="${1:?Usage: validate-devicecfg.sh <path-to-devicecfg.json>}"

if [ ! -f "$CFG" ]; then
    echo "FAIL: File not found: $CFG"
    exit 1
fi

ERRORS=0

echo "=== devicecfg.json Validation ==="
echo "File: $CFG"
echo ""

# Step 1: JSON syntax
echo "[1/5] Checking JSON syntax..."
if jq empty "$CFG" 2>/dev/null; then
    echo "  PASS: Valid JSON"
else
    echo "  FAIL: Invalid JSON syntax"
    exit 1
fi

# Step 2: Required fields
echo "[2/5] Checking required fields..."
HAS_SUBNODE=$(jq 'has("SubNode")' "$CFG")
HAS_DEVICES=$(jq 'has("DeviceConfigs")' "$CFG")

if [ "$HAS_SUBNODE" = "true" ]; then
    echo "  PASS: SubNode section present"
    NAME=$(jq -r '.SubNode.Name // empty' "$CFG")
    TYPE=$(jq -r '.SubNode.SubNodeType // empty' "$CFG")
    [ -n "$NAME" ] && echo "    Name: $NAME" || { echo "  WARN: SubNode.Name is missing"; }
    [ -n "$TYPE" ] && echo "    Type: $TYPE" || { echo "  WARN: SubNode.SubNodeType is missing"; }
else
    echo "  FAIL: SubNode section missing"
    ERRORS=$((ERRORS + 1))
fi

if [ "$HAS_DEVICES" = "true" ]; then
    echo "  PASS: DeviceConfigs section present"
else
    echo "  FAIL: DeviceConfigs section missing"
    ERRORS=$((ERRORS + 1))
fi

# Step 3: DataType vs RegisterCount consistency
echo "[3/5] Checking DataType/RegisterCount consistency..."
SENSORS=$(jq -r '
  .DeviceConfigs // {} | to_entries[] |
  .key as $dev |
  .value.Sensors // [] | .[] |
  "\($dev)|\(.Name)|\(.Parameters.DataType // "unknown")|\(.Parameters.RegisterCount // 0)"
' "$CFG" 2>/dev/null || echo "")

while IFS='|' read -r DEV SENSOR DTYPE RCOUNT; do
    [ -z "$DEV" ] && continue
    EXPECTED=""
    case "$DTYPE" in
        UInt16|Int16) EXPECTED=1 ;;
        UInt32|Int32|Float32) EXPECTED=2 ;;
        UInt64|Int64|Float64) EXPECTED=4 ;;
        Boolean|String|String16|unknown) continue ;;
    esac
    if [ -n "$EXPECTED" ] && [ "$RCOUNT" != "0" ] && [ "$RCOUNT" != "$EXPECTED" ]; then
        echo "  FAIL: $DEV/$SENSOR: DataType=$DTYPE expects RegisterCount=$EXPECTED, got $RCOUNT"
        ERRORS=$((ERRORS + 1))
    fi
done <<< "$SENSORS"

[ "$ERRORS" -eq 0 ] && echo "  PASS: All DataType/RegisterCount pairs consistent" || true

# Step 4: Register address overlap detection
echo "[4/5] Checking for register address overlaps..."
OVERLAP_FOUND=0
for DEV in $(jq -r '.DeviceConfigs // {} | keys[]' "$CFG" 2>/dev/null); do
    ADDRS=$(jq -r "
      .DeviceConfigs[\"$DEV\"].Sensors // [] | .[] |
      \"\(.Parameters.RegisterType // \"HoldingRegister\")|\(.Parameters.RegisterAddress // 0)|\(.Parameters.RegisterCount // 1)|\(.Name)\"
    " "$CFG" 2>/dev/null | sort)

    # Group by RegisterType and check overlaps
    echo "$ADDRS" | while IFS='|' read -r RTYPE ADDR COUNT NAME; do
        [ -z "$RTYPE" ] && continue
        END=$((ADDR + COUNT))
        echo "$ADDRS" | while IFS='|' read -r RTYPE2 ADDR2 COUNT2 NAME2; do
            [ -z "$RTYPE2" ] && continue
            [ "$NAME" = "$NAME2" ] && continue
            [ "$RTYPE" != "$RTYPE2" ] && continue
            END2=$((ADDR2 + COUNT2))
            if [ "$ADDR" -lt "$END2" ] && [ "$ADDR2" -lt "$END" ]; then
                echo "  WARN: $DEV: '$NAME' [$ADDR-$((END-1))] overlaps with '$NAME2' [$ADDR2-$((END2-1))] ($RTYPE)"
                OVERLAP_FOUND=1
            fi
        done
    done
done

[ "$OVERLAP_FOUND" -eq 0 ] && echo "  PASS: No register address overlaps" || true

# Step 5: Duplicate sensor names
echo "[5/5] Checking for duplicate sensor names..."
DUPES=$(jq -r '
  .DeviceConfigs // {} | to_entries[] |
  .key as $dev |
  .value.Sensors // [] | [.[].Name] |
  group_by(.) | map(select(length > 1)) | .[][] |
  "\($dev): \(.)"
' "$CFG" 2>/dev/null || echo "")

if [ -z "$DUPES" ]; then
    echo "  PASS: No duplicate sensor names"
else
    echo "$DUPES" | while read -r line; do
        echo "  FAIL: Duplicate sensor name in $line"
        ERRORS=$((ERRORS + 1))
    done
fi

# Summary
echo ""
if [ "$ERRORS" -gt 0 ]; then
    echo "RESULT: $ERRORS error(s) found"
    exit 1
else
    echo "RESULT: All checks passed"
    exit 0
fi
