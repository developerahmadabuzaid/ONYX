// Delivery board and backlog ordering: drag and drop, with a keyboard alternative.
// Progressive enhancement only: every card links to its issue page, and the backlog keeps its "move to" forms. The server
// validates every move (workflow transitions, project role, versions); on any failure the card returns to where it was and
// the server's message is announced. No inline script (CSP script-src 'self').
(function () {
  'use strict';

  var root = document.querySelector('[data-board="editable"], [data-board="backlog"]');
  if (!root) {
    return;
  }

  var isBoard = root.getAttribute('data-board') === 'editable';
  var itemSelector = isBoard ? '.d-card' : '.d-row';
  var listSelector = isBoard ? '.d-column__list' : '.d-list';
  var tokenInput = document.querySelector('#d-board-token input[name="__RequestVerificationToken"]');
  var status = document.querySelector('[data-board-status]');
  var rtl = document.documentElement.getAttribute('dir') === 'rtl';
  var allowed = {};
  (root.getAttribute('data-transitions') || '').split(',').forEach(function (pair) {
    if (pair) { allowed[pair] = true; }
  });

  var dragged = null;
  var origin = null;
  var picked = null;
  var busy = false;

  function message(key) { return root.getAttribute('data-msg-' + key) || ''; }

  function announce(text) {
    if (status) { status.textContent = text; }
  }

  function listOf(item) { return item.closest(listSelector); }

  function listKey(list) {
    return isBoard ? list.closest('.d-column').getAttribute('data-status-id') : list.getAttribute('data-sprint-id');
  }

  function remember(item) {
    return { list: listOf(item), next: item.nextElementSibling };
  }

  function restore(item, place) {
    place.list.insertBefore(item, place.next && place.next.parentNode === place.list ? place.next : null);
  }

  function updateCounts() {
    if (!isBoard) { return; }
    root.querySelectorAll('.d-column').forEach(function (column) {
      var badge = column.querySelector('[data-column-count]');
      if (!badge) { return; }
      var count = column.querySelectorAll(itemSelector).length;
      var limit = badge.textContent.split('/')[1];
      badge.textContent = count + (limit ? '/' + limit : '');
      column.classList.toggle('is-over', !!limit && count > parseInt(limit, 10));
      var empty = column.querySelector('.d-column__empty');
      if (empty) { empty.hidden = count > 0; }
    });
  }

  function transitionAllowed(fromList, toList) {
    if (!isBoard) { return true; }
    var from = listKey(fromList);
    var to = listKey(toList);
    return from === to || allowed[from + '>' + to] === true;
  }

  function save(item, place) {
    var list = listOf(item);
    if (list === place.list && item.nextElementSibling === place.next) {
      return; // Dropped where it started.
    }

    if (!transitionAllowed(place.list, list)) {
      restore(item, place);
      updateCounts();
      announce(message('not-allowed'));
      return;
    }

    var next = item.nextElementSibling;
    while (next && !next.matches(itemSelector)) { next = next.nextElementSibling; }
    var body = new FormData();
    body.append('__RequestVerificationToken', tokenInput ? tokenInput.value : '');
    body.append('version', item.getAttribute('data-version'));
    body.append('beforeIssueId', next ? next.getAttribute('data-issue-id') : '');
    if (isBoard) {
      body.append('statusId', listKey(list));
    } else {
      body.append('changeSprint', 'true');
      body.append('sprintId', listKey(list) || '');
    }

    busy = true;
    item.classList.add('is-saving');
    announce(message('saving'));
    fetch(root.getAttribute('data-move-url').replace('__id__', item.getAttribute('data-issue-id')), {
      method: 'POST',
      body: body,
      credentials: 'same-origin',
      headers: { 'Accept': 'application/json' }
    }).then(function (response) {
      return response.json().catch(function () { return { ok: false }; }).then(function (data) {
        if (response.ok && data.ok) {
          item.setAttribute('data-version', data.version);
          announce(message('saved'));
        } else {
          restore(item, place);
          announce((data && data.message) || message('failed'));
        }
      });
    }).catch(function () {
      restore(item, place);
      announce(message('failed'));
    }).then(function () {
      busy = false;
      item.classList.remove('is-saving');
      updateCounts();
    });
  }

  function itemAfter(list, y) {
    var items = Array.prototype.slice.call(list.querySelectorAll(itemSelector)).filter(function (candidate) { return candidate !== dragged; });
    for (var i = 0; i < items.length; i++) {
      var box = items[i].getBoundingClientRect();
      if (y < box.top + box.height / 2) { return items[i]; }
    }
    return null;
  }

  root.querySelectorAll(itemSelector).forEach(function (item) {
    item.setAttribute('draggable', 'true');
    item.setAttribute('tabindex', '0');
    item.setAttribute('aria-roledescription', 'draggable');
  });

  root.addEventListener('dragstart', function (event) {
    var item = event.target.closest && event.target.closest(itemSelector);
    if (!item || busy) { event.preventDefault(); return; }
    dragged = item;
    origin = remember(item);
    item.classList.add('is-dragging');
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', item.getAttribute('data-issue-id'));
  });

  root.addEventListener('dragover', function (event) {
    if (!dragged) { return; }
    var zone = event.target.closest && event.target.closest('.d-column, .d-sprint');
    var list = zone && zone.querySelector(listSelector);
    if (!list) { return; }
    event.preventDefault();
    var after = itemAfter(list, event.clientY);
    list.insertBefore(dragged, after);
    list.closest('.d-column, .d-sprint').classList.add('is-target');
  });

  root.addEventListener('dragleave', function (event) {
    var zone = event.target.closest && event.target.closest('.d-column, .d-sprint');
    if (zone && !zone.contains(event.relatedTarget)) { zone.classList.remove('is-target'); }
  });

  root.addEventListener('drop', function (event) {
    if (dragged) { event.preventDefault(); }
  });

  root.addEventListener('dragend', function () {
    if (!dragged) { return; }
    var item = dragged;
    dragged = null;
    item.classList.remove('is-dragging');
    root.querySelectorAll('.is-target').forEach(function (zone) { zone.classList.remove('is-target'); });
    updateCounts();
    save(item, origin);
  });

  // Keyboard: Space picks an item up, arrows move it (between columns or lists, and within one), Space or Enter drops it,
  // Escape puts it back. Arrow directions follow the reading direction in Arabic.
  root.addEventListener('keydown', function (event) {
    var item = event.target.matches && event.target.matches(itemSelector) ? event.target : null;
    if (!item || busy) { return; }

    if (!picked) {
      if (event.key === ' ' || event.key === 'Enter') {
        event.preventDefault();
        picked = { item: item, place: remember(item) };
        item.classList.add('is-picked');
        item.setAttribute('aria-grabbed', 'true');
        announce(message('picked'));
      }
      return;
    }

    if (picked.item !== item) { return; }
    var lists = Array.prototype.slice.call(root.querySelectorAll(listSelector));
    var list = listOf(item);
    var index = lists.indexOf(list);
    var handled = true;
    switch (event.key) {
      case 'ArrowUp':
        if (item.previousElementSibling) { list.insertBefore(item, item.previousElementSibling); }
        break;
      case 'ArrowDown':
        if (item.nextElementSibling) { list.insertBefore(item.nextElementSibling, item); }
        break;
      case 'ArrowLeft':
      case 'ArrowRight': {
        // Board: the next/previous column. Backlog: the next/previous sprint list (the backlog is last).
        var forward = (event.key === 'ArrowRight') !== rtl;
        var target = lists[index + (forward ? 1 : -1)];
        if (target) { target.appendChild(item); }
        break;
      }
      case ' ':
      case 'Enter': {
        var place = picked.place;
        picked = null;
        item.classList.remove('is-picked');
        item.setAttribute('aria-grabbed', 'false');
        announce(message('dropped'));
        updateCounts();
        save(item, place);
        break;
      }
      case 'Escape':
        restore(item, picked.place);
        item.classList.remove('is-picked');
        item.setAttribute('aria-grabbed', 'false');
        picked = null;
        updateCounts();
        announce('');
        break;
      default:
        handled = false;
    }

    if (handled) {
      event.preventDefault();
      item.focus();
    }
  });
})();
