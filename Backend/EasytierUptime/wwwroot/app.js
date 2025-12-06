let token = localStorage.getItem('token') || '';
let role = localStorage.getItem('role') || '';
let username = localStorage.getItem('username') || '';

function authHeader() {
  return token ? { 'Authorization': `Bearer ${token}` } : {};
}

async function api(path, opts = {}) {
  const headers = { 'Content-Type': 'application/json', ...authHeader(), ...(opts.headers || {}) };
  const res = await fetch(path, { ...opts, headers });
  if (res.status === 401) throw new Error('未授权，请先登录');
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  if (res.status === 204) return null;
  return await res.json();
}

function qs(id) { return document.getElementById(id); }
function show(el) { el.classList.remove('hidden'); }
function hide(el) { el.classList.add('hidden'); }

function updateAuthUi() {
  if (token) {
    hide(qs('login-form'));
    show(qs('login-info'));
    qs('login-name').textContent = username;
    qs('login-role').textContent = role;
    hide(qs('auth-hint'));
  } else {
    show(qs('login-form'));
    hide(qs('login-info'));
    show(qs('auth-hint'));
  }
}

async function login() {
  const u = qs('login-username').value.trim();
  const p = qs('login-password').value.trim();
  const res = await api('/auth/login', { method: 'POST', body: JSON.stringify({ username: u, password: p }) });
  token = res.access_token; role = res.role || ''; username = u;
  localStorage.setItem('token', token);
  localStorage.setItem('role', role);
  localStorage.setItem('username', username);
  updateAuthUi();
  await loadNodes();
}

function logout() {
  token = ''; role = ''; username = '';
  localStorage.removeItem('token');
  localStorage.removeItem('role');
  localStorage.removeItem('username');
  updateAuthUi();
  // 清空表格
  const tbody = document.querySelector('#nodes-table tbody');
  tbody.innerHTML = '';
}

async function refreshApiHealth() {
  try {
    await fetch('/health');
    qs('api-health').textContent = 'OK';
    qs('api-health').style.color = '#2e7d32';
  } catch {
    qs('api-health').textContent = 'DOWN';
    qs('api-health').style.color = '#c62828';
  }
}

async function loadNodes() {
  if (!token) { return; }
  const tbody = document.querySelector('#nodes-table tbody');
  tbody.innerHTML = '';
  const nodes = await api('/api/nodes');
  for (const n of nodes) {
    const tr = document.createElement('tr');
    tr.innerHTML = `
      <td>${n.id}</td>
      <td>${n.name}</td>
      <td>${n.host}:${n.port}</td>
      <td>${n.protocol}</td>
      <td>${n.version || ''}</td>
      <td>${n.current_connections ?? ''}</td>
      <td>${n.is_approved ? '已审批' : '<span style="color:#c62828">未审批</span>'}</td>
      <td>${n.is_active ? '启用' : '停用'}</td>
      <td>
        <button data-id="${n.id}" class="btn-approve">审批</button>
        <button data-id="${n.id}" class="btn-reject">拒绝</button>
        <button data-id="${n.id}" class="btn-activate">启用</button>
        <button data-id="${n.id}" class="btn-deactivate">停用</button>
        <button data-id="${n.id}" class="btn-health">健康</button>
      </td>
    `;
    tbody.appendChild(tr);
  }
}

async function addNode() {
  if (!token) { alert('请先登录'); return; }
  const payload = {
    name: qs('name').value.trim(),
    protocol: qs('protocol').value,
    host: qs('host').value.trim(),
    port: Number(qs('port').value),
    network_name: qs('network_name').value.trim(),
    network_secret: qs('network_secret').value.trim(),
    max_connections: Number(qs('max_connections').value || 0),
    allow_relay: qs('allow_relay').checked,
    description: qs('description').value.trim()
  };
  const res = await api('/api/nodes', { method: 'POST', body: JSON.stringify(payload) });
  qs('op-msg').textContent = `已创建：ID ${res.id}`;
  await loadNodes();
}

async function testConnection() {
  const payload = {
    name: qs('name').value.trim() || 'quick-test',
    protocol: qs('protocol').value,
    host: qs('host').value.trim(),
    port: Number(qs('port').value),
    network_name: qs('network_name').value.trim(),
    network_secret: qs('network_secret').value.trim()
  };
  const res = await api('/api/test_connection', { method: 'POST', body: JSON.stringify(payload) });
  const box = qs('test-result');
  box.textContent = JSON.stringify(res, null, 2);
  show(box);
}

function bindTableActions() {
  document.querySelector('#nodes-table').addEventListener('click', async (e) => {
    const btn = e.target;
    if (!(btn instanceof HTMLButtonElement)) return;
    const id = Number(btn.getAttribute('data-id'));
    try {
      if (btn.classList.contains('btn-approve')) {
        await api(`/api/nodes/${id}/approve`, { method: 'POST' });
      } else if (btn.classList.contains('btn-reject')) {
        await api(`/api/nodes/${id}/reject`, { method: 'POST' });
      } else if (btn.classList.contains('btn-activate')) {
        await api(`/api/nodes/${id}/activate`, { method: 'POST' });
      } else if (btn.classList.contains('btn-deactivate')) {
        await api(`/api/nodes/${id}/deactivate`, { method: 'POST' });
      } else if (btn.classList.contains('btn-health')) {
        const stats = await api(`/api/nodes/${id}/health/stats`);
        const records = await api(`/api/nodes/${id}/health`);
        showNodeDetails(id, stats, records);
      }
      await loadNodes();
    } catch (err) {
      alert(err.message || err);
    }
  });
}

function showNodeDetails(id, stats, records) {
  qs('detail-title').textContent = `#${id}`;
  const statsBox = qs('stats');
  statsBox.innerHTML = `
    <div>总检查: ${stats.total_checks}</div>
    <div>健康: ${stats.healthy_count} / ${stats.total_checks} (${(stats.health_percentage||0).toFixed(2)}%)</div>
    <div>平均响应(us): ${stats.average_response_time ?? 0}</div>
    <div>可用率: ${(stats.uptime_percentage||0).toFixed(2)}%</div>
  `;
  const tbody = document.querySelector('#health-table tbody');
  tbody.innerHTML = '';
  for (const r of records) {
    const tr = document.createElement('tr');
    tr.innerHTML = `
      <td>${r.checked_at}</td>
      <td>${r.status}</td>
      <td>${r.response_time}</td>
      <td>${r.error_message || ''}</td>
    `;
    tbody.appendChild(tr);
  }
  show(document.getElementById('node-details'));
}

function wire() {
  qs('btn-login').addEventListener('click', () => login().catch(err => alert(err.message)));
  qs('btn-logout').addEventListener('click', () => { logout(); });
  qs('btn-add').addEventListener('click', () => addNode().catch(err => alert(err.message)));
  qs('btn-test').addEventListener('click', () => testConnection().catch(err => alert(err.message)));
  bindTableActions();
  updateAuthUi();
  refreshApiHealth();
  loadNodes();
}

window.addEventListener('DOMContentLoaded', wire);
