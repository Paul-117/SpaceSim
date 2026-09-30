(() => {
  const FUEL_CAPACITY = 100;
  const MAX_OUTPUT_POWER = 125;
  const slider = document.getElementById("operating-level");
  const canvas = document.getElementById("reactor-canvas");
  const context = canvas.getContext("2d");
  const RAMP_LEVELS_PER_SECOND = 100 / 60;
  const state = {
    fuel: FUEL_CAPACITY,
    targetLevel: Number(slider.value),
    actualLevel: Number(slider.value),
    tick: 0,
    drawFactor: .77,
    wavePhase: 0
  };
  let lastFrame = performance.now();

  function outputPower() {
    return state.fuel <= 0 ? 0 : state.actualLevel / 100 * MAX_OUTPUT_POWER;
  }

  function fuelUsagePerMinute() {
    const level = state.actualLevel / 100;
    return state.fuel <= 0 || state.actualLevel === 0 ? 0 : .2 + 6.8 * level * level;
  }

  function currentDraw() {
    return outputPower() * state.drawFactor;
  }

  function reactorStatus() {
    if (state.fuel <= 0) return "FUEL DEPLETED";
    if (state.fuel <= 15) return "FUEL LOW";
    if (state.actualLevel >= 85) return "HIGH OUTPUT";
    return "NOMINAL";
  }

  function render() {
    const output = outputPower();
    const usage = fuelUsagePerMinute();
    const fuelPercent = Math.max(0, state.fuel / FUEL_CAPACITY * 100);
    const lowFuel = state.fuel <= 15;
    document.getElementById("level-output").textContent = `${state.targetLevel} %`;
    document.getElementById("level-label").textContent = `LEVEL ${Math.round(state.actualLevel)} %`;
    document.getElementById("fuel-value").textContent = `${state.fuel.toFixed(1)} U`;
    document.getElementById("usage-value").textContent = `${usage.toFixed(1)} U / MIN`;
    document.getElementById("power-value").textContent = `${output.toFixed(1)} PU`;
    document.getElementById("draw-value").textContent = `${currentDraw().toFixed(1)} PU`;
    document.getElementById("tick-label").textContent = `TICK ${String(Math.floor(state.tick)).padStart(6, "0")}`;
    document.getElementById("reactor-status").textContent = reactorStatus();
    document.getElementById("command-state").textContent = output === 0 ? "REACTOR IDLE" : "OUTPUT STABLE";
    const fuelFill = document.getElementById("fuel-fill");
    fuelFill.style.width = `${fuelPercent}%`;
    fuelFill.className = state.fuel <= 0 ? "empty" : lowFuel ? "caution" : "";
    const dot = document.getElementById("status-dot");
    dot.className = `status-dot${state.fuel <= 0 ? " empty" : lowFuel || state.actualLevel >= 85 ? " caution" : ""}`;
    document.getElementById("level-fill").style.width = `${state.actualLevel}%`;
    document.getElementById("level-indicator").style.left = `${state.actualLevel}%`;
    slider.disabled = state.fuel <= 0;
    drawReactor();
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
    const outputFraction = outputPower() / MAX_OUTPUT_POWER;
    context.clearRect(0, 0, bounds.width, bounds.height);
    context.fillStyle = "#151b1e";
    context.fillRect(0, 0, bounds.width, bounds.height);
    context.strokeStyle = "#354247";
    context.lineWidth = 1;
    for (let i = 0; i < 8; i += 1) {
      context.beginPath();
      context.arc(center, center, size * (.13 + i * .047), 0, Math.PI * 2);
      context.stroke();
    }
    for (let angle = 0; angle < 360; angle += 30) {
      const radians = angle * Math.PI / 180;
      context.beginPath();
      context.moveTo(center + Math.cos(radians) * size * .12, center + Math.sin(radians) * size * .12);
      context.lineTo(center + Math.cos(radians) * size * .47, center + Math.sin(radians) * size * .47);
      context.stroke();
    }
    drawCoreMass(center, size, outputFraction);
  }

  function drawCoreMass(center, size, outputFraction) {
    if (outputFraction <= 0) return;
    const baseRadius = size * (.065 + outputFraction * .205);
    const wobble = size * (.006 + outputFraction * .022);
    const layers = [
      { scale: 1.2, color: "#267254", alpha: .28 },
      { scale: .96, color: "#3ca96f", alpha: .48 },
      { scale: .7, color: "#6ed391", alpha: .72 }
    ];
    layers.forEach((layer, layerIndex) => {
      context.beginPath();
      for (let point = 0; point <= 72; point += 1) {
        const angle = point / 72 * Math.PI * 2;
        const wave = Math.sin(angle * 3 + state.wavePhase * (1.15 + layerIndex * .18))
          + Math.sin(angle * 5 - state.wavePhase * .72 + layerIndex) * .45
          + Math.sin(angle * 8 + state.wavePhase * .43) * .2;
        const radius = baseRadius * layer.scale + wave * wobble * layer.scale;
        const x = center + Math.cos(angle) * radius;
        const y = center + Math.sin(angle) * radius;
        if (point === 0) context.moveTo(x, y); else context.lineTo(x, y);
      }
      context.closePath();
      context.globalAlpha = layer.alpha;
      context.fillStyle = layer.color;
      context.fill();
    });
    context.globalAlpha = 1;
  }

  function simulate(timestamp) {
    const elapsedSeconds = Math.min(.1, (timestamp - lastFrame) / 1000);
    const difference = state.targetLevel - state.actualLevel;
    const step = RAMP_LEVELS_PER_SECOND * elapsedSeconds;
    state.actualLevel += Math.sign(difference) * Math.min(Math.abs(difference), step);
    state.fuel = Math.max(0, state.fuel - fuelUsagePerMinute() / 60 * elapsedSeconds);
    state.drawFactor = .68 + Math.sin(timestamp / 1200) * .09 + Math.sin(timestamp / 390) * .025;
    state.tick += elapsedSeconds * 30;
    state.wavePhase += elapsedSeconds * (1.1 + state.actualLevel / 100 * 2.4);
    lastFrame = timestamp;
    render();
    requestAnimationFrame(simulate);
  }

  function setTargetLevel(nextLevel) {
    const roundedLevel = Math.round(nextLevel / 5) * 5;
    state.targetLevel = Math.max(0, Math.min(100, roundedLevel));
    slider.value = state.targetLevel;
    render();
  }

  slider.addEventListener("input", () => {
    setTargetLevel(Number(slider.value));
  });
  document.addEventListener("keydown", event => {
    if (slider.disabled || (event.key !== "ArrowLeft" && event.key !== "ArrowRight")) return;
    event.preventDefault();
    setTargetLevel(state.targetLevel + (event.key === "ArrowRight" ? 5 : -5));
  });
  window.addEventListener("resize", drawReactor);
  render();
  requestAnimationFrame(simulate);
})();
