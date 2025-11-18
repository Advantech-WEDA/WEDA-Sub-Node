#!/bin/bash
# =============================================================================
# Weda SubNode SDK - Template Installation Script
# =============================================================================
# This script installs all project templates for the Weda SubNode SDK
# Usage: bash scripts/install-templates.sh
# =============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TEMPLATES_DIR="$PROJECT_ROOT/templates"

echo "════════════════════════════════════════════════════════════════"
echo " Weda SubNode SDK - Template Installation"
echo "════════════════════════════════════════════════════════════════"
echo ""

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if templates directory exists
if [ ! -d "$TEMPLATES_DIR" ]; then
    echo "Error: Templates directory not found at $TEMPLATES_DIR"
    exit 1
fi

echo -e "${BLUE}Installing templates from: $TEMPLATES_DIR${NC}"
echo ""

# Step 1: Uninstall ALL old Weda SubNode templates (including NuGet packages)
echo -e "${YELLOW}️Cleaning up old templates...${NC}"
echo ""

# Uninstall known old package names
OLD_PACKAGE="Weda.SubNode.Templates"

echo "Removing old package: $OLD_PACKAGE"
dotnet new uninstall "$OLD_PACKAGE" 2>/dev/null || true

# Also uninstall local templates directory (if previously installed)
dotnet new uninstall "$TEMPLATES_DIR" 2>/dev/null || true
echo ""

# Step 2: Install fresh templates from local directory
echo -e "${BLUE}Installing fresh templates...${NC}"
dotnet new install "$TEMPLATES_DIR"

echo ""
echo -e "${GREEN}Template installation completed!${NC}"
echo ""
echo "════════════════════════════════════════════════════════════════"
echo " Available Templates:"
echo "════════════════════════════════════════════════════════════════"
echo ""
echo "  subnode   - Console App style (single device dev/debug)"
echo "  wedabuilder   - Web API style (production multi-device)"
echo ""
echo "────────────────────────────────────────────────────────────────"
echo " Usage Examples:"
echo "────────────────────────────────────────────────────────────────"
echo ""
echo "  # Create console app for single device"
echo "  dotnet new subnode -n MyDevice"
echo ""
echo "  # Create web API for production"
echo "  dotnet new wedabuilder -n MyApp"
echo ""
echo "────────────────────────────────────────────────────────────────"
echo " Verify Installation:"
echo "────────────────────────────────────────────────────────────────"
echo ""
echo "  dotnet new list | grep -i subnode"
echo ""