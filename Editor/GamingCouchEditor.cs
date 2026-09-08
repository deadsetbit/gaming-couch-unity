using DSB.GC;
using DSB.GC.Dev;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GamingCouch))]
internal sealed class GamingCouchEditor : Editor
{
    // The active-scene readiness scan is expensive (disk reads/parses, a full scene
    // GetComponentsInChildren walk, GameView reflection, WebGL template stats). Cache
    // it on the instance and refresh at most this often instead of on every repaint.
    private const double StartScreenSummaryRefreshIntervalSeconds = 0.5;

    private GCDevJsonInspectorState devJsonState;
    private GCDevJsonInspectorView devJsonView;
    private GCStartScreenReadinessSummary startScreenSummary;
    private bool hasStartScreenSummary;
    private bool startScreenSummaryScanErrorLogged;
    private double nextStartScreenSummaryRefreshTime;

    private void OnEnable()
    {
        devJsonState = new GCDevJsonInspectorState();
        devJsonView = new GCDevJsonInspectorView();
        GCDevJsonEditorPlayModeGate.Register(devJsonState);
        EditorApplication.update -= OnEditorApplicationUpdate;
        EditorApplication.update += OnEditorApplicationUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        // Populate the cache eagerly so the first paint after selection is not blank.
        RefreshStartScreenSummary();
    }

    private void OnDisable()
    {
        DisposeDevJsonState();
    }

    public override void OnInspectorGUI()
    {
        if (target == null)
        {
            DisposeDevJsonState();
            return;
        }

        DrawStartScreenControls();

        EditorGUILayout.Space();
        serializedObject.Update();
        GamingCouchInspectorHost.DrawSerializedFields(serializedObject);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (devJsonView != null)
        {
            devJsonView.Draw(devJsonState);
        }
    }

    private void DrawStartScreenControls()
    {
        if (GUILayout.Button("Open Start Screen"))
        {
            GamingCouchStartScreenWindow.Open();
        }

        // Draw the cached summary only -- the scan itself is throttled in
        // OnEditorApplicationUpdate (and refreshed on external gc.dev/platform json
        // changes). Compute lazily but STILL throttled on the off chance the cache was never
        // populated (e.g. the OnEnable scan threw): a persistently failing scan must not re-run
        // the expensive inspection on every repaint.
        if (!hasStartScreenSummary)
        {
            MaybeRefreshStartScreenSummary();
        }

        var summary = startScreenSummary;
        if (summary == null || string.IsNullOrEmpty(summary.message))
        {
            return;
        }

        EditorGUILayout.HelpBox(summary.message, GetStartScreenSummaryMessageType(summary.state));
    }

    // Recompute the readiness summary and repaint if the visible output changed. Also
    // resets the throttle gate, so callers may invoke this to force a refresh.
    private void RefreshStartScreenSummary()
    {
        nextStartScreenSummaryRefreshTime =
            EditorApplication.timeSinceStartup + StartScreenSummaryRefreshIntervalSeconds;

        GCStartScreenReadinessSummary next;
        try
        {
            next = GCStartScreenReadinessService.InspectActiveSceneSummary();
            startScreenSummaryScanErrorLogged = false;
        }
        catch (Exception exception)
        {
            // The readiness scan does disk I/O + reflection and can throw transiently. This runs from
            // EditorApplication.update at ~2 Hz even while the inspector is hidden, so log at most
            // once per failure streak instead of spamming, keep the last good summary, and let a
            // later tick recover.
            if (!startScreenSummaryScanErrorLogged)
            {
                Debug.LogWarning("Gaming Couch: start screen readiness inspection failed; keeping last known state. " + exception);
                startScreenSummaryScanErrorLogged = true;
            }

            return;
        }

        if (hasStartScreenSummary && StartScreenSummariesEqual(startScreenSummary, next))
        {
            return;
        }

        startScreenSummary = next;
        hasStartScreenSummary = true;
        Repaint();
    }

    private void MaybeRefreshStartScreenSummary()
    {
        if (EditorApplication.timeSinceStartup < nextStartScreenSummaryRefreshTime)
        {
            return;
        }

        RefreshStartScreenSummary();
    }

    // The HelpBox shows summary.message with a MessageType derived from summary.state,
    // so those two fields fully determine the visible output.
    private static bool StartScreenSummariesEqual(
        GCStartScreenReadinessSummary a,
        GCStartScreenReadinessSummary b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        return a.state == b.state && string.Equals(a.message, b.message, StringComparison.Ordinal);
    }

