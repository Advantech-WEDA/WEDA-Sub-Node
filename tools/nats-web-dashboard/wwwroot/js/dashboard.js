// --- State ---
const state = {
  totalMsg: 0,
  telemetryCount: 0,
  healthCount: 0,
  sensors: {},
  history: {},
  logs: [],
  msgTimestamps: [],
  startTime: Date.now(),
  selectedSensor: '__all__',
};
const MAX_HISTORY = 120;
const MAX_LOGS = 50;
const COLORS = ['#4f8ff7', '#22c55e', '#eab308', '#ef4444', '#06b6d4', '#a855f7'];

// --- SignalR Connection ---
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hub')
  .withAutomaticReconnect()
  .build();

connection.onreconnecting(() => {
  document.getElementById('statusDot').className = 'status-dot disconnected';
  document.getElementById('statusText').textContent = 'Reconnecting...';
});

connection.onreconnected(() => {
  document.getElementById('statusDot').className = 'status-dot connected';
  document.getElementById('statusText').textContent = 'Connected';
});

connection.onclose(() => {
  document.getElementById('statusDot').className = 'status-dot disconnected';
  document.getElementById('statusText').textContent = 'Disconnected';
});

connection.on('NatsMessage', (subject, timestamp, payloadStr) => {
  const payload = tryParse(payloadStr);

  state.totalMsg++;
  state.msgTimestamps.push(Date.now());

  const msgType = classifyMessage(subject);

  if (msgType === 'telemetry' && payload?.data?.measures) {
    state.telemetryCount++;
    for (const m of payload.data.measures) {
      if (!state.sensors[m.sensorId]) {
        state.sensors[m.sensorId] = { min: Infinity, max: -Infinity, sum: 0, count: 0 };
        state.history[m.sensorId] = [];
      }
      const s = state.sensors[m.sensorId];
      s.current = m.value;
      s.min = Math.min(s.min, m.value);
      s.max = Math.max(s.max, m.value);
      s.sum += m.value;
      s.count++;
      s.avg = s.sum / s.count;
      state.history[m.sensorId].push(m.value);
      if (state.history[m.sensorId].length > MAX_HISTORY)
        state.history[m.sensorId].shift();
    }
  } else if (msgType === 'health' && payload?.data) {
    state.healthCount++;
    state.health = payload.data;
  }

  state.logs.unshift({ time: new Date(), type: msgType, subject, payload: payloadStr });
  if (state.logs.length > MAX_LOGS) state.logs.pop();

  render();
});

async function start() {
  try {
    await connection.start();
    document.getElementById('statusDot').className = 'status-dot connected';
    document.getElementById('statusText').textContent = 'Connected';
  } catch (err) {
    document.getElementById('statusText').textContent = 'Connection failed';
    setTimeout(start, 3000);
  }
}

start();

// --- Helpers ---
function tryParse(s) {
  try { return JSON.parse(s); } catch { return null; }
}

function classifyMessage(subject) {
  if (subject.includes('telemetry')) return 'telemetry';
  if (subject.includes('health')) return 'health';
  if (subject.includes('shadow')) return 'config';
  if (subject.includes('cmd')) return 'command';
  return 'other';
}

// --- Rendering ---
function render() {
  document.getElementById('totalMsg').textContent = state.totalMsg;
  document.getElementById('telemetryCount').textContent = state.telemetryCount;
  document.getElementById('healthCount').textContent = state.healthCount;
  document.getElementById('lastTime').textContent = new Date().toLocaleTimeString();

  const now = Date.now();
  state.msgTimestamps = state.msgTimestamps.filter(t => now - t < 5000);
  document.getElementById('msgRate').textContent = (state.msgTimestamps.length / 5).toFixed(1);

  renderSensors();
  renderHealth();
  renderChart();
  renderLogs();
}

function renderSensors() {
  const grid = document.getElementById('sensorGrid');
  const ids = Object.keys(state.sensors);
  if (ids.length === 0) return;

  grid.innerHTML = ids.map((id, i) => {
    const s = state.sensors[id];
    const color = COLORS[i % COLORS.length];
    return `<div class="sensor-item" style="border-left-color:${color}">
      <div class="sensor-name">${id}</div>
      <div class="sensor-value">${s.current.toFixed(3)}</div>
      <div class="sensor-stats">
        <span>Min: ${s.min.toFixed(2)}</span>
        <span>Max: ${s.max.toFixed(2)}</span>
        <span>Avg: ${s.avg.toFixed(2)}</span>
        <span>#${s.count}</span>
      </div>
    </div>`;
  }).join('');
}

function renderHealth() {
  if (!state.health) return;
  const h = state.health;

  document.getElementById('healthStatus').textContent = h.isHealthy ? 'Healthy' : 'Unhealthy';
  document.getElementById('healthStatus').style.color = h.isHealthy ? 'var(--green)' : 'var(--red)';

  const cpu = (h.cpuUsage || 0).toFixed(1);
  document.getElementById('cpuValue').textContent = cpu + '%';
  document.getElementById('cpuBar').style.width = cpu + '%';
  document.getElementById('cpuBar').style.background =
    cpu < 50 ? 'var(--green)' : cpu < 80 ? 'var(--yellow)' : 'var(--red)';

  const mem = (h.memoryUsage || 0).toFixed(1);
  document.getElementById('memValue').textContent = mem + '%';
  document.getElementById('memBar').style.width = mem + '%';
  document.getElementById('memBar').style.background =
    mem < 50 ? 'var(--green)' : mem < 80 ? 'var(--yellow)' : 'var(--red)';

  const uptime = Math.floor((Date.now() - state.startTime) / 1000);
  const m = Math.floor(uptime / 60), s = uptime % 60;
  document.getElementById('uptimeValue').textContent = `${m}m ${s}s`;
  document.getElementById('uptimeValue').style.color = 'var(--text)';
}

