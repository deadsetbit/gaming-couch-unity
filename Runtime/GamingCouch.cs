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
using UnityEngine.SceneManagement;
using DSB.GC.Dev;
using DSB.GC.RuntimeMessages;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Gaming Couch - Netcode for GameObjects")]
[assembly: InternalsVisibleTo("GamingCouch.Editor")]
[assembly: InternalsVisibleTo("GamingCouch.Editor.Tests")]

namespace DSB.GC
{
    public enum GCMode { Development = 1, Production = 2 }

    public enum GCStatus { PendingSetup, SetupDone, Playing, GameOver }

    public enum GCPlayerColor { blue, red, green, yellow, purple, pink, cyan, brown }

    public enum GCPlayerType { unset = 0, player = 1, bot = 2 }

    [ExecuteInEditMode]
    [RequireComponent(typeof(GCDevUtils))]
    [RequireComponent(typeof(GCDevAppIntegration))]
    public class GamingCouch : MonoBehaviour
    {
        [DllImport("__Internal")]
        private static extern void GamingCouchInstanceStarted();

        [DllImport("__Internal")]
        private static extern void GamingCouchSetupDone();

        [DllImport("__Internal")]
        private static extern void GamingCouchGameEnd(byte[] placementsByPlayerIndex, int placementsByPlayerIndexLength);

        [DllImport("__Internal")]
        private static extern void GamingCouchSendProjectInfo(string projectName);

        private static int MAX_PLAYERS = 8;
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
        private GCSeatIdentity[] playSeatIdentities = Array.Empty<GCSeatIdentity>();
        private GCActivePlayerMapping activePlayerMapping;
        private bool isRestarting = false;
        private bool isRuntimeStateSnapshotPending = false;
        private bool terminalPlacementAccepted = false;
        public bool IsRestarting => isRestarting;
        public bool IsPaused => paused;
        public float CurrentTimescale => paused ? timeScaleOnPause : Time.timeScale;
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
        public int GameSeed
        {
            get
            {
                Assert.IsNotNull(playOptions, "GamingCouch play options not set when reading GameSeed.");
                return playOptions.seed;
            }
        }
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
            CaptureEditorPlaySettings();
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
                if (TryRequireSetupOptions("Editor setup"))
                {
                    GamingCouchSetup();
                    SendProjectInfo();
                }
            }
#else
            if (!onlineMultiplayerSupport)
            {
                GamingCouchInstanceStarted();
                SendProjectInfo();
            }
#endif
        }

        private void Update()
        {
            HandleEditorInputs();
        }

        private void LateUpdate()
        {
            if (game != null)
            {
                game.HandlePlayersHudAutoUpdate();
                hud.HandleQueue();
                FlushRuntimeOutput();
            }
        }

        #region Methods called by the GamingCouch platform
        /// <summary>
        /// Called by the platform to provide necessary setup options on start.
        /// </summary>
        private void GamingCouchSetupOptions(string optionsJson)
        {
            GCLog.LogInfo("GamingCouchSetupOptions: " + optionsJson);

#if UNITY_EDITOR
            if (Application.isEditor)
            {
                if (GCLocalPlaySession.TryRequireCapturedSetupOptions("Editor setup options", out var capturedSetupOptions))
                {
                    setupOptions = capturedSetupOptions;
                }

                return;
            }
#endif

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
            if (!TryRequireSetupOptions("GamingCouchSetup"))
            {
                return;
            }

#if UNITY_EDITOR
            if (Application.isEditor)
            {
                if (!GCLocalPlaySession.TryRequireCapturedSetupOptions("GamingCouchSetup", out var capturedSetupOptions))
                {
                    return;
                }

                setupOptions = capturedSetupOptions;
            }
#endif

            mode = setupOptions.mode;

            listener.SendMessage("GamingCouchSetup", setupOptions, SendMessageOptions.RequireReceiver);
        }

        private bool TryRequireSetupOptions(string source)
        {
            if (setupOptions != null)
            {
                return true;
            }

#if UNITY_EDITOR
            if (Application.isEditor)
            {
                if (GCLocalPlaySession.TryRequireCapturedSetupOptions(source, out var capturedSetupOptions))
                {
                    setupOptions = capturedSetupOptions;
                    return true;
                }

                return false;
            }
#endif

            throw new Exception("GamingCouch setup options not set. Make sure to call GCSetup method with setup options.");
        }

        /// <summary>
        /// Called by the platform when all players are loaded and ready to be instantiated in the game.
        /// After play is called, players can be instantiated and game or intro can be started.
        /// </summary>
        private void GamingCouchPlay(string optionsJson)
        {
            GCLog.LogInfo("GamingCouchPlay: " + optionsJson);

#if UNITY_EDITOR
            if (Application.isEditor)
            {
                if (!TryGetEditorPlayOptions("Editor play", out var capturedPlayOptions, out var capturedSeatIdentities))
                {
                    return;
                }

                Play(capturedPlayOptions, capturedSeatIdentities);
                return;
            }
#endif

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
                inputsByPlayerIndex.Clear();
                externalInputsByPlayerIndex.Clear();

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

        private void SendProjectInfo()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string projectName = Application.productName;
            if (!string.IsNullOrEmpty(projectName))
            {
                GamingCouchSendProjectInfo(projectName);
            }
#endif
        }

