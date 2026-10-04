const stage = document.querySelector("#stage");
const stageWrap = document.querySelector("#stage-wrap");
const viewport = document.querySelector("#canvas-viewport");
const objectsRoot = document.querySelector("#objects");
const reference = document.querySelector("#reference");
const stageLayers = document.querySelector("#stage-layers");
const form = document.querySelector("#inspector-form");
const emptyInspector = document.querySelector("#empty-inspector");
const textFields = document.querySelector("#text-fields");
const barFields = document.querySelector("#bar-fields");
const layoutFile = document.querySelector("#layout-file");
const assetDialog = document.querySelector("#asset-dialog");
const assetDialogList = document.querySelector("#asset-dialog-list");
const assetDialogStatus = document.querySelector("#asset-dialog-status");
const bindingFields = document.querySelector("#binding-fields");
const bindingStatus = document.querySelector("#binding-status");
const bridgeAssetsPath = "../../UI/Bridge/";

const fields = {
  name: document.querySelector("#field-name"), x: document.querySelector("#field-x"), y: document.querySelector("#field-y"),
  width: document.querySelector("#field-width"), height: document.querySelector("#field-height"), text: document.querySelector("#field-text"),
  fontSize: document.querySelector("#field-font-size"), color: document.querySelector("#field-color"), lock: document.querySelector("#field-lock"),
  fill: document.querySelector("#field-fill"), segments: document.querySelector("#field-segments"), gap: document.querySelector("#field-gap"),
  segmentWidth: document.querySelector("#field-segment-width"), segmentHeight: document.querySelector("#field-segment-height"), barColor: document.querySelector("#field-bar-color")
};

const assets = [
  ["Main chassis", "Hintergrund-Rahmen.png", 0, 0, 1920, 1080],
  ["Tactical overlay", "Tactical Overlay.png", 0, 0, 1920, 1080],
  ["Header frame", "Header-Rahmen.png", 0, 0, 581, 128],
  ["Warp frame", "Warp-Rahmen.png", 630, 0, 672, 128],
  ["Contacts frame", "Contacts-Rahmen.png", 0, 137, 465, 610],
  ["Systems frame", "Systems-Rahmen.png", 1453, 126, 459, 406],
  ["Energy frame", "Energy-Rahmen.png", 1453, 545, 459, 463],
  ["Flight status frame", "Flight Status Rahmen.png", 448, 827, 1007, 223],
  ["10 segment frame", "1.png", 800, 200, 320, 40],
  ["4 segment frame", "10.png", 800, 260, 180, 38],
  ["Green status point", "17.png", 800, 320, 50, 50],
  ["Yellow status point", "18..png", 870, 320, 50, 50],
  ["Red status point", "19.png", 940, 320, 50, 50],
  ["Wide blue frame", "37.png", 640, 210, 640, 214]
];

// The complete UI/Bridge asset folder is available from the add dialog. Named
// entries above are the common assets; the remaining source files stay usable
// without having to add them to the stage layer list first.
const additionalAssets = [
  ["Asset 2", "2.png", 640, 210, 640, 214],
  ["Asset 3", "3.png", 640, 210, 640, 214],
  ["Asset 4", "4.png", 640, 210, 640, 214],
  ["Asset 5", "5.png", 680, 260, 480, 240],
  ["Asset 6", "6.png", 680, 260, 480, 240],
  ["Asset 7", "7.png", 800, 360, 120, 120],
  ["Asset 8", "8.png", 800, 360, 120, 120],
  ["Asset 9", "9.png", 800, 360, 120, 120],
  ["Asset 11", "11.png", 760, 300, 260, 174],
  ["Asset 12", "12.png", 640, 210, 640, 214],
  ["Asset 13", "13.png", 760, 300, 260, 174],
  ["Asset 14", "14.png", 640, 210, 640, 214],
  ["Asset 15", "15.png", 760, 300, 260, 174],
  ["Asset 16", "16.png", 760, 300, 260, 174],
  ["Bridge UI current", "Bridge UI current.png", 0, 0, 1920, 1080],
  ["Bridge UI New", "Bridge UI New.png", 0, 0, 1920, 1080]
];

