using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Collections;
using DSB.GC.Hud;
using DSB.GC.Game;
using DSB.GC.Log;
using System.Linq;
using UnityEngine.Assertions;

namespace DSB.GC
{
    public enum GCMode { Development = 1, Production = 2 }

    public enum GCStatus { PendingSetup, SetupDone, Playing, GameOver }

    public enum GCPlayerColor { blue, red, green, yellow, purple, pink, cyan, brown }

    public enum GCPlayerType { unset = 0, player = 1, bot = 2 }

    [ExecuteInEditMode]
    public class GamingCouch : MonoBehaviour
    {
        [DllImport("__Internal")]
        private static extern void GamingCouchInstanceStarted();

        [DllImport("__Internal")]
        private static extern void GamingCouchSetupDone();

        [DllImport("__Internal")]
        private static extern void GamingCouchGameEnd(byte[] placementsByPlayerId, int placementsByPlayerIdLength);

        private static int MAX_PLAYERS = 8;
        private static int MAX_NAME_LENGTH = 8;
        private static float AUDIO_FADE_SECONDS = 3.0f;
        private static GamingCouch instance = null;
        public static GamingCouch Instance => instance;
        [Header("Integration configuration")]
        [SerializeField]
        private GameObject listener;
        [SerializeField]
        [Tooltip("Make sure your player prefab extends GCPlayer.")]
        private GameObject playerPrefab;
        private GCSetupOptions setupOptions;
        private GCPlayOptions playOptions;
        public bool IsServer
        {
            get
            {
                Assert.IsNotNull(setupOptions, "GamingCouch setup options not set when reading IsServer.");
                return setupOptions.isServer;
            }
        }
        public uint ClientId
        {
            get
            {
                Assert.IsNotNull(setupOptions, "GamingCouch setup options not set when reading ClientId.");
                return setupOptions.clientId;
            }
        }
        private GCMode mode = GCMode.Production;
        public GCMode Mode => mode;
        [SerializeField]
        [Tooltip("Mark the game to support online multiplayer. After this is enabled you need to call OnlineMultiplayerServerReady() for server and OnlineMultiplayerClientReady() for player. This will indicate to the platform that your game is ready to communicate.")]
        private bool onlineMultiplayerSupport = false;
        public bool OnlineMultiplayerSupport => onlineMultiplayerSupport;
        private bool onlineMultiplayerReadyCalled = false;
        private GCStatus status = GCStatus.PendingSetup;
        public GCStatus Status => status;
        private GCPlayerStore<GCPlayer> internalPlayerStore = new GCPlayerStore<GCPlayer>();
        public GCPlayerStore<GCPlayer> InternalPlayerStore => internalPlayerStore;
        private GCGame game;
        private GCHud hud = new GCHud();
        public GCHud Hud => hud;
        public LogLevel LogLevel = LogLevel.Debug;
        private float timeScaleOnPause = 1.0f;
        private float volumeOnPause = 1.0f;
        private bool paused = false;

        private void Awake()
        {
            GCLog.logLevel = LogLevel;

            GCLog.LogDebug("Awake");
            if (FindObjectsByType<GamingCouch>(FindObjectsSortMode.None).Length > 1)
            {
                if (Application.isEditor && !Application.isPlaying)
                {
                    throw new Exception("You have multiple GamingCouch instances in the scene. Make sure to have only one.");
                }

                GCLog.LogWarning("GamingCouch instance already exists. Destroying new instance.");

                Destroy(gameObject);
                return;
            }

            instance = this;

            if (Application.isEditor && !Application.isPlaying)
            {
                return;
            }

            DontDestroyOnLoad(gameObject);

            if (!listener)
            {
                Debug.LogError("GamingCouch listener not set. Set game object via inspector that will receive and handle GamingCouch related events. This will likely be your main game script.");
            }

#if UNITY_EDITOR
            // When integrated, platform will define the setup options on Unity boot up via GamingCouchSetup.
            setupOptions = GetEditorSetupOptions();
#endif
        }

        private void Start()
        {
            if (Application.isEditor && !Application.isPlaying)
            {
                return;
            }

            GCLog.LogDebug("Start");

            AudioListener.volume = 0.0f;

            status = GCStatus.PendingSetup;

#if UNITY_EDITOR
            if (!onlineMultiplayerSupport)
            {
                GamingCouchSetup();
            }
#else
            if (!onlineMultiplayerSupport)
            {
                GamingCouchInstanceStarted();
            }
#endif
        }

