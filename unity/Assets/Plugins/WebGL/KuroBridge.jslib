// Мост из Unity на страницу (см. WebBridge.cs и шаблон KuroFull):
// открыть окно демо для предмета, показать подпись предмета под мышью, ролик прихода окончен.
mergeInto(LibraryManager.library, {
  KuroOpen: function (action) {
    var a = UTF8ToString(action);
    if (window.kuroOpen) window.kuroOpen(a);
  },
  KuroHover: function (label) {
    var l = UTF8ToString(label);
    if (window.kuroHover) window.kuroHover(l);
  },
  KuroArrived: function () {
    if (window.kuroArrived) window.kuroArrived();
  },
});
