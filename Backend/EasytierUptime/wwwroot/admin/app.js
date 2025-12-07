let token = localStorage.getItem('token') || '';
let role = localStorage.getItem('role') || '';
let username = localStorage.getItem('username') || '';
let editingId = null; // 当前编辑的节点 ID

function authHeader() { return token ? { 'Authorization': `Bearer ${token}` } : {}; }
async function api(path, opts = {}) {
  const headers = { 'Content-Type': 'application/json', ...authHeader(), ...(opts.headers || {}) };
  const res = await fetch(path, { ...opts, headers });
  if (res.status === 401) {
    token=''; role=''; username='';
    localStorage.removeItem('token');
    localStorage.removeItem('role');
    localStorage.removeItem('username');
    const back = encodeURIComponent(location.pathname + location.search);
    if (!/\/auth\//.test(location.pathname)) {
      location.replace(`/auth/?back=${back}`);
    }
    throw new Error('未授权，请先登录');
  }
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  if (res.status === 204) return null;
  return await res.json();
}
function qs(id) { return document.getElementById(id); }
function show(el) { el.classList.remove('hidden'); } function hide(el) { el.classList.add('hidden'); }
function toast(msg, cls='') { const box = qs('toast'); if(!box) return; box.textContent = msg; box.classList.remove('ok','bad','warn'); if(cls) box.classList.add(cls==='error'?'bad':cls); box.classList.add('show'); clearTimeout(box._timer); box._timer=setTimeout(()=>{ box.classList.remove('show'); }, 3200); }
function esc(s){ return String(s??'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;').replace(/'/g,'&#39;'); }
function formatLocalTime(v){ if(!v) return ''; const str = String(v); const noTz = /^(\d{4}-\d{2}-\d{2})[T ](\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?)$/; let d; if (noTz.test(str)) { const iso = str.replace(' ', 'T') + 'Z'; d = new Date(iso); } else { d = new Date(str); } return isNaN(d) ? str : d.toLocaleString(); }

// 简易加载遮罩
function ensureLoadingStyles(){
  if (document.getElementById('loading-overlay-style')) return;
  const style = document.createElement('style');
  style.id = 'loading-overlay-style';
  style.textContent = `
  #loading-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.35); display: flex; align-items: center; justify-content: center; z-index: 2000; }
  #loading-overlay .box { background: rgba(255,255,255,0.9); border-radius: 12px; padding: 16px 20px; box-shadow: 0 8px 24px rgba(0,0,0,0.15); display:flex; align-items:center; gap:10px; }
  #loading-overlay .loading { display:inline-block; width:20px; height:20px; border: 2px solid rgba(0,0,0,0.2); border-radius:50%; border-top-color: #6c5ce7; animation: spin 1s linear infinite; }
  @keyframes spin { 0%{transform:rotate(0)} 100%{transform:rotate(360deg)} }
  `;
  document.head.appendChild(style);
}
function showLoading(text='处理中...'){
  ensureLoadingStyles();
  let overlay = document.getElementById('loading-overlay');
  if (!overlay){
    overlay = document.createElement('div');
    overlay.id = 'loading-overlay';
    overlay.innerHTML = `<div class="box"><span class="loading"></span><span>${esc(text)}</span></div>`;
    document.body.appendChild(overlay);
  } else {
    overlay.querySelector('.box span:last-child').textContent = text;
    overlay.style.display = 'flex';
  }
}
function hideLoading(){ const overlay = document.getElementById('loading-overlay'); if(overlay) overlay.style.display='none'; }

function parseHostPort(){ const hostInput = qs('host'); const portInput = qs('port'); const v = hostInput.value.trim(); const idx = v.lastIndexOf(':'); if (idx > 0 && idx === v.indexOf(':')) { const h = v.slice(0, idx); const p = Number(v.slice(idx+1)); if (h && Number.isFinite(p) && p>0 && p<=65535){ hostInput.value = h; portInput.value = String(p); } } }
function formValid(){ const name = qs('name').value.trim(); const host = qs('host').value.trim(); const port = Number(qs('port').value); const nn = qs('network_name').value.trim(); const ns = qs('network_secret').value.trim(); return !!(name && host && nn && ns && Number.isFinite(port) && port>=1 && port<=65535); }
function updateAddButton(){ const btn = qs('btn-add'); if(!btn) return; const valid = formValid(); btn.disabled = !valid; btn.classList.toggle('btn-disabled', !valid); if(valid){ highlightInvalid(false); } }
function resetForm(){ qs('node-form').reset(); qs('port').value = '11010'; qs('test-result').classList.add('hidden'); qs('test-result').textContent=''; updateAddButton(); setSeg(false); highlightInvalid(false); }

function setSeg(publicFlag){ const pri = qs('seg-private'); const pub = qs('seg-public'); const hidden = qs('is_public'); if (!pri || !pub || !hidden) return; hidden.checked = !!publicFlag; pub.classList.toggle('active', !!publicFlag); pri.classList.toggle('active', !publicFlag); }
function wireSeg(){ const pri = qs('seg-private'); const pub = qs('seg-public'); if (pri) pri.addEventListener('click', () => { setSeg(false); updateAddButton(); }); if (pub) pub.addEventListener('click', () => { setSeg(true); updateAddButton(); }); }

function updateAuthUi() { const loginInfo = document.getElementById('login-info'); const loginLink = document.getElementById('login-link'); if (token) { if (loginInfo) show(loginInfo); if (loginLink) loginLink.style.display = 'none'; if (qs('login-name')) qs('login-name').textContent = username; if (qs('login-role')) qs('login-role').textContent = role; hide(qs('auth-hint')); const usersLink = qs('lnk-users'); if (usersLink) usersLink.style.display = (role === 'admin') ? '' : 'none'; } else { if (loginInfo) hide(loginInfo); if (loginLink) loginLink.style.display = ''; show(qs('auth-hint')); const back = encodeURIComponent(location.pathname + location.search); if (!/\/auth\//.test(location.pathname)) location.replace(`/auth/?back=${back}`); } }
function logout() { token = ''; role = ''; username = ''; localStorage.removeItem('token'); localStorage.removeItem('role'); localStorage.removeItem('username'); updateAuthUi(); const tbody = document.querySelector('#nodes-table tbody'); if (tbody) tbody.innerHTML = ''; toast('已退出','ok'); }
async function refreshApiHealth() { try { await fetch('/health'); qs('api-health').textContent = 'OK'; qs('api-health').style.color = '#10b981'; } catch { qs('api-health').textContent = 'DOWN'; qs('api-health').style.color = '#ef4444'; } }

function pill(text, cls){ return `<span class="pill ${cls}">${text}</span>`; }

function highlightInvalid(show){ const required = ['name','host','port','network_name','network_secret']; for(const id of required){ const el=qs(id); if(!el) continue; el.classList.toggle('invalid', show && !el.value.trim()); if(id==='port'){ const v=Number(el.value); if(show && (!Number.isFinite(v) || v<1 || v>65535)) el.classList.add('invalid'); }
  }
  const msgBox = qs('op-msg'); if(msgBox){ if(show) msgBox.innerHTML='<span style="color:#e74c3c">请先补全必填字段</span>'; else msgBox.textContent=''; }
}

async function loadNodes() {
  if (!token) { return; }
  const tbody = document.querySelector('#nodes-table tbody');
  tbody.innerHTML = '';
  const nodes = await api('/api/nodes');
  window.__nodes_cache = nodes; // 缓存用于编辑
  for (const n of nodes) {
    const tr = document.createElement('tr');
    const approveState = n.is_approved ? pill('已审批','ok') : pill('未审批','warn');
    const activeState = n.is_active ? pill('启用','ok') : pill('停用','bad');
    const publicState = n.is_public ? pill('公开','ok') : pill('私有','warn');

    const approveBtns = n.is_approved
      ? `<button data-id="${n.id}" class="btn-reject ghost">撤销</button>`
      : `<button data-id="${n.id}" class="btn-approve">审批</button>`;
    const activeBtns = n.is_active
      ? `<button data-id="${n.id}" class="btn-deactivate ghost">停用</button>`
      : `<button data-id="${n.id}" class="btn-activate">启用</button>`;
    const publicBtns = n.is_public
      ? `<button data-id="${n.id}" class="btn-private ghost">设私有</button>`
      : `<button data-id="${n.id}" class="btn-public">设公开</button>`;

    tr.innerHTML = `
      <td>${n.id}</td>
      <td>${esc(n.name)}</td>
      <td>${esc(n.host)}:${n.port}</td>
      <td>${esc(n.protocol)}</td>
      <td>${n.version || ''}</td>
      <td class="num">${n.current_connections ?? ''}</td>
      <td>${approveState} ${approveBtns}</td>
      <td>${activeState} ${activeBtns}</td>
      <td>${publicState} ${publicBtns}</td>
      <td>
        <button data-id="${n.id}" class="btn-edit ghost">编辑</button>
        <button data-id="${n.id}" class="btn-health ghost">健康</button>
        <button data-id="${n.id}" class="btn-delete ghost">删除</button>
      </td>
    `;
    tbody.appendChild(tr);
  }
}

function enterEdit(node){ editingId = node.id; qs('name').value = node.name || ''; qs('protocol').value = node.protocol || 'tcp'; qs('host').value = node.host || ''; qs('port').value = node.port || 11010; qs('network_name').value = node.network_name || ''; qs('network_secret').value = node.network_secret || ''; qs('max_connections').value = node.max_connections ?? 0; qs('allow_relay').checked = !!node.allow_relay; qs('description').value = node.description || ''; const pub = !!node.is_public; setSeg(pub); qs('is_public').checked = pub; const addBtn = qs('btn-add'); addBtn.textContent = '保存修改'; updateAddButton(); highlightInvalid(false); toast(`编辑节点 #${node.id}`,'ok'); }
function cancelEdit(){ editingId = null; resetForm(); const addBtn = qs('btn-add'); addBtn.textContent = '添加节点'; }

async function updateNode(){ if(!editingId){ return; } parseHostPort(); if(!formValid()){ highlightInvalid(true); toast('请完整填写表单','error'); return; } const payload = { name: qs('name').value.trim(), protocol: qs('protocol').value, host: qs('host').value.trim(), port: Number(qs('port').value), network_name: qs('network_name').value.trim(), network_secret: qs('network_secret').value.trim(), max_connections: Number(qs('max_connections').value || 0), allow_relay: qs('allow_relay').checked, is_public: qs('is_public').checked, description: qs('description').value.trim() }; try{ await api(`/api/nodes/${editingId}`, { method:'PUT', body: JSON.stringify(payload) }); toast('已保存','ok'); cancelEdit(); await loadNodes(); }catch(err){ toast(err.message||err,'error'); }}

async function addNode(){ if(editingId){ return updateNode(); } if (!token) { toast('请先登录','error'); return; } parseHostPort(); if (!formValid()) { highlightInvalid(true); toast('请补全必填字段','error'); return; } const payload = { name: qs('name').value.trim(), protocol: qs('protocol').value, host: qs('host').value.trim(), port: Number(qs('port').value), network_name: qs('network_name').value.trim(), network_secret: qs('network_secret').value.trim(), max_connections: Number(qs('max_connections').value || 0), allow_relay: qs('allow_relay').checked, is_public: qs('is_public').checked, description: qs('description').value.trim() }; try { const res = await api('/api/nodes', { method: 'POST', body: JSON.stringify(payload) }); qs('op-msg').textContent = `已创建：ID ${res.id}`; toast('节点已创建','ok'); resetForm(); await loadNodes(); } catch(err){ toast(err.message||err,'error'); } }

function bindTableActions(){ const table = document.querySelector('#nodes-table'); table.addEventListener('click', async (e)=>{ const btn = e.target; if(!(btn instanceof HTMLButtonElement)) return; const id = Number(btn.getAttribute('data-id')); if(!id) return; try{ if (btn.classList.contains('btn-edit')){ const node = (window.__nodes_cache||[]).find(n=>n.id===id); if(node) enterEdit(node); return; } if (btn.classList.contains('btn-approve')){ await api(`/api/nodes/${id}/approve`, { method:'POST' }); toast('已审批','ok'); } else if (btn.classList.contains('btn-reject')){ await api(`/api/nodes/${id}/reject`, { method:'POST' }); toast('已撤销审批','warn'); } else if (btn.classList.contains('btn-activate')){ await api(`/api/nodes/${id}/activate`, { method:'POST' }); toast('已启用','ok'); } else if (btn.classList.contains('btn-deactivate')){ await api(`/api/nodes/${id}/deactivate`, { method:'POST' }); toast('已停用','warn'); } else if (btn.classList.contains('btn-public')){ await api(`/api/nodes/${id}/visibility`, { method:'PUT', body: JSON.stringify({ is_public:true }) }); toast('已设为公开','ok'); } else if (btn.classList.contains('btn-private')){ await api(`/api/nodes/${id}/visibility`, { method:'PUT', body: JSON.stringify({ is_public:false }) }); toast('已设为私有','warn'); } else if (btn.classList.contains('btn-health')){ const stats = await api(`/api/nodes/${id}/health/stats`); const records = await api(`/api/nodes/${id}/health`); showNodeDetails(id, stats, records); } else if (btn.classList.contains('btn-delete')){ if(!confirm('确认删除该节点？')) return; await api(`/api/nodes/${id}`, { method:'DELETE' }); toast('已删除','warn'); } await loadNodes(); }catch(err){ toast(err.message||err,'error'); }}); }

function showNodeDetails(id, stats, records){ const detail = qs('node-details'); if(!detail) return; show(detail); const node = (window.__nodes_cache||[]).find(n=>n.id===id); qs('detail-title').textContent = `#${id} ${node?node.name:''}`; const statsBox = qs('stats'); statsBox.innerHTML = ''; const avgRespMs = (stats.average_response_time||0)/1000.0; const kpiData = [ { label:'总检查次数', value: stats.total_checks }, { label:'健康次数', value: stats.healthy_count }, { label:'不健康次数', value: stats.unhealthy_count }, { label:'健康率(%)', value: (stats.health_percentage||0).toFixed(2) }, { label:'平均响应(ms)', value: avgRespMs.toFixed(1) } ]; for(const k of kpiData){ const div=document.createElement('div'); div.innerHTML = `<div style="font-size:1.4rem;font-weight:600">${esc(k.value)}</div><div class="small muted">${esc(k.label)}</div>`; statsBox.appendChild(div); } const tbody = qs('health-table').querySelector('tbody'); tbody.innerHTML=''; records.forEach(r=>{ const tr=document.createElement('tr'); const statusCls = r.status==='Healthy' ? 'status-online' : 'status-offline'; const rtMs = r.response_time_ms ?? r.response_time ?? r.ResponseTime; const conn = r.connection_count ?? r.ConnectionCount ?? ''; tr.innerHTML = `<td>${formatLocalTime(r.checked_at)||formatLocalTime(r.CheckedAt)}</td><td><span class="badge ${statusCls}">${esc(r.status||r.Status)}</span></td><td class="num">${esc(conn)}</td><td class="num">${esc(rtMs)}</td><td>${esc(r.error_message??r.ErrorMessage)}</td>`; tbody.appendChild(tr); }); if(records.length===0){ const tr=document.createElement('tr'); tr.innerHTML = '<td colspan="5" class="muted">暂无记录</td>'; tbody.appendChild(tr); }
}

async function testConnection(){
  parseHostPort();
  if(!formValid()){ highlightInvalid(true); toast('请先填写必要字段','error'); return; }
  const payload = { name: qs('name').value.trim(), protocol: qs('protocol').value, host: qs('host').value.trim(), port: Number(qs('port').value), network_name: qs('network_name').value.trim(), network_secret: qs('network_secret').value.trim(), max_connections: Number(qs('max_connections').value||0), allow_relay: qs('allow_relay').checked, is_public: qs('is_public').checked, description: qs('description').value.trim() };
  const btn = qs('btn-test');
  try{
    if (btn){ btn.disabled = true; btn.innerHTML = '<span class="loading" style="vertical-align:middle"></span> 测试中...'; }
    showLoading('正在测试连通性，请稍候...');
    const res = await api('/api/test_connection', { method:'POST', body: JSON.stringify(payload) });
    const box = qs('test-result');
    box.classList.remove('hidden');
    box.innerHTML = `<div><b>状态:</b> ${esc(res.status)} | <b>响应(us):</b> ${esc(res.response_time)} | <b>版本:</b> ${esc(res.version||'')}</div><div><b>当前连接:</b> ${esc(res.conn_count)} ${res.error_message?('| <b>错误:</b> '+esc(res.error_message)):''}</div>`;
    toast('测试完成','ok');
  }catch(err){
    toast(err.message||err,'error');
  }finally{
    hideLoading();
    if (btn){ btn.disabled = false; btn.textContent = '测试连通性'; }
  }
}

function resetForm(){ qs('node-form').reset(); qs('port').value='11010'; qs('test-result').classList.add('hidden'); qs('test-result').textContent=''; updateAddButton(); setSeg(false); highlightInvalid(false); if(editingId) cancelEdit(); }

function wire(){ const logoutBtn = qs('btn-logout'); if(logoutBtn) logoutBtn.addEventListener('click', ()=>{ logout(); }); const addBtn = qs('btn-add'); if(addBtn) addBtn.addEventListener('click', ()=> addNode().catch(err=> toast(err.message,'error'))); const testBtn = qs('btn-test'); if(testBtn) testBtn.addEventListener('click', ()=> testConnection().catch(err=> toast(err.message,'error'))); const resetBtn = qs('btn-reset'); if(resetBtn) resetBtn.addEventListener('click', resetForm); ['name','host','port','network_name','network_secret','protocol','max_connections','allow_relay','is_public','description'].forEach(id=>{ const el = qs(id); if(el) el.addEventListener('input', updateAddButton); }); wireSeg(); setSeg(false); bindTableActions(); updateAuthUi(); refreshApiHealth(); loadNodes(); }

window.addEventListener('DOMContentLoaded', wire);
