mergeInto(LibraryManager.library, {
  TesseraSetTheme: function (isLight) {
    try {
      if (typeof window.setTesseraTheme === 'function') {
        window.setTesseraTheme(isLight === 1);
      }
    } catch (e) {}
  },

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
  }
});