const spriteAssets = [
  ["HUD - crest", "allgemeine HUD-Icons.png", 760, 250, 200, 200, { x: 120, y: 170, width: 300, height: 300 }],
  ["HUD - crosshair", "allgemeine HUD-Icons.png", 760, 250, 205, 205, { x: 480, y: 160, width: 310, height: 315 }],
  ["HUD - gear", "allgemeine HUD-Icons.png", 760, 250, 200, 200, { x: 855, y: 165, width: 280, height: 290 }],
  ["HUD - lightning", "allgemeine HUD-Icons.png", 760, 250, 140, 205, { x: 135, y: 530, width: 200, height: 290 }],
  ["HUD - atom", "allgemeine HUD-Icons.png", 760, 250, 205, 205, { x: 485, y: 520, width: 300, height: 300 }],
  ["HUD - sensor", "allgemeine HUD-Icons.png", 760, 250, 205, 205, { x: 880, y: 530, width: 280, height: 280 }],
  ["HUD - energy bolt", "allgemeine HUD-Icons.png", 760, 250, 145, 205, { x: 110, y: 885, width: 200, height: 280 }],
  ["HUD - gauge", "allgemeine HUD-Icons.png", 760, 250, 205, 205, { x: 370, y: 890, width: 260, height: 260 }],
  ["HUD - rotate", "allgemeine HUD-Icons.png", 760, 250, 205, 205, { x: 700, y: 875, width: 280, height: 270 }],
  ["HUD - rocket", "allgemeine HUD-Icons.png", 760, 250, 105, 220, { x: 1000, y: 850, width: 150, height: 310 }],
  ["Thruster - port", "Thruster-Icons.png", 760, 540, 210, 195, { x: 130, y: 330, width: 280, height: 260 }],
  ["Thruster - starboard", "Thruster-Icons.png", 760, 540, 210, 195, { x: 530, y: 330, width: 280, height: 260 }],
  ["Thruster - reverse", "Thruster-Icons.png", 760, 540, 195, 195, { x: 880, y: 330, width: 260, height: 260 }],
  ["Thruster - forward", "Thruster-Icons.png", 760, 540, 165, 200, { x: 340, y: 680, width: 250, height: 300 }],
  ["Thruster - main", "Thruster-Icons.png", 760, 540, 140, 200, { x: 700, y: 680, width: 210, height: 300 }]
];

assets.push(...additionalAssets, ...spriteAssets);

const gameWidgets = [
  { id: "warp-title", type: "text", name: "Warp label", text: "WARP", x: 755, y: 18, width: 135, height: 30, fontSize: 28, color: "#8eb7d2" },
  { id: "drive-title", type: "text", name: "Drive label", text: "DRIVE", x: 755, y: 50, width: 135, height: 30, fontSize: 28, color: "#8eb7d2" },
  { id: "warp-state", type: "text", name: "Warp status", text: "CHARGING", x: 930, y: 18, width: 160, height: 30, fontSize: 26, color: "#ff7440" },
  { id: "warp-percent", type: "text", name: "Warp percentage", text: "75%", x: 1230, y: 50, width: 90, height: 34, fontSize: 34, color: "#e8f5ff" },
  { id: "warp-bar", type: "bar", name: "Warp charge bar", x: 820, y: 82, width: 340, height: 22, segments: 10, fill: 0.7, color: "#ff7440" },
  { id: "energy-bar", type: "bar", name: "Energy bar", x: 1568, y: 720, width: 205, height: 18, segments: 10, fill: 0.6, color: "#ffcf5c" },
  { id: "thrust-bar", type: "bar", name: "Thrust bar", x: 1116, y: 925, width: 180, height: 20, segments: 4, fill: 0.75, color: "#8bdcff" },
  { id: "ship-hull-bar", type: "bar", name: "Ship hull bar", x: 1562, y: 480, width: 260, height: 22, segments: 3, fill: 1, color: "#65eca8" },
  { id: "contact-hull-bar", type: "bar", name: "Contact hull bar", x: 245, y: 548, width: 145, height: 18, segments: 4, fill: 0.5, color: "#65eca8" },
  { id: "port-thruster-label", type: "text", name: "Port thrusters label", text: "Port Thrusters:", x: 1580, y: 820, width: 210, height: 25, fontSize: 20, color: "#8eb7d2" },
  { id: "port-thruster-state", type: "text", name: "Port thrusters status", text: "ONLINE", x: 1780, y: 820, width: 95, height: 25, fontSize: 20, color: "#65eca8" },
  { id: "reverse-thruster-label", type: "text", name: "Reverse thrusters label", text: "Reverse Thrusters:", x: 1580, y: 870, width: 210, height: 25, fontSize: 20, color: "#8eb7d2" },
  { id: "reverse-thruster-state", type: "text", name: "Reverse thrusters status", text: "ONLINE", x: 1780, y: 870, width: 95, height: 25, fontSize: 20, color: "#65eca8" },
  { id: "main-thruster-label", type: "text", name: "Main thrusters label", text: "Main Thrusters:", x: 1580, y: 920, width: 210, height: 25, fontSize: 20, color: "#8eb7d2" },
  { id: "main-thruster-state", type: "text", name: "Main thrusters status", text: "ONLINE", x: 1780, y: 920, width: 95, height: 25, fontSize: 20, color: "#65eca8" }
];