        private void OnValidate()
        {
            OnValidatePlayerDataField();
            OnValidateNumberOfPlayersField();
        }

        private void Update()
        {
            if (Application.isEditor && Application.isPlaying)
            {
                UpdateEditorInputs();
            }
        }

        private void LateUpdate()
        {
            if (game != null)
            {
                game.HandlePlayersHudAutoUpdate();
                hud.HandleQueue();
            }
        }

        #region Methods called by the GamingCouch platform
        /// <summary>
        /// Called by the platform to provide necessary setup options on start.
        /// </summary>
        private void GamingCouchSetupOptions(string optionsJson)
        {
            GCLog.LogInfo("GamingCouchSetupOptions: " + optionsJson);

            // store as we don't want to call the listener before Start so that Unity is fully initialized.
            // this will also ensure the splash screen is shown before game gets to report setup as ready.
            setupOptions = GCSetupOptions.CreateFromJSON(optionsJson);
        }

        /// <summary>
        /// Called by the platform when the game is ready for setup and receive network messages.
        /// This occurs after all the players has loaded the game and called GamingCouchInstanceStarted.
        /// In setup, the game should prepare game mode eg. load/instantiate required levels and so on.
        /// Setup is not yet a place to spawn players as that should occur in GamingCouchPlay where
        /// the available players are locked in for the round as there is a possibility that some one
        /// leaves or joins during the setup phase.
        /// </summary>
        private void GamingCouchSetup()
        {
            if (setupOptions == null)
            {
                throw new Exception("GamingCouch setup options not set. Make sure to call GCSetup method with setup options.");
            }

            mode = setupOptions.mode;

            listener.SendMessage("GamingCouchSetup", setupOptions, SendMessageOptions.RequireReceiver);
        }

        /// <summary>
        /// Called by the platform when all players are loaded and ready to be instantiated in the game.
        /// After play is called, players can be instantiated and game or intro can be started.
        /// </summary>
        private void GamingCouchPlay(string optionsJson)
        {
            GCLog.LogInfo("GamingCouchPlay: " + optionsJson);

            GCPlayOptions options = GCPlayOptions.CreateFromJSON(optionsJson);
            Play(options);
        }

        /// <summary>
        /// Called by the platform when the game is paused or resumed.
        /// Sets the time scale to 0 when paused and back to previous value when resumed.
        /// </summary>
        private void GamingCouchPause(string pauseString)
        {
            var pause = bool.Parse(pauseString);
            GCLog.LogInfo("GamingCouchPause: " + pause);

            if (paused && pause)
            {
                GCLog.LogWarning("GamingCouchPause: Trying to pause while already paused.");
                return;
            }

            if (!paused && !pause)
            {
                GCLog.LogWarning("GamingCouchPause: Trying to resume when not paused.");
                return;
            }

            paused = pause;

            if (pause)
            {
                inputsByPlayerId.Clear();

                volumeOnPause = AudioListener.volume;
                AudioListener.volume = 0.0f;
                GCLog.LogInfo("GamingCouchPause: Pausing");
                timeScaleOnPause = Time.timeScale;
                Time.timeScale = 0;
                return;
            }

            GCLog.LogInfo("GamingCouchPause: Resuming");
            AudioListener.volume = volumeOnPause;
            Time.timeScale = timeScaleOnPause;
        }

        private IEnumerator _EditorPlay()
        {
            GCLog.LogInfo("_EditorPlay");
            yield return new WaitForSeconds(0.1f); // fake some delay as if Play was called by the platform
            Play(GetEditorPlayOptions());
        }

        /// <summary>
        /// Triggers GamingCouchPlay and sets the status to Playing.
        /// </summary>
        private void Play(GCPlayOptions options)
        {
            playOptions = options;
            listener.SendMessage("GamingCouchPlay", options, SendMessageOptions.RequireReceiver);
            status = GCStatus.Playing;
        }

