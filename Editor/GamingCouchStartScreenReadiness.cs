using System;
using DSB.GC;
using DSB.GC.Dev;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

internal enum GCStartScreenReadinessCheckState
{
    Pass,
    Warning,
    Fail,
    Blocked,
}

internal enum GCStartScreenReadinessCheckId
{
    ActiveScene,
    GamingCouchInstance,
    SingleGamingCouchInstance,
    ListenerAssigned,
    PlayerPrefabAssigned,
    ActiveSceneFirstBuildSettingsScene,
    GameViewAspect16By9,
    WebGLExportSetup,
    LocalPlayJsonValid,
}

internal sealed class GCStartScreenReadinessCheck
{
    internal readonly GCStartScreenReadinessCheckId id;
    internal readonly string label;
    internal readonly GCStartScreenReadinessCheckState state;
    internal readonly string message;

    internal GCStartScreenReadinessCheck(
        GCStartScreenReadinessCheckId id,
        string label,
        GCStartScreenReadinessCheckState state,
        string message
    )
    {
        this.id = id;
        this.label = label;
        this.state = state;
        this.message = message;
    }

    internal bool IsSatisfied
    {
        get { return state == GCStartScreenReadinessCheckState.Pass || state == GCStartScreenReadinessCheckState.Warning; }
    }
}

internal sealed class GCStartScreenLocalPlayJsonReadiness
{
    internal readonly bool isValid;
    internal readonly string path;
    internal readonly string message;
    internal readonly GCDevJsonValidationResult validation;

    internal GCStartScreenLocalPlayJsonReadiness(
        bool isValid,
        string path,
        string message,
        GCDevJsonValidationResult validation
    )
    {
        this.isValid = isValid;
        this.path = path;
        this.message = message;
        this.validation = validation;
    }

    internal GCDevJsonIssue[] Issues
    {
        get { return validation != null && validation.issues != null ? validation.issues : new GCDevJsonIssue[0]; }
    }
}

internal sealed class GCStartScreenReadiness
{
    internal const string GamingCouchInstanceCheckLabel = "Exactly one GamingCouch in scene";

    internal readonly Scene scene;
    internal readonly string sceneName;
    internal readonly string scenePath;
    internal readonly GamingCouch[] gamingCouches;
    internal readonly GamingCouch gamingCouch;
    internal readonly UnityEngine.Object listener;
    internal readonly UnityEngine.Object playerPrefab;
    internal readonly GCStartScreenLocalPlayJsonReadiness localPlayJson;
    internal readonly GCActiveSceneBuildSettingsReadiness buildSettings;
    internal readonly GCGameViewAspectReadiness gameViewAspect;
    internal readonly GCWebGLExportReadiness webGLExport;
    internal readonly GCStartScreenReadinessCheck activeSceneCheck;
    internal readonly GCStartScreenReadinessCheck[] checklist;

    internal GCStartScreenReadiness(
        Scene scene,
        GamingCouch[] gamingCouches,
        GamingCouch gamingCouch,
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab,
        GCStartScreenLocalPlayJsonReadiness localPlayJson,
        GCActiveSceneBuildSettingsReadiness buildSettings = null,
        GCGameViewAspectReadiness gameViewAspect = null,
        GCWebGLExportReadiness webGLExport = null
    )
    {
        this.scene = scene;
        var hasLoadedScene = scene.IsValid() && scene.isLoaded;
        sceneName = hasLoadedScene && !string.IsNullOrEmpty(scene.name) ? scene.name : "Untitled";
        scenePath = hasLoadedScene ? scene.path : null;
        this.gamingCouches = gamingCouches ?? new GamingCouch[0];
        this.gamingCouch = gamingCouch;
        this.listener = listener;
        this.playerPrefab = playerPrefab;
        this.localPlayJson = localPlayJson;
        this.buildSettings = buildSettings ?? GamingCouchBuildSettingsReadiness.Inspect(scene, EditorBuildSettings.scenes);
        this.gameViewAspect = gameViewAspect ?? GamingCouchGameViewAspect.InspectSizeEntries(null, -1, false, null);
        this.webGLExport = webGLExport;
        activeSceneCheck = BuildActiveSceneCheck();
        checklist = BuildChecklist();
    }

