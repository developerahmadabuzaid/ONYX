// Chart tooltips: any [data-tip] inside a .chart shows its text near the pointer (or the focused element). The text is set with
// textContent, so names typed by users are never interpreted as markup. Every chart also states its numbers in the page.
(function () {
  'use strict';

  var tip = document.createElement('div');
  tip.className = 'chart-tip';
  tip.setAttribute('role', 'tooltip');
  tip.hidden = true;
  document.body.appendChild(tip);

  var place = function (x, y) {
    var width = tip.offsetWidth;
    var height = tip.offsetHeight;
    var left = Math.min(Math.max(8, x - width / 2), window.innerWidth - width - 8);
    var top = y - height - 12;
    if (top < 8) { top = y + 18; }
    tip.style.left = left + 'px';
    tip.style.top = top + 'px';
  };

  var show = function (target, x, y) {
    var text = target.getAttribute('data-tip');
    if (!text) { return; }
    tip.textContent = text;
    tip.hidden = false;
    place(x, y);
    target.classList.add('is-hot');
  };

  var hide = function (target) {
    tip.hidden = true;
    if (target) { target.classList.remove('is-hot'); }
  };

  var current = null;
  document.addEventListener('pointermove', function (event) {
    var target = event.target instanceof Element ? event.target.closest('.chart [data-tip]') : null;
    if (target !== current) {
      hide(current);
      current = target;
    }

    if (target) { show(target, event.clientX, event.clientY); }
  });

  document.addEventListener('focusin', function (event) {
    var holder = event.target instanceof Element ? event.target.closest('.chart a') : null;
    var target = holder ? holder.querySelector('[data-tip]') : null;
    if (!target) { return; }
    var box = target.getBoundingClientRect();
    current = target;
    show(target, box.left + box.width / 2, box.top);
  });

  document.addEventListener('focusout', function () { hide(current); current = null; });
  // Any scroll (the page or a chart's own box) moves the chart under a tooltip that stays put, so the tooltip goes.
  document.addEventListener('scroll', function () { hide(current); current = null; }, { passive: true, capture: true });

  // Timelines open at today: unless today's line is already in view, the timeline's own box (never the page) scrolls so the
  // line sits a quarter of the way in from the reading start.
  Array.prototype.forEach.call(document.querySelectorAll('[data-gantt-scroll]'), function (box) {
    var line = box.querySelector('.gantt__today');
    if (!line) { return; }
    var view = box.getBoundingClientRect();
    var at = line.getBoundingClientRect().left;
    if (at >= view.left + 48 && at <= view.right - 48) { return; }
    var rtl = window.getComputedStyle(box).direction === 'rtl';
    box.scrollLeft += at - (view.left + view.width * (rtl ? 0.75 : 0.25));
  });
})();