    private static MessageType GetStartScreenSummaryMessageType(GCStartScreenReadinessSummaryState state)
    {
        switch (state)
        {
            case GCStartScreenReadinessSummaryState.Ready:
                return MessageType.Info;
            case GCStartScreenReadinessSummaryState.Warning:
            case GCStartScreenReadinessSummaryState.Actionable:
            case GCStartScreenReadinessSummaryState.PendingCompilation:
                return MessageType.Warning;
            default:
                return MessageType.Error;
        }
    }

    private void OnEditorApplicationUpdate()
    {
        if (target == null)
        {
            DisposeDevJsonState();
            return;
        }

        if (devJsonState != null && devJsonState.PollForExternalChanges())
        {
            // A gc.dev/platform json change can alter readiness -- recompute eagerly on
            // the same path that already repaints for the dev-json view.
            RefreshStartScreenSummary();
            Repaint();
        }

        MaybeRefreshStartScreenSummary();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (devJsonState != null && devJsonState.HandlePlayModeStateChanged(change))
        {
            Repaint();
        }
    }

    private void DisposeDevJsonState()
    {
        EditorApplication.update -= OnEditorApplicationUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        GCDevJsonEditorPlayModeGate.Unregister(devJsonState);
        devJsonState = null;
        devJsonView = null;
        startScreenSummary = null;
        hasStartScreenSummary = false;
        nextStartScreenSummaryRefreshTime = 0;
    }
}

[InitializeOnLoad]
internal static class GCDevJsonEditorPlayModeGate
{
    private static readonly List<GCDevJsonInspectorState> states = new List<GCDevJsonInspectorState>();

    static GCDevJsonEditorPlayModeGate()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        GCLocalPlaySession.RegisterProvider(new GCDevJsonLocalPlaySessionProvider());
        GCLocalPlaySession.RegisterPreflightHandler(RunPreflight);
        GCLocalPlaySession.RegisterCaptureSucceededHandler(MarkPlayChangesCaptured);
    }

    internal static void Register(GCDevJsonInspectorState state)
    {
        if (state == null || states.Contains(state))
        {
            return;
        }

        states.Add(state);
    }

    internal static void Unregister(GCDevJsonInspectorState state)
    {
        if (state == null)
        {
            return;
        }

        states.Remove(state);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode)
        {
            return;
        }

        var result = GCLocalPlaySession.RunPreflight(GCLocalPlaySessionBoundary.UnityPlayModeEntry);
        if (result.success)
        {
            return;
        }

        GCLocalPlaySession.LogBlockedBoundary(result);
        EditorApplication.isPlaying = false;
    }

    private static GCLocalPlaySessionPreflightResult RunPreflight(GCLocalPlaySessionBoundary context)
    {
        try
        {
            var snapshot = GetStateSnapshot();
            GCDevJsonInspectorState dirtyState = null;
            for (var index = 0; index < snapshot.Length; index++)
            {
                var state = snapshot[index];
                var stateResult = state.ValidateForPlayBoundary(context);
                if (!stateResult.success)
                {
                    return stateResult;
                }

                if (!state.IsDirty)
                {
                    continue;
                }

                if (dirtyState != null)
                {
                    return FailForMultipleDirtyDrafts(context, dirtyState.DevJsonPath);
                }

                dirtyState = state;
            }

            if (dirtyState != null)
            {
                var applyResult = dirtyState.PrepareForPlayBoundary(context);
                if (!applyResult.success)
                {
                    return applyResult;
                }
            }

            return GCLocalPlaySessionPreflightResult.Succeeded();
        }
        catch (Exception exception)
        {
            var message = GCLocalPlaySession.GetBoundaryDisplayName(context) + " blocked because gc.dev.json preflight failed: " + exception.Message;
            return GCLocalPlaySessionPreflightResult.Failed(
                message,
                null,
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    message,
                    null,
                    code: GCDevJsonIssueCode.ReadError.ToString()
                ))
            );
        }
    }

    private static void MarkPlayChangesCaptured()
    {
        var snapshot = GetStateSnapshot();
        for (var index = 0; index < snapshot.Length; index++)
        {
            var state = snapshot[index];
            state.MarkPlayChangesCaptured();
        }
    }

    private static GCDevJsonInspectorState[] GetStateSnapshot()
    {
        for (var index = states.Count - 1; index >= 0; index--)
        {
            if (states[index] == null)
            {
                states.RemoveAt(index);
            }
        }

        return states.ToArray();
    }

    private static GCLocalPlaySessionPreflightResult FailForMultipleDirtyDrafts(GCLocalPlaySessionBoundary context, string path)
    {
        var message = GCLocalPlaySession.GetBoundaryDisplayName(context) +
                      " blocked because multiple GamingCouch inspectors have unsaved gc.dev.json drafts. Apply, revert, or close duplicate inspectors before continuing.";
        return GCLocalPlaySessionPreflightResult.Failed(
            message,
            path,
            GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                message,
                path,
                code: GCDevJsonIssueCode.WriteError.ToString()
            ))
        );
    }
}