        /// <summary>
        /// Called by the platform to update player inputs.
        /// </summary>
        private void GamingCouchInputs(string playerIdAndInputs)
        {
            if (paused)
            {
                return;
            }

            string[] playerIdAndInputsArray = playerIdAndInputs.Split('|');

            GCControllerInputs inputs = GCControllerInputs.CreateFromJSON(playerIdAndInputsArray[1]);

            var playerId = int.Parse(playerIdAndInputsArray[0]);
            inputsByPlayerId[playerId] = inputs;
        }
        #endregion

        #region Methods to be called by the game

        /// <summary>
        /// Inform the platform tha the server is ready to receive multiplayer clients.
        /// </summary>
        public void OnlineMultiplayerServerReady()
        {
            Assert.IsNotNull(setupOptions, "[GamingCouch] GamingCouch setup options not set.");
            Assert.IsTrue(setupOptions.isServer, "[GamingCouch] ServerReady should only be called by the server.");
            Assert.IsFalse(onlineMultiplayerReadyCalled, "[GamingCouch] ServerReady should only be called once.");

            onlineMultiplayerReadyCalled = true;

#if UNITY_WEBGL && !UNITY_EDITOR
            GamingCouchInstanceStarted();
#else
            GamingCouchSetup();
#endif
        }

        /// <summary>
        /// Inform the platform that the client is ready to connect with the multiplayer server.
        /// </summary>
        public void OnlineMultiplayerClientReady()
        {
            Assert.IsNotNull(setupOptions, "[GamingCouch] GamingCouch setup options not set.");
            Assert.IsFalse(setupOptions.isServer, "[GamingCouch] ClientReady should only be called by the client.");
            Assert.IsFalse(onlineMultiplayerReadyCalled, "[GamingCouch] ClientReady should only be called once.");

            onlineMultiplayerReadyCalled = true;

#if UNITY_WEBGL && !UNITY_EDITOR
            GamingCouchInstanceStarted();
#else
            GamingCouchSetup();
#endif
        }

        /// <summary>
        /// Call after game setup is done eg. level and other assets are loaded and the game is ready to play intro and spawn players.
        /// GamingCouchPlay will be called next by the platform. You should not start the game before GamingCouchPlay is called.
        /// </summary>
        public void SetupDone()
        {
            GCLog.LogDebug("SetupDone");

            StartCoroutine(_FadeVolume(AudioListener.volume, 1.0f));

#if UNITY_WEBGL && !UNITY_EDITOR
            GamingCouchSetupDone();
#else
            StartCoroutine(_EditorPlay());
#endif
            status = GCStatus.SetupDone;
        }
        public void SetupGameVersus(GCGameVersusSetupOptions options)
        {
            SetupGame(new GCGameVersus(this, internalPlayerStore, options));
        }

        private void RequireGameSetupDone(string source)
        {
            if (game == null)
            {
                throw new InvalidOperationException("[GamingCouch] Game not set. You should call GamingCouch.Instance.SetupGame() before calling '" + source + "'.");
            }
        }

        public void SetGameMaxScore(int maxScore)
        {
            RequireGameSetupDone("SetGameMaxScore");
            game.SetMaxScore(maxScore);
        }

        private void SetupGame(GCGame game)
        {
            if (this.game != null)
            {
                throw new InvalidOperationException("[GamingCouch] Game already set. You should call SetupGame only once.");
            }

            this.game = game;
        }

        /// <summary>
        /// Call when the game is over.
        /// Note: This will trigger the platform to show the game over screen immediately,
        /// so make sure to call this after possible outro animations etc. are done.
        /// </summary>
        /// <exception cref="InvalidOperationException">Throws if SetupGame is not called before.</exception>
        public void GameOver()
        {
            RequireGameSetupDone("GameOver");

            var players = internalPlayerStore.PlayersEnumerable.ToList();
            var playersSorted = game.GetPlayersInPlacementOrder(players).ToList();

            var placementsByPlayerId = new int[playersSorted.Count];
            for (int i = 0; i < playersSorted.Count; i++)
            {
                placementsByPlayerId[i] = playersSorted[i].Id;
            }

            GCLog.LogInfo($"GameOver: {string.Join(",", placementsByPlayerId)}");

            for (var i = 0; i < placementsByPlayerId.Length; i++)
            {
                var playerId = placementsByPlayerId[i];
                var player = internalPlayerStore.GetPlayerById(playerId);
                GCLog.LogInfo($"Player {player.PlayerName} placed {i + 1} - (id:{playerId})");
            }

            byte[] result = new byte[placementsByPlayerId.Length];
            for (int i = 0; i < placementsByPlayerId.Length; i++)
            {
                result[i] = (byte)placementsByPlayerId[i];
            }

            StartCoroutine(_FadeVolume(AudioListener.volume, 0.0f));

#if UNITY_WEBGL && !UNITY_EDITOR
        GamingCouchGameEnd(result, result.Length);
#endif

            status = GCStatus.GameOver;
        }
        #endregion

