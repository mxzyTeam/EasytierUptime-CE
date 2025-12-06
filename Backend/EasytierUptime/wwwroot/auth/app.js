let token = localStorage.getItem('token') || '';
let role = localStorage.getItem('role') || '';
let username = localStorage.getItem('username') || '';
let captchaId = '';
let loginCaptchaId = '';
let sendCooldown = 0;
let sendTimer = null;

function qs(id) { return document.getElementById(id); }
function show(el) { el.classList.remove('hidden'); }
function hide(el) { el.classList.add('hidden'); }
function toast(msg, cls=''){
  const box = document.getElementById('toast'); if(!box) return;
  const el = document.createElement('div'); el.className = `toast ${cls}`; el.textContent = msg;
  box.appendChild(el); setTimeout(()=>{ el.remove(); }, 3200);
}

async function api(path, opts = {}) {
  const headers = { 'Content-Type': 'application/json', ...(opts.headers || {}) };
  const res = await fetch(path, { ...opts, headers });
  if (!res.ok) {
    let msg = '';
    try { const t = await res.json(); msg = t.message || JSON.stringify(t); } catch { msg = await res.text(); }
    throw new Error(msg || `HTTP ${res.status}`);
  }
  if (res.status === 204) return null;
  return await res.json();
}

async function loadCaptcha(target){
  try {
    const data = await api('/auth/captcha');
    if(target === 'login'){
      loginCaptchaId = data.captcha_id;
      const imgEl = qs('login-captcha-img');
      if (data.image_base64?.startsWith('data:')) imgEl.src = data.image_base64; else imgEl.src = 'data:image/png;base64,' + (data.image_base64 || '');
    } else {
      captchaId = data.captcha_id;
      const imgEl = qs('captcha-img');
      if (data.image_base64?.startsWith('data:')) imgEl.src = data.image_base64; else imgEl.src = 'data:image/png;base64,' + (data.image_base64 || '');
    }
  } catch (e){ toast('获取验证码失败','bad'); }
}

async function doLogin() {
  const u = qs('login-username').value.trim();
  const p = qs('login-password').value.trim();
  const cap = qs('login-captcha-code').value.trim();
  if (!u || !p) { toast('请输入用户名和密码','bad'); return; }
  if (!loginCaptchaId || !cap){ toast('请输入图形验证码','bad'); return; }
  try{
    const res = await api('/auth/login', { method: 'POST', body: JSON.stringify({ username: u, password: p, captcha_id: loginCaptchaId, captcha_code: cap }) });
    token = res.access_token; role = res.role || ''; username = u;
    localStorage.setItem('token', token);
    localStorage.setItem('role', role);
    localStorage.setItem('username', username);
    toast('登录成功');
    const back = new URLSearchParams(location.search).get('back') || '/admin/';
    location.href = back;
  }catch(err){
    toast(err.message,'bad');
    loadCaptcha('login');
    qs('login-captcha-code').value = '';
  }
}

function startSendCooldown(sec = 60){
  sendCooldown = sec;
  const btn = qs('btn-send-code');
  btn.disabled = true;
  btn.textContent = `${sendCooldown}s 后可重发`;
  if (sendTimer) clearInterval(sendTimer);
  sendTimer = setInterval(() => {
    sendCooldown--;
    if (sendCooldown <= 0){
      clearInterval(sendTimer); sendTimer = null;
      btn.disabled = false; btn.textContent = '发送邮箱验证码';
    } else {
      btn.textContent = `${sendCooldown}s 后可重发`;
    }
  }, 1000);
}

async function sendCode(){
  const email = qs('code-email').value.trim();
  const cap = qs('captcha-code').value.trim();
  if(!email){ toast('请输入邮箱','bad'); return; }
  if(!captchaId || !cap){ toast('请输入图形验证码','bad'); return; }
  try{
    await api('/auth/send_code', { method: 'POST', body: JSON.stringify({ email, captcha_id: captchaId, captcha_code: cap }) });
    toast('邮箱验证码已发送');
    qs('send-hint').textContent = '已发送，10 分钟内有效';
    startSendCooldown(60);
  }catch(err){
    toast(err.message,'bad');
    loadCaptcha('register');
  }
}

