# .NET 10 Upgrade Impact Analysis

## Executive Summary

**Current Version:** .NET 10.0.11
**Target Version:** .NET 10
**Analysis Date:** 2026-01-13
**Project:** system-agent (IIoT monitoring application)

### Quick Assessment

| Category | Impact Level | Notes |
|----------|--------------|-------|
| **Breaking Changes** | 🟡 Medium | Some API changes expected |
| **Dependencies** | 🟢 Low | Most packages support .NET 10 |
| **Performance** | 🟢 Positive | Performance improvements expected |
| **Support Lifecycle** | 🟡 Medium | .NET 9 support ends Nov 2025 |
| **Docker Images** | 🟢 Low | Microsoft provides .NET 10 images |
| **Testing Effort** | 🟡 Medium | Comprehensive testing required |
| **Overall Risk** | 🟡 **MEDIUM** | Manageable with proper testing |

**Recommendation:** ✅ **Plan migration, but wait for .NET 10.0.1 (first patch release)**

---

## .NET 10 Overview

### Release Timeline

- **Preview:** Available now (as of Jan 2025)
- **RC (Release Candidate):** Expected ~Oct 2025
- **GA (General Availability):** Expected Nov 2025
- **First Patch (.NET 10.0.1):** Expected Dec 2025

### Support Lifecycle

| Version | Type | Release | End of Support |
|---------|------|---------|----------------|
| .NET 9 | STS (Standard Term Support) | Nov 2024 | **May 2026** |
| .NET 10 | STS | Nov 2025 | May 2027 |
| .NET 8 | LTS (Long Term Support) | Nov 2023 | Nov 2026 |

⚠️ **Important:** .NET 9 support ends in ~5 months (May 2026). Upgrade is recommended before then.

---

## Current Project Analysis

### Target Frameworks

**Files to Update:**
- `common.props` (line 4): `<TargetFramework>net9.0</TargetFramework>` → `net10.0`
- `examples/system-agent/SystemAgentDevice.csproj` (line 5): `net9.0` → `net10.0`
- `Dockerfile` (line 5): `mcr.microsoft.com/dotnet/sdk:10.0.11` → `sdk:10.0`
- `Dockerfile` (line 47): `mcr.microsoft.com/dotnet/runtime:10.0.11` → `runtime:10.0`

### Dependencies Analysis

#### Microsoft.Extensions.* (Currently 9.0.8)

**Status:** ✅ **Compatible**
- Microsoft releases matching versions for each .NET version
- Update to 10.0.x when .NET 10 releases
- No breaking changes expected in your usage patterns

**Impact:** Low - These are framework packages that evolve with .NET

#### Third-Party Packages

| Package | Current Version | .NET 10 Status | Impact |
|---------|----------------|----------------|--------|
| **Serilog** | 4.1.0 | ✅ Compatible | None - TFM agnostic |
| **Serilog.Extensions.Hosting** | 8.0.0 | ✅ Compatible | Update to 9.0.0 when available |
| **MQTTnet** | 4.3.7 | ✅ Compatible | None - supports .NET Standard 2.0 |
| **NATS.Net** | 2.6.10 | ⚠️ Check | Verify .NET 10 support |
| **Polly.Core** | 8.5.0 | ✅ Compatible | None - modern package |
| **ErrorOr** | 2.0.1 | ✅ Compatible | None - simple library |
| **Advantech.Edge** | 1.1.1 | ⚠️ **CRITICAL** | **Verify vendor support** |
| **ManagedCuda-Nvml** | 9.1.300 | ⚠️ Check | Native interop - test thoroughly |

**Critical Dependencies:**
1. **Advantech.Edge** - Contact vendor to confirm .NET 10 support
2. **ManagedCuda-Nvml** - Native CUDA bindings, test hardware access

#### Your Internal Libraries

```
Weda.SubNode.* packages (1.0.0)
```

**Impact:** ✅ None - You control these packages, update TargetFramework in common.props

---

## Expected Changes in .NET 10

### Known Improvements

1. **Performance**
   - JIT compiler improvements
   - GC (Garbage Collection) optimizations
   - Faster JSON serialization
   - Better ARM64 performance

