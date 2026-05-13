# Bug Fix: SIGSEGV (Exit Code 139) on Concurrent SUSI IoT P/Invoke Calls

**Date**: 2026-05-13  
**Severity**: Critical (process crash)  
**Affected Component**: `Advantech.Edge.Internal.SusiIot.SusiIotLibFunctions`  
**Reproduction Environment**: ARK-2252, SUSI 4.0 driver, system-agent container

---

## Problem

When `Advantech.Edge` is used by a multi-threaded consumer (e.g., `system-agent`), the process crashes with **exit code 139 (SIGSEGV)** during `Gpio.GetLevel()` calls. The crash occurs inside the native `libSusiIoT.so` library, which is **not thread-safe**.

The same `Advantech.Edge` library (version 1.1.3, identical binary) works correctly when called sequentially from a single thread (e.g., the `TestProject` console app on the same machine), confirming the issue is concurrency-related, not a driver or version mismatch.

---

## Evidence

### 1. Coredump Analysis

A 215MB heap dump was captured from the crashing `system-agent` container at `/opt/Advantech/system-agent/dumps/coredump.1`.

**Crashing thread (OS Thread 0x20) managed stack trace:**

```
IL_STUB_PInvoke(SusiIotIdStruct)                               ← SIGSEGV here
  SusiIotLibFunctions.GetDataString(SusiIotIds, String&)        ← calls _susiIoTGetPFDataString() with no lock
    SusiIotLibFunctions.GetValueInt(SusiIotIds)
      SusiIotLib.GpioGetLevel(Int32)
        SusiIotLib.GpioGetLevel(String)
          FeatureProvider.GpioGetLevel(String)
            Gpio.GetLevel(String)
              HardwarePlatformCollector.CollectGpioMetrics()     ← system-agent collector
                LocalSystemResourceCollector.<CollectMetricsAsync>b__10_19()  ← ThreadPool task
```

**Concurrently active SUSI-related thread (OS Thread 0x1F):**  
`StdoutRedirectionThread.read()` — another P/Invoke call active at the same time.

**Additional ThreadPool threads** were also running other metric collection tasks (hwinfo, temperature, voltage, etc.) that also route through `SusiIotLibFunctions.GetDataString()` → `_susiIoTGetPFDataString()`.

### 2. No Managed Exception

`dotnet-dump analyze` confirmed `pe -nested` returns "There is no current managed exception on this thread" — this is a **native segfault**, not a managed exception.

### 3. Container Environment Comparison

| Item | system-agent (CRASH) | TestProject (OK) |
|------|---------------------|-------------------|
| Advantech.Edge version | 1.1.3 | 1.1.3 |
| DLL MD5 hash | `dc58a572f7b350bdce2e50c215a2de66` | `dc58a572f7b350bdce2e50c215a2de66` |
| Calling pattern | Multi-threaded (ThreadPool) | Single-threaded (sequential for loop) |
| SUSI driver | Same host `/lib/libSusiIoT.so` bind-mounted | Same host native |

### 4. Code Analysis

In `SusiIotLibFunctions.cs`, the `GetDataString()` method calls `_susiIoTGetPFDataString(idStruct)` directly without any synchronization:

```csharp
// Current code — NO LOCK around native call
private bool GetDataString(SusiIotIds id, out string? value)
{
    value = null;
    var rawId = (int)id;
    var idStruct = new SusiIotIdStruct(0, 0, 0, 0, rawId);
    
    nint ptr = nint.Zero;
    try { ptr = this._susiIoTGetPFDataString(idStruct); }  // ← unprotected P/Invoke
    catch (DllNotFoundException) { return false; }
    // ...
}
```

Similarly, `SetDataString()` calls `_susiIoTSetPFDataString()` without a lock.

The only existing lock (`_libExistLock`) protects library initialization, not runtime API calls.

---

## Root Cause

The native `libSusiIoT.so` library is **not thread-safe**. When multiple threads concurrently invoke `SusiIoTGetPFDataString()` or `SusiIoTSetPFDataString()`, the native library corrupts its internal state, causing a segmentation fault.

`Advantech.Edge` does not serialize access to these native calls, so any multi-threaded consumer will eventually trigger this crash.

---

## Fix

Add a global lock around all native SUSI IoT API calls in `SusiIotLibFunctions`:

```csharp
// Add to SusiIotLibFunctions class
private readonly object _nativeApiLock = new();

private bool GetDataString(SusiIotIds id, out string? value)
{
    value = null;
    var rawId = (int)id;
    var idStruct = new SusiIotIdStruct(0, 0, 0, 0, rawId);

    lock (_nativeApiLock)
    {
        nint ptr = nint.Zero;
        try { ptr = this._susiIoTGetPFDataString(idStruct); }
        catch (DllNotFoundException) { return false; }
        if (ptr == nint.Zero) { return false; }

        string? pfDataString = Marshal.PtrToStringAnsi(ptr);
        if (string.IsNullOrEmpty(pfDataString)) { return false; }

        try { this._susiIoTMemFree(ptr); }
        catch (DllNotFoundException) { return false; }
    }

    // JSON parsing can remain outside the lock (managed-only, thread-safe)
    // ...
}

private bool SetDataString(SusiIotIds id, SusiIotDataType valueType, string value)
{
    // ... build JSON string (outside lock) ...

    lock (_nativeApiLock)
    {
        var status = this._susiIoTSetPFDataString(jsonToBeSet.ToString());
        if (status != SusiIotStatus.SUCCESS)
            return false;
    }
    return true;
}
```

### Scope of Lock

The lock must cover the entire native call cycle: `GetPFDataString()` → `PtrToStringAnsi()` → `MemFree()`, because the returned pointer is owned by native memory and must be copied and freed before another call overwrites it.

Methods that need protection:
- `GetDataString()` — calls `_susiIoTGetPFDataString`
- `SetDataString()` — calls `_susiIoTSetPFDataString`
- `ReadDeviceCapStr()` — calls `_susiIoTGetPFCapabilityString`

---

## Expected Result After Fix

1. **No more SIGSEGV** — all native SUSI IoT calls are serialized, preventing concurrent access to the non-thread-safe native library.
2. **system-agent can safely use ThreadPool** — consumers no longer need to worry about serializing their calls to `Advantech.Edge`.
3. **Minimal performance impact** — the lock only covers the native P/Invoke + memory copy + free cycle (microseconds). JSON parsing and managed logic remain outside the lock.
4. **No behavior change for single-threaded consumers** — the lock is uncontended in single-threaded scenarios and has negligible overhead.

---

## Why Fix in Advantech.Edge, Not in system-agent?

While system-agent *could* serialize its calls as a workaround, the correct fix belongs in `Advantech.Edge` because:

1. **Library responsibility** — a library must be safe for any consumer, regardless of threading model.
2. **Internal threads** — `Advantech.Edge` itself creates `StdoutRedirectionThread`, so even a single-threaded consumer has at least 2 threads touching native code.
3. **Fragility** — requiring every consumer to know about and implement serialization is error-prone and undocumented.