const defaultObjects = [
  { id: "chassis", type: "asset", name: "Main chassis", file: "Hintergrund-Rahmen.png", x: 0, y: 0, width: 1920, height: 1080, locked: true },
  { id: "tactical", type: "asset", name: "Tactical overlay", file: "Tactical Overlay.png", x: 0, y: 0, width: 1920, height: 1080, locked: true },
  { id: "header", type: "asset", name: "Header frame", file: "Header-Rahmen.png", x: 0, y: 0, width: 581, height: 128 },
  { id: "warp", type: "asset", name: "Warp frame", file: "Warp-Rahmen.png", x: 630, y: 0, width: 672, height: 128 },
  { id: "contacts", type: "asset", name: "Contacts frame", file: "Contacts-Rahmen.png", x: 0, y: 137, width: 465, height: 610 },
  { id: "systems", type: "asset", name: "Systems frame", file: "Systems-Rahmen.png", x: 1453, y: 126, width: 459, height: 406 },
  { id: "energy", type: "asset", name: "Energy frame", file: "Energy-Rahmen.png", x: 1453, y: 545, width: 459, height: 463 },
  { id: "flight", type: "asset", name: "Flight status frame", file: "Flight Status Rahmen.png", x: 448, y: 827, width: 1007, height: 223 },
  { id: "game-name", type: "text", name: "Game name", text: "SpaceSim", x: 120, y: 17, width: 220, height: 44, fontSize: 38, color: "#8bdcff" },
  { id: "encounter", type: "text", name: "Encounter", text: "/ Encounter 2", x: 337, y: 17, width: 250, height: 44, fontSize: 38, color: "#ff7440" },
  { id: "version", type: "text", name: "Version", text: "FlightLab / Version 2.1.2", x: 120, y: 66, width: 385, height: 28, fontSize: 21, color: "#8bdcff" },
  { id: "contacts-title", type: "text", name: "Contacts title", text: "CONTACTS", x: 121, y: 163, width: 230, height: 35, fontSize: 29, color: "#8bdcff" },
  { id: "systems-title", type: "text", name: "Systems title", text: "SYSTEMS", x: 1545, y: 154, width: 230, height: 34, fontSize: 29, color: "#8bdcff" },
  { id: "energy-title", type: "text", name: "Energy title", text: "ENERGY", x: 1546, y: 572, width: 220, height: 35, fontSize: 29, color: "#8bdcff" },
  { id: "flight-title", type: "text", name: "Flight title", text: "FLIGHT STATUS", x: 542, y: 851, width: 260, height: 35, fontSize: 29, color: "#8bdcff" }
];

function defaultText(id, name, text, x, y, width = 180, fontSize = 17, color = "#8eb7d2") {
  return { id, type: "text", name, text, x, y, width, height: Math.max(24, fontSize + 8), fontSize, color };
}

