# Feature Extraction: Implementation Plan
---

## Overview

* **Primary implementation target (WISE-2410 sensor specification)**:
  * Feature output rate (PHM trend target): 1 Hz
  * Target analysis bandwidth: 1 – 1000 Hz
  * Reference sensor native output capability (WISE-2410): 6600 Hz

* **Target output (aligned to primary implementation target)** :  
  * Source hardware:
    * B10BG3 (single-axis accelerometer, X-axis)
    * idaq-801 (analog signal acquisition module)
    * idaq-934 (DAQ module chassis)
  * WISE-2410 equivalent PHM features (28 parameters)
  * Computable feature count (current hardware setup) : ***10 / 28 parameters***
    * Metadata : `Timestamp_Timestamp`, `Device_Time`
    * X-axis PHM features : `X-Axis_RMSmg`, `X-Axis_Peakmg`, `X-Axis_Peak-to-Peak_Displacement`, `X-Axis_OAVelocity`, `X-Axis_Deviation`, `X-Axis_Skewness`, `X-Axis_Kurtosis`, `X-Axis_CrestFactor`
  * Alignment rules:
    * Output-rate target (1 Hz) applies to the final feature publication cadence of `Wise2410Features` for PHM trend analysis.
    * Bandwidth target (1 – 1000 Hz) constrains frequency-domain feature computation and valid reporting band.

* **Machine Configuration**:
  * Rated speed: 3000 RPM (50 Hz fundamental frequency)
  * Application context: Rotating machinery PHM (bearing wear, imbalance, misalignment detection)
  * Analysis focus: Harmonics 1X–5X (50–250 Hz band)

* **DAQ Configuration** (validated baseline):
  * Sampling rate: 2500 Hz (fixed)
  * Frame length (analysis interval): 1 second
  * Samples per frame: 2500 (exact correspondence with FFT size)
  * FFT size: 2500
  * Frequency resolution: 1 Hz (= fs / FFT_size = 2500 / 2500)
  * Window: Hann
  * Overlap: None (consecutive non-overlapping frames)
  * Nyquist frequency: 1250 Hz (covers target band 1–1000 Hz with headroom)

* **Processing location** :  
  * WEDA Edge Node (Sub Node / WEDA Node)

---

## Signal Domain Classification (WISE-2410 Feature Reference)

This plan adopts the signal domain classification from the WISE-2410 sensor specification. All computable features are categorized into one of two domains based on their **computation method**, not their conceptual meaning.

**Domain Definitions** (per WISE-2410 specification):

- **Frequency-domain features**: `X-Axis_RMSmg`, `X-Axis_Peakmg`, `X-Axis_Peak-to-Peak_Displacement`, `X-Axis_OAVelocity`
  - **Computation**: Derived from FFT-transformed acceleration spectrum (windowed frame → FFT → spectral operations)
  - **Output units**: Magnitude (mg), velocity (m/s), or displacement (m/mm)
  - **Examples of spectral operations**: Parseval-equivalent energy, spectral integration (velocity & displacement), direct magnitude extraction

- **Time-domain features**: `X-Axis_Deviation`, `X-Axis_Skewness`, `X-Axis_Kurtosis`, `X-Axis_CrestFactor`
  - **Computation**: Derived directly from raw time-series acceleration samples (no FFT)
  - **Output**: Statistical moments (central moments) or dimensionless ratios
  - **Key note on Crest Factor**: Defined as the ratio of time-domain Peak (max absolute value in raw samples) to time-domain RMS (energy-equivalent from raw samples). This is **distinct** from spectral Peak/RMS and must be computed from raw time-domain data only.

This classification applies identically to X, Y, and Z axes. In the current B10BG3 single-axis hardware setup, only X-axis features are computable.

---


Sensors covered:
- **B10BG3**: single-axis accelerometer (X-axis only); Y-axis and Z-axis features are not measurable
- **idaq-801**: analog signal acquisition module (not an accelerometer)

