mergeInto(LibraryManager.library, {
  GamingCouchInstanceStarted: function () {
    if (!window.gamingCouchInstanceStarted) {
      console.error("gamingCouchInstanceStarted is not defined");
      return;
    }
    window.gamingCouchInstanceStarted();
  },

  GamingCouchRegisterRuntimeInfo: function (runtimeInfoJsonString) {
    if (!window.gamingCouchRegisterRuntimeInfo) {
      console.error("gamingCouchRegisterRuntimeInfo is not defined");
      return;
    }

    var runtimeInfoJson = UTF8ToString(runtimeInfoJsonString);
    var runtimeInfo;
    try {
      runtimeInfo = JSON.parse(runtimeInfoJson);
    } catch (error) {
      console.error("GamingCouchRegisterRuntimeInfo received invalid JSON", error);
      return;
    }

    window.gamingCouchRegisterRuntimeInfo(runtimeInfo);
  },

  GamingCouchSetupDone: function () {
    if (!window.gamingCouchSetupDone) {
      console.error("GamingCouchSetupDone is not defined");
      return;
    }
    window.gamingCouchSetupDone();
  },

  GamingCouchSetupHud: function (hudConfigJsonString) {
    if (!window.gamingCouchSetupHud) {
      console.error("gamingCouchSetupHud is not defined");
      return;
    }

    var hudConfig = JSON.parse(UTF8ToString(hudConfigJsonString));
    window.gamingCouchSetupHud(hudConfig);
  },

  GamingCouchSendProjectInfo: function (projectNameString) {
    if (!window.gamingCouchSendProjectInfo) {
      console.error("gamingCouchSendProjectInfo is not defined");
      return;
    }

    var projectName = UTF8ToString(projectNameString);
    window.gamingCouchSendProjectInfo(projectName);
  },

  GamingCouchRuntimeMessages: function (runtimeMessagesJsonString) {
    if (!window.gamingCouchRuntimeMessages) {
      return;
    }

    var runtimeMessages = JSON.parse(UTF8ToString(runtimeMessagesJsonString));
    window.gamingCouchRuntimeMessages(runtimeMessages);
  },

  GamingCouchScreenSpace: function (screenSpaceJsonString) {
    if (!window.gamingCouchScreenSpace) {
      return;
    }

    var screenSpace = JSON.parse(UTF8ToString(screenSpaceJsonString));
    window.gamingCouchScreenSpace(screenSpace);
  }
});
