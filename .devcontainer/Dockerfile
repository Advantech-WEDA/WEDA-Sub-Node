# Use Microsoft's official .NET SDK image
# FROM mcr.microsoft.com/dotnet/sdk:10.0

# Use the most recent .NET LTS as the base image
FROM mcr.microsoft.com/devcontainers/dotnet:1-8.0

# Install the current .NET STS release on top of that
COPY --from=mcr.microsoft.com/dotnet/sdk:9.0 /usr/share/dotnet /usr/share/dotnet

# Finally install the most recent .NET 10.0 preview using the dotnet-install script
COPY --from=mcr.microsoft.com/dotnet/sdk:10.0 /usr/share/dotnet /usr/share/dotnet

# Install additional system tools
RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
    && apt-get -y install --no-install-recommends \
    git \
    wget \
    curl \
    vim \
    nano \
    sudo \
    # Clean up
    && apt-get autoremove -y \
    && apt-get clean -y \
    && rm -rf /var/lib/apt/lists/*

# Create a non-root user
ARG USERNAME=vscode
ARG USER_UID=1000
ARG USER_GID=$USER_UID

RUN groupadd --gid $USER_GID $USERNAME \
    && useradd --uid $USER_UID --gid $USER_GID -m $USERNAME -s /bin/bash \
    && echo $USERNAME ALL=\(root\) NOPASSWD:ALL > /etc/sudoers.d/$USERNAME \
    && chmod 0440 /etc/sudoers.d/$USERNAME || true

# Switch to non-root user
USER $USERNAME

# Install .NET global tools for the user (install separately to avoid failure cascade)
# Skip dotnet-ef for now as it may have package issues
# RUN dotnet tool install --global dotnet-format || true
# RUN dotnet tool install --global dotnet-outdated-tool || true

# Add .NET tools to PATH
ENV PATH="$PATH:/home/${USERNAME}/.dotnet/tools"

# Set the working directory
WORKDIR /workspace

# Configure git
# RUN git config --global --add safe.directory /workspace

# Copy template installation script
USER root
COPY install-templates.sh /tmp/install-templates.sh
RUN chmod +x /tmp/install-templates.sh
USER $USERNAME
