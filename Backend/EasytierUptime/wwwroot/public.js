/* Shared util for both pages */
function qs(id) { return document.getElementById(id); }
function toast(msg, cls=''){
  const box = qs('toast'); if(!box) return;
  const el = document.createElement('div'); el.className = `toast ${cls}`; el.textContent = msg;
  box.appendChild(el); setTimeout(()=>{ el.remove(); }, 3500);
}
function esc(s){ return String(s??'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;').replace(/'/g,'&#39;'); }
function parseUtcDate(v){ if(!v) return new Date(NaN); let str=String(v).trim(); if(/^[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}(:[0-9]{2})?(\.[0-9]+)?$/.test(str)){ str=str.replace(' ','T'); if(!/[Zz]$/.test(str)) str+='Z'; } else if(/^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}(:[0-9]{2})?(\.[0-9]+)?$/.test(str) && !/[Zz]$/.test(str)){ str+='Z'; } return new Date(str); }
function formatLocalTime(v){ if(!v) return ''; const d=parseUtcDate(v); if(isNaN(d)) return String(v); const pad=n=>n.toString().padStart(2,'0'); return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`; }
function tzInfo(){ const mins=-new Date().getTimezoneOffset(); const sign=mins>=0?'+':'-'; const abs=Math.abs(mins); const hh=String(Math.trunc(abs/60)).padStart(2,'0'); const mm=String(abs%60).padStart(2,'0'); const tz=Intl.DateTimeFormat().resolvedOptions().timeZone||''; return { offset:`UTC${sign}${hh}:${mm}`, name:tz }; }

let state={ rows:[], pagination:null };
let refreshTimer=null; const REFRESH_MS=180000;
async function fetchPublicNodes(page=1, perPage=20){
  const token = localStorage.getItem('token') || '';
  const headers = token ? { 'Authorization': `Bearer ${token}` } : {};
  const res=await fetch(`/api/public/nodes?page=${page}&per_page=${perPage}`, { cache:'no-store', headers });
  if(!res.ok) throw new Error('加载公开节点失败');
  const json=await res.json();
  if(!json || json.success!==true || !json.data || !Array.isArray(json.data.items)) throw new Error('返回数据格式不正确');
  state.pagination={ total: Number(json.data.total)||json.data.items.length, page: Number(json.data.page)||page, per_page: Number(json.data.per_page)||perPage, total_pages: Number(json.data.total_pages)||1 };
  const last=qs('last-updated');
  if(last){ const t=formatLocalTime(json.latest_update); const {offset,name}=tzInfo(); last.textContent=`本地时间: ${t} (${offset}${name?` · ${name}`:''})`; last.title=`原始(UTC): ${json.latest_update}`; }
  return json.data.items;
}
function badge(status){ const s=(status||'Unknown').toLowerCase(); let cls='status-unknown'; if(s==='healthy') cls='status-healthy'; else if(['unhealthy','down','error'].includes(s)) cls='status-unhealthy'; return `<span class="badge ${cls}">${esc(status||'Unknown')}</span>`; }
function sortByIdAsc(rows){ rows.sort((a,b)=>{ const ai=Number(a.id||a.server_id)||0; const bi=Number(b.id||b.server_id)||0; return ai-bi; }); }
function renderCards(){ const wrap=document.getElementById('cards'); if(!wrap) return; wrap.innerHTML=''; const rows=[...state.rows]; sortByIdAsc(rows); for(const n of rows){ const item=document.createElement('div'); item.className='card-item'; const displayTime=n.last_checked_at||n.updated_at; const time=displayTime?formatLocalTime(displayTime):''; const fullDesc=(n.description||''); const tags=Array.isArray(n.tags)&&n.tags.length?`<div class="card-tags small">${n.tags.map(t=>`<span class="tag">${esc(t)}</span>`).join('')}</div>`:''; const rtUs=Number(n.last_response_time)||0; const rtMs=rtUs? (rtUs/1000.0).toFixed(1)+' ms':'--'; const addr=`${esc(n.host||'')}:${esc(n.port||'')}`; const idStr = esc(n.id||n.server_id||''); const idLine = idStr ? `<div class="line">ID: ${idStr}</div>` : ''; const relay = Number(n.allow_relay)===1 ? '允许' : '不允许'; const detailBtn = `<a class="btn" href="/node.html?id=${n.id||n.server_id}">详情</a>`;
  item.innerHTML=`<div class="card-head"><div class="title">${esc(n.name||'')}</div><div class="status">${badge(n.current_health_status)}</div></div>${tags}<div class="card-lines small">${idLine}<div class="line">协议: ${esc(n.protocol||'')}</div><div class="line">地址: ${addr}</div><div class="line">中继: ${relay}</div><div class="line">负载: ${esc(n.load_text||'')}</div><div class="line">连接: ${Number(n.current_connections)||0}/${Number(n.max_connections)||0}</div><div class="line" title="UTC: ${esc(displayTime||'')}">检测: ${time}</div><div class="line">延迟: ${rtMs}</div></div><div class="card-desc"><div class="desc-full">${esc(fullDesc)}</div></div><div class="card-actions">${detailBtn}</div>`; wrap.appendChild(item);} }
function renderKpis(rows){ const total=(state.pagination?.total ?? rows.length) || 0; const elCount=qs('kpi-count'); if(elCount) elCount.textContent=String(total); const totalConn=rows.reduce((a,b)=>a+(Number(b.current_connections)||0),0); const elConn=qs('kpi-conns'); if(elConn) elConn.textContent=String(totalConn); const active=rows.filter(r=>Number(r.is_active)===1).length; const elHealth=qs('kpi-health'); if(elHealth) elHealth.textContent = rows.length? `${Math.round(active/rows.length*100)}%` : '-'; }
function renderPublic(){ renderCards(); renderPagination(); updateAuthUiPublic(); }
function renderPagination(){ const box=qs('pagination'); if(!box) return; const p=state.pagination; if(!p){ box.innerHTML=''; return; } const {page,total_pages}=p; const makeBtn=(txt, targetPage, disabled=false)=>`<button class=\"pg-btn\" data-pg=\"${targetPage}\" ${disabled?'disabled':''}>${txt}</button>`; let html=''; html+=makeBtn('«',1,page<=1); html+=makeBtn('‹',page-1,page<=1); html+=`<span class=\"pg-info\">第 ${page} / ${total_pages} 页</span>`; html+=makeBtn('›',page+1,page>=total_pages); html+=makeBtn('»',total_pages,page>=total_pages); box.innerHTML=html; box.onclick=e=>{ const btn=e.target.closest('.pg-btn'); if(!btn) return; const pg=Number(btn.getAttribute('data-pg')); if(!pg || pg===state.pagination.page) return; loadPage(pg); };
}
function updateAuthUiPublic(){ const token = localStorage.getItem('token')||''; const role = localStorage.getItem('role')||''; const username = localStorage.getItem('username')||''; const info = document.getElementById('login-info'); const link = document.getElementById('login-link'); if(token){ if(info) info.classList.remove('hidden'); if(link) link.classList.add('hidden'); const nm = document.getElementById('login-name'); const rl = document.getElementById('login-role'); if(nm) nm.textContent = username; if(rl) rl.textContent = role; const btn = document.getElementById('btn-logout'); if(btn && !btn._wired){ btn._wired=true; btn.addEventListener('click', ()=>{ localStorage.removeItem('token'); localStorage.removeItem('role'); localStorage.removeItem('username'); location.reload(); }); } } else { if(info) info.classList.add('hidden'); if(link) link.classList.remove('hidden'); } }
async function loadPage(pg){ try{ const rows=await fetchPublicNodes(pg,state.pagination?.per_page||20); state.rows=rows; renderKpis(rows); renderPublic(); }catch(err){ console.error(err); } }
function startAutoRefresh(){ if(refreshTimer) clearInterval(refreshTimer); refreshTimer=setInterval(()=>{ loadPage(state.pagination?.page||1); }, REFRESH_MS); }
async function init(){ await loadPage(1); startAutoRefresh(); }
window.addEventListener('DOMContentLoaded', init);