    internal bool IsSceneReady
    {
        get
        {
            return GetCheck(GCStartScreenReadinessCheckId.ActiveScene).IsSatisfied &&
                   GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance).IsSatisfied &&
                   GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned).IsSatisfied &&
                   GetCheck(GCStartScreenReadinessCheckId.PlayerPrefabAssigned).IsSatisfied;
        }
    }

    internal bool IsLocalPlayReady
    {
        get { return IsSceneReady && GetCheck(GCStartScreenReadinessCheckId.LocalPlayJsonValid).IsSatisfied; }
    }

    internal bool HasBlockingVisibleChecklistIssues
    {
        get { return HasBlockingChecklistIssues(checklist); }
    }

    internal bool HasSafeAutomatableSetupActions
    {
        get
        {
            return DoesActiveSceneSetupCheckNeedSetup(GCStartScreenReadinessCheckId.GamingCouchInstance) ||
                   DoesActiveSceneSetupCheckNeedSetup(GCStartScreenReadinessCheckId.ListenerAssigned) ||
                   DoesActiveSceneSetupCheckNeedSetup(GCStartScreenReadinessCheckId.PlayerPrefabAssigned) ||
                   DoesBuildSettingsNeedSetup() ||
                   DoesGameViewNeedSetup();
        }
    }

    internal GCStartScreenReadinessCheck GetCheck(GCStartScreenReadinessCheckId id)
    {
        GCStartScreenReadinessCheck check;
        if (TryGetCheck(id, out check))
        {
            return check;
        }

        throw new ArgumentException("Unknown readiness check: " + id, nameof(id));
    }

    internal bool TryGetCheck(GCStartScreenReadinessCheckId id, out GCStartScreenReadinessCheck check)
    {
        id = NormalizeCheckId(id);
        if (id == GCStartScreenReadinessCheckId.ActiveScene)
        {
            check = activeSceneCheck;
            return check != null;
        }

        for (var index = 0; checklist != null && index < checklist.Length; index++)
        {
            if (checklist[index] != null && checklist[index].id == id)
            {
                check = checklist[index];
                return true;
            }
        }

        check = null;
        return false;
    }

    internal static GCStartScreenReadinessCheckId NormalizeCheckId(GCStartScreenReadinessCheckId id)
    {
        return id == GCStartScreenReadinessCheckId.SingleGamingCouchInstance
            ? GCStartScreenReadinessCheckId.GamingCouchInstance
            : id;
    }

    internal static bool HasBlockingChecklistIssues(GCStartScreenReadinessCheck[] checks)
    {
        for (var index = 0; checks != null && index < checks.Length; index++)
        {
            if (checks[index] == null)
            {
                continue;
            }

            if (checks[index].state == GCStartScreenReadinessCheckState.Fail ||
                checks[index].state == GCStartScreenReadinessCheckState.Blocked)
            {
                return true;
            }
        }

        return false;
    }

    private GCStartScreenReadinessCheck[] BuildChecklist()
    {
        return new[]
        {
            BuildGamingCouchInstanceCheck(),
            BuildListenerAssignedCheck(),
            BuildPlayerPrefabAssignedCheck(),
            BuildActiveSceneFirstBuildSettingsSceneCheck(),
            BuildGameViewAspect16By9Check(),
            BuildWebGLExportSetupCheck(),
            BuildLocalPlayJsonValidCheck(),
        };
    }

    private GCStartScreenReadinessCheck BuildActiveSceneCheck()
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.ActiveScene,
                "Active scene is available",
                GCStartScreenReadinessCheckState.Fail,
                "No loaded active scene is available for GamingCouch setup inspection."
            );
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.ActiveScene,
            "Active scene is available",
            GCStartScreenReadinessCheckState.Pass,
            "Inspecting active scene: " + sceneName + "."
        );
    }

    private GCStartScreenReadinessCheck BuildGamingCouchInstanceCheck()
    {
        if (gamingCouches.Length == 0)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.GamingCouchInstance,
                GamingCouchInstanceCheckLabel,
                GCStartScreenReadinessCheckState.Fail,
                "Create a GamingCouch object to continue active-scene setup."
            );
        }

        if (gamingCouches.Length > 1)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.GamingCouchInstance,
                GamingCouchInstanceCheckLabel,
                GCStartScreenReadinessCheckState.Fail,
                "The active scene contains multiple GamingCouch components. Remove duplicates manually before running quick-start setup."
            );
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.GamingCouchInstance,
            GamingCouchInstanceCheckLabel,
            GCStartScreenReadinessCheckState.Pass,
            "The active scene has one GamingCouch component."
        );
    }

    private GCStartScreenReadinessCheck BuildListenerAssignedCheck()
    {
        if (gamingCouch == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.ListenerAssigned,
                "Listener is assigned",
                GCStartScreenReadinessCheckState.Blocked,
                "Listener assignment can be checked after the active scene has exactly one GamingCouch component."
            );
        }

        if (listener == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.ListenerAssigned,
                "Listener is assigned",
                GCStartScreenReadinessCheckState.Fail,
                "The GamingCouch listener reference is missing."
            );
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.ListenerAssigned,
            "Listener is assigned",
            GCStartScreenReadinessCheckState.Pass,
            "The GamingCouch listener reference points to " + listener.name + "."
        );
    }

    private GCStartScreenReadinessCheck BuildPlayerPrefabAssignedCheck()
    {
        if (gamingCouch == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.PlayerPrefabAssigned,
                "Player prefab is assigned",
                GCStartScreenReadinessCheckState.Blocked,
                "Player prefab assignment can be checked after the active scene has exactly one GamingCouch component."
            );
        }

        if (playerPrefab == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.PlayerPrefabAssigned,
                "Player prefab is assigned",
                GCStartScreenReadinessCheckState.Fail,
                "The GamingCouch player prefab reference is missing."
            );
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.PlayerPrefabAssigned,
            "Player prefab is assigned",
            GCStartScreenReadinessCheckState.Pass,
            "The GamingCouch player prefab reference points to " + playerPrefab.name + "."
        );
    }

    private GCStartScreenReadinessCheck BuildLocalPlayJsonValidCheck()
    {
        if (localPlayJson == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.LocalPlayJsonValid,
                "Local play JSON is valid",
                GCStartScreenReadinessCheckState.Fail,
                "gc.dev.json validation did not produce a result."
            );
        }

        if (localPlayJson.isValid)
        {
            var warningCount = localPlayJson.validation != null ? localPlayJson.validation.WarningCount : 0;
            if (warningCount > 0)
            {
                return new GCStartScreenReadinessCheck(
                    GCStartScreenReadinessCheckId.LocalPlayJsonValid,
                    "Local play JSON is valid",
                    GCStartScreenReadinessCheckState.Warning,
                    "gc.dev.json is valid with " + warningCount + " warning" + (warningCount == 1 ? string.Empty : "s") + "."
                );
            }

            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.LocalPlayJsonValid,
                "Local play JSON is valid",
                GCStartScreenReadinessCheckState.Pass,
                "gc.dev.json is valid for local Play Mode."
            );
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.LocalPlayJsonValid,
            "Local play JSON is valid",
            GCStartScreenReadinessCheckState.Fail,
            string.IsNullOrEmpty(localPlayJson.message)
                ? "gc.dev.json is missing or invalid for local Play Mode."
                : localPlayJson.message
        );
    }

    private GCStartScreenReadinessCheck BuildActiveSceneFirstBuildSettingsSceneCheck()
    {
        if (buildSettings == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.ActiveSceneFirstBuildSettingsScene,
                "Active scene is first Build Settings scene",
                GCStartScreenReadinessCheckState.Fail,
                "Build Settings readiness could not be inspected."
            );
        }

        if (buildSettings.IsReady)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.ActiveSceneFirstBuildSettingsScene,
                "Active scene is first Build Settings scene",
                GCStartScreenReadinessCheckState.Pass,
                buildSettings.message
            );
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.ActiveSceneFirstBuildSettingsScene,
            "Active scene is first Build Settings scene",
            buildSettings.status == GCActiveSceneBuildSettingsStatus.NoActiveScene
                ? GCStartScreenReadinessCheckState.Blocked
                : GCStartScreenReadinessCheckState.Fail,
            buildSettings.message
        );
    }

    private GCStartScreenReadinessCheck BuildGameViewAspect16By9Check()
    {
        if (gameViewAspect == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.GameViewAspect16By9,
                "Game View uses 16:9 preview",
                GCStartScreenReadinessCheckState.Warning,
                "Game View aspect could not be inspected. Choose a 16:9 Game View entry manually if needed."
            );
        }

        var state = GCStartScreenReadinessCheckState.Fail;
        if (gameViewAspect.status == GCGameViewAspectStatus.Ready)
        {
            state = GCStartScreenReadinessCheckState.Pass;
        }
        else if (gameViewAspect.status == GCGameViewAspectStatus.Unknown)
        {
            state = GCStartScreenReadinessCheckState.Warning;
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.GameViewAspect16By9,
            "Game View uses 16:9 preview",
            state,
            gameViewAspect.message
        );
    }

    private GCStartScreenReadinessCheck BuildWebGLExportSetupCheck()
    {
        if (webGLExport == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.WebGLExportSetup,
                "Clean WebGL export setup is ready",
                GCStartScreenReadinessCheckState.Fail,
                "Clean WebGL export setup readiness could not be inspected."
            );
        }

        var state = GCStartScreenReadinessCheckState.Fail;
        switch (webGLExport.status)
        {
            case GCWebGLExportSetupStatus.Ready:
                state = GCStartScreenReadinessCheckState.Pass;
                break;
            case GCWebGLExportSetupStatus.Warning:
                state = GCStartScreenReadinessCheckState.Warning;
                break;
            case GCWebGLExportSetupStatus.Blocked:
                state = GCStartScreenReadinessCheckState.Blocked;
                break;
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.WebGLExportSetup,
            "Clean WebGL export setup is ready",
            state,
            webGLExport.message
        );
    }

    private bool DoesActiveSceneSetupCheckNeedSetup(GCStartScreenReadinessCheckId id)
    {
        id = NormalizeCheckId(id);
        GCStartScreenReadinessCheck check;
        if (!TryGetCheck(id, out check) || check.state != GCStartScreenReadinessCheckState.Fail)
        {
            return false;
        }

        switch (id)
        {
            case GCStartScreenReadinessCheckId.GamingCouchInstance:
                return gamingCouches != null && gamingCouches.Length == 0;
            case GCStartScreenReadinessCheckId.ListenerAssigned:
                return CanAssignMissingActiveSceneReference(GamingCouchSceneWiring.ListenerPropertyName);
            case GCStartScreenReadinessCheckId.PlayerPrefabAssigned:
                return CanAssignMissingActiveSceneReference(GamingCouchSceneWiring.PlayerPrefabPropertyName);
            default:
                return false;
        }
    }

    private bool DoesBuildSettingsNeedSetup()
    {
        GCStartScreenReadinessCheck check;
        return buildSettings != null &&
               buildSettings.CanSetFirst &&
               TryGetCheck(GCStartScreenReadinessCheckId.ActiveSceneFirstBuildSettingsScene, out check) &&
               !check.IsSatisfied;
    }

    private bool DoesGameViewNeedSetup()
    {
        GCStartScreenReadinessCheck check;
        return gameViewAspect != null &&
               gameViewAspect.HasSafeSelectionAction &&
               TryGetCheck(GCStartScreenReadinessCheckId.GameViewAspect16By9, out check) &&
               !check.IsSatisfied;
    }

    private bool CanAssignMissingActiveSceneReference(string propertyName)
    {
        return gamingCouch != null &&
               GamingCouchSceneWiring.HasObjectReferenceSlot(gamingCouch, propertyName) &&
               !GamingCouchSceneWiring.HasObjectReference(gamingCouch, propertyName);
    }
}