// These are deliberately individual objects instead of one text block. This
// lets the final positions of labels and live values be tuned independently.
defaultObjects.push(...[
  defaultText("contact-name-label", "Contact label: Name", "Name:", 42, 233, 120),
  defaultText("contact-name-value", "Contact value: Name", "Cetus-01", 190, 233, 160, 17, "#e8f5ff"),
  defaultText("contact-class-label", "Contact label: Class", "Class:", 42, 284, 120),
  defaultText("contact-class-value", "Contact value: Class", "Corvette", 190, 284, 160, 17, "#e8f5ff"),
  defaultText("contact-distance-label", "Contact label: Distance", "Distance:", 42, 335, 130),
  defaultText("contact-distance-value", "Contact value: Distance", "5542 m", 190, 335, 160, 17, "#e8f5ff"),
  defaultText("contact-velocity-label", "Contact label: Rel velocity", "Rel Velocity:", 42, 386, 140),
  defaultText("contact-velocity-value", "Contact value: Rel velocity", "100.0 m/s", 190, 386, 160, 17, "#e8f5ff"),
  defaultText("contact-reactor-label", "Contact label: Reactor", "Reactor:", 42, 437, 120),
  defaultText("contact-reactor-value", "Contact value: Reactor", "50%", 190, 437, 160, 17, "#e8f5ff"),
  defaultText("contact-shields-label", "Contact label: Shields", "Shields:", 42, 488, 120),
  defaultText("contact-shields-value", "Contact value: Shields", "Offline", 190, 488, 160, 17, "#ff7777"),
  defaultText("contact-weapons-label", "Contact label: Weapons", "Weapons:", 42, 539, 120),
  defaultText("contact-weapons-value", "Contact value: Weapons", "Charging", 190, 539, 160, 17, "#ffbe67"),
  defaultText("contact-hull-label", "Contact label: Hull integrity", "Hull Integrity:", 42, 590, 145),
  defaultText("contact-hull-value", "Contact value: Hull integrity", "3/3", 190, 590, 160, 17, "#e8f5ff"),

  defaultText("systems-amarium-label", "Systems label: Amarium", "Amarium:", 1540, 233, 165),
  defaultText("systems-amarium-value", "Systems value: Amarium", "ONLINE", 1765, 233, 110, 17, "#65eca8"),
  defaultText("systems-sensorium-label", "Systems label: Sensorium", "Sensorium:", 1540, 294, 165),
  defaultText("systems-sensorium-value", "Systems value: Sensorium", "ONLINE", 1765, 294, 110, 17, "#65eca8"),
  defaultText("systems-voltarium-label", "Systems label: Voltarium", "Voltarium:", 1540, 355, 165),
  defaultText("systems-voltarium-value", "Systems value: Voltarium", "LIMITED", 1765, 355, 110, 17, "#ffcf5c"),
  defaultText("systems-hull-value", "Systems value: Hull integrity", "Hull Integrity: 100%", 1540, 416, 245),

  defaultText("energy-label", "Energy label", "Available Energy:", 1492, 635, 190),
  defaultText("energy-value", "Energy live value", "500.0 PU", 1772, 676, 120, 17, "#e8f5ff"),
  defaultText("energy-thruster-label", "Energy label: Starboard thrusters", "Starboard Thrusters:", 1492, 748, 210),
  defaultText("energy-thruster-value", "Energy value: Starboard thrusters", "ONLINE", 1792, 748, 95, 17, "#65eca8"),

  defaultText("flight-velocity-label", "Flight label: Velocity", "Velocity:", 560, 902, 120),
  defaultText("flight-velocity-value", "Flight value: Velocity", "0.0 m/s", 560, 946, 150, 23, "#e8f5ff"),
  defaultText("flight-angular-label", "Flight label: Angular velocity", "Angular Velocity:", 850, 902, 180),
  defaultText("flight-angular-value", "Flight value: Angular velocity", "0.000 rad/s", 850, 946, 180, 23, "#e8f5ff"),
  defaultText("flight-thrust-label", "Flight label: Thrust", "Thrust:", 1185, 902, 100),
  defaultText("flight-thrust-value", "Flight value: Thrust", "0%", 1410, 946, 70, 23, "#e8f5ff")
]);

let objects = loadLayout();
let selectedId = null;
let action = null;
let snapEnabled = true;
const snapSize = 10;
let zoomFactor = 1;
const panKeys = new Set();
let panAnimationFrame = null;
const collapsedGroups = new Set();

function clone(value) { return JSON.parse(JSON.stringify(value)); }
function layoutKey() { return "spacesim-bridge-layout-editor-v1"; }
function loadLayout() {
  try {
    const layout = JSON.parse(localStorage.getItem(layoutKey())) || clone(defaultObjects);
    reconcileBindings(layout);
    return layout;
  }
  catch { return clone(defaultObjects); }
}

function render() {
  objectsRoot.innerHTML = "";
  objects.forEach((item, index) => {
    if (item.type === "bar") normalizeBar(item);
    const node = document.createElement("div");
    node.className = `layout-object ${item.type === "text" ? "text" : item.type === "bar" ? "bar" : "asset"}${item.locked ? " locked" : ""}${item.id === selectedId ? " selected" : ""}`;
    node.dataset.id = item.id;
    node.dataset.name = item.name;
    node.style.left = `${item.x}px`;
    node.style.top = `${item.y}px`;
    node.style.width = `${item.width}px`;
    node.style.height = `${item.height}px`;
    node.style.zIndex = index + 1;
    if (item.type === "asset") {
      const image = document.createElement("img");
      image.src = `${bridgeAssetsPath}${item.file}`;
      image.alt = item.name;
      node.append(image);
    } else if (item.type === "sprite") {
      const image = document.createElement("img");
      const scaleX = item.width / item.crop.width;
      const scaleY = item.height / item.crop.height;
      image.src = `${bridgeAssetsPath}${item.file}`;
      image.alt = item.name;
      image.style.position = "absolute";
      image.style.width = `${1254 * scaleX}px`;
      image.style.height = `${1254 * scaleY}px`;
      image.style.left = `${-item.crop.x * scaleX}px`;
      image.style.top = `${-item.crop.y * scaleY}px`;
      node.style.overflow = "hidden";
      node.append(image);
    } else if (item.type === "bar") {
      const activeSegments = Math.round((item.fill ?? 0) * item.segments);
      for (let segment = 0; segment < item.segments; segment++) {
        const part = document.createElement("div");
        part.className = `bar-segment${segment < activeSegments ? " active" : ""}`;
        if (segment < activeSegments) part.style.background = item.color;
        part.style.left = `${segment * (item.segmentWidth + item.gap)}px`;
        part.style.width = `${item.segmentWidth}px`;
        part.style.height = `${item.segmentHeight}px`;
        node.append(part);
      }
    } else {
      node.textContent = item.text;
      node.style.color = item.color;
      node.style.fontSize = `${item.fontSize}px`;
    }
    if (item.id === selectedId && !item.locked) {
      const handle = document.createElement("div");
      handle.className = "resize-handle";
      node.append(handle);
    }
    node.addEventListener("pointerdown", pointerDown);
    objectsRoot.append(node);
  });
  renderLayerList();
  updateInspector();
}

