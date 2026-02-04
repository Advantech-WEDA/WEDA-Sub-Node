using System.IO.Hashing;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Shouldly;

using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;
using Weda.SubNode.Core.Telemetry.Validation;
using Weda.SubNode.Core.Transforms;

using Xunit;

namespace Weda.SubNode.Core.Tests.Telemetry;

public class ImageChunkingTests
{
    private static string GenerateBase64(int sizeInBytes)
    {
        var bytes = new byte[sizeInBytes];
        Random.Shared.NextBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    #region SchemaBasedValidator - MaxBinarySize

    [Fact]
    public void Validator_Base64WithinLimit_ShouldPass()
    {
        // Arrange
        var options = new TelemetryOptions { MaxBinarySize = 1024 * 1024 }; // 1MB
        var validator = new SchemaBasedValidator(Options.Create(options));
        var base64 = GenerateBase64(500 * 1024); // 500KB

        // Act
        var result = validator.Validate(base64, "image/png");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validator_Base64ExceedsLimit_ShouldFail()
    {
        // Arrange
        var options = new TelemetryOptions { MaxBinarySize = 100 * 1024 }; // 100KB
        var validator = new SchemaBasedValidator(Options.Create(options));
        var base64 = GenerateBase64(200 * 1024); // 200KB -> ~270KB encoded

        // Act
        var result = validator.Validate(base64, "image/jpeg");

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("exceeds maximum size");
    }

    #endregion

    #region TelemetryMeasureDto.From - Auto Chunking

    [Fact]
    public void From_SmallBase64_ShouldNotChunk()
    {
        // Arrange
        var base64 = GenerateBase64(100 * 1024); // 100KB
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "dev01-12345", Value = base64 }
        };

        // Act
        var dto = TelemetryMeasureDto.From(measures);

        // Assert
        dto.Measures.Count.ShouldBe(1);
        dto.Measures[0].Value.ShouldBe(base64);
        dto.Measures[0].Metadata.ShouldBeNull();
    }

    [Fact]
    public void From_LargeBase64_ShouldChunk()
    {
        // Arrange
        var options = new TelemetryOptions { ChunkSize = 100 * 1024 }; // 100KB chunks
        var base64 = GenerateBase64(250 * 1024); // ~333KB encoded -> 4 chunks
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "dev01-12345", Value = base64 }
        };

        // Act
        var dto = TelemetryMeasureDto.From(measures, options);

        // Assert
        dto.Measures.Count.ShouldBeGreaterThan(1);

        var firstChunk = dto.Measures[0];
        firstChunk.Metadata.ShouldNotBeNull();
        firstChunk.Metadata!["chunkIndex"].ShouldBe(0);
        firstChunk.Metadata["totalChunks"].ShouldBe(dto.Measures.Count);
        firstChunk.Metadata.ContainsKey("imageId").ShouldBeTrue();
        firstChunk.Metadata.ContainsKey("checksum").ShouldBeTrue();

        // All chunks should have same imageId
        var imageId = firstChunk.Metadata["imageId"];
        foreach (var chunk in dto.Measures)
        {
            chunk.Metadata!["imageId"].ShouldBe(imageId);
        }
    }

    [Fact]
    public void From_JsonString_ShouldNotChunk()
    {
        // Arrange - Large JSON string that looks like it could be chunked
        var largeJson = "{\"data\":\"" + new string('x', 800 * 1024) + "\"}";
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "dev01-12345", Value = largeJson }
        };

        // Act
        var dto = TelemetryMeasureDto.From(measures);

        // Assert - JSON should be converted to JsonElement, not chunked
        dto.Measures.Count.ShouldBe(1);
        dto.Measures[0].Value.ShouldBeOfType<JsonElement>();
    }

    #endregion

    #region ChunkingTransform

    [Fact]
    public async Task ChunkingTransform_SmallData_ShouldPassThrough()
    {
        // Arrange
        var transform = ChunkingTransform.Create(new Dictionary<string, object>
        {
            ["chunkSize"] = 100 * 1024
        });
        var base64 = GenerateBase64(50 * 1024); // 50KB
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "dev01-12345", Value = base64 }
        };

        // Act
        var result = await transform.TransformAsync(measures, new TelemetryTransformContext());

        // Assert
        result.Count.ShouldBe(1);
        result[0].Value.ShouldBe(base64);
    }

    [Fact]
    public async Task ChunkingTransform_LargeData_ShouldChunk()
    {
        // Arrange
        var transform = ChunkingTransform.Create(new Dictionary<string, object>
        {
            ["chunkSize"] = 50 * 1024 // 50KB chunks
        });
        var base64 = GenerateBase64(100 * 1024); // ~133KB encoded -> 3 chunks
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "dev01-12345", Value = base64 }
        };

        // Act
        var result = await transform.TransformAsync(measures, new TelemetryTransformContext());

        // Assert
        result.Count.ShouldBeGreaterThan(1);
        result[0].Metadata.ShouldNotBeNull();
        result[0].Metadata!["chunkIndex"].ShouldBe(0);
    }

    [Fact]
    public async Task ChunkingTransform_Checksum_ShouldMatchReassembledData()
    {
        // Arrange
        var transform = ChunkingTransform.Create(new Dictionary<string, object>
        {
            ["chunkSize"] = 50 * 1024
        });
        var base64 = GenerateBase64(100 * 1024);
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "dev01-12345", Value = base64 }
        };

        // Act
        var result = await transform.TransformAsync(measures, new TelemetryTransformContext());

        // Assert - all chunks should carry the same checksum
        result.Count.ShouldBeGreaterThan(1);
        var expectedChecksum = Crc32.HashToUInt32(Encoding.UTF8.GetBytes(base64));

        foreach (var chunk in result)
        {
            chunk.Metadata!["checksum"].ShouldBe(expectedChecksum);
        }

        // Reassemble and verify
        var reassembled = string.Concat(result.Select(c => (string)c.Value));
        var actualChecksum = Crc32.HashToUInt32(Encoding.UTF8.GetBytes(reassembled));
        actualChecksum.ShouldBe(expectedChecksum);
    }

    [Fact]
    public void ChunkingTransform_ValidateParameters_TooSmall_ShouldFail()
    {
        // Arrange
        var transform = new ChunkingTransform();

        // Act
        var result = transform.ValidateParameters(new Dictionary<string, object>
        {
            ["chunkSize"] = 512 // Less than 1KB
        });

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("at least 1KB");
    }

    [Fact]
    public void ChunkingTransform_ValidateParameters_TooLarge_ShouldFail()
    {
        // Arrange
        var transform = new ChunkingTransform();

        // Act
        var result = transform.ValidateParameters(new Dictionary<string, object>
        {
            ["chunkSize"] = 1024 * 1024 // 1MB, exceeds 750KB limit
        });

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("cannot exceed 750KB");
    }

    #endregion
}
