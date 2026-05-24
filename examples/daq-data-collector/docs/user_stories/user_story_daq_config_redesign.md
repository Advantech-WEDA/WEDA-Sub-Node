## User Story: Intuitive DAQ Configuration for System Integrators

As a system integrator,
I want to configure the DAQ data collector using parameters that directly express my physical and operational requirements,
So that I can deploy the system independently and confidently, even without expertise in signal processing, reducing the risk of misconfiguration and shortening integration time.

## Definition of Done
* Configuration parameter redesign implementation is completed.
* No critical or high-severity defects remain open.
* Code is reviewed by at least one other team member.
* Code is merged into develop branch of WEDA subnode repo.
* Relevant technical documentation or comments are updated.
* All acceptance criteria are satisfied.

## Acceptance Criteria

### AC1: Direct Expression of Signal Acquisition Requirements
* Given
  * The system integrator knows the target frequency range of interest:
    * The highest frequency component they need to detect (determines required sampling rate via Nyquist)
    * The lowest frequency component they need to detect (determines required observation window duration)
* When
  * The integrator sets exactly two parameters in the configuration file:
    * `AcquisitionRateHz` — the hardware sampling rate, in Hz
    * `ObservationWindowSeconds` — the signal observation window duration, in seconds
* Then
  * The system automatically derives the following internal signal processing parameters:
    * `frame_size` (samples) = `AcquisitionRateHz` × `ObservationWindowSeconds`
    * `frequency_resolution` (Hz) = 1 / `ObservationWindowSeconds`
    * `nyquist_frequency` (Hz) = `AcquisitionRateHz` / 2
  * The integrator is not required to set or understand any of the above derived values
  * The correctness of the derived values can be verified via the startup log (see AC4)

### AC2: Independent Per-Sensor Reporting Interval
* Given
  * Multiple PHM feature sensors (e.g., RMS, FFT peak) with different monitoring cadence requirements
* When
  * The integrator sets `Report.Interval` (milliseconds) independently for each sensor in the configuration file
* Then
  * Each sensor uploads telemetry to WEDA node at its own configured `Report.Interval`
  * The reporting interval for each sensor is independent from the signal analysis window setting

### AC3: Configuration Validation
* Given
  * The DAQ service loads configuration at startup, or the integrator updates a configuration field via a WEDA node command at runtime
  * (All scenarios below apply under this precondition)
* When
  * `AcquisitionRateHz` or `ObservationWindowSeconds` has an invalid value (e.g., non-positive number, wrong type)
* Then
  * The system refuses to start and emits a clear, actionable error message identifying the offending field
* When
  * A sensor's `Report.Interval` is less than `ObservationWindowSeconds × 1000` (ms)
* Then
  * At startup: the system refuses to start and emits a clear, actionable error message identifying the offending sensor
  * At runtime: the system rejects the change, retains the previous configuration, and emits a clear, actionable error message identifying the offending sensor
* When
  * `Report.Interval` is not an exact integer multiple of `ObservationWindowSeconds × 1000` (ms)
* Then
  * The system logs a warning identifying the offending sensor, the configured value, and the effective value applied
  * Execution continues with the rounded-down effective interval: `N × ObservationWindowSeconds × 1000` ms, where `N = floor(Report.Interval / (ObservationWindowSeconds × 1000))`

### AC4: Configuration Transparency via Startup Log
* Given
  * The DAQ data collector is started with a configuration file
* When
  * The configuration is loaded successfully
* Then
  * The startup log displays the following at **device level**:
    * `frame_size`, `frequency_resolution`, `nyquist_frequency` (as defined in AC1)
  * The startup log displays the following at **per-sensor level**:
    * The effective reporting interval applied (`N × ObservationWindowSeconds × 1000` ms, as defined in AC3)
  * The integrator can verify that the actual runtime behavior matches their intent by reading the log, without modifying source code