function renderLayerList() {
  stageLayers.innerHTML = "";
  const knownIds = new Set(objects.map(item => item.id));
  [...objects].reverse().filter(item => !item.parentId || !knownIds.has(item.parentId)).forEach(item => renderLayerItem(item, stageLayers));
}

function renderLayerItem(item, target) {
  const children = objects.filter(entry => entry.parentId === item.id).reverse();
  const entry = document.createElement("div");
  entry.className = "layer-entry";
  const button = document.createElement("button");
  button.type = "button";
  button.className = `layer-button${item.id === selectedId ? " selected" : ""}`;
  const label = document.createElement("span");
  label.className = "layer-label";
  if (children.length > 0) {
    const toggle = document.createElement("span");
    toggle.className = "group-toggle";
    toggle.textContent = collapsedGroups.has(item.id) ? "▸" : "▾";
    toggle.addEventListener("click", event => {
      event.stopPropagation();
      if (collapsedGroups.has(item.id)) collapsedGroups.delete(item.id);
      else collapsedGroups.add(item.id);
      renderLayerList();
    });
    button.append(toggle);
  }
  label.textContent = item.name;
  button.append(label);
  if (item.locked) {
    const state = document.createElement("span");
    state.className = "locked-state";
    state.textContent = "LOCK";
    button.append(state);
  } else if (children.length > 0) {
    const state = document.createElement("span");
    state.className = "locked-state";
    state.textContent = `${children.length}`;
    button.append(state);
  }
  button.addEventListener("click", () => { selectedId = item.id; render(); });
  entry.append(button);
  if (children.length > 0 && !collapsedGroups.has(item.id)) {
    const list = document.createElement("div");
    list.className = "layer-children";
    children.forEach(child => renderLayerItem(child, list));
    entry.append(list);
  }
  if (target) target.append(entry);
  return entry;
}

function isContainer(item) {
  return item?.type === "asset" && !item.locked;
}

function childrenOf(item) {
  return objects.filter(entry => entry.parentId === item.id);
}

function centerIsInside(child, parent) {
  const centerX = child.x + child.width / 2;
  const centerY = child.y + child.height / 2;
  return centerX >= parent.x && centerX <= parent.x + parent.width && centerY >= parent.y && centerY <= parent.y + parent.height;
}

function isAncestor(candidate, item) {
  let current = item;
  while (current?.parentId) {
    if (current.parentId === candidate.id) return true;
    current = objects.find(entry => entry.id === current.parentId);
  }
  return false;
}

function captureRelative(child, parent) {
  child.relativeX = (child.x - parent.x) / parent.width;
  child.relativeY = (child.y - parent.y) / parent.height;
  child.relativeWidth = child.width / parent.width;
  child.relativeHeight = child.height / parent.height;
  if (child.type === "text") child.relativeFontSize = child.fontSize / parent.height;
}

function applyRelative(child, parent) {
  child.x = Math.round(parent.x + child.relativeX * parent.width);
  child.y = Math.round(parent.y + child.relativeY * parent.height);
  child.width = Math.max(10, Math.round(child.relativeWidth * parent.width));
  child.height = Math.max(10, Math.round(child.relativeHeight * parent.height));
  if (child.type === "text" && Number.isFinite(child.relativeFontSize)) {
    child.fontSize = Math.max(8, Math.round(child.relativeFontSize * parent.height));
  }
  if (child.type === "bar") {
    child.segmentWidth = (child.width - child.gap * (child.segments - 1)) / child.segments;
    child.segmentHeight = child.height;
    normalizeBar(child);
  }
  syncBoundChildren(child);
}

function syncBoundChildren(parent) {
  childrenOf(parent).forEach(child => applyRelative(child, parent));
}

function geometryChanged(item) {
  const parent = objects.find(entry => entry.id === item.parentId);
  if (parent) captureRelative(item, parent);
  syncBoundChildren(item);
}

function reconcileBindings(layout = objects) {
  const byId = new Map(layout.map(item => [item.id, item]));
  layout.forEach(item => {
    const parent = byId.get(item.parentId);
    if (!parent) {
      delete item.parentId;
      return;
    }
    if (![item.relativeX, item.relativeY, item.relativeWidth, item.relativeHeight].every(Number.isFinite)) {
      item.relativeX = (item.x - parent.x) / parent.width;
      item.relativeY = (item.y - parent.y) / parent.height;
      item.relativeWidth = item.width / parent.width;
      item.relativeHeight = item.height / parent.height;
      if (item.type === "text") item.relativeFontSize = item.fontSize / parent.height;
    }
  });
}

