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

  GamingCouchUpdatePlayersHud: function (playersHudDataJsonString) {
    if (!window.gamingCouchUpdatePlayersHud) {
      console.error("gamingCouchUpdatePlayersHud is not defined");
      return;
    }

    var playersHudData = JSON.parse(UTF8ToString(playersHudDataJsonString));
    window.gamingCouchUpdatePlayersHud(playersHudData);
  },

  GamingCouchUpdateScreenPointHud: function (screenPointHudDataJsonString) {
    if (!window.gamingCouchUpdateScreenPointHud) {
      console.error("gamingCouchUpdateScreenPointHud is not defined");
      return;
    }

    var screenPointHudData = JSON.parse(
      UTF8ToString(screenPointHudDataJsonString)
    );
    window.gamingCouchUpdateScreenPointHud(screenPointHudData);
  },

  GamingCouchGameEnd: function (
    placementsByPlayerIndex,
    placementsByPlayerIndexLength
  ) {
    if (!window.gamingCouchGameEnd) {
      console.error("gamingCouchGameEnd is not defined");
      return;
    }

    var playerIndicesByPlacement = [];
    for (var i = 0; i < placementsByPlayerIndexLength; i++) {
      playerIndicesByPlacement.push(HEAPU8[(placementsByPlayerIndex >> 0) + i]);
    }
    window.gamingCouchGameEnd({ playerIndicesByPlacement: playerIndicesByPlacement });
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
  }
});
