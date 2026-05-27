# Weda.Dtdl Extraction Migration

> Status: Planned (2026-05-26)
> Target repo: `~/advantech/projects/weda_dt_validator` → Azure DevOps `Advantech-EBO/IoT Platform/weda_dt_validator`
> Target package: `Weda.Dtdl` (NuGet, v1.0.0)

## 1. Motivation

The DTDL emitter, the custom `dtmi:advantech:edgesync:validation;1` extension, and the wrapper around the official `DTDLParser` are **horizontal utilities** — useful to:

- SubNode (capability upload — emit + self-validate)
- system-agent (`devicecfg.json` DTDL authoring + validation)
- Cloud / front-end (parse received capability DTDL + read `ConfigConstraint` fields)
- Any future Weda product line that wants to declare strongly-typed config via DTDL

Keeping them inside `Weda.SubNode.Core` makes them invisible to non-SubNode consumers and forces them to pull the entire SubNode dependency tree. Extracting to a standalone versioned package (`Weda.Dtdl`) removes that coupling and lets the extension evolve at its own cadence.

## 2. What moves

| Origin (`edge_subnode/`) | Destination (`weda_dt_validator/`) | Notes |
|---|---|---|
| `src/Weda.SubNode.Core/Schema/DtdlInterfaceEmitter.cs` | `src/Weda.Dtdl/Emit/DtdlInterfaceEmitter.cs` | Namespace `Weda.SubNode.Core.Schema` → `Weda.Dtdl.Emit` |
| `src/Weda.SubNode.Core/Schema/DtdlInterfaceEmissionException` (nested in same file) | `src/Weda.Dtdl/Emit/DtdlInterfaceEmissionException.cs` | Split into own file |
| `tests/Weda.SubNode.Core.Tests/Schema/DtdlInterfaceEmitterTests.cs` | `tests/Weda.Dtdl.Tests/Emit/DtdlInterfaceEmitterTests.cs` | Drop BatchReport real-world test (production-coupled — stays in edge_subnode) |
| `tests/Weda.SubNode.Core.Tests/Schema/DtdlParserCompatibilityTests.cs` | `tests/Weda.Dtdl.Tests/Parsing/WedaDtdlParserTests.cs` | Renamed; rewritten to assert `WedaDtdlParser.Create()` behaviour rather than raw `ModelParser` |
| `docs/dtdl-extension-validation-v1.md` | `docs/extension-spec.md` (in weda_dt_validator) | Becomes the canonical spec doc inside the package repo |

## 3. What stays in edge_subnode

| Stays in | Reason |
|---|---|
| `TypedParameterConverter` (`src/Weda.SubNode.Core/Schema/`) | SubNode-specific — bridges `Dictionary<string, object>` ↔ POCO at the `devicecfg.json` boundary. Not DTDL-related. |
| `IConfigurableDevice` / `IConfigurableSensor` interfaces (`src/Weda.SubNode.Abstractions/`) | SubNode capability registration abstractions. They are *consumed by* the emitter, not part of it. |
| Command / Transform / DSP filter / Device POCOs + `[Display]` annotations | Application-level — they are the *input* to the emitter. |
| `BatchReportParameters` real-world emit fixture | Production POCO coupling — stays alongside the production code. |
| `docs/dtdl-design.md`, `docs/command-dtdl.md`, `docs/refactor/*` | SubNode-side design docs; cross-link to the extracted package's spec doc. |

## 4. New code (born in `weda_dt_validator`)

These do not exist in edge_subnode and will be authored fresh in the new repo:

| File | Purpose |
|---|---|
| `src/Weda.Dtdl/Parsing/WedaDtdlParser.cs` | `Create()` and `ParseAsync()` static helpers — wrap `ModelParser` with `AllowUndefinedExtensions = WhenToAllow.Always` so consumers never touch the flag. |
| `src/Weda.Dtdl/Parsing/ConfigConstraint.cs` | Typed record projection of the `ConfigConstraint` co-type — `From(DTEntityInfo)` factory reads `UndefinedProperties` (or native `DTPropertyInfo.Writable` for Property `writable`). |
| `src/Weda.Dtdl/Parsing/ConfigConstraintValidator.cs` | Walks parsed model, type-checks every `ConfigConstraint`-co-typed element against the spec — catches emitter bugs that vanilla parser cannot. |
| `src/Weda.Dtdl/Shared/DtdlExtensionConstants.cs` | Single source of truth for the context URI, co-type name, and 8 extension field name strings. Both emit and parse reference it. |
| `src/Weda.Dtdl/Exceptions/WedaDtdlException.cs` | Base exception. |
| `src/Weda.Dtdl/Exceptions/ConfigConstraintValidationException.cs` | Thrown by validator. |

## 5. Namespace mapping

| Before (edge_subnode) | After (weda_dt_validator) |
|---|---|
| `Weda.SubNode.Core.Schema.DtdlInterfaceEmitter` | `Weda.Dtdl.Emit.DtdlInterfaceEmitter` |
| `Weda.SubNode.Core.Schema.DtdlInterfaceEmissionException` | `Weda.Dtdl.Emit.DtdlInterfaceEmissionException` |
| (new) | `Weda.Dtdl.Parsing.WedaDtdlParser` |
| (new) | `Weda.Dtdl.Parsing.ConfigConstraint` |
| (new) | `Weda.Dtdl.Parsing.ConfigConstraintValidator` |
| (new) | `Weda.Dtdl.DtdlExtensionConstants` |

