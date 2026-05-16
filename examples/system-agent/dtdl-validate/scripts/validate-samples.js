#!/usr/bin/env node
// Instance-level validation: run every sample under docs/Metrics/samples/
// against the predicates in dtdl-validate/scripts/predicates.js. A sample
// passes when its predicate's verdict matches the declared `expect` value.
//
// Usage:  node dtdl-validate/scripts/validate-samples.js
// Exit:   0 on success, 1 if any sample lands on the wrong verdict.

const fs = require('fs');
const path = require('path');
const predicates = require('./predicates');

// Resolve sample fixtures relative to DTDL_BASE (Docker / CI) or, for local
// dev, walk from this script (dtdl-validate/scripts/) up to the project root
// and into docs/Metrics/samples/.
const SAMPLES_DIR = process.env.DTDL_BASE
  ? path.resolve(process.env.DTDL_BASE, 'samples')
  : path.resolve(__dirname, '..', '..', 'docs', 'Metrics', 'samples');

if (!fs.existsSync(SAMPLES_DIR)) {
  console.error(`ERR samples dir not found: ${SAMPLES_DIR}`);
  process.exit(1);
}

const fixtures = fs.readdirSync(SAMPLES_DIR)
  .filter(f => f.endsWith('.json'))
  .sort()
  .map(f => ({
    file: f,
    samples: JSON.parse(fs.readFileSync(path.join(SAMPLES_DIR, f), 'utf8'))
  }));

let failed = 0;
let total = 0;

for (const { file, samples } of fixtures) {
  console.log(`\n--- ${file} (${samples.length} samples) ---`);
  for (const s of samples) {
    total++;
    const fn = predicates[s.ResourceId];
    if (!fn) {
      console.error(`  ERR no predicate registered for ${s.ResourceId}`);
      failed++;
      continue;
    }
    let ok;
    try {
      ok = fn(s.Value);
    } catch (e) {
      console.error(`  ERR predicate threw for ${s.ResourceId}: ${e.message}`);
      failed++;
      continue;
    }
    const expected = s.expect === 'valid';
    const verdict = ok ? 'valid' : 'invalid';
    if (ok === expected) {
      console.log(`  OK  ${s.expect.padEnd(7)} ${s.ResourceId} = ${JSON.stringify(s.Value)}`);
    } else {
      console.error(
        `  ERR ${s.expect.padEnd(7)} ${s.ResourceId} = ${JSON.stringify(s.Value)} ` +
        `(predicate said ${verdict})` + (s.reason ? `  ${s.reason}` : ''));
      failed++;
    }
  }
}

console.log(`\n${failed === 0 ? `ALL ${total} SAMPLES PASSED` : `${failed} of ${total} samples FAILED`}`);
process.exit(failed === 0 ? 0 : 1);