**Feature domain classification**: Per § Signal Domain Classification above, features are partitioned into frequency-domain (computed from FFT spectrum) and time-domain (computed directly from raw samples). This classification is based on computation method and aligns with the WISE-2410 sensor specification.

| #   | WISE-2410 Parameter              | Category         | Computable (B10BG3) | Reason / Notes                                                                                                                                       | Failure Modes Detected                                                                 |
| --- | -------------------------------- | ---------------- | :-----------------: | ---------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| 1   | Timestamp_Timestamp              | Metadata         |          ✔          | Mapped directly from hardware sample timestamp                                                                                                       | N/A (metadata only)                                                                    |
| 2   | Device_Time                      | Metadata         |          ✔          | Edge device system clock (`DateTimeOffset.UtcNow`)                                                                                                   | N/A (metadata only)                                                                    |
| 3   | Device_BatteryVolt               | Metadata         |          ✖          | Neither B10BG3 nor idaq-801 exposes battery telemetry                                                                                                | N/A (power telemetry not available)                                                    |
| 4   | X-Axis_RMSmg                     | Frequency-domain |          ✔          | RMS estimated from FFT-transformed spectrum using Parseval-equivalent energy mapping                                                                 | Excess overall vibration energy; possible mechanical looseness or assembly instability |
| 5   | X-Axis_Peakmg                    | Frequency-domain |          ✔          | Maximum spectral magnitude in the FFT-transformed X-axis spectrum                                                                                    | Dominant vibration components and concentrated fault-related spectral spikes           |
| 6   | X-Axis_Peak-to-Peak_Displacement | Frequency-domain |          ✔          | Displacement spectrum derived via double spectral integration of acceleration spectrum, then peak-to-peak range of reconstructed displacement signal | Bearing wear and shaft misalignment indicators                                         |
| 7   | X-Axis_OAVelocity                | Frequency-domain |          ✔          | Velocity spectrum derived from acceleration spectrum, then overall velocity RMS computed over spectral energy                                        | Imbalance and general rotating machinery faults                                        |
| 8   | X-Axis_Deviation                 | Time-domain      |          ✔          | Sample standard deviation of X-axis samples                                                                                                          | Early instability and increased vibration variability                                  |
| 9   | X-Axis_Skewness                  | Time-domain      |          ✔          | Third standardized central moment of X-axis samples                                                                                                  | Distribution asymmetry linked to imbalance or bearing defects                          |
| 10  | X-Axis_Kurtosis                  | Time-domain      |          ✔          | Fourth standardized central moment of X-axis samples                                                                                                 | Impact-type vibration and early-stage bearing defects                                  |
| 11  | X-Axis_CrestFactor               | Time-domain      |          ✔          | Ratio of time-domain Peak to time-domain RMS; both computed directly from raw samples (per § Signal Domain Classification). Dimensionless output.    | Shock severity and localized bearing defect impacts                                    |
| 12  | Y-Axis_RMSmg                     | Frequency-domain |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_RMSmg (not measurable in current setup)                                 |
| 13  | Y-Axis_Peakmg                    | Frequency-domain |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_Peakmg (not measurable in current setup)                                |
| 14  | Y-Axis_Peak-to-Peak_Displacement | Frequency-domain |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_Peak-to-Peak_Displacement (not measurable in current setup)             |
| 15  | Y-Axis_OAVelocity                | Frequency-domain |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_OAVelocity (not measurable in current setup)                            |
| 16  | Y-Axis_Deviation                 | Time-domain      |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_Deviation (not measurable in current setup)                             |
| 17  | Y-Axis_Skewness                  | Time-domain      |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_Skewness (not measurable in current setup)                              |
| 18  | Y-Axis_Kurtosis                  | Time-domain      |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_Kurtosis (not measurable in current setup)                              |
| 19  | Y-Axis_CrestFactor               | Time-domain      |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_CrestFactor (not measurable in current setup)                           |
| 20  | Z-Axis_RMSmg                     | Frequency-domain |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_RMSmg (not measurable in current setup)                                 |
| 21  | Z-Axis_Peakmg                    | Frequency-domain |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_Peakmg (not measurable in current setup)                                |
| 22  | Z-Axis_Peak-to-Peak_Displacement | Frequency-domain |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_Peak-to-Peak_Displacement (not measurable in current setup)             |
| 23  | Z-Axis_OAVelocity                | Frequency-domain |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_OAVelocity (not measurable in current setup)                            |
| 24  | Z-Axis_Deviation                 | Time-domain      |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_Deviation (not measurable in current setup)                             |
| 25  | Z-Axis_Skewness                  | Time-domain      |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_Skewness (not measurable in current setup)                              |
| 26  | Z-Axis_Kurtosis                  | Time-domain      |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_Kurtosis (not measurable in current setup)                              |
| 27  | Z-Axis_CrestFactor               | Time-domain      |          ✖          | B10BG3 is single-axis (X only)                                                                                                                       | Same as X-Axis_CrestFactor (not measurable in current setup)                           |
| 28  | TempHumi_SenVal                  | Environmental    |          ✖          | Requires a separate temperature/humidity sensor                                                                                                      | Thermal or humidity-related stress (sensor unavailable in current setup)               |

