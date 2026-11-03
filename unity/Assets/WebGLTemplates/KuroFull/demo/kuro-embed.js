// Мост демо ↔ 3D-офис Unity. Подключается к копии demo/index.html при WebGL-сборке
// (WebBuild.CopyDemo), сам файл демо не меняется.
// В офисе картинка демо скрыта — вместо нее 3D. Окна демо открываются по предметам Unity
// (kuroOpen), а странице сообщается, занято ли демо (титул, окно поверх) или свободен офис,
// и когда в офисе начинается день или наступает вечер (страница открывает и закрывает глаза),
// сколько непрочитанных сообщений и открытых задач (телефон и лоток в 3D).
// После «Создать мир» страница показывает ролик прихода в деловой центр
// (экран демо arrive прозрачный), по его концу — kuroArrived и первый день.
window.kuroEmbedded = true;
(function () {
  var demo = window.kuroDemo;
  if (!demo) {
    console.warn("kuro-embed: демо не открыло состояние (window.kuroDemo)");
    return;
  }
  var stage = document.getElementById("stage");
  var root = document.documentElement;

  // Сколько длятся глаза на странице (шаблон KuroFull). Пока они открываются или закрываются,
  // интерфейс демо скрыт; вечерняя сводка появляется, когда глаза уже закрыты.
  var WAKE_MS = 3200, SLEEP_MS = 1600;

  var css = document.createElement("style");
  css.textContent =
    "html.kuro-office,html.kuro-office body,html.kuro-office #vp,html.kuro-office #stage" +
    "{background:transparent!important;box-shadow:none!important}" +
    "html.kuro-office .office,html.kuro-office .ohint{visibility:hidden}" +
    "html.kuro-office:not(.kuro-wake):not(.kuro-sleep) #stage{transition:opacity .45s}" +
    "html.kuro-wake #stage,html.kuro-sleep #stage{opacity:0}";
  document.head.appendChild(css);

  // Предмет в Unity → то же действие, что клик по картинке офиса в демо.
  var ACTIONS = {
    pc: ["openPC"], tray: ["openTray"], book: ["openBook"], phone: ["modal", "phone"],
    accounts: ["modal", "accounts"], terms: ["modal", "terms"], docs: ["modal", "docs"],
    cal: ["modal", "cal"], clock: ["clock"], door: ["door"], pause: ["modal", "pause"],
  };

  function busy() {
    var S = demo.S;
    return S.screen !== "office" || !!(S.pc || S.tray || S.bookOpen || S.modal || S.meet || S.talk || S.night);
  }

  function post(msg) {
    parent.postMessage(msg, "*");
  }

  // Новый день в офисе — "wake", вечер — "sleep", ролик прихода — "arrive", вне офиса — "off".
  var lastPhase = null, lastDay = null, phaseTimer = 0;
  function eyes() {
    var S = demo.S;
    var phase = S.screen === "arrive" ? "arrive" : S.screen !== "office" ? "off" : S.night ? "sleep" : "wake";
    if (phase === lastPhase && S.day === lastDay) return;
    if (phase === "off" && lastPhase === "off") {
      lastDay = S.day;
      return;
    }
    lastPhase = phase;
    lastDay = S.day;

    clearTimeout(phaseTimer);
    root.classList.remove("kuro-wake", "kuro-sleep");
    var ms = phase === "wake" ? WAKE_MS : phase === "sleep" ? SLEEP_MS : 0;
    if (ms) {
      var cls = "kuro-" + phase;
      root.classList.add(cls);
      phaseTimer = setTimeout(function () {
        root.classList.remove(cls);
      }, ms);
    }
    var d = demo.DAYS[S.day] || {};
    // note — название личной компании, команды и прогноз прибыли.
    var note = window.kuroDayNote ? window.kuroDayNote() : "";
    post({ kuro: "eyes", phase: phase, ms: ms, wd: d.wd || "", date: d.d ? d.d + " " + (d.m || "января") : "", tasks: demo.openTasks(), note: note });
  }

  // Для 3D-офиса: телефон звонит при непрочитанных, в лотке столько листов, сколько задач.
  var lastOffice = "";
  function office() {
    var key = (demo.S.unread || 0) + "," + demo.openTasks();
    if (key === lastOffice) return;
    lastOffice = key;
    post({ kuro: "office", data: key });
  }

  var last = null;
  function sync() {
    root.classList.toggle("kuro-office", demo.S.screen === "office" || demo.S.screen === "arrive");
    eyes();
    office();
    var b = busy();
    if (b !== last) {
      last = b;
      post({ kuro: "mode", busy: b });
    }
  }
  new MutationObserver(sync).observe(stage, { childList: true, subtree: true });

  function press(act) {
    var b = document.createElement("button");
    b.hidden = true;
    b.dataset.act = act;
    stage.appendChild(b);
    b.click();
    b.remove();
  }

  // Страница → демо: ролик прихода окончен (экран уже затемнен) — начать первый день.
  window.kuroArrived = function () {
    press("arrived");
  };

  // Страница загрузила 3D: перерисовать комикс, чтобы его анимация не прошла под заставкой.
  window.addEventListener("message", function (e) {
    if (e.source === parent && e.data && e.data.kuro === "ready" && demo.S.screen === "comic") press("redraw");
  });

  window.kuroOpen = function (action) {
    var a = ACTIONS[action];
    if (a && !busy()) {
      var b = document.createElement("button");
      b.hidden = true;
      b.dataset.act = a[0];
      if (a[1]) b.dataset.m = a[1];
      stage.appendChild(b);
      b.click();
      b.remove();
    }
    // Окно не открылось (у часов — только всплывашка): отпустить камеру, когда ее прочитают.
    if (!busy()) {
      setTimeout(function () {
        if (!busy()) post({ kuro: "mode", busy: false });
      }, a && a[0] === "clock" ? 1500 : 0);
    }
  };

  sync();
})();
