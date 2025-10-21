#!/bin/bash
# =============================================================================
# Weda SubNode SDK - Template Uninstallation Script
# =============================================================================
# This script uninstalls all project templates for the Weda SubNode SDK
# Usage: bash scripts/uninstall-templates.sh
# =============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TEMPLATES_DIR="$PROJECT_ROOT/templates"

echo "════════════════════════════════════════════════════════════════"
echo " Weda SubNode SDK - Template Uninstallation"
echo "════════════════════════════════════════════════════════════════"
echo ""

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}🗑️  Uninstalling templates from: $TEMPLATES_DIR${NC}"
echo ""

# Uninstall templates
if dotnet new uninstall "$TEMPLATES_DIR" 2>&1 | grep -q "not currently installed"; then
    echo -e "${YELLOW}⚠️  Templates were not installed${NC}"
else
    echo -e "${GREEN}✅ Templates uninstalled successfully!${NC}"
fi

echo ""
echo "════════════════════════════════════════════════════════════════"
echo ""
