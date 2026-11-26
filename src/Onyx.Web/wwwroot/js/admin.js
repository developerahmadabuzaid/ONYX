// Admin enhancements only; every form works without JavaScript. The API remains the only enforcement point.
(function () {
  'use strict';

  // Without this class the sidebar simply stays visible (no-JS fallback); with it, it becomes a drawer on small screens.
  document.documentElement.classList.add('js');

  var readStore = function (key) { try { return window.localStorage.getItem(key); } catch (e) { return null; } };
  var writeStore = function (key, value) { try { window.localStorage.setItem(key, value); } catch (e) { /* private mode: keep it per page */ } };

  var shell = document.querySelector('[data-shell]');
  if (shell) {
    var storageKey = 'onyx.admin.nav';
    var overlay = shell.querySelector('.a-overlay');
    var toggle = shell.querySelector('[data-nav-toggle]');
    var collapse = shell.querySelector('[data-nav-collapse]');

    var setCollapsed = function (collapsed) {
      shell.classList.toggle('is-collapsed', collapsed);
      if (collapse) { collapse.setAttribute('aria-pressed', collapsed ? 'true' : 'false'); }
    };
    setCollapsed(readStore(storageKey) === 'collapsed');
    if (collapse) {
      collapse.addEventListener('click', function () {
        var collapsed = !shell.classList.contains('is-collapsed');
        setCollapsed(collapsed);
        writeStore(storageKey, collapsed ? 'collapsed' : 'expanded');
      });
    }

    var setOpen = function (open) {
      shell.classList.toggle('is-nav-open', open);
      if (overlay) { overlay.hidden = !open; }
      if (toggle) { toggle.setAttribute('aria-expanded', open ? 'true' : 'false'); }
      if (open) {
        var first = shell.querySelector('.a-sidebar a, .a-sidebar summary');
        if (first) { first.focus(); }
      } else if (toggle && shell.contains(document.activeElement) && document.activeElement.closest('.a-sidebar')) {
        toggle.focus();
      }
    };
    if (toggle) { toggle.addEventListener('click', function () { setOpen(!shell.classList.contains('is-nav-open')); }); }
    shell.querySelectorAll('[data-nav-close]').forEach(function (element) {
      element.addEventListener('click', function () { setOpen(false); });
    });
    document.addEventListener('keydown', function (event) {
      if (event.key === 'Escape' && shell.classList.contains('is-nav-open')) { setOpen(false); }
    });
  }

  // Menus built on <details> (user menu, "New" menu, row actions): close on an outside click or Escape.
  var menus = Array.prototype.slice.call(document.querySelectorAll('details[data-menu]'));
  if (menus.length) {
    document.addEventListener('click', function (event) {
      menus.forEach(function (menu) { if (menu.open && !menu.contains(event.target)) { menu.open = false; } });
    });
    document.addEventListener('keydown', function (event) {
      if (event.key !== 'Escape') { return; }
      menus.forEach(function (menu) {
        if (menu.open) {
          menu.open = false;
          menu.querySelector('summary').focus();
        }
      });
    });
  }

  // ------------------------------------------------------------------ Theme
  // An explicit choice is remembered in a preference cookie (read by the server, so there is no flash on load);
  // without one the operating system's setting applies.
  var root = document.documentElement;
  var themeToggle = document.querySelector('[data-theme-toggle]');
  var systemDark = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;
  var currentTheme = function () {
    var chosen = root.getAttribute('data-theme');
    if (chosen === 'dark' || chosen === 'light') { return chosen; }
    return systemDark && systemDark.matches ? 'dark' : 'light';
  };
  var labelTheme = function () {
    if (!themeToggle) { return; }
    var label = currentTheme() === 'dark' ? themeToggle.getAttribute('data-label-light') : themeToggle.getAttribute('data-label-dark');
    themeToggle.setAttribute('aria-label', label);
    themeToggle.setAttribute('title', label);
  };
  if (themeToggle) {
    themeToggle.hidden = false;
    labelTheme();
    themeToggle.addEventListener('click', function () {
      var next = currentTheme() === 'dark' ? 'light' : 'dark';
      root.setAttribute('data-theme', next);
      document.cookie = 'onyx-admin-theme=' + next + '; Path=/admin; Max-Age=31536000; SameSite=Lax' + (location.protocol === 'https:' ? '; Secure' : '');
      labelTheme();
    });
    if (systemDark && systemDark.addEventListener) { systemDark.addEventListener('change', labelTheme); }
  }

  // ------------------------------------------------------------------ Confirmation dialog
  // Destructive actions (delete, discard, cancel) ask first, in a styled dialog when the browser supports it.
  var confirmDialog = document.querySelector('[data-confirm-dialog]');
  var askToConfirm = function (message, onConfirm) {
    if (!confirmDialog || typeof confirmDialog.showModal !== 'function') {
      if (window.confirm(message)) { onConfirm(); }
      return;
    }
    confirmDialog.querySelector('[data-confirm-message]').textContent = message;
    var onClose = function () {
      confirmDialog.removeEventListener('close', onClose);
      if (confirmDialog.returnValue === 'ok') { onConfirm(); }
    };
    confirmDialog.returnValue = '';
    confirmDialog.addEventListener('close', onClose);
    confirmDialog.showModal();
  };
  document.addEventListener('submit', function (event) {
    var form = event.target;
    if (!(form instanceof HTMLFormElement) || form.hasAttribute('data-confirmed')) { return; }
    var submitter = event.submitter;
    var message = (submitter && submitter.getAttribute('data-confirm')) || form.getAttribute('data-confirm');
    if (!message) { return; }
    event.preventDefault();
    askToConfirm(message, function () {
      form.setAttribute('data-confirmed', '');
      if (submitter && submitter.name) {
        var carrier = document.createElement('input');
        carrier.type = 'hidden';
        carrier.name = submitter.name;
        carrier.value = submitter.value;
        form.appendChild(carrier);
      }
      form.submit();
    });
  });

  // ------------------------------------------------------------------ Toasts
  // One-time success notices float briefly in the corner; errors stay in the page until the next action.
  document.querySelectorAll('.notice[data-toast]').forEach(function (toast) {
    toast.classList.add('is-toast');
    var close = document.createElement('button');
    close.type = 'button';
    close.className = 'toast__close';
    close.setAttribute('aria-label', toast.getAttribute('data-close-label') || 'Close');
    close.textContent = '×';
    close.addEventListener('click', function () { toast.remove(); });
    toast.appendChild(close);
    var timer = window.setTimeout(function () { toast.classList.add('is-leaving'); window.setTimeout(function () { toast.remove(); }, 400); }, 6000);
    toast.addEventListener('mouseenter', function () { window.clearTimeout(timer); });
    toast.addEventListener('focusin', function () { window.clearTimeout(timer); });
  });

  // ------------------------------------------------------------------ Command palette (Ctrl/Cmd + K)
  var palette = document.querySelector('[data-palette]');
  var paletteOpen = document.querySelector('[data-palette-open]');
  if (palette && typeof palette.showModal === 'function') {
    var input = palette.querySelector('[data-palette-input]');
    var results = palette.querySelector('[data-palette-results]');
    var empty = palette.querySelector('[data-palette-empty]');
    var searchUrl = palette.getAttribute('data-search-url');
    var pages = [];
    document.querySelectorAll('[data-palette-source] a[href]').forEach(function (link) {
      var label = (link.querySelector('.a-nav__label') || link).textContent.trim();
      if (label) { pages.push({ label: label, url: link.getAttribute('href'), group: palette.getAttribute('data-group-pages') }); }
    });
    var actions = [];
    document.querySelectorAll('[data-palette-action]').forEach(function (link) {
      actions.push({ label: link.getAttribute('data-palette-action'), url: link.getAttribute('href'), group: palette.getAttribute('data-group-actions') });
    });
    var entries = [];
    var selected = 0;
    var searchTimer = null;
    var searchSeq = 0;

    var normalize = function (text) { return (text || '').toLocaleLowerCase(); };
    var render = function () {
      results.textContent = '';
      var lastGroup = null;
      entries.forEach(function (entry, index) {
        if (entry.group !== lastGroup) {
          var heading = document.createElement('li');
          heading.className = 'palette__group';
          heading.setAttribute('role', 'presentation');
          heading.textContent = entry.group;
          results.appendChild(heading);
          lastGroup = entry.group;
        }
        var item = document.createElement('li');
        item.className = 'palette__item' + (index === selected ? ' is-selected' : '');
        item.id = 'palette-option-' + index;
        item.setAttribute('role', 'option');
        item.setAttribute('aria-selected', index === selected ? 'true' : 'false');
        if (entry.key) {
          var key = document.createElement('span');
          key.className = 'palette__key';
          key.setAttribute('dir', 'ltr');
          key.textContent = entry.key;
          item.appendChild(key);
        }
        var label = document.createElement('span');
        label.className = 'palette__label';
        label.setAttribute('dir', 'auto');
        label.textContent = entry.label;
        item.appendChild(label);
        item.addEventListener('mousedown', function (event) { event.preventDefault(); go(index); });
        results.appendChild(item);
      });
      empty.hidden = entries.length > 0;
      input.setAttribute('aria-activedescendant', entries.length ? 'palette-option-' + selected : '');
      var current = results.querySelector('.is-selected');
      if (current && current.scrollIntoView) { current.scrollIntoView({ block: 'nearest' }); }
    };
    var filter = function (issues) {
      var query = normalize(input.value.trim());
      var matches = function (entry) { return !query || normalize(entry.label).indexOf(query) >= 0; };
      entries = actions.filter(matches).concat(pages.filter(matches).slice(0, query ? 12 : 8)).concat(issues || []);
      selected = Math.min(selected, Math.max(0, entries.length - 1));
      render();
    };
    var searchIssues = function () {
      var query = input.value.trim();
      if (!searchUrl || query.length < 2) { filter([]); return; }
      var sequence = ++searchSeq;
      fetch(searchUrl + '?q=' + encodeURIComponent(query), { credentials: 'same-origin', headers: { Accept: 'application/json' } })
        .then(function (response) { return response.ok ? response.json() : []; })
        .then(function (issues) {
          if (sequence !== searchSeq) { return; }
          filter((issues || []).map(function (issue) {
            return { label: issue.title, key: issue.key, url: issue.url, group: palette.getAttribute('data-group-issues') };
          }));
        })
        .catch(function () { filter([]); });
    };
    var go = function (index) {
      var entry = entries[index];
      if (entry) { window.location.href = entry.url; }
    };
    var openPalette = function () {
      input.value = '';
      selected = 0;
      filter([]);
      palette.showModal();
      input.focus();
    };

    if (paletteOpen) {
      paletteOpen.hidden = false;
      paletteOpen.addEventListener('click', openPalette);
    }
    document.addEventListener('keydown', function (event) {
      if ((event.ctrlKey || event.metaKey) && (event.key === 'k' || event.key === 'K')) {
        event.preventDefault();
        if (palette.open) { palette.close(); } else { openPalette(); }
      }
    });
    input.addEventListener('input', function () {
      selected = 0;
      filter([]);
      window.clearTimeout(searchTimer);
      searchTimer = window.setTimeout(searchIssues, 180);
    });
    input.addEventListener('keydown', function (event) {
      if (event.key === 'ArrowDown') { event.preventDefault(); selected = Math.min(selected + 1, entries.length - 1); render(); }
      else if (event.key === 'ArrowUp') { event.preventDefault(); selected = Math.max(selected - 1, 0); render(); }
      else if (event.key === 'Enter') { event.preventDefault(); go(selected); }
    });
    palette.addEventListener('click', function (event) { if (event.target === palette) { palette.close(); } });
  }

  // Warn before leaving an editor with unsaved edits.
  document.querySelectorAll('form.editor').forEach(function (form) {
    var dirty = false;
    form.addEventListener('input', function () { dirty = true; });
    form.addEventListener('submit', function () { dirty = false; });
    window.addEventListener('beforeunload', function (event) {
      if (dirty) {
        event.preventDefault();
        event.returnValue = '';
      }
    });
  });

  // Media pickers: refresh the thumbnail when another image is chosen.
  document.querySelectorAll('.media-select select[data-raw-url]').forEach(function (select) {
    select.addEventListener('change', function () {
      var wrapper = select.closest('.media-select');
      var thumb = wrapper.querySelector('img.media-select__thumb');
      if (!select.value) {
        if (thumb) { thumb.remove(); }
        return;
      }
      if (!thumb) {
        thumb = document.createElement('img');
        thumb.className = 'media-select__thumb';
        thumb.alt = '';
        wrapper.insertBefore(thumb, select);
      }
      thumb.src = select.getAttribute('data-raw-url').replace('00000000-0000-0000-0000-000000000000', encodeURIComponent(select.value));
    });
  });
})();
