(() => {
  const ui = {
    files: document.querySelector('#fileInput'), select: document.querySelector('#encounterSelect'),
    canvas: document.querySelector('#mapCanvas'), timeline: document.querySelector('#timeline'),
    play: document.querySelector('#playButton'), fit: document.querySelector('#fitButton'),
    time: document.querySelector('#timeLabel'), metrics: document.querySelector('#metrics'),
    details: document.querySelector('#snapshotDetails'), events: document.querySelector('#eventList')
  };
  const ctx = ui.canvas.getContext('2d');
  let sessions = [], current = null, index = 0, playing = false, timer = null;
  let view = { scale: 1, center: {x:0,z:0}, fitted: false }, drag = null;

  function number(value, fallback = 0) { return Number.isFinite(+value) ? +value : fallback; }
  function vector(v) { return v || {x:0,y:0,z:0}; }
  function eventsOf(snapshot) { return (snapshot?.events || []).map(e => typeof e === 'string' ? {type:e} : e); }
  function snapshots(session) { return session.records.filter(r => r.type === 'snapshot'); }
  function finished(session) { return session.records.find(r => r.type === 'session_finished'); }
  function label(session) { const end = finished(session); return `${session.name} — ${snapshots(session).length} Snapshots${end ? ` — ${end.outcome}` : ''}`; }

  async function loadFiles(files) {
    sessions = [];
    for (const file of files) {
      const records = (await file.text()).split(/\r?\n/).filter(Boolean).flatMap(line => { try { return [JSON.parse(line)]; } catch { return []; } });
      if (records.some(r => r.type === 'snapshot')) sessions.push({ name:file.name, records });
    }
    ui.select.innerHTML = '';
    sessions.forEach((session, n) => { const option = document.createElement('option'); option.value=n; option.textContent=label(session); ui.select.append(option); });
    ui.select.disabled = sessions.length === 0;
    if (sessions.length) choose(0); else clear();
  }

  function choose(n) {
    current = sessions[n]; index = 0; playing = false; clearInterval(timer); ui.play.textContent='▶';
    ui.select.value=n; const data = snapshots(current); ui.timeline.max=Math.max(0,data.length-1); ui.timeline.value=0;
    ui.timeline.disabled=!data.length; ui.play.disabled=!data.length; ui.fit.disabled=!data.length;
    fit(); refresh();
  }
  function clear() { current=null; ctx.clearRect(0,0,ui.canvas.width,ui.canvas.height); ui.metrics.innerHTML='<p>Keine lesbaren 1VS1-Logs geladen.</p>'; ui.details.innerHTML=''; ui.events.innerHTML='<p class="empty">Keine Daten geladen.</p>'; }

  function bounds() {
    const data=snapshots(current); const values=[];
    data.forEach(s => { if(s.player?.position) values.push(vector(s.player.position)); if(s.enemy?.position) values.push(vector(s.enemy.position)); eventsOf(s).forEach(e=>{ if(e.origin) values.push(vector(e.origin)); if(e.end) values.push(vector(e.end)); }); });
    if(!values.length) return {minX:-1,maxX:1,minZ:-1,maxZ:1};
    let minX=Math.min(...values.map(p=>number(p.x))), maxX=Math.max(...values.map(p=>number(p.x))), minZ=Math.min(...values.map(p=>number(p.z))), maxZ=Math.max(...values.map(p=>number(p.z)));
    const pad=Math.max(120,Math.max(maxX-minX,maxZ-minZ)*.12); return {minX:minX-pad,maxX:maxX+pad,minZ:minZ-pad,maxZ:maxZ+pad};
  }
  function resize() { const r=ui.canvas.getBoundingClientRect(), ratio=devicePixelRatio||1; ui.canvas.width=Math.max(1,Math.floor(r.width*ratio)); ui.canvas.height=Math.max(1,Math.floor(r.height*ratio)); ctx.setTransform(ratio,0,0,ratio,0,0); }
  function fit() { if(!current) return; resize(); const r=ui.canvas.getBoundingClientRect(), b=bounds(); view.center={x:(b.minX+b.maxX)/2,z:(b.minZ+b.maxZ)/2}; view.scale=Math.min(r.width/(b.maxX-b.minX),r.height/(b.maxZ-b.minZ)); view.fitted=true; draw(); }
  function screen(p) { const r=ui.canvas.getBoundingClientRect(); return { x:r.width/2+(number(p.x)-view.center.x)*view.scale, y:r.height/2+(number(p.z)-view.center.z)*view.scale }; }
  function world(p) { const r=ui.canvas.getBoundingClientRect(); return { x:view.center.x+(p.x-r.width/2)/view.scale, z:view.center.z+(p.y-r.height/2)/view.scale }; }

  function line(points, color, width=1, dash=[]) { if(points.length<2)return; ctx.save();ctx.strokeStyle=color;ctx.lineWidth=width;ctx.setLineDash(dash);ctx.beginPath(); points.forEach((p,n)=>{const q=screen(p); n?ctx.lineTo(q.x,q.y):ctx.moveTo(q.x,q.y);});ctx.stroke();ctx.restore(); }
  function ship(p, forward, color, name) { if(!p)return; const q=screen(p), f=vector(forward), angle=Math.atan2(number(f.z),number(f.x)); ctx.save();ctx.translate(q.x,q.y);ctx.rotate(angle);ctx.fillStyle=color;ctx.beginPath();ctx.moveTo(10,0);ctx.lineTo(-7,6);ctx.lineTo(-4,0);ctx.lineTo(-7,-6);ctx.closePath();ctx.fill();ctx.restore();ctx.fillStyle=color;ctx.font='12px Segoe UI';ctx.fillText(name,q.x+12,q.y-9); }
  function hitMark(p, color='#ff5b65') { if(!p)return; const q=screen(p);ctx.save();ctx.strokeStyle=color;ctx.lineWidth=2;ctx.beginPath();ctx.arc(q.x,q.y,8,0,Math.PI*2);ctx.stroke();ctx.beginPath();ctx.moveTo(q.x-12,q.y);ctx.lineTo(q.x+12,q.y);ctx.moveTo(q.x,q.y-12);ctx.lineTo(q.x,q.y+12);ctx.stroke();ctx.restore(); }
  // Older logs recorded only event names. Their shot path is reconstructed from the
  // firing ship's stored pose, so they remain useful after the detailed format upgrade.
  function drawLegacyShot(snapshot, bright) {
    const range=number(snapshot.settings?.lanceRangeMeters,1000);
    [['player','#c7f7ff'],['enemy','#ffd0a2']].forEach(([side,color])=>{
      const ship=snapshot[side]; if(!ship?.command?.fireLance||!ship.position||!ship.forward)return;
      const f=vector(ship.forward), p=vector(ship.position);
      line([p,{x:number(p.x)+number(f.x)*range,z:number(p.z)+number(f.z)*range}],color,bright?2:1,[7,4]);
    });
  }
  function legacyHitMark(snapshot, type) {
    if(type==='EnemyDestroyed') return hitMark(snapshot.enemy?.position);
    if(type==='PlayerDestroyed') return hitMark(snapshot.player?.position);
    if(snapshot.player?.command?.fireLance) return hitMark(snapshot.enemy?.position, type==='ShieldHit'?'#ffcb5a':'#ff5b65');
    if(snapshot.enemy?.command?.fireLance) return hitMark(snapshot.player?.position, type==='ShieldHit'?'#ffcb5a':'#ff5b65');
  }
  function drawGrid() { const r=ui.canvas.getBoundingClientRect();ctx.fillStyle='#03070d';ctx.fillRect(0,0,r.width,r.height);ctx.strokeStyle='#0d2232';ctx.lineWidth=1; const step=niceStep(95/view.scale); const startX=Math.floor((view.center.x-r.width/(2*view.scale))/step)*step; const endX=view.center.x+r.width/(2*view.scale); const startZ=Math.floor((view.center.z-r.height/(2*view.scale))/step)*step; const endZ=view.center.z+r.height/(2*view.scale); for(let x=startX;x<=endX;x+=step){let a=screen({x,z:0}),b=screen({x,z:1});ctx.beginPath();ctx.moveTo(a.x,0);ctx.lineTo(a.x,r.height);ctx.stroke();} for(let z=startZ;z<=endZ;z+=step){let a=screen({x:0,z}),b=screen({x:1,z});ctx.beginPath();ctx.moveTo(0,a.y);ctx.lineTo(r.width,a.y);ctx.stroke();} ctx.fillStyle='#607d92';ctx.font='11px Segoe UI';ctx.fillText(`${step.toFixed(step<1?1:0)} m Raster`,8,16); }
  function niceStep(raw){const p=Math.pow(10,Math.floor(Math.log10(raw)));const n=raw/p;return (n<1.5?1:n<3?2:n<7?5:10)*p;}
  function draw() {
    resize(); if(!current){ctx.clearRect(0,0,ui.canvas.width,ui.canvas.height);return;} drawGrid(); const data=snapshots(current), now=data[index];
    const fullP=data.map(s=>s.player?.position).filter(Boolean), fullE=data.map(s=>s.enemy?.position).filter(Boolean); line(fullP,'#167c91',1);line(fullE,'#9a5425',1);line(fullP.slice(0,index+1),'#35d5f4',2);line(fullE.slice(0,index+1),'#ff9d4d',2);
    data.slice(0,index+1).forEach((s,i)=>eventsOf(s).forEach(e=>{
      if(e.type==='WeaponFired') {
        if(e.origin&&e.end) line([e.origin,e.end],e.owner==='Player'?'#c7f7ff':'#ffd0a2',i===index?2:1,[7,4]);
        else drawLegacyShot(s, i===index);
      }
      if(['ShieldHit','HullDamaged','EnemyDestroyed','PlayerDestroyed','ShipCollision'].includes(e.type)) {
        if(e.position) hitMark(e.position,e.type==='ShieldHit'?'#ffcb5a':'#ff5b65');
        else legacyHitMark(s, e.type);
      }
    }));
    ship(now?.player?.position,now?.player?.forward,'#35d5f4','NOMAD'); ship(now?.enemy?.position,now?.enemy?.forward,'#ff9d4d',now?.enemy?.name||'GEGNER');
  }
  function metric(label,value,klass=''){return `<p class="metric"><span>${label}</span><b class="value ${klass}">${value}</b></p>`;}
  function refresh() { if(!current)return; const data=snapshots(current), s=data[index], end=finished(current), all=data.flatMap(eventsOf), shots=all.filter(e=>e.type==='WeaponFired'), hits=shots.filter(e=>e.hitKind&&e.hitKind!=='None'); ui.time.textContent=formatTime(number(s?.simulationSeconds)); ui.timeline.value=index;
    ui.metrics.innerHTML=metric('Ergebnis',end?.outcome||'läuft',end?.outcome==='player_victory'?'victory':end?.outcome==='player_destroyed'?'defeat':'')+metric('Dauer',`${formatTime(number(end?.simulationSeconds ?? data.at(-1)?.simulationSeconds))}`)+metric('Minimaldistanz',`${Math.min(...data.map(x=>number(x.aiContext?.distanceMeters,Infinity))).toFixed(1)} m`)+metric('Schüsse',`${shots.length}`)+metric('Treffer',`${hits.length}`)+metric('Snapshots',data.length);
    const a=s?.aiContext||{}, e=s?.enemy||{}, p=s?.player||{}; ui.details.innerHTML=detail('Zeit',formatTime(number(s?.simulationSeconds)))+detail('AI State',e.state||'-')+detail('Distanz',`${number(a.distanceMeters).toFixed(1)} m`)+detail('Relativ',`${number(a.relativeSpeedMetersPerSecond).toFixed(1)} m/s`)+detail('Closing',`${number(a.closingSpeedMetersPerSecond).toFixed(1)} m/s`)+detail('Enemy Aim',`${number(a.enemyAimErrorDegrees).toFixed(2)}°`)+detail('Player Aim',`${number(a.playerAimErrorDegrees).toFixed(2)}°`)+detail('Enemy Lance',`${(number(e.lance?.charge)*100).toFixed(0)} %`)+detail('Player Lance',`${(number(p.lance?.charge)*100).toFixed(0)} %`)+detail('Enemy Hull / Shield',`${e.hull ?? '-'} / ${number(e.shield).toFixed(0)}`)+detail('Player Hull / Shield',`${p.hull ?? '-'} / ${number(p.shield).toFixed(0)}`);
    const rows=data.flatMap((x,n)=>eventsOf(x).map(ev=>({...ev,time:number(x.simulationSeconds),n})));ui.events.innerHTML=rows.length?rows.map(ev=>`<div class="event" data-index="${ev.n}"><time>${formatTime(ev.time)}</time><span class="event-type">${ev.type}</span><span>${eventText(ev)}</span></div>`).join(''):'<p class="empty">Bislang keine Kampfereignisse.</p>';ui.events.querySelectorAll('.event').forEach(el=>el.onclick=()=>{index=+el.dataset.index;refresh();draw();});draw(); }
  function detail(k,v){return `<dt>${k}</dt><dd>${v}</dd>`;} function formatTime(s){s=Math.max(0,s||0);return `${Math.floor(s/60).toString().padStart(2,'0')}:${(s%60).toFixed(1).padStart(4,'0')}`;} function eventText(e){if(e.type==='WeaponFired')return `${e.owner||'?'} → ${e.hitKind||'?'}`;if(e.type==='ShieldHit')return `${e.targetOwner||'?'} Schild ${number(e.shieldBefore).toFixed(0)} → ${number(e.shieldAfter).toFixed(0)}`;if(e.type==='HullDamaged')return `${e.targetOwner||'?'} Hull ${e.hullBefore} → ${e.hullAfter}`;return '';}
  ui.files.onchange=e=>loadFiles([...e.target.files]);ui.select.onchange=()=>choose(+ui.select.value);ui.timeline.oninput=()=>{index=+ui.timeline.value;refresh();};ui.play.onclick=()=>{playing=!playing;ui.play.textContent=playing?'Ⅱ':'▶';clearInterval(timer);if(playing)timer=setInterval(()=>{const d=snapshots(current);index=(index+1)%d.length;refresh();},100);};ui.fit.onclick=fit;ui.canvas.onwheel=e=>{if(!current)return;e.preventDefault();const before=world({x:e.offsetX,y:e.offsetY});view.scale*=e.deltaY<0?1.15:1/1.15;view.scale=Math.max(.002,Math.min(20,view.scale));const after=world({x:e.offsetX,y:e.offsetY});view.center.x+=before.x-after.x;view.center.z+=before.z-after.z;draw();};ui.canvas.onpointerdown=e=>{drag={p:{x:e.clientX,y:e.clientY},c:{...view.center}};ui.canvas.setPointerCapture(e.pointerId);ui.canvas.classList.add('dragging');};ui.canvas.onpointermove=e=>{if(!drag)return;const r=ui.canvas.getBoundingClientRect();view.center.x=drag.c.x-(e.clientX-drag.p.x)/view.scale;view.center.z=drag.c.z-(e.clientY-drag.p.y)/view.scale;draw();};ui.canvas.onpointerup=()=>{drag=null;ui.canvas.classList.remove('dragging');};window.onresize=()=>{if(current)draw();};window.onkeydown=e=>{if(e.key.toLowerCase()==='a'&&!['INPUT','SELECT'].includes(document.activeElement.tagName))fit();};
})();
