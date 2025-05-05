var LibraryGCClient = {
  $state: {},

  _SetOnOpen: function (callback) {},

  _SetOnMessage: function (callback) {
    state.onMessage = callback;
  },

  SendMessage: function (data) {
    if (data instanceof ArrayBuffer) {
      var dataBuffer = new Uint8Array(data);

      var buffer = _malloc(dataBuffer.length);
      HEAPU8.set(dataBuffer, buffer);

      try {
        Module["dynCall_vii"](state.onMessage, buffer, dataBuffer.length);
      } finally {
        _free(buffer);
      }
    }
  },

  _GCGameMessageToJS: function (bufferPtr, offset, count, isReliable) {
    window.gcNetHandleJsonMessage(
      HEAPU8.buffer.slice(bufferPtr + offset, bufferPtr + count - offset),
      Boolean(isReliable)
    );
  },
};

autoAddDeps(LibraryGCClient, "$state");
mergeInto(LibraryManager.library, LibraryGCClient);