function bindContent(parent) {
  const candidates = objects.filter(child =>
    child !== parent && !child.locked && !isAncestor(child, parent) && centerIsInside(child, parent));
  candidates.forEach(child => {
    child.parentId = parent.id;
    captureRelative(child, parent);
  });
  collapsedGroups.delete(parent.id);
  render();
}

function unbindContent(parent) {
  childrenOf(parent).forEach(child => {
    delete child.parentId;
    delete child.relativeX;
    delete child.relativeY;
    delete child.relativeWidth;
    delete child.relativeHeight;
    delete child.relativeFontSize;
  });
  render();
}

function selected() { return objects.find(item => item.id === selectedId); }
function pointerDown(event) {
  const item = objects.find(entry => entry.id === event.currentTarget.dataset.id);
  selectedId = item.id;
  if (!item.locked) {
    const point = stagePoint(event);
    action = event.target.classList.contains("resize-handle")
      ? { type: "resize", id: item.id, point, width: item.width, height: item.height }
      : { type: "move", id: item.id, point, x: item.x, y: item.y };
    event.currentTarget.setPointerCapture(event.pointerId);
  }
  render();
}

function stagePoint(event) {
  const rect = stage.getBoundingClientRect();
  return { x: (event.clientX - rect.left) * 1920 / rect.width, y: (event.clientY - rect.top) * 1080 / rect.height };
}

function snap(value) {
  return snapEnabled ? Math.round(value / snapSize) * snapSize : value;
}

window.addEventListener("pointermove", event => {
  if (!action) return;
  const item = objects.find(entry => entry.id === action.id);
  const point = stagePoint(event);
  if (action.type === "move") {
    item.x = snap(Math.round(action.x + point.x - action.point.x));
    item.y = snap(Math.round(action.y + point.y - action.point.y));
  } else {
    const width = Math.max(10, snap(Math.round(action.width + point.x - action.point.x)));
    const height = Math.max(10, snap(Math.round(action.height + point.y - action.point.y)));
    if (item.type === "bar") {
      item.segmentWidth = (width - item.gap * (item.segments - 1)) / item.segments;
      item.segmentHeight = height;
      normalizeBar(item);
    } else {
      item.width = width;
      item.height = height;
    }
  }
  geometryChanged(item);
  render();
});
window.addEventListener("pointerup", () => { action = null; });

function updateInspector() {
  const item = selected();
  emptyInspector.hidden = !!item;
  form.hidden = !item;
  if (!item) return;
  fields.name.value = item.name;
  fields.x.value = item.x;
  fields.y.value = item.y;
  fields.width.value = item.width;
  fields.height.value = item.height;
  fields.lock.checked = !!item.locked;
  textFields.hidden = item.type !== "text";
  barFields.hidden = item.type !== "bar";
  bindingFields.hidden = !isContainer(item);
  if (isContainer(item)) {
    const count = childrenOf(item).length;
    bindingStatus.textContent = count === 0 ? "Noch keine Inhalte gebunden." : `${count} Inhalt${count === 1 ? "" : "e"} gebunden.`;
    document.querySelector("#unbind-content").disabled = count === 0;
  }
  if (item.type === "text") {
    fields.text.value = item.text;
    fields.fontSize.value = item.fontSize;
    fields.color.value = item.color;
  }
  if (item.type === "bar") {
    normalizeBar(item);
    fields.fill.value = String(Math.round(item.fill * 100));
    fields.segments.value = item.segments;
    fields.gap.value = item.gap;
    fields.segmentWidth.value = item.segmentWidth;
    fields.segmentHeight.value = item.segmentHeight;
    fields.barColor.value = item.color;
  }
}

form.addEventListener("input", event => {
  const item = selected();
  if (!item) return;
  const id = event.target.id;
  const map = { "field-name": "name", "field-x": "x", "field-y": "y", "field-width": "width", "field-height": "height", "field-text": "text", "field-font-size": "fontSize", "field-color": "color" };
  if (id === "field-lock") item.locked = event.target.checked;
  else if (id === "field-fill" && item.type === "bar") item.fill = Math.max(0, Math.min(1, Number(event.target.value) / 100));
  else if (id === "field-segments" && item.type === "bar") item.segments = Number(event.target.value);
  else if (id === "field-gap" && item.type === "bar") item.gap = Number(event.target.value);
  else if (id === "field-segment-width" && item.type === "bar") item.segmentWidth = Number(event.target.value);
  else if (id === "field-segment-height" && item.type === "bar") item.segmentHeight = Number(event.target.value);
  else if (id === "field-bar-color" && item.type === "bar") item.color = event.target.value;
  else if (map[id]) {
    const property = map[id];
    item[property] = ["x", "y", "width", "height", "fontSize"].includes(property) ? Number(event.target.value) : event.target.value;
    if (item.type === "bar" && property === "width") item.segmentWidth = (item.width - item.gap * (item.segments - 1)) / item.segments;
    if (item.type === "bar" && property === "height") item.segmentHeight = item.height;
  }
  if (item.type === "bar") normalizeBar(item);
  geometryChanged(item);
  render();
});

