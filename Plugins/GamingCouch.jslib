mergeInto(LibraryManager.library, {
  GamingCouchInstanceStarted: function () {
    if (!window.gamingCouchInstanceStarted) {
      console.error("gamingCouchInstanceStarted is not defined");
      return;
    }
    window.gamingCouchInstanceStarted();
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
