// Browser WebSocket for the WebGL build. Messages are queued here and
// polled from C# each frame, which avoids SendMessage string round-trips.
mergeInto(LibraryManager.library, {
  SgSocketConnect: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var old = window.sgSocket;
    if (old) { try { old.close(); } catch (e) {} }
    window.sgInbox = [];
    window.sgState = 0;
    var ws = new WebSocket(url);
    window.sgSocket = ws;
    ws.onopen = function () { if (window.sgSocket === ws) window.sgState = 1; };
    ws.onmessage = function (e) {
      if (window.sgSocket === ws && typeof e.data === 'string') window.sgInbox.push(e.data);
    };
    ws.onclose = function () { if (window.sgSocket === ws) window.sgState = 3; };
    ws.onerror = function () { if (window.sgSocket === ws) window.sgState = 3; };
  },

  SgSocketSend: function (msgPtr) {
    var ws = window.sgSocket;
    if (ws && ws.readyState === 1) ws.send(UTF8ToString(msgPtr));
  },

  SgSocketClose: function () {
    var ws = window.sgSocket;
    window.sgSocket = null;
    window.sgState = 3;
    if (ws) { try { ws.close(); } catch (e) {} }
  },

  // 0 connecting, 1 open, 3 closed
  SgSocketState: function () {
    return window.sgState === undefined ? 3 : window.sgState;
  },

  SgSocketPoll: function () {
    var q = window.sgInbox;
    if (!q || q.length === 0) return 0;
    var s = q.shift();
    var size = lengthBytesUTF8(s) + 1;
    var buf = _malloc(size);
    stringToUTF8(s, buf, size);
    return buf;
  },

  // ?server=wss://... on the page URL overrides the default server.
  SgPageQuery: function (keyPtr) {
    var key = UTF8ToString(keyPtr);
    var v = new URLSearchParams(window.location.search).get(key) || '';
    var size = lengthBytesUTF8(v) + 1;
    var buf = _malloc(size);
    stringToUTF8(v, buf, size);
    return buf;
  }
});
