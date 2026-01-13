# .NET 10 Upgrade - Quick Reference

## TL;DR

**Current:** .NET 10.0.11
**Target:** .NET 10
**Risk:** 🟡 **MEDIUM**
**Recommendation:** ✅ Upgrade after .NET 10.0.1 (Dec 2025)
**Critical:** Verify Advantech.Edge supports .NET 10

---

## Timeline

```
Now ──────────> Oct 2025 ──────> Nov 2025 ──────> Dec 2025 ──────> May 2026
  |               |                 |                |               |
  |               |                 |                |               |
Preparation    .NET 10 RC       .NET 10 GA      .NET 10.0.1     .NET 9 EOL
  |               |                 |                |               ⚠️
  |               |                 |                |
  └─ Contact      └─ RC Testing     └─ Wait          └─ **MIGRATE HERE**
     Advantech
```

---

## What Changes

### Files to Update

```diff
# common.props (line 4)
- <TargetFramework>net9.0</TargetFramework>
+ <TargetFramework>net10.0</TargetFramework>

# Dockerfile (line 5)
- FROM mcr.microsoft.com/dotnet/sdk:10.0.11 AS build
+ FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Dockerfile (line 47)
- FROM mcr.microsoft.com/dotnet/runtime:10.0.11 AS final
+ FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final

# Directory.Packages.props
- Microsoft.Extensions.* Version="9.0.8"
+ Microsoft.Extensions.* Version="10.0.0"
```

---

## Risk Assessment

| Dependency | Risk | Action Required |
|------------|------|-----------------|
| **Advantech.Edge** | 🔴 HIGH | **Contact vendor NOW** |
| ManagedCuda-Nvml | 🟡 Medium | Test on hardware |
| Microsoft.Extensions.* | 🟢 Low | Update versions |
| Serilog | 🟢 Low | None (.NET Standard) |
| MQTTnet | 🟢 Low | None (.NET Standard) |
| Your code | 🟡 Medium | Test thoroughly |

---

## Migration Steps

### Step 1: Verify Vendor Support (NOW)
```bash
# Contact Advantech
# Ask: "Does Advantech.Edge 1.1.1+ support .NET 10?"
```

### Step 2: Wait for .NET 10.0.1 (Dec 2025)
```bash
# Don't use .NET 10.0.0 (Nov 2025)
# Wait for first patch release
```

### Step 3: Update Project Files
```bash
# Update common.props
sed -i 's/net9.0/net10.0/g' common.props

# Update Directory.Packages.props
# Change all Microsoft.Extensions.* from 9.0.8 → 10.0.0
```

### Step 4: Update Dockerfile
```bash
# Update both FROM statements
# sdk:10.0.11 → sdk:10.0
# runtime:10.0.11 → runtime:10.0
```

### Step 5: Build & Test
```bash
dotnet clean
dotnet restore
dotnet build
dotnet test

# Build Docker image
docker build -t system-agent:net10-test .

# Test on hardware
docker run -it --privileged system-agent:net10-test
```

### Step 6: Deploy
```bash
# Deploy to dev → staging → production
# Monitor for 1 week each environment
```

---

## Testing Checklist

### Must Test

- [ ] Application starts without errors
- [ ] **Advantech SUSI Driver works**
- [ ] **NVIDIA GPU monitoring works** (if used)
- [ ] Configuration files load correctly
- [ ] Logging outputs correctly
- [ ] MQTT connectivity works
- [ ] All sensors report data
- [ ] No performance degradation
- [ ] No memory leaks
- [ ] Multi-arch Docker builds (amd64, arm64)

---

## Expected Benefits

✅ **10-20% performance improvement**
✅ **Better ARM64 support** (for embedded)
✅ **Security patches** until May 2027
✅ **Latest C# 13 features**
✅ **Continued Microsoft support**

---

## If Things Go Wrong

### Rollback Plan

```bash
# Revert Git changes
git revert <migration-commit>

# Redeploy old Docker image
docker pull harbor.arfa.wise-paas.com/edge-coa/system-agent:net9-backup
```

### Before Migration
```bash
# Tag current image
docker tag system-agent:latest system-agent:net9-backup
docker push system-agent:net9-backup
```

---

## Decision Tree

```
Is .NET 10.0.1 released?
├─ NO → ⏸️ Wait
└─ YES
    │
    Does Advantech.Edge support .NET 10?
    ├─ NO → Consider .NET 8 LTS instead
    └─ YES
        │
        Did RC testing pass?
        ├─ NO → Investigate issues
        └─ YES → ✅ **PROCEED WITH MIGRATION**
```

---

## Key Dates

| Date | Event | Action |
|------|-------|--------|
| **Now** | Planning | Contact Advantech |
| **Oct 2025** | .NET 10 RC | Start testing |
| **Nov 2025** | .NET 10 GA | Wait for .0.1 |
| **Dec 2025** | .NET 10.0.1 | **Migration window** |
| **Apr 2026** | Deadline | Must complete before .NET 9 EOL |
| **May 2026** | .NET 9 EOL | Support ends |

---

## Questions?

See full analysis: [DOTNET-10-UPGRADE-IMPACT-ANALYSIS.md](./DOTNET-10-UPGRADE-IMPACT-ANALYSIS.md)

---

## One-Line Summary

> **Wait for .NET 10.0.1, verify Advantech.Edge support, then migrate by April 2026.**
