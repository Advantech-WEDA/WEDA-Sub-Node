// Sensor-config shape rules.
//
// Each rule reads a Sensor entry (devicecfg.json Sensors[] element) and
// returns either:
//   { ok: true }                 -- the sensor satisfies this rule
//   { ok: false, reason: '...' } -- the sensor violates this rule (text shown
//                                   to operators / CI logs)
//
// Rules are grouped by Parameters.MetricType. A sensor of a type with no
// registered rule set passes through unchecked. Add new (MetricType) ->
// [rule, rule, ...] entries as new validation needs land.

function isStringArray(v) {
  return Array.isArray(v) && v.every(x => typeof x === 'string');
}

function isStringOrStringArray(v) {
  return typeof v === 'string' || isStringArray(v);
}

const rules = {
  // ============================================================
  // network -- Parameters.Interface / Parameters.Interfaces
  //
  // Covers the v1.0/v1.1 expansion contract documented in
  // docs/Metrics/01_CPU_NETWORK_FIELDS.md:
  //   - v1.0: single 'Interface' (string) required.
  //   - v1.1: 'Interfaces' (string array or CSV string); empty value /
  //           omitted both -> auto-detect.
  //   - 'Interface' and 'Interfaces' MUST NOT both be set (ambiguous).
  // ============================================================
  network: [
    // Interfaces, when present, must be string OR string[].
    sensor => {
      const ifaces = sensor.Parameters?.Interfaces;
      if (ifaces === undefined) return { ok: true };
      if (isStringOrStringArray(ifaces)) return { ok: true };
      return {
        ok: false,
        reason: `Parameters.Interfaces must be string or array<string>; got ${Array.isArray(ifaces) ? 'array with non-string element' : typeof ifaces}`,
      };
    },

    // Interface (singular), when present, must be a non-empty string.
    sensor => {
      const iface = sensor.Parameters?.Interface;
      if (iface === undefined) return { ok: true };
      if (typeof iface === 'string' && iface.length > 0) return { ok: true };
      return {
        ok: false,
        reason: `Parameters.Interface must be a non-empty string; got ${typeof iface === 'string' ? '""' : typeof iface}`,
      };
    },

    // Mutual exclusivity: setting both 'Interface' and a non-empty
    // 'Interfaces' is ambiguous (runtime picks Interface, but the config
    // can't express intent clearly).
    sensor => {
      const iface = sensor.Parameters?.Interface;
      const ifaces = sensor.Parameters?.Interfaces;
      const hasIface = typeof iface === 'string' && iface.length > 0;
      const hasIfaces =
        ifaces !== undefined &&
        ifaces !== '' &&
        !(Array.isArray(ifaces) && ifaces.length === 0);
      if (hasIface && hasIfaces) {
        return {
          ok: false,
          reason: "Parameters.Interface and Parameters.Interfaces both set; runtime would pick 'Interface' but the config is ambiguous",
        };
      }
      return { ok: true };
    },
  ],

  // Extension points (left empty -- add rules when needed):
  //   disk:        [/* require Parameters.MountPoint */],
  //   temperature: [/* Source / Sources / MetricName tri-mode check */],
  //   gpio:        [/* PinId / PinIds for pinState */],
};

function validate(sensor) {
  const mt = sensor.Parameters?.MetricType;
  const ruleSet = rules[mt];
  if (!ruleSet) return { ok: true, skipped: true };
  for (const r of ruleSet) {
    const res = r(sensor);
    if (!res.ok) return res;
  }
  return { ok: true };
}

module.exports = { validate, rules };
