# Internal Examples

This directory contains internal testing examples using real Advantech devices.

**⚠️ IMPORTANT**: This directory and its contents are **NOT included in the public `develop` branch**. It exists only in internal branches for Advantech internal testing and development.

## Contents

- **wise-4012/** - WISE-4012 device integration example
- **wise-4012-isensing/** - WISE-4012 with iSensing module integration example

## Branch Strategy

- `develop` branch: Does **NOT** include this `internal/` directory
- `internal` or `advantech-internal` branch: Includes this directory

## Usage

These examples demonstrate integration with real Advantech hardware devices and are used for:
- Internal testing and validation
- Hardware compatibility verification
- Customer reference implementations (under NDA)
- Production deployment examples

## Adding New Internal Examples

When adding new internal device examples:
1. Create a new subdirectory under `internal/`
2. Follow the naming convention: `{product-line}-{model}/`
3. Include a README.md in each example explaining the hardware setup
4. Ensure examples are only committed to internal branches

## Confidentiality

Do **NOT** merge this directory into the public `develop` branch.
