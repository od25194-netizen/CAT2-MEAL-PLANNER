/**
 * My Meal Planner — Main JavaScript
 * Enhanced: scroll-to-top, reading progress, cookie consent,
 *           numeric notification badge, Mia pulse, lazy-load.
 */

document.addEventListener('DOMContentLoaded', () => {

  // ── Theme / Dark Mode ────────────────────────────────────────
  const themeToggle = document.getElementById('theme-toggle');
  const themeIcon   = document.getElementById('theme-icon');
  const htmlRoot    = document.getElementById('html-root');

  const savedTheme = localStorage.getItem('mmp-theme') || 'light';
  applyTheme(savedTheme);

  themeToggle?.addEventListener('click', () => {
    const next = htmlRoot.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
    applyTheme(next);
    localStorage.setItem('mmp-theme', next);
    fetch('/Account/SetTheme', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken() },
      body: JSON.stringify({ darkMode: next === 'dark' })
    }).catch(() => {});
  });

  function applyTheme(theme) {
    htmlRoot?.setAttribute('data-bs-theme', theme);
    if (themeIcon) themeIcon.className = theme === 'dark' ? 'bi bi-sun' : 'bi bi-moon-stars';
  }

  // ── Reading Progress Bar ──────────────────────────────────────
  const progressBar = document.getElementById('reading-progress');
  if (progressBar) {
    window.addEventListener('scroll', () => {
      const scrollTop  = window.scrollY;
      const docHeight  = document.documentElement.scrollHeight - window.innerHeight;
      progressBar.style.width = docHeight > 0 ? `${Math.min((scrollTop / docHeight) * 100, 100)}%` : '0%';
    }, { passive: true });
  }

  // ── Scroll-To-Top Button ──────────────────────────────────────
  const scrollTopBtn = document.getElementById('scroll-top-btn');
  if (scrollTopBtn) {
    window.addEventListener('scroll', () => {
      scrollTopBtn.classList.toggle('visible', window.scrollY > 400);
    }, { passive: true });
    scrollTopBtn.addEventListener('click', () => window.scrollTo({ top: 0, behavior: 'smooth' }));
  }

  // ── Cookie Consent ────────────────────────────────────────────
  const cookieBanner   = document.getElementById('cookie-banner');
  const cookieAccept   = document.getElementById('cookie-accept');
  const cookieSettings = document.getElementById('cookie-settings');

  if (cookieBanner && !localStorage.getItem('mmp-cookies-accepted')) {
    setTimeout(() => cookieBanner.classList.add('visible'), 1500);
  }
  cookieAccept?.addEventListener('click', () => {
    localStorage.setItem('mmp-cookies-accepted', 'all');
    cookieBanner.classList.remove('visible');
    showToast('Cookie preferences saved 🍪');
  });
  cookieSettings?.addEventListener('click', () => {
    localStorage.setItem('mmp-cookies-accepted', 'essential');
    cookieBanner.classList.remove('visible');
    showToast('Only essential cookies will be used.');
  });

  // ── Mia Pulse (first session visit) ──────────────────────────
  const miaToggle = document.getElementById('mia-toggle');
  if (miaToggle && !sessionStorage.getItem('mmp-mia-shown')) {
    setTimeout(() => {
      miaToggle.classList.add('pulse');
      miaToggle.addEventListener('animationend', () => miaToggle.classList.remove('pulse'), { once: true });
      sessionStorage.setItem('mmp-mia-shown', '1');
    }, 10000);
  }

  // ── Search Overlay ───────────────────────────────────────────
  const searchToggle  = document.getElementById('search-toggle');
  const searchOverlay = document.getElementById('search-overlay');
  const searchInput   = document.getElementById('global-search-input');
  const searchClose   = document.getElementById('search-close');
  const searchResults = document.getElementById('search-results');

  searchToggle?.addEventListener('click', () => { searchOverlay?.classList.add('visible'); searchInput?.focus(); });
  searchClose?.addEventListener('click',  () => searchOverlay?.classList.remove('visible'));

  document.addEventListener('keydown', e => {
    if (e.key === 'Escape') searchOverlay?.classList.remove('visible');
    if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
      e.preventDefault();
      searchOverlay?.classList.add('visible');
      searchInput?.focus();
    }
  });

  searchOverlay?.addEventListener('click', e => {
    if (e.target === searchOverlay) searchOverlay.classList.remove('visible');
  });

  // Search pill suggestions
  document.querySelectorAll('.search-pill').forEach(pill => {
    pill.addEventListener('click', () => {
      if (searchInput) searchInput.value = pill.textContent.trim().replace(/^[\S]+\s/, '');
      triggerSearch();
    });
  });

  // Live search debounce
  let searchTimeout;
  searchInput?.addEventListener('input', () => {
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
      searchInput.value.trim().length >= 2 ? triggerSearch() : (searchResults && (searchResults.innerHTML = ''));
    }, 300);
  });

  searchInput?.addEventListener('keydown', e => {
    if (e.key === 'Enter') { e.preventDefault(); window.location.href = `/Recipe?q=${encodeURIComponent(searchInput.value)}`; }
  });

  function triggerSearch() {
    const q = searchInput?.value.trim();
    if (!q || !searchResults) return;
    searchResults.innerHTML = '<div class="p-3 text-muted small"><i class="bi bi-hourglass-split me-1"></i>Searching…</div>';
    fetch(`/api/search?q=${encodeURIComponent(q)}&limit=6`)
      .then(r => r.json())
      .then(data => {
        if (!data.length) { searchResults.innerHTML = `<div class="p-3 text-muted small">No results found for <strong>${escHtml(q)}</strong></div>`; return; }
        searchResults.innerHTML = data.map(item => `
          <a href="/Recipe/Details/${item.id}/${item.slug}" class="d-flex gap-3 p-3 border-bottom text-decoration-none align-items-center">
            <div style="width:48px;height:48px;border-radius:8px;overflow:hidden;flex-shrink:0;background:var(--light-2)">
              ${item.coverImageUrl ? `<img src="${item.coverImageUrl}" style="width:100%;height:100%;object-fit:cover" loading="lazy">` : '<div style="width:100%;height:100%;display:flex;align-items:center;justify-content:center;font-size:1.5rem">🍽️</div>'}
            </div>
            <div style="flex:1;min-width:0">
              <div style="font-weight:600;color:var(--dark);font-size:.9rem;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${escHtml(item.title)}</div>
              <div style="font-size:.78rem;color:var(--mid)">${item.originCountry ? '🌍 ' + escHtml(item.originCountry) + ' · ' : ''}${item.mealType || ''}</div>
            </div>
            <i class="bi bi-chevron-right text-muted" style="font-size:.75rem;flex-shrink:0"></i>
          </a>`).join('')
          + `<a href="/Recipe?q=${encodeURIComponent(q)}" class="d-block p-2 text-center text-orange small fw-medium">See all results for "${escHtml(q)}" →</a>`;
      }).catch(() => { searchResults.innerHTML = ''; });
  }

  // ── Image Search ─────────────────────────────────────────────
  const imgSearchBtn   = document.getElementById('search-img-btn');
  const imgSearchInput = document.getElementById('search-img-input');
  imgSearchBtn?.addEventListener('click', () => imgSearchInput?.click());
  imgSearchInput?.addEventListener('change', async e => {
    const file = e.target.files?.[0];
    if (!file) return;
    if (searchResults) searchResults.innerHTML = '<div class="p-3 text-muted small">🔍 Identifying food in photo…</div>';
    const fd = new FormData();
    fd.append('image', file);
    try {
      const data = await (await fetch('/api/search/image', { method: 'POST', headers: { 'RequestVerificationToken': getAntiForgeryToken() }, body: fd })).json();
      if (data.identifiedDish) { if (searchInput) searchInput.value = data.identifiedDish; triggerSearch(); }
    } catch { if (searchResults) searchResults.innerHTML = '<div class="p-3 text-muted small">Could not identify food. Try another photo.</div>'; }
  });

  // ── Voice Search ─────────────────────────────────────────────
  const voiceBtn = document.getElementById('search-voice-btn');
  if ('webkitSpeechRecognition' in window || 'SpeechRecognition' in window) {
    const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    const recognition = new SR();
    recognition.continuous = false;
    recognition.lang = document.documentElement.lang || 'en-US';
    recognition.onresult = e => {
      const t = e.results[0][0].transcript;
      if (searchInput) { searchInput.value = t; triggerSearch(); }
      voiceBtn?.classList.remove('recording');
    };
    recognition.onerror = () => voiceBtn?.classList.remove('recording');
    voiceBtn?.addEventListener('click', () => { voiceBtn.classList.add('recording'); recognition.start(); });
  } else {
    voiceBtn?.setAttribute('disabled', 'true');
    voiceBtn?.setAttribute('title', 'Voice search not supported in this browser');
  }

  // ── Notification Panel ───────────────────────────────────────
  const notifToggle = document.getElementById('notif-toggle');
  const notifPanel  = document.getElementById('notif-panel');
  const notifBadge  = document.getElementById('notif-badge');
  const markAllBtn  = document.getElementById('mark-all-read');
  let   unreadCount = 0;

  notifToggle?.addEventListener('click', e => { e.stopPropagation(); notifPanel?.classList.toggle('visible'); });
  document.addEventListener('click', e => {
    if (notifPanel?.classList.contains('visible') && !notifPanel.contains(e.target) && e.target !== notifToggle)
      notifPanel.classList.remove('visible');
  });
  markAllBtn?.addEventListener('click', () => {
    notifHubConnection?.invoke('MarkAllRead').catch(console.error);
    document.querySelectorAll('.notif-item.unread').forEach(el => el.classList.remove('unread'));
    unreadCount = 0; updateBadge();
  });

  function updateBadge() {
    if (!notifBadge) return;
    if (unreadCount > 0) { notifBadge.textContent = unreadCount > 99 ? '99+' : unreadCount; notifBadge.style.display = 'flex'; }
    else notifBadge.style.display = 'none';
  }

  // ── SignalR — Notifications ───────────────────────────────────
  let notifHubConnection;
  if (document.body.dataset.authenticated === 'true') {
    notifHubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notifications').withAutomaticReconnect().build();
    notifHubConnection.on('NewNotification', payload => { prependNotification(payload); unreadCount++; updateBadge(); });
    notifHubConnection.on('UnreadCountUpdated', count => { unreadCount = count; updateBadge(); });
    notifHubConnection.start().catch(console.error);

    fetch('/api/notifications').then(r => r.json()).then(data => {
      if (data.length) {
        const list = document.getElementById('notif-list');
        if (list) { list.innerHTML = data.map(n => renderNotification(n)).join(''); unreadCount = data.filter(n => !n.isRead).length; updateBadge(); }
      }
    }).catch(() => {});
  }

  function renderNotification(n) {
    const icons = { DailyMealSuggestion:'🍽️', LevelUpAlert:'🌟', BadgeEarned:'🏅', QuizReadyToTake:'📝', NewRecipeFromFollowed:'👨‍🍳', SomeoneCommentedYours:'💬', TrendingInYourCountry:'🔥', HealthTipOfTheDay:'💚', NewJokeOfTheDay:'😂' };
    return `<div class="notif-item ${n.isRead ? '' : 'unread'}" data-id="${n.id}" onclick="markNotifRead(${n.id},'${n.actionUrl || ''}')">
      <span class="notif-icon">${icons[n.type] || '🔔'}</span>
      <div style="flex:1;min-width:0">
        <div style="font-size:.875rem;font-weight:600;color:var(--dark)">${escHtml(n.title)}</div>
        <div style="font-size:.8rem;color:var(--mid);overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${escHtml(n.body)}</div>
        <div style="font-size:.73rem;color:var(--mid);margin-top:.2rem">${timeAgo(n.createdAt)}</div>
      </div></div>`;
  }

  function prependNotification(n) {
    const list = document.getElementById('notif-list');
    if (!list) return;
    list.querySelector('.notif-empty')?.remove();
    list.insertAdjacentHTML('afterbegin', renderNotification(n));
  }

  window.markNotifRead = (id, url) => {
    notifHubConnection?.invoke('MarkNotificationRead', id).catch(console.error);
    const el = document.querySelector(`.notif-item[data-id="${id}"]`);
    if (el?.classList.contains('unread')) { el.classList.remove('unread'); unreadCount = Math.max(0, unreadCount - 1); updateBadge(); }
    if (url) window.location.href = url;
  };

  // ── Like Button ───────────────────────────────────────────────
  document.querySelectorAll('[data-like-recipe]').forEach(btn => {
    btn.addEventListener('click', async e => {
      e.preventDefault(); e.stopPropagation();
      try {
        const data = await (await fetch(`/Recipe/Like/${btn.dataset.likeRecipe}`, { method:'POST', headers:{'RequestVerificationToken': getAntiForgeryToken()} })).json();
        const icon = btn.querySelector('.like-icon'), count = btn.querySelector('.like-count');
        if (icon) icon.className = data.liked ? 'bi bi-heart-fill like-icon text-danger' : 'bi bi-heart like-icon';
        if (count) count.textContent = data.likeCount.toLocaleString();
        btn.classList.toggle('liked', data.liked);
      } catch(e) { console.error(e); }
    });
  });

  // ── Save Button ───────────────────────────────────────────────
  document.querySelectorAll('[data-save-recipe]').forEach(btn => {
    btn.addEventListener('click', async e => {
      e.preventDefault(); e.stopPropagation();
      try {
        const data = await (await fetch(`/Recipe/Save/${btn.dataset.saveRecipe}`, { method:'POST', headers:{'RequestVerificationToken': getAntiForgeryToken()} })).json();
        const icon = btn.querySelector('i');
        if (icon) icon.className = data.saved ? 'bi bi-bookmark-fill text-orange' : 'bi bi-bookmark';
        btn.classList.toggle('saved', data.saved);
        showToast(data.saved ? 'Saved to your cookbook! 📚' : 'Removed from cookbook');
      } catch(e) { console.error(e); }
    });
  });

  // ── Ingredient Scaling ────────────────────────────────────────
  const servingsInput = document.getElementById('servings-input');
  const baseServings  = parseInt(servingsInput?.dataset.base || '4');
  servingsInput?.addEventListener('input', () => {
    const ratio = (parseInt(servingsInput.value) || baseServings) / baseServings;
    document.querySelectorAll('[data-qty]').forEach(el => {
      const scaled = parseFloat(el.dataset.qty) * ratio;
      el.textContent = scaled % 1 === 0 ? scaled.toFixed(0) : scaled.toFixed(1);
      el.classList.toggle('scaled', ratio !== 1);
    });
  });

  // ── Quiz ──────────────────────────────────────────────────────
  document.querySelectorAll('.quiz-option').forEach(opt => {
    opt.addEventListener('click', () => {
      const group = opt.closest('[data-question]');
      group?.querySelectorAll('.quiz-option').forEach(o => o.classList.remove('selected'));
      opt.classList.add('selected');
      const hidden = document.getElementById(`answer-${group?.dataset.question}`);
      if (hidden) hidden.value = opt.dataset.value;
    });
  });

  // ── Cook Log ─────────────────────────────────────────────────
  const cookLogForm = document.getElementById('cook-log-form');
  cookLogForm?.addEventListener('submit', async e => {
    e.preventDefault();
    try { await fetch(cookLogForm.action, { method:'POST', body: new FormData(cookLogForm) }); showToast('Cook log added! 🍳 Streak continues!'); cookLogForm.closest('[id]')?.classList.add('d-none'); }
    catch { showToast('Could not save. Please try again.', 'error'); }
  });

  // ── Language Picker ───────────────────────────────────────────
  const langPicker = document.getElementById('language-picker');
  langPicker?.addEventListener('change', () => {
    document.cookie = `.AspNetCore.Culture=c=${langPicker.value}|uic=${langPicker.value};path=/;max-age=31536000`;
    window.location.reload();
  });

  // ── Joke Like ─────────────────────────────────────────────────
  document.querySelectorAll('[data-joke-like]').forEach(btn => {
    btn.addEventListener('click', async () => {
      try {
        const data = await (await fetch(`/Jokes/Like/${btn.dataset.jokeLike}`, { method:'POST', headers:{'RequestVerificationToken': getAntiForgeryToken()} })).json();
        btn.innerHTML = `<i class="bi bi-hand-thumbs-up-fill text-orange"></i> ${data.likeCount}`;
        btn.disabled = true;
      } catch { /* silent */ }
    });
  });

  // ── Toast ─────────────────────────────────────────────────────
  function showToast(msg, type = 'success') {
    const toast = document.createElement('div');
    const err   = type === 'error';
    toast.className  = `alert-banner alert-banner-${err ? 'danger' : 'success'} fade-up`;
    toast.style.cssText = 'position:fixed;bottom:5rem;right:1.5rem;z-index:9999;max-width:360px;border-radius:12px;box-shadow:var(--shadow-lg);pointer-events:auto';
    toast.innerHTML  = `<i class="bi bi-${err ? 'exclamation-circle-fill' : 'check-circle-fill'}"></i> ${escHtml(msg)}
      <button onclick="this.parentElement.remove()" style="margin-left:auto;background:none;border:none;font-size:1rem;cursor:pointer;opacity:.6">✕</button>`;
    document.body.appendChild(toast);
    setTimeout(() => toast?.remove(), 4000);
  }
  window.showToast = showToast;

  // ── Helpers ───────────────────────────────────────────────────
  function getAntiForgeryToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
  }
  function escHtml(str) {
    if (!str) return '';
    return str.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
  }
  function timeAgo(dateStr) {
    const s = Math.floor((Date.now() - new Date(dateStr)) / 1000);
    if (s < 60)    return 'Just now';
    if (s < 3600)  return `${Math.floor(s/60)}m ago`;
    if (s < 86400) return `${Math.floor(s/3600)}h ago`;
    return `${Math.floor(s/86400)}d ago`;
  }

  // ── Intersection Observer (fade-up + lazy images) ─────────────
  const io = new IntersectionObserver(entries => {
    entries.forEach(e => {
      if (!e.isIntersecting) return;
      e.target.classList.add('visible');
      const img = e.target.querySelector('img[data-src]');
      if (img) { img.src = img.dataset.src; delete img.dataset.src; }
      io.unobserve(e.target);
    });
  }, { threshold: 0.08, rootMargin: '0px 0px -40px 0px' });
  document.querySelectorAll('.fade-up').forEach(el => io.observe(el));

  // ── Keyboard shortcut tooltip ─────────────────────────────────
  const st = document.getElementById('search-toggle');
  if (st) st.title = navigator.platform?.includes('Mac') ? 'Search (⌘K)' : 'Search (Ctrl+K)';

});
