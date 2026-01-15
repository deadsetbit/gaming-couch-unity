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
    placementsByPlayerId,
    placementsByPlayerIdLength
  ) {
    if (!window.gamingCouchGameEnd) {
      console.error("gamingCouchGameEnd is not defined");
      return;
    }

    var result = [];
    for (var i = 0; i < placementsByPlayerIdLength; i++) {
      result.push(HEAPU8[(placementsByPlayerId >> 0) + i]);
    }
    window.gamingCouchGameEnd(result);
  },

  GamingCouchTimescaleUpdate: function (timescaleString, pausedString) {
    if (!window.gamingCouchTimescaleUpdate) {
      console.error("gamingCouchTimescaleUpdate is not defined");
      return;
    }

    var timescale = parseFloat(UTF8ToString(timescaleString));
    var paused = UTF8ToString(pausedString) === "true";
    window.gamingCouchTimescaleUpdate(timescale, paused);
  },

  GamingCouchSendProjectInfo: function (projectNameString) {
    if (!window.gamingCouchSendProjectInfo) {
      console.error("gamingCouchSendProjectInfo is not defined");
      return;
    }

    var projectName = UTF8ToString(projectNameString);
    window.gamingCouchSendProjectInfo(projectName);
  },
});