function normalizeBar(item) {
  item.segments = Math.max(1, Math.round(Number(item.segments) || 1));
  item.fill = Math.max(0, Math.min(1, Number(item.fill) || 0));
  item.gap = Math.max(0, Number(item.gap) || 3);
  item.segmentWidth = Math.max(1, Number(item.segmentWidth) || (item.width - item.gap * (item.segments - 1)) / item.segments);
  item.segmentHeight = Math.max(1, Number(item.segmentHeight) || item.height);
  item.width = item.segmentWidth * item.segments + item.gap * (item.segments - 1);
  item.height = item.segmentHeight;
}

window.addEventListener("keydown", event => {
  const item = selected();
  if (!item || item.locked || document.activeElement.tagName === "INPUT") return;
  const delta = event.shiftKey ? (snapEnabled ? 50 : 10) : (snapEnabled ? snapSize : 1);
  const movement = { ArrowLeft: [-delta, 0], ArrowRight: [delta, 0], ArrowUp: [0, -delta], ArrowDown: [0, delta] }[event.key];
  if (movement) { item.x += movement[0]; item.y += movement[1]; geometryChanged(item); event.preventDefault(); render(); }
  if (event.key === "Delete") {
    unbindContent(item);
    objects = objects.filter(entry => entry.id !== item.id);
    selectedId = null;
    render();
  }
});

function panViewport() {
  panAnimationFrame = null;
  const step = panKeys.has("ShiftLeft") || panKeys.has("ShiftRight") ? 28 : 10;
  if (panKeys.has("KeyW")) viewport.scrollTop -= step;
  if (panKeys.has("KeyS")) viewport.scrollTop += step;
  if (panKeys.has("KeyA")) viewport.scrollLeft -= step;
  if (panKeys.has("KeyD")) viewport.scrollLeft += step;
  if (panKeys.size > 0) panAnimationFrame = requestAnimationFrame(panViewport);
}

window.addEventListener("keydown", event => {
  const focused = document.activeElement;
  const isEditingText = focused.matches("textarea, select, input:not([type='range']):not([type='color'])");
  if (isEditingText) return;
  if (!["KeyW", "KeyA", "KeyS", "KeyD", "ShiftLeft", "ShiftRight"].includes(event.code)) return;
  panKeys.add(event.code);
  event.preventDefault();
  if (!panAnimationFrame) panAnimationFrame = requestAnimationFrame(panViewport);
});

window.addEventListener("keyup", event => {
  panKeys.delete(event.code);
});

window.addEventListener("blur", () => {
  panKeys.clear();
});

function addAsset([name, file, x, y, width, height, crop]) {
  const id = `asset-${Date.now()}`;
  objects.push({ id, type: crop ? "sprite" : "asset", name, file, x, y, width, height, ...(crop ? { crop } : {}) });
  selectedId = id;
  render();
}

function addText() {
  const id = `text-${Date.now()}`;
  objects.push({ id, type: "text", name: "New text", text: "NEW TEXT", x: 800, y: 500, width: 260, height: 44, fontSize: 30, color: "#8bdcff" });
  selectedId = id;
  render();
}

function openAddElementDialog() {
  assetDialog.showModal();
  refreshAssetCatalog();
}

function defaultAssetForFile(file) {
  const known = assets.find(asset => asset[1] === file);
  return known ?? [`Neue Datei: ${file}`, file, 760, 360, 280, 140];
}

function renderAssetCatalog(files) {
  assetDialogList.innerHTML = "";
  [...new Set(files)].sort((left, right) => left.localeCompare(right, "de")).forEach(file => {
    const asset = defaultAssetForFile(file);
    const button = document.createElement("button");
    button.type = "button";
    button.className = "asset-button";
    button.innerHTML = `<span>${asset[0]}</span><b>+</b>`;
    button.title = file;
    button.addEventListener("click", () => {
      addAsset(asset);
      assetDialog.close();
    });
    assetDialogList.append(button);
  });
}