        private IEnumerator _FadeVolume(float startVolume, float endVolume)
        {
            var duration = AUDIO_FADE_SECONDS;
            var time = 0.0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                AudioListener.volume = Mathf.Lerp(startVolume, endVolume, time / duration);
                yield return null;
            }
        }


        #region Player
        private T InstantiatePlayer<T>(GCPlayerOptions options, Vector3 position, Quaternion rotation)
        {
            GCLog.LogDebug($"InstantiatePlayer: {options.playerId}, {options.name}, {options.color}");

            var activeOriginal = playerPrefab.activeSelf;
            playerPrefab.SetActive(false);

            try
            {
                var gameObject = Instantiate(playerPrefab, position, rotation);
                var targetType = gameObject.GetComponent<T>();
                if (targetType == null)
                {
                    throw new Exception("Player prefab does not have a component of type " + typeof(T).Name);
                }

                var player = gameObject.GetComponent<GCPlayer>();
                if (player == null)
                {
                    throw new Exception("Player prefab does not have a component that extends GCPlayer.");
                }

                _InternalSetPlayerProperties(player, options);

                playerPrefab.SetActive(activeOriginal);
                gameObject.SetActive(activeOriginal);

                return targetType;
            }
            catch (Exception e)
            {
                playerPrefab.SetActive(activeOriginal);
                Debug.LogError("Error instantiating player: " + e.Message);
                throw;
            }
        }

        public void _InternalSetPlayerProperties(GCPlayer player, GCPlayerOptions options)
        {
            player.gameObject.name = "Player - " + options.name;

            var playerSetupOptions = new GCPlayerSetupOptions
            {
                type = (GCPlayerType)Enum.Parse(typeof(GCPlayerType), options.type),
                playerId = options.playerId,
                name = options.name,
                colorEnum = (GCPlayerColor)Enum.Parse(typeof(GCPlayerColor), options.color),
                colorName = options.color,
            };

            player._InternalGamingCouchSetup(playerSetupOptions);

            // TODO: move as this fnc is for player properties?
            game.SetupPlayer(player);
            internalPlayerStore.AddPlayer(player);
            SetupPlayerReady?.Invoke(player);
        }

        private Action<GCPlayer> SetupPlayerReady;
        private void SetPlayerReadyCallback<T>(Action<T> onReady) where T : GCPlayer
        {
            SetupPlayerReady = (gcPlayer) =>
            {
                var player = gcPlayer as T;
                Debug.Assert(player != null, "Invalid player type in SetPlayerReadyCallback. Expected: " + typeof(T).Name + ", got: " + gcPlayer.GetType().Name);
                onReady(player);
            };
        }

        /// <summary>
        /// Properties to define the player spawn position and rotation when calling SetupPlayers.
        /// </summary>
        public struct GCPlayerSpawnProperties
        {
            public Vector3 position;
            public Quaternion rotation;
        }

        public void SetupPlayers<T>(GCPlayerOptions[] playerOptions, Action<T> onPlayerSetupReady) where T : GCPlayer
        {
            SetupPlayers(playerOptions, null, onPlayerSetupReady);
        }