2. **New Features**
   - C# 13 language features
   - Enhanced System.Text.Json
   - Improved async/await performance
   - Better Linux/container support

3. **API Additions**
   - New APIs in Microsoft.Extensions.*
   - Enhanced diagnostics and metrics
   - Improved HTTP client features

### Potential Breaking Changes

#### High Probability

1. **Obsolete API Removals**
   - APIs marked obsolete in .NET 8/9 may be removed
   - Check for compiler warnings in your current build

2. **Serialization Changes**
   - `System.Text.Json` behavior changes
   - Impact: Your JSON configuration files (appsettings.json, systemcfg.json, etc.)
   - **Action:** Test all configuration loading

3. **Async/Await Behavior**
   - Subtle changes in async state machines
   - Impact: Potential timing issues in edge cases
   - **Action:** Test all async operations thoroughly

#### Medium Probability

1. **Dependency Injection Changes**
   - Microsoft.Extensions.DependencyInjection updates
   - Impact: Service registration and lifetime management
   - **Action:** Test DI container initialization

2. **Logging Changes**
   - Microsoft.Extensions.Logging enhancements
   - Impact: Log output formatting, performance
   - **Action:** Verify log outputs match expectations

3. **Configuration System**
   - Microsoft.Extensions.Configuration updates
   - Impact: Configuration binding behavior
   - **Action:** Test all configuration scenarios

---

## Impact Assessment by Component

### 1. Application Code

**Files to Review:**
- All `.cs` files with obsolete API usage
- Async/await patterns
- JSON serialization/deserialization

**Risk Level:** 🟡 Medium

**Actions Required:**
1. Run `dotnet build` with `/warnaserror` to catch warnings
2. Search for `[Obsolete]` API usage
3. Test all async operations
4. Verify JSON configuration loading

### 2. Docker Images

**Current:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0.11 AS build
FROM mcr.microsoft.com/dotnet/runtime:10.0.11 AS final
```

**Updated:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
```

**Risk Level:** 🟢 Low

**Notes:**
- Microsoft maintains multi-arch images (amd64, arm64)
- Same base OS (Debian Bookworm likely)
- Image size may change slightly

### 3. Build Pipeline

**azure-pipelines.yml Changes:**
- No changes required (uses Dockerfile)
- SBOM generation will reflect .NET 10 packages
- Vulnerability scanning will use .NET 10 database

**Risk Level:** 🟢 Low

### 4. Hardware Integrations

**Critical Components:**
- **Advantech.Edge (SUSI Driver)**
  - Risk: 🔴 **HIGH** - Native driver compatibility
  - Action: **Contact Advantech for .NET 10 support confirmation**

- **ManagedCuda-Nvml (NVIDIA)**
  - Risk: 🟡 Medium - P/Invoke to native libraries
  - Action: Test on target hardware platforms

**Risk Level:** 🔴 **HIGH** - Must verify vendor support

### 5. Runtime Performance

**Expected Changes:**
- ✅ Faster JIT compilation
- ✅ Improved GC pauses (better for real-time monitoring)
- ✅ Lower memory usage
- ✅ Better ARM64 performance (for embedded devices)

**Risk Level:** 🟢 **POSITIVE** - Performance improvements expected

---

## Migration Strategy

### Phase 1: Preparation (Before .NET 10 GA)

**Timeline:** Now - Oct 2025

1. **Audit Current Code**
   ```bash
   # Check for obsolete API usage
   dotnet build /p:TreatWarningsAsErrors=true

   # Check for deprecated APIs
   grep -r "\[Obsolete\]" --include="*.cs"
   ```

2. **Contact Vendors**
   - ✅ Contact Advantech for .NET 10 support timeline
   - ✅ Check ManagedCuda-Nvml compatibility
   - ✅ Review NATS.Net roadmap

3. **Update Documentation**
   - Document current .NET 9 baseline
   - Create rollback plan
   - Update CI/CD documentation

### Phase 2: Preview Testing (Oct 2025)

**Timeline:** Oct - Nov 2025 (.NET 10 RC period)

