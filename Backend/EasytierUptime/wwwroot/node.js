function qs(id){return document.getElementById(id);}function esc(s){return String(s??'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;').replace(/'/g,'&#39;');}
function parseUtcDate(v){ if(!v) return new Date(NaN); let str=String(v).trim(); if(/^[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}(:[0-9]{2})?(\.[0-9]+)?$/.test(str)){ str=str.replace(' ','T'); if(!/[Zz]$/.test(str)) str+='Z'; } else if(/^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}(:[0-9]{2})?(\.[0-9]+)?$/.test(str) && !/[Zz]$/.test(str)){ str+='Z'; } return new Date(str); }
function fmt(v){ const d=parseUtcDate(v); if(isNaN(d)) return String(v||''); const p=n=>String(n).padStart(2,'0'); return `${d.getFullYear()}-${p(d.getMonth()+1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`; }
function badge(status){ const s=(status||'Unknown').toLowerCase(); let cls='status-unknown'; if(s==='healthy') cls='status-healthy'; else if(['unhealthy','down','error'].includes(s)) cls='status-unhealthy'; return `<span class="badge ${cls}">${esc(status||'Unknown')}</span>`; }
function pct(v){ const x=Math.max(0, Math.min(100, Number(v)||0)); return x; }

function getQueryId(){ const u=new URL(location.href); return Number(u.searchParams.get('id'))||0; }

async function loadBasic(id){
  const token=localStorage.getItem('token')||''; const headers= token? { 'Authorization': `Bearer ${token}` } : {};
  const res=await fetch(`/api/public/nodes/${id}`, { headers });
  const card=qs('card-basic');
  if(res.status===404){ card.innerHTML='<div class="muted">未找到该节点或不可公开访问。<a class="btn btn-sm" href="/">返回首页</a></div>'; return null; }
  if(!res.ok) throw new Error('加载节点信息失败');
  const n=await res.json();

  // Head
  const head=qs('detail-head');
  const title = esc(n.name||'');
  const addr = `${esc(n.host||'')}:${esc(n.port||'')}`;
  head.innerHTML = `
    <div class="detail-title">
      <div class="name">${title}</div>
      <div>${badge(n.current_health_status)}</div>
    </div>
    <div class="subtitle">协议: ${esc(n.protocol||'')} · 地址: ${addr} · ID: ${esc(n.id||n.server_id||'')}</div>
  `;

  // KV grid
  const kv=qs('kv');
  const cur=Number(n.current_connections)||0; const max=Number(n.max_connections)||0;
  const use = max>0 ? (cur*100.0/max) : 0;
  const usePct = pct(use).toFixed(1);
  const last = n.last_checked_at || n.last_check_time || n.updated_at;
  const rtUs = Number(n.last_response_time)||0; const rtMs = rtUs? (rtUs/1000.0).toFixed(1)+' ms' : '--';
  const items = [
    {label:'版本', value: esc(n.version||'-') },
    {label:'连接', value: `${cur}/${max}` },
    {label:'负载', value: `${usePct}%`, bar:true },
    {label:'允许中继', value: (Number(n.allow_relay)===1?'是':'否') },
    {label:'最近检测', value: esc(fmt(last)||'-') },
    {label:'响应(ms)', value: esc(rtMs) },
  ];
  kv.innerHTML = items.map(x=>`
    <div class="kv-item">
      <div class="label">${x.label}</div>
      <div class="value">${x.value}</div>
      ${x.bar? `<div class="progress" style="margin-top:6px"><div class="bar" style="width:${pct(use)}%"></div></div>`:''}
    </div>
  `).join('');

  // Tags
  const tg=qs('tags');
  const tags = Array.isArray(n.tags)? n.tags : [];
  tg.innerHTML = tags.length? tags.map(t=>`<span class="chip">${esc(t)}</span>`).join('') : '<span class="muted">无标签</span>';

  // Desc
  const desc=qs('desc');
  desc.textContent = n.description || '';

  return n;
}

async function loadStats(id){ const res=await fetch(`/api/nodes/${id}/health/stats`); if(!res.ok) throw new Error('加载统计失败'); const s=await res.json(); const avg=(s.average_response_time||0)/1000.0; const kpis=[
  {k:'总检查次数', v:s.total_checks},
  {k:'健康次数', v:s.healthy_count},
  {k:'不健康次数', v:s.unhealthy_count},
  {k:'健康率(%)', v:(s.health_percentage||0).toFixed(2)},
  {k:'平均响应(ms)', v:avg.toFixed(1)},
]; const box=qs('stats'); box.innerHTML=''; for(const x of kpis){ const div=document.createElement('div'); div.className='kpi'; div.innerHTML=`<div style="font-size:1.2rem;font-weight:600">${esc(x.v)}</div><div class="small">${esc(x.k)}</div>`; box.appendChild(div);} }

async function loadRecords(id){ const res=await fetch(`/api/nodes/${id}/health`); if(!res.ok) throw new Error('加载记录失败'); const list=await res.json(); const tbody=qs('records'); tbody.innerHTML=''; if(!list || list.length===0){ const tr=document.createElement('tr'); tr.innerHTML='<td colspan="5" class="muted">暂无记录</td>'; tbody.appendChild(tr); return; } for(const r of list){ const when=r.checked_at||r.CheckedAt; const status=(r.status||r.Status); const cls=status==='Healthy'?'status-healthy':'status-unhealthy'; const rt=r.response_time_ms ?? r.response_time ?? r.ResponseTime; const conn=r.connection_count ?? r.ConnectionCount ?? ''; const tr=document.createElement('tr'); tr.innerHTML=`<td>${esc(fmt(when))}</td><td><span class="badge ${cls}">${esc(status)}</span></td><td>${esc(conn)}</td><td>${esc(rt)}</td><td>${esc(r.error_message||r.ErrorMessage||'')}</td>`; tbody.appendChild(tr); }
}

async function init(){ const id=getQueryId(); const head=qs('detail-head'); if(!id){ head.innerHTML='<div class="muted">缺少 id 参数</div>'; return; } const n=await loadBasic(id); if(n){ document.title = `节点 #${id} - ${n.name || ''} - EasyTier Uptime`; } await Promise.allSettled([loadStats(id), loadRecords(id)]); }

window.addEventListener('DOMContentLoaded', init);
