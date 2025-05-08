var LibraryGCServer = {
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

  _GCGameMessageToJS: function (
    bufferPtr,
    offset,
    count,
    gcClientId,
    isReliable
  ) {
    window.gcHandleGameMessageFromUnity(
      HEAPU8.buffer.slice(bufferPtr + offset, bufferPtr + count - offset),
      Number(gcClientId),
      Boolean(isReliable)
    );
  },
};

autoAddDeps(LibraryGCServer, "$state");
mergeInto(LibraryManager.library, LibraryGCServer);