1. **Test Environment Setup**
   ```bash
   # Install .NET 10 RC
   # Create test branch
   git checkout -b feature/dotnet10-migration
   ```

2. **Update Project Files**
   - common.props: net9.0 → net10.0
   - Directory.Packages.props: Update Microsoft.Extensions.* to 10.0.x
   - Dockerfile: SDK and runtime to 10.0

3. **Build and Test**
   ```bash
   dotnet clean
   dotnet restore
   dotnet build
   dotnet test
   ```

4. **Integration Testing**
   - Test on actual hardware (Advantech devices)
   - Test CUDA/NVIDIA functionality
   - Test all industrial protocols (MQTT, OPC UA, Modbus)
   - Run for 72 hours minimum

### Phase 3: Production Migration (Dec 2025)

**Timeline:** Dec 2025 (after .NET 10.0.1 patch)

⚠️ **Wait for .NET 10.0.1** - First GA release often has issues

1. **Final Testing**
   - Rebuild with .NET 10.0.1
   - Full regression testing
   - Performance benchmarking
   - Security scanning

2. **Staged Rollout**
   - Deploy to dev environment
   - Deploy to staging for 1 week
   - Deploy to production in phases

3. **Monitoring**
   - Enhanced logging for first week
   - Monitor for exceptions
   - Monitor performance metrics
   - Have rollback plan ready

---

## Testing Checklist

### Unit Tests
- [ ] All existing unit tests pass
- [ ] No new warnings or errors
- [ ] Code coverage maintained

### Integration Tests
- [ ] MQTT connectivity
- [ ] Configuration loading (all JSON files)
- [ ] Dependency injection
- [ ] Logging output
- [ ] Async operations
- [ ] Error handling

### Hardware Tests
- [ ] Advantech SUSI Driver functionality
- [ ] NVIDIA GPU monitoring (if applicable)
- [ ] Serial port communication (System.IO.Ports)
- [ ] Hardware sensor readings
- [ ] Real-time data collection

### Performance Tests
- [ ] Startup time
- [ ] Memory usage baseline
- [ ] CPU usage under load
- [ ] GC pause times
- [ ] Throughput (telemetry messages/sec)

### Docker Tests
- [ ] Multi-arch build (amd64, arm64)
- [ ] Image size comparison
- [ ] Container startup time
- [ ] Runtime dependencies
- [ ] Environment variable handling

### Security Tests
- [ ] Vulnerability scan (Trivy)
- [ ] SBOM generation
- [ ] No new CVEs introduced
- [ ] Authentication/authorization still works

---

## Risk Mitigation

### High Risk: Advantech.Edge Compatibility

**Issue:** Native driver may not support .NET 10

**Mitigation:**
1. Contact Advantech sales/support NOW
2. Request .NET 10 compatibility timeline
3. If not supported, consider:
   - Option A: Stay on .NET 8 LTS (supported until Nov 2026)
   - Option B: Use process isolation (separate .NET 9 process for driver)
   - Option C: Wait for vendor support

### Medium Risk: Breaking API Changes

**Issue:** Code may not compile or behave differently

**Mitigation:**
1. Test in RC phase (Oct-Nov 2025)
2. Address compiler warnings NOW
3. Have 2-week buffer for fixes
4. Maintain .NET 9 branch for rollback

### Low Risk: Third-Party Package Compatibility

**Issue:** NuGet packages may not support .NET 10 immediately

**Mitigation:**
1. Most packages are .NET Standard 2.0 compatible
2. Check package maintainers' GitHub for .NET 10 issues
3. Test all packages in RC phase

---

## Cost-Benefit Analysis

### Costs

| Item | Effort | Risk |
|------|--------|------|
| Code updates | 1-2 days | Low |
| Testing | 1-2 weeks | Medium |
| Vendor verification | Unknown | High |
| Documentation | 1-2 days | Low |
| Training | 0.5 days | Low |
| **Total** | **3-4 weeks** | **Medium-High** |

### Benefits

