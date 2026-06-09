mergeInto(LibraryManager.library, {
  // Live-mirror the playground state into the address bar (no reload) so the URL is always a
  // shareable link. `queryPtr` is the body after '?', e.g. "g=voronoi&w=12". The existing hash
  // (e.g. the preview shell's #b=<branch>) is preserved. An empty body clears the query.
  TesseraReplaceQuery: function (queryPtr) {
    try {
      var query = UTF8ToString(queryPtr);
      var base = window.location.origin + window.location.pathname;
      var url = base + (query && query.length ? '?' + query : '') + window.location.hash;
      window.history.replaceState(null, '', url);
    } catch (e) {}
  },

  // Theme is page-driven: the build reads the current page theme at boot and registers a target
  // the page SendMessages to (ApplyPageTheme) when the theme changes.
  TesseraGetTheme: function () {
    try { return document.documentElement.classList.contains('tessera-light') ? 1 : 0; } catch (e) { return 0; }
  },
  TesseraRegisterThemeTarget: function (namePtr) {
    try { window.tesseraThemeTarget = UTF8ToString(namePtr); } catch (e) {}
  }
});
