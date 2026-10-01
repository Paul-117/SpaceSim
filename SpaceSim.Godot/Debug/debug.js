(() => {
  const protocolVersion = 9;
  const reconnectDelayMs = 1500;
  const fields = ["enemy-id", "difficulty", "mode", "ai-state", "distance", "closing-speed", "relative-speed", "enemy-speed", "hull", "shield", "lance", "lance-ready", "reactor-level", "reactor-power", "fuel", "propulsion-power", "weapons-power", "shields-power", "enemy-combat-active", "enemy-attack-state", "enemy-lance-ready", "enemy-target-range", "enemy-target-front", "enemy-aim", "enemy-ray-hit", "enemy-fire-ready", "player-lance-ready", "player-target-range", "player-target-front", "player-aim", "player-ray-hit", "player-fire-ready", "tick"];
  const elements = Object.fromEntries(fields.map(id => [id, document.getElementById(id)]));
  const connection = document.getElementById("connection"), notice = document.getElementById("no-enemy"), dashboard = document.getElementById("dashboard");
  let socket = null;
  const number = value => Number.isFinite(value) ? value.toFixed(1) : "—";
  function setConnection(connected) { connection.textContent = connected ? "DEBUG ONLINE" : "DEBUG OFFLINE"; connection.className = `connection ${connected ? "connected" : "disconnected"}`; }
  function setText(id, value) { elements[id].textContent = value; }
  function condition(name, value) { const percent = Math.max(0, Math.min(100, value * 100)); const bar = document.getElementById(`${name}-condition`); bar.style.width = `${percent}%`; bar.className = percent <= 0 ? "offline" : percent <= 50 ? "damaged" : ""; document.getElementById(`${name}-condition-label`).textContent = `${percent.toFixed(0)} %`; }
  function gate(id, passed) { const element = elements[id]; element.textContent = passed ? "PASS" : "BLOCK"; element.className = passed ? "pass" : "fail"; }
  function fireDiagnostics(prefix, control, showCombatState) {
    const info = control || {};
    if (showCombatState) { gate(`${prefix}-combat-active`, info.combatActive === true); gate(`${prefix}-attack-state`, info.attackState === true); }
    gate(`${prefix}-lance-ready`, info.lanceReady === true); gate(`${prefix}-target-range`, info.targetInRange === true); gate(`${prefix}-target-front`, info.targetInFront === true);
    const error = number(Math.abs(info.aimErrorDegrees)); const tolerance = number(info.aimToleranceDegrees);
    setText(`${prefix}-aim`, `${error}° / ${tolerance}°`); elements[`${prefix}-aim`].className = info.aimWithinTolerance === true ? "pass" : "fail";
    gate(`${prefix}-ray-hit`, info.rayWouldHit === true); gate(`${prefix}-fire-ready`, info.fireCommandWouldBeIssued === true);
  }
  function update(state) {
    const available = state.enemyAvailable === true; notice.hidden = available; dashboard.hidden = !available; setText("tick", `TICK ${state.simulationTick ?? "—"}`); if (!available) return;
    setText("enemy-id", `#${state.enemyId}`); setText("difficulty", state.difficulty); setText("mode", state.playerDetected ? "COMBAT / DETECTED" : "PATROL / UNAWARE"); setText("ai-state", state.aiState);
    setText("distance", `${number(state.distanceToPlayer)} m`); setText("closing-speed", `${number(state.closingSpeed)} m/s`); setText("relative-speed", `${number(state.relativeSpeed)} m/s`); setText("enemy-speed", `${number(state.enemySpeed)} m/s`);
    setText("hull", `${state.hull} / ${state.maximumHull}`); setText("shield", `${number(state.shield)} / ${number(state.maximumShield)}`); setText("lance", `${number((state.lanceCharge || 0) * 100)} %`); setText("lance-ready", state.lanceReady ? "READY" : "CHARGING");
    condition("propulsion", state.propulsionCondition); condition("weapons", state.weaponsCondition); condition("shields", state.shieldsCondition);
    setText("reactor-level", `${number(state.reactorTargetOperatingLevelPercent)} / ${number(state.reactorOperatingLevelPercent)} %`); setText("reactor-power", `${number(state.reactorAvailablePower)} / ${number(state.reactorCurrentDraw)} PU`); setText("fuel", `${number(state.reactorFuel)} / ${number(state.reactorFuelCapacity)}`);
    setText("propulsion-power", `${number(state.propulsionRequested)} / ${number(state.propulsionDraw)} PU`); setText("weapons-power", `${number(state.weaponsRequested)} / ${number(state.weaponsDraw)} PU`); setText("shields-power", `${number(state.shieldsRequested)} / ${number(state.shieldsDraw)} PU`);
    fireDiagnostics("enemy", state.enemyFireControl, true); fireDiagnostics("player", state.playerFireControl, false);
  }
  function connect() { const scheme = location.protocol === "https:" ? "wss" : "ws"; socket = new WebSocket(`${scheme}://${location.host}/station`); socket.addEventListener("open", () => socket.send(JSON.stringify({ type: "hello", station: "debug", protocolVersion }))); socket.addEventListener("message", event => { const message = JSON.parse(event.data); if (message.type === "welcome") setConnection(true); if (message.type === "enemy_debug_state") update(message); }); socket.addEventListener("close", () => { setConnection(false); window.setTimeout(connect, reconnectDelayMs); }); socket.addEventListener("error", () => socket.close()); }
  setConnection(false); connect();
})();
