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
    echo "❌ Error: Templates directory not found at $TEMPLATES_DIR"
    exit 1
fi

echo -e "${BLUE}📦 Installing templates from: $TEMPLATES_DIR${NC}"
echo ""

# Uninstall existing templates first (to avoid conflicts)
echo -e "${YELLOW}🗑️  Uninstalling existing templates...${NC}"
dotnet new uninstall "$TEMPLATES_DIR" 2>/dev/null || true
echo ""

# Install templates
echo -e "${BLUE}📥 Installing new templates...${NC}"
dotnet new install "$TEMPLATES_DIR"

echo ""
echo -e "${GREEN}✅ Template installation completed!${NC}"
echo ""
echo "════════════════════════════════════════════════════════════════"
echo " Available Templates:"
echo "════════════════════════════════════════════════════════════════"
echo ""
echo "  subnode   - Custom SubNode (inherits from ModbusDevice)"
echo "  wedaapi     - Simple Weda SubNode API"
echo "  wedaapi-c   - Advanced Weda SubNode API (CreateBuilder pattern)"
echo ""
echo "────────────────────────────────────────────────────────────────"
echo " Usage Examples:"
echo "────────────────────────────────────────────────────────────────"
echo ""
echo "  # Create custom device"
echo "  dotnet new subnode -n MyDevice"
echo ""
echo "  # Create simple API application"
echo "  dotnet new wedaapi -n MyApp"
echo ""
echo "  # Create advanced API application"
echo "  dotnet new wedaapi-c -n MyAdvancedApp"
echo ""
echo "════════════════════════════════════════════════════════════════"
echo ""
echo -e "${YELLOW}💡 Tip: Use 'bash scripts/setup-dev.sh <project-path>' to enable${NC}"
echo -e "${YELLOW}   SDK source code debugging for your project${NC}"
echo ""
