// Small enhancements. Every page works without this file.
(function () {
  'use strict';

  function flash(button, text) {
    var original = button.textContent;
    button.textContent = text;
    setTimeout(function () { button.textContent = original; }, 1600);
  }

  // "Copy" next to a path, and "Copy a link to these steps" (data-copy-link="<id>").
  document.querySelectorAll('.copy').forEach(function (button) {
    var anchor = button.getAttribute('data-copy-link');
    var target = anchor ? null : button.parentElement.querySelector('code');
    if ((!anchor && !target) || !navigator.clipboard) { button.hidden = true; return; }
    button.addEventListener('click', function () {
      var text = anchor ? location.href.split('#')[0] + '#' + anchor : target.textContent.trim();
      navigator.clipboard.writeText(text).then(function () { flash(button, anchor ? 'Link copied' : 'Copied'); });
    });
  });

  // Choose your start: both paths stay on the page; the chosen one is struck forward, the other stays readable.
  document.querySelectorAll('[data-signpost]').forEach(function (post) {
    var arms = post.querySelectorAll('[data-path]');
    function choose(path) {
      post.setAttribute('data-chosen', path);
      arms.forEach(function (arm) {
        var on = arm.getAttribute('data-path') === path;
        arm.setAttribute('aria-pressed', on ? 'true' : 'false');
        var card = document.getElementById(arm.getAttribute('aria-controls'));
        if (card) { if (on) card.removeAttribute('data-quiet'); else card.setAttribute('data-quiet', ''); }
      });
    }
    arms.forEach(function (arm) {
      arm.addEventListener('click', function () { choose(arm.getAttribute('data-path')); });
    });
    choose(location.hash === '#path-split' ? 'split' : 'new');
  });

  // The phone menu closes once a link in it is followed.
  document.querySelectorAll('.menu').forEach(function (menu) {
    menu.addEventListener('click', function (e) { if (e.target.closest('a')) menu.removeAttribute('open'); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && menu.open) { menu.removeAttribute('open'); menu.querySelector('summary').focus(); } });
  });

  // The hero's barter loop rests while the map is off screen.
  var map = document.querySelector('.map');
  if (map && 'IntersectionObserver' in window) {
    new IntersectionObserver(function (entries) {
      map.classList.toggle('at-rest', !entries[0].isIntersecting);
    }).observe(map);
  }

  // Open the question a link points at, so #anchors from other pages land on an expanded answer.
  function openFromHash(scroll) {
    var id = location.hash && decodeURIComponent(location.hash.slice(1));
    var target = id && document.getElementById(id);
    if (target && target.tagName === 'DETAILS') {
      target.open = true;
      if (scroll) target.scrollIntoView({ block: 'start' });
    }
  }
  addEventListener('hashchange', function () { openFromHash(true); });
  openFromHash(true);
})();
