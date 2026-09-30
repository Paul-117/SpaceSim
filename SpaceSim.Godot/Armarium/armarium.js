(() => {
  const protocolVersion = 6;
  const visibleBearingDegrees = 7.5;
  const tacticalMapRadiusMeters = 1000;
  const tacticalMapHalfAngleDegrees = 30;
  const turretLimitDegrees = 5;
  const reconnectDelayMs = 1500;
  const canvas = document.getElementById("scope");
  const context = canvas.getContext("2d");
  const tacticalMap = document.getElementById("tactical-map");
  const tacticalMapContext = tacticalMap.getContext("2d");
  const connection = document.getElementById("connection");
  const targetStatus = document.getElementById("target-status");
  const lanceLabel = document.getElementById("lance-label");
  const chargeFill = document.getElementById("charge-fill");
  const turretStatus = document.getElementById("turret-status");
  const mapStatus = document.getElementById("map-status");
  let socket = null;
  let heldTurretDirection = null;
  let state = { targetAvailable: false, targetBearingDegrees: 0, targetDistanceMeters: 0, lanceCharge: 0, lanceReady: false, lanceTurretAngleDegrees: 0, targetHitSequence: 0, lastTargetHitBearingDegrees: 0 };
  let displayedTargetBearing = 0;
  let displayedTurretAngle = 0;
  let lastTargetHitSequence = null;
  let hitFlashUntil = 0;
  let hitFlashBearing = 0;
  let lastAnimationTime = performance.now();

  function setConnection(connected) {
    connection.textContent = connected ? "ARMARIUM ONLINE" : "ARMARIUM OFFLINE";
    connection.className = `connection ${connected ? "connected" : "disconnected"}`;
  }
  function resizeCanvas(element, drawingContext) {
    const bounds = element.getBoundingClientRect(); const ratio = window.devicePixelRatio || 1;
    element.width = Math.round(bounds.width * ratio); element.height = Math.round(bounds.height * ratio);
    drawingContext.setTransform(ratio, 0, 0, ratio, 0, 0);
  }
  function resizeCanvases() {
    resizeCanvas(canvas, context); resizeCanvas(tacticalMap, tacticalMapContext); draw();
  }
  function animate(now) {
    const deltaSeconds = Math.min(.1, (now - lastAnimationTime) / 1000);
    lastAnimationTime = now;
    const smoothing = 1 - Math.exp(-18 * deltaSeconds);
    if (state.targetAvailable) displayedTargetBearing += (state.targetBearingDegrees - displayedTargetBearing) * smoothing;
    displayedTurretAngle += (state.lanceTurretAngleDegrees - displayedTurretAngle) * smoothing;
    draw();
    requestAnimationFrame(animate);
  }
  function draw() {
    const width = canvas.clientWidth, height = canvas.clientHeight, center = width / 2, y = height * .64;
    context.clearRect(0, 0, width, height); context.strokeStyle = "#91aabc"; context.fillStyle = "#91aabc";
    context.lineWidth = 1; context.beginPath(); context.moveTo(28, y); context.lineTo(width - 28, y); context.stroke();
    [-visibleBearingDegrees, -visibleBearingDegrees / 2, 0, visibleBearingDegrees / 2, visibleBearingDegrees].forEach(degrees => {
      const x = center + degrees / visibleBearingDegrees * (width / 2 - 28);
      context.beginPath(); context.moveTo(x, y - 9); context.lineTo(x, y + 9); context.stroke();
      context.font = "12px monospace"; context.textAlign = "center"; context.fillText(degrees > 0 ? `+${degrees}°` : `${degrees}°`, x, y + 31);
    });
    context.strokeStyle = "#dceaf1"; context.lineWidth = 2; context.beginPath(); context.moveTo(center, y - 42); context.lineTo(center, y + 42); context.stroke();
    context.beginPath(); context.moveTo(center - 9, y); context.lineTo(center + 9, y); context.stroke();
    const turretAngle = Math.max(-turretLimitDegrees, Math.min(turretLimitDegrees, displayedTurretAngle));
    const turretX = center + turretAngle / visibleBearingDegrees * (width / 2 - 28);
    context.strokeStyle = "#6ee7ef"; context.lineWidth = 2; context.beginPath(); context.moveTo(turretX, y - 28); context.lineTo(turretX, y + 28); context.stroke();
    const flashingHit = performance.now() < hitFlashUntil;
    if (state.targetAvailable || flashingHit) {
      const bearing = flashingHit ? hitFlashBearing : displayedTargetBearing;
      const clamped = Math.max(-visibleBearingDegrees, Math.min(visibleBearingDegrees, bearing));
      const x = center + clamped / visibleBearingDegrees * (width / 2 - 28);
      context.save();
      if (flashingHit) {
        const pulse = .5 + .5 * Math.sin(performance.now() * .035);
        context.shadowColor = "#fff2a8"; context.shadowBlur = 18 + pulse * 22;
        context.fillStyle = "#fff2a8";
      }
      else context.fillStyle = "#ff6577";
      context.beginPath(); context.arc(x, y, flashingHit ? 10 : 8, 0, Math.PI * 2); context.fill(); context.restore();
      if (!flashingHit && Math.abs(bearing) > visibleBearingDegrees) {
        context.font = "16px monospace"; context.textAlign = clamped < 0 ? "left" : "right";
        context.fillText(clamped < 0 ? "<" : ">", clamped < 0 ? 12 : width - 12, y - 22);
      }
    }
    drawTacticalMap();
  }
  function drawTacticalMap() {
    const width = tacticalMap.clientWidth, height = tacticalMap.clientHeight;
    const originX = width / 2, originY = height - 28;
    const radius = Math.max(1, Math.min(width / 2 - 30, height - 58));
    const halfRadians = tacticalMapHalfAngleDegrees * Math.PI / 180;
    const pointAt = (bearingDegrees, distance) => {
      const angle = bearingDegrees * Math.PI / 180;
      return { x: originX + Math.sin(angle) * distance, y: originY - Math.cos(angle) * distance };
    };
    tacticalMapContext.clearRect(0, 0, width, height);
    tacticalMapContext.fillStyle = "#091925";
    tacticalMapContext.beginPath();
    tacticalMapContext.moveTo(originX, originY);
    tacticalMapContext.arc(originX, originY, radius, -Math.PI / 2 - halfRadians, -Math.PI / 2 + halfRadians);
    tacticalMapContext.closePath();
    tacticalMapContext.fill();
    tacticalMapContext.strokeStyle = "#58758a";
    tacticalMapContext.lineWidth = 1;
    tacticalMapContext.beginPath();
    tacticalMapContext.moveTo(originX, originY);
    tacticalMapContext.lineTo(pointAt(-tacticalMapHalfAngleDegrees, radius).x, pointAt(-tacticalMapHalfAngleDegrees, radius).y);
    tacticalMapContext.arc(originX, originY, radius, -Math.PI / 2 - halfRadians, -Math.PI / 2 + halfRadians);
    tacticalMapContext.lineTo(originX, originY);
    tacticalMapContext.stroke();
    tacticalMapContext.setLineDash([5, 6]);
    tacticalMapContext.beginPath();
    tacticalMapContext.arc(originX, originY, radius * .5, -Math.PI / 2 - halfRadians, -Math.PI / 2 + halfRadians);
    tacticalMapContext.stroke();
    tacticalMapContext.setLineDash([]);
    tacticalMapContext.fillStyle = "#91aabc";
    tacticalMapContext.font = "12px monospace";
    tacticalMapContext.textAlign = "center";
    tacticalMapContext.fillText("500 m", originX, originY - radius * .5 - 8);
    tacticalMapContext.fillText("1 km", originX, originY - radius - 8);
    tacticalMapContext.strokeStyle = "#3f93a0";
    tacticalMapContext.fillStyle = "rgba(110, 231, 239, .08)";
    tacticalMapContext.beginPath();
    tacticalMapContext.moveTo(originX, originY);
    tacticalMapContext.lineTo(pointAt(-turretLimitDegrees, radius).x, pointAt(-turretLimitDegrees, radius).y);
    tacticalMapContext.arc(originX, originY, radius, -Math.PI / 2 - turretLimitDegrees * Math.PI / 180,
      -Math.PI / 2 + turretLimitDegrees * Math.PI / 180);
    tacticalMapContext.closePath();
    tacticalMapContext.fill();
    tacticalMapContext.stroke();
    const turretEnd = pointAt(Math.max(-turretLimitDegrees, Math.min(turretLimitDegrees, state.lanceTurretAngleDegrees)), radius);
    tacticalMapContext.strokeStyle = "#6ee7ef";
    tacticalMapContext.lineWidth = 2;
    tacticalMapContext.beginPath();
    tacticalMapContext.moveTo(originX, originY);
    tacticalMapContext.lineTo(turretEnd.x, turretEnd.y);
    tacticalMapContext.stroke();
    tacticalMapContext.fillStyle = "#dceaf1";
    tacticalMapContext.beginPath();
    tacticalMapContext.arc(originX, originY, 5, 0, Math.PI * 2);
    tacticalMapContext.fill();
    const targetInSector = state.targetAvailable && state.targetDistanceMeters <= tacticalMapRadiusMeters &&
      Math.abs(state.targetBearingDegrees) <= tacticalMapHalfAngleDegrees;
    if (targetInSector) {
      const target = pointAt(state.targetBearingDegrees, radius * state.targetDistanceMeters / tacticalMapRadiusMeters);
      tacticalMapContext.fillStyle = "#ff6577";
      tacticalMapContext.beginPath();
      tacticalMapContext.arc(target.x, target.y, 7, 0, Math.PI * 2);
      tacticalMapContext.fill();
    }
    mapStatus.textContent = targetInSector ? "TARGET IN FORWARD SECTOR" : `FORWARD SECTOR ±${tacticalMapHalfAngleDegrees}°`;
  }
  function update(next) {
    state = next;
    if (lastTargetHitSequence !== null && next.targetHitSequence !== lastTargetHitSequence) {
      hitFlashBearing = next.lastTargetHitBearingDegrees;
      displayedTargetBearing = hitFlashBearing;
      hitFlashUntil = performance.now() + 650;
    }
    lastTargetHitSequence = next.targetHitSequence;
    const percent = Math.round(Math.max(0, Math.min(1, state.lanceCharge)) * 100);
    lanceLabel.textContent = state.lanceReady ? "READY" : `${percent} %`; chargeFill.style.width = `${percent}%`;
    turretStatus.textContent = `TURRET ${state.lanceTurretAngleDegrees >= 0 ? "+" : ""}${state.lanceTurretAngleDegrees.toFixed(1)}° / ±5°`;
    targetStatus.textContent = state.targetAvailable ? "TARGET ACQUIRED" : "NO TARGET";
  }
  function connect() {
    const scheme = location.protocol === "https:" ? "wss" : "ws";
    socket = new WebSocket(`${scheme}://${location.host}/station`);
    socket.addEventListener("open", () => { setConnection(true); socket.send(JSON.stringify({ type: "hello", station: "armarium", protocolVersion })); });
    socket.addEventListener("message", event => { const message = JSON.parse(event.data); if (message.type === "armarium_state") update(message); if (message.type === "error") { setConnection(false); targetStatus.textContent = message.message; } });
    socket.addEventListener("close", () => { heldTurretDirection = null; setConnection(false); setTimeout(connect, reconnectDelayMs); });
    socket.addEventListener("error", () => socket.close());
  }
  function sendTurret(direction, active) {
    if (socket?.readyState !== WebSocket.OPEN) return;
    socket.send(JSON.stringify({ type: "turret", direction, active }));
  }
  function releaseTurret() {
    if (heldTurretDirection !== null) sendTurret(heldTurretDirection, false);
    heldTurretDirection = null;
  }
  document.addEventListener("keydown", event => {
    const direction = event.key === "ArrowLeft" ? "left" : event.key === "ArrowRight" ? "right" : null;
    if (direction) {
      event.preventDefault();
      if (!event.repeat) { heldTurretDirection = direction; sendTurret(direction, true); }
      return;
    }
    if (event.key.toLowerCase() === "f" && !event.repeat && socket?.readyState === WebSocket.OPEN)
      socket.send(JSON.stringify({ type: "fire_lance" }));
  });
  document.addEventListener("keyup", event => {
    const direction = event.key === "ArrowLeft" ? "left" : event.key === "ArrowRight" ? "right" : null;
    if (direction) { event.preventDefault(); if (heldTurretDirection === direction) releaseTurret(); }
  });
  window.addEventListener("blur", releaseTurret);
  document.addEventListener("visibilitychange", () => { if (document.hidden) releaseTurret(); });
  window.addEventListener("resize", resizeCanvases); resizeCanvases(); requestAnimationFrame(animate); connect();
})();