**Summary** : 
* For the selected B10BG3 single-axis setup, 10 out of 28 parameters are computable (2 metadata + 8 X-axis features). All Y-axis and Z-axis features are unavailable, and `Device_BatteryVolt` plus `TempHumi_SenVal` require additional hardware telemetry/sensors.

---

## 2. Feature Computation Dataflow

The pipeline runs once per time frame (default: 1 second / 2500 samples). In this implementation, B10BG3 is a single-axis accelerometer, so only the X-axis stream is buffered and processed.
  
Domain relation in this pipeline:
- **Frequency-domain group**: RMS, Peak, Peak-to-Peak Displacement, OAVelocity
- **Time-domain group**: Deviation, Skewness, Kurtosis, CrestFactor

### 2.1 Top-Level Pipeline

```
Raw path: B10BG3 -> idaq-801 -> idaq-934
        |
        |  Raw acceleration stream (X-axis only) @ 2500 Hz
        v
+-------------------+
|  Circular Buffer  |  accumulates X-axis samples only
|  (1 x N sample)   |
+--------+----------+
         |  emit frame when buffer reaches N samples
         v
+------------------------------+
|       Frame Extractor        |  slice N samples for X-axis only
+--------------+---------------+
               |
           (X axis)
               |
               v
+------------------------------------------------------------------+
|                    Per-Axis Feature Stage                        |
|                                                                  |
|  +-----------------------------------------------------------+   |
|  |  Stage A: Direct time-domain features                     |   |
|  |  Input: raw acceleration samples[]                        |   |
|  |                                                           |   |
|  |  |- Deviation     [time-domain]                           |   |
|  |  |- Skewness      [time-domain]                           |   |
|  |  |- Kurtosis      [time-domain]                           |   |
|  |  '- CrestFactor   [time-domain] (peak/rms ratio)          |   |
|  +-----------------------------------------------------------+   |
|                                                                  |
|  +-----------------------------------------------------------+   |
|  |  Stage B: Spectrum generation                             |   |
|  |  Input: raw acceleration samples[]                        |   |
|  |                                                           |   |
|  |  |- Window (Hann)                                         |   |
|  |  '- FFT -> acceleration spectrum[]                        |   |
|  +-------------------------------+----------------------------+   |
|                                  | spectrum                      |
|  +-----------------------------------------------------------+   |
|  |  Stage C: Spectrum-based features                         |   |
|  |  Input: acceleration spectrum[]                           |   |
|  |                                                           |   |
|  |  |- RMS             [frequency-domain]                    |   |
|  |  |- Peak            [frequency-domain]                    |   |
|  |  |- OAVelocity      [frequency-domain]                    |   |
|  |  '- Peak-to-Peak Displacement [frequency-domain]         |   |
|  +-----------------------------------------------------------+   |
+------------------------------------------------------------------+
       |
       v
+----------------------------------------------+
|        Metadata Attachment                   |
|  |- Timestamp  <- idaq-801/idaq-934 packet ts |
|  '- Device_Time <- system clock               |
+----------------------------------------------+
       |
       v
  Wise2410Features object
  (sent to PHM Inferencer @ 1 Hz, aligned with PHM trend target
   and batched to WEDA Core)
```

  

