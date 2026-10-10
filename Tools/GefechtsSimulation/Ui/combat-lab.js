const el=id=>document.getElementById(id);
let catalog=null, sessions=[];
const entries=[
  ['name','NAME','names'],['shipClass','KLASSE','classes'],['massKg','GEWICHT','mass'],['maximumHull','HÜLLEN-HP','hull'],['subclass','SUBKLASSE','subclasses'],
  ['reactor','REAKTOR','reactors'],['bowWeapon','BUGWAFFE','bowWeapons'],['shield','SCHILDGENERATOR','shields'],['sensor','SENSOR ARRAY','sensors'],
  ['mainBooster','MAIN BOOSTER','mainBoosters'],['reverseBooster','REVERSE BOOSTER','reverseBoosters'],['sideBooster','SIDE BOOSTER','sideBoosters'],['boardComputer','BOARDCOMPUTER','boardComputer']
];
const lockKey={name:'name',shipClass:'class',massKg:'mass',maximumHull:'hull',subclass:'subclass',reactor:'reactor',bowWeapon:'weapon',shield:'shield',sensor:'sensor',mainBooster:'mainBooster',reverseBooster:'reverseBooster',sideBooster:'sideBooster',boardComputer:'boardComputer'};
const label=(collection,value)=>{
  const item=(catalog?.[collection]||[]).find(x=>String(x.value)===String(value));
  return item?.label||String(value);
};
const boardValue=ship=>`${ship.boardComputerClass}:${ship.boardComputerMark}`;
const option=(value,text,selected)=>`<option value="${value}" ${String(value)===String(selected)?'selected':''}>${text}</option>`;
const options=(collection,value)=>collection==='names'
  ? catalog.names.map(x=>option(x,x,value)).join('')
  : catalog[collection].map(x=>option(x.value,x.label,value)).join('');
