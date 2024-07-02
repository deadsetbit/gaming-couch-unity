using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Collections;
using DSB.GC.Hud;
using DSB.GC.Game;
using System.Linq;

namespace DSB.GC
{
    public enum GCStatus { PendingSetup, SetupDone, Playing, GameOver }

    public enum GCPlayerColor { blue, red, green, yellow, purple, pink, cyan, brown }

    public enum LogLevel { None, Warning, Info, Debug }

    [ExecuteInEditMode]
    public class GamingCouch : MonoBehaviour
    {
        [DllImport("__Internal")]
        private static extern void GamingCouchSetupDone();

        [DllImport("__Internal")]
        private static extern void GamingCouchGameEnd(byte[] placementsByPlayerId, int placementsByPlayerIdLength);

        private static int MAX_PLAYERS = 8;
        private static int MAX_NAME_LENGTH = 8;
        private static GamingCouch instance = null;
        public static GamingCouch Instance => instance;
        [Header("Integration configuration")]
        [SerializeField]
        private GameObject listener;
        [SerializeField]
        [Tooltip("Make sure your player prefab extends GCPlayer.")]
        private GameObject playerPrefab;
        private GCSetupOptions setupOptions; // this is assigned in GCSetup
        private GCStatus status = GCStatus.PendingSetup;
        public GCStatus Status => status;
        private GCPlayerStoreOutput<GCPlayer> playerStoreOutput;

        private GCHud hud = new GCHud();
        public GCHud Hud => hud;

        public LogLevel LogLevel = LogLevel.Debug;

        private void Log(LogLevel level, string message)
        {
            if (LogLevel == LogLevel.None) return;

            var logLevelIndex = (int)LogLevel;

            switch (level)
            {
                case LogLevel.Warning:
                    if (logLevelIndex < (int)LogLevel.Warning) return;
                    Debug.LogWarning($"[GC] ({level}) {message}");
                    break;
                case LogLevel.Info:
                    if (logLevelIndex < (int)LogLevel.Info) return;
                    Debug.Log($"[GC] ({level}) {message}");
                    break;
                case LogLevel.Debug:
                    if (logLevelIndex < (int)LogLevel.Debug) return;
                    Debug.Log($"[GC] ({level}) {message}");
                    break;
            }
        }

        private void LogWarning(string message)
        {
            Log(LogLevel.Warning, message);
        }

        private void LogInfo(string message)
        {
            Log(LogLevel.Info, message);
        }

        private void LogDebug(string message)
        {
            Log(LogLevel.Debug, message);
        }

        private void Awake()
        {
            LogDebug("Awake");
            if (FindObjectsOfType<GamingCouch>().Length > 1)
            {
                if (Application.isEditor && !Application.isPlaying)
                {
                    throw new Exception("You have multiple GamingCouch instances in the scene. Make sure to have only one.");
                }

                LogWarning("GamingCouch instance already exists. Destroying new instance.");

                Destroy(gameObject);
                return;
            }

            GamingCouch.instance = this;

            if (Application.isEditor && !Application.isPlaying)
            {
                return;
            }

            DontDestroyOnLoad(gameObject);

            if (!listener)
            {
                Debug.LogError("GamingCouch listener not set. Set game object via inspector that will receive and handle GamingCouch related events. This will likely be your main game script.");
            }
        }

        private void Start()
        {
            if (Application.isEditor && !Application.isPlaying)
            {
                return;
            }

            LogDebug("Start");

#if UNITY_EDITOR
            // When integrated, platform will define the setup options on Unity boot up.
            setupOptions = GetEditorSetupOptions();
#endif

            GCStart();
        }

        private void GCStart()
        {
            LogDebug("GCStart");

            status = GCStatus.PendingSetup;

            if (setupOptions == null)
            {
                throw new Exception("GamingCouch setup options not set. Make sure to call GCSetup method with setup options.");
            }

            Debug.Log("GamingCouch setup options: " + setupOptions.gameModeId);

            listener.SendMessage("GamingCouchSetup", setupOptions, SendMessageOptions.RequireReceiver);
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
            hud.HandleQueue();
        }

        #region Methods called by the GamingCouch platform
        /// <summary>
        /// Called by the platform when the game is ready for setup.
        /// In setup, the game should load levels, assets etc. but not yet instantiate any players.
        /// </summary>
        private void GamingCouchSetup(string optionsJson)
        {
            LogInfo("GamingCouchSetup: " + optionsJson);

            // store as we don't want to call the listener before Start so that Unity is fully initialized.
            // this will also ensure the splash screen is shown before game gets to report setup as ready.
            setupOptions = GCSetupOptions.CreateFromJSON(optionsJson);
        }