### 2.2 Dependency Graph

```
raw samples[]
    ├─► (Stage A) Deviation [time-domain] -------------------------► output
    ├─► (Stage A) Skewness [time-domain] -------------------------► output
    ├─► (Stage A) Kurtosis [time-domain] -------------------------► output
    ├─► (Stage A) CrestFactor [time-domain] ----------------------► output
    │
    └─► (Stage B) Window (Hann) + FFT → spectrum[]
            ├─► (Stage C) RMS [frequency-domain] ------------------► output
            ├─► (Stage C) Peak [frequency-domain] -----------------► output
            ├─► (Stage C) Spectral integration → OAVelocity --------► output
            └─► (Stage C) Spectral integration → displacement[]
                    └─► Peak-to-Peak [frequency-domain] ----------► output
```

---

## 3. Algorithm Reference

This chapter documents the algorithm, pseudocode, and C# implementation for each computable feature.

---

### 3.1 Timestamp

**Algorithm**: Direct timestamp mapping  
**Input**: idaq-801 hardware sample timestamp  
**Output**: Unix millisecond timestamp

**Pseudocode**

```
function GetTimestamp(idaqSampleTimestamp):
    return idaqSampleTimestamp   // already in ms since epoch
```

**C# Implementation**

| Library         | Method                                                                                             |
| --------------- | -------------------------------------------------------------------------------------------------- |
| System (BCL)    | `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` (fallback when hardware timestamp is unavailable) |
| idaq-801 driver | Use the `Timestamp` field from the hardware packet directly                                        |

---

### 3.2 Device_Time
 
**Algorithm**: System clock read  
**Input**: Operating system clock  
**Output**: ISO-8601 datetime string or Unix timestamp

**Pseudocode**

```
function GetDeviceTime():
    return SystemClock.UtcNow
```

**C# Implementation**

| Library      | Method                  |
| ------------ | ----------------------- |
| System (BCL) | `DateTimeOffset.UtcNow` |

---

### 3.3 RMS (Root Mean Square)

**Algorithm**: RMS from FFT-transformed spectrum (Parseval-equivalent)  

**Signal Domain**: Frequency-domain  

**Input**: `spectrum[]` - N complex FFT bins from one frame  

**Output**: Scalar (mg or m/s²)

  

**Pseudocode**

```
function ComputeRMSFromSpectrum(spectrum[N]):
    powerSum = 0
    for k in 0..N-1:
        powerSum += abs(spectrum[k])^2
    return sqrt(powerSum / (N * N))
```

**Mathematical definition**:
$$\text{RMS} = \sqrt{\frac{1}{N^2} \sum_{k=0}^{N-1} |X[k]|^2}$$

> Note: This expression assumes an unnormalized forward DFT convention. If your FFT library applies different normalization, adjust the scaling factor accordingly.

**C# Implementation**

| Library          | Package            | Method                                                                                   |
| ---------------- | ------------------ | ---------------------------------------------------------------------------------------- |
| MathNet.Numerics | `MathNet.Numerics` | `Fourier.Forward(samplesComplex)` + magnitude-squared accumulation (`Complex.Magnitude`) |
| System / LINQ    | Built-in           | `Math.Sqrt(spectrum.Select(c => c.Magnitude * c.Magnitude).Sum() / (N * N))`             |

---

### 3.4 Peak

**Algorithm**: Peak spectral magnitude from FFT-transformed spectrum  

**Signal Domain**: Frequency-domain  

**Input**: `spectrum[]` - N complex FFT bins from one frame  

**Output**: Scalar (mg or m/s²)

**Pseudocode**

```

function ComputePeakFromSpectrum(spectrum[N]):

    peak = 0

    for k in 0..N-1:

        mag = abs(spectrum[k])

        if mag > peak:

            peak = mag

    return peak

```

