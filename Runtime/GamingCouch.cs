using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Collections;
using DSB.GC.Hud;
using DSB.GC.Game;
using DSB.GC.Log;
using System.Linq;

namespace DSB.GC
{
    public enum GCStatus { PendingSetup, SetupDone, Playing, GameOver }

    public enum GCPlayerColor { blue, red, green, yellow, purple, pink, cyan, brown }

    public enum GCPlayerType { unset, player, bot }

    [ExecuteInEditMode]
    public class GamingCouch : MonoBehaviour
    {
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
        private GCSetupOptions setupOptions; // this is assigned in GCSetup
        private GCStatus status = GCStatus.PendingSetup;
        public GCStatus Status => status;
        private GCPlayerStoreOutput<GCPlayer> playerStoreOutput;
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
            if (FindObjectsOfType<GamingCouch>().Length > 1)
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
        }

        private void Start()
        {
            if (Application.isEditor && !Application.isPlaying)
            {
                return;
            }

            GCLog.LogDebug("Start");

            AudioListener.volume = 0.0f;

#if UNITY_EDITOR
            // When integrated, platform will define the setup options on Unity boot up.
            setupOptions = GetEditorSetupOptions();
#endif

            GCStart();
        }

        private void GCStart()
        {
            GCLog.LogDebug("GCStart");

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
            if (game != null)
            {
                game.HandlePlayersHudAutoUpdate();
                hud.HandleQueue();
            }
        }

        #region Methods called by the GamingCouch platform
        /// <summary>
        /// Called by the platform when the game is ready for setup.
        /// In setup, the game should load levels, assets etc. but not yet instantiate any players.
        /// </summary>
        private void GamingCouchSetup(string optionsJson)
        {
            GCLog.LogInfo("GamingCouchSetup: " + optionsJson);

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
            SetupGame(new GCGameVersus(this, playerStoreOutput, options));
        }

        private void SetupGame(GCGame game)
        {
            if (this.game != null)
            {
                throw new InvalidOperationException("Game already set. You should call SetupGame only once.");
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
            if (game == null)
            {
                throw new InvalidOperationException("Game not set. You should call SetupGame before calling GameOver.");
            }

            var players = playerStoreOutput.PlayersEnumerable.ToList();
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
                var player = playerStoreOutput.GetPlayerById(playerId);
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
        private T InstantiatePlayer<T>(GCPlayerOptions options, Vector3 position, Quaternion rotation) where T : GCPlayer
        {
            GCLog.LogDebug($"InstantiatePlayer: {options.playerId}, {options.name}, {options.color}");

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

            gameObject.name = "Player - " + options.name;

            var playerSetupOptions = new GCPlayerSetupOptions
            {
                type = (GCPlayerType)Enum.Parse(typeof(GCPlayerType), options.type),
                playerId = options.playerId,
                name = options.name,
                colorEnum = (GCPlayerColor)Enum.Parse(typeof(GCPlayerColor), options.color),
                colorName = options.color,
            };

            player.SendMessage("_InternalGamingCouchSetup", playerSetupOptions, SendMessageOptions.RequireReceiver);

            return targetType;
        }

        /// <summary>
        /// Instantiate players by using the prefab defined in GamingCouch game object's inspector.
        /// </summary>
        /// <typeparam name="T">Your game specific player class that extends GCPlayer.</typeparam>
        /// <param name="playerStore">Player store to add the players to. Note: You should instantiate this store in your main Game script to be able to provide it here. Refer the integration manual.</param>
        /// <param name="playerOptions">Player options to instantiate the players with. These options are available via GamingCouchPlay</param>
        public void InstantiatePlayers<T>(GCPlayerStore<T> playerStore, GCPlayerOptions[] playerOptions, Vector3 position, Quaternion rotation) where T : GCPlayer
        {
            GCLog.LogInfo("InstantiatePlayers");

            if (typeof(T) == typeof(GCPlayer))
            {
                throw new InvalidOperationException("Call InstantiatePlayers by providing your game specific class as generic. The class should inherit GCPlayer or extend GCPlayer. Eg. do not call InstantiatePlayers<GCPlayer>, but instead InstantiatePlayers<MyPlayer> where MyPlayer is a class that extends GCPlayer.");
            }

            if (playerStore.PlayerCount > 0)
            {
                throw new InvalidOperationException("Players already instantiated. Call ClearPlayers before calling InstantiatePlayers.");
            }

            for (var i = 0; i < playerOptions.Length; i++)
            {
                var player = InstantiatePlayer<T>(playerOptions[i], position, rotation);
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
                gameModeId = gameModeId,
            };
        }

        private GCPlayOptions GetEditorPlayOptions()
        {
            GCPlayOptions options = new GCPlayOptions
            {
                players = new GCPlayerOptions[numberOfPlayers]
            };

            var usedColors = new List<GCPlayerColor>();

            for (int i = 0; i < numberOfPlayers; i++)
            {
                if (usedColors.Contains(playerData[i].color))
                {
                    throw new Exception("Player color '" + playerData[i].color + "' set more than once in GamingCouch 'playerData'. Make sure to use unique colors for each player.");
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

            if (playerStoreOutput == null) return;

            if (playerStoreOutput.PlayerCount == 0) return;

            for (int i = 0; i < MAX_PLAYERS; i++)
            {
                if (Input.GetKeyDown((i + 1).ToString()))
                {
                    controlPlayerIndex = i;
                }
            }

            var player = playerStoreOutput.GetPlayerByIndex(controlPlayerIndex);

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
            GCLog.LogDebug("Restart");

            if (Application.isEditor && !Application.isPlaying)
            {
                throw new Exception("Restart can only be called in play mode.");
            }

            game = null;

            Clear();
            Start();
        }
        #endregion
    }
}
