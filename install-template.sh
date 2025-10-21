#!/bin/bash
# Weda SubNode Template Installation Script
# Automatically extracts version from common.props and handles installation states

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored messages
print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

# Function to extract version from common.props
get_version() {
    local version=$(grep -oP '(?<=<Version>)[^<]+' common.props 2>/dev/null | head -1)
    if [ -z "$version" ]; then
        print_error "Failed to extract version from common.props"
        exit 1
    fi
    echo "$version"
}

# Function to check if template is installed
is_template_installed() {
    local version=$1
    dotnet new list 2>/dev/null | grep -q "Weda.SubNode.Templates::$version"
    return $?
}

# Function to get installed template version
get_installed_version() {
    dotnet new list 2>/dev/null | grep -oP 'Weda\.SubNode\.Templates::\K[0-9]+\.[0-9]+\.[0-9]+' | head -1
}

# Function to uninstall template
uninstall_template() {
    local version=$1
    print_info "Uninstalling Weda.SubNode.Templates::$version..."
    if dotnet new uninstall Weda.SubNode.Templates 2>/dev/null; then
        print_success "Template uninstalled successfully"
        return 0
    else
        print_warning "Template uninstall skipped (may not be installed)"
        return 1
    fi
}

# Function to build template package
build_template() {
    local version=$1
    print_info "Building template package (version $version)..."

    cd templates

    # Clean previous builds
    rm -rf bin/Release 2>/dev/null || true

    # Build the package
    if dotnet pack -c Release > /dev/null 2>&1; then
        local package_path="./bin/Release/Weda.SubNode.Templates.${version}.nupkg"
        if [ -f "$package_path" ]; then
            print_success "Template package built: $package_path"
            cd ..
            echo "$package_path"
            return 0
        else
            print_error "Package file not found: $package_path"
            cd ..
            return 1
        fi
    else
        print_error "Failed to build template package"
        cd ..
        return 1
    fi
}

# Function to install template
install_template() {
    local package_path=$1
    local version=$2

    print_info "Installing template from $package_path..."

    if dotnet new install "$package_path" > /dev/null 2>&1; then
        print_success "Template installed successfully"
        return 0
    else
        print_error "Failed to install template"
        return 1
    fi
}

# Function to verify installation
verify_installation() {
    print_info "Verifying installation..."
    echo ""
    dotnet new list | grep -A 2 "weda SubNode" || true
    echo ""
}

# Main script
main() {
    echo "╔════════════════════════════════════════════════════════╗"
    echo "║   Weda SubNode Template Installation Script         ║"
    echo "╚════════════════════════════════════════════════════════╝"
    echo ""

    # Check if we're in the right directory
    if [ ! -f "common.props" ]; then
        print_error "common.props not found. Please run this script from the repository root."
        exit 1
    fi

    # Get version from common.props
    VERSION=$(get_version)
    print_info "Detected version: $VERSION"
    echo ""

    # Check if template is already installed
    INSTALLED_VERSION=$(get_installed_version)

    if [ -n "$INSTALLED_VERSION" ]; then
        print_warning "Template is already installed (version $INSTALLED_VERSION)"

        if [ "$INSTALLED_VERSION" = "$VERSION" ]; then
            echo ""
            echo "The installed version matches the current version."
            echo ""
            echo "Options:"
            echo "  [i] Ignore - Keep existing installation (default)"
            echo "  [r] Replace - Reinstall the template"
            echo "  [q] Quit"
            echo ""
            read -p "Choose an option [i/r/q]: " -n 1 -r choice
            echo ""
            echo ""

            case $choice in
                r|R)
                    print_info "Replacing template..."
                    uninstall_template "$VERSION"
                    ;;
                q|Q)
                    print_info "Installation cancelled"
                    exit 0
                    ;;
                i|I|"")
                    print_success "Keeping existing installation"
                    verify_installation
                    exit 0
                    ;;
                *)
                    print_error "Invalid option"
                    exit 1
                    ;;
            esac
        else
            print_warning "Installed version ($INSTALLED_VERSION) differs from current version ($VERSION)"
            echo ""
            echo "Options:"
            echo "  [u] Update - Uninstall old version and install new version (recommended)"
            echo "  [k] Keep - Keep old version and skip installation"
            echo "  [q] Quit"
            echo ""
            read -p "Choose an option [u/k/q]: " -n 1 -r choice
            echo ""
            echo ""

            case $choice in
                u|U|"")
                    print_info "Updating template..."
                    uninstall_template "$INSTALLED_VERSION"
                    ;;
                k|K)
                    print_success "Keeping existing installation"
                    verify_installation
                    exit 0
                    ;;
                q|Q)
                    print_info "Installation cancelled"
                    exit 0
                    ;;
                *)
                    print_error "Invalid option"
                    exit 1
                    ;;
            esac
        fi
    fi

    # Build the template package
    PACKAGE_PATH=$(build_template "$VERSION")
    if [ $? -ne 0 ]; then
        print_error "Build failed"
        exit 1
    fi

    echo ""

    # Install the template
    if install_template "templates/bin/Release/Weda.SubNode.Templates.${VERSION}.nupkg" "$VERSION"; then
        echo ""
        print_success "Installation completed successfully!"
        echo ""
        verify_installation

        echo ""
        print_info "Usage:"
        echo "  cd examples"
        echo "  dotnet new subnode -n MyDeviceName"
        echo ""

        exit 0
    else
        print_error "Installation failed"
        exit 1
    fi
}

# Run main function
main
