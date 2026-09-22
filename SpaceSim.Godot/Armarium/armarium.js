(() => {
  const protocolVersion = 1;
  const visibleBearingDegrees = 30;
  const reconnectDelayMs = 1500;
  const canvas = document.getElementById("scope");
  const context = canvas.getContext("2d");
  const connection = document.getElementById("connection");
  const targetStatus = document.getElementById("target-status");
  const lanceLabel = document.getElementById("lance-label");
  const chargeFill = document.getElementById("charge-fill");
  let socket = null;
  let state = { targetAvailable: false, targetBearingDegrees: 0, lanceCharge: 0, lanceReady: false };

  function setConnection(connected) {
    connection.textContent = connected ? "CONNECTED" : "DISCONNECTED";
    connection.className = `connection ${connected ? "connected" : "disconnected"}`;
  }
  function resizeCanvas() {
    const bounds = canvas.getBoundingClientRect(); const ratio = window.devicePixelRatio || 1;
    canvas.width = Math.round(bounds.width * ratio); canvas.height = Math.round(bounds.height * ratio);
    context.setTransform(ratio, 0, 0, ratio, 0, 0); draw();
  }
  function draw() {
    const width = canvas.clientWidth, height = canvas.clientHeight, center = width / 2, y = height * .64;
    context.clearRect(0, 0, width, height); context.strokeStyle = "#91aabc"; context.fillStyle = "#91aabc";
    context.lineWidth = 1; context.beginPath(); context.moveTo(28, y); context.lineTo(width - 28, y); context.stroke();
    [-30, -15, 0, 15, 30].forEach(degrees => {
      const x = center + degrees / visibleBearingDegrees * (width / 2 - 28);
      context.beginPath(); context.moveTo(x, y - 9); context.lineTo(x, y + 9); context.stroke();
      context.font = "12px monospace"; context.textAlign = "center"; context.fillText(degrees > 0 ? `+${degrees}°` : `${degrees}°`, x, y + 31);
    });
    context.strokeStyle = "#dceaf1"; context.lineWidth = 2; context.beginPath(); context.moveTo(center, y - 42); context.lineTo(center, y + 42); context.stroke();
    context.beginPath(); context.moveTo(center - 9, y); context.lineTo(center + 9, y); context.stroke();
    if (!state.targetAvailable) return;
    const clamped = Math.max(-visibleBearingDegrees, Math.min(visibleBearingDegrees, state.targetBearingDegrees));
    const x = center + clamped / visibleBearingDegrees * (width / 2 - 28);
    context.fillStyle = "#ff6577"; context.beginPath(); context.arc(x, y - 58, 8, 0, Math.PI * 2); context.fill();
    if (Math.abs(state.targetBearingDegrees) > visibleBearingDegrees) {
      context.font = "16px monospace"; context.textAlign = clamped < 0 ? "left" : "right";
      context.fillText(clamped < 0 ? "<" : ">", clamped < 0 ? 12 : width - 12, y - 53);
    }
  }
  function update(next) {
    state = next; const percent = Math.round(Math.max(0, Math.min(1, state.lanceCharge)) * 100);
    lanceLabel.textContent = state.lanceReady ? "READY" : `${percent} %`; chargeFill.style.width = `${percent}%`;
    targetStatus.textContent = state.targetAvailable ? "TARGET ACQUIRED" : "NO TARGET"; draw();
  }
  function connect() {
    const scheme = location.protocol === "https:" ? "wss" : "ws";
    socket = new WebSocket(`${scheme}://${location.host}/station`);
    socket.addEventListener("open", () => { setConnection(true); socket.send(JSON.stringify({ type: "hello", station: "armarium", protocolVersion })); });
    socket.addEventListener("message", event => { const message = JSON.parse(event.data); if (message.type === "armarium_state") update(message); if (message.type === "error") { setConnection(false); targetStatus.textContent = message.message; } });
    socket.addEventListener("close", () => { setConnection(false); setTimeout(connect, reconnectDelayMs); });
    socket.addEventListener("error", () => socket.close());
  }
  document.addEventListener("keydown", event => { if (event.key.toLowerCase() === "f" && !event.repeat && socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify({ type: "fire_lance" })); });
  window.addEventListener("resize", resizeCanvas); resizeCanvas(); connect();
})();