        /// <summary>
        /// Called by the platform when all players are loaded and ready to be instantiated in the game.
        /// After play is called, players can be instantiated and game or intro can be started.
        /// </summary>
        private void GamingCouchPlay(string optionsJson)
        {
            LogInfo("GamingCouchPlay: " + optionsJson);

            GCPlayOptions options = GCPlayOptions.CreateFromJSON(optionsJson);
            Play(options);
        }

        private IEnumerator _EditorPlay()
        {
            LogInfo("_EditorPlay");
            yield return new WaitForSeconds(0.1f); // fake some delay as if Play was called via GCPlay by the platform
            Play(GetEditorPlayOptions());
        }

        /// <summary>
        /// Triggers GamingCouchPlay and sets the status to Playing.
        /// </summary>
        private void Play(GCPlayOptions options)
        {
            listener.SendMessage("GamingCouchPlay", options, SendMessageOptions.RequireReceiver);
            status = GCStatus.Playing;
        }

        /// <summary>
        /// Called by the platform to update player inputs.
        /// </summary>
        private void GamingCouchInputs(string playerIdAndInputs)
        {
            string[] playerIdAndInputsArray = playerIdAndInputs.Split('|');

            GCControllerInputs inputs = GCControllerInputs.CreateFromJSON(playerIdAndInputsArray[1]);

            var playerId = int.Parse(playerIdAndInputsArray[0]);
            inputsByPlayerId[playerId] = inputs;
        }
        #endregion

        #region Methods to be called by the game

        /// <summary>
        /// Call after game setup is done eg. level and other assets are loaded and the game is ready to play intro and spawn players.
        /// GamingCouchPlay will be called next by the platform. You should not start the game before GamingCouchPlay is called.
        /// </summary>
        public void SetupDone()
        {
            LogDebug("SetupDone");

#if UNITY_WEBGL && !UNITY_EDITOR
            GamingCouchSetupDone();
#else
            StartCoroutine(_EditorPlay());
#endif
            status = GCStatus.SetupDone;
        }

        private GCGame game;

        public void SetupGame(GCGame game)
        {
            if (this.game != null)
            {
                throw new InvalidOperationException("Game already set. You should call SetupGame only once.");
            }

            this.game = game;
        }

        public void GameOver()
        {
            if (game == null)
            {
                throw new InvalidOperationException("Game not set. You should call SetupGame before calling GameOver.");
            }

            var players = playerStoreOutput.GetPlayersEnumerable().ToList();
            var playersSorted = game.GetPlayersInPlacementOrder(players);

            var placementsByPlayerId = new int[playersSorted.Count];
            for (int i = 0; i < playersSorted.Count; i++)
            {
                placementsByPlayerId[i] = playersSorted[i].GetId();
            }

            LogInfo($"GameOver: {string.Join(",", placementsByPlayerId)}");

            for (var i = 0; i < placementsByPlayerId.Length; i++)
            {
                var playerId = placementsByPlayerId[i];
                var player = playerStoreOutput.GetPlayerById(playerId);
                LogInfo($"Player {player.GetName()} placed {i + 1} - (id:{playerId})");
            }

            byte[] result = new byte[placementsByPlayerId.Length];
            for (int i = 0; i < placementsByPlayerId.Length; i++)
            {
                result[i] = (byte)placementsByPlayerId[i];
            }

#if UNITY_WEBGL && !UNITY_EDITOR
        GamingCouchGameEnd(result, result.Length);
#endif

            status = GCStatus.GameOver;
        }

        /// <summary>
        /// Inform the platform that the game has ended.
        /// </summary>
        /// <param name="placementsByPlayerId">Player ID's in placement order.</param>
        [Obsolete("Use GameOver() instead. This will require you to setup the game with GamingCouch.Instance.SetupGame()")]
        public void GameEnd(int[] placementsByPlayerId)
        {
            LogInfo($"GameEnd: {string.Join(",", placementsByPlayerId)}");

            for (var i = 0; i < placementsByPlayerId.Length; i++)
            {
                var playerId = placementsByPlayerId[i];
                var player = playerStoreOutput.GetPlayerById(playerId);
                LogInfo($"Player {player.GetName()} (id:{playerId}) placed {i + 1}");
            }

            byte[] result = new byte[placementsByPlayerId.Length];
            for (int i = 0; i < placementsByPlayerId.Length; i++)
            {
                result[i] = (byte)placementsByPlayerId[i];
            }
#if UNITY_WEBGL && !UNITY_EDITOR
        GamingCouchGameEnd(result, result.Length);
#endif

            status = GCStatus.GameOver;
        }
        #endregion

