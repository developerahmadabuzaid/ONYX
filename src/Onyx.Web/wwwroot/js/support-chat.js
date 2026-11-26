/* Support chat — progressive enhancement only.
 *
 * The panel is a <details> element, so it opens and its quick replies work with this file missing,
 * blocked or still loading. This script only adds:
 *   1. the staggered greeting (messages fade in one by one instead of appearing as a wall of text),
 *   2. the typing indicator and the reply bubble when a quick reply is chosen,
 *   3. Escape to close.
 *
 * It never invents an answer and never sends anything: every quick reply is an ordinary link to a page
 * that really exists. No free-text input, no network calls, no storage, no analytics.
 */
(function () {
    "use strict";

    var chat = document.querySelector("[data-support-chat]");
    if (!chat) { return; }

    var log = chat.querySelector("[data-support-chat-log]");
    var messages = chat.querySelector("[data-support-chat-messages]");
    var typing = chat.querySelector("[data-support-typing]");
    var summary = chat.querySelector(".support-chat__launcher");
    var greetings = Array.prototype.slice.call(chat.querySelectorAll("[data-support-greeting]"));
    var chips = Array.prototype.slice.call(chat.querySelectorAll("[data-support-chip]"));
    if (!log || !messages) { return; }

    var prefersReducedMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var replies = chat.dataset.replies ? JSON.parse(chat.dataset.replies) : {};
    var shown = false;

    function scrollToLatest() {
        log.scrollTop = log.scrollHeight;
    }

    function addMessage(text, side) {
        var item = document.createElement("li");
        item.className = "support-chat__message support-chat__message--" + side;
        item.textContent = text;
        messages.appendChild(item);
        scrollToLatest();
        return item;
    }

    function showTyping(visible) {
        if (typing) { typing.hidden = !visible; }
        if (visible) { scrollToLatest(); }
    }

    function playGreetings() {
        greetings.forEach(function (item) { item.classList.remove("is-revealed"); });
        if (prefersReducedMotion) {
            greetings.forEach(function (item) { item.classList.add("is-revealed"); });
            scrollToLatest();
            return;
        }
        greetings.forEach(function (item, index) {
            window.setTimeout(function () {
                item.classList.add("is-revealed");
                scrollToLatest();
            }, 90 * index + 60);
        });
    }

    function openPanel() {
        chat.classList.add("is-open");
        if (!shown) {
            shown = true;
            playGreetings();
        } else {
            scrollToLatest();
        }
    }

    function closePanel() {
        chat.classList.remove("is-open");
        if (summary) { summary.focus(); }
    }

    if (summary) {
        summary.addEventListener("click", function () {
            // Let the native <details> toggle do the work; we only react to the new state.
            window.setTimeout(function () {
                if (chat.open) { openPanel(); } else { chat.classList.remove("is-open"); }
            }, 0);
        });
    }

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape" && chat.open) {
            closePanel();
            chat.open = false;
        }
    });

    // A chosen quick reply shows the visitor's choice, then the canned answer, before following the link.
    chips.forEach(function (chip) {
        chip.addEventListener("click", function () {
            if (shown) { return; } // replies only make sense after the greeting has played
            shown = true;
            greetings.forEach(function (item) { item.classList.add("is-revealed"); });
            var label = chip.textContent.trim();
            addMessage(label, "user");
            showTyping(true);
            var reply = replies[chip.dataset.replyKey];
            window.setTimeout(function () {
                showTyping(false);
                addMessage(reply || label, "bot");
            }, prefersReducedMotion ? 0 : 620);
        });
    });
})();
