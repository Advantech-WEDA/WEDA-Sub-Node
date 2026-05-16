#!/usr/bin/env node
// Schema-level validation for all five WEDA System-Agent DTDL Interfaces.
// Usage:  (cd dtdl-validate/scripts && npm install && npm run validate:dtdl)
// Exit:   0 on success, 1 if any file fails to parse, 2 if parser not installed.

let createParser, ModelParsingOption;
try {
  ({ createParser, ModelParsingOption } = require('@azure/dtdl-parser'));
} catch (e) {
  console.error('ERR @azure/dtdl-parser not installed.');
  console.error('    Install: cd dtdl-validate/scripts && npm install');
  process.exit(2);
}

const fs = require('fs');
const path = require('path');

// Resolve DTDL files relative to DTDL_BASE (Docker / CI) or, for local dev,
// walk from this script (dtdl-validate/scripts/) up to the project root and
// into docs/Metrics/.
const BASE = process.env.DTDL_BASE
  ? path.resolve(process.env.DTDL_BASE)
  : path.resolve(__dirname, '..', '..', 'docs', 'Metrics');
const FILES = [
  '01_CPU_NETWORK.dtdl.json',
  '02_MEMORY_DISK_SYSTEM_GPU.dtdl.json',
  '03_HARDWARE_INFO.dtdl.json',
  '04_ONBOARD_SENSOR.dtdl.json',
  '05_HARDWARE_FEATURE.dtdl.json',
];

(async () => {
  let failed = 0;

  for (const f of FILES) {
    const json = fs.readFileSync(path.join(BASE, f), 'utf8');
    try {
      const parser = createParser(ModelParsingOption.PermitAnyTopLevelElement);
      const model = await parser.parse([json]);
      console.log(`OK  ${f}: ${Object.keys(model).length} entities`);
    } catch (e) {
      failed++;
      console.error(`ERR ${f}:`);
      console.error('  ' + (e.message || e).toString().split('\n').slice(0, 20).join('\n  '));
    }
  }

  try {
    const jsons = FILES.map(f => fs.readFileSync(path.join(BASE, f), 'utf8'));
    const parser = createParser(ModelParsingOption.PermitAnyTopLevelElement);
    const model = await parser.parse(jsons);
    console.log(`OK  combined: ${Object.keys(model).length} entities`);
  } catch (e) {
    failed++;
    console.error('ERR combined:');
    console.error('  ' + (e.message || e).toString().split('\n').slice(0, 20).join('\n  '));
  }

  process.exit(failed === 0 ? 0 : 1);
})();
