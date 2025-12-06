let token = localStorage.getItem('token') || '';
function authHeader() { return token ? { 'Authorization': `Bearer ${token}` } : {}; }
function qs(id){ return document.getElementById(id); }
function toast(msg, cls=''){ const box=document.getElementById('toast'); if(!box) return; const el=document.createElement('div'); el.className=`toast ${cls}`; el.textContent=msg; box.appendChild(el); setTimeout(()=>el.remove(),3200); }
async function api(path, opts={}){ const headers={ 'Content-Type':'application/json', ...authHeader(), ...(opts.headers||{}) }; const res=await fetch(path,{...opts, headers}); if(res.status===401){ location.href=`/auth/?back=${encodeURIComponent(location.pathname)}`; return; } if(!res.ok){ let msg=''; try{ const t=await res.json(); msg=t.message||JSON.stringify(t); } catch{ msg=await res.text(); } throw new Error(msg||`HTTP ${res.status}`); } if(res.status===204) return null; return await res.json(); }

async function loadUsers(){
  const tbody = document.querySelector('#users-table tbody');
  tbody.innerHTML='';
  const users = await api('/api/users');
  if(!users) return;
  for(const u of users){
    const tr = document.createElement('tr');
    const emailCell = u.email ? `<span title="${u.email}">${u.email}</span>` : '<span class="small" style="color:#888">(无)</span>';
    tr.innerHTML = `
      <td>${u.id}</td>
      <td>${u.username}</td>
      <td>${emailCell}</td>
      <td>${u.role}</td>
      <td>${u.created_at}</td>
      <td>
        <button class="btn-del" data-id="${u.id}">删除</button>
        <button class="btn-role" data-id="${u.id}">改角色</button>
        <button class="btn-pass" data-id="${u.id}">改密码</button>
      </td>
    `;
    tbody.appendChild(tr);
  }
}

async function createUser(){
  const username = qs('new-username').value.trim();
  const password = qs('new-password').value.trim();
  const role = qs('new-role').value;
  const email = qs('new-email').value.trim();
  if(!username || !password){ toast('请输入用户名和密码','bad'); return; }
  try{
    await api('/api/users', { method:'POST', body: JSON.stringify({ username, password, role, email: email || null }) });
    toast('创建成功');
    qs('new-username').value='';
    qs('new-password').value='';
    qs('new-email').value='';
    await loadUsers();
  }catch(err){ toast(err.message,'bad'); }
}

function bindActions(){
  document.querySelector('#users-table').addEventListener('click', async (e) => {
    const btn = e.target; if(!(btn instanceof HTMLButtonElement)) return;
    const id = Number(btn.getAttribute('data-id'));
    try{
      if(btn.classList.contains('btn-del')){
        if(!confirm('确认删除该用户？')) return;
        await api(`/api/users/${id}`, { method: 'DELETE' });
        toast('已删除');
      } else if(btn.classList.contains('btn-role')){
        const role = prompt('输入角色：user/admin'); if(!role) return;
        await api(`/api/users/${id}/role`, { method: 'PUT', body: JSON.stringify({ role }) });
        toast('角色已更新');
      } else if(btn.classList.contains('btn-pass')){
        const password = prompt('输入新密码'); if(!password) return;
        await api(`/api/users/${id}/password`, { method: 'PUT', body: JSON.stringify({ password }) });
        toast('密码已更新');
      }
      await loadUsers();
    }catch(err){ toast(err.message||err,'bad'); }
  });
}

function wire(){
  const btn = qs('btn-create'); if(btn) btn.addEventListener('click', ()=> createUser());
  loadUsers();
  bindActions();
}

window.addEventListener('DOMContentLoaded', wire);
