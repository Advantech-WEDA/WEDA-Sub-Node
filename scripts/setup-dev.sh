#!/bin/bash
# =============================================================================
# Weda SubNode SDK - Developer Mode Setup Script
# =============================================================================
# This script converts a template-generated project to use local SDK source
# code instead of NuGet packages, enabling SDK debugging and development.
#
# Usage: bash scripts/setup-dev.sh <project-path>
# Example: bash scripts/setup-dev.sh examples/MyDevice
# =============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SDK_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# =============================================================================
# Usage and Validation
# =============================================================================

if [ $# -eq 0 ]; then
    echo -e "${RED}❌ Error: Project path required${NC}"
    echo ""
    echo "Usage: bash scripts/setup-dev.sh <project-path>"
    echo ""
    echo "Examples:"
    echo "  bash scripts/setup-dev.sh examples/MyDevice"
    echo "  bash scripts/setup-dev.sh /absolute/path/to/MyDevice"
    exit 1
fi

PROJECT_PATH="$1"

# Convert to absolute path
if [[ "$PROJECT_PATH" != /* ]]; then
    PROJECT_PATH="$SDK_ROOT/$PROJECT_PATH"
fi

# Validate project path
if [ ! -d "$PROJECT_PATH" ]; then
    echo -e "${RED}❌ Error: Project directory not found: $PROJECT_PATH${NC}"
    exit 1
fi

# Find .csproj file
CSPROJ_FILE=$(find "$PROJECT_PATH" -maxdepth 1 -name "*.csproj" | head -n 1)

if [ -z "$CSPROJ_FILE" ]; then
    echo -e "${RED}❌ Error: No .csproj file found in $PROJECT_PATH${NC}"
    exit 1
fi

PROJECT_NAME=$(basename "$CSPROJ_FILE" .csproj)

echo "════════════════════════════════════════════════════════════════"
echo " Weda SubNode SDK - Developer Mode Setup"
echo "════════════════════════════════════════════════════════════════"
echo ""
echo -e "${BLUE}📁 Project: $PROJECT_NAME${NC}"
echo -e "${BLUE}📂 Path: $PROJECT_PATH${NC}"
echo -e "${BLUE}🔧 SDK Root: $SDK_ROOT${NC}"
echo ""

# =============================================================================
# Step 1: Remove NuGet Package References
# =============================================================================

echo -e "${YELLOW}🗑️  Step 1/4: Removing NuGet package references...${NC}"

SDK_PACKAGES=(
    "Weda.SubNode.Host"
    "Weda.SubNode.Core"
    "Weda.SubNode.Devices"
    "Weda.SubNode.Abstractions"
    "Weda.SubNode.Cloud"
)

cd "$PROJECT_PATH"

for package in "${SDK_PACKAGES[@]}"; do
    if dotnet list "$CSPROJ_FILE" package 2>/dev/null | grep -q "$package"; then
        echo "  - Removing $package"
        dotnet remove "$CSPROJ_FILE" package "$package" 2>/dev/null || true
    fi
done

echo ""

# =============================================================================
# Step 2: Add Project References
# =============================================================================

echo -e "${YELLOW}🔗 Step 2/4: Adding SDK project references...${NC}"

# Determine which SDK projects to reference based on existing dependencies
NEED_HOST=false
NEED_CORE=false
NEED_DEVICES=false

# Check what was removed to determine what to add back
if dotnet list "$CSPROJ_FILE" package 2>/dev/null | grep -q "Weda.SubNode" || \
   grep -q "Weda.SubNode.Host" "$CSPROJ_FILE" || \
   [ ! -s "$CSPROJ_FILE" ]; then
    # Default: add Host project (which transitively includes others)
    NEED_HOST=true
fi

# Special case: if it's a custom device, it might only need Core
if grep -q "ModbusDevice" "$PROJECT_PATH"/*.cs 2>/dev/null; then
    NEED_CORE=true
fi

# Add project references
if [ "$NEED_HOST" = true ]; then
    echo "  + Adding Weda.SubNode.Host"
    dotnet add "$CSPROJ_FILE" reference "$SDK_ROOT/src/Weda.SubNode.Host/Weda.SubNode.Host.csproj"
elif [ "$NEED_CORE" = true ]; then
    echo "  + Adding Weda.SubNode.Core"
    dotnet add "$CSPROJ_FILE" reference "$SDK_ROOT/src/Weda.SubNode.Core/Weda.SubNode.Core.csproj"
fi

echo ""

# =============================================================================
# Step 3: Create Solution File
# =============================================================================

echo -e "${YELLOW}📦 Step 3/4: Creating solution file...${NC}"

SLN_FILE="$PROJECT_PATH/$PROJECT_NAME.sln"

# Remove existing solution if present
if [ -f "$SLN_FILE" ]; then
    echo "  - Removing existing solution"
    rm "$SLN_FILE"
fi

# Create new solution
echo "  + Creating $PROJECT_NAME.sln"
dotnet new sln -n "$PROJECT_NAME" -o "$PROJECT_PATH" --force

# Add user project
echo "  + Adding $PROJECT_NAME to solution"
dotnet sln "$SLN_FILE" add "$CSPROJ_FILE"

# Add SDK projects to solution for easy navigation
echo "  + Adding SDK projects to solution"
dotnet sln "$SLN_FILE" add "$SDK_ROOT/src/Weda.SubNode.Abstractions/Weda.SubNode.Abstractions.csproj"
dotnet sln "$SLN_FILE" add "$SDK_ROOT/src/Weda.SubNode.Core/Weda.SubNode.Core.csproj"
dotnet sln "$SLN_FILE" add "$SDK_ROOT/src/Weda.SubNode.Devices/Weda.SubNode.Devices.csproj"
dotnet sln "$SLN_FILE" add "$SDK_ROOT/src/Weda.SubNode.Cloud/Weda.SubNode.Cloud.csproj"
dotnet sln "$SLN_FILE" add "$SDK_ROOT/src/Weda.SubNode.Host/Weda.SubNode.Host.csproj"

echo ""

# =============================================================================
# Step 4: Restore and Verify
# =============================================================================

echo -e "${YELLOW}🔄 Step 4/4: Restoring packages...${NC}"
dotnet restore "$SLN_FILE"

echo ""
echo -e "${GREEN}✅ Developer mode setup completed!${NC}"
echo ""
echo "════════════════════════════════════════════════════════════════"
echo " Next Steps:"
echo "════════════════════════════════════════════════════════════════"
echo ""
echo "  1. Open the solution in your IDE:"
echo -e "     ${BLUE}$SLN_FILE${NC}"
echo ""
echo "  2. You can now debug into SDK source code!"
echo ""
echo "  3. To revert to NuGet packages:"
echo "     - Remove project references"
echo "     - Add back: dotnet add package Weda.SubNode.Host"
echo ""
echo "════════════════════════════════════════════════════════════════"
echo ""