**Mathematical definition**:
$$\text{Peak} = \max_{0 \le k < N} |X[k]|$$

> Note: If your FFT implementation applies scaling on forward or inverse transforms, keep Peak and RMS scaling conventions aligned before calculating CrestFactor.

**C# Implementation**

| Library          | Package            | Method                                                               |
| ---------------- | ------------------ | -------------------------------------------------------------------- |
| MathNet.Numerics | `MathNet.Numerics` | `Fourier.Forward(samplesComplex)` + `spectrum.Max(c => c.Magnitude)` |
| System / LINQ    | Built-in           | `spectrum.Max(c => c.Magnitude)`                                     |

---

### 3.5 Deviation (Standard Deviation)

**Algorithm**: Sample standard deviation (Bessel-corrected, divisor N-1)  

**Signal Domain**: Time-domain  

**Input**: `samples[]`  

**Output**: Scalar in same unit as input

  

**Pseudocode**

```

function ComputeDeviation(samples[N]):

    mean = Average(samples)

    sumSq = 0

    for i in 0..N-1:

        sumSq += (samples[i] - mean)^2

    return sqrt(sumSq / (N - 1))

```

**Mathematical definition**:

$$\sigma = \sqrt{\frac{1}{N-1}\sum_{i=1}^{N}(x_i - \bar{x})^2}$$

**C# Implementation**

| Library          | Package             | Method                                                                                                          |
| ---------------- | ------------------- | --------------------------------------------------------------------------------------------------------------- |
| MathNet.Numerics | `MathNet.Numerics`  | `new DescriptiveStatistics(samples).StandardDeviation`                                                          |
| Accord.NET       | `Accord.Statistics` | `samples.StandardDeviation()`                                                                                   |
| System / LINQ    | Built-in            | Custom (sample std, divisor `N-1`): `Math.Sqrt(samples.Sum(x => Math.Pow(x - mean, 2)) / (samples.Length - 1))` |

---

### 3.6 Skewness

**Algorithm**: Third standardized central moment  

**Signal Domain**: Time-domain  

**Input**: `samples[]`  

**Output**: Dimensionless scalar (0 = symmetric)

**Convention used in this plan**: population central moments (divisor `N`) and Pearson kurtosis (normal baseline = 3). This convention is used consistently for Skewness (§3.6) and Kurtosis (§3.7).

**Pseudocode**

```

function ComputeSkewness(samples[N]):

    mean = Average(samples)

    m2 = (1/N) * sum((samples[i] - mean)^2)

    m3 = (1/N) * sum((samples[i] - mean)^3)

    return m3 / (m2^(3/2))

```

**Mathematical definition**:

$$\text{Skewness} = \frac{\frac{1}{N}\sum_{i=1}^{N}(x_i - \bar{x})^3}{\left(\frac{1}{N}\sum_{i=1}^{N}(x_i - \bar{x})^2\right)^{3/2}}$$

**C# Implementation**

| Library          | Package             | Method                                                                                                    |
| ---------------- | ------------------- | --------------------------------------------------------------------------------------------------------- |
| MathNet.Numerics | `MathNet.Numerics`  | `new DescriptiveStatistics(samples).Skewness`                                                             |
| Accord.NET       | `Accord.Statistics` | `samples.Skewness()`                                                                                      |
| System / LINQ    | Built-in            | Custom (population moment): `m3 / Math.Pow(m2, 1.5)` where `m2 = Avg((x-mean)^2)`, `m3 = Avg((x-mean)^3)` |

---

### 3.7 Kurtosis

**Algorithm**: Fourth standardized central moment  

**Signal Domain**: Time-domain  

**Input**: `samples[]`  

**Output**: Dimensionless scalar (normal distribution baseline ≈ 3)

**Pseudocode**

```

function ComputeKurtosis(samples[N]):

    mean = Average(samples)

    m2 = (1/N) * sum((samples[i] - mean)^2)

    m4 = (1/N) * sum((samples[i] - mean)^4)

    return m4 / (m2 * m2)

```