        /// <summary>
        /// Setup and instantiate players by using the prefab defined in GamingCouch game object's inspector.
        /// </summary>
        /// <typeparam name="T">Your game specific player class that extends GCPlayer.</typeparam>
        /// <param name="playerOptions">Player options to instantiate the players with. These options are available via GamingCouchPlay</param>
        /// <param name="spawnProperties">Spawn properties to define the player spawn position and rotation.</param>
        /// <param name="onPlayerSetupReady">Callback to be called when the player is ready. This is useful to store the player in your own game specific player store to access players by your games player type.</param>
        public void SetupPlayers<T>(GCPlayerOptions[] playerOptions, GCPlayerSpawnProperties[] spawnProperties, Action<T> onPlayerSetupReady) where T : GCPlayer
        {
            GCLog.LogInfo("SetupPlayers");

            RequireGameSetupDone("InstantiatePlayers");

            if (spawnProperties != null)
            {
                if (spawnProperties.Length < MAX_PLAYERS)
                {
                    throw new ArgumentException($"[GamingCouch] Not enough GCPlayerSpawnProperties provided. Expected at least {MAX_PLAYERS}, got {spawnProperties.Length}.");
                }

                if (spawnProperties.Length != MAX_PLAYERS)
                {
                    GCLog.LogWarning($"Number of GCPlayerSpawnProperties provided ({spawnProperties.Length}) does not match the maximum players ({MAX_PLAYERS}). Properties exceeding max players will never be used. This may indicate an error in your code.");
                }
            }

            if (internalPlayerStore.PlayerCount > 0)
            {
                GCLog.LogWarning("Players already instantiated. Call GamingCouch.Instance.ClearPlayers() before calling InstantiatePlayers. Note that clearing players is only for dev purposes in dev mode to reset game for example.");
            }

            SetPlayerReadyCallback(onPlayerSetupReady);

            // Client never instantiates the players as the server will do that.
            // We still want to listen for the SetPlayerReadyCallback as defined above.
            if (OnlineMultiplayerSupport && !IsServer)
            {
                return;
            }

            for (var i = 0; i < playerOptions.Length; i++)
            {
                var position = spawnProperties != null && i < spawnProperties.Length ? spawnProperties[i].position : Vector3.zero;
                var rotation = spawnProperties != null && i < spawnProperties.Length ? spawnProperties[i].rotation : Quaternion.identity;
                InstantiatePlayer<T>(playerOptions[i], position, rotation);
            }
        }

        public GCPlayerOptions GetPlayerOptions(int playerId)
        {
            return playOptions.players.Single(p => p.playerId == playerId);
        }
        #endregion

        #region Player inputs
        private Dictionary<int, GCControllerInputs> inputsByPlayerId = new Dictionary<int, GCControllerInputs>();
        /// <summary>
        /// Get player inputs by player ID.
        /// </summary>
        /// <param name="playerId">Player ID</param>
        /// <returns>null if not available</returns>
        public GCControllerInputs GetInputsByPlayerId(int playerId)
        {
            if (inputsByPlayerId.ContainsKey(playerId))
            {
                return inputsByPlayerId[playerId];
            }

            return null;
        }

        /// <summary>
        /// Clear all player inputs.
        /// </summary>
        public void ClearInputs()
        {
            GCLog.LogDebug("ClearInputs");
            inputsByPlayerId.Clear();
        }
        #endregion

        #region Development settings for editor and inspector
        private void OnValidatePlayerDataField()
        {
            // Initialize
            if (playerData.Length == 0)
            {
                Array.Resize(ref playerData, MAX_PLAYERS);

                for (int i = 0; i < MAX_PLAYERS; i++)
                {
                    playerData[i].color = (GCPlayerColor)i;
                }
            }

            if (playerData.Length != MAX_PLAYERS)
            {
                Array.Resize(ref playerData, MAX_PLAYERS);
            }

            for (int i = 0; i < MAX_PLAYERS; i++)
            {
                if (playerData[i].name == null || playerData[i].name == "")
                {
                    playerData[i].name = $"Player {i + 1}"[..MAX_NAME_LENGTH];
                }
            }
        }

        private void OnValidateNumberOfPlayersField()
        {
            if (numberOfPlayers > MAX_PLAYERS)
            {
                numberOfPlayers = MAX_PLAYERS;
            }
        }

        [SerializeField]
        [Header("Editor game settings")]
        [Tooltip("The game mode ID to be played in editor play mode.")]
        private string gameModeId;

        [Serializable]
        private struct PlayerEditorData
        {
            public string name;
            public GCPlayerColor color;
            public bool isBot;
        }

        [SerializeField]
        [Header("Editor player settings")]
        private PlayerEditorData[] playerData = new PlayerEditorData[0];

        [SerializeField]
        [Tooltip("Number of players to instantiate on editor play mode.")]
        private int numberOfPlayers = MAX_PLAYERS;

