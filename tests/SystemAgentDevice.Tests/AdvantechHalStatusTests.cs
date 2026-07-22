using System.Collections.Generic;

using SystemAgentExample.Models;

using Xunit;

namespace SystemAgentDevice.Tests;

public class AdvantechHalStatusTests
{
    [Fact]
    public void Ok_SetsLoadedAndCarriesNameBackendVersionsAndFeatures()
    {
        var status = AdvantechHalStatus.Ok(
            name: "Advantech.Edge",
            backend: AdvantechHalStatus.SusiBackend,
            backendLibrary: "libSusiIoT.so",
            packageVersion: "1.1.3.0",
            driverVersion: "1.0.9",
            libraryVersion: "1.1.3",
            onboardSensorsSupported: true,
            gpioSupported: false,
            watchdogSupported: true,
            thermalProtectionSupported: false);

        Assert.True(status.IsLoaded);
        Assert.True(status.Loaded);
        Assert.Null(status.FailureReason);
        Assert.Equal("Advantech.Edge", status.Name);
        Assert.Equal(AdvantechHalStatus.SusiBackend, status.Backend);
        Assert.Equal("libSusiIoT.so", status.BackendLibrary);
        Assert.Equal("1.0.9", status.DriverVersion);
        Assert.Equal("1.1.3", status.LibraryVersion);
        Assert.True(status.OnboardSensorsSupported);
        Assert.False(status.GpioSupported);
        Assert.True(status.WatchdogSupported);
        Assert.False(status.ThermalProtectionSupported);
    }

    [Fact]
    public void Failed_MarksNotLoadedAndCapturesReason_UnknownBackend()
    {
        var ex = new System.InvalidOperationException("native library not found");

        var status = AdvantechHalStatus.Failed(ex);

        Assert.False(status.IsLoaded);
        Assert.Equal(AdvantechHalStatus.DefaultName, status.Name);
        Assert.Equal(AdvantechHalStatus.UnknownBackend, status.Backend);
        Assert.Equal(string.Empty, status.BackendLibrary);
        Assert.Equal(string.Empty, status.DriverVersion);
        Assert.Equal(string.Empty, status.LibraryVersion);
        Assert.False(status.OnboardSensorsSupported);
        Assert.Equal("InvalidOperationException: native library not found", status.FailureReason);
    }

    [Fact]
    public void LoadedWithError_IsLoadedButCarriesBackendAndReason()
    {
        var ex = new System.Exception("read timeout");

        var status = AdvantechHalStatus.LoadedWithError("Advantech.Edge", AdvantechHalStatus.PlatformSdkBackend, "libEAPI.so", "1.1.3.0", ex);

        Assert.True(status.IsLoaded);
        Assert.Equal("Advantech.Edge", status.Name);
        Assert.Equal(AdvantechHalStatus.PlatformSdkBackend, status.Backend);
        Assert.Equal("libEAPI.so", status.BackendLibrary);
        Assert.Equal("1.1.3.0", status.PackageVersion);
        Assert.Equal("Exception: read timeout", status.FailureReason);
    }

    [Fact]
    public void ToMetadata_Ok_ProjectsBackendAndNestedShapeWithoutFailureReason()
    {
        var status = AdvantechHalStatus.Ok(
            name: "Advantech.Edge",
            backend: AdvantechHalStatus.SusiBackend,
            backendLibrary: "libSusiIoT.so",
            packageVersion: "1.1.3.0",
            driverVersion: "1.0.9",
            libraryVersion: "1.1.3",
            onboardSensorsSupported: true,
            gpioSupported: true,
            watchdogSupported: false,
            thermalProtectionSupported: true);

        var metadata = status.ToMetadata();

        // name is synced to the library result; backend is the live native library.
        Assert.Equal("Advantech.Edge", metadata["name"]);
        Assert.Equal("SUSI", metadata["backend"]);
        Assert.Equal("libSusiIoT.so", metadata["backendLibrary"]);
        Assert.Equal(true, metadata["loaded"]);
        Assert.Equal("1.1.3.0", metadata["packageVersion"]);
        Assert.Equal("1.0.9", metadata["driverVersion"]);
        Assert.Equal("1.1.3", metadata["libraryVersion"]);
        Assert.False(metadata.ContainsKey("failureReason"));

        var features = Assert.IsType<Dictionary<string, object>>(metadata["features"]);
        Assert.Equal(true, features["onboardSensors"]);
        Assert.Equal(true, features["gpio"]);
        Assert.Equal(false, features["watchdog"]);
        Assert.Equal(true, features["thermalProtection"]);
    }

    [Fact]
    public void ToMetadata_Failed_IncludesFailureReasonUnknownBackendAndLoadedFalse()
    {
        var status = AdvantechHalStatus.Failed(new System.InvalidOperationException("boom"));

        var metadata = status.ToMetadata();

        Assert.Equal(false, metadata["loaded"]);
        Assert.Equal(AdvantechHalStatus.UnknownBackend, metadata["backend"]);
        Assert.Equal("InvalidOperationException: boom", metadata["failureReason"]);
        Assert.Equal(string.Empty, metadata["driverVersion"]);
    }

    [Fact]
    public void ToMetadata_ReturnsFreshDictionaryEachCall()
    {
        var status = AdvantechHalStatus.Ok("Advantech.Edge", AdvantechHalStatus.SusiBackend, "libSusiIoT.so", "1", "1", "1", true, true, true, true);

        var first = status.ToMetadata();
        var second = status.ToMetadata();

        Assert.NotSame(first, second);
        Assert.NotSame(first["features"], second["features"]);
    }

    [Fact]
    public void MetadataKey_IsTheFixedDeviceInfoSlot()
    {
        Assert.Equal("advantechHal", AdvantechHalStatus.MetadataKey);
    }
}
