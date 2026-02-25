# syntax=docker/dockerfile:1.6

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy source (note: build context should be repository root)
COPY . .

# Restore dependencies for the image-sensor project
WORKDIR /src/examples/image-sensor
RUN dotnet restore

# Publish
RUN dotnet publish ImageSensor.csproj \
    -c Release \
    -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble AS final

WORKDIR /app

# Copy published files
COPY --from=build /app/publish/ ./

# Optional: reduce globalization dependencies
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

ENTRYPOINT ["dotnet", "ImageSensor.dll"]