        [SerializeField]
        [Tooltip("Randomize player ID's to better replicate real use case where ID's comes from the platform. If false, player ID's will be assigned in order starting from 1. (RECOMMENDED TO KEEP THIS ENABLED)")]
        private bool randomizePlayerIds = true;

        private GCSetupOptions GetEditorSetupOptions()
        {
            return new GCSetupOptions
            {
                isServer = true,
                gameModeId = gameModeId,
                mode = GCMode.Development,
            };
        }

        private GCPlayOptions GetEditorPlayOptions()
        {
            GCPlayOptions options = new GCPlayOptions
            {
                players = new GCPlayerOptions[numberOfPlayers],
                seed = UnityEngine.Random.Range(1, 999999),
            };

            var usedColors = new List<GCPlayerColor>();

            for (int i = 0; i < numberOfPlayers; i++)
            {
                if (usedColors.Contains(playerData[i].color))
                {
                    throw new Exception("[GamingCouch] Player color '" + playerData[i].color + "' set more than once in GamingCouch 'playerData'. Make sure to use unique colors for each player.");
                }

                usedColors.Add(playerData[i].color);


                options.players[i] = new GCPlayerOptions
                {
                    type = playerData[i].isBot ? GCPlayerType.bot.ToString() : GCPlayerType.player.ToString(),
                    playerId = randomizePlayerIds ? UnityEngine.Random.Range(1, 99) : i + 1,
                    name = playerData[i].name,
                    color = playerData[i].color.ToString(),
                };
            }

            return options;
        }
        #endregion

        [Header("Editor keyboard controls")]

        #region Editor keyboard controls
        [SerializeField]
        private bool useKeyboardControls = true;
        [SerializeField]
        [Tooltip("Unity Input button for editor testing button for editor testing. Default: 'Horizontal'")]
        private string a0 = "Horizontal";
        [SerializeField]
        [Tooltip("Unity Input button for editor testing button for editor testing. Default: 'Vertical'")]
        private string a1 = "Vertical";
        [SerializeField]
        [Tooltip("Unity Input button for editor testing. Default: 'Jump'")]
        private string b0 = "Jump";
        [SerializeField]
        [Tooltip("Unity Input button for editor testing. Default: 'Fire1'")]
        private string b1 = "Fire1";
        [SerializeField]
        [Tooltip("Unity Input button for editor testing. Default: 'Fire2'")]
        private string b2 = "Fire2";
        [SerializeField]
        [Tooltip("Unity Input button for editor testing. Default: 'Fire3'")]
        private string b3 = "Fire3";

        private int controlPlayerIndex = 0;

        private void UpdateEditorInputs()
        {
            if (!useKeyboardControls) return;

            if (internalPlayerStore == null) return;
            if (internalPlayerStore.PlayerCount == 0) return;

            for (int i = 0; i < MAX_PLAYERS; i++)
            {
                if (Input.GetKeyDown((i + 1).ToString()))
                {
                    controlPlayerIndex = i;
                }
            }

            var player = internalPlayerStore.GetPlayerByIndex(controlPlayerIndex);

            if (player == null) return;

            var inputs = new GCControllerInputs(new GCControllerInputsData
            {
                a0 = Input.GetAxis(a0),
                a1 = Input.GetAxis(a1),
                b0 = Input.GetButton(b0) ? 1 : 0,
                b1 = Input.GetButton(b1) ? 1 : 0,
                b2 = Input.GetButton(b2) ? 1 : 0,
                b3 = Input.GetButton(b3) ? 1 : 0
            });

            inputsByPlayerId[player.Id] = inputs;
        }
        #endregion

        #region Other public methods

        /// <summary>
        /// 1) Clears players from the player store and destroys the game objects.
        /// 2) Clears player inputs.
        /// </summary>
        public void Clear()
        {
            internalPlayerStore.Clear();
            ClearInputs();
        }

        /**
        * Can be called for dev purposes to quickly restart the game instead of editor play mode restart.
        * GC methods such as GCSetup and GCPlay will be called again.
        */

        /// <summary>
        /// Can be called for dev purposes to quickly restart the game in editor play mode.
        /// </summary>
        public void Restart()
        {
            GCLog.LogDebug("Restart");

            if (Application.isEditor && !Application.isPlaying)
            {
                throw new Exception("[GamingCouch] Restart can only be called in play mode.");
            }

            game = null;

            Clear();
            Start();
        }
        #endregion
    }
}