function renderLogs() {
  const container = document.getElementById('logContainer');
  container.innerHTML = state.logs.map(l => {
    const time = l.time.toLocaleTimeString();
    const shortSubject = l.subject.split('.').slice(-2).join('.');
    const shortPayload = l.payload.length > 120 ? l.payload.substring(0, 117) + '...' : l.payload;
    return `<div class="log-entry">
      <span class="log-time">${time}</span>
      <span class="log-type ${l.type}">${l.type}</span>
      <span class="log-subject" title="${l.subject}">${shortSubject}</span>
      <span class="log-payload">${escapeHtml(shortPayload)}</span>
    </div>`;
  }).join('');
}

function escapeHtml(s) {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// --- Chart Tabs ---
function renderChartTabs() {
  const tabs = document.getElementById('chartTabs');
  const ids = Object.keys(state.history);
  if (ids.length === 0) return;

  const tabKey = ['__all__', ...ids].join(',');
  if (tabs.dataset.key === tabKey) {
    tabs.querySelectorAll('.chart-tab').forEach(t => {
      t.classList.toggle('active', t.dataset.sensor === state.selectedSensor);
    });
    return;
  }
  tabs.dataset.key = tabKey;

  let html = `<span class="chart-tab all ${state.selectedSensor === '__all__' ? 'active' : ''}" data-sensor="__all__">All</span>`;
  ids.forEach((id, i) => {
    const color = COLORS[i % COLORS.length];
    const isActive = state.selectedSensor === id;
    html += `<span class="chart-tab ${isActive ? 'active' : ''}" data-sensor="${id}" style="${isActive ? '' : `color:${color};border-color:${color}`}">${id}</span>`;
  });
  tabs.innerHTML = html;
  tabs.querySelectorAll('.chart-tab').forEach(t => {
    t.addEventListener('click', () => {
      state.selectedSensor = t.dataset.sensor;
      render();
    });
  });
}

// --- Canvas Chart ---
function renderChart() {
  renderChartTabs();

  const canvas = document.getElementById('chart');
  const ctx = canvas.getContext('2d');
  const dpr = window.devicePixelRatio || 1;
  const rect = canvas.parentElement.getBoundingClientRect();
  canvas.width = rect.width * dpr;
  canvas.height = rect.height * dpr;
  ctx.scale(dpr, dpr);
  const W = rect.width, H = rect.height;

  ctx.clearRect(0, 0, W, H);

  const allIds = Object.keys(state.history);
  const visibleSeries = state.selectedSensor === '__all__'
    ? allIds.map((id, i) => [id, state.history[id], i])
    : allIds.filter(id => id === state.selectedSensor).map(id => [id, state.history[id], allIds.indexOf(id)]);

  if (visibleSeries.length === 0) return;

  let gMin = Infinity, gMax = -Infinity;
  for (const [, vals] of visibleSeries) {
    for (const v of vals) { gMin = Math.min(gMin, v); gMax = Math.max(gMax, v); }
  }
  const padding = (gMax - gMin) * 0.1 || 1;
  gMin -= padding;
  gMax += padding;

  const marginL = 50, marginR = 10, marginT = 10, marginB = 25;
  const plotW = W - marginL - marginR;
  const plotH = H - marginT - marginB;

  ctx.strokeStyle = '#2a2d3a';
  ctx.lineWidth = 0.5;
  ctx.font = '10px monospace';
  ctx.fillStyle = '#8b8fa3';
  for (let i = 0; i <= 4; i++) {
    const y = marginT + (plotH / 4) * i;
    ctx.beginPath();
    ctx.moveTo(marginL, y);
    ctx.lineTo(W - marginR, y);
    ctx.stroke();
    const val = gMax - ((gMax - gMin) / 4) * i;
    ctx.fillText(val.toFixed(1), 4, y + 4);
  }

  visibleSeries.forEach(([id, vals, colorIdx]) => {
    if (vals.length < 2) return;
    const color = COLORS[colorIdx % COLORS.length];
    ctx.strokeStyle = color;
    ctx.lineWidth = 2;
    ctx.beginPath();
    for (let i = 0; i < vals.length; i++) {
      const x = marginL + (i / (MAX_HISTORY - 1)) * plotW;
      const y = marginT + (1 - (vals[i] - gMin) / (gMax - gMin)) * plotH;
      i === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y);
    }
    ctx.stroke();

    const lastX = marginL + ((vals.length - 1) / (MAX_HISTORY - 1)) * plotW;
    const lastY = marginT + (1 - (vals[vals.length - 1] - gMin) / (gMax - gMin)) * plotH;
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.arc(lastX, lastY, 4, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = color;
    ctx.font = '11px monospace';
    ctx.fillText(`${id}: ${vals[vals.length - 1].toFixed(2)}`, lastX + 8, lastY + 4);
  });
}

window.addEventListener('resize', render);
