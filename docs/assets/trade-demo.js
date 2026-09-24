/*
 * The interactive Trading Post panel. A working copy of the mod's panel (Colonies/TradingPostFragment.cs) with the
 * mod's own rules (ExchangeTerms, TradeOfferForm): two colonies, one post, one exchange at a time. The visitor plays
 * either colony (or lets the other answer by itself), makes an offer, watches each side's workers bring the goods to
 * their own half, and sees the round cross only once both sides are in. Nothing here is saved.
 */
(function () {
  'use strict';

  var MAX_AMOUNT = 100, MAX_ROUNDS = 99;
  var SCIENCE = 'science', BEAVERS = 'beaver';

  var GOODS = [
    { id: 'logs', name: 'Logs', icon: 'logs', plural: 'Logs' },
    { id: 'gear', name: 'Gears', icon: 'gear', plural: 'Gears' },
    { id: 'berries', name: 'Berries', icon: 'berries', plural: 'Berries' },
    { id: 'carrot', name: 'Carrots', icon: 'carrot', plural: 'Carrots' },
    { id: SCIENCE, name: 'Science', icon: 'science', plural: 'Science', special: true },
    { id: BEAVERS, name: 'Beavers', icon: 'beaver', plural: 'Beavers', special: true }
  ];
  function good(id) { for (var i = 0; i < GOODS.length; i++) if (GOODS[i].id === id) return GOODS[i]; return null; }
  function icon(id, size) { var g = good(id); return '<img src="assets/goods/' + g.icon + '-' + size + '.png" alt="" width="' + size / 2 + '" height="' + size / 2 + '">'; }
  function nameOf(id, n) { var g = good(id); if (id === BEAVERS) return n === 1 ? 'Beaver' : 'Beavers'; return g.plural; }
  function amountOf(n, id) { return n + ' ' + nameOf(id, n); }

  // The panel names a colony by its player (the seat table), as the mod does; 'Colony N' only for an unclaimed one.
  var COLONY = ['Player 1', 'Player 2'];
  var CLS = ['tp-c1', 'tp-c2'];
  function colored(slot) { return '<b class="' + CLS[slot] + '">' + COLONY[slot] + '</b>'; }

  // ---- state ----
  var S;
  function reset() {
    S = {
      me: 0,
      auto: true,
      stock: [
        { logs: 240, gear: 12, berries: 90, carrot: 60, science: 310, beaver: 14 },
        { logs: 30, gear: 61, berries: 15, carrot: 140, science: 140, beaver: 9 }
      ],
      workers: [2, 2],
      draft: { give: 'logs', get: 'gear', giveAmount: 100, getAmount: 25, rounds: 4, repeat: false },
      exchange: null,           // { by, side: [{good,total,held}], rounds, repeat, done, state: proposed|active, cancelAsked: [b,b], serial }
      ledger: [[], []],         // per colony: {cycle, day, gave, gaveN, got, gotN}
      totals: { '0>1': {}, '1>0': {} },
      cycle: 3, day: 12, dayTicks: 0,
      pickerOpen: null,
      log: [],
      serial: 0
    };
    render();
    say('Demo reset. You are ' + colored(0) + ' (colony 1), at your half of the post.');
  }

  // ---- rules (TradeOfferForm.Judge) ----
  function judge(d) {
    if (!d.give || !d.get) return { verdict: 'noItem' };
    if (d.give === d.get) return { verdict: 'same' };
    var give = d.giveAmount, get = d.getAmount;
    if (isNaN(give) || isNaN(get) || give < 0 || get < 0 || give > MAX_AMOUNT || get > MAX_AMOUNT) return { verdict: 'badAmount' };
    if (!d.repeat && (isNaN(d.rounds) || d.rounds < 1 || d.rounds > MAX_ROUNDS)) return { verdict: 'badRounds' };
    if (give === 0 && get === 0) return { verdict: 'nothing' };
    var v = give > 0 && get > 0 ? 'exchange' : give > 0 ? 'gift' : 'request';
    return { verdict: v, give: give, get: get, rounds: d.repeat ? 1 : d.rounds };
  }
  function isOffer(v) { return v === 'exchange' || v === 'gift' || v === 'request'; }
  function step(item, shift) { return item === BEAVERS ? (shift ? 10 : 1) : (shift ? 1 : 10); }
  function stepped(value, st, up, min, max) {
    var next = up ? Math.floor(value / st) * st + st : Math.ceil(value / st) * st - st;
    return Math.max(min, Math.min(max, next));
  }
  function roundsText(rounds, repeat, give, giveId, get, getId) {
    if (repeat) return 'Round after round, until you both agree to stop.';
    if (rounds <= 1) return 'Once.';
    var a = give > 0 ? amountOf(give * rounds, giveId) : 'nothing', b = get > 0 ? amountOf(get * rounds, getId) : 'nothing';
    return rounds + ' rounds: ' + a + ' for ' + b + ' in all.';
  }

  // ---- the simulation (what the tick does in the game) ----
  var timer = null;
  function running() { return S.exchange && S.exchange.state === 'active' && !(S.exchange.cancelAsked[0] || S.exchange.cancelAsked[1]); }
  function tick() {
    S.dayTicks++;
    if (S.dayTicks >= 14) { S.dayTicks = 0; S.day++; if (S.day > 30) { S.day = 1; S.cycle++; } }
    var x = S.exchange;
    if (running()) {
      var crossed = false;
      for (var c = 0; c < 2; c++) {
        var side = x.side[c];
        if (side.total <= 0 || good(side.good).special) continue;
        if (side.held >= side.total) continue;
        if (S.workers[c] === 0) continue;
        // Each worker carries a load from storage to the half; what is not in storage cannot come.
        var load = Math.min(side.total - side.held, S.workers[c] * 8, S.stock[c][side.good]);
        if (load > 0) { side.held += load; S.stock[c][side.good] -= load; }
      }
      if (isIn(0) && isIn(1)) { cross(); crossed = true; }
      render(crossed);
    } else {
      renderClock();
    }
  }
  function isIn(c) {
    var side = S.exchange.side[c];
    if (side.total <= 0) return true;
    if (side.good === SCIENCE) return S.stock[c].science >= side.total;
    if (side.good === BEAVERS) return S.stock[c].beaver - 1 >= side.total;
    return side.held >= side.total;
  }
  function cross() {
    var x = S.exchange;
    for (var c = 0; c < 2; c++) {
      var side = x.side[c], other = 1 - c;
      if (side.total <= 0) continue;
      if (side.good === SCIENCE || side.good === BEAVERS) { S.stock[c][side.good] -= side.total; }
      S.stock[other][side.good] += side.total;
      var key = c + '>' + other; S.totals[key][side.good] = (S.totals[key][side.good] || 0) + side.total;
      side.held = 0;
    }
    for (c = 0; c < 2; c++) {
      var mine = x.side[c], theirs = x.side[1 - c];
      S.ledger[c].unshift({ cycle: S.cycle, day: S.day, gave: mine.total > 0 ? mine.good : null, gaveN: mine.total, got: theirs.total > 0 ? theirs.good : null, gotN: theirs.total });
      if (S.ledger[c].length > 20) S.ledger[c].length = 20;
    }
    x.done++;
    var beavers = x.side[0].good === BEAVERS ? [0, x.side[0].total] : x.side[1].good === BEAVERS ? [1, x.side[1].total] : null;
    say('Round ' + x.done + ' crossed: both sides were in.' + (beavers ? ' ' + beavers[1] + ' beaver' + (beavers[1] === 1 ? '' : 's') + ' joined ' + colored(1 - beavers[0]) + ' from ' + colored(beavers[0]) + ' through the Trading Post.' : ''));
    if (!x.repeat && x.done >= x.rounds) {
      S.exchange = null;
      say('The exchange is complete. The Trading Post is free for the next one.');
    }
  }
  function ensureTimer() {
    if (timer) return;
    timer = setInterval(function () { if (!document.hidden) tick(); }, 650);
  }

  // ---- actions (the replayed events) ----
  function propose() {
    var j = judge(S.draft); if (!isOffer(j.verdict) || S.exchange) return;
    var d = S.draft, me = S.me, them = 1 - me;
    var side = [];
    side[me] = { good: j.give > 0 ? d.give : null, total: j.give, held: 0 };
    side[them] = { good: j.get > 0 ? d.get : null, total: j.get, held: 0 };
    S.exchange = { by: me, side: side, rounds: j.rounds, repeat: d.repeat, done: 0, state: 'proposed', cancelAsked: [false, false], serial: ++S.serial };
    say(colored(me) + ' made an offer; ' + colored(them) + ' gets a notice.');
    render(true);
    if (S.auto) setTimeout(function () {
      if (S.exchange && S.exchange.state === 'proposed' && S.exchange.serial === (S.serial)) { S.me === them ? null : null; accept(them, true); }
    }, 1800);
  }
  function accept(who, auto) {
    var x = S.exchange; if (!x || x.state !== 'proposed' || who === x.by) return;
    x.state = 'active';
    say(colored(who) + ' accepted' + (auto ? ' (by itself: untick "answers by itself" to play both sides)' : '') + '. Each colony\'s workers now bring its side to its own half.');
    render(true);
  }
  function decline(who) {
    var x = S.exchange; if (!x || x.state !== 'proposed') return;
    S.exchange = null;
    say(who === x.by ? colored(who) + ' withdrew the offer.' : colored(who) + ' declined.');
    render(true);
  }
  function askCancel(who) {
    var x = S.exchange; if (!x || x.state !== 'active') return;
    x.cancelAsked[who] = true;
    say(colored(who) + ' asked to end the exchange. Nothing is brought and nothing crosses until ' + colored(1 - who) + ' answers.');
    render(true);
    if (S.auto && who === S.me) setTimeout(function () { if (S.exchange === x && x.cancelAsked[who]) agreeCancel(1 - who, true); }, 1800);
  }
  function keep(who) {
    var x = S.exchange; if (!x) return;
    x.cancelAsked = [false, false];
    say(colored(who) + ' chose to keep trading; the round goes on.');
    render(true);
  }
  function agreeCancel(who, auto) {
    var x = S.exchange; if (!x) return;
    // What waits on each half goes back into its own colony's storage; rounds that crossed stay crossed.
    for (var c = 0; c < 2; c++) { var s = x.side[c]; if (s.good && !good(s.good).special) S.stock[c][s.good] += s.held; }
    S.exchange = null;
    say(colored(who) + (auto ? ' agreed to cancel (by itself)' : ' agreed to cancel') + '. What waited on each half went back home; ' + x.done + ' round' + (x.done === 1 ? '' : 's') + ' stay' + (x.done === 1 ? 's' : '') + ' in the ledger.');
    render(true);
  }

  // ---- rendering ----
  var root, side, logEl;
  function esc(s) { return String(s).replace(/[&<>"]/g, function (ch) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[ch]; }); }
  function say(html) { S.log.unshift(html); if (S.log.length > 4) S.log.length = 4; if (logEl) logEl.innerHTML = S.log.map(function (l) { return '<div>' + l + '</div>'; }).join(''); }
  function renderClock() { var el = root.querySelector('[data-clock]'); if (el) el.textContent = 'Cycle ' + S.cycle + ', day ' + S.day; }

  function render(flash) {
    var me = S.me, them = 1 - me, x = S.exchange;
    var h = '';
    // header: "Trading with Colony 2" [All posts]
    h += '<div class="tp-head"><span class="tp-title">Trading with ' + colored(them) + '</span><button class="tp-btn tp-btn--small" type="button" disabled title="Opens the trading posts and colonies window (Ctrl+T) in the game.">All posts</button></div>';

    if (!x) {
      h += renderCompose(me, them);
    } else if (x.state === 'proposed') {
      h += renderProposal(me, them, x);
    } else {
      h += renderActive(me, them, x);
    }
    // ledger
    h += '<div class="tp-rule"></div><span class="tp-caption" title="Every round that crossed at this Trading Post, newest first.">Ledger</span>';
    var L = S.ledger[me];
    if (!L.length) h += '<div class="tp-muted">No round has crossed here yet.</div>';
    for (var i = 0; i < Math.min(L.length, 4); i++) {
      var r = L[i];
      h += '<div class="tp-ledger-row"><span class="tp-date" title="Cycle ' + r.cycle + ', day ' + r.day + '">' + r.cycle + '-' + r.day + '</span>'
        + ledgerPart(colored(me) + ' gave', r.gave, r.gaveN) + ledgerPart(colored(them) + ' gave', r.got, r.gotN) + '</div>';
    }
    if (L.length > 4) h += '<div class="tp-muted">and ' + (L.length - 4) + ' earlier</div>';
    // totals
    h += '<div class="tp-rule"></div><span class="tp-caption">Traded with ' + esc(COLONY[them]) + ' (all posts)</span>';
    h += chips(colored(me) + ' sent', S.totals[me + '>' + them]) + chips(colored(them) + ' sent', S.totals[them + '>' + me]);
    var focused = focusKey();
    root.innerHTML = h;
    renderClock();
    renderSide();
    bind();
    restoreFocus(focused);
    if (flash) { root.classList.remove('tp-flash'); void root.offsetWidth; root.classList.add('tp-flash'); }
  }

  // Every render replaces the panel's markup, so keyboard focus is carried across by the control's data attributes.
  // When that control is gone (Accept after it was pressed, say), focus goes to the panel itself, not the page top.
  var FOCUS_ATTRS = ['data-pick', 'data-choose', 'data-step', 'data-dir', 'data-rounds', 'data-amount', 'data-rounds-box',
    'data-repeat', 'data-propose', 'data-withdraw', 'data-accept', 'data-decline', 'data-ask', 'data-agree', 'data-keep'];
  function focusKey() {
    var el = document.activeElement;
    if (!el || !root.contains(el)) return null;
    var sel = '';
    for (var i = 0; i < FOCUS_ATTRS.length; i++) {
      var a = FOCUS_ATTRS[i];
      if (el.hasAttribute(a)) sel += '[' + a + '="' + el.getAttribute(a).replace(/"/g, '') + '"]';
    }
    return sel || '*';
  }
  function restoreFocus(sel) {
    if (!sel) return;
    var el = sel !== '*' && root.querySelector(sel);
    if (el && !el.disabled) { el.focus(); return; }
    root.setAttribute('tabindex', '-1');
    root.focus();
  }

  function renderCompose(me, them) {
    var d = S.draft, h = '';
    h += offerSide('give', 'You give', d.give, 'You have ' + S.stock[me][d.give], d.giveAmount);
    h += offerSide('get', 'You get', d.get, esc(COLONY[them]) + ' has ' + S.stock[them][d.get], d.getAmount);
    // rounds card
    h += '<div class="tp-card"><div class="tp-head"><span class="tp-caption">Rounds</span><span class="tp-muted">up to ' + MAX_AMOUNT + ' of each per round</span></div>'
      + '<div class="tp-row"><button class="tp-btn tp-sq" type="button" data-rounds="-1"' + (d.repeat ? ' disabled' : '') + ' aria-label="One round fewer" title="-1 (Shift+click: -10)">&minus;</button>'
      + '<input class="tp-input tp-input--rounds" type="text" inputmode="numeric" maxlength="2" value="' + d.rounds + '" data-rounds-box' + (d.repeat ? ' disabled' : '') + ' aria-label="Rounds, 1 to 99" title="How many times the exchange runs: 1 to 99.">'
      + '<button class="tp-btn tp-sq" type="button" data-rounds="1"' + (d.repeat ? ' disabled' : '') + ' aria-label="One round more" title="+1 (Shift+click: +10)">+</button>'
      + '<label class="tp-check" style="margin-left:10px" title="A standing deal: round after round, until both colonies agree to end it."><input type="checkbox" data-repeat' + (d.repeat ? ' checked' : '') + '><span class="box"></span>Repeat until cancelled</label></div></div>';
    // summary
    var j = judge(d), text, ok = isOffer(j.verdict);
    switch (j.verdict) {
      case 'exchange': text = esc(COLONY[them]) + ' gets ' + amountOf(j.give, d.give) + ', and you get ' + amountOf(j.get, d.get) + '.'; break;
      case 'gift': text = 'A gift: ' + esc(COLONY[them]) + ' gets ' + amountOf(j.give, d.give) + ', and you ask nothing back.'; break;
      case 'request': text = 'A request: you ask ' + esc(COLONY[them]) + ' for ' + amountOf(j.get, d.get) + ', and give nothing.'; break;
      case 'badAmount': text = 'Each side gives 0 to ' + MAX_AMOUNT + ' per round. For more, add rounds.'; break;
      case 'badRounds': text = 'Rounds go from 1 to ' + MAX_ROUNDS + '.'; break;
      case 'nothing': text = 'Set an amount above 0 on at least one side.'; break;
      case 'same': text = 'Choose two different goods.'; break;
      default: text = 'Choose what to trade.';
    }
    if (ok) text += ' ' + roundsText(j.rounds, d.repeat, j.give, d.give, j.get, d.get);
    h += '<div class="tp-notice ' + (ok ? 'tp-muted' : 'tp-warn') + '" style="font-size:12px;margin-left:1px">' + text + '</div>';
    h += '<button class="tp-btn tp-btn--full" type="button" data-propose' + (ok ? '' : ' disabled') + ' title="Each round, your Trading Post workers bring what you give to your half, and ' + esc(COLONY[them]) + '\'s bring theirs to their half. When both are in, the round crosses.">Make offer</button>';
    return h;
  }

  function offerSide(which, caption, item, stock, amount) {
    var open = S.pickerOpen === which;
    var h = '<div class="tp-card"><div class="tp-head"><span class="tp-caption">' + caption + '</span><span class="tp-muted">' + stock + '</span></div>'
      + '<div class="tp-row"><button class="tp-btn tp-select" type="button" data-pick="' + which + '" aria-expanded="' + open + '" title="Choose what to trade">' + icon(item, 60) + '<span>' + esc(good(item).name) + '</span><i></i></button>'
      + '<button class="tp-btn tp-sq" type="button" data-step="' + which + '" data-dir="-1" aria-label="' + step(item, false) + ' fewer ' + esc(good(item).plural) + ' you ' + which + '" title="-' + step(item, false) + ' (Shift+click: -' + step(item, true) + ')">&minus;</button>'
      + '<input class="tp-input tp-input--amount" type="text" inputmode="numeric" maxlength="3" value="' + amount + '" data-amount="' + which + '" aria-label="' + esc(good(item).plural) + ' you ' + which + ' each round, 0 to ' + MAX_AMOUNT + '" title="How many each round: 0 to ' + MAX_AMOUNT + '.">'
      + '<button class="tp-btn tp-sq" type="button" data-step="' + which + '" data-dir="1" aria-label="' + step(item, false) + ' more ' + esc(good(item).plural) + ' you ' + which + '" title="+' + step(item, false) + ' (Shift+click: +' + step(item, true) + ')">+</button></div>';
    if (open) {
      var owner = which === 'give' ? S.me : 1 - S.me;
      h += '<div class="tp-picker"><span class="tp-caption">' + (which === 'give' ? 'What you give' : 'What you ask ' + esc(COLONY[1 - S.me]) + ' for') + '</span><div class="tp-grid">';
      for (var i = 0; i < GOODS.length; i++) {
        var g = GOODS[i], n = S.stock[owner][g.id];
        h += '<button class="tp-cell" type="button" data-choose="' + g.id + '" aria-pressed="' + (g.id === item) + '" title="' + esc(g.name) + ': ' + n + (g.id === BEAVERS ? ' adults' : g.id === SCIENCE ? ' in the pool' : ' in stock') + '">' + icon(g.id, 60) + esc(g.name) + '<small>' + n + '</small></button>';
      }
      h += '</div></div>';
    }
    return h + '</div>';
  }

  function renderProposal(me, them, x) {
    var mine = x.side[me], theirs = x.side[them], byMe = x.by === me;
    var h = '<div class="tp-card"><div class="tp-head"><span class="tp-text tp-bold">' + (byMe ? 'Your offer' : colored(them) + ' offers an exchange') + '</span><span class="tp-muted">' + (byMe ? 'waiting for ' + esc(COLONY[them]) : '') + '</span></div>';
    h += term('You give', mine, byMe ? '' : 'you have ' + S.stock[me][mine.good || 'logs']);
    h += term('You get', theirs, '');
    h += '<div class="tp-muted" style="margin-top:3px">' + roundsText(x.rounds, x.repeat, mine.total, mine.good, theirs.total, theirs.good) + '</div></div>';
    if (byMe) h += '<div class="tp-btns"><button class="tp-btn tp-btn--red" type="button" data-withdraw>Withdraw offer</button></div>';
    else h += '<div class="tp-btns"><button class="tp-btn" type="button" data-accept>Accept</button><button class="tp-btn tp-btn--red" type="button" data-decline>Decline</button></div>';
    return h;
  }
  function term(caption, s, note) {
    if (s.total <= 0 || !s.good) return '<div class="tp-term"><span class="tp-caption">' + caption + '</span><span class="tp-text">nothing</span></div>';
    return '<div class="tp-term"><span class="tp-caption">' + caption + '</span>' + icon(s.good, 40) + '<span class="tp-text">' + amountOf(s.total, s.good) + '</span>' + (note ? '<span class="tp-muted">' + note + '</span>' : '') + '</div>';
  }

  function renderActive(me, them, x) {
    var mine = x.side[me], theirs = x.side[them];
    var onHold = x.cancelAsked[0] || x.cancelAsked[1];
    var h = '<div class="tp-card"><div class="tp-head"><span class="tp-text tp-bold">Exchange under way</span><span class="tp-muted">' + (x.repeat ? 'round ' + (x.done + 1) + ', repeating' : 'round ' + (x.done + 1) + ' of ' + x.rounds) + '</span></div>';
    h += progress('You give', mine, me, true) + progress('You get', theirs, them, false);
    var mineIn = isIn(me), theirsIn = isIn(them), status;
    if (onHold) status = 'On hold: a colony asked to end the exchange.';
    else if (!mineIn) {
      if (mine.good === SCIENCE) status = 'Your colony needs ' + (mine.total - S.stock[me].science) + ' more science for this round.';
      else if (mine.good === BEAVERS) status = 'Your district needs ' + mine.total + ' adults to spare (one adult always stays).';
      else if (S.workers[me] === 0) status = 'Your half has no workers: assign some so they bring your side.';
      else status = 'Your workers are bringing ' + (mine.total - mine.held) + ' more ' + nameOf(mine.good, 2) + ' to your half.';
    } else if (!theirsIn) {
      if (good(theirs.good).special) status = 'Your side is in. Waiting for ' + esc(COLONY[them]) + '\'s ' + nameOf(theirs.good, 2) + '.';
      else if (S.workers[them] === 0) status = 'Your side is in. Waiting for ' + esc(COLONY[them]) + ' to bring ' + (theirs.total - theirs.held) + ' more ' + nameOf(theirs.good, 2) + ' (their half has no workers).';
      else status = 'Your side is in. Waiting for ' + esc(COLONY[them]) + ' to bring ' + (theirs.total - theirs.held) + ' more ' + nameOf(theirs.good, 2) + '.';
    } else status = 'Both sides are in: crossing now.';
    h += '<div class="tp-status' + (onHold ? ' tp-warn' : '') + '">' + status + '</div></div>';
    // ending takes both
    var iAsked = x.cancelAsked[me], theyAsked = x.cancelAsked[them];
    h += '<div class="tp-cancel">';
    if (theyAsked) h += '<span class="tp-muted tp-warn">' + colored(them) + ' asks to end this exchange. Nothing crosses until you answer.</span>';
    else if (iAsked) h += '<span class="tp-muted">You asked ' + esc(COLONY[them]) + ' to end this exchange. Nothing crosses until they answer.</span>';
    h += '<div class="tp-btns">';
    if (theyAsked) h += '<button class="tp-btn tp-btn--red" type="button" data-agree title="Ends the exchange. What waits on each half goes back to its own colony; rounds that crossed stay crossed.">Agree to cancel</button><button class="tp-btn" type="button" data-keep>Keep trading</button>';
    else if (iAsked) h += '<button class="tp-btn" type="button" data-keep>Keep trading</button>';
    else h += '<button class="tp-btn tp-btn--red" type="button" data-ask title="Asks ' + esc(COLONY[them]) + ' to end the exchange. It ends when both colonies agree; what waits on each half then goes back home.">Cancel exchange</button>';
    h += '</div></div>';
    return h;
  }
  function progress(caption, s, owner, own) {
    if (s.total <= 0) return '';
    var have, note;
    if (s.good === SCIENCE) { have = Math.min(S.stock[owner].science, s.total); note = 'to spare in the pool'; }
    else if (s.good === BEAVERS) { have = Math.max(0, Math.min(S.stock[owner].beaver - 1, s.total)); note = 'adults to spare'; }
    else { have = Math.min(s.held, s.total); note = own ? 'on your half' : 'on their half'; }
    var pct = Math.round(100 * have / s.total);
    return '<div class="tp-progress"><div class="tp-head"><span class="tp-caption">' + caption + '</span><span class="tp-muted">' + note + '</span></div>'
      + '<div class="tp-line">' + icon(s.good, 60) + '<div class="tp-bar' + (own ? '' : ' tp-bar--green') + '"><i style="transform:scaleX(' + (pct / 100) + ')"></i><span>' + have + ' / ' + s.total + ' ' + nameOf(s.good, s.total) + '</span></div></div></div>';
  }
  function ledgerPart(caption, item, n) {
    return '<span class="tp-part"><span class="tp-muted">' + caption + '</span>' + (n > 0 && item ? icon(item, 40) + '<span>' + n + '</span>' : '<span>nothing</span>') + '</span>';
  }
  function chips(caption, totals) {
    var keys = Object.keys(totals).sort(function (a, b) { return totals[b] - totals[a]; });
    var h = '<div class="tp-chips"><span class="tp-muted">' + caption + '</span><span class="tp-chiplist">';
    if (!keys.length) h += '<span class="tp-muted">nothing</span>';
    for (var i = 0; i < keys.length; i++) h += '<span class="tp-chip" title="' + amountOf(totals[keys[i]], keys[i]) + '">' + icon(keys[i], 40) + totals[keys[i]] + '</span>';
    return h + '</span></div>';
  }

  // the controls beside the panel
  function renderSide() {
    var q = function (s) { return side.querySelector(s); };
    q('[data-me="0"]').setAttribute('aria-pressed', S.me === 0);
    q('[data-me="1"]').setAttribute('aria-pressed', S.me === 1);
    q('[data-auto]').checked = S.auto;
    var w = q('[data-workers]'); if (w) w.value = S.workers[S.me];
    var head = document.querySelector('[data-tp-half]'); if (head) head.textContent = COLONY[S.me] + "'s half (colony " + (S.me + 1) + ")";
    var wl = document.querySelector('[data-tp-workers]'); if (wl) wl.textContent = S.workers[S.me];
  }

  // ---- events ----
  function bind() {
    var q = function (s) { return root.querySelector(s); }, qa = function (s) { return root.querySelectorAll(s); };
    var b;
    if ((b = q('[data-propose]'))) b.addEventListener('click', propose);
    if ((b = q('[data-withdraw]'))) b.addEventListener('click', function () { decline(S.me); });
    if ((b = q('[data-accept]'))) b.addEventListener('click', function () { accept(S.me, false); });
    if ((b = q('[data-decline]'))) b.addEventListener('click', function () { decline(S.me); });
    if ((b = q('[data-ask]'))) b.addEventListener('click', function () { askCancel(S.me); });
    if ((b = q('[data-agree]'))) b.addEventListener('click', function () { agreeCancel(S.me, false); });
    if ((b = q('[data-keep]'))) b.addEventListener('click', function () { keep(S.me); });
    Array.prototype.forEach.call(qa('[data-pick]'), function (el) {
      el.addEventListener('click', function () { var w = el.getAttribute('data-pick'); S.pickerOpen = S.pickerOpen === w ? null : w; render(); });
    });
    Array.prototype.forEach.call(qa('[data-choose]'), function (el) {
      el.addEventListener('click', function () {
        var id = el.getAttribute('data-choose'), w = S.pickerOpen;
        if (w === 'give') { if (S.draft.get === id) S.draft.get = S.draft.give; S.draft.give = id; }
        else { if (S.draft.give === id) S.draft.give = S.draft.get; S.draft.get = id; }
        if (id === BEAVERS) S.draft[w + 'Amount'] = Math.min(S.draft[w + 'Amount'], 5);
        S.pickerOpen = null; render();
      });
    });
    Array.prototype.forEach.call(qa('[data-step]'), function (el) {
      el.addEventListener('click', function (e) {
        var w = el.getAttribute('data-step'), up = el.getAttribute('data-dir') === '1', item = S.draft[w];
        S.draft[w + 'Amount'] = stepped(S.draft[w + 'Amount'], step(item, e.shiftKey), up, 0, MAX_AMOUNT); render();
      });
    });
    Array.prototype.forEach.call(qa('[data-amount]'), function (el) {
      el.addEventListener('input', function () { var v = parseInt(el.value, 10); S.draft[el.getAttribute('data-amount') + 'Amount'] = isNaN(v) ? NaN : v; softRender(el); });
    });
    Array.prototype.forEach.call(qa('[data-rounds]'), function (el) {
      el.addEventListener('click', function (e) { S.draft.rounds = stepped(S.draft.rounds, e.shiftKey ? 10 : 1, el.getAttribute('data-rounds') === '1', 1, MAX_ROUNDS); render(); });
    });
    if ((b = q('[data-rounds-box]'))) b.addEventListener('input', function () { var v = parseInt(b.value, 10); S.draft.rounds = isNaN(v) ? NaN : v; softRender(b); });
    if ((b = q('[data-repeat]'))) b.addEventListener('change', function () { S.draft.repeat = b.checked; render(); });
  }
  // Re-render while typing without losing the caret: swap everything but the focused box.
  function softRender(focused) {
    var sel = focused.hasAttribute('data-amount') ? '[data-amount="' + focused.getAttribute('data-amount') + '"]' : '[data-rounds-box]';
    var start = focused.selectionStart, end = focused.selectionEnd, val = focused.value;
    render();
    var again = root.querySelector(sel);
    if (again) { again.value = val; again.focus(); try { again.setSelectionRange(start, end); } catch (e) { /* not selectable */ } }
  }

  function preset(name) {
    S.exchange = null; S.pickerOpen = null;
    if (name === 'logs') S.draft = { give: 'logs', get: 'gear', giveAmount: 100, getAmount: 25, rounds: 4, repeat: false };
    if (name === 'gift') S.draft = { give: 'berries', get: 'carrot', giveAmount: 60, getAmount: 0, rounds: 1, repeat: false };
    if (name === 'science') S.draft = { give: SCIENCE, get: 'berries', giveAmount: 50, getAmount: 30, rounds: 1, repeat: false };
    if (name === 'beavers') S.draft = { give: BEAVERS, get: 'carrot', giveAmount: 2, getAmount: 100, rounds: 1, repeat: false };
    if (name === 'standing') S.draft = { give: 'logs', get: 'carrot', giveAmount: 20, getAmount: 10, rounds: 1, repeat: true };
    render(true);
  }

  function init() {
    root = document.querySelector('[data-tp-panel]');
    side = document.querySelector('[data-tp-side]');
    logEl = document.querySelector('[data-tp-log]');
    if (!root || !side) return;
    Array.prototype.forEach.call(side.querySelectorAll('[data-me]'), function (el) {
      el.addEventListener('click', function () { S.me = parseInt(el.getAttribute('data-me'), 10); S.pickerOpen = null; render(true); say('You are now ' + colored(S.me) + ' (colony ' + (S.me + 1) + '), at their half.'); });
    });
    Array.prototype.forEach.call(side.querySelectorAll('[data-preset]'), function (el) {
      el.addEventListener('click', function () { preset(el.getAttribute('data-preset')); });
    });
    var a = side.querySelector('[data-auto]'); if (a) a.addEventListener('change', function () { S.auto = a.checked; });
    var w = side.querySelector('[data-workers]'); if (w) w.addEventListener('change', function () { S.workers[S.me] = Math.max(0, Math.min(2, parseInt(w.value, 10) || 0)); render(); });
    var r = side.querySelector('[data-reset]'); if (r) r.addEventListener('click', reset);
    reset();
    ensureTimer();
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();
