#!/bin/bash
# Script to rebuild and reinstall the template

echo "🔨 Building template package..."
cd "$(dirname "$0")"
dotnet pack -c Release

echo ""
echo "🗑️  Uninstalling old template..."
dotnet new uninstall Weda.SubNode.Templates 2>/dev/null || true

echo ""
echo "📦 Installing new template..."
dotnet new install ./bin/Release/Weda.SubNode.Templates.1.0.0.nupkg

echo ""
echo "✅ Template updated successfully!"
echo ""
echo "Usage:"
echo "  cd ../examples"
echo "  dotnet new subnode -n MyNewProject"