## 6. Versioning policy

- `Weda.Dtdl` v**X.Y.Z** corresponds 1:1 to spec version `dtmi:advantech:edgesync:validation;X` (major version locked).
- v1.0.0 ships `dtmi:advantech:edgesync:validation;1` — the spec frozen at first non-draft release.
- Minor releases (v1.1, v1.2 ...) add fields backward-compatibly; major release (v2.0) bumps spec to `validation;2`.
- See `CHANGELOG.md` in `weda_dt_validator` for per-release diff.

## 7. Phased migration

| Phase | Action | When |
|---|---|---|
| **A** | Set up `weda_dt_validator` repo (solution / csproj / Directory.*.props / CI) | This refactor |
| **B** | Migrate emitter + emitter tests (file move + namespace update) | This refactor |
| **C** | Author new parser code (`WedaDtdlParser`, `ConfigConstraint`, `ConfigConstraintValidator`) + tests | This refactor |
| **D** | Author docs (`README.md`, `CHANGELOG.md`, `docs/extension-spec.md`, `docs/annotations.md`) | This refactor |
| **E** | Set up Azure DevOps pipeline producing `Weda.Dtdl.1.0.0.nupkg` artifact | This refactor |
| **F** | First green CI build, publish `1.0.0` artifact | Triggered by phase E |
| **G** | edge_subnode consumes `Weda.Dtdl` via `<PackageReference>` in `Directory.Packages.props`; remove migrated files; update namespaces in consumer code | Follow-up commit |

Phase G can run as a separate edge_subnode commit so the package is available + verified before edge_subnode pulls it in.

## 8. Consumer impact in edge_subnode (Phase G)

| File | Change |
|---|---|
| `Directory.Packages.props` | Add `<PackageVersion Include="Weda.Dtdl" Version="1.0.0" />` |
| `src/Weda.SubNode.Core/Weda.SubNode.Core.csproj` | Add `<PackageReference Include="Weda.Dtdl" />` |
| Any file that `using Weda.SubNode.Core.Schema;` for emitter | Add `using Weda.Dtdl.Emit;` (and remove old reference when emitter is deleted) |
| `tests/Weda.SubNode.Core.Tests/Schema/DtdlInterfaceEmitterTests.cs` | Deleted — migrated; the BatchReport real-world fixture moves into a new `tests/Weda.SubNode.Core.Tests/Schema/EmitterIntegrationTests.cs` that only contains the production-POCO smoke tests |
| `tests/Weda.SubNode.Core.Tests/Schema/DtdlParserCompatibilityTests.cs` | Deleted — its purpose is fulfilled by `WedaDtdlParserTests` in the new repo |
| `docs/dtdl-extension-validation-v1.md` | Replaced with a short pointer doc that links to the canonical spec in `weda_dt_validator` |

## 9. Distribution posture

`weda_dt_validator` is a **private repository publishing a public NuGet package**:

- Repository: private (Azure DevOps `Advantech-EBO/IoT Platform`)
- Package feed: **NuGet.org** (`Weda.Dtdl` package, publicly installable)
- Package license: **Apache-2.0** (`<PackageLicenseExpression>` in `common.props`)
- README at repo root: shipped inside the `.nupkg` and rendered on the NuGet.org package page
- No LICENSE file at repo root, no CONTRIBUTING.md — repo is not open-source; external contributions are not expected
- No GitHub mirror — Azure DevOps is the single source of truth

## 10. Risks and rollback

| Risk | Mitigation |
|---|---|
| Build break in edge_subnode after Phase G if `Weda.Dtdl` package not yet published | Phase G blocked on green CI from Phase F; ProjectReference-based local development possible during transit. |
| Namespace churn for downstream that already references `Weda.SubNode.Core.Schema.DtdlInterfaceEmitter` | No downstream exists yet — pre-emptive rename has no breakage cost. |
| `Weda.Dtdl` and edge_subnode versions drift | Central package management (`Directory.Packages.props`) pins one version; CHANGELOG documents breaking changes. |

Rollback (if needed before Phase G): revert the deletion commits in edge_subnode and leave `weda_dt_validator` as an isolated experimental package — no integration disruption.

## 11. Related

- [`docs/dtdl-design.md`](../dtdl-design.md) — master design doc for SubNode capability DTDL mapping
- [`docs/dtdl-extension-validation-v1.md`](../dtdl-extension-validation-v1.md) — spec doc that will move to `weda_dt_validator/docs/extension-spec.md`
- [`docs/command-dtdl.md`](../command-dtdl.md) — command-specific DTDL mapping (stays in edge_subnode)
- [`docs/refactor/capability-schema-upload.md`](./capability-schema-upload.md) — background research
- [`docs/refactor/typed-poco-dtdl-emit.md`](./typed-poco-dtdl-emit.md) — POCO author manual (stays in edge_subnode; cross-links to spec in `weda_dt_validator`)