function estimateStrength(pwd){
  let score = 0;
  if (pwd.length >= 6) score++;
  if (pwd.length >= 10) score++;
  if (/[A-Z]/.test(pwd)) score++;
  if (/[a-z]/.test(pwd)) score++;
  if (/\d/.test(pwd)) score++;
  if (/[^A-Za-z0-9]/.test(pwd)) score++;
  return Math.min(score, 5); // 0..5
}

function updateStrength(){
  const pwd = qs('code-password').value;
  const score = estimateStrength(pwd);
  const bar = qs('pwd-strength-bar');
  const text = qs('pwd-strength-text');
  const percent = [0,20,40,60,80,100][score];
  bar.style.width = percent + '%';
  const words = ['极弱','很弱','弱','一般','较强','强'];
  text.textContent = '强度：' + words[score];
}

async function registerWithCode(){
  const email = qs('code-email').value.trim();
  const code = qs('code-code').value.trim();
  const u = qs('code-username').value.trim();
  const p = qs('code-password').value.trim();
  if(!email || !code || !u || !p){ toast('请完整填写信息','bad'); return; }
  try{
    await api('/auth/register_with_code', { method: 'POST', body: JSON.stringify({ username: u, password: p, email, code }) });
    toast('注册成功，请完成登录');
    // switch to login tab and prepare captcha
    qs('tab-login').click();
    qs('login-username').value = u;
    qs('login-password').value = '';
    qs('login-captcha-code').value = '';
    await loadCaptcha('login');
    qs('login-password').focus();
  }catch(err){
    toast(err.message,'bad');
  }
}

function wire() {
  const tabLogin = qs('tab-login');
  const tabCode = qs('tab-code');
  tabLogin.addEventListener('click', () => {
    tabLogin.classList.add('active'); tabCode.classList.remove('active');
    show(qs('form-login')); hide(qs('form-code'));
    if(!loginCaptchaId) loadCaptcha('login');
  });
  tabCode.addEventListener('click', () => {
    tabCode.classList.add('active'); tabLogin.classList.remove('active');
    show(qs('form-code')); hide(qs('form-login'));
    if(!captchaId) loadCaptcha('register');
  });
  qs('btn-login').addEventListener('click', () => doLogin());
  qs('btn-send-code').addEventListener('click', () => sendCode());
  qs('btn-register-code').addEventListener('click', () => registerWithCode());
  qs('btn-refresh-captcha').addEventListener('click', () => { loadCaptcha('register'); qs('captcha-code').value=''; });
  qs('captcha-img').addEventListener('click', () => { loadCaptcha('register'); qs('captcha-code').value=''; });
  qs('btn-login-refresh-captcha').addEventListener('click', () => { loadCaptcha('login'); qs('login-captcha-code').value=''; });
  qs('login-captcha-img').addEventListener('click', () => { loadCaptcha('login'); qs('login-captcha-code').value=''; });

  // toggle password visibility
  qs('btn-toggle-login-pwd').addEventListener('click', () => {
    const input = qs('login-password');
    const t = input.type === 'password' ? 'text' : 'password';
    input.type = t; qs('btn-toggle-login-pwd').textContent = t==='password' ? '显示' : '隐藏';
  });
  qs('btn-toggle-reg-pwd').addEventListener('click', () => {
    const input = qs('code-password');
    const t = input.type === 'password' ? 'text' : 'password';
    input.type = t; qs('btn-toggle-reg-pwd').textContent = t==='password' ? '显示' : '隐藏';
  });
  qs('code-password').addEventListener('input', updateStrength);

  // Submit on Enter
  qs('form-login').addEventListener('keydown', (e)=>{ if(e.key==='Enter'){ e.preventDefault(); doLogin(); } });
  qs('form-code').addEventListener('keydown', (e)=>{ if(e.key==='Enter'){ e.preventDefault(); registerWithCode(); } });

  // initial
  loadCaptcha('login');
}

window.addEventListener('DOMContentLoaded', wire);