        #region Player
        private T InstantiatePlayer<T>(int playerId, string name, string htmlColor) where T : GCPlayer
        {
            LogDebug($"InstantiatePlayer: {playerId}, {name}, {htmlColor}");

            var gameObject = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
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

            gameObject.name = "Player - " + name;


            // ColorUtility.TryParseHtmlString does not support color name "pink"
            if (htmlColor == "pink")
            {
                htmlColor = "#FFC0CB";
            }

            ColorUtility.TryParseHtmlString(htmlColor, out Color unityColor);

            player.GamingCouchSetup(new GCPlayerSetupOptions
            {
                playerId = playerId,
                name = name,
                color = unityColor
            });

            return targetType;
        }

        /// <summary>
        /// Instantiate players by using the prefab defined in GamingCouch game object's inspector.
        /// </summary>
        /// <typeparam name="T">Your game specific player class that extends GCPlayer.</typeparam>
        /// <param name="playerStore">Player store to add the players to. Note: You should instantiate this store in your main Game script to be able to provide it here. Refer the integration manual.</param>
        /// <param name="playerOptions">Player options to instantiate the players with. These options are available via GamingCouchPlay</param>
        public void InstantiatePlayers<T>(GCPlayerStore<T> playerStore, GCPlayerOptions[] playerOptions) where T : GCPlayer
        {
            LogInfo("InstantiatePlayers");

            if (typeof(T) == typeof(GCPlayer))
            {
                throw new InvalidOperationException("Call InstantiatePlayers by providing your game specific class as generic. The class should inherit GCPlayer or extend GCPlayer. Eg. do not call InstantiatePlayers<GCPlayer>, but instead InstantiatePlayers<MyPlayer> where MyPlayer is a class that extends GCPlayer.");
            }

            if (playerStore.GetPlayerCount() > 0)
            {
                throw new InvalidOperationException("Players already instantiated. Call ClearPlayers before calling InstantiatePlayers.");
            }

            for (var i = 0; i < playerOptions.Length; i++)
            {
                var player = InstantiatePlayer<T>(playerOptions[i].playerId, playerOptions[i].name, playerOptions[i].color);
                playerStore.AddPlayer(player);
            }

            playerStoreOutput = playerStore;
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
            LogDebug("ClearInputs");
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
        }

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
                gameModeId = gameModeId,
            };
        }

        private GCPlayOptions GetEditorPlayOptions()
        {
            GCPlayOptions options = new GCPlayOptions
            {
                players = new GCPlayerOptions[numberOfPlayers]
            };

            for (int i = 0; i < numberOfPlayers; i++)
            {
                options.players[i] = new GCPlayerOptions
                {
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
        private string lx = "Horizontal";
        [SerializeField]
        [Tooltip("Unity Input button for editor testing button for editor testing. Default: 'Vertical'")]
        private string ly = "Vertical";
        [SerializeField]
        [Tooltip("Unity Input button for editor testing. Default: 'Jump'")]
        private string b1 = "Jump";
        [SerializeField]
        [Tooltip("Unity Input button for editor testing. Default: 'Fire1'")]
        private string b2 = "Fire1";
        [SerializeField]
        [Tooltip("Unity Input button for editor testing. Default: 'Fire2'")]
        private string b3 = "Fire2";
        [SerializeField]
        [Tooltip("Unity Input button for editor testing. Default: 'Fire3'")]
        private string b4 = "Fire3";

        private int controlPlayerIndex = 0;

        private void UpdateEditorInputs()
        {
            if (!useKeyboardControls) return;

            if (playerStoreOutput == null) return;

            if (playerStoreOutput.GetPlayerCount() == 0) return;

            for (int i = 0; i < MAX_PLAYERS; i++)
            {
                if (Input.GetKeyDown((i + 1).ToString()))
                {
                    controlPlayerIndex = i;
                }
            }

            var player = playerStoreOutput.GetPlayerByIndex(controlPlayerIndex);

            if (player == null) return;

            var inputs = new GCControllerInputs
            {
                lx = Input.GetAxis(lx),
                ly = Input.GetAxis(ly),
                b1 = Input.GetButton(b1) ? 1 : 0,
                b2 = Input.GetButton(b2) ? 1 : 0,
                b3 = Input.GetButton(b3) ? 1 : 0,
                b4 = Input.GetButton(b4) ? 1 : 0
            };

            inputsByPlayerId[player.GetId()] = inputs;
        }
        #endregion

        #region Other public methods

        /// <summary>
        /// 1) Clears players from the player store and destroys the game objects.
        /// 2) Clears player inputs.
        /// </summary>
        public void Clear()
        {
            playerStoreOutput.Clear();
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
            LogDebug("Restart");

            if (Application.isEditor && !Application.isPlaying)
            {
                throw new Exception("Restart can only be called in play mode.");
            }

            Clear();
            Start();
        }
        #endregion
    }
}
