mergeInto(LibraryManager.library, {
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
});
