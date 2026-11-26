// Workspace conveniences: dashboard widget layout, issue-list bulk selection and columns, calendar drag and drop.
// Preferences live in this browser only (localStorage); every page works without this script, and the API enforces every rule.
(function () {
  'use strict';

  var read = function (key) { try { return JSON.parse(window.localStorage.getItem(key) || 'null'); } catch (e) { return null; } };
  var write = function (key, value) { try { window.localStorage.setItem(key, JSON.stringify(value)); } catch (e) { /* storage unavailable */ } };
  var remove = function (key) { try { window.localStorage.removeItem(key); } catch (e) { /* storage unavailable */ } };

  // ---------------------------------------------------------------- Dashboard widgets: show, hide and reorder
  var grid = document.querySelector('[data-widgets]');
  var dialog = document.querySelector('[data-customize]');
  var openButton = document.querySelector('[data-customize-open]');
  if (grid && dialog && openButton && typeof dialog.showModal === 'function') {
    var key = 'onyx.delivery.widgets.v1';
    var widgets = Array.prototype.slice.call(grid.querySelectorAll('[data-widget]'));
    var defaults = widgets.map(function (widget) { return widget.getAttribute('data-widget'); });
    var titleOf = function (id) {
      var widget = grid.querySelector('[data-widget="' + id + '"]');
      var heading = widget ? widget.querySelector('h2') : null;
      return heading ? heading.textContent.trim() : id;
    };
    var state = read(key) || {};
    var order = Array.isArray(state.order) ? state.order.filter(function (id) { return defaults.indexOf(id) >= 0; }) : [];
    defaults.forEach(function (id) { if (order.indexOf(id) < 0) { order.push(id); } });
    var hidden = Array.isArray(state.hidden) ? state.hidden.filter(function (id) { return defaults.indexOf(id) >= 0; }) : [];

    var apply = function () {
      order.forEach(function (id) {
        var widget = grid.querySelector('[data-widget="' + id + '"]');
        if (widget) {
          grid.appendChild(widget);
          widget.hidden = hidden.indexOf(id) >= 0;
        }
      });
    };
    var save = function () { write(key, { order: order, hidden: hidden }); apply(); };
    var list = dialog.querySelector('[data-customize-list]');
    var renderList = function (focusId, focusMove) {
      list.textContent = '';
      order.forEach(function (id, index) {
        var item = document.createElement('li');
        item.className = 'customize__item';
        var label = document.createElement('label');
        var box = document.createElement('input');
        box.type = 'checkbox';
        box.checked = hidden.indexOf(id) < 0;
        box.addEventListener('change', function () {
          hidden = box.checked ? hidden.filter(function (other) { return other !== id; }) : hidden.concat([id]);
          save();
        });
        label.appendChild(box);
        label.appendChild(document.createTextNode(' ' + titleOf(id)));
        item.appendChild(label);
        [['up', -1], ['down', 1]].forEach(function (move) {
          var button = document.createElement('button');
          button.type = 'button';
          button.className = 'btn btn--small customize__move';
          button.textContent = move[1] < 0 ? '↑' : '↓';
          button.setAttribute('aria-label', dialog.getAttribute('data-' + move[0]) + ': ' + titleOf(id));
          button.disabled = (move[1] < 0 && index === 0) || (move[1] > 0 && index === order.length - 1);
          button.addEventListener('click', function () {
            var target = index + move[1];
            order.splice(index, 1);
            order.splice(target, 0, id);
            save();
            renderList(id, move[0]);
          });
          item.appendChild(button);
          if (focusId === id && focusMove === move[0]) { window.setTimeout(function () { (button.disabled ? box : button).focus(); }, 0); }
        });
        list.appendChild(item);
      });
    };

    apply();
    openButton.hidden = false;
    openButton.addEventListener('click', function () { renderList(); dialog.showModal(); });
    dialog.querySelector('[data-customize-reset]').addEventListener('click', function () {
      remove(key);
      order = defaults.slice();
      hidden = [];
      apply();
      renderList();
    });
  }

  // ---------------------------------------------------------------- Issue list: bulk selection
  var bulk = document.querySelector('[data-bulk-form]');
  if (bulk) {
    var all = document.querySelector('[data-bulk-all]');
    var boxes = Array.prototype.slice.call(document.querySelectorAll('[data-bulk-item]'));
    var bar = document.querySelector('[data-bulk-bar]');
    var count = document.querySelector('[data-bulk-count]');
    var update = function () {
      var checked = boxes.filter(function (box) { return box.checked; }).length;
      if (bar) { bar.classList.toggle('is-active', checked > 0); }
      if (count) { count.textContent = String(checked); }
      if (all) {
        all.checked = checked > 0 && checked === boxes.length;
        all.indeterminate = checked > 0 && checked < boxes.length;
      }
      boxes.forEach(function (box) { var row = box.closest('tr'); if (row) { row.classList.toggle('is-selected', box.checked); } });
    };
    boxes.forEach(function (box) { box.addEventListener('change', update); });
    if (all) {
      all.hidden = false;
      all.addEventListener('change', function () { boxes.forEach(function (box) { box.checked = all.checked; }); update(); });
    }
    var actionSelect = bulk.querySelector('[data-bulk-action]');
    var syncFields = function () {
      var value = actionSelect ? actionSelect.value : '';
      bulk.querySelectorAll('[data-bulk-field]').forEach(function (field) {
        field.hidden = field.getAttribute('data-bulk-field') !== value;
      });
      var submit = bulk.querySelector('[data-bulk-submit]');
      if (submit) {
        if (value === 'Delete') { submit.setAttribute('data-confirm', submit.getAttribute('data-confirm-delete')); }
        else { submit.removeAttribute('data-confirm'); }
      }
    };
    if (actionSelect) { actionSelect.addEventListener('change', syncFields); syncFields(); }
    update();
  }

  // ---------------------------------------------------------------- Issue list: choose columns
  var table = document.querySelector('[data-columns-table]');
  var chooser = document.querySelector('[data-columns-chooser]');
  if (table && chooser) {
    var columnsKey = 'onyx.delivery.columns.v1';
    var hiddenColumns = read(columnsKey) || [];
    var applyColumns = function () {
      table.querySelectorAll('[data-col]').forEach(function (cell) {
        cell.hidden = hiddenColumns.indexOf(cell.getAttribute('data-col')) >= 0;
      });
    };
    chooser.hidden = false;
    chooser.querySelectorAll('input[data-col-toggle]').forEach(function (box) {
      var column = box.getAttribute('data-col-toggle');
      box.checked = hiddenColumns.indexOf(column) < 0;
      box.addEventListener('change', function () {
        hiddenColumns = box.checked ? hiddenColumns.filter(function (other) { return other !== column; }) : hiddenColumns.concat([column]);
        write(columnsKey, hiddenColumns);
        applyColumns();
      });
    });
    applyColumns();
  }

  // ---------------------------------------------------------------- Calendar: drag an issue to another day
  var calendar = document.querySelector('[data-calendar="editable"]');
  if (calendar) {
    var token = document.querySelector('#d-calendar-token input[name="__RequestVerificationToken"]');
    var status = document.querySelector('[data-calendar-status]');
    var dragged = null;
    var say = function (text) { if (status) { status.textContent = text || ''; } };
    var shift = function (iso, days) {
      if (!iso) { return ''; }
      var parts = iso.split('-');
      var date = new Date(Date.UTC(+parts[0], +parts[1] - 1, +parts[2] + days));
      return date.toISOString().slice(0, 10);
    };
    var dayDiff = function (from, to) {
      var a = from.split('-'); var b = to.split('-');
      return Math.round((Date.UTC(+b[0], +b[1] - 1, +b[2]) - Date.UTC(+a[0], +a[1] - 1, +a[2])) / 86400000);
    };
    calendar.querySelectorAll('[data-chip]').forEach(function (chip) {
      chip.setAttribute('draggable', 'true');
      chip.addEventListener('dragstart', function (event) {
        dragged = chip;
        chip.classList.add('is-dragging');
        event.dataTransfer.effectAllowed = 'move';
        event.dataTransfer.setData('text/plain', chip.getAttribute('data-issue-id'));
      });
      chip.addEventListener('dragend', function () { chip.classList.remove('is-dragging'); dragged = null; });
    });
    calendar.querySelectorAll('[data-day]').forEach(function (cell) {
      cell.addEventListener('dragover', function (event) { if (dragged) { event.preventDefault(); cell.classList.add('is-over'); } });
      cell.addEventListener('dragleave', function () { cell.classList.remove('is-over'); });
      cell.addEventListener('drop', function (event) {
        event.preventDefault();
        cell.classList.remove('is-over');
        if (!dragged) { return; }
        var chip = dragged;
        var due = chip.getAttribute('data-due');
        var target = cell.getAttribute('data-day');
        if (!due || due === target) { return; }
        var delta = dayDiff(due, target);
        var start = chip.getAttribute('data-start');
        var body = new URLSearchParams();
        body.append('dueDate', target);
        if (start) { body.append('startDate', shift(start, delta)); }
        body.append('version', chip.getAttribute('data-version'));
        if (token) { body.append('__RequestVerificationToken', token.value); }
        var list = cell.querySelector('[data-day-list]');
        var origin = chip.parentElement;
        if (list) { list.appendChild(chip); }
        chip.classList.add('is-saving');
        say(calendar.getAttribute('data-saving'));
        fetch(chip.getAttribute('data-dates-url'), {
          method: 'POST',
          credentials: 'same-origin',
          headers: { Accept: 'application/json', 'Content-Type': 'application/x-www-form-urlencoded' },
          body: body.toString()
        }).then(function (response) {
          return response.json().then(function (json) { return { ok: response.ok && json.ok, json: json }; });
        }).then(function (result) {
          chip.classList.remove('is-saving');
          if (result.ok) {
            chip.setAttribute('data-due', target);
            if (start) { chip.setAttribute('data-start', shift(start, delta)); }
            chip.setAttribute('data-version', result.json.version);
            say(calendar.getAttribute('data-saved'));
          } else {
            origin.appendChild(chip);
            say((result.json && result.json.message) || calendar.getAttribute('data-failed'));
          }
        }).catch(function () {
          chip.classList.remove('is-saving');
          origin.appendChild(chip);
          say(calendar.getAttribute('data-failed'));
        });
      });
    });
  }
})();