internal sealed class GCDevJsonLocalPlaySessionProvider : IGCLocalPlaySessionProvider
{
    private static readonly GCPlayerColor[] SeatColors =
    {
        GCPlayerColor.blue,
        GCPlayerColor.red,
        GCPlayerColor.green,
        GCPlayerColor.yellow,
        GCPlayerColor.purple,
        GCPlayerColor.pink,
        GCPlayerColor.cyan,
        GCPlayerColor.brown,
    };

    private readonly GCDevJsonStore devStore;

    internal GCDevJsonLocalPlaySessionProvider()
        : this(new GCDevJsonStore())
    {
    }

    internal GCDevJsonLocalPlaySessionProvider(GCDevJsonStore devStore)
    {
        if (devStore == null)
        {
            throw new ArgumentNullException(nameof(devStore));
        }

        this.devStore = devStore;
    }

    public GCLocalPlaySessionCaptureResult Capture()
    {
        return Capture(devStore.Read());
    }

    public GCLocalPlaySessionPreflightResult Validate(GCLocalPlaySessionBoundary context)
    {
        GCDevJsonReadResult readResult;
        try
        {
            readResult = devStore.Read();
        }
        catch (Exception exception)
        {
            var exceptionMessage = GCLocalPlaySession.GetBoundaryDisplayName(context) +
                                   " blocked because gc.dev.json preflight failed: " + exception.Message;
            return GCLocalPlaySessionPreflightResult.Failed(
                exceptionMessage,
                null,
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    exceptionMessage,
                    null,
                    code: GCDevJsonIssueCode.ReadError.ToString()
                ))
            );
        }

        if (readResult != null && readResult.IsValid)
        {
            return GCLocalPlaySessionPreflightResult.Succeeded();
        }

        var message = GCLocalPlaySession.GetBoundaryDisplayName(context) +
                      " blocked because root gc.dev.json is missing, invalid, or rejected by valid platform data gates.";
        return GCLocalPlaySessionPreflightResult.Failed(
            message,
            GetPath(readResult),
            MapValidation(GetValidation(readResult, "gc.dev.json could not be read because the read result was missing."))
        );
    }

    internal GCLocalPlaySessionCaptureResult Capture(GCDevJsonReadResult readResult)
    {
        if (readResult == null)
        {
            return GCLocalPlaySessionCaptureResult.Failed(
                null,
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    "gc.dev.json could not be read because the read result was missing.",
                    null,
                    code: GCDevJsonIssueCode.ReadError.ToString()
                ))
            );
        }

        if (!readResult.IsValid)
        {
            return GCLocalPlaySessionCaptureResult.Failed(GetPath(readResult), MapValidation(readResult.validation));
        }

        var data = readResult.data;
        int seed;
        if (!TryResolveSeed(data.seed, out seed))
        {
            return GCLocalPlaySessionCaptureResult.Failed(
                GetPath(readResult),
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    "gc.dev.json seed must be \"random\" or an integer string from " +
                    GCDevJsonFile.MinSeed + " to " + GCDevJsonFile.MaxSeed + ".",
                    GetPath(readResult),
                    code: GCDevJsonIssueCode.InvalidSeed.ToString()
                ))
            );
        }

        var setupOptions = CreateSetupOptions(data);
        var playOptions = CreatePlayOptions(
            data,
            seed,
            GCPlatformRuntimeViewBuilder.Build(readResult.platformDataReadResult, data.entryKey),
            out var seatIdentities
        );
        return GCLocalPlaySessionCaptureResult.Succeeded(
            setupOptions,
            playOptions,
            seatIdentities,
            MapValidation(readResult.validation),
            GetPath(readResult)
        );
    }

    internal static GCLocalPlaySessionValidationResult MapValidation(GCDevJsonValidationResult validation)
    {
        if (validation == null || validation.issues == null)
        {
            return GCLocalPlaySessionValidationResult.Valid();
        }

        var issues = new GCLocalPlaySessionIssue[validation.issues.Length];
        for (var index = 0; index < validation.issues.Length; index++)
        {
            issues[index] = MapIssue(validation.issues[index]);
        }

        return GCLocalPlaySessionValidationResult.FromIssues(issues);
    }

    private static GCLocalPlaySessionIssue MapIssue(GCDevJsonIssue issue)
    {
        if (issue == null)
        {
            return null;
        }

        if (issue.severity == GCDevJsonIssueSeverity.Warning)
        {
            return GCLocalPlaySessionIssue.Warning(
                issue.message,
                issue.path,
                issue.seatIndex,
                issue.fieldName,
                issue.entryKey,
                issue.code.ToString()
            );
        }

        return GCLocalPlaySessionIssue.Error(
            issue.message,
            issue.path,
            issue.seatIndex,
            issue.fieldName,
            issue.entryKey,
            issue.code.ToString()
        );
    }

    private static GCDevJsonValidationResult GetValidation(GCDevJsonReadResult readResult, string fallbackMessage)
    {
        if (readResult != null && readResult.validation != null)
        {
            return readResult.validation;
        }

        return GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(
            GCDevJsonIssueCode.ReadError,
            fallbackMessage,
            GetPath(readResult)
        ));
    }

    private static GCSetupOptions CreateSetupOptions(GCDevJsonFile data)
    {
        return new GCSetupOptions
        {
            isServer = true,
            gameModeId = data.entryKey,
            mode = GCMode.Development,
        };
    }

    private static GCPlayOptions CreatePlayOptions(
        GCDevJsonFile data,
        int seed,
        GCPlatformRuntimeView platformData,
        out GCSeatIdentity[] seatIdentities
    )
    {
        var playerCount = data.EnabledSeatCount;
        var options = new GCPlayOptions
        {
            players = new GCPlayerOptions[playerCount],
            seed = seed,
            platformData = platformData ?? GCPlatformRuntimeView.CreateFallbackMissing(),
        };
        seatIdentities = new GCSeatIdentity[playerCount];

        var playerIndex = 0;
        for (var sourceSeatIndex = 0; sourceSeatIndex < data.seats.Length; sourceSeatIndex++)
        {
            var seat = data.seats[sourceSeatIndex];
            if (!seat.enabled)
            {
                continue;
            }

            var playerType = seat.isBot ? GCPlayerType.bot : GCPlayerType.player;
            var playerColor = SeatColors[sourceSeatIndex];
            var oneBasedSourceSeatIndex = sourceSeatIndex + 1;

            options.players[playerIndex] = new GCPlayerOptions
            {
                playerIndex = playerIndex,
                playerSeed = GCPlayerSeed.FromPlayerName(seat.name),
                type = playerType.ToString(),
                color = playerColor.ToString(),
            };

            seatIdentities[playerIndex] = new GCSeatIdentity
            {
                sourceSeatIndex = oneBasedSourceSeatIndex,
                stableKey = oneBasedSourceSeatIndex.ToString(),
                label = "Seat " + oneBasedSourceSeatIndex,
                playerType = playerType,
                playerColor = playerColor,
            };

            playerIndex++;
        }

        return options;
    }

    private static bool TryResolveSeed(string seed, out int value)
    {
        if (seed == GCDevJsonFile.RandomSeed)
        {
            value = UnityEngine.Random.Range(GCDevJsonFile.MinSeed, GCDevJsonFile.MaxSeed + 1);
            return true;
        }

        return TryParseSeed(seed, out value) &&
               value >= GCDevJsonFile.MinSeed &&
               value <= GCDevJsonFile.MaxSeed;
    }

    internal static bool TryParseSeed(string seed, out int value)
    {
        return int.TryParse(seed, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static string GetPath(GCDevJsonReadResult readResult)
    {
        return readResult != null && readResult.parsedFile != null ? readResult.parsedFile.path : null;
    }
}
