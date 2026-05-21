#!/usr/bin/env node
// One-shot verification: load every Interface in docs/Metrics/dtdl/, validate
// each sensor entry in docs/Metrics/samples-config/*.configs.json against:
//   (a) the parsed DTDL Interface for its MetricType -- type + Enum allow-lists
//       on the FLAT Property surface (Name, SensorGroup, MetricType, MetricName,
//       MountPoint, Interface(s), Source(s), PinId(s), Enabled, Interval,
//       Description, DisplayName, Schema), mapped to nested devicecfg.json paths.
//   (b) the existing config-rules.js (cross-field rules that DTDL can't express).
// Per-fixture verdict prints both layers' opinions and compares the combined
// agent-equivalent verdict (config-rules) against the fixture's `expect` field.

const { createParser, ModelParsingOption } = require('@azure/dtdl-parser');
const fs = require('fs');
const path = require('path');
const { validate: configRulesValidate } = require('./config-rules');

const ROOT = path.resolve(__dirname, '..', '..');
const DTDL_DIR = path.join(ROOT, 'docs', 'Metrics', 'dtdl');
const CFG_DIR  = path.join(ROOT, 'docs', 'Metrics', 'samples-config');

// MetricType -> Interface name suffix (matches the @id's last segment).
const METRIC_TO_IFACE = {
  cpu:               'CpuSensorConfig',
  memory:            'MemorySensorConfig',
  disk:              'DiskSensorConfig',
  network:           'NetworkSensorConfig',
  system:            'SystemSensorConfig',
  gpu:               'GpuSensorConfig',
  hwinfo:            'HwinfoSensorConfig',
  temperature:       'TemperatureSensorConfig',
  voltage:           'VoltageSensorConfig',
  fanspeed:          'FanspeedSensorConfig',
  gpio:              'GpioSensorConfig',
  watchdog:          'WatchdogSensorConfig',
  thermalprotection: 'ThermalProtectionSensorConfig',
};

// Flat Property name -> getter from a Sensor JSON entry.
const PROP_GETTERS = {
  Name:        s => s.Name,
  SensorGroup: s => s.SensorGroup,
  MetricType:  s => s.Parameters?.MetricType,
  MetricName:  s => s.Parameters?.MetricName,
  MountPoint:  s => s.Parameters?.MountPoint,
  Interface:   s => s.Parameters?.Interface,
  Interfaces:  s => s.Parameters?.Interfaces,
  Source:      s => s.Parameters?.Source,
  Sources:     s => s.Parameters?.Sources,
  PinId:       s => s.Parameters?.PinId,
  PinIds:      s => s.Parameters?.PinIds,
  Enabled:     s => s.Report?.Enabled,
  Interval:    s => s.Report?.Interval,
  Description: s => s.SensorInfo?.Description,
  DisplayName: s => s.SensorInfo?.DisplayName,
  Schema:      s => s.SensorInfo?.Schema,
};

// Properties that may legitimately be absent on a Sensor entry.
const OPTIONAL_PROPS = new Set([
  'MountPoint',     // required only by config-rules.js, not by DTDL Property contract
  'Interface', 'Interfaces',
  'Source',  'Sources',
  'PinId',   'PinIds',
]);

function dtKindOf(schema) {
  // Primitive kinds come back as "string" / "integer" / "boolean" / etc.
  // Complex kinds: "enum", "array", "object", "map".
  return schema && schema.entityKind;
}

function checkValue(value, propName, schema) {
  const kind = dtKindOf(schema);
  if (kind === 'string') {
    if (typeof value !== 'string') return `expected string, got ${typeof value}`;
  } else if (kind === 'integer' || kind === 'long') {
    if (typeof value !== 'number' || !Number.isInteger(value)) return `expected integer, got ${JSON.stringify(value)}`;
  } else if (kind === 'double' || kind === 'float') {
    if (typeof value !== 'number') return `expected number, got ${typeof value}`;
  } else if (kind === 'boolean') {
    if (typeof value !== 'boolean') return `expected boolean, got ${typeof value}`;
  } else if (kind === 'enum') {
    const allowed = (schema.enumValues || []).map(e => e.enumValue);
    if (!allowed.includes(value)) return `not in enum [${allowed.join(', ')}]`;
  } else if (kind === 'array') {
    if (!Array.isArray(value)) return `expected array, got ${typeof value}`;
    const elemKind = dtKindOf(schema.elementSchema);
    for (let i = 0; i < value.length; i++) {
      const v = value[i];
      if (elemKind === 'string'  && typeof v !== 'string')   return `element[${i}] not string`;
      if ((elemKind === 'integer' || elemKind === 'long') && (typeof v !== 'number' || !Number.isInteger(v))) return `element[${i}] not integer`;
    }
  } else {
    return `unsupported schema kind ${kind}`;
  }
  return null;
}