**Mathematical definition**:

$$\text{Kurtosis} = \frac{\frac{1}{N}\sum_{i=1}^{N}(x_i - \bar{x})^4}{\left(\frac{1}{N}\sum_{i=1}^{N}(x_i - \bar{x})^2\right)^2}$$

> Convention lock for this plan: use **Pearson kurtosis** (normal baseline = 3). If a library returns **excess kurtosis**, convert by `kurtosisPearson = kurtosisExcess + 3` before mapping to WISE-2410 output.

**C# Implementation** 

| Library          | Package             | Method                                                                                  |
| ---------------- | ------------------- | --------------------------------------------------------------------------------------- |
| MathNet.Numerics | `MathNet.Numerics`  | `new DescriptiveStatistics(samples).Kurtosis`                                           |
| Accord.NET       | `Accord.Statistics` | `samples.Kurtosis()`                                                                    |
| System / LINQ    | Built-in            | Custom (Pearson): `m4 / (m2 * m2)` where `m2 = Avg((x-mean)^2)`, `m4 = Avg((x-mean)^4)` |

---

### 3.8 Crest Factor

**Algorithm**: Ratio of time-domain Peak to time-domain RMS

**Signal Domain**: Time-domain

**Input**: `samples[]` (raw acceleration samples)

**Output**: Dimensionless scalar (≥ 1)

**Pseudocode**

```
function ComputeCrestFactor(samples[N]):
    peak = max(abs(samples[i]))  for i in 0..N-1
    rms = sqrt(mean(samples[i]^2))  for i in 0..N-1
    if rms == 0: return 0
    return peak / rms
```

**Mathematical definition**:

$$\text{CrestFactor} = \frac{\max(|x_i|)}{\sqrt{\frac{1}{N}\sum_{i=1}^{N} x_i^2}}$$

**C# Implementation**

| Library       | Package  | Method                                                                   |
| ------------- | -------- | ------------------------------------------------------------------------ |
| System / LINQ | Built-in | `samples.Max(x => Math.Abs(x)) / Math.Sqrt(samples.Average(x => x * x))` |

> **Note**: Computed directly from raw time-domain samples (Stage A, § 2.1), independent of spectral calculations (§3.3 and §3.4).

---

### 3.9 Overall Velocity (OAVelocity)

**Algorithm**: Compute velocity spectrum from acceleration spectrum, then estimate overall velocity RMS from spectral energy  

**Signal Domain**: Frequency-domain  

**Input**: `spectrum[]` (acceleration FFT bins), sampling frequency `fs`  

**Output**: Scalar velocity (m/s RMS)

  

**Pseudocode**

```

function ComputeOAVelocityFromSpectrum(accelSpectrum[N], fs):

    velocitySpectrum[0] = 0   // suppress DC term

    for k in 1..N-1:

        f = k * fs / N

        omega = 2 * pi * f

        velocitySpectrum[k] = accelSpectrum[k] / (j * omega)

  

    powerSum = 0

    for k in 0..N-1:

        powerSum += abs(velocitySpectrum[k])^2

    return sqrt(powerSum / (N * N))

```

  

**Mathematical definition**:

$$V[k] = \frac{A[k]}{j\omega_k},\quad \omega_k = 2\pi f_k,\quad f_k = \frac{k f_s}{N},\ k>0$$

$$V[0] = 0$$

$$\text{OAVelocity} = \sqrt{\frac{1}{N^2} \sum_{k=0}^{N-1} |V[k]|^2}$$

  

> Note: In practice, apply a low-frequency cutoff (or detrend/high-pass in time domain before FFT) to reduce instability near $\omega \to 0$.

  

**C# Implementation**

  

| Library          | Package            | Method                                                                                                                    |
| ---------------- | ------------------ | ------------------------------------------------------------------------------------------------------------------------- |
| MathNet.Numerics | `MathNet.Numerics` | `Fourier.Forward(samplesComplex)` + per-bin `A[k] / (Complex.ImaginaryOne * omega)` + RMS from `Magnitude^2` accumulation |
| System / LINQ    | Built-in           | Custom spectral integration loop with DC suppression and optional low-frequency cutoff                                    |

  

