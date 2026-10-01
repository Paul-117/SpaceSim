(() => {
  const protocolVersion = 9;
  const slider = document.querySelector('#operating-level');
  const canvas = document.querySelector('#reactor-canvas');
  const context = canvas.getContext('2d');
  let socket;
  let wavePhase = 0;
  let lastFrame = performance.now();
  let state = { targetOperatingLevelPercent: 0, operatingLevelPercent: 0, outputPower: 0,
    maximumOutputPower: 125, currentDraw: 0, fuel: 100, fuelCapacity: 100,
    fuelUsagePerMinute: 0, bridgePercent: 40, shieldsPercent: 28, armariumPercent: 32,
    bridgePower: 0, shieldsPower: 0, armariumPower: 0,
    bridgeMaximumPower: 50, shieldsMaximumPower: 35, armariumMaximumPower: 40, simulationTick: 0 };
  const allocations = [
    { key: 'bridge', slider: document.querySelector('#bridge-allocation'), value: document.querySelector('#bridge-allocation-value'), fill: document.querySelector('#bridge-allocation-fill') },
    { key: 'shields', slider: document.querySelector('#shields-allocation'), value: document.querySelector('#shields-allocation-value'), fill: document.querySelector('#shields-allocation-fill') },
    { key: 'armarium', slider: document.querySelector('#armarium-allocation'), value: document.querySelector('#armarium-allocation-value'), fill: document.querySelector('#armarium-allocation-fill') }
  ];

  function fuelFraction() { return Math.max(0, state.fuel / Math.max(1, state.fuelCapacity)); }
  function status() {
    if (state.fuel <= 0) return 'FUEL DEPLETED';
    if (state.fuel <= state.fuelCapacity * .15) return 'FUEL LOW';
    if (state.operatingLevelPercent >= 85) return 'HIGH OUTPUT';
    return state.operatingLevelPercent <= 0 ? 'REACTOR IDLE' : 'NOMINAL';
  }
  function renderTelemetry() {
    const fuelPercent = fuelFraction() * 100;
    const lowFuel = fuelPercent <= 15;
    slider.value = state.targetOperatingLevelPercent;
    slider.disabled = state.fuel <= 0;
    document.querySelector('#level-output').textContent = `${state.targetOperatingLevelPercent.toFixed(0)} %`;
    document.querySelector('#level-label').textContent = `LEVEL ${state.operatingLevelPercent.toFixed(0)} %`;
    document.querySelector('#fuel-value').textContent = `${state.fuel.toFixed(1)} / ${state.fuelCapacity.toFixed(0)} U`;
    document.querySelector('#usage-value').textContent = `${state.fuelUsagePerMinute.toFixed(1)} U / MIN`;
    document.querySelector('#power-value').textContent = `${state.outputPower.toFixed(1)} / ${state.maximumOutputPower.toFixed(0)} PU`;
    document.querySelector('#draw-value').textContent = `${state.currentDraw.toFixed(1)} PU`;
    document.querySelector('#tick-label').textContent = `TICK ${String(state.simulationTick).padStart(6, '0')}`;
    document.querySelector('#reactor-status').textContent = status();
    document.querySelector('#command-state').textContent = state.fuel <= 0 ? 'REACTOR SHUT DOWN' : state.operatingLevelPercent === 0 ? 'REACTOR IDLE' : 'OUTPUT STABLE';
    document.querySelector('#capacity-label').textContent = `FUEL CAPACITY ${state.fuelCapacity.toFixed(0)} U`;
    const fuelFill = document.querySelector('#fuel-fill');
    fuelFill.style.width = `${fuelPercent}%`;
    fuelFill.className = state.fuel <= 0 ? 'empty' : lowFuel ? 'caution' : '';
    const dot = document.querySelector('#status-dot');
    dot.className = `status-dot${state.fuel <= 0 ? ' empty' : lowFuel || state.operatingLevelPercent >= 85 ? ' caution' : ''}`;
    document.querySelector('#level-fill').style.width = `${state.operatingLevelPercent}%`;
    document.querySelector('#level-indicator').style.left = `${state.operatingLevelPercent}%`;
    renderAllocations();
  }
  function allocationPercent(entry) { return Number(state[`${entry.key}Percent`]) || 0; }
  function allocationPower(entry) { return Number(state[`${entry.key}Power`]) || 0; }
  function allocationMaximum(entry) { return Number(state[`${entry.key}MaximumPower`]) || 0; }
  function allocationLimitPercent(entry) {
    if (state.outputPower <= 0) return 100;
    return Math.min(100, allocationMaximum(entry) / state.outputPower * 100);
  }
  function renderAllocations() {
    let total = 0;
    allocations.forEach(entry => {
      const percent = allocationPercent(entry);
      const limit = allocationLimitPercent(entry);
      total += percent;
      entry.slider.max = String(Math.max(0, Math.floor(limit)));
      entry.slider.value = String(percent);
      entry.fill.style.width = `${limit <= 0 ? 0 : Math.min(1, percent / limit) * 100}%`;
      entry.value.textContent = `${allocationPower(entry).toFixed(1)} / ${allocationMaximum(entry).toFixed(0)} PU`;
      entry.slider.disabled = state.outputPower <= 0;
    });
    document.querySelector('#allocation-total').textContent = `TOTAL ${total.toFixed(0)} %`;
  }
  function drawReactor() {
    const bounds = canvas.getBoundingClientRect();
    const ratio = window.devicePixelRatio || 1;
    const width = Math.max(1, Math.round(bounds.width * ratio));
    const height = Math.max(1, Math.round(bounds.height * ratio));
    if (canvas.width !== width || canvas.height !== height) { canvas.width = width; canvas.height = height; }
    context.setTransform(ratio, 0, 0, ratio, 0, 0);
    const size = Math.min(bounds.width, bounds.height);
    const center = size / 2;
    const outputFraction = state.outputPower / Math.max(1, state.maximumOutputPower);
    context.clearRect(0, 0, bounds.width, bounds.height);
    context.fillStyle = '#151b1e'; context.fillRect(0, 0, bounds.width, bounds.height);
    context.strokeStyle = '#354247'; context.lineWidth = 1;
    for (let i = 0; i < 8; i += 1) { context.beginPath(); context.arc(center, center, size * (.13 + i * .047), 0, Math.PI * 2); context.stroke(); }
    for (let angle = 0; angle < 360; angle += 30) { const radians = angle * Math.PI / 180; context.beginPath(); context.moveTo(center + Math.cos(radians) * size * .12, center + Math.sin(radians) * size * .12); context.lineTo(center + Math.cos(radians) * size * .47, center + Math.sin(radians) * size * .47); context.stroke(); }
    drawCoreMass(center, size, outputFraction);
  }
  function drawCoreMass(center, size, outputFraction) {
    if (outputFraction <= 0) return;
    const baseRadius = size * (.065 + outputFraction * .205);
    const wobble = size * (.006 + outputFraction * .022);
    [{ scale: 1.2, color: '#267254', alpha: .28 }, { scale: .96, color: '#3ca96f', alpha: .48 }, { scale: .7, color: '#6ed391', alpha: .72 }].forEach((layer, layerIndex) => {
      context.beginPath();
      for (let point = 0; point <= 72; point += 1) {
        const angle = point / 72 * Math.PI * 2;
        const wave = Math.sin(angle * 3 + wavePhase * (1.15 + layerIndex * .18)) + Math.sin(angle * 5 - wavePhase * .72 + layerIndex) * .45 + Math.sin(angle * 8 + wavePhase * .43) * .2;
        const radius = baseRadius * layer.scale + wave * wobble * layer.scale;
        const x = center + Math.cos(angle) * radius, y = center + Math.sin(angle) * radius;
        if (point === 0) context.moveTo(x, y); else context.lineTo(x, y);
      }
      context.closePath(); context.globalAlpha = layer.alpha; context.fillStyle = layer.color; context.fill();
    });
    context.globalAlpha = 1;
  }
  function animate(timestamp) {
    const elapsedSeconds = Math.min(.1, (timestamp - lastFrame) / 1000);
    wavePhase += elapsedSeconds * (1.1 + state.operatingLevelPercent / 100 * 2.4);
    lastFrame = timestamp; drawReactor(); requestAnimationFrame(animate);
  }
  function setConnection(online) {
    const statusNode = document.querySelector('#reactor-status');
    if (!online) { statusNode.textContent = 'OFFLINE'; document.querySelector('#status-dot').className = 'status-dot offline'; }
  }
  function connect() {
    const scheme = location.protocol === 'https:' ? 'wss' : 'ws';
    socket = new WebSocket(`${scheme}://${location.host}/station`);
    socket.onopen = () => socket.send(JSON.stringify({ type: 'hello', station: 'reactorium', protocolVersion }));
    socket.onmessage = event => { const message = JSON.parse(event.data); if (message.type === 'reactorium_state') { state = message; renderTelemetry(); } if (message.type === 'error') setConnection(false); };
    socket.onclose = () => { setConnection(false); setTimeout(connect, 1500); };
    socket.onerror = () => socket.close();
  }
  slider.addEventListener('input', () => {
    if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify({ type: 'reactor_level', levelPercent: Number(slider.value) }));
  });
  allocations.forEach(entry => entry.slider.addEventListener('input', () => {
    const requested = Number(entry.slider.value);
    const otherTotal = allocations.filter(other => other !== entry).reduce((sum, other) => sum + allocationPercent(other), 0);
    const accepted = Math.max(0, Math.min(requested, allocationLimitPercent(entry), 100 - otherTotal));
    entry.slider.value = String(accepted);
    state[`${entry.key}Percent`] = accepted;
    renderAllocations();
    if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify({
      type: 'power_allocation', bridgePercent: allocationPercent(allocations[0]),
      shieldsPercent: allocationPercent(allocations[1]), armariumPercent: allocationPercent(allocations[2])
    }));
  }));
  document.addEventListener('keydown', event => {
    if (slider.disabled || (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight')) return;
    event.preventDefault(); slider.value = Math.max(0, Math.min(100, Number(slider.value) + (event.key === 'ArrowRight' ? 5 : -5))); slider.dispatchEvent(new Event('input'));
  });
  renderTelemetry(); requestAnimationFrame(animate); connect();
})();
