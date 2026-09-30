(() => {
  const canvas = document.getElementById("spectrum-canvas");
  const context = canvas.getContext("2d");
  const bearingCanvas = document.getElementById("bearing-canvas");
  const bearingContext = bearingCanvas.getContext("2d");
  const sonarCanvas = document.getElementById("sonar-canvas");
  const sonarContext = sonarCanvas.getContext("2d");
  const wavelengthStart = 380;
  const wavelengthEnd = 780;
  const binSize = 5;
  const shipTypes = [
    {
      code: "CETUS",
      type: "CORVETTE",
      color: "#d6b66a",
      peaks: { reactor: 432, shields: 514, propulsion: 656, weapons: 730 },
      envelope: wavelength => 6
        + gaussian(wavelength, 432, 34, 32)
        + gaussian(wavelength, 514, 30, 21)
        + gaussian(wavelength, 656, 44, 26)
        + gaussian(wavelength, 730, 28, 16)
    },
    {
      code: "ARGUS",
      type: "FRIGATE",
      color: "#86d5b0",
      peaks: { reactor: 486, shields: 619, propulsion: 684, weapons: 548 },
      envelope: wavelength => 7
        + gaussian(wavelength, 486, 38, 29)
        + gaussian(wavelength, 548, 29, 17)
        + gaussian(wavelength, 619, 39, 37)
        + gaussian(wavelength, 684, 34, 24)
    },
    {
      code: "ATLAS",
      type: "CRUISER",
      color: "#a89bdb",
      peaks: { reactor: 454, shields: 572, propulsion: 702, weapons: 644 },
      envelope: wavelength => 6
        + gaussian(wavelength, 454, 46, 20)
        + gaussian(wavelength, 572, 43, 35)
        + gaussian(wavelength, 644, 32, 22)
        + gaussian(wavelength, 702, 42, 30)
    }
  ];
  const peakLabels = { reactor: "REACTOR", shields: "SHIELDS", propulsion: "DRIVE", weapons: "WEAPONS" };
  let selectedShipIndex = 0;
  let activeSensorMode = false;
  let sonarSweep = 0;
  let sonarContacts = [];
  let currentContact = {
    shipTypeIndex: 1,
    name: "RNS VIGILANT",
    class: "ARGUS FRIGATE",
    reactorOutput: .78,
    shields: .93,
    propulsion: .42,
    weapons: .57,
    noiseSeed: 0,
    targetBearing: 42,
    bearing: 0,
    estimatedDistance: 8.4
  };
  let contacts = [currentContact];
  let displayedContactIndex = null;

  function gaussian(value, center, width, strength) {
    return strength * Math.exp(-Math.pow(value - center, 2) / (2 * width * width));
  }

  function signatureIntensity(wavelength) {
    const ship = shipTypes[currentContact.shipTypeIndex];
    const backgroundNoise = 1.2
      + Math.sin((wavelength - wavelengthStart) / 18 + currentContact.noiseSeed) * 1.3
      + Math.sin((wavelength - wavelengthStart) / 7 + currentContact.noiseSeed * 1.7) * .55;
    const dynamicPeaks = gaussian(wavelength, ship.peaks.reactor, 4.5, currentContact.reactorOutput * 67)
      + gaussian(wavelength, ship.peaks.shields, 4.5, currentContact.shields * 58)
      + gaussian(wavelength, ship.peaks.propulsion, 5.5, currentContact.propulsion * 48)
      + gaussian(wavelength, ship.peaks.weapons, 4.5, currentContact.weapons * 46);
    const signal = Math.min(100, Math.max(0, ship.envelope(wavelength) + backgroundNoise + dynamicPeaks));
    return signal * alignmentStrength();
  }

  function normalizeBearing(degrees) {
    return ((degrees + 180) % 360 + 360) % 360 - 180;
  }

  function alignmentStrength() {
    const error = normalizeBearing(currentContact.bearing - currentContact.targetBearing);
    return .04 + .96 * Math.exp(-(error * error) / (2 * 20 * 20));
  }

  function updateShipSelector() {
    const ship = shipTypes[selectedShipIndex];
    document.getElementById("ship-code").textContent = ship.code;
    document.getElementById("ship-type").textContent = ship.type;
    document.getElementById("ship-position").textContent = `${String(selectedShipIndex + 1).padStart(2, "0")} / ${String(shipTypes.length).padStart(2, "0")}`;
    document.getElementById("envelope-swatch").style.background = ship.color;
  }

  function moveShipSelection(direction) {
    selectedShipIndex = (selectedShipIndex + direction + shipTypes.length) % shipTypes.length;
    updateShipSelector();
    clearConfirmation();
    drawSpectrum();
  }

  function clearConfirmation() {
    displayedContactIndex = null;
    const status = document.getElementById("contact-status");
    status.textContent = "UNCONFIRMED";
    status.className = "state-label muted";
    document.getElementById("contact-data").classList.add("is-hidden");
    const empty = document.getElementById("contact-empty");
    empty.hidden = false;
    empty.textContent = "NO CONFIRMED CONTACT";
  }

  function showContactParameters(contact) {
    const status = document.getElementById("contact-status");
    const data = document.getElementById("contact-data");
    const empty = document.getElementById("contact-empty");
    status.textContent = "CONFIRMED";
    status.className = "state-label confirmed";
    document.getElementById("contact-name").textContent = contact.name;
    document.getElementById("contact-class").textContent = contact.class;
    document.getElementById("contact-reactor").textContent = `${Math.round(contact.reactorOutput * 100)} %`;
    document.getElementById("contact-shields").textContent = `${Math.round(contact.shields * 100)} %`;
    document.getElementById("contact-propulsion").textContent = `${Math.round(contact.propulsion * 100)} %`;
    document.getElementById("contact-weapons").textContent = `${Math.round(contact.weapons * 100)} %`;
    document.getElementById("contact-distance").textContent = `${contact.estimatedDistance.toFixed(1)} KM`;
    document.getElementById("contact-hull").textContent = "--";
    empty.hidden = true;
    data.classList.remove("is-hidden");
  }

  function confirmIdentification() {
    const status = document.getElementById("contact-status");
    const data = document.getElementById("contact-data");
    const empty = document.getElementById("contact-empty");
    if (selectedShipIndex !== currentContact.shipTypeIndex || alignmentStrength() < .72) {
      status.textContent = "MISMATCH";
      status.className = "state-label mismatch";
      data.classList.add("is-hidden");
      empty.hidden = false;
      empty.textContent = "SIGNATURE MISMATCH";
      return;
    }
    currentContact.identified = true;
    displayedContactIndex = contacts.indexOf(currentContact);
    showContactParameters(currentContact);
    if (activeSensorMode) drawSonar();
  }

  function cycleIdentifiedContacts() {
    const identifiedIndices = contacts.map((contact, index) => contact.identified ? index : -1).filter(index => index >= 0);
    if (identifiedIndices.length === 0) return;
    const position = identifiedIndices.indexOf(displayedContactIndex);
    displayedContactIndex = identifiedIndices[(position + 1) % identifiedIndices.length];
    showContactParameters(contacts[displayedContactIndex]);
    if (activeSensorMode) drawSonar();
  }

  function randomValue(minimum, maximum) {
    return minimum + Math.random() * (maximum - minimum);
  }

  function generateSonarContacts() {
    sonarContacts = contacts.map((contact, index) => ({
      index,
      bearing: contact.targetBearing,
      distance: Math.max(.16, Math.min(.88, contact.estimatedDistance / 25)),
      strength: 1
    }));
  }

  function createContact() {
    const shipTypeIndex = Math.floor(Math.random() * shipTypes.length);
    const names = ["RNS VIGILANT", "ISS SOLACE", "KSV NOVA", "HMS AURORA", "CSV PEREGRINE", "TNS MERIDIAN"];
    const ship = shipTypes[shipTypeIndex];
    return {
      shipTypeIndex,
      name: names[Math.floor(Math.random() * names.length)],
      class: `${ship.code} ${ship.type}`,
      reactorOutput: randomValue(.25, .98),
      shields: randomValue(.05, 1),
      propulsion: randomValue(.1, .9),
      weapons: randomValue(.08, .95),
      noiseSeed: Math.random() * Math.PI * 2,
      targetBearing: Math.round(randomValue(-150, 150)),
      bearing: 0,
      estimatedDistance: randomValue(2.5, 22),
      identified: false
    };
  }

  function selectSignalContact() {
    currentContact = contacts.reduce((nearest, contact) => {
      const nearestError = Math.abs(normalizeBearing(nearest.bearing - nearest.targetBearing));
      const contactError = Math.abs(normalizeBearing(contact.bearing - contact.targetBearing));
      return contactError < nearestError ? contact : nearest;
    });
  }

  function generateSpectrum() {
    contacts = [createContact(), createContact()];
    currentContact = contacts[0];
    displayedContactIndex = null;
    generateSonarContacts();
    selectedShipIndex = 0;
    updateShipSelector();
    clearConfirmation();
    updateBearing();
  }

  function drawPeakZones(graph, graphWidth, graphHeight) {
    const ship = shipTypes[selectedShipIndex];
    const zoneHalfWidth = 8;
    const labels = graphWidth < 380
      ? { reactor: "RCTR", shields: "SHLD", propulsion: "DRIVE", weapons: "WPN" }
      : peakLabels;
    const labelFontSize = graphWidth < 380 ? 8 : 10;
    const laneEnds = [-Infinity, -Infinity];
    const zones = Object.entries(ship.peaks).map(([component, wavelength]) => ({
      component,
      wavelength,
      centerX: graph.left + (wavelength - wavelengthStart) / (wavelengthEnd - wavelengthStart) * graphWidth
    })).sort((left, right) => left.centerX - right.centerX);
    context.save();
    zones.forEach(({ component, wavelength, centerX }) => {
      const leftX = graph.left + (wavelength - zoneHalfWidth - wavelengthStart) / (wavelengthEnd - wavelengthStart) * graphWidth;
      const rightX = graph.left + (wavelength + zoneHalfWidth - wavelengthStart) / (wavelengthEnd - wavelengthStart) * graphWidth;
      context.globalAlpha = .08;
      context.fillStyle = ship.color;
      context.fillRect(leftX, graph.top, rightX - leftX, graphHeight);
      context.globalAlpha = .72;
      context.strokeStyle = ship.color;
      context.lineWidth = 1;
      context.setLineDash([4, 4]);
      [leftX, rightX].forEach(x => {
        context.beginPath();
        context.moveTo(x, graph.top);
        context.lineTo(x, graph.top + graphHeight);
        context.stroke();
      });
      context.setLineDash([]);
      context.globalAlpha = 1;
      context.fillStyle = ship.color;
      context.font = `${labelFontSize}px ui-monospace, monospace`;
      context.textAlign = "center";
      const labelWidth = context.measureText(labels[component]).width;
      const lane = laneEnds[0] + 5 < centerX - labelWidth / 2 ? 0 : 1;
      laneEnds[lane] = centerX + labelWidth / 2;
      context.fillText(labels[component], centerX, lane === 0 ? 13 : 26);
    });
    context.restore();
  }

  function drawBearing() {
    const bounds = bearingCanvas.getBoundingClientRect();
    const ratio = window.devicePixelRatio || 1;
    const width = Math.max(1, Math.round(bounds.width * ratio));
    const height = Math.max(1, Math.round(bounds.height * ratio));
    if (bearingCanvas.width !== width || bearingCanvas.height !== height) { bearingCanvas.width = width; bearingCanvas.height = height; }
    bearingContext.setTransform(ratio, 0, 0, ratio, 0, 0);
    const size = Math.min(bounds.width, bounds.height);
    const centerX = bounds.width / 2;
    const centerY = bounds.height / 2;
    const radius = Math.max(12, size * .36);
    bearingContext.clearRect(0, 0, bounds.width, bounds.height);
    bearingContext.fillStyle = "#12191a";
    bearingContext.fillRect(0, 0, bounds.width, bounds.height);
    bearingContext.strokeStyle = "#304041";
    bearingContext.lineWidth = 1;
    [1, .66, .33].forEach(scale => {
      bearingContext.beginPath();
      bearingContext.arc(centerX, centerY, radius * scale, 0, Math.PI * 2);
      bearingContext.stroke();
    });
    for (let degrees = 0; degrees < 360; degrees += 30) {
      const radians = (degrees - 90) * Math.PI / 180;
      bearingContext.beginPath();
      bearingContext.moveTo(centerX + Math.cos(radians) * radius * .92, centerY + Math.sin(radians) * radius * .92);
      bearingContext.lineTo(centerX + Math.cos(radians) * radius, centerY + Math.sin(radians) * radius);
      bearingContext.stroke();
      bearingContext.fillStyle = "#788987";
      bearingContext.font = `${Math.min(9, Math.max(6, size * .035))}px ui-monospace, monospace`;
      bearingContext.textAlign = "center";
      bearingContext.fillText(String(degrees).padStart(3, "0"), centerX + Math.cos(radians) * radius * .78, centerY + Math.sin(radians) * radius * .78 + 2);
    }
    const bearingRadians = (currentContact.bearing - 90) * Math.PI / 180;
    bearingContext.strokeStyle = "#8cdbb8";
    bearingContext.lineWidth = Math.max(2, size * .012);
    bearingContext.beginPath();
    bearingContext.moveTo(centerX, centerY);
    bearingContext.lineTo(centerX + Math.cos(bearingRadians) * radius, centerY + Math.sin(bearingRadians) * radius);
    bearingContext.stroke();
    bearingContext.fillStyle = "#8cdbb8";
    bearingContext.beginPath();
    bearingContext.arc(centerX + Math.cos(bearingRadians) * radius, centerY + Math.sin(bearingRadians) * radius, Math.max(3, size * .018), 0, Math.PI * 2);
    bearingContext.fill();
    bearingContext.fillStyle = "#dce8e1";
    bearingContext.beginPath();
    bearingContext.moveTo(centerX, centerY - size * .075);
    bearingContext.lineTo(centerX + size * .047, centerY + size * .06);
    bearingContext.lineTo(centerX, centerY + size * .032);
    bearingContext.lineTo(centerX - size * .047, centerY + size * .06);
    bearingContext.closePath();
    bearingContext.fill();
    bearingContext.fillStyle = "#9aaba7";
    bearingContext.font = `${Math.min(16, Math.max(10, size * .055))}px ui-monospace, monospace`;
    bearingContext.textAlign = "left";
    bearingContext.fillText("BACKBORD", 10, 15);
    bearingContext.textAlign = "right";
    bearingContext.fillText("STEUERBORD", bounds.width - 10, 15);
    const formattedBearing = `${currentContact.bearing >= 0 ? "+" : ""}${String(Math.round(currentContact.bearing)).padStart(3, "0")} DEG`;
    bearingContext.fillStyle = "#8cdbb8";
    bearingContext.textAlign = "right";
    bearingContext.fillText(formattedBearing, bounds.width - 10, bounds.height - 10);
  }

  function drawSonar() {
    const bounds = sonarCanvas.getBoundingClientRect();
    const ratio = window.devicePixelRatio || 1;
    const width = Math.max(1, Math.round(bounds.width * ratio));
    const height = Math.max(1, Math.round(bounds.height * ratio));
    if (sonarCanvas.width !== width || sonarCanvas.height !== height) { sonarCanvas.width = width; sonarCanvas.height = height; }
    sonarContext.setTransform(ratio, 0, 0, ratio, 0, 0);
    const size = Math.min(bounds.width, bounds.height);
    const centerX = bounds.width / 2;
    const centerY = bounds.height / 2;
    const radius = Math.max(12, size * .38);
    sonarContext.clearRect(0, 0, bounds.width, bounds.height);
    sonarContext.fillStyle = "#101a17";
    sonarContext.fillRect(0, 0, bounds.width, bounds.height);
    sonarContext.strokeStyle = "#295044";
    sonarContext.lineWidth = 1;
    [1, .75, .5, .25].forEach(scale => {
      sonarContext.beginPath();
      sonarContext.arc(centerX, centerY, radius * scale, 0, Math.PI * 2);
      sonarContext.stroke();
    });
    sonarContext.beginPath();
    sonarContext.moveTo(centerX - radius, centerY);
    sonarContext.lineTo(centerX + radius, centerY);
    sonarContext.moveTo(centerX, centerY - radius);
    sonarContext.lineTo(centerX, centerY + radius);
    sonarContext.stroke();
    for (let trail = 9; trail >= 1; trail -= 1) {
      const trailRadians = (sonarSweep - trail * 8 - 90) * Math.PI / 180;
      sonarContext.globalAlpha = (10 - trail) * .025;
      sonarContext.strokeStyle = "#75cda1";
      sonarContext.lineWidth = Math.max(1, size * .007);
      sonarContext.beginPath();
      sonarContext.moveTo(centerX, centerY);
      sonarContext.lineTo(centerX + Math.cos(trailRadians) * radius, centerY + Math.sin(trailRadians) * radius);
      sonarContext.stroke();
    }
    sonarContext.globalAlpha = 1;
    const sweepRadians = (sonarSweep - 90) * Math.PI / 180;
    sonarContext.strokeStyle = "#7ed6a7";
    sonarContext.lineWidth = Math.max(1, size * .008);
    sonarContext.beginPath();
    sonarContext.moveTo(centerX, centerY);
    sonarContext.lineTo(centerX + Math.cos(sweepRadians) * radius, centerY + Math.sin(sweepRadians) * radius);
    sonarContext.stroke();
    sonarContacts.forEach(contact => {
      const sourceContact = contacts[contact.index];
      const contactRadians = (contact.bearing - 90) * Math.PI / 180;
      const x = centerX + Math.cos(contactRadians) * radius * contact.distance;
      const y = centerY + Math.sin(contactRadians) * radius * contact.distance;
      const sweepDifference = Math.abs(normalizeBearing(sonarSweep - contact.bearing));
      const visibility = .45 + Math.max(0, 1 - sweepDifference / 45) * .55;
      const dotRadius = Math.max(3, size * .024 * contact.strength);
      sonarContext.globalAlpha = visibility;
      sonarContext.fillStyle = "#ef7772";
      sonarContext.beginPath();
      sonarContext.arc(x, y, dotRadius, 0, Math.PI * 2);
      sonarContext.fill();
      sonarContext.globalAlpha = 1;
      if (sourceContact.identified) {
        sonarContext.strokeStyle = "#e5c36c";
        sonarContext.lineWidth = Math.max(1, size * .009);
        sonarContext.beginPath();
        sonarContext.arc(x, y, dotRadius + Math.max(4, size * .028), 0, Math.PI * 2);
        sonarContext.stroke();
      }
      if (contact.index === displayedContactIndex) {
        const frameRadius = dotRadius + Math.max(8, size * .052);
        sonarContext.strokeStyle = "#91e0c2";
        sonarContext.lineWidth = Math.max(1, size * .01);
        sonarContext.strokeRect(x - frameRadius, y - frameRadius, frameRadius * 2, frameRadius * 2);
      }
    });
    sonarContext.globalAlpha = 1;
    sonarContext.fillStyle = "#d7e8df";
    sonarContext.beginPath();
    sonarContext.arc(centerX, centerY, Math.max(3, size * .025), 0, Math.PI * 2);
    sonarContext.fill();
    sonarContext.fillStyle = "#8da99b";
    sonarContext.font = `${Math.max(7, size * .04)}px ui-monospace, monospace`;
    sonarContext.textAlign = "left";
    sonarContext.fillText("ACTIVE SONAR", 10, 15);
  }

  function toggleSensorMode() {
    activeSensorMode = !activeSensorMode;
    document.getElementById("bearing-frame").hidden = activeSensorMode;
    document.getElementById("sonar-frame").hidden = !activeSensorMode;
    document.getElementById("array-heading").textContent = activeSensorMode ? "Active Sonar" : "Spectrometer Bearing";
    const mode = document.getElementById("array-mode");
    mode.textContent = activeSensorMode ? "ACTIVE" : "PASSIVE";
    mode.className = `state-label ${activeSensorMode ? "active" : "muted"}`;
    if (activeSensorMode) drawSonar(); else drawBearing();
  }

  function updateBearing() {
    selectSignalContact();
    drawBearing();
    drawSpectrum();
  }

  function drawSpectrum() {
    const bounds = canvas.getBoundingClientRect();
    const ratio = window.devicePixelRatio || 1;
    const width = Math.max(1, Math.round(bounds.width * ratio));
    const height = Math.max(1, Math.round(bounds.height * ratio));
    if (canvas.width !== width || canvas.height !== height) { canvas.width = width; canvas.height = height; }
    context.setTransform(ratio, 0, 0, ratio, 0, 0);

    const graph = { left: 58, right: 22, top: 38, bottom: 42 };
    const graphWidth = Math.max(1, bounds.width - graph.left - graph.right);
    const graphHeight = Math.max(1, bounds.height - graph.top - graph.bottom);
    context.clearRect(0, 0, bounds.width, bounds.height);
    context.fillStyle = "#12191a";
    context.fillRect(0, 0, bounds.width, bounds.height);

    context.strokeStyle = "#2e3b3c";
    context.lineWidth = 1;
    context.font = "11px ui-monospace, monospace";
    context.fillStyle = "#879794";
    context.textAlign = "right";
    [0, 25, 50, 75, 100].forEach(intensity => {
      const y = graph.top + graphHeight - intensity / 100 * graphHeight;
      context.beginPath();
      context.moveTo(graph.left, y);
      context.lineTo(bounds.width - graph.right, y);
      context.stroke();
      context.fillText(String(intensity), graph.left - 9, y + 4);
    });

    context.textAlign = "center";
    [400, 500, 600, 700, 780].forEach(wavelength => {
      const x = graph.left + (wavelength - wavelengthStart) / (wavelengthEnd - wavelengthStart) * graphWidth;
      context.beginPath();
      context.moveTo(x, graph.top);
      context.lineTo(x, graph.top + graphHeight);
      context.stroke();
      context.fillText(String(wavelength), x, bounds.height - 20);
    });

    drawPeakZones(graph, graphWidth, graphHeight);

    const bins = (wavelengthEnd - wavelengthStart) / binSize;
    const stepWidth = graphWidth / bins;
    for (let index = 0; index < bins; index += 1) {
      const wavelength = wavelengthStart + index * binSize + binSize / 2;
      const intensity = signatureIntensity(wavelength);
      const barHeight = intensity / 100 * graphHeight;
      const x = graph.left + index * stepWidth + 1;
      const y = graph.top + graphHeight - barHeight;
      context.fillStyle = intensity >= 58 ? "#a6ddb0" : "#5bbd9b";
      context.fillRect(x, y, Math.max(1, stepWidth - 2), barHeight);
    }

    const selectedShip = shipTypes[selectedShipIndex];
    context.strokeStyle = selectedShip.color;
    context.lineWidth = 2;
    context.beginPath();
    for (let point = 0; point <= 160; point += 1) {
      const wavelength = wavelengthStart + point / 160 * (wavelengthEnd - wavelengthStart);
      const intensity = Math.min(100, selectedShip.envelope(wavelength));
      const x = graph.left + point / 160 * graphWidth;
      const y = graph.top + graphHeight - intensity / 100 * graphHeight;
      if (point === 0) context.moveTo(x, y); else context.lineTo(x, y);
    }
    context.stroke();

    context.strokeStyle = "#92aaa6";
    context.lineWidth = 1;
    context.beginPath();
    context.moveTo(graph.left, graph.top);
    context.lineTo(graph.left, graph.top + graphHeight);
    context.lineTo(bounds.width - graph.right, graph.top + graphHeight);
    context.stroke();
    context.fillStyle = "#9baaa8";
    context.font = "11px ui-monospace, monospace";
    context.save();
    context.translate(14, graph.top + graphHeight / 2);
    context.rotate(-Math.PI / 2);
    context.textAlign = "center";
    context.fillText("INTENSITY", 0, 0);
    context.restore();
    context.textAlign = "right";
    context.fillText("WAVELENGTH / NM", bounds.width - graph.right, bounds.height - 4);
  }

  window.addEventListener("resize", () => {
    drawSpectrum();
    drawBearing();
    if (activeSensorMode) drawSonar();
  });
  document.getElementById("previous-ship").addEventListener("click", () => {
    moveShipSelection(-1);
  });
  document.getElementById("next-ship").addEventListener("click", () => {
    moveShipSelection(1);
  });
  document.getElementById("generate-spectrum").addEventListener("click", generateSpectrum);
  document.addEventListener("keydown", event => {
    const key = event.key.toLowerCase();
    if (key === "p") {
      event.preventDefault();
      if (!event.repeat) toggleSensorMode();
      return;
    }
    if (key === "l") {
      event.preventDefault();
      if (!event.repeat) cycleIdentifiedContacts();
      return;
    }
    if (key === "arrowleft" || key === "arrowright") {
      event.preventDefault();
      moveShipSelection(key === "arrowleft" ? -1 : 1);
      return;
    }
    if (key === "a" || key === "d") {
      event.preventDefault();
      if (activeSensorMode) return;
      const nextBearing = normalizeBearing(currentContact.bearing + (key === "a" ? -2 : 2));
      contacts.forEach(contact => { contact.bearing = nextBearing; });
      updateBearing();
      return;
    }
    if (event.key !== "Enter" || event.repeat) return;
    event.preventDefault();
    confirmIdentification();
  });
  updateShipSelector();
  clearConfirmation();
  generateSonarContacts();
  updateBearing();
  function animateSonar(timestamp) {
    sonarSweep = timestamp / 7 % 360;
    if (activeSensorMode) drawSonar();
    requestAnimationFrame(animateSonar);
  }
  requestAnimationFrame(animateSonar);
})();