#if UNITY_EDITOR
        private IEnumerator _EditorPlay()
        {
            GCLog.LogInfo("_EditorPlay");
            yield return new WaitForSeconds(0.1f); // fake some delay as if Play was called by the platform
            if (!TryGetEditorPlayOptions("Editor play", out var capturedPlayOptions, out var capturedSeatIdentities))
            {
                yield break;
            }

            Play(capturedPlayOptions, capturedSeatIdentities);
        }
#endif

        /// <summary>
        /// Triggers GamingCouchPlay and sets the status to Playing.
        /// </summary>
        private void Play(GCPlayOptions options)
        {
            Play(options, CreateFallbackSeatIdentities(options));
        }

        /// <summary>
        /// Triggers GamingCouchPlay and sets the status to Playing.
        /// </summary>
        private void Play(GCPlayOptions options, GCSeatIdentity[] seatIdentities)
        {
            var playerCount = options?.players?.Length ?? 0;
            var resolvedSeatIdentities = seatIdentities ?? CreateFallbackSeatIdentities(options);
            if (resolvedSeatIdentities.Length != playerCount)
            {
                throw new ArgumentException("[GamingCouch] Seat identity count must match play player count.");
            }

            activePlayerMapping = GCActivePlayerMapping.Create(options, resolvedSeatIdentities);
            playOptions = activePlayerMapping.CreateGameFacingPlayOptions();
            playOptions.runtimeOutput = options.runtimeOutput ?? new GCRuntimeOutputOptions();
            playOptions.platformData = GCPlatformRuntimeView.CopyForRuntime(options.platformData);
#if UNITY_EDITOR
            playOptions.runtimeOutput = GCDevAppRuntimeOutputSettings.Apply(playOptions.runtimeOutput);
#endif
            playSeatIdentities = CreateMappedSeatIdentities(resolvedSeatIdentities, activePlayerMapping);
            terminalPlacementAccepted = false;
            isRuntimeStateSnapshotPending = false;
            GCRuntimeMessageOutput.BeginActiveRun(playOptions.runtimeOutput);
            EmitPlatformMetadataDiagnostics(playOptions.platformData);
            listener.SendMessage("GamingCouchPlay", playOptions, SendMessageOptions.RequireReceiver);
            status = GCStatus.Playing;
            QueueRuntimeStateSnapshot();
        }

        private static GCPlayOptions CopyPlayOptions(GCPlayOptions options)
        {
            if (options == null)
            {
                return null;
            }

            GCActivePlayerOptions[] players = null;
            if (options.players != null)
            {
                players = new GCActivePlayerOptions[options.players.Length];
                Array.Copy(options.players, players, options.players.Length);
            }

            return new GCPlayOptions
            {
                players = players,
                seed = options.seed,
                runtimeOutput = options.runtimeOutput ?? new GCRuntimeOutputOptions(),
                platformData = GCPlatformRuntimeView.CopyForRuntime(options.platformData),
                participantIdentities = CopyParticipantIdentities(options.participantIdentities),
                usesMappedActivePlayers = options.usesMappedActivePlayers,
            };
        }

        internal static void EmitPlatformMetadataDiagnostics(GCPlatformRuntimeView platformData)
        {
            if (platformData == null || !platformData.fallbackActive)
            {
                return;
            }

            var validationState = platformData.validationState;
            var isMissing = string.Equals(validationState, GCPlatformRuntimeValidationState.Missing, StringComparison.Ordinal);
            var context = CreatePlatformMetadataDiagnosticContext(platformData);
            var sourceMessage = platformData.source != null ? platformData.source.message : null;
            var metadataMessage = !string.IsNullOrWhiteSpace(sourceMessage)
                ? sourceMessage
                : isMissing
                    ? "gc.platform.json was not found."
                    : "gc.platform.json is invalid.";

            GCDiagnostics.Emit(
                isMissing ? GCDiagnosticCodes.MissingPlatformData : GCDiagnosticCodes.InvalidPlatformData,
                GCDiagnosticSeverity.Warning,
                GCDiagnosticSourceAreas.Metadata,
                GCRuntimePayloadBounds.Truncate(metadataMessage, GCDiagnostics.MaxMessageLength),
                context
            );

            GCDiagnostics.Emit(
                GCDiagnosticCodes.FallbackActive,
                GCDiagnosticSeverity.Warning,
                GCDiagnosticSourceAreas.Metadata,
                "Fallback platform metadata is active.",
                context
            );
        }

        private static GCDiagnosticContext CreatePlatformMetadataDiagnosticContext(GCPlatformRuntimeView platformData)
        {
            var context = new GCDiagnosticContext()
                .AddDetail("validationState", platformData.validationState)
                .AddDetail("selectedEntryKey", platformData.selectedEntryKey);

            var source = platformData.source;
            if (source == null)
            {
                return context;
            }

            if (!string.IsNullOrWhiteSpace(source.fieldName))
            {
                context.AddDetail("fieldName", source.fieldName);
            }

            if (!string.IsNullOrWhiteSpace(source.path))
            {
                context.AddDebug("path", GCRuntimePayloadBounds.Truncate(source.path, GCDiagnosticFields.MaxStringLength));
            }

            if (!string.IsNullOrWhiteSpace(source.message))
            {
                context.AddDebug("sourceMessage", GCRuntimePayloadBounds.Truncate(source.message, GCDiagnosticFields.MaxStringLength));
            }

            return context;
        }

        private static GCPlatformParticipantIdentity[] CopyParticipantIdentities(GCPlatformParticipantIdentity[] participantIdentities)
        {
            if (participantIdentities == null || participantIdentities.Length == 0)
            {
                return Array.Empty<GCPlatformParticipantIdentity>();
            }

            var copiedParticipantIdentities = new GCPlatformParticipantIdentity[participantIdentities.Length];
            Array.Copy(participantIdentities, copiedParticipantIdentities, participantIdentities.Length);
            return copiedParticipantIdentities;
        }

        private static GCSeatIdentity[] CopySeatIdentities(GCSeatIdentity[] seatIdentities)
        {
            if (seatIdentities == null || seatIdentities.Length == 0)
            {
                return Array.Empty<GCSeatIdentity>();
            }

            var copiedSeatIdentities = new GCSeatIdentity[seatIdentities.Length];
            Array.Copy(seatIdentities, copiedSeatIdentities, seatIdentities.Length);
            return copiedSeatIdentities;
        }

        private static GCSeatIdentity[] CreateFallbackSeatIdentities(GCPlayOptions options)
        {
            if (options?.players == null)
            {
                return Array.Empty<GCSeatIdentity>();
            }

            var seatIdentities = new GCSeatIdentity[options.players.Length];
            for (var index = 0; index < options.players.Length; index++)
            {
                var playerOption = options.players[index];
                var sourceSeatIndex = index + 1;
                var participantIdentity = options.participantIdentities != null && index < options.participantIdentities.Length
                    ? options.participantIdentities[index]
                    : default;
                seatIdentities[index] = new GCSeatIdentity
                {
                    platformPlayerId = participantIdentity.platformPlayerId,
                    sourceSeatIndex = sourceSeatIndex,
                    stableKey = !string.IsNullOrWhiteSpace(participantIdentity.stableKey) ? participantIdentity.stableKey : sourceSeatIndex.ToString(),
                    label = "Seat " + sourceSeatIndex,
                    playerType = ResolvePlayerType(playerOption.type),
                    playerColor = ResolvePlayerColor(playerOption.color),
                };
            }

            return seatIdentities;
        }

        private static GCSeatIdentity[] CreateMappedSeatIdentities(
            GCSeatIdentity[] capturedSeatIdentities,
            GCActivePlayerMapping mapping
        )
        {
            if (mapping == null || capturedSeatIdentities == null || capturedSeatIdentities.Length == 0)
            {
                return Array.Empty<GCSeatIdentity>();
            }

            var mappedSeatIdentities = new GCSeatIdentity[capturedSeatIdentities.Length];
            for (var playerIndex = 0; playerIndex < capturedSeatIdentities.Length; playerIndex++)
            {
                var entry = mapping.GetByPlayerIndex(playerIndex);
                mappedSeatIdentities[playerIndex] = capturedSeatIdentities[entry.CapturedOrder];
            }

            return mappedSeatIdentities;
        }

        private static GCPlayerType ResolvePlayerType(string value)
        {
            return string.Equals(value, GCPlayerType.bot.ToString(), StringComparison.OrdinalIgnoreCase) ? GCPlayerType.bot : GCPlayerType.player;
        }

        private static GCPlayerColor ResolvePlayerColor(string value)
        {
            return !string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out GCPlayerColor playerColor) ? playerColor : GCPlayerColor.blue;
        }

        /// <summary>
        /// Called by the platform to update player inputs.
        /// </summary>
        private void GamingCouchInputs(string playerIndexAndInputs)
        {
            if (paused)
            {
                return;
            }

            string[] playerIndexAndInputsArray = playerIndexAndInputs.Split('|');
            var inputsData = GCControllerInputsData.CreateFromJSON(playerIndexAndInputsArray[1]);
            GCControllerInputs inputs = new GCControllerInputs(inputsData);

            var playerIndex = int.Parse(playerIndexAndInputsArray[0]);
            if (!TryValidateActivePlayerIndex(playerIndex, "input", out _))
            {
                return;
            }

            externalInputsByPlayerIndex[playerIndex] = inputs;
            inputsByPlayerIndex[playerIndex] = inputs;
        }
        #endregion

        #region Methods to be called by the game

        /// <summary>
        /// Inform the platform tha the server is ready to receive multiplayer clients.
        /// </summary>
        public void OnlineMultiplayerServerReady()
        {
            if (!TryRequireSetupOptions("OnlineMultiplayerServerReady"))
            {
                return;
            }

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
            if (!TryRequireSetupOptions("OnlineMultiplayerClientReady"))
            {
                return;
            }

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

#if UNITY_EDITOR
            if (!TryGetEditorPlayOptions("Editor play", out _, out _))
            {
                return;
            }
#endif

            StartCoroutine(_FadeVolume(AudioListener.volume, 1.0f));

#if UNITY_WEBGL && !UNITY_EDITOR
            GamingCouchSetupDone();
#elif UNITY_EDITOR
            StartCoroutine(_EditorPlay());
#else
            Debug.LogError("[GamingCouch] Local editor play callbacks are only available in the Unity editor.");
            return;
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

            var players = internalPlayerStore.Players.ToList();
            var playersSorted = game.GetPlayersInPlacementOrder(players).ToList();

            var playerIndicesByPlacement = new int[playersSorted.Count];
            for (int i = 0; i < playersSorted.Count; i++)
            {
                playerIndicesByPlacement[i] = playersSorted[i].Index;
            }

            GCLog.LogInfo($"GameOver: {string.Join(",", playerIndicesByPlacement)}");

            for (var i = 0; i < playerIndicesByPlacement.Length; i++)
            {
                var playerIndex = playerIndicesByPlacement[i];
                var player = internalPlayerStore.GetPlayerByIndex(playerIndex);
                GCLog.LogInfo($"Player index {playerIndex} placed {i + 1}");
            }

            byte[] result = new byte[playerIndicesByPlacement.Length];
            for (int i = 0; i < playerIndicesByPlacement.Length; i++)
            {
                result[i] = (byte)playerIndicesByPlacement[i];
            }

            if (!TrySubmitTerminalPlacement(playerIndicesByPlacement, out _))
            {
                return;
            }

            StartCoroutine(_FadeVolume(AudioListener.volume, 0.0f));

#if UNITY_EDITOR
            GetComponent<GCDevAppIntegration>()?.PublishRuntimeGameOver(playerIndicesByPlacement);
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        GamingCouchGameEnd(result, result.Length);
#endif
        }

        internal GCRuntimeStateSnapshotPayload BuildRuntimeStateSnapshotPayload()
        {
            RequireGameSetupDone("BuildRuntimeStateSnapshotPayload");
            return game.BuildRuntimeStateSnapshotPayload(status);
        }

        internal bool TrySubmitTerminalPlacement(int[] playerIndicesByPlacement, out string runtimeMessagesJson)
        {
            runtimeMessagesJson = null;
            if (terminalPlacementAccepted)
            {
                GCDiagnostics.Emit(
                    GCDiagnosticCodes.InvalidTerminalPlacement,
                    GCDiagnosticSeverity.Error,
                    GCDiagnosticSourceAreas.RuntimeMessages,
                    "Terminal placement was already accepted for this active run."
                );
                return false;
            }

            if (!TryValidateTerminalPlacement(playerIndicesByPlacement, "game_over"))
            {
                GCDiagnostics.Emit(
                    GCDiagnosticCodes.InvalidTerminalPlacement,
                    GCDiagnosticSeverity.Error,
                    GCDiagnosticSourceAreas.RuntimeMessages,
                    "Terminal placement was rejected."
                );
                return false;
            }

            status = GCStatus.GameOver;
            QueueRuntimeStateSnapshot();
            FlushRuntimeStateSnapshotToPending();
            var terminalRecord = GCRuntimeMessageOutput.CreateRecord(
                GCRuntimeMessageTypes.TerminalPlacementSubmitted,
                GCRuntimeTerminalPlacementPayload.BuildJson(playerIndicesByPlacement, internalPlayerStore.Players.Count)
            );
            terminalPlacementAccepted = true;
            runtimeMessagesJson = GCRuntimeMessageOutput.FlushPendingWith(terminalRecord);
            return true;
        }

        internal void QueueRuntimeStateSnapshot()
        {
            if (game == null)
            {
                return;
            }

            isRuntimeStateSnapshotPending = true;
        }

        internal void QueueRuntimePlayerTransition(string messageType, string payloadJson)
        {
            GCRuntimeMessageOutput.QueueTransition(messageType, payloadJson);
        }

        internal void FlushRuntimeOutput()
        {
            FlushRuntimeStateSnapshotToPending();
            GCRuntimeMessageOutput.FlushPending();
        }

        private void FlushRuntimeStateSnapshotToPending()
        {
            if (!isRuntimeStateSnapshotPending)
            {
                return;
            }

            isRuntimeStateSnapshotPending = false;
            if (!GCRuntimeMessageOutput.IsStateSnapshotsEnabled || game == null)
            {
                return;
            }

            GCRuntimeMessageOutput.QueueStateSnapshot(BuildRuntimeStateSnapshotPayload().ToJson());
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
        private T InstantiatePlayer<T>(GCActivePlayerOptions options, Vector3 position, Quaternion rotation)
        {
            GCLog.LogDebug($"InstantiatePlayer: {options.playerIndex}, {options.color}");

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

        internal void _InternalSetPlayerProperties(GCPlayer player, GCActivePlayerOptions options)
        {
            player.gameObject.name = "Player - " + options.playerIndex;

            var playerSetupOptions = new GCPlayerSetupOptions
            {
                playerIndex = options.playerIndex,
                type = (GCPlayerType)Enum.Parse(typeof(GCPlayerType), options.type),
                colorEnum = (GCPlayerColor)Enum.Parse(typeof(GCPlayerColor), options.color),
                colorName = options.color,
            };

            player._InternalGamingCouchSetup(playerSetupOptions);

            // TODO: move as this fnc is for player properties?
            game.SetupPlayer(player);
            internalPlayerStore.AddPlayer(player);
            QueueRuntimeStateSnapshot();
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

        public void SetupPlayers<T>(GCActivePlayerOptions[] playerOptions, Action<T> onPlayerSetupReady) where T : GCPlayer
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
        public void SetupPlayers<T>(GCActivePlayerOptions[] playerOptions, GCPlayerSpawnProperties[] spawnProperties, Action<T> onPlayerSetupReady) where T : GCPlayer
        {
            GCLog.LogInfo("SetupPlayers");

            RequireGameSetupDone("SetupPlayers");

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

            if (internalPlayerStore.Players.Count > 0)
            {
                GCLog.LogWarning("Players already instantiated. Call GamingCouch.Instance.ClearPlayers() before calling SetupPlayers. Note that clearing players is only for dev purposes in dev mode to reset game for example.");
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

        public GCActivePlayerOptions GetPlayerOptions(int playerIndex)
        {
            return playOptions.players.Single(p => p.playerIndex == playerIndex);
        }
        #endregion

        #region Player inputs
        private Dictionary<int, GCControllerInputs> inputsByPlayerIndex = new Dictionary<int, GCControllerInputs>();
        private Dictionary<int, GCControllerInputs> externalInputsByPlayerIndex = new Dictionary<int, GCControllerInputs>();

        /// <summary>
        /// Removed. Use GetInputsByPlayerIndex.
        /// </summary>
        /// <param name="playerIndex">Active player index</param>
        /// <returns>null if not available</returns>
        [Obsolete("GetInputsByPlayerId has been removed from the game-facing runtime contract. Use GetInputsByPlayerIndex.", true)]
        public GCControllerInputs GetInputsByPlayerId(int playerIndex)
        {
            throw new InvalidOperationException("GetInputsByPlayerId has been removed. Use GetInputsByPlayerIndex.");
        }

        /// <summary>
        /// Get player inputs by active player index.
        /// </summary>
        /// <param name="playerIndex">Active player index</param>
        /// <returns>null if not available</returns>
        public GCControllerInputs GetInputsByPlayerIndex(int playerIndex)
        {
            if (inputsByPlayerIndex.ContainsKey(playerIndex))
            {
                return inputsByPlayerIndex[playerIndex];
            }

            return null;
        }

        /// <summary>
        /// Clear all player inputs.
        /// </summary>
        public void ClearInputs()
        {
            GCLog.LogDebug("ClearInputs");
            inputsByPlayerIndex.Clear();
            externalInputsByPlayerIndex.Clear();
        }

        internal bool TryGetPlayerIndexForSourceSeat(int sourceSeatIndex, out int playerIndex)
        {
            playerIndex = -1;
            return activePlayerMapping != null && activePlayerMapping.TryGetPlayerIndexForSourceSeat(sourceSeatIndex, out playerIndex);
        }

        internal bool TryGetPlayerIndexForLegacyPlayerId(int playerId, out int playerIndex)
        {
            playerIndex = -1;
            return activePlayerMapping != null && activePlayerMapping.TryGetPlayerIndexForLegacyPlayerId(playerId, out playerIndex);
        }

        internal bool TryValidateActivePlayerIndex(int playerIndex, string source, out GCActivePlayerMappingEntry entry)
        {
            if (activePlayerMapping == null)
            {
                entry = default;
                return false;
            }

            return activePlayerMapping.TryValidatePlayerIndex(playerIndex, source, out entry);
        }

        internal bool TryValidateTerminalPlacement(int[] playerIndicesByPlacement, string source)
        {
            return activePlayerMapping != null && activePlayerMapping.TryValidatePlacement(playerIndicesByPlacement, source);
        }
        #endregion

        #region Development settings for editor and inspector
#if UNITY_EDITOR
        private void CaptureEditorPlaySettings()
        {
            setupOptions = null;
            if (GCLocalPlaySession.CaptureForRuntimeEntry() &&
                GCLocalPlaySession.TryRequireCapturedSetupOptions("Editor setup", out var capturedSetupOptions))
            {
                setupOptions = capturedSetupOptions;
            }
        }

        private void RecaptureEditorPlaySettingsForRestart()
        {
            setupOptions = null;
            if (GCLocalPlaySession.CaptureForRestart() &&
                GCLocalPlaySession.TryRequireCapturedSetupOptions("Editor setup", out var capturedSetupOptions))
            {
                setupOptions = capturedSetupOptions;
            }
        }

        private bool TryGetEditorPlayOptions(
            string source,
            out GCPlayOptions capturedPlayOptions,
            out GCSeatIdentity[] capturedSeatIdentities
        )
        {
            return GCLocalPlaySession.TryRequireCapturedPlayOptions(
                source,
                out capturedPlayOptions,
                out capturedSeatIdentities
            );
        }
#endif

        [Serializable]
        private struct PlayerEditorData
        {
            public string name;
            public GCPlayerColor color;
            public bool isBot;
        }

        // Obsolete serialized editor play fields are kept only so old scenes deserialize.
        // Root gc.dev.json is the functional source for editor setup and play capture.
#pragma warning disable 0169, 0414
        [SerializeField, HideInInspector]
        private string gameModeId;

        [SerializeField, HideInInspector]
        private PlayerEditorData[] playerData = new PlayerEditorData[0];

        [SerializeField, HideInInspector]
        private int numberOfPlayers = MAX_PLAYERS;

        [SerializeField, HideInInspector]
        private bool randomizePlayerIds = true;
#pragma warning restore 0169, 0414
        #endregion

        [Header("Editor keyboard controls (Unity Input System map)")]

        #region Editor keyboard controls
        [SerializeField]
        private bool useKeyboardControls = true;
        [SerializeField]
        [Tooltip("Left stick X-axis for editor testing. Default: 'Horizontal'")]
        private string axisX = "Horizontal";
        [SerializeField]
        [Tooltip("Left stick Y-axis for editor testing. Default: 'Vertical'")]
        private string axisY = "Vertical";
        [SerializeField]
        [Tooltip("Unity Input keyboard input 'primary' action button (A on Xbox controller). Default: 'Jump'")]
        private string buttonPrimary = "Jump";
        [SerializeField]
        [Tooltip("Unity Input keyboard input 'secondary' action button (B on Xbox controller). Default: 'Fire1'")]
        private string buttonSecondary = "Fire1";
        private static float INPUT_AXIS_INNER_DEADZONE = 0.15f;

        private int editorControlPlayerIndex = 0;

        private static bool HasNonZeroInput(GCControllerInputsData inputs, float axisDeadzone)
        {
            if (inputs == null)
            {
                return false;
            }

            if (Mathf.Abs(inputs.a0) > axisDeadzone || Mathf.Abs(inputs.a1) > axisDeadzone)
            {
                return true;
            }

            if (Mathf.Abs(inputs.a2) > axisDeadzone || Mathf.Abs(inputs.a3) > axisDeadzone)
            {
                return true;
            }

            return inputs.b0 == 1 || inputs.b1 == 1 || inputs.b2 == 1 || inputs.b3 == 1 ||
                inputs.b12 == 1 || inputs.b13 == 1 || inputs.b14 == 1 || inputs.b15 == 1;
        }

        internal static GCControllerInputsData ResolveEditorInputs(
            GCControllerInputsData keyboardInputsData,
            GCControllerInputsData externalInputsData,
            float axisDeadzone
        )
        {
            var selectedInputs = HasNonZeroInput(keyboardInputsData, axisDeadzone)
                ? keyboardInputsData
                : HasNonZeroInput(externalInputsData, axisDeadzone)
                    ? externalInputsData
                    : null;

            if (selectedInputs == null)
            {
                return new GCControllerInputsData();
            }

            return new GCControllerInputsData
            {
                a0 = Mathf.Clamp(selectedInputs.a0, -1.0f, 1.0f),
                a1 = Mathf.Clamp(selectedInputs.a1, -1.0f, 1.0f),
                a2 = Mathf.Clamp(selectedInputs.a2, -1.0f, 1.0f),
                a3 = Mathf.Clamp(selectedInputs.a3, -1.0f, 1.0f),
                b0 = selectedInputs.b0,
                b1 = selectedInputs.b1,
                b2 = selectedInputs.b2,
                b3 = selectedInputs.b3,
                b12 = selectedInputs.b12,
                b13 = selectedInputs.b13,
                b14 = selectedInputs.b14,
                b15 = selectedInputs.b15,
            };
        }

        internal static void ApplyEditorInputsForPlayer(
            int playerIndex,
            GCControllerInputsData keyboardInputsData,
            Dictionary<int, GCControllerInputs> gameFacingInputsByPlayerIndex,
            Dictionary<int, GCControllerInputs> externalInputsByPlayerIndex,
            float axisDeadzone
        )
        {
            GCControllerInputsData externalInputsData = null;
            if (externalInputsByPlayerIndex != null &&
                externalInputsByPlayerIndex.TryGetValue(playerIndex, out var externalInputs))
            {
                externalInputsData = externalInputs.RawData;
            }

            var finalInputsData = ResolveEditorInputs(
                keyboardInputsData: keyboardInputsData,
                externalInputsData: externalInputsData,
                axisDeadzone: axisDeadzone
            );
            gameFacingInputsByPlayerIndex[playerIndex] = new GCControllerInputs(finalInputsData);
        }

        private void HandleEditorInputs()
        {
            if (!Application.isEditor || !Application.isPlaying)
            {
                return;
            }

            if (internalPlayerStore == null) return;
            if (internalPlayerStore.Players.Count == 0) return;

            for (int i = 0; i < MAX_PLAYERS; i++)
            {
                if (Input.GetKeyDown((i + 1).ToString()))
                {
                    editorControlPlayerIndex = i;
                }
            }

            var player = internalPlayerStore.GetPlayerByIndex(editorControlPlayerIndex);

            if (player == null) return;

            // keyboard inputs
            GCControllerInputsData keyboardInputsData = null;
            if (useKeyboardControls)
            {
                keyboardInputsData = new GCControllerInputsData
                {
                    a0 = Input.GetAxis(axisX),
                    a1 = Input.GetAxis(axisY),
                    b0 = Input.GetButton(buttonPrimary) ? 1 : 0,
                    b1 = Input.GetButton(buttonSecondary) ? 1 : 0,
                };
            }

            if (keyboardInputsData == null && !externalInputsByPlayerIndex.ContainsKey(player.Index))
            {
                return;
            }

            ApplyEditorInputsForPlayer(
                playerIndex: player.Index,
                keyboardInputsData: keyboardInputsData,
                gameFacingInputsByPlayerIndex: inputsByPlayerIndex,
                externalInputsByPlayerIndex: externalInputsByPlayerIndex,
                axisDeadzone: INPUT_AXIS_INNER_DEADZONE
            );
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
            activePlayerMapping = null;
        }

        public GCActivePlayerOptions[] GetCurrentPlayPlayerOptions()
        {
            if (playOptions?.players == null)
            {
                return Array.Empty<GCActivePlayerOptions>();
            }

            var playerOptions = new GCActivePlayerOptions[playOptions.players.Length];
            Array.Copy(playOptions.players, playerOptions, playOptions.players.Length);
            return playerOptions;
        }

        internal GCSeatIdentity[] GetCurrentPlaySeatIdentities()
        {
            return CopySeatIdentities(playSeatIdentities);
        }

        public void ApplyDevPause(bool nextPaused)
        {
            GamingCouchPause(nextPaused.ToString());
        }

        public void ApplyDevTimescale(float nextTimescale)
        {
            var clampedTimescale = Mathf.Clamp(nextTimescale, 0.1f, 10.0f);

            if (paused)
            {
                timeScaleOnPause = clampedTimescale;
                return;
            }

            Time.timeScale = clampedTimescale;
        }

        /**
        * Can be called for dev purposes to quickly restart the game instead of editor play mode restart.
        * GC methods such as GCSetup and GCPlay will be called again.
        */

        /// <summary>
        /// Can be called for dev purposes to quickly restart the game in editor play mode.
        /// NOTE:
        /// The preferred automatic out of the box way to restart the game in editor during play mode is to use restart shortcut defined in GCDevUtils.
        /// </summary>
        public void Restart()
        {
            GCLog.LogDebug("Restart");

            if (Application.isEditor && !Application.isPlaying)
            {
                throw new Exception("[GamingCouch] Restart can only be called in play mode.");
            }

#if UNITY_EDITOR
            if (Application.isEditor)
            {
                if (!GCLocalPlaySession.TryRunRestartPreflight())
                {
                    return;
                }

                RecaptureEditorPlaySettingsForRestart();
                if (!TryGetEditorPlayOptions("Editor play", out _, out _))
                {
                    return;
                }
            }
#endif

            game = null;

            Clear();
            Start();
        }

        internal void _InternalHandleGamePlayModeRestart()
        {
            if (!isRestarting)
            {
                StartCoroutine(_HandleGamePlayModeRestart());
            }
        }

        private IEnumerator _HandleGamePlayModeRestart()
        {
            if (isRestarting || !Application.isEditor || !Application.isPlaying)
            {
                yield break;
            }

#if UNITY_EDITOR
            if (!GCLocalPlaySession.TryRunRestartPreflight())
            {
                yield break;
            }
#endif

            isRestarting = true;

            try
            {
                GCLog.LogDebug("Restarting game in play mode -----------");

                GameObject temp = new GameObject("SceneProbe");
                DontDestroyOnLoad(temp);
                Scene dontDestroyScene = temp.scene;
                DestroyImmediate(temp);

                GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                List<GameObject> donDestroyOnLoadObjects = new List<GameObject>();

                foreach (var obj in allObjects)
                {
                    if (obj.scene == dontDestroyScene && obj.transform.parent == null)
                    {
                        donDestroyOnLoadObjects.Add(obj);
                    }
                }

                foreach (var obj in donDestroyOnLoadObjects)
                {
                    if (obj != null)
                    {
                        DestroyImmediate(obj);
                    }
                }

                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            catch (Exception e)
            {
                GCLog.LogWarning("Error restarting game in play mode: " + e.Message);
            }

            yield return null;
            isRestarting = false;
        }
        #endregion
    }
}