async function main() {
  if (!fs.existsSync(DTDL_DIR)) { console.error(`ERR DTDL dir not found: ${DTDL_DIR}`); process.exit(1); }
  if (!fs.existsSync(CFG_DIR))  { console.error(`ERR samples-config dir not found: ${CFG_DIR}`); process.exit(1); }

  const dtdlFiles = fs.readdirSync(DTDL_DIR).filter(f => f.endsWith('.json')).sort();
  const jsons = dtdlFiles.map(f => fs.readFileSync(path.join(DTDL_DIR, f), 'utf8'));
  const parser = createParser(ModelParsingOption.PermitAnyTopLevelElement);
  const model = await parser.parse(jsons);
  const interfaces = Object.fromEntries(
    Object.entries(model)
      .filter(([, e]) => e.entityKind === 'interface')
      .map(([id, e]) => [id.split(':').pop().split(';')[0], e])
  );
  console.log(`Loaded ${Object.keys(interfaces).length} Interfaces from ${DTDL_DIR}`);

  const fixtures = fs.readdirSync(CFG_DIR).filter(f => f.endsWith('.json')).sort()
    .map(f => ({ file: f, sensors: JSON.parse(fs.readFileSync(path.join(CFG_DIR, f), 'utf8')) }));

  let total = 0, mismatches = 0;
  let dtdlPassCount = 0, rulesPassCount = 0;
  const divergences = [];

  for (const { file, sensors } of fixtures) {
    console.log(`\n--- ${file} (${sensors.length} sensors) ---`);
    for (const s of sensors) {
      total++;
      const expected = s.expect === 'valid';
      const mt = s.Parameters?.MetricType;
      const ifaceName = METRIC_TO_IFACE[mt];
      const iface = ifaceName && interfaces[ifaceName];

      let dtdlOk = true;
      let dtdlReason = '';

      if (!iface) {
        dtdlOk = false;
        dtdlReason = `no Interface found for MetricType '${mt}'`;
      } else {
        for (const [pname, pinfo] of Object.entries(iface.contents || {})) {
          if (pinfo.entityKind !== 'property') continue;
          const getter = PROP_GETTERS[pname];
          if (!getter) { dtdlOk = false; dtdlReason = `no getter for Property '${pname}'`; break; }
          const value = getter(s);
          if (value === undefined || value === null) {
            if (OPTIONAL_PROPS.has(pname)) continue;
            dtdlOk = false; dtdlReason = `Property '${pname}' missing`; break;
          }
          const err = checkValue(value, pname, pinfo.schema);
          if (err) { dtdlOk = false; dtdlReason = `Property '${pname}': ${err}`; break; }
        }
      }
      if (dtdlOk) dtdlPassCount++;

      let rulesResult;
      try {
        rulesResult = configRulesValidate(s);
      } catch (e) {
        rulesResult = { ok: false, reason: `rule threw: ${e.message}` };
      }
      if (rulesResult.ok) rulesPassCount++;

      // The agent contract = config-rules. DTDL-native may be stricter (e.g.
      // CSV string for Interfaces is wire-tolerated by the agent but rejected
      // by the DTDL Array<string> schema). We declare the fixture verdict by
      // config-rules (matches the existing validator), but flag DTDL divergence.
      const agentOk = rulesResult.ok;
      const verdictOk = agentOk === expected;
      const label = (s.expect || '?').padEnd(7);
      const dtdlMark = dtdlOk ? 'D+' : 'D-';
      const rulesMark = rulesResult.ok ? 'R+' : 'R-';

      const line = `  ${verdictOk ? 'OK ' : 'ERR'} ${label} ${dtdlMark} ${rulesMark}  ${s.Name || '<unnamed>'}`;
      if (verdictOk) {
        console.log(line);
      } else {
        const got = agentOk ? 'valid' : 'invalid';
        const rr  = rulesResult.reason ? `: ${rulesResult.reason}` : '';
        console.error(`${line}    (agent verdict: ${got}${rr})`);
        mismatches++;
      }

      if (dtdlOk !== agentOk) {
        divergences.push({
          file, name: s.Name, expect: s.expect,
          dtdl: dtdlOk ? 'pass' : `fail (${dtdlReason})`,
          rules: rulesResult.ok ? 'pass' : `fail (${rulesResult.reason || ''})`,
        });
      }
    }
  }

  console.log('');
  console.log(`Total sensors:              ${total}`);
  console.log(`DTDL-native pass:           ${dtdlPassCount}/${total}`);
  console.log(`config-rules pass:          ${rulesPassCount}/${total}`);
  console.log(`Fixture verdict mismatches: ${mismatches}`);
  console.log('');

  if (divergences.length) {
    console.log(`Layer divergences (${divergences.length}):  DTDL strict vs config-rules tolerant.`);
    console.log('  These are EXPECTED for fixtures that exercise wire-format tolerances (CSV interface lists, etc.).');
    console.log('  Listed below for transparency:');
    for (const d of divergences) {
      console.log(`    [${d.file}] ${d.name}  expect=${d.expect}  dtdl=${d.dtdl}  rules=${d.rules}`);
    }
  }

  process.exit(mismatches === 0 ? 0 : 1);
}

main().catch(e => { console.error('fatal:', e.message); process.exit(2); });
