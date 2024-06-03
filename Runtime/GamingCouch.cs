using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Collections;

namespace DSB.GC
{
    public enum GCStatus { PendingSetup, SetupDone, Playing, GameOver }

    public enum GCPlayerColor { blue, red, green, yellow, purple, pink, cyan, brown }

    [Serializable]
    public struct GCHudPlayersConfig
    {
        public string valueType;
    }

    [Serializable]
    public struct GCHudConfig
    {
        public GCHudPlayersConfig players;
    }

    [Serializable]
    public struct GCPlayersHudDataPlayer
    {
        public int playerId;
        public bool eliminated;
        public int placement;
        public string value;
    }

    [Serializable]
    public struct GCPlayersHudData
    {
        public GCPlayersHudDataPlayer[] players;
    }

    public class GamingCouch : MonoBehaviour
    {
        [DllImport("__Internal")]
        private static extern void GamingCouchSetupDone();

        [DllImport("__Internal")]
        private static extern void GamingCouchSetupHud(string hudConfigJson);

        [DllImport("__Internal")]
        private static extern void GamingCouchUpdatePlayersHud(string playersHudDataJson);

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
        [Tooltip("Make sure your player prefab inherits IGCPLayer or extends GCPLayer.")]
        private GameObject playerPrefab;
        private GCSetupOptions setupOptions; // this is assigned in GCSetup
        private GCStatus status = GCStatus.PendingSetup;
        public GCStatus Status => status;
        private IGCPlayerStoreOutput<IGCPlayer> playerStoreOutput;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Debug.LogWarning("GamingCouch instance already exists. Destroying new instance.");
                Destroy(gameObject);
                return;
            }

            if (!listener)
            {
                Debug.LogError("GamingCouch listener not set. Set game object via inspector that will receive and handle GamingCouch related events. This will likely be your main game script.");
            }
        }

        private void Start()
        {
#if UNITY_EDITOR
            // When integrated, platform will define the setup options on Unity boot up.
            setupOptions = GetEditorSetupOptions();
#endif

            GCStart();
        }

        private void GCStart()
        {
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
            UpdateEditorInputs();
        }

        #region Methods called by the GamingCouch platform
        /**
        * Called by the platform when the game is ready for setup, but players are not yet all ready.
        * See GCPLay to start the game when all the players are ready.
        */
        private void GamingCouchSetup(string optionsJson)
        {
            Debug.Log("GCSetup: " + optionsJson);

            // store as we don't want to call the listener before Start so that Unity is fully initialized.
            // this will also ensure the splash screen is shown before game gets to report setup as ready.
            setupOptions = GCSetupOptions.CreateFromJSON(optionsJson);
        }

        /**
        * Called by the platform when all players are loaded and the game is ready to play.
        * This will be called after GCSetup.
        */
        private void GamingCouchPlay(string optionsJson)
        {
            Debug.Log("GamingCouchPlay: " + optionsJson);

            GCPlayOptions options = GCPlayOptions.CreateFromJSON(optionsJson);
            Play(options);
        }

        private IEnumerator _EditorPlay()
        {
            yield return new WaitForSeconds(0.1f); // fake some delay as if Play was called via GCPLay by the platform
            Play(GetEditorPlayOptions());
        }

        private void Play(GCPlayOptions options)
        {
            listener.SendMessage("GamingCouchPlay", options, SendMessageOptions.RequireReceiver);
            status = GCStatus.Playing;
        }

        /**
        * Called by the platform.
        **/
        private void GamingCouchInputs(string playerIdAndInputs)
        {
            string[] playerIdAndInputsArray = playerIdAndInputs.Split('|');

            GCControllerInputs inputs = GCControllerInputs.CreateFromJSON(playerIdAndInputsArray[1]);

            var playerId = int.Parse(playerIdAndInputsArray[0]);
            inputsByPlayerId[playerId] = inputs;
        }
        #endregion

        #region Methods to be called by the game
        public void SetupDone()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            GamingCouchSetupDone();
#else
            StartCoroutine(_EditorPlay());
#endif
            status = GCStatus.SetupDone;
        }

        public void GameEnd(int[] placementsByPlayerId)
        {
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

        #region HUD
        public void SetupHud(GCHudConfig playersHudData)
        {
            string playersHudDataJson = JsonUtility.ToJson(playersHudData);
#if UNITY_WEBGL && !UNITY_EDITOR
        GamingCouchSetupHud(playersHudDataJson);
#endif
        }

        public void UpdatePlayersHud(GCPlayersHudData playersHudData)
        {
            string playersHudDataJson = JsonUtility.ToJson(playersHudData);
#if UNITY_WEBGL && !UNITY_EDITOR
        GamingCouchUpdatePlayersHud(playersHudDataJson);
#endif
        }
        #endregion

        #region Player
        private T InstantiatePlayer<T>(int playerId, string name, string htmlColor) where T : IGCPlayer
        {
            var gameObject = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            var targetType = gameObject.GetComponent<T>();
            if (targetType == null)
            {
                throw new Exception("Player prefab does not have a component of type " + typeof(T).Name);
            }

            var player = gameObject.GetComponent<IGCPlayer>();
            if (player == null)
            {
                throw new Exception("Player prefab does not have a component that inherits IGCPLayer or extends GCPLayer.");
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

        public void InstantiatePlayers<T>(GCPlayerStore<T> playerStore, PlayerOptions[] playerOptions) where T : class, IGCPlayer
        {
            if (typeof(T) == typeof(IGCPlayer))
            {
                throw new InvalidOperationException("Call InstantiatePlayers by providing your game specific class as generic. The class should inherit IGCPLayer or extend GCPLayer. Eg. do not call InstantiatePlayers<IGCPLayer>, but instead InstantiatePlayers<MyPlayer> where MyPlayer is a class that inherits IGCPLayer or extends GCPLayer.");
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
        public GCControllerInputs GetInputsByPlayerId(int playerId)
        {
            if (inputsByPlayerId.ContainsKey(playerId))
            {
                return inputsByPlayerId[playerId];
            }

            return null;
        }

        public void ClearInputs()
        {
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
        public struct PlayerEditorData
        {
            public string name;
            public GCPlayerColor color;
        }

        [Header("Editor player settings")]
        [SerializeField]
        private PlayerEditorData[] playerData = new PlayerEditorData[0];

        [SerializeField]
        [Tooltip("Number of players to instantiate on editor play mode.")]
        private int numberOfPlayers = MAX_PLAYERS;

        [SerializeField]
        [Tooltip("Randomize player ID's to better replicate real use case where ID's comes from the platform. If false, player ID's will be assigned in order starting from 1. (RECOMMENDED TO KEEP THIS ENABLED)")]
        private bool randomizePlayerIds = true;

        public GCSetupOptions GetEditorSetupOptions()
        {
            return new GCSetupOptions
            {
                gameModeId = gameModeId,
            };
        }

        public GCPlayOptions GetEditorPlayOptions()
        {
            GCPlayOptions options = new GCPlayOptions
            {
                players = new PlayerOptions[numberOfPlayers]
            };

            for (int i = 0; i < numberOfPlayers; i++)
            {
                options.players[i] = new PlayerOptions
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
        /**
        * 1) Clears players from the player store and destroys the game objects.
        * 2) Clears player inputs.
        */
        public void Clear()
        {
            playerStoreOutput.Clear();
            ClearInputs();
        }

        /**
        * Can be called for dev purposes to quickly restart the game instead of editor play mode restart.
        * GC methods such as GCSetup and GCPLay will be called again.
        */
        public void Restart()
        {
            Clear();
            Start();
        }
        #endregion
    }
}
