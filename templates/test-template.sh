#!/bin/bash

set -e

echo "=== Rebuilding template package ==="
cd /Users/rainhu/advantech/projects/weda_subnode/templates
dotnet pack

echo ""
echo "=== Uninstalling old template ==="
dotnet new uninstall Weda.SubNode.Templates 2>/dev/null || true

echo ""
echo "=== Installing new template ==="
dotnet new install ./bin/Release/Weda.SubNode.Templates.1.0.0.nupkg

echo ""
echo "=== Creating test project ==="
cd /Users/rainhu/advantech/projects/weda_subnode/examples
rm -rf TemplateTest 2>/dev/null || true
dotnet new subnode -n TemplateTest

echo ""
echo "=== Checking if project was created ==="
ls -la TemplateTest/

echo ""
echo "=== Building test project ==="
cd TemplateTest
dotnet build

echo ""
echo "=== Success! Cleaning up ==="
cd ..
rm -rf TemplateTest

echo ""
echo "✅ Template test completed successfully!"