function massOptions(ship){
  const active=catalog.hullOptions.filter(x=>Number(x.shipClass)===Number(ship.shipClass));
  return active.map(x=>option(x.massKg,x.label,ship.massKg)).join('');
}
function hullOptions(ship){
  const active=catalog.hullOptions.filter(x=>Number(x.shipClass)===Number(ship.shipClass));
  return active.map(x=>option(x.maximumHull,`${x.maximumHull} HP`,ship.maximumHull)).join('');
}
function boardOptions(ship){
  return catalog.boardComputerClasses.flatMap(cls=>catalog.boardComputerMarks.map(mark=>option(`${cls.value}:${mark}`,`${cls.label} MK ${roman(mark)}`,boardValue(ship)))).join('');
}
function roman(value){return ['I','II','III','IV','V'][Number(value)-1]||String(value)}
function row(side,[key,title,source],ship){
  const value=key==='boardComputer'?boardValue(ship):ship[key];
  let choices=source==='mass'?massOptions(ship):source==='hull'?hullOptions(ship):source==='boardComputer'?boardOptions(ship):options(source,value);
  return `<div class="loadout-row"><label for="${side}-${key}">${title}</label><select id="${side}-${key}" data-key="${key}">${choices}</select><label class="lock"><input id="${side}-lock-${key}" type="checkbox"><span>SPERREN</span></label></div>`;
}
function renderShip(side,ship){
  const box=document.querySelector(`[data-side="${side}"]`);
  box.innerHTML=`<legend>${side==='nomad'?'SCHIFF 1':'SCHIFF 2'}</legend><div class="ship-actions"><button class="generate" data-side="${side}">GENERATE</button><span id="${side}-status" class="mini-state"></span></div><div class="loadout">${entries.map(entry=>row(side,entry,ship)).join('')}</div>`;
  el(`${side}-shipClass`).onchange=()=>{
    const current=readShip(side);
    const mass=el(`${side}-massKg`), hull=el(`${side}-maximumHull`);
    mass.innerHTML=massOptions(current); hull.innerHTML=hullOptions(current);
  };
  box.querySelector('.generate').onclick=()=>generate(side);
}
function readShip(side){
  const ship={};
  entries.forEach(([key])=>{
    const value=el(`${side}-${key}`).value;
    if(key==='name') ship.name=value;
    else if(key==='boardComputer'){const [kind,mark]=value.split(':');ship.boardComputerClass=Number(kind);ship.boardComputerMark=Number(mark)}
    else ship[key]=Number(value);
  });
  return ship;
}
function lockedFields(side){return entries.filter(([key])=>el(`${side}-lock-${key}`).checked).map(([key])=>lockKey[key])}
async function generate(side){
  const status=el(`${side}-status`);status.textContent='GENERIEREN …';status.className='mini-state';
  try{
    const response=await fetch('/api/generate',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({current:readShip(side),lockedFields:lockedFields(side)})});
    if(!response.ok)throw new Error((await response.text()).replace(/^.*?detail":"?/, '').replace(/["}]+$/,''));
    const ship=await response.json();const retained=new Set(lockedFields(side));renderShip(side,ship);
    entries.forEach(([key])=>el(`${side}-lock-${key}`).checked=retained.has(lockKey[key]));
    el(`${side}-status`).textContent='GENERATED';el(`${side}-status`).className='mini-state green';
  }catch(error){status.textContent=`FEHLER: ${error.message}`;status.className='mini-state red'}
}
async function showForm(copy){
  el('overview').classList.add('hidden');el('formPanel').classList.remove('hidden');el('runState').textContent='';
  const nomad=copy?.nomadShip||await requestGenerated();
  const enemy=copy?.enemyShip||await requestGenerated();
  renderShip('nomad',nomad);renderShip('enemy',enemy);el('battleCount').value=copy?.battleCount||copy?.battles?.length||5;
}
async function requestGenerated(){const response=await fetch('/api/generate',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'});if(!response.ok)throw new Error('Schiffsgenerator nicht verfügbar');return response.json()}
function fmt(value,digits=1){return Number(value||0).toFixed(digits)}
function battleOutcome(outcome,firstName,secondName){return outcome==='nomad_sieg'?`${firstName} SIEG`:outcome==='gegner_sieg'?`${secondName} SIEG`:outcome||'LÄUFT'}
function modules(ship){return [
  ['Reaktor',label('reactors',ship.reactor)],['Bugwaffe',label('bowWeapons',ship.bowWeapon)],['Schildgenerator',label('shields',ship.shield)],['Sensor Array',label('sensors',ship.sensor)],
  ['Main Booster',label('mainBoosters',ship.mainBooster)],['Reverse Booster',label('reverseBoosters',ship.reverseBooster)],['Side Booster',label('sideBoosters',ship.sideBooster)],['Boardcomputer',`${label('boardComputerClasses',ship.boardComputerClass)} MK ${roman(ship.boardComputerMark)}`]
].map(([name,value])=>`<tr><td>${name}</td><td>${value}</td></tr>`).join('')}
function shipTable(title,ship){return `<table><thead><tr><th colspan="2">${title}</th></tr></thead><tbody><tr><td>Name</td><td>${ship.name}</td></tr><tr><td>Klasse</td><td>${label('classes',ship.shipClass)}</td></tr><tr><td>Gewicht</td><td>${fmt(ship.massKg/1000,2)} t</td></tr><tr><td>Hüllen-HP</td><td>${ship.maximumHull}</td></tr><tr><td>Subklasse</td><td>${label('subclasses',ship.subclass)}</td></tr>${modules(ship)}</tbody></table>`}
function legacyTable(title,p){return `<table><thead><tr><th colspan="2">${title}</th></tr></thead><tbody><tr><td>Älterer Lauf</td><td>skalare Parameter</td></tr><tr><td>Main Booster</td><td>${p.mainBoosterKilonewtons} kN</td></tr><tr><td>Lanze</td><td>${p.lanceRangeMeters} m / ${p.lanceChargeSeconds} s</td></tr></tbody></table>`}
function overview(session){
  el('formPanel').classList.add('hidden');el('overview').classList.remove('hidden');
  if(session.configured===false||(!session.nomadShip&&!session.nomad&&!session.enemyShip&&!session.enemy)){el('overview').innerHTML=`<div class="dashboard"><div class="title-row"><div><span>ÄLTERER LAUF</span><h2>${session.id}</h2></div><button id="reuse">NEUES LOADOUT TESTEN</button></div><p class="hint">Dieser Lauf hat noch keine gespeicherten Schiffskonfigurationen. Seine Detail-Logs bleiben im Duell-Log-Viewer verfügbar.</p></div>`;el('reuse').onclick=()=>showForm();return}
  const firstName=session.nomadShip?.name||'SCHIFF 1', secondName=session.enemyShip?.name||'SCHIFF 2';
  const first=session.nomadShip?shipTable(firstName,session.nomadShip):legacyTable(firstName,session.nomad);
  const second=session.enemyShip?shipTable(secondName,session.enemyShip):legacyTable(secondName,session.enemy);
  const wins=session.nomadWins+session.enemyWins;
  el('overview').innerHTML=`<div class="dashboard"><div class="title-row"><div><span>GESPEICHERTER LAUF</span><h2>${session.id}</h2></div><button id="reuse">KONFIGURATION KOPIEREN</button></div><h2>VERWENDETE SCHIFFE</h2><div class="parameters">${first}${second}</div><h2 style="margin-top:24px">STATISTIK</h2><div class="summary"><div class="card"><span>${firstName}-SIEGE</span><b class="green">${session.nomadWins}</b></div><div class="card"><span>${secondName}-SIEGE</span><b class="orange">${session.enemyWins}</b></div><div class="card"><span>TIMEOUTS</span><b class="red">${session.timeouts}</b></div><div class="card"><span>GEFECHTE</span><b>${session.battleCount}</b></div></div><h2 style="margin-top:24px">EINZELERGEBNISSE</h2><div class="battles">${(session.battles||[]).map(b=>`<div class="battle"><span>GEFECHT ${String(b.number).padStart(2,'0')}</span><b class="${b.outcome==='nomad_sieg'?'green':b.outcome==='gegner_sieg'?'orange':'red'}">${battleOutcome(b.outcome,firstName,secondName)}</b><small>${fmt(b.durationSeconds,1)} s · min. ${fmt(b.minimumDistanceMeters,0)} m</small></div>`).join('')}</div><p class="hint">Siege ${firstName}/${secondName}: ${wins?fmt(session.nomadWins/wins*100,0)+'% / '+fmt(session.enemyWins/wins*100)+'%':'keine Entscheidung'}</p></div>`;
  el('reuse').onclick=()=>showForm(session);
}
async function load(){const response=await fetch('/api/sessions');sessions=await response.json();const list=el('sessions');list.innerHTML=sessions.length?'':'<p class="muted">Noch keine Läufe.</p>';sessions.forEach(s=>{const firstName=s.nomadShip?.name||'SCHIFF 1',secondName=s.enemyShip?.name||'SCHIFF 2';const b=document.createElement('button');b.className='session';b.innerHTML=`<b>${s.id}</b><small>${s.battleCount} Gefechte · ${s.configured?`${firstName} ${s.nomadWins}:${s.enemyWins} ${secondName} · ${s.timeouts} TO`:'älterer Lauf'}</small>`;b.onclick=async()=>{document.querySelectorAll('.session').forEach(x=>x.classList.remove('active'));b.classList.add('active');if(s.configured){const r=await fetch('/api/session/'+encodeURIComponent(s.id));overview(await r.json())}else overview(s)};list.append(b)})}
el('newBattle').onclick=()=>showForm().catch(error=>el('runState').textContent=`Fehler: ${error.message}`);
el('cancel').onclick=()=>{el('formPanel').classList.add('hidden');el('overview').classList.remove('hidden')};
el('run').onclick=async()=>{const button=el('run');button.disabled=true;el('runState').textContent='Simulation läuft – alle Ticks werden ohne Echtzeitwartezeit berechnet …';try{const response=await fetch('/api/run',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({battleCount:Number(el('battleCount').value),nomadShip:readShip('nomad'),enemyShip:readShip('enemy')})});if(!response.ok)throw new Error((await response.text())||'Serverfehler');const result=await response.json();await load();overview(result)}catch(error){el('runState').textContent='Fehler: '+error.message}finally{button.disabled=false}};
async function init(){catalog=await (await fetch('/api/catalog')).json();await load()}
init().catch(error=>el('sessions').innerHTML=`<p class="muted">Serverfehler: ${error.message}</p>`);
