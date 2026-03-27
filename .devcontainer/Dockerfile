# Use devcontainers base image with .NET 8.0 (includes vscode user, sudo, git, etc.)
FROM mcr.microsoft.com/devcontainers/dotnet:1-8.0

# Install .NET 10.0 SDK (required by SubNode SDK)
COPY --from=mcr.microsoft.com/dotnet/sdk:10.0 /usr/share/dotnet /usr/share/dotnet

# Install NATS CLI via GitHub release
ARG NATS_CLI_VERSION=0.2.2
RUN ARCH=$(dpkg --print-architecture) \
    && curl -fsSL "https://github.com/nats-io/natscli/releases/download/v${NATS_CLI_VERSION}/nats-${NATS_CLI_VERSION}-linux-${ARCH}.deb" -o /tmp/nats.deb \
    && dpkg -i /tmp/nats.deb \
    && rm /tmp/nats.deb

# Set the working directory
WORKDIR /workspace

# Configure git safe directory
RUN git config --global --add safe.directory /workspace

# Copy template installation script
COPY install-templates.sh /tmp/install-templates.sh
RUN chmod +x /tmp/install-templates.sh
