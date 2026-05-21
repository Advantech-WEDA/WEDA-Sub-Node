#!/usr/bin/env node
// Configuration-level validation: walks docs/Metrics/samples-config/*.json,
// applies the shape rules in config-rules.js to each sensor entry, and
// reports pass/fail. The first shipped ruleset covers Parameters.Interfaces
// shape and mutual-exclusivity with Parameters.Interface for `network`
// sensors; add more types in config-rules.js as you need them.
//
// Each fixture is a JSON array of sensor entries with two extra fields:
//   "expect": "valid" | "invalid"
//   "reason": "<human readable reason>" (informational)
//
// Usage:  (cd dtdl-validate/scripts && npm install && npm run validate:configs)
// Exit:   0 on success, 1 if any sensor lands on the wrong verdict.

const fs = require('fs');
const path = require('path');
const { validate } = require('./config-rules');

const CONFIGS_DIR = process.env.DTDL_BASE
  ? path.resolve(process.env.DTDL_BASE, 'samples-config')
  : path.resolve(__dirname, '..', '..', 'docs', 'Metrics', 'samples-config');

if (!fs.existsSync(CONFIGS_DIR)) {
  console.error(`ERR samples-config dir not found: ${CONFIGS_DIR}`);
  process.exit(1);
}

const fixtures = fs.readdirSync(CONFIGS_DIR)
  .filter(f => f.endsWith('.json'))
  .sort()
  .map(f => ({
    file: f,
    sensors: JSON.parse(fs.readFileSync(path.join(CONFIGS_DIR, f), 'utf8')),
  }));

let failed = 0;
let total = 0;

for (const { file, sensors } of fixtures) {
  console.log(`\n--- ${file} (${sensors.length} sensors) ---`);
  for (const s of sensors) {
    total++;
    let result;
    try {
      result = validate(s);
    } catch (e) {
      console.error(`  ERR rule threw for ${s.Name || '<unnamed>'}: ${e.message}`);
      failed++;
      continue;
    }
    const expected = s.expect === 'valid';
    const ok = !!result.ok;
    const label = (s.expect || 'unknown').padEnd(7);
    const name  = s.Name || '<unnamed>';
    if (ok === expected) {
      console.log(`  OK  ${label} ${name}`);
    } else {
      const verdict = ok ? 'valid' : 'invalid';
      const got = result.reason ? `: ${result.reason}` : '';
      const exp = s.reason ? `  // expected: ${s.reason}` : '';
      console.error(`  ERR ${label} ${name} (validator said ${verdict}${got})${exp}`);
      failed++;
    }
  }
}

console.log();
console.log(failed === 0 ? `ALL ${total} CONFIGS PASSED` : `${failed} of ${total} configs FAILED`);
process.exit(failed === 0 ? 0 : 1);
