/**
 * My Meal Planner — Main JavaScript
 * Minimal vanilla JS wiring Bootstrap + SignalR together.
 * No framework needed — all heavy lifting is done in C#.
 */

document.addEventListener('DOMContentLoaded', () => {

  // ── Theme / Dark Mode ────────────────────────────────────────
  const themeToggle = document.getElementById('theme-toggle');
  const themeIcon   = document.getElementById('theme-icon');
  const htmlRoot    = document.getElementById('html-root');

  const savedTheme  = localStorage.getItem('mmp-theme') || 'light';
  applyTheme(savedTheme);

  themeToggle?.addEventListener('click', () => {
    const next = htmlRoot.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
    applyTheme(next);
    localStorage.setItem('mmp-theme', next);
    // Persist to server
    fetch('/Account/SetTheme', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken() },
      body: JSON.stringify({ darkMode: next === 'dark' })
    }).catch(() => {});
  });

  function applyTheme(theme) {
    htmlRoot.setAttribute('data-bs-theme', theme);
    if (themeIcon) {
      themeIcon.className = theme === 'dark' ? 'bi bi-sun' : 'bi bi-moon-stars';
    }
  }

  // ── Search Overlay ───────────────────────────────────────────
  const searchToggle  = document.getElementById('search-toggle');
  const searchOverlay = document.getElementById('search-overlay');
  const searchInput   = document.getElementById('global-search-input');
  const searchClose   = document.getElementById('search-close');
  const searchResults = document.getElementById('search-results');

  searchToggle?.addEventListener('click', () => {
    searchOverlay?.classList.add('visible');
    searchInput?.focus();
  });

  searchClose?.addEventListener('click', () => {
    searchOverlay?.classList.remove('visible');
  });

  document.addEventListener('keydown', e => {
    if (e.key === 'Escape') searchOverlay?.classList.remove('visible');
    if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
      e.preventDefault();
      searchOverlay?.classList.add('visible');
      searchInput?.focus();
    }
  });

  // Click outside overlay to close
  searchOverlay?.addEventListener('click', e => {
    if (e.target === searchOverlay) searchOverlay.classList.remove('visible');
  });

  // Search suggestions — click to populate
  document.querySelectorAll('.search-pill').forEach(pill => {
    pill.addEventListener('click', () => {
      if (searchInput) { searchInput.value = pill.textContent.trim().replace(/^[\S]+\s/, ''); }
      triggerSearch();
    });
  });

  // Live search with debounce
  let searchTimeout;
  searchInput?.addEventListener('input', () => {
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
      if (searchInput.value.trim().length >= 2) triggerSearch();
      else if (searchResults) searchResults.innerHTML = '';
    }, 300);
  });

  searchInput?.addEventListener('keydown', e => {
    if (e.key === 'Enter') {
      e.preventDefault();
      window.location.href = `/Recipe?q=${encodeURIComponent(searchInput.value)}`;
    }
  });

  function triggerSearch() {
    const q = searchInput?.value.trim();
    if (!q || !searchResults) return;

    searchResults.innerHTML = '<div class="p-3 text-muted small">Searching…</div>';

    fetch(`/api/search?q=${encodeURIComponent(q)}&limit=6`)
      .then(r => r.json())
      .then(data => {
        if (!data.length) {
          searchResults.innerHTML = '<div class="p-3 text-muted small">No results found</div>';
          return;
        }
        searchResults.innerHTML = data.map(item => `
          <a href="/Recipe/Details/${item.id}/${item.slug}" class="d-flex gap-3 p-3 border-bottom text-decoration-none hover-orange align-items-center">
            <div style="width:48px;height:48px;border-radius:8px;overflow:hidden;flex-shrink:0;background:var(--light-2)">
              ${item.coverImageUrl
                ? `<img src="${item.coverImageUrl}" style="width:100%;height:100%;object-fit:cover" />`
                : '<div style="width:100%;height:100%;display:flex;align-items:center;justify-content:center;font-size:1.5rem">🍽️</div>'
              }
            </div>
            <div>
              <div style="font-weight:600;color:var(--dark);font-size:.9rem">${escHtml(item.title)}</div>
              <div style="font-size:.78rem;color:var(--mid)">${item.originCountry ? '🌍 ' + escHtml(item.originCountry) : ''} · ${item.mealType}</div>
            </div>
          </a>
        `).join('') + `<a href="/Recipe?q=${encodeURIComponent(q)}" class="d-block p-2 text-center text-orange small fw-medium">See all results →</a>`;
      })
      .catch(() => {
        searchResults.innerHTML = '';
      });
  }

  // ── Image Search ─────────────────────────────────────────────
  const imgSearchBtn   = document.getElementById('search-img-btn');
  const imgSearchInput = document.getElementById('search-img-input');

  imgSearchBtn?.addEventListener('click', () => imgSearchInput?.click());

  imgSearchInput?.addEventListener('change', async e => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (searchResults) searchResults.innerHTML = '<div class="p-3 text-muted small">🔍 Identifying food in photo…</div>';

    const formData = new FormData();
    formData.append('image', file);

    try {
      const resp = await fetch('/api/search/image', {
        method: 'POST',
        headers: { 'RequestVerificationToken': getAntiForgeryToken() },
        body: formData
      });
      const data = await resp.json();

      if (data.identifiedDish) {
        if (searchInput) searchInput.value = data.identifiedDish;
        triggerSearch();
      }
    } catch {
      if (searchResults) searchResults.innerHTML = '<div class="p-3 text-muted small">Could not identify food. Try another photo.</div>';
    }
  });

  // ── Voice Search ─────────────────────────────────────────────
  const voiceBtn = document.getElementById('search-voice-btn');
  let recognition;

  if ('webkitSpeechRecognition' in window || 'SpeechRecognition' in window) {
    const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    recognition = new SR();
    recognition.continuous = false;
    recognition.lang = document.documentElement.lang || 'en-US';

    recognition.onresult = e => {
      const transcript = e.results[0][0].transcript;
      if (searchInput) { searchInput.value = transcript; triggerSearch(); }
      voiceBtn?.classList.remove('recording');
    };

    recognition.onerror = () => voiceBtn?.classList.remove('recording');

    voiceBtn?.addEventListener('click', () => {
      voiceBtn.classList.add('recording');
      recognition.start();
    });
  } else {
    voiceBtn?.setAttribute('disabled', 'true');
  }

  // ── Notification Panel ───────────────────────────────────────
  const notifToggle = document.getElementById('notif-toggle');
  const notifPanel  = document.getElementById('notif-panel');
  const notifBadge  = document.getElementById('notif-badge');
  const markAllBtn  = document.getElementById('mark-all-read');

  notifToggle?.addEventListener('click', e => {
    e.stopPropagation();
    notifPanel?.classList.toggle('visible');
  });

  document.addEventListener('click', e => {
    if (notifPanel?.classList.contains('visible') &&
        !notifPanel.contains(e.target) &&
        e.target !== notifToggle) {
      notifPanel.classList.remove('visible');
    }
  });

  markAllBtn?.addEventListener('click', () => {
    if (notifHubConnection) notifHubConnection.invoke('MarkAllRead').catch(console.error);
    document.querySelectorAll('.notif-item.unread').forEach(el => el.classList.remove('unread'));
    if (notifBadge) notifBadge.style.display = 'none';
  });

  // ── SignalR — Notifications ───────────────────────────────────
  let notifHubConnection;

  if (document.body.dataset.authenticated === 'true') {
    notifHubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notifications')
      .withAutomaticReconnect()
      .build();

    notifHubConnection.on('NewNotification', payload => {
      prependNotification(payload);
      if (notifBadge) notifBadge.style.display = 'block';
    });

    notifHubConnection.on('UnreadCountUpdated', count => {
      if (notifBadge) notifBadge.style.display = count > 0 ? 'block' : 'none';
    });

    notifHubConnection.start().catch(console.error);

    // Load initial notifications
    fetch('/api/notifications')
      .then(r => r.json())
      .then(data => {
        if (data.length) {
          const list = document.getElementById('notif-list');
          if (list) {
            list.innerHTML = data.map(n => renderNotification(n)).join('');
            const unread = data.filter(n => !n.isRead).length;
            if (unread > 0 && notifBadge) notifBadge.style.display = 'block';
          }
        }
      })
      .catch(() => {});
  }

  function renderNotification(n) {
    const icons = {
      DailyMealSuggestion: '🍽️', LevelUpAlert: '🌟', BadgeEarned: '🏅',
      QuizReadyToTake: '📝', NewRecipeFromFollowed: '👨‍🍳', SomeoneCommentedYours: '💬',
      TrendingInYourCountry: '🔥', HealthTipOfTheDay: '💚', NewJokeOfTheDay: '😂'
    };
    const icon = icons[n.type] || '🔔';
    return `
      <div class="notif-item ${n.isRead ? '' : 'unread'}" data-id="${n.id}"
           onclick="markNotifRead(${n.id}, '${n.actionUrl || ''}')">
        <span class="notif-icon">${icon}</span>
        <div>
          <div style="font-size:.875rem;font-weight:600;color:var(--dark)">${escHtml(n.title)}</div>
          <div style="font-size:.8rem;color:var(--mid)">${escHtml(n.body)}</div>
          <div style="font-size:.73rem;color:var(--mid);margin-top:.2rem">${timeAgo(n.createdAt)}</div>
        </div>
      </div>`;
  }

  function prependNotification(n) {
    const list = document.getElementById('notif-list');
    if (!list) return;
    const empty = list.querySelector('.notif-empty');
    if (empty) empty.remove();
    list.insertAdjacentHTML('afterbegin', renderNotification(n));
  }

  window.markNotifRead = (id, url) => {
    notifHubConnection?.invoke('MarkNotificationRead', id).catch(console.error);
    const el = document.querySelector(`.notif-item[data-id="${id}"]`);
    el?.classList.remove('unread');
    if (url) window.location.href = url;
  };

  // ── Like Button ───────────────────────────────────────────────
  document.querySelectorAll('[data-like-recipe]').forEach(btn => {
    btn.addEventListener('click', async () => {
      const id = btn.dataset.likeRecipe;
      const token = getAntiForgeryToken();
      try {
        const resp = await fetch(`/Recipe/Like/${id}`, {
          method: 'POST',
          headers: { 'RequestVerificationToken': token }
        });
        const data = await resp.json();
        const icon = btn.querySelector('.like-icon');
        const count = btn.querySelector('.like-count');
        if (icon) icon.className = data.liked ? 'bi bi-heart-fill like-icon text-danger' : 'bi bi-heart like-icon';
        if (count) count.textContent = data.likeCount.toLocaleString();
        btn.classList.toggle('liked', data.liked);
      } catch (e) { console.error(e); }
    });
  });

  // ── Save Button ───────────────────────────────────────────────
  document.querySelectorAll('[data-save-recipe]').forEach(btn => {
    btn.addEventListener('click', async () => {
      const id = btn.dataset.saveRecipe;
      try {
        const resp = await fetch(`/Recipe/Save/${id}`, {
          method: 'POST',
          headers: { 'RequestVerificationToken': getAntiForgeryToken() }
        });
        const data = await resp.json();
        const icon = btn.querySelector('i');
        if (icon) icon.className = data.saved ? 'bi bi-bookmark-fill text-orange' : 'bi bi-bookmark';
        showToast(data.saved ? 'Saved to your cookbook! 📚' : 'Removed from cookbook');
      } catch (e) { console.error(e); }
    });
  });

  // ── Ingredient Scaling ────────────────────────────────────────
  const servingsInput = document.getElementById('servings-input');
  const baseServings  = parseInt(servingsInput?.dataset.base || '4');

  servingsInput?.addEventListener('input', () => {
    const target = parseInt(servingsInput.value) || baseServings;
    const ratio  = target / baseServings;

    document.querySelectorAll('[data-qty]').forEach(el => {
      const base   = parseFloat(el.dataset.qty);
      const scaled = base * ratio;
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

      const qId = group?.dataset.question;
      const val  = opt.dataset.value;
      const hidden = document.getElementById(`answer-${qId}`);
      if (hidden) hidden.value = val;
    });
  });

  // ── Cook Log Submission ───────────────────────────────────────
  const cookLogForm = document.getElementById('cook-log-form');
  cookLogForm?.addEventListener('submit', async e => {
    e.preventDefault();
    const fd = new FormData(cookLogForm);
    try {
      await fetch(cookLogForm.action, { method: 'POST', body: fd });
      showToast('Cook log added! 🍳 Your streak continues!');
      cookLogForm.closest('[id]')?.classList.add('d-none');
    } catch { showToast('Could not save. Please try again.', 'error'); }
  });

  // ── Language Picker ───────────────────────────────────────────
  const langPicker = document.getElementById('language-picker');
  langPicker?.addEventListener('change', () => {
    const lang = langPicker.value;
    document.cookie = `.AspNetCore.Culture=c=${lang}|uic=${lang};path=/;max-age=31536000`;
    window.location.reload();
  });

  // ── Toast ─────────────────────────────────────────────────────
  function showToast(msg, type = 'success') {
    const toast = document.createElement('div');
    toast.className = `alert-banner alert-banner-${type === 'error' ? 'danger' : 'success'} fade-up`;
    toast.style.cssText = 'position:fixed;bottom:1.5rem;right:1.5rem;z-index:9999;max-width:360px;border-radius:12px;box-shadow:var(--shadow-lg)';
    toast.innerHTML = `<i class="bi bi-check-circle-fill"></i> ${msg}`;
    document.body.appendChild(toast);
    setTimeout(() => toast.remove(), 3500);
  }

  // ── Helpers ───────────────────────────────────────────────────
  function getAntiForgeryToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
  }

  function escHtml(str) {
    if (!str) return '';
    return str.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
  }

  function timeAgo(dateStr) {
    const secs = Math.floor((Date.now() - new Date(dateStr)) / 1000);
    if (secs < 60) return 'Just now';
    if (secs < 3600) return `${Math.floor(secs/60)}m ago`;
    if (secs < 86400) return `${Math.floor(secs/3600)}h ago`;
    return `${Math.floor(secs/86400)}d ago`;
  }

  // ── Fade-up observer ─────────────────────────────────────────
  const io = new IntersectionObserver(entries => {
    entries.forEach(e => { if (e.isIntersecting) e.target.classList.add('visible'); });
  }, { threshold: .1 });

  document.querySelectorAll('.fade-up').forEach(el => io.observe(el));

});
