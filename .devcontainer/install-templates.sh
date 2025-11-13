#!/bin/bash
set -e

echo "Installing Weda SubNode Templates..."

# Navigate to workspace
cd /workspace

# Uninstall existing templates if any
dotnet new uninstall Weda.SubNode.Templates 2>/dev/null || true

# Install templates from local source
dotnet new install ./templates

echo "✓ Weda SubNode Templates installed successfully"
echo ""
echo "Available templates:"
dotnet new list | grep -i weda || echo "  - wedaapi: Web API style template"
echo "  - subnode: Console style template"
echo ""
