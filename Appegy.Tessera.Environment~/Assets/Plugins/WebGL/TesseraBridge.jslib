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
  },

  // Background image: open a native file picker, hand the chosen image to the build as a blob URL
  // (loaded with UnityWebRequestTexture). Runtime-only; never persisted or shared.
  TesseraPickImage: function (targetPtr) {
    try {
      var target = UTF8ToString(targetPtr);
      var input = document.createElement('input');
      input.type = 'file';
      input.accept = 'image/*';
      input.style.display = 'none';
      input.addEventListener('change', function () {
        if (input.files && input.files[0] && window.tesseraUnity) {
          var url = URL.createObjectURL(input.files[0]);
          window.tesseraUnity.SendMessage(target, 'OnImagePicked', url);
        }
        if (input.parentNode) input.parentNode.removeChild(input);
      });
      document.body.appendChild(input);
      input.click();
    } catch (e) {}
  },
  TesseraRevokeUrl: function (urlPtr) {
    try { URL.revokeObjectURL(UTF8ToString(urlPtr)); } catch (e) {}
  }
});