> **Note**: Keep FFT normalization and unit conversion consistent with §3.3 and §3.4 before publishing final m/s RMS values.

---

### 3.10 Peak-to-Peak Displacement

**Algorithm**: Double spectral integration of acceleration spectrum to obtain displacement spectrum, then peak-to-peak range of reconstructed displacement signal  

**Signal Domain**: Frequency-domain  

**Input**: `spectrum[]` (acceleration FFT bins), sampling frequency `fs`  

**Output**: Scalar displacement range (m or mm)

**Pseudocode**

```

function ComputePeakToPeakDisplacementFromSpectrum(accelSpectrum[N], fs):

    displacementSpectrum[0] = 0   // suppress DC term

    for k in 1..N-1:

        f = k * fs / N

        omega = 2 * pi * f

        displacementSpectrum[k] = accelSpectrum[k] / (-(omega * omega))

    displacement[] = IFFT(displacementSpectrum)

    return max(displacement) - min(displacement)

```

**Mathematical definition**:
$$D[k] = \frac{A[k]}{(j\omega_k)^2} = \frac{-A[k]}{\omega_k^2},\quad k>0$$
$$D[0] = 0$$
$$d[i] = \text{IFFT}(D)[i]$$
$$\text{P2P} = \max(d) - \min(d)$$

> Note: Apply the same low-frequency cutoff used in §3.9 to suppress numerical instability near $\omega \to 0$.

**C# Implementation**

| Library          | Package            | Method                                                                                                            |
| ---------------- | ------------------ | ----------------------------------------------------------------------------------------------------------------- |
| MathNet.Numerics | `MathNet.Numerics` | `Fourier.Forward(samplesComplex)` + per-bin `A[k] / (-(omega * omega))` + `Fourier.Inverse(displacementSpectrum)` |
| System / LINQ    | Built-in           | `displacement.Max() - displacement.Min()` for the final range after IFFT                                          |

> **Note**: Keep FFT normalization and unit conversion consistent with §3.9 before publishing final displacement values.
---

## Appendix: Recommended NuGet Packages

| Package                 | NuGet ID           | Primary use in this plan                                             |
| ----------------------- | ------------------ | -------------------------------------------------------------------- |
| MathNet.Numerics        | `MathNet.Numerics` | RMS, Standard Deviation, Skewness, Kurtosis                          |
| FftSharp                | `FftSharp`         | Optional future FFT-based features (currently not used in this plan) |
| System.Numerics.Vectors | Built-in (.NET 6+) | SIMD acceleration for custom inner loops                             |

## Appendix: Processing Constraints

| Parameter                                     | Value                                       |
| --------------------------------------------- | ------------------------------------------- |
| Machine rated speed                           | 3000 RPM (50 Hz fundamental)                |
| Sampling rate                                 | 2500 Hz (fixed)                             |
| Default frame size                            | 2500 samples (1 second)                     |
| FFT size                                      | 2500                                        |
| Frequency resolution                          | 1 Hz                                        |
| Nyquist frequency                             | 1250 Hz                                     |
| Window function                               | Hann                                        |
| Frame overlap                                 | None (consecutive frames)                   |
| Output rate (WISE-2410 features)              | 1 Hz                                        |
| Output rate (Recommended PHM features)        | 1 Hz (aligned with WISE-2410 cadence)       |
| Reference sensor capability (WISE-2410)       | 6600 Hz (hardware specification)            |
| Axes processed                                | X only (B10BG3 single-axis)                 |
| Total WISE-2410 output values per window      | 10 (2 metadata + 8 X-axis features)         |
| Target analysis bandwidth                     | 1–1000 Hz (covers 1X–5X harmonics + margin) |
| Features unavailable from idaq-934 + idaq-801 | `Device_BatteryVolt`, `TempHumi_SenVal`     |