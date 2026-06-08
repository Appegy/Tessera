mergeInto(LibraryManager.library, {
  TesseraSetTheme: function (isLight) {
    try {
      if (typeof window.setTesseraTheme === 'function') {
        window.setTesseraTheme(isLight === 1);
      }
    } catch (e) {}
  }
});