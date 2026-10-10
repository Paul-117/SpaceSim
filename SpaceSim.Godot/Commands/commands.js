(() => {
  const protocolVersion = 9;
  const form = document.querySelector('#command-form');
  const input = document.querySelector('#command-input');
  const history = document.querySelector('#history');
  const connection = document.querySelector('#connection');
  const tick = document.querySelector('#tick');
  let socket;
  let lastResultKey = '';

  function line(text, kind) {
    const entry = document.createElement('p');
    entry.className = kind;
    entry.textContent = text;
    history.append(entry);
    while (history.children.length > 80) history.firstElementChild.remove();
    history.scrollTop = history.scrollHeight;
  }
  function setConnection(online) {
    connection.className = `connection${online ? '' : ' offline'}`;
    connection.innerHTML = `<i></i> ${online ? 'COMMAND UPLINK ONLINE' : 'OFFLINE'}`;
  }
  function connect() {
    const scheme = location.protocol === 'https:' ? 'wss' : 'ws';
    socket = new WebSocket(`${scheme}://${location.host}/station`);
    socket.onopen = () => socket.send(JSON.stringify({ type: 'hello', station: 'commands', protocolVersion }));
    socket.onmessage = event => {
      const message = JSON.parse(event.data);
      if (message.type === 'welcome') { setConnection(true); line('UPLINK ESTABLISHED', 'system'); }
      else if (message.type === 'command_state') {
        tick.textContent = `TICK ${String(message.simulationTick || 0).padStart(6, '0')}`;
        const key = `${message.lastCommand}|${message.message}|${message.simulationTick}`;
        if (message.lastCommand && key !== lastResultKey) {
          lastResultKey = key;
          line(message.message, message.accepted ? 'accepted' : 'rejected');
        }
      } else if (message.type === 'error') line(`ERROR: ${message.message}`, 'rejected');
    };
    socket.onclose = () => { setConnection(false); setTimeout(connect, 1500); };
    socket.onerror = () => socket.close();
  }
  form.addEventListener('submit', event => {
    event.preventDefault();
    const command = input.value.trim();
    if (!command) return;
    line(`> ${command.toUpperCase()}`, 'command');
    if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify({ type: 'command_line', command }));
    else line('COMMAND REJECTED: UPLINK OFFLINE', 'rejected');
    input.value = '';
  });
  window.addEventListener('focus', () => input.focus());
  setConnection(false); connect(); input.focus();
})();