| Benefit | Impact | Timeline |
|---------|--------|----------|
| Performance improvements | 10-20% faster | Immediate |
| Security patches | Ongoing | Throughout support lifecycle |
| Latest features | Incremental | Ongoing |
| .NET 9 EOL avoidance | Critical | Required by May 2026 |
| Better ARM64 support | Significant | For embedded devices |

### ROI Assessment

**Return:** ✅ **Positive** - Required for continued support, plus performance gains

---

## Recommendations

### Immediate Actions (Now - Feb 2025)

1. ✅ **Contact Advantech** for .NET 10 support confirmation
2. ✅ Fix all compiler warnings in current codebase
3. ✅ Document current performance baseline
4. ✅ Review this analysis with team

### Before .NET 10 GA (Feb - Nov 2025)

1. ⏸️ **Monitor .NET 10 preview releases** for breaking changes
2. ⏸️ Update non-critical projects first as proof-of-concept
3. ⏸️ Prepare test environments

### After .NET 10.0.1 (Dec 2025+)

1. 🎯 **Migrate to .NET 10** following Phase 3 plan
2. 🎯 Complete migration by **April 2026** (1 month before .NET 9 EOL)

### Alternative: Consider .NET 8 LTS

If Advantech.Edge doesn't support .NET 10:

**Option:** Migrate to .NET 8 LTS instead
- **Support until:** Nov 2026
- **Benefit:** Longer support window, more stable
- **Tradeoff:** Fewer features, older performance

---

## Decision Matrix

### Should We Upgrade to .NET 10?

```
IF Advantech.Edge supports .NET 10
  AND .NET 10.0.1 is released
  AND RC testing is successful
THEN → ✅ YES, migrate to .NET 10

ELSE IF Advantech.Edge does NOT support .NET 10
  THEN → Consider .NET 8 LTS migration instead

ELSE IF Critical blocker found in testing
  THEN → Stay on .NET 9 until May 2026, then reassess
```

---

## Appendix A: Files to Update

### Project Files
- [ ] `/common.props` (line 4) - TargetFramework
- [ ] `/examples/system-agent/SystemAgentDevice.csproj` (line 5)
- [ ] `/Directory.Packages.props` - Microsoft.Extensions.* versions

### Docker Files
- [ ] `/examples/system-agent/Dockerfile` (lines 5, 47)

### Documentation
- [ ] README.md - Prerequisites section
- [ ] CI/CD documentation

### No Changes Required
- ✅ `azure-pipelines.yml` - Uses Dockerfile
- ✅ Configuration files (*.json)
- ✅ Application code (if no obsolete APIs)

---

## Appendix B: Vendor Contact Information

### Advantech Support
- **Package:** Advantech.Edge (1.1.1)
- **Website:** https://www.advantech.com/
- **Support:** Contact your Advantech representative
- **Question:** ".NET 10 support timeline for SUSI Driver / Advantech.Edge package"

### ManagedCuda-Nvml
- **Package:** ManagedCuda-Nvml.NETStandard (9.1.300)
- **GitHub:** https://github.com/kunzmi/managedCuda
- **Action:** Check GitHub issues for .NET 10 compatibility

---

## Appendix C: Rollback Plan

If issues occur after .NET 10 migration:

### Immediate Rollback (< 24 hours)
```bash
# Revert Docker images
docker pull harbor.arfa.wise-paas.com/edge-coa/system-agent:net9-backup

# Redeploy previous version
kubectl rollout undo deployment/system-agent
```

### Code Rollback
```bash
git revert <migration-commit>
git push origin main
```

### Prevention
- Tag .NET 9 images before migration: `system-agent:net9-backup`
- Keep .NET 9 branch: `maintenance/net9`
- Test rollback procedure before migration

---

## Document Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-01-13 | Analysis Team | Initial assessment |

---

## Sign-Off

**Analysis Complete:** ✅
**Recommendation:** Upgrade to .NET 10 after vendor verification and .NET 10.0.1 release
**Target Date:** Q4 2025 - Q1 2026
**Critical Dependency:** Advantech.Edge .NET 10 support confirmation

**Approval Required:**
- [ ] Technical Lead
- [ ] DevOps Lead
- [ ] Product Owner
- [ ] Security Team