internal static class GCStartScreenReadinessService
{
    internal static GCStartScreenReadiness InspectActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        var gamingCouches = GamingCouchSceneWiring.FindGamingCouchesInScene(scene);
        var gamingCouch = gamingCouches.Length == 1 ? gamingCouches[0] : null;
        var listener = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName);
        var playerPrefab = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName);
        var localPlayJson = InspectLocalPlayJson();
        var buildSettings = GamingCouchBuildSettingsReadiness.Inspect(scene, EditorBuildSettings.scenes);
        var gameViewAspect = GamingCouchGameViewAspect.Inspect();
        var webGLExport = GamingCouchWebGLExportSetup.InspectReadiness();

        return new GCStartScreenReadiness(
            scene,
            gamingCouches,
            gamingCouch,
            listener,
            playerPrefab,
            localPlayJson,
            buildSettings,
            gameViewAspect,
            webGLExport
        );
    }

    private static GCStartScreenLocalPlayJsonReadiness InspectLocalPlayJson()
    {
        try
        {
            var readResult = new GCDevJsonStore().Read();
            if (readResult == null)
            {
                return new GCStartScreenLocalPlayJsonReadiness(
                    false,
                    null,
                    "gc.dev.json could not be read because the read result was missing.",
                    GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(
                        GCDevJsonIssueCode.ReadError,
                        "gc.dev.json could not be read because the read result was missing.",
                        null
                    ))
                );
            }

            var validation = readResult.validation ?? GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(
                GCDevJsonIssueCode.ReadError,
                "gc.dev.json validation did not produce a result.",
                readResult.parsedFile != null ? readResult.parsedFile.path : null
            ));

            return new GCStartScreenLocalPlayJsonReadiness(
                readResult.IsValid,
                readResult.parsedFile != null ? readResult.parsedFile.path : null,
                readResult.IsValid ? null : "gc.dev.json is missing, invalid, or rejected by valid gc.metadata.json gates.",
                validation
            );
        }
        catch (Exception exception)
        {
            var message = "gc.dev.json could not be validated: " + exception.Message;
            return new GCStartScreenLocalPlayJsonReadiness(
                false,
                null,
                message,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.ReadError, message, null))
            );
        }
    }
}