async function refreshAssetCatalog() {
  assetDialogStatus.textContent = "Durchsuche UI/Bridge ...";
  try {
    const response = await fetch(new URL("../../api/bridge-assets", window.location.href), { cache: "no-store" });
    if (!response.ok) throw new Error("Asset API unavailable");
    const files = await response.json();
    if (!Array.isArray(files)) throw new Error("Invalid asset response");
    renderAssetCatalog(files);
    assetDialogStatus.textContent = `${files.length} Dateien aus UI/Bridge - automatisch aktualisiert`;
  } catch {
    renderAssetCatalog(assets.map(asset => asset[1]));
    assetDialogStatus.textContent = "Lokaler Server nicht aktiv - zeige bekannten Asset-Katalog.";
  }
}

function addMissingGameWidgets() {
  const existingIds = new Set(objects.map(item => item.id));
  const additions = gameWidgets.filter(item => !existingIds.has(item.id)).map(clone);
  objects.push(...additions);
  return additions;
}
document.querySelector("#dialog-add-game-widgets").addEventListener("click", () => {
  const additions = addMissingGameWidgets();
  selectedId = additions.at(-1)?.id ?? selectedId;
  assetDialog.close();
  render();
});
document.querySelector("#dialog-add-text").addEventListener("click", () => {
  addText();
  assetDialog.close();
});
document.querySelector("#add-element").addEventListener("click", openAddElementDialog);
document.querySelector("#add-element-side").addEventListener("click", openAddElementDialog);
document.querySelector("#bind-content").addEventListener("click", () => {
  const item = selected();
  if (isContainer(item)) bindContent(item);
});
document.querySelector("#unbind-content").addEventListener("click", () => {
  const item = selected();
  if (isContainer(item)) unbindContent(item);
});
document.querySelector("#import-layout").addEventListener("click", () => layoutFile.click());
layoutFile.addEventListener("change", async event => {
  const [file] = event.target.files;
  if (!file) return;
  try {
    const imported = JSON.parse(await file.text());
    if (!Array.isArray(imported)) throw new Error("Layout is not an array.");
    objects = imported;
    reconcileBindings();
    const additions = addMissingGameWidgets();
    selectedId = additions.at(-1)?.id ?? null;
    render();
  } catch {
    alert("Die JSON-Datei konnte nicht als Bridge-Layout gelesen werden.");
  } finally {
    layoutFile.value = "";
  }
});
document.querySelector("#reference-toggle").addEventListener("click", event => {
  reference.classList.toggle("hidden");
  event.currentTarget.textContent = reference.classList.contains("hidden") ? "Reference: Off" : "Reference: On";
});
document.querySelector("#grid-toggle").addEventListener("click", event => {
  stage.classList.toggle("show-grid");
  event.currentTarget.textContent = stage.classList.contains("show-grid") ? "Grid: On" : "Grid: Off";
});
document.querySelector("#snap-toggle").addEventListener("click", event => {
  snapEnabled = !snapEnabled;
  event.currentTarget.textContent = snapEnabled ? "Snap: 10 px" : "Snap: Off";
});
const zoomControl = document.querySelector("#zoom-control");
const zoomValue = document.querySelector("#zoom-value");
function setZoom(value) {
  zoomFactor = Number(value) / 100;
  zoomControl.value = String(value);
  zoomValue.textContent = `${value}%`;
  resizeStage();
}
zoomControl.addEventListener("input", event => setZoom(event.target.value));
document.querySelector("#zoom-fit").addEventListener("click", () => setZoom(100));
document.querySelector("#bring-front").addEventListener("click", () => {
  const item = selected();
  if (!item) return;
  objects = objects.filter(entry => entry !== item);
  objects.push(item);
  render();
});
document.querySelector("#delete-selected").addEventListener("click", () => {
  const item = selected();
  if (!item || item.locked) return;
  unbindContent(item);
  objects = objects.filter(entry => entry !== item);
  selectedId = null;
  render();
});
document.querySelector("#save-layout").addEventListener("click", () => localStorage.setItem(layoutKey(), JSON.stringify(objects)));
document.querySelector("#reset-layout").addEventListener("click", () => { objects = clone(defaultObjects); selectedId = null; render(); });
document.querySelector("#export-layout").addEventListener("click", () => {
  const blob = new Blob([JSON.stringify(objects, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = "bridge-layout.json";
  link.click();
  URL.revokeObjectURL(url);
});

function resizeStage() {
  const availableWidth = viewport.clientWidth - 36;
  const availableHeight = viewport.clientHeight - 36;
  const fitScale = Math.min(availableWidth / 1920, availableHeight / 1080, 1);
  const scale = fitScale * zoomFactor;
  stage.style.setProperty("--scale", scale);
  stageWrap.style.width = `${1920 * scale}px`;
  stageWrap.style.height = `${1080 * scale}px`;
  stageWrap.style.marginLeft = `${Math.max(0, (availableWidth - 1920 * scale) / 2)}px`;
  stageWrap.style.marginTop = `${Math.max(0, (availableHeight - 1080 * scale) / 2)}px`;
}
new ResizeObserver(resizeStage).observe(viewport);
resizeStage();
render();
