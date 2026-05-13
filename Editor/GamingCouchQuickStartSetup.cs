using DSB.GC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal enum GCQuickStartSetupIntent
{
    ActiveScene,
    QuickStartScene,
}

internal enum GCQuickStartSetupAction
{
    ActiveSceneMissingPieces,
    ActiveScenePlayerPrefab,
    ActiveSceneGameListener,
    QuickStartScene,
}

internal enum GCQuickStartScriptSetupStatus
{
    Ready,
    PendingCompilation,
    Blocked,
}

internal enum GCQuickStartPlayerPrefabSetupStatus
{
    Ready,
    Blocked,
}

internal enum GCQuickStartGameListenerSetupStatus
{
    Ready,
    Blocked,
}

internal enum GCQuickStartSceneSetupStatus
{
    Ready,
    PendingCompilation,
    Blocked,
}

internal enum GCQuickStartActiveSceneSetupStatus
{
    Ready,
    PendingCompilation,
    Blocked,
}

internal sealed class GCQuickStartScriptSetupResult
{
    internal readonly GCQuickStartScriptSetupStatus status;
    internal readonly bool changed;
    internal readonly string message;
    internal readonly string[] createdAssetPaths;
    internal readonly string[] reusedAssetPaths;
    internal readonly string[] blockedReasons;

    internal GCQuickStartScriptSetupResult(
        GCQuickStartScriptSetupStatus status,
        bool changed,
        string message,
        string[] createdAssetPaths,
        string[] reusedAssetPaths,
        string[] blockedReasons
    )
    {
        this.status = status;
        this.changed = changed;
        this.message = message;
        this.createdAssetPaths = createdAssetPaths ?? new string[0];
        this.reusedAssetPaths = reusedAssetPaths ?? new string[0];
        this.blockedReasons = blockedReasons ?? new string[0];
    }

    internal bool IsBlocked
    {
        get { return status == GCQuickStartScriptSetupStatus.Blocked; }
    }

    internal bool IsPendingCompilation
    {
        get { return status == GCQuickStartScriptSetupStatus.PendingCompilation; }
    }
}

internal sealed class GCQuickStartPlayerPrefabSetupResult
{
    internal readonly GCQuickStartPlayerPrefabSetupStatus status;
    internal readonly bool changed;
    internal readonly GameObject prefab;
    internal readonly string message;
    internal readonly string[] blockedReasons;

    internal GCQuickStartPlayerPrefabSetupResult(
        GCQuickStartPlayerPrefabSetupStatus status,
        bool changed,
        GameObject prefab,
        string message,
        string[] blockedReasons
    )
    {
        this.status = status;
        this.changed = changed;
        this.prefab = prefab;
        this.message = message;
        this.blockedReasons = blockedReasons ?? new string[0];
    }

    internal bool IsBlocked
    {
        get { return status == GCQuickStartPlayerPrefabSetupStatus.Blocked; }
    }
}

internal sealed class GCQuickStartGameListenerSetupResult
{
    internal readonly GCQuickStartGameListenerSetupStatus status;
    internal readonly bool changed;
    internal readonly GameObject listenerObject;
    internal readonly string message;
    internal readonly string[] blockedReasons;

    internal GCQuickStartGameListenerSetupResult(
        GCQuickStartGameListenerSetupStatus status,
        bool changed,
        GameObject listenerObject,
        string message,
        string[] blockedReasons
    )
    {
        this.status = status;
        this.changed = changed;
        this.listenerObject = listenerObject;
        this.message = message;
        this.blockedReasons = blockedReasons ?? new string[0];
    }

    internal bool IsBlocked
    {
        get { return status == GCQuickStartGameListenerSetupStatus.Blocked; }
    }
}

internal sealed class GCQuickStartSceneSetupResult
{
    internal readonly GCQuickStartSceneSetupStatus status;
    internal readonly bool changed;
    internal readonly Scene scene;
    internal readonly string sceneAssetPath;
    internal readonly string message;
    internal readonly string[] blockedReasons;

    internal GCQuickStartSceneSetupResult(
        GCQuickStartSceneSetupStatus status,
        bool changed,
        Scene scene,
        string sceneAssetPath,
        string message,
        string[] blockedReasons
    )
    {
        this.status = status;
        this.changed = changed;
        this.scene = scene;
        this.sceneAssetPath = sceneAssetPath;
        this.message = message;
        this.blockedReasons = blockedReasons ?? new string[0];
    }

    internal bool IsBlocked
    {
        get { return status == GCQuickStartSceneSetupStatus.Blocked; }
    }

    internal bool IsPendingCompilation
    {
        get { return status == GCQuickStartSceneSetupStatus.PendingCompilation; }
    }
}

internal sealed class GCQuickStartActiveSceneSetupResult
{
    internal readonly GCQuickStartActiveSceneSetupStatus status;
    internal readonly bool changed;
    internal readonly string message;
    internal readonly string[] details;

    internal GCQuickStartActiveSceneSetupResult(
        GCQuickStartActiveSceneSetupStatus status,
        bool changed,
        string message,
        string[] details
    )
    {
        this.status = status;
        this.changed = changed;
        this.message = message;
        this.details = details ?? new string[0];
    }

    internal bool IsBlocked
    {
        get { return status == GCQuickStartActiveSceneSetupStatus.Blocked; }
    }

    internal bool IsPendingCompilation
    {
        get { return status == GCQuickStartActiveSceneSetupStatus.PendingCompilation; }
    }
}

internal sealed class GCQuickStartScriptSetupSpec
{
    internal readonly string scriptFolderAssetPath;
    internal readonly string gameScriptAssetPath;
    internal readonly string playerScriptAssetPath;
    internal readonly string playerPrefabAssetPath;
    internal readonly string gameTypeName;
    internal readonly string playerTypeName;
    internal readonly string listenerObjectName;
    internal readonly bool requiresGeneratedScriptFolder;
    internal readonly bool requiresQuickStartFolders;
    private readonly Func<string> gameScriptSourceFactory;
    private readonly Func<string> playerScriptSourceFactory;

    internal GCQuickStartScriptSetupSpec(
        string scriptFolderAssetPath,
        string gameScriptAssetPath,
        string playerScriptAssetPath,
        string playerPrefabAssetPath,
        string gameTypeName,
        string playerTypeName,
        string listenerObjectName,
        bool requiresGeneratedScriptFolder,
        bool requiresQuickStartFolders,
        Func<string> gameScriptSourceFactory,
        Func<string> playerScriptSourceFactory
    )
    {
        this.scriptFolderAssetPath = scriptFolderAssetPath;
        this.gameScriptAssetPath = gameScriptAssetPath;
        this.playerScriptAssetPath = playerScriptAssetPath;
        this.playerPrefabAssetPath = playerPrefabAssetPath;
        this.gameTypeName = gameTypeName;
        this.playerTypeName = playerTypeName;
        this.listenerObjectName = listenerObjectName;
        this.requiresGeneratedScriptFolder = requiresGeneratedScriptFolder;
        this.requiresQuickStartFolders = requiresQuickStartFolders;
        this.gameScriptSourceFactory = gameScriptSourceFactory;
        this.playerScriptSourceFactory = playerScriptSourceFactory;
    }

    internal string BuildGameScriptSource()
    {
        return gameScriptSourceFactory();
    }

    internal string BuildPlayerScriptSource()
    {
        return playerScriptSourceFactory();
    }
}

internal sealed class GCQuickStartSetupContinuationContext
{
    internal readonly GCQuickStartSetupIntent intent;
    internal readonly GCQuickStartSetupAction action;
    internal readonly bool resumedAfterCompilation;
    internal readonly string quickStartFolderAssetPath;
    internal readonly string gameScriptAssetPath;
    internal readonly string playerScriptAssetPath;
    internal readonly string playerPrefabAssetPath;
    internal readonly string gameTypeName;
    internal readonly string playerTypeName;
    internal readonly string listenerObjectName;
    internal readonly Type gameType;
    internal readonly Type playerType;

    internal GCQuickStartSetupContinuationContext(
        GCQuickStartSetupIntent intent,
        GCQuickStartSetupAction action,
        bool resumedAfterCompilation,
        string quickStartFolderAssetPath,
        string gameScriptAssetPath,
        string playerScriptAssetPath,
        string playerPrefabAssetPath,
        string gameTypeName,
        string playerTypeName,
        string listenerObjectName,
        Type gameType,
        Type playerType
    )
    {
        this.intent = intent;
        this.action = action;
        this.resumedAfterCompilation = resumedAfterCompilation;
        this.quickStartFolderAssetPath = quickStartFolderAssetPath;
        this.gameScriptAssetPath = gameScriptAssetPath;
        this.playerScriptAssetPath = playerScriptAssetPath;
        this.playerPrefabAssetPath = playerPrefabAssetPath;
        this.gameTypeName = gameTypeName;
        this.playerTypeName = playerTypeName;
        this.listenerObjectName = listenerObjectName;
        this.gameType = gameType;
        this.playerType = playerType;
    }

    internal bool HasRequiredTypes
    {
        get { return gameType != null && playerType != null; }
    }
}

[InitializeOnLoad]
internal static class GamingCouchQuickStartSetup
{
    internal const string ProjectFolderAssetPath = "Assets/GamingCouch";
    internal const string ExampleFolderAssetPath = ProjectFolderAssetPath + "/GCExample";
    internal const string QuickStartFolderAssetPath = ExampleFolderAssetPath;
    internal const string GameTypeName = "GCGameExample";
    internal const string PlayerTypeName = "GCPlayerExample";
    internal const string GameScriptAssetPath = QuickStartFolderAssetPath + "/" + GameTypeName + ".cs";
    internal const string PlayerScriptAssetPath = QuickStartFolderAssetPath + "/" + PlayerTypeName + ".cs";
    internal const string ActiveSceneGameTypeName = GameTypeName;
    internal const string ActiveScenePlayerTypeName = PlayerTypeName;
    internal const string ActiveSceneGameScriptAssetPath = GameScriptAssetPath;
    internal const string ActiveScenePlayerScriptAssetPath = PlayerScriptAssetPath;
    internal const string PlayerPrefabAssetPath = QuickStartFolderAssetPath + "/" + PlayerTypeName + ".prefab";
    internal const string ActiveScenePlayerPrefabAssetPath = PlayerPrefabAssetPath;
    // Legacy quick-start names are retained only to recognize old generated prefab references.
    internal const string LegacyQuickStartPlayerTypeName = "GCQuickStartPlayer";
    internal const string LegacyQuickStartPlayerPrefabAssetPath = QuickStartFolderAssetPath + "/" + LegacyQuickStartPlayerTypeName + ".prefab";
    internal const string LegacyPlayerPrefabAssetPath = ProjectFolderAssetPath + "/QuickStart/" + LegacyQuickStartPlayerTypeName + ".prefab";
    internal const string QuickStartSceneAssetPath = QuickStartFolderAssetPath + "/GamingCouchQuickStart.unity";

    private const string PendingSetupSessionKey = "DSB.GC.QuickStart.PendingSetup.v1";
    private const string PendingIntentSessionKey = "DSB.GC.QuickStart.PendingIntent.v1";
    private const string PendingActionSessionKey = "DSB.GC.QuickStart.PendingAction.v1";
    private const string PendingWarningLoggedSessionKey = "DSB.GC.QuickStart.PendingWarningLogged.v1";
    private const string DefaultIntentValue = "ActiveScene";
    private const string DefaultActionValue = "ActiveSceneMissingPieces";
    private const string ListenerObjectName = "Game";
    private const string ActiveSceneGameListenerObjectName = ListenerObjectName;
    private const string CreateGameListenerUndoName = "Create Quick-Start Game Listener";
    private const string AddGameListenerComponentUndoName = "Add Quick-Start Game Listener";
    private const string CreateCameraUndoName = "Create Quick-Start Camera";
    private const string CreateLightUndoName = "Create Quick-Start Light";
    private const string PlayerVisualName = "Visual";
    private const string ExampleTemplateHeader =
        "/*\n" +
        " * GamingCouch example template file.\n" +
        " *\n" +
        " * Move this script into your project's own scripts folder, then rename the file and class to fit your project.\n" +
        " * For example: Game.cs/Game for your game script and Player.cs/Player for your player script.\n" +
        " */\n\n";

    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);
    private static readonly GCQuickStartScriptSetupSpec QuickStartScriptSetupSpec =
        new GCQuickStartScriptSetupSpec(
            QuickStartFolderAssetPath,
            GameScriptAssetPath,
            PlayerScriptAssetPath,
            PlayerPrefabAssetPath,
            GameTypeName,
            PlayerTypeName,
            ListenerObjectName,
            true,
            true,
            () => BuildGameScriptSource(GameTypeName, PlayerTypeName, true),
            () => BuildPlayerScriptSource(PlayerTypeName, true)
        );
    private static readonly GCQuickStartScriptSetupSpec ActiveSceneGameScriptSetupSpec =
        new GCQuickStartScriptSetupSpec(
            ExampleFolderAssetPath,
            ActiveSceneGameScriptAssetPath,
            ActiveScenePlayerScriptAssetPath,
            ActiveScenePlayerPrefabAssetPath,
            ActiveSceneGameTypeName,
            ActiveScenePlayerTypeName,
            ActiveSceneGameListenerObjectName,
            true,
            false,
            () => BuildGameScriptSource(ActiveSceneGameTypeName, ActiveScenePlayerTypeName, true),
            () => BuildPlayerScriptSource(ActiveScenePlayerTypeName, true)
        );
    private static Action<GCQuickStartSetupContinuationContext> scriptsReadyHandlers;
    private static GCQuickStartSceneSetupResult lastQuickStartSceneSetupResult;

    static GamingCouchQuickStartSetup()
    {
        RegisterScriptsReadyHandler(EnsureQuickStartGameListenerOnScriptsReady);
        RegisterScriptsReadyHandler(EnsureQuickStartPlayerPrefabOnScriptsReady);
        RegisterScriptsReadyHandler(EnsureQuickStartSceneOnScriptsReady);

        if (HasPendingSetup())
        {
            StartPendingSetupPolling();
        }
    }

    internal static void RegisterScriptsReadyHandler(Action<GCQuickStartSetupContinuationContext> handler)
    {
        if (handler == null)
        {
            return;
        }

        scriptsReadyHandlers -= handler;
        scriptsReadyHandlers += handler;

        if (HasPendingSetup())
        {
            StartPendingSetupPolling();
        }
    }

    internal static void UnregisterScriptsReadyHandler(Action<GCQuickStartSetupContinuationContext> handler)
    {
        if (handler == null)
        {
            return;
        }

        scriptsReadyHandlers -= handler;
    }

    internal static GCQuickStartScriptSetupResult EnsureQuickStartScripts()
    {
        return EnsureQuickStartScripts(GCQuickStartSetupIntent.ActiveScene);
    }

    internal static GCQuickStartScriptSetupResult EnsureQuickStartScripts(GCQuickStartSetupIntent intent)
    {
        return EnsureQuickStartScripts(intent, GetDefaultAction(intent), true);
    }

    private static GCQuickStartScriptSetupResult EnsureQuickStartScripts(
        GCQuickStartSetupIntent intent,
        GCQuickStartSetupAction action,
        bool dispatchWhenReady
    )
    {
        var spec = GetScriptSetupSpec(intent, action);
        var createdAssetPaths = new List<string>();
        var reusedAssetPaths = new List<string>();
        var blockedReasons = new List<string>();

        EnsureScriptFolders(spec, blockedReasons);
        if (blockedReasons.Count > 0)
        {
            return CreateResult(
                GCQuickStartScriptSetupStatus.Blocked,
                false,
                "Quick-start script generation is blocked.",
                createdAssetPaths,
                reusedAssetPaths,
                blockedReasons
            );
        }

        EnsureScriptAsset(spec.gameScriptAssetPath, spec.gameTypeName, spec.BuildGameScriptSource(), createdAssetPaths, reusedAssetPaths, blockedReasons);
        EnsureScriptAsset(spec.playerScriptAssetPath, spec.playerTypeName, spec.BuildPlayerScriptSource(), createdAssetPaths, reusedAssetPaths, blockedReasons);
        if (blockedReasons.Count > 0)
        {
            if (createdAssetPaths.Count > 0)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }

            return CreateResult(
                GCQuickStartScriptSetupStatus.Blocked,
                createdAssetPaths.Count > 0,
                "Quick-start script generation is blocked.",
                createdAssetPaths,
                reusedAssetPaths,
                blockedReasons
            );
        }

        if (createdAssetPaths.Count > 0)
        {
            PersistPendingSetup(intent, action);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            StartPendingSetupPolling();

            return CreateResult(
                GCQuickStartScriptSetupStatus.PendingCompilation,
                true,
                "Created missing quick-start scripts and queued setup continuation after Unity compiles them.",
                createdAssetPaths,
                reusedAssetPaths,
                blockedReasons
            );
        }

        var context = CreateContinuationContext(intent, action, false);
        if (!context.HasRequiredTypes)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                PersistPendingSetup(intent, action);
                StartPendingSetupPolling();

                return CreateResult(
                    GCQuickStartScriptSetupStatus.PendingCompilation,
                    false,
                    "Quick-start scripts already exist; waiting for Unity to compile their types.",
                    createdAssetPaths,
                    reusedAssetPaths,
                    blockedReasons
                );
            }

            blockedReasons.Add("Quick-start script assets already exist, but compiled types " + spec.gameTypeName + " and " + spec.playerTypeName + " are not both available. Existing scripts were left untouched.");
            return CreateResult(
                GCQuickStartScriptSetupStatus.Blocked,
                false,
                "Quick-start setup cannot continue until the existing script assets compile with the expected type names.",
                createdAssetPaths,
                reusedAssetPaths,
                blockedReasons
            );
        }

        if (dispatchWhenReady)
        {
            DispatchScriptsReady(context);
        }

        return CreateResult(
            GCQuickStartScriptSetupStatus.Ready,
            false,
            "Quick-start scripts already exist and compiled types are available.",
            createdAssetPaths,
            reusedAssetPaths,
            blockedReasons
        );
    }

    internal static GCQuickStartScriptSetupSpec GetScriptSetupSpec(
        GCQuickStartSetupIntent intent,
        GCQuickStartSetupAction action
    )
    {
        return intent == GCQuickStartSetupIntent.ActiveScene &&
               (action == GCQuickStartSetupAction.ActiveSceneMissingPieces ||
                action == GCQuickStartSetupAction.ActiveScenePlayerPrefab ||
                action == GCQuickStartSetupAction.ActiveSceneGameListener)
            ? ActiveSceneGameScriptSetupSpec
            : QuickStartScriptSetupSpec;
    }

    internal static bool CanReplaceActiveSceneGeneratedPlayerPrefab(
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab
    )
    {
        return IsActiveSceneGeneratedGameListener(listener) &&
               IsLegacyQuickStartPlayerPrefab(playerPrefab);
    }

    internal static bool IsActiveSceneGeneratedPlayerPrefabCompatible(
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab,
        out string message
    )
    {
        message = null;
        if (!IsActiveSceneGeneratedGameListener(listener))
        {
            return true;
        }

        var prefabObject = playerPrefab as GameObject;
        if (prefabObject == null)
        {
            message = "The active-scene Game script uses " + ActiveScenePlayerTypeName + ", but the assigned player prefab could not be inspected.";
            return false;
        }

        if (FindComponentByTypeName(prefabObject, ActiveScenePlayerTypeName, typeof(GCPlayer)) != null)
        {
            return true;
        }

        if (IsLegacyQuickStartPlayerPrefab(playerPrefab))
        {
            message = "The active-scene Game script uses " + ActiveScenePlayerTypeName + ", but the assigned player prefab is a legacy " + LegacyQuickStartPlayerTypeName + " prefab. Use Wire Player Prefab to replace it with " + ActiveScenePlayerPrefabAssetPath + ".";
            return false;
        }

        message = "The active-scene Game script uses " + ActiveScenePlayerTypeName + ", but the assigned player prefab root does not have " + ActiveScenePlayerTypeName + ". Assign a compatible player prefab manually.";
        return false;
    }

    private static bool IsActiveSceneGeneratedGameListener(UnityEngine.Object listener)
    {
        var listenerObject = listener as GameObject;
        return listenerObject != null &&
               FindComponentByTypeName(listenerObject, ActiveSceneGameTypeName, typeof(MonoBehaviour)) != null;
    }

    private static bool IsLegacyQuickStartPlayerPrefab(UnityEngine.Object playerPrefab)
    {
        var assetPath = AssetDatabase.GetAssetPath(playerPrefab);
        return string.Equals(assetPath, LegacyQuickStartPlayerPrefabAssetPath, StringComparison.Ordinal) ||
               string.Equals(assetPath, LegacyPlayerPrefabAssetPath, StringComparison.Ordinal);
    }

    private static Component FindComponentByTypeName(
        GameObject gameObject,
        string typeName,
        Type requiredBaseType
    )
    {
        if (gameObject == null)
        {
            return null;
        }

        var components = gameObject.GetComponents<Component>();
        for (var index = 0; index < components.Length; index++)
        {
            var component = components[index];
            if (component == null)
            {
                continue;
            }

            var componentType = component.GetType();
            if (componentType.Name == typeName &&
                requiredBaseType.IsAssignableFrom(componentType))
            {
                return component;
            }
        }

        return null;
    }

    private static void EnsureScriptFolders(
        GCQuickStartScriptSetupSpec spec,
        List<string> blockedReasons
    )
    {
        if (spec == null || !spec.requiresGeneratedScriptFolder)
        {
            return;
        }

        EnsureProjectFolder(ProjectFolderAssetPath, "Assets", "GamingCouch", blockedReasons);
        EnsureProjectFolder(
            spec.scriptFolderAssetPath,
            ProjectFolderAssetPath,
            GetAssetPathName(spec.scriptFolderAssetPath),
            blockedReasons
        );
    }

    internal static bool HasPendingSetup()
    {
        return SessionState.GetBool(PendingSetupSessionKey, false);
    }

    internal static GCQuickStartActiveSceneSetupResult EnsureActiveSceneQuickStartSetup()
    {
        var details = new List<string>();
        var gamingCouchResult = GamingCouchSceneWiring.EnsureActiveSceneGamingCouch();
        details.Add(gamingCouchResult.message);

        var changed = gamingCouchResult.changed;
        var buildSettingsResult = EnsureActiveSceneFirstBuildSettingsScene(details);
        changed |= buildSettingsResult.changed;
        var gameViewResult = EnsureGameView16By9IfSafe(details);
        changed |= gameViewResult.changed;

        if (gamingCouchResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                changed,
                "Active-scene quick-start setup is blocked.",
                details
            );
        }

        if (buildSettingsResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                changed,
                buildSettingsResult.message,
                details
            );
        }

        if (gameViewResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                changed,
                gameViewResult.message,
                details
            );
        }

        if (IsActiveSceneQuickStartWiringReady(gamingCouchResult.gamingCouch))
        {
            details.Add("The GamingCouch Game script reference already contains a serialized reference.");
            details.Add("The GamingCouch player prefab reference already contains a serialized reference.");
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Ready,
                changed,
                changed
                    ? "Active-scene quick-start setup completed."
                    : "Active-scene quick-start setup was already complete; existing scene references were reused.",
                details
            );
        }

        var scriptResult = EnsureQuickStartScripts(
            GCQuickStartSetupIntent.ActiveScene,
            GCQuickStartSetupAction.ActiveSceneMissingPieces,
            false
        );

        AddScriptResultDetails(scriptResult, details);
        if (scriptResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                changed || scriptResult.changed,
                scriptResult.message,
                details
            );
        }

        if (scriptResult.IsPendingCompilation)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.PendingCompilation,
                changed || scriptResult.changed,
                scriptResult.message,
                details
            );
        }

        var context = CreateContinuationContext(
            GCQuickStartSetupIntent.ActiveScene,
            GCQuickStartSetupAction.ActiveSceneMissingPieces,
            false
        );

        changed |= scriptResult.changed;
        var listenerResult = EnsureQuickStartGameListenerReference(context, gamingCouchResult.gamingCouch, details);
        changed |= listenerResult.changed;
        if (listenerResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                changed,
                listenerResult.message,
                details
            );
        }

        var prefabResult = EnsureQuickStartPlayerPrefabReference(context, gamingCouchResult.gamingCouch, details);
        changed |= prefabResult.changed;
        if (prefabResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                changed,
                prefabResult.message,
                details
            );
        }

        return CreateActiveSceneResult(
            GCQuickStartActiveSceneSetupStatus.Ready,
            changed,
            changed
                ? "Active-scene quick-start setup completed."
                : "Active-scene quick-start setup was already complete; existing assets and references were reused.",
            details
        );
    }

    internal static GCQuickStartActiveSceneSetupResult EnsureActiveSceneQuickStartPlayerPrefabReference()
    {
        var details = new List<string>();
        var gamingCouch = GetSingleActiveSceneGamingCouch(details);
        if (gamingCouch == null)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                false,
                "Quick-start player prefab setup is blocked.",
                details
            );
        }

        var listener = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName);
        var playerPrefab = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName);
        string playerPrefabCompatibilityMessage;
        if (GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName) &&
            !IsActiveSceneGeneratedPlayerPrefabCompatible(listener, playerPrefab, out playerPrefabCompatibilityMessage) &&
            !CanReplaceActiveSceneGeneratedPlayerPrefab(listener, playerPrefab))
        {
            details.Add(playerPrefabCompatibilityMessage);
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                false,
                "Quick-start player prefab setup is blocked.",
                details
            );
        }

        if (GamingCouchSceneWiring.HasObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName) &&
            !CanReplaceActiveSceneGeneratedPlayerPrefab(listener, playerPrefab))
        {
            details.Add("The GamingCouch player prefab reference already contains a serialized reference.");
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Ready,
                false,
                "Player prefab setup was already complete; existing reference was reused.",
                details
            );
        }

        var scriptResult = EnsureQuickStartScripts(
            GCQuickStartSetupIntent.ActiveScene,
            GCQuickStartSetupAction.ActiveScenePlayerPrefab,
            false
        );

        return ContinueActiveSceneObjectReferenceSetup(
            scriptResult,
            GCQuickStartSetupAction.ActiveScenePlayerPrefab,
            gamingCouch,
            details
        );
    }

    internal static GCQuickStartActiveSceneSetupResult EnsureActiveSceneQuickStartGameListenerReference()
    {
        var details = new List<string>();
        var gamingCouch = GetSingleActiveSceneGamingCouch(details);
        if (gamingCouch == null)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                false,
                "Game script setup is blocked.",
                details
            );
        }

        var listenerReferenceState = GamingCouchSceneWiring.GetObjectReferenceState(
            gamingCouch,
            GamingCouchSceneWiring.ListenerPropertyName
        );
        if (listenerReferenceState == GamingCouchObjectReferenceState.Assigned)
        {
            details.Add("The GamingCouch Game script reference already contains a serialized reference.");
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Ready,
                false,
                "Game script setup was already complete; existing reference was reused.",
                details
            );
        }

        if (listenerReferenceState == GamingCouchObjectReferenceState.Missing)
        {
            details.Add("The GamingCouch listener field points to a missing GameObject and will be replaced.");
        }

        var scriptResult = EnsureQuickStartScripts(
            GCQuickStartSetupIntent.ActiveScene,
            GCQuickStartSetupAction.ActiveSceneGameListener,
            false
        );

        return ContinueActiveSceneObjectReferenceSetup(
            scriptResult,
            GCQuickStartSetupAction.ActiveSceneGameListener,
            gamingCouch,
            details
        );
    }

    internal static GCQuickStartSceneSetupResult CreateOrOpenQuickStartScene()
    {
        lastQuickStartSceneSetupResult = null;
        var scriptResult = EnsureQuickStartScripts(GCQuickStartSetupIntent.QuickStartScene);
        if (scriptResult.IsBlocked)
        {
            return CreateSceneResult(
                GCQuickStartSceneSetupStatus.Blocked,
                scriptResult.changed,
                default(Scene),
                QuickStartSceneAssetPath,
                scriptResult.message,
                new List<string>(scriptResult.blockedReasons)
            );
        }

        if (scriptResult.IsPendingCompilation)
        {
            return CreateSceneResult(
                GCQuickStartSceneSetupStatus.PendingCompilation,
                scriptResult.changed,
                default(Scene),
                QuickStartSceneAssetPath,
                scriptResult.message,
                new List<string>(scriptResult.blockedReasons)
            );
        }

        if (lastQuickStartSceneSetupResult != null)
        {
            return lastQuickStartSceneSetupResult;
        }

        return CreateSceneResult(
            GCQuickStartSceneSetupStatus.Blocked,
            scriptResult.changed,
            default(Scene),
            QuickStartSceneAssetPath,
            "Quick-start scene setup did not run after scripts became ready.",
            new List<string> { "The quick-start scene continuation handler was not available." }
        );
    }

    private static GCQuickStartScriptSetupResult CreateResult(
        GCQuickStartScriptSetupStatus status,
        bool changed,
        string message,
        List<string> createdAssetPaths,
        List<string> reusedAssetPaths,
        List<string> blockedReasons
    )
    {
        return new GCQuickStartScriptSetupResult(
            status,
            changed,
            message,
            createdAssetPaths.ToArray(),
            reusedAssetPaths.ToArray(),
            blockedReasons.ToArray()
        );
    }

    private static GCQuickStartActiveSceneSetupResult CreateActiveSceneResult(
        GCQuickStartActiveSceneSetupStatus status,
        bool changed,
        string message,
        List<string> details
    )
    {
        return new GCQuickStartActiveSceneSetupResult(status, changed, message, details.ToArray());
    }

    private static GCQuickStartActiveSceneSetupResult ContinueActiveSceneObjectReferenceSetup(
        GCQuickStartScriptSetupResult scriptResult,
        GCQuickStartSetupAction action,
        GamingCouch gamingCouch,
        List<string> details
    )
    {
        AddScriptResultDetails(scriptResult, details);
        if (scriptResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                scriptResult.changed,
                scriptResult.message,
                details
            );
        }

        if (scriptResult.IsPendingCompilation)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.PendingCompilation,
                scriptResult.changed,
                scriptResult.message,
                details
            );
        }

        var context = CreateContinuationContext(GCQuickStartSetupIntent.ActiveScene, action, false);
        if (action == GCQuickStartSetupAction.ActiveScenePlayerPrefab)
        {
            return EnsureQuickStartPlayerPrefabReference(context, gamingCouch, details);
        }

        if (action == GCQuickStartSetupAction.ActiveSceneGameListener)
        {
            return EnsureQuickStartGameListenerReference(context, gamingCouch, details);
        }

        details.Add("Unknown active-scene quick-start setup action: " + action + ".");
        return CreateActiveSceneResult(
            GCQuickStartActiveSceneSetupStatus.Blocked,
            scriptResult.changed,
            "Active-scene quick-start setup is blocked.",
            details
        );
    }

    private static GCQuickStartActiveSceneSetupResult EnsureQuickStartPlayerPrefabReference(
        GCQuickStartSetupContinuationContext context,
        GamingCouch gamingCouch,
        List<string> details
    )
    {
        var existingListener = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName);
        var existingPlayerPrefab = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName);
        string playerPrefabCompatibilityMessage;
        if (GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName) &&
            !IsActiveSceneGeneratedPlayerPrefabCompatible(existingListener, existingPlayerPrefab, out playerPrefabCompatibilityMessage) &&
            !CanReplaceActiveSceneGeneratedPlayerPrefab(existingListener, existingPlayerPrefab))
        {
            details.Add(playerPrefabCompatibilityMessage);
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                false,
                "Quick-start player prefab setup is blocked.",
                details
            );
        }

        var prefabResult = EnsureQuickStartPlayerPrefab(context);
        details.Add(prefabResult.message);
        AddDetails(prefabResult.blockedReasons, details);
        if (prefabResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                prefabResult.changed,
                prefabResult.message,
                details
            );
        }

        var assignResult = AssignOrReplaceActiveScenePlayerPrefab(gamingCouch, prefabResult.prefab);
        details.Add(assignResult.message);
        if (assignResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                prefabResult.changed || assignResult.changed,
                assignResult.message,
                details
            );
        }

        return CreateActiveSceneResult(
            GCQuickStartActiveSceneSetupStatus.Ready,
            prefabResult.changed || assignResult.changed,
            prefabResult.changed || assignResult.changed
                ? "Quick-start player prefab reference is ready."
                : "Quick-start player prefab reference was already ready; existing assets and references were reused.",
            details
        );
    }

    private static GCQuickStartActiveSceneSetupResult EnsureQuickStartGameListenerReference(
        GCQuickStartSetupContinuationContext context,
        GamingCouch gamingCouch,
        List<string> details
    )
    {
        var listenerResult = EnsureQuickStartGameListener(context, gamingCouch);
        details.Add(listenerResult.message);
        AddDetails(listenerResult.blockedReasons, details);
        if (listenerResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                listenerResult.changed,
                listenerResult.message,
                details
            );
        }

        if (listenerResult.listenerObject == null)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Ready,
                listenerResult.changed,
                listenerResult.message,
                details
            );
        }

        var assignResult = GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listenerResult.listenerObject);
        details.Add(assignResult.message);
        if (assignResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCQuickStartActiveSceneSetupStatus.Blocked,
                listenerResult.changed || assignResult.changed,
                assignResult.message,
                details
            );
        }

        return CreateActiveSceneResult(
            GCQuickStartActiveSceneSetupStatus.Ready,
            listenerResult.changed || assignResult.changed,
            listenerResult.changed || assignResult.changed
                ? "Game script is ready."
                : "Game script was already ready; existing assets and references were reused.",
            details
        );
    }

    private static GamingCouch GetSingleActiveSceneGamingCouch(List<string> details)
    {
        var gamingCouches = GamingCouchSceneWiring.FindActiveSceneGamingCouches();
        if (gamingCouches.Length == 0)
        {
            details.Add("Create or reuse a GamingCouch object before wiring this reference.");
            return null;
        }

        if (gamingCouches.Length > 1)
        {
            details.Add("The active scene contains multiple GamingCouch components. Remove duplicates manually before running setup.");
            return null;
        }

        details.Add("Reused the active scene GamingCouch object.");
        return gamingCouches[0];
    }

    private static bool IsActiveSceneQuickStartWiringReady(GamingCouch gamingCouch)
    {
        if (!GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName) ||
            !GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName))
        {
            return false;
        }

        string playerPrefabCompatibilityMessage;
        return IsActiveSceneGeneratedPlayerPrefabCompatible(
            GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName),
            GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName),
            out playerPrefabCompatibilityMessage
        );
    }

    private static GCActiveSceneBuildSettingsSetupResult EnsureActiveSceneFirstBuildSettingsScene(
        List<string> details
    )
    {
        var readiness = GamingCouchBuildSettingsReadiness.InspectActiveScene();
        if (readiness == null || readiness.IsReady)
        {
            return new GCActiveSceneBuildSettingsSetupResult(
                GCActiveSceneBuildSettingsSetupStatus.Ready,
                false,
                "The active scene is already the first enabled Build Settings scene.",
                new string[0]
            );
        }

        if (!readiness.CanSetFirst)
        {
            details.Add(readiness.message);
            return new GCActiveSceneBuildSettingsSetupResult(
                GCActiveSceneBuildSettingsSetupStatus.Blocked,
                false,
                "Build Settings setup is blocked.",
                new[] { readiness.message }
            );
        }

        var result = GamingCouchBuildSettingsReadiness.EnsureActiveSceneFirstEnabled();
        details.Add(result.message);
        AddDetails(result.details, details);
        return result;
    }

    private static GCGameViewAspectSetupResult EnsureGameView16By9IfSafe(List<string> details)
    {
        var readiness = GamingCouchGameViewAspect.Inspect();
        if (readiness == null || readiness.IsReady || !readiness.HasSafeSelectionAction)
        {
            return new GCGameViewAspectSetupResult(
                GCGameViewAspectSetupStatus.Ready,
                false,
                "No safe Game View 16:9 setup action is available.",
                new string[0]
            );
        }

        var result = GamingCouchGameViewAspect.SelectExisting16By9Size();
        details.Add(result.message);
        AddDetails(result.details, details);
        return result;
    }

    private static void AddScriptResultDetails(GCQuickStartScriptSetupResult result, List<string> details)
    {
        if (result == null)
        {
            return;
        }

        details.Add(result.message);
        AddLabeledDetails("Created", result.createdAssetPaths, details);
        AddLabeledDetails("Reused", result.reusedAssetPaths, details);
        AddDetails(result.blockedReasons, details);
    }

    private static void AddLabeledDetails(string label, string[] values, List<string> details)
    {
        if (values == null)
        {
            return;
        }

        for (var index = 0; index < values.Length; index++)
        {
            if (!string.IsNullOrEmpty(values[index]))
            {
                details.Add(label + ": " + values[index]);
            }
        }
    }

    private static void AddDetails(string[] values, List<string> details)
    {
        if (values == null)
        {
            return;
        }

        for (var index = 0; index < values.Length; index++)
        {
            if (!string.IsNullOrEmpty(values[index]))
            {
                details.Add(values[index]);
            }
        }
    }

    internal static GCQuickStartPlayerPrefabSetupResult EnsureQuickStartPlayerPrefab(
        GCQuickStartSetupContinuationContext context
    )
    {
        var blockedReasons = new List<string>();

        if (context == null)
        {
            blockedReasons.Add("Quick-start player prefab generation requires a scripts-ready continuation context.");
            return CreatePlayerPrefabResult(
                GCQuickStartPlayerPrefabSetupStatus.Blocked,
                false,
                null,
                "Quick-start player prefab generation is blocked.",
                blockedReasons
            );
        }

        if (context.playerType == null)
        {
            blockedReasons.Add("Quick-start player prefab generation requires the compiled " + context.playerTypeName + " type.");
            return CreatePlayerPrefabResult(
                GCQuickStartPlayerPrefabSetupStatus.Blocked,
                false,
                null,
                "Quick-start player prefab generation is blocked.",
                blockedReasons
            );
        }

        if (context.playerType.Name != context.playerTypeName)
        {
            blockedReasons.Add("Quick-start player prefab generation requires " + context.playerTypeName + ", but the continuation context provided " + context.playerType.FullName + ".");
            return CreatePlayerPrefabResult(
                GCQuickStartPlayerPrefabSetupStatus.Blocked,
                false,
                null,
                "Quick-start player prefab generation is blocked.",
                blockedReasons
            );
        }

        if (!typeof(GCPlayer).IsAssignableFrom(context.playerType))
        {
            blockedReasons.Add("Compiled type " + context.playerType.FullName + " does not inherit from GCPlayer.");
            return CreatePlayerPrefabResult(
                GCQuickStartPlayerPrefabSetupStatus.Blocked,
                false,
                null,
                "Quick-start player prefab generation is blocked.",
                blockedReasons
            );
        }

        if (!AssetDatabase.IsValidFolder(context.quickStartFolderAssetPath))
        {
            blockedReasons.Add("Quick-start folder " + context.quickStartFolderAssetPath + " is missing. Run quick-start script setup first.");
            return CreatePlayerPrefabResult(
                GCQuickStartPlayerPrefabSetupStatus.Blocked,
                false,
                null,
                "Quick-start player prefab generation is blocked.",
                blockedReasons
            );
        }

        var existingPrefab = LoadExistingPlayerPrefab(context.playerPrefabAssetPath, blockedReasons);
        if (blockedReasons.Count > 0)
        {
            return CreatePlayerPrefabResult(
                GCQuickStartPlayerPrefabSetupStatus.Blocked,
                false,
                null,
                "Quick-start player prefab generation is blocked.",
                blockedReasons
            );
        }

        if (existingPrefab != null)
        {
            if (existingPrefab.GetComponent(context.playerType) == null)
            {
                blockedReasons.Add("Existing prefab " + context.playerPrefabAssetPath + " does not have " + context.playerTypeName + " on its root. Existing prefab assets are never overwritten.");
                return CreatePlayerPrefabResult(
                    GCQuickStartPlayerPrefabSetupStatus.Blocked,
                    false,
                    existingPrefab,
                    "Quick-start player prefab generation is blocked.",
                    blockedReasons
                );
            }

            return CreatePlayerPrefabResult(
                GCQuickStartPlayerPrefabSetupStatus.Ready,
                false,
                existingPrefab,
                "Reused existing quick-start player prefab.",
                blockedReasons
            );
        }

        var createdPrefab = CreatePlayerPrefab(context, blockedReasons);
        if (blockedReasons.Count > 0)
        {
            return CreatePlayerPrefabResult(
                GCQuickStartPlayerPrefabSetupStatus.Blocked,
                false,
                null,
                "Quick-start player prefab generation is blocked.",
                blockedReasons
            );
        }

        return CreatePlayerPrefabResult(
            GCQuickStartPlayerPrefabSetupStatus.Ready,
            true,
            createdPrefab,
            "Created quick-start player prefab.",
            blockedReasons
        );
    }

    internal static GCQuickStartGameListenerSetupResult EnsureQuickStartGameListener(
        GCQuickStartSetupContinuationContext context,
        GamingCouch gamingCouch
    )
    {
        var blockedReasons = new List<string>();

        if (context == null)
        {
            blockedReasons.Add("Game script setup requires a scripts-ready continuation context.");
            return CreateGameListenerResult(
                GCQuickStartGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        if (context.gameType == null)
        {
            blockedReasons.Add("Game script setup requires the compiled " + context.gameTypeName + " type.");
            return CreateGameListenerResult(
                GCQuickStartGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        if (context.gameType.Name != context.gameTypeName)
        {
            blockedReasons.Add("Game script setup requires " + context.gameTypeName + ", but the continuation context provided " + context.gameType.FullName + ".");
            return CreateGameListenerResult(
                GCQuickStartGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        if (!typeof(MonoBehaviour).IsAssignableFrom(context.gameType))
        {
            blockedReasons.Add("Compiled type " + context.gameType.FullName + " does not inherit from MonoBehaviour.");
            return CreateGameListenerResult(
                GCQuickStartGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        if (!GCStartScreenGameScriptReceiverCompatibility.CanReceiveSetupAndPlay(context.gameType))
        {
            blockedReasons.Add("Compiled type " + context.gameType.FullName + " cannot receive both GamingCouchSetup(GCSetupOptions) and GamingCouchPlay(GCPlayOptions). Existing scripts were left untouched.");
            return CreateGameListenerResult(
                GCQuickStartGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        if (gamingCouch == null)
        {
            blockedReasons.Add("A GamingCouch object is required before creating the quick-start Game script object.");
            return CreateGameListenerResult(
                GCQuickStartGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        var listenerReferenceState = GamingCouchSceneWiring.GetObjectReferenceState(
            gamingCouch,
            GamingCouchSceneWiring.ListenerPropertyName
        );
        if (listenerReferenceState == GamingCouchObjectReferenceState.Assigned)
        {
            return CreateGameListenerResult(
                GCQuickStartGameListenerSetupStatus.Ready,
                false,
                null,
                "The GamingCouch Game script reference already contains a serialized reference.",
                blockedReasons
            );
        }

        if (!GamingCouchSceneWiring.HasObjectReferenceSlot(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName))
        {
            blockedReasons.Add("The GamingCouch Game script serialized field could not be found.");
            return CreateGameListenerResult(
                GCQuickStartGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        var scene = gamingCouch.gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            blockedReasons.Add("The GamingCouch object is not in a loaded scene.");
            return CreateGameListenerResult(
                GCQuickStartGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        var listenerObject = EnsureQuickStartGameListenerObject(context, scene, blockedReasons, out var changed);
        if (blockedReasons.Count > 0)
        {
            return CreateGameListenerResult(
                GCQuickStartGameListenerSetupStatus.Blocked,
                changed,
                listenerObject,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        return CreateGameListenerResult(
            GCQuickStartGameListenerSetupStatus.Ready,
            changed,
            listenerObject,
            changed ? "Created quick-start Game script object." : "Reused existing quick-start Game script object.",
            blockedReasons
        );
    }

    private static void EnsureProjectFolder(
        string assetPath,
        string parentFolderAssetPath,
        string folderName,
        List<string> blockedReasons
    )
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        var existingAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
        if (existingAsset != null)
        {
            blockedReasons.Add("Cannot create folder " + assetPath + " because a non-folder asset already exists at that path.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(parentFolderAssetPath))
        {
            blockedReasons.Add("Cannot create folder " + assetPath + " because parent folder " + parentFolderAssetPath + " is missing.");
            return;
        }

        var fullPath = AssetPathToFullPath(assetPath);
        if (File.Exists(fullPath))
        {
            blockedReasons.Add("Cannot create folder " + assetPath + " because a file exists at that path.");
            return;
        }

        if (Directory.Exists(fullPath))
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            blockedReasons.Add("Cannot create folder " + assetPath + " because a directory exists at that path but Unity did not import it as a valid asset folder.");
            return;
        }

        var createdFolderGuid = AssetDatabase.CreateFolder(parentFolderAssetPath, folderName);
        if (string.IsNullOrEmpty(createdFolderGuid))
        {
            blockedReasons.Add("Cannot create folder " + assetPath + " because Unity did not create the folder.");
            return;
        }

        var createdFolderAssetPath = AssetDatabase.GUIDToAssetPath(createdFolderGuid);
        if (!string.Equals(createdFolderAssetPath, assetPath, StringComparison.Ordinal))
        {
            blockedReasons.Add("Cannot create folder " + assetPath + " because Unity created " + createdFolderAssetPath + " instead.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(assetPath))
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        if (!AssetDatabase.IsValidFolder(assetPath))
        {
            blockedReasons.Add("Cannot create folder " + assetPath + " because Unity did not import it as a valid asset folder.");
        }
    }

    private static string GetAssetPathName(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return string.Empty;
        }

        var separatorIndex = assetPath.LastIndexOf('/');
        return separatorIndex >= 0 && separatorIndex + 1 < assetPath.Length
            ? assetPath.Substring(separatorIndex + 1)
            : assetPath;
    }

    private static void EnsureScriptAsset(
        string assetPath,
        string typeName,
        string source,
        List<string> createdAssetPaths,
        List<string> reusedAssetPaths,
        List<string> blockedReasons
    )
    {
        var fullPath = AssetPathToFullPath(assetPath);
        if (Directory.Exists(fullPath))
        {
            blockedReasons.Add("Cannot create script " + assetPath + " because a folder exists at that path.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath) != null)
        {
            reusedAssetPaths.Add(assetPath);
            return;
        }

        var existingAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
        if (existingAsset != null)
        {
            blockedReasons.Add("Cannot create script " + assetPath + " because a non-script asset already exists at that path.");
            return;
        }

        if (File.Exists(fullPath))
        {
            EnsureGeneratedAssetFileWithoutOverwrite(assetPath, source, createdAssetPaths, reusedAssetPaths, blockedReasons);
            return;
        }

        if (FindTypeByName(typeName) != null)
        {
            blockedReasons.Add("Cannot create " + assetPath + " because a compiled type named " + typeName + " already exists outside the generated script path.");
            return;
        }

        EnsureGeneratedAssetFileWithoutOverwrite(assetPath, source, createdAssetPaths, reusedAssetPaths, blockedReasons);
    }

    internal static void EnsureGeneratedAssetFileWithoutOverwrite(
        string assetPath,
        string source,
        List<string> createdAssetPaths,
        List<string> reusedAssetPaths,
        List<string> blockedReasons
    )
    {
        var fullPath = AssetPathToFullPath(assetPath);
        if (File.Exists(fullPath))
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            reusedAssetPaths.Add(assetPath);
            return;
        }

        try
        {
            using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Utf8WithoutBom))
            {
                writer.Write(source);
            }

            createdAssetPaths.Add(assetPath);
        }
        catch (IOException exception)
        {
            blockedReasons.Add("Could not create " + assetPath + " without overwriting existing content: " + exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            blockedReasons.Add("Could not create " + assetPath + ": " + exception.Message);
        }
    }

    private static GCQuickStartPlayerPrefabSetupResult CreatePlayerPrefabResult(
        GCQuickStartPlayerPrefabSetupStatus status,
        bool changed,
        GameObject prefab,
        string message,
        List<string> blockedReasons
    )
    {
        return new GCQuickStartPlayerPrefabSetupResult(
            status,
            changed,
            prefab,
            message,
            blockedReasons.ToArray()
        );
    }

    private static GCQuickStartGameListenerSetupResult CreateGameListenerResult(
        GCQuickStartGameListenerSetupStatus status,
        bool changed,
        GameObject listenerObject,
        string message,
        List<string> blockedReasons
    )
    {
        return new GCQuickStartGameListenerSetupResult(
            status,
            changed,
            listenerObject,
            message,
            blockedReasons.ToArray()
        );
    }

    private static GCQuickStartSceneSetupResult CreateSceneResult(
        GCQuickStartSceneSetupStatus status,
        bool changed,
        Scene scene,
        string sceneAssetPath,
        string message,
        List<string> blockedReasons
    )
    {
        return new GCQuickStartSceneSetupResult(
            status,
            changed,
            scene,
            sceneAssetPath,
            message,
            blockedReasons.ToArray()
        );
    }

    private static GCQuickStartSceneSetupResult EnsureQuickStartScene(
        GCQuickStartSetupContinuationContext context
    )
    {
        var blockedReasons = new List<string>();
        var changed = false;

        if (context == null)
        {
            blockedReasons.Add("Quick-start scene setup requires a scripts-ready continuation context.");
            return CreateSceneResult(
                GCQuickStartSceneSetupStatus.Blocked,
                false,
                default(Scene),
                QuickStartSceneAssetPath,
                "Quick-start scene setup is blocked.",
                blockedReasons
            );
        }

        var scene = OpenOrCreateQuickStartScene(blockedReasons, out var createdScene);
        changed |= createdScene;
        if (blockedReasons.Count > 0)
        {
            return CreateSceneResult(
                GCQuickStartSceneSetupStatus.Blocked,
                changed,
                scene,
                QuickStartSceneAssetPath,
                "Quick-start scene setup is blocked.",
                blockedReasons
            );
        }

        if (!EnsureQuickStartSceneIsActive(scene, blockedReasons))
        {
            return CreateSceneResult(
                GCQuickStartSceneSetupStatus.Blocked,
                changed,
                scene,
                QuickStartSceneAssetPath,
                "Quick-start scene setup is blocked.",
                blockedReasons
            );
        }

        var prefabResult = EnsureQuickStartPlayerPrefab(context);
        changed |= prefabResult.changed;
        if (prefabResult.IsBlocked)
        {
            blockedReasons.Add(prefabResult.message + " " + string.Join(" ", prefabResult.blockedReasons));
            return CreateSceneResult(
                GCQuickStartSceneSetupStatus.Blocked,
                changed,
                scene,
                QuickStartSceneAssetPath,
                "Quick-start scene setup is blocked.",
                blockedReasons
            );
        }

        if (createdScene)
        {
            CreateDefaultQuickStartSceneObjects(scene);
        }

        var gamingCouchResult = GamingCouchSceneWiring.EnsureActiveSceneGamingCouch();
        if (gamingCouchResult.IsBlocked)
        {
            blockedReasons.Add(gamingCouchResult.message);
        }
        else
        {
            changed |= gamingCouchResult.changed;
        }

        if (gamingCouchResult.gamingCouch != null)
        {
            var listenerResult = EnsureQuickStartGameListener(context, gamingCouchResult.gamingCouch);
            if (listenerResult.IsBlocked)
            {
                blockedReasons.Add(listenerResult.message + " " + string.Join(" ", listenerResult.blockedReasons));
            }
            else
            {
                changed |= listenerResult.changed;
                if (listenerResult.listenerObject != null)
                {
                    var assignListenerResult = GamingCouchSceneWiring.AssignListenerIfMissing(
                        gamingCouchResult.gamingCouch,
                        listenerResult.listenerObject
                    );
                    if (assignListenerResult.IsBlocked)
                    {
                        blockedReasons.Add(assignListenerResult.message);
                    }
                    else
                    {
                        changed |= assignListenerResult.changed;
                    }
                }
            }
        }

        if (gamingCouchResult.gamingCouch != null)
        {
            var assignPlayerResult = AssignOrReplaceActiveScenePlayerPrefab(
                gamingCouchResult.gamingCouch,
                prefabResult.prefab
            );
            if (assignPlayerResult.IsBlocked)
            {
                blockedReasons.Add(assignPlayerResult.message);
            }
            else
            {
                changed |= assignPlayerResult.changed;
            }
        }

        if (blockedReasons.Count > 0)
        {
            return CreateSceneResult(
                GCQuickStartSceneSetupStatus.Blocked,
                changed,
                scene,
                QuickStartSceneAssetPath,
                "Quick-start scene setup is blocked.",
                blockedReasons
            );
        }

        if (!EditorSceneManager.SaveScene(scene, QuickStartSceneAssetPath))
        {
            blockedReasons.Add("Unity did not save the quick-start scene at " + QuickStartSceneAssetPath + ".");
            return CreateSceneResult(
                GCQuickStartSceneSetupStatus.Blocked,
                changed,
                scene,
                QuickStartSceneAssetPath,
                "Quick-start scene setup is blocked.",
                blockedReasons
            );
        }

        changed |= AddQuickStartSceneToBuildSettings();

        return CreateSceneResult(
            GCQuickStartSceneSetupStatus.Ready,
            changed,
            scene,
            QuickStartSceneAssetPath,
            "Quick-start scene is ready at " + QuickStartSceneAssetPath + ".",
            blockedReasons
        );
    }

    private static Scene OpenOrCreateQuickStartScene(List<string> blockedReasons, out bool createdScene)
    {
        createdScene = false;

        var fullPath = AssetPathToFullPath(QuickStartSceneAssetPath);
        if (Directory.Exists(fullPath))
        {
            blockedReasons.Add("Cannot create quick-start scene " + QuickStartSceneAssetPath + " because a folder exists at that path.");
            return default(Scene);
        }

        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() &&
            activeScene.isLoaded &&
            string.Equals(activeScene.path, QuickStartSceneAssetPath, StringComparison.Ordinal))
        {
            return activeScene;
        }

        var existingSceneAsset = LoadExistingQuickStartSceneAsset(blockedReasons);
        if (blockedReasons.Count > 0)
        {
            return default(Scene);
        }

        if (!PromptToSaveModifiedScenesBeforeReplacingActiveScene())
        {
            blockedReasons.Add("Quick-start scene setup was cancelled before replacing the active scene.");
            return default(Scene);
        }

        if (existingSceneAsset != null)
        {
            var openedScene = EditorSceneManager.OpenScene(QuickStartSceneAssetPath, OpenSceneMode.Single);
            if (!openedScene.IsValid() || !openedScene.isLoaded)
            {
                blockedReasons.Add("Unity did not open the existing quick-start scene at " + QuickStartSceneAssetPath + ".");
            }

            return openedScene;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            blockedReasons.Add("Unity did not create a new quick-start scene.");
            return scene;
        }

        createdScene = true;
        return scene;
    }

    private static bool EnsureQuickStartSceneIsActive(Scene scene, List<string> blockedReasons)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            blockedReasons.Add("No loaded quick-start scene is available for scene wiring.");
            return false;
        }

        var activeScene = SceneManager.GetActiveScene();
        if (activeScene == scene)
        {
            return true;
        }

        if (SceneManager.SetActiveScene(scene))
        {
            return true;
        }

        blockedReasons.Add("Unity did not make " + QuickStartSceneAssetPath + " the active scene for setup.");
        return false;
    }

    private static SceneAsset LoadExistingQuickStartSceneAsset(List<string> blockedReasons)
    {
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(QuickStartSceneAssetPath);
        if (sceneAsset != null)
        {
            return sceneAsset;
        }

        var existingAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(QuickStartSceneAssetPath);
        if (existingAsset != null)
        {
            blockedReasons.Add("Cannot create quick-start scene " + QuickStartSceneAssetPath + " because a non-scene asset already exists at that path.");
            return null;
        }

        var fullPath = AssetPathToFullPath(QuickStartSceneAssetPath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        AssetDatabase.ImportAsset(QuickStartSceneAssetPath, ImportAssetOptions.ForceSynchronousImport);
        sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(QuickStartSceneAssetPath);
        if (sceneAsset == null)
        {
            blockedReasons.Add("Cannot open quick-start scene " + QuickStartSceneAssetPath + " because a file exists there but Unity did not import it as a scene asset.");
        }

        return sceneAsset;
    }

    private static bool PromptToSaveModifiedScenesBeforeReplacingActiveScene()
    {
        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static void CreateDefaultQuickStartSceneObjects(Scene scene)
    {
        var cameraObject = new GameObject("Main Camera");
        Undo.RegisterCreatedObjectUndo(cameraObject, CreateCameraUndoName);
        if (cameraObject.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
        }

        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0.0f, 1.5f, -6.0f);
        cameraObject.transform.rotation = Quaternion.Euler(12.0f, 0.0f, 0.0f);
        cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        GamingCouchSceneWiring.MarkSceneDirty(cameraObject);

        var lightObject = new GameObject("Directional Light");
        Undo.RegisterCreatedObjectUndo(lightObject, CreateLightUndoName);
        if (lightObject.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(lightObject, scene);
        }

        lightObject.transform.rotation = Quaternion.Euler(50.0f, -30.0f, 0.0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.0f;
        GamingCouchSceneWiring.MarkSceneDirty(lightObject);
    }

    private static bool AddQuickStartSceneToBuildSettings()
    {
        return AddSceneToBuildSettingsIfMissing(QuickStartSceneAssetPath);
    }

    internal static bool AddSceneToBuildSettingsIfMissing(string sceneAssetPath)
    {
        var scenes = EditorBuildSettings.scenes;
        for (var index = 0; index < scenes.Length; index++)
        {
            if (scenes[index] != null &&
                string.Equals(scenes[index].path, sceneAssetPath, StringComparison.Ordinal))
            {
                return false;
            }
        }

        var nextScenes = new EditorBuildSettingsScene[scenes.Length + 1];
        for (var index = 0; index < scenes.Length; index++)
        {
            nextScenes[index] = scenes[index];
        }

        nextScenes[nextScenes.Length - 1] = new EditorBuildSettingsScene(sceneAssetPath, true);
        EditorBuildSettings.scenes = nextScenes;
        return true;
    }

    private static GameObject EnsureQuickStartGameListenerObject(
        GCQuickStartSetupContinuationContext context,
        Scene scene,
        List<string> blockedReasons,
        out bool changed
    )
    {
        changed = false;

        var existingComponents = FindComponentsInScene(scene, context.gameType);
        if (existingComponents.Length > 1)
        {
            blockedReasons.Add("The scene contains multiple " + context.gameTypeName + " components. Assign the GamingCouch listener manually or remove duplicates before rerunning quick-start setup.");
            return null;
        }

        if (existingComponents.Length == 1)
        {
            var componentObject = existingComponents[0].gameObject;
            if (string.Equals(componentObject.name, context.listenerObjectName, StringComparison.Ordinal))
            {
                return componentObject;
            }

            blockedReasons.Add("The scene already contains a " + context.gameTypeName + " component on " + componentObject.name + ". Assign it manually or move it to a scene object named " + context.listenerObjectName + " before rerunning setup.");
            return null;
        }

        var existingObject = FindRootGameObjectInScene(scene, context.listenerObjectName);
        if (existingObject != null)
        {
            try
            {
                Undo.SetCurrentGroupName(AddGameListenerComponentUndoName);
                var listenerComponent = Undo.AddComponent(existingObject, context.gameType);
                if (listenerComponent == null)
                {
                    blockedReasons.Add("Unity did not add " + context.gameTypeName + " to existing scene object " + context.listenerObjectName + ".");
                    return existingObject;
                }

                EditorUtility.SetDirty(listenerComponent);
                EditorUtility.SetDirty(existingObject);
                GamingCouchSceneWiring.MarkSceneDirty(existingObject);
                changed = true;
                return existingObject;
            }
            catch (Exception exception)
            {
                blockedReasons.Add("Could not add " + context.gameTypeName + " to existing scene object " + context.listenerObjectName + ": " + exception.Message);
                return existingObject;
            }
        }

        GameObject listenerObject = null;
        try
        {
            Undo.SetCurrentGroupName(CreateGameListenerUndoName);
            listenerObject = new GameObject(context.listenerObjectName);
            if (listenerObject.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(listenerObject, scene);
            }

            var listenerComponent = listenerObject.AddComponent(context.gameType);
            if (listenerComponent == null)
            {
                blockedReasons.Add("Unity did not add " + context.gameTypeName + " to the new quick-start Game script object.");
                UnityEngine.Object.DestroyImmediate(listenerObject);
                return null;
            }

            Undo.RegisterCreatedObjectUndo(listenerObject, CreateGameListenerUndoName);
            EditorUtility.SetDirty(listenerComponent);
            EditorUtility.SetDirty(listenerObject);
            GamingCouchSceneWiring.MarkSceneDirty(listenerObject);
            changed = true;
            return listenerObject;
        }
        catch (Exception exception)
        {
            blockedReasons.Add("Could not create quick-start Game script object " + context.listenerObjectName + ": " + exception.Message);
            if (listenerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(listenerObject);
            }

            return null;
        }
    }

    private static Component[] FindComponentsInScene(Scene scene, Type componentType)
    {
        var foundComponents = new List<Component>();
        var roots = scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var root = roots[rootIndex];
            if (root == null)
            {
                continue;
            }

            var components = root.GetComponentsInChildren(componentType, true);
            for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                if (components[componentIndex] != null)
                {
                    foundComponents.Add(components[componentIndex]);
                }
            }
        }

        return foundComponents.ToArray();
    }

    private static GameObject FindRootGameObjectInScene(Scene scene, string objectName)
    {
        var roots = scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var root = roots[rootIndex];
            if (root != null && root.name == objectName)
            {
                return root;
            }
        }

        return null;
    }

    private static GameObject LoadExistingPlayerPrefab(string playerPrefabAssetPath, List<string> blockedReasons)
    {
        var fullPath = AssetPathToFullPath(playerPrefabAssetPath);
        if (Directory.Exists(fullPath))
        {
            blockedReasons.Add("Cannot create player prefab " + playerPrefabAssetPath + " because a folder exists at that path.");
            return null;
        }

        var existingAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(playerPrefabAssetPath);
        if (existingAsset == null && File.Exists(fullPath))
        {
            AssetDatabase.ImportAsset(playerPrefabAssetPath, ImportAssetOptions.ForceSynchronousImport);
            existingAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(playerPrefabAssetPath);
            if (existingAsset == null)
            {
                blockedReasons.Add("Cannot create player prefab " + playerPrefabAssetPath + " because a file exists at that path but Unity did not import it as a prefab asset.");
                return null;
            }
        }

        if (existingAsset == null)
        {
            return null;
        }

        var prefab = existingAsset as GameObject;
        if (prefab == null)
        {
            blockedReasons.Add("Cannot create player prefab " + playerPrefabAssetPath + " because a non-prefab asset already exists at that path.");
            return null;
        }

        var prefabAssetType = PrefabUtility.GetPrefabAssetType(prefab);
        if (prefabAssetType == PrefabAssetType.NotAPrefab)
        {
            blockedReasons.Add("Cannot create player prefab " + playerPrefabAssetPath + " because a non-prefab asset already exists at that path.");
            return null;
        }

        if (prefabAssetType != PrefabAssetType.Regular && prefabAssetType != PrefabAssetType.Variant)
        {
            blockedReasons.Add("Cannot create player prefab " + playerPrefabAssetPath + " because the existing asset imports as a " + prefabAssetType + " prefab asset. Existing prefab assets are never overwritten.");
            return null;
        }

        return prefab;
    }

    private static GameObject CreatePlayerPrefab(
        GCQuickStartSetupContinuationContext context,
        List<string> blockedReasons
    )
    {
        var root = new GameObject(context.playerTypeName);
        GameObject visual = null;
        try
        {
            var playerComponent = root.AddComponent(context.playerType);
            visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = PlayerVisualName;
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0.0f, 0.75f, 0.0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(0.7f, 1.0f, 0.7f);

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                var serializedPlayer = new SerializedObject(playerComponent);
                serializedPlayer.Update();
                var colorRendererProperty = serializedPlayer.FindProperty("colorRenderer");
                if (colorRendererProperty != null &&
                    colorRendererProperty.propertyType == SerializedPropertyType.ObjectReference)
                {
                    colorRendererProperty.objectReferenceValue = renderer;
                    serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, context.playerPrefabAssetPath, out var success);
            if (!success || prefab == null)
            {
                blockedReasons.Add("Unity did not save the quick-start player prefab at " + context.playerPrefabAssetPath + ".");
                return null;
            }

            return prefab;
        }
        catch (Exception exception)
        {
            blockedReasons.Add("Could not create quick-start player prefab " + context.playerPrefabAssetPath + ": " + exception.Message);
            return null;
        }
        finally
        {
            if (visual != null && visual.transform.parent != root.transform)
            {
                UnityEngine.Object.DestroyImmediate(visual);
            }

            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void EnsureQuickStartPlayerPrefabOnScriptsReady(
        GCQuickStartSetupContinuationContext context
    )
    {
        if (context.intent != GCQuickStartSetupIntent.ActiveScene ||
            (context.action != GCQuickStartSetupAction.ActiveSceneMissingPieces &&
             context.action != GCQuickStartSetupAction.ActiveScenePlayerPrefab))
        {
            if (context.intent != GCQuickStartSetupIntent.QuickStartScene)
            {
                Debug.Log("Quick-start player prefab assignment is deferred until a player prefab setup action runs.");
            }

            return;
        }

        var prefabResult = EnsureQuickStartPlayerPrefab(context);
        if (prefabResult.IsBlocked)
        {
            Debug.LogWarning(prefabResult.message + " " + string.Join(" ", prefabResult.blockedReasons));
            return;
        }

        var gamingCouch = GetGamingCouchForScriptsReadyAction(context.action);
        if (gamingCouch == null)
        {
            return;
        }

        var assignResult = AssignOrReplaceActiveScenePlayerPrefab(gamingCouch, prefabResult.prefab);
        if (assignResult.IsBlocked)
        {
            Debug.LogWarning(assignResult.message);
            return;
        }

        Debug.Log(prefabResult.message + " " + assignResult.message);
    }

    private static GamingCouchSceneWiringResult AssignOrReplaceActiveScenePlayerPrefab(
        GamingCouch gamingCouch,
        GameObject playerPrefab
    )
    {
        var listener = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName);
        var existingPlayerPrefab = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName);
        if (CanReplaceActiveSceneGeneratedPlayerPrefab(listener, existingPlayerPrefab))
        {
            return GamingCouchSceneWiring.ReplacePlayerPrefab(gamingCouch, playerPrefab);
        }

        string playerPrefabCompatibilityMessage;
        if (GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName) &&
            !IsActiveSceneGeneratedPlayerPrefabCompatible(listener, existingPlayerPrefab, out playerPrefabCompatibilityMessage))
        {
            return GamingCouchSceneWiringResult.BlockedResult(
                gamingCouch,
                playerPrefabCompatibilityMessage
            );
        }

        return GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);
    }

    private static void EnsureQuickStartGameListenerOnScriptsReady(
        GCQuickStartSetupContinuationContext context
    )
    {
        if (context.intent != GCQuickStartSetupIntent.ActiveScene ||
            (context.action != GCQuickStartSetupAction.ActiveSceneMissingPieces &&
             context.action != GCQuickStartSetupAction.ActiveSceneGameListener))
        {
            if (context.intent != GCQuickStartSetupIntent.QuickStartScene)
            {
                Debug.Log("Game script assignment is deferred until a Game script setup action runs.");
            }

            return;
        }

        var gamingCouch = GetGamingCouchForScriptsReadyAction(context.action);
        if (gamingCouch == null)
        {
            return;
        }

        var listenerResult = EnsureQuickStartGameListener(context, gamingCouch);
        if (listenerResult.IsBlocked)
        {
            Debug.LogWarning(listenerResult.message + " " + string.Join(" ", listenerResult.blockedReasons));
            return;
        }

        if (listenerResult.listenerObject == null)
        {
            Debug.Log(listenerResult.message);
            return;
        }

        var assignResult = GamingCouchSceneWiring.AssignListenerIfMissing(
            gamingCouch,
            listenerResult.listenerObject
        );
        if (assignResult.IsBlocked)
        {
            Debug.LogWarning(assignResult.message);
            return;
        }

        Debug.Log(listenerResult.message + " " + assignResult.message);
    }

    private static GamingCouch GetGamingCouchForScriptsReadyAction(GCQuickStartSetupAction action)
    {
        if (action == GCQuickStartSetupAction.ActiveSceneMissingPieces)
        {
            var gamingCouchResult = GamingCouchSceneWiring.EnsureActiveSceneGamingCouch();
            if (gamingCouchResult.IsBlocked)
            {
                Debug.LogWarning(gamingCouchResult.message);
                return null;
            }

            return gamingCouchResult.gamingCouch;
        }

        var gamingCouches = GamingCouchSceneWiring.FindActiveSceneGamingCouches();
        if (gamingCouches.Length == 0)
        {
            Debug.LogWarning("Create or reuse a GamingCouch object before wiring quick-start references.");
            return null;
        }

        if (gamingCouches.Length > 1)
        {
            Debug.LogWarning("The active scene contains multiple GamingCouch components. Remove duplicates manually before running setup.");
            return null;
        }

        return gamingCouches[0];
    }

    private static void EnsureQuickStartSceneOnScriptsReady(
        GCQuickStartSetupContinuationContext context
    )
    {
        if (context.intent != GCQuickStartSetupIntent.QuickStartScene ||
            context.action != GCQuickStartSetupAction.QuickStartScene)
        {
            return;
        }

        lastQuickStartSceneSetupResult = EnsureQuickStartScene(context);
        if (lastQuickStartSceneSetupResult.IsBlocked)
        {
            Debug.LogWarning(lastQuickStartSceneSetupResult.message + " " + string.Join(" ", lastQuickStartSceneSetupResult.blockedReasons));
            return;
        }

        Debug.Log(lastQuickStartSceneSetupResult.message);
    }

    private static string AssetPathToFullPath(string assetPath)
    {
        const string assetsPrefix = "Assets/";
        if (!assetPath.StartsWith(assetsPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("Expected a project Assets path: " + assetPath, nameof(assetPath));
        }

        return Path.Combine(Application.dataPath, assetPath.Substring(assetsPrefix.Length));
    }

    private static void PersistPendingSetup(GCQuickStartSetupIntent intent, GCQuickStartSetupAction action)
    {
        SessionState.SetBool(PendingSetupSessionKey, true);
        SessionState.SetString(PendingIntentSessionKey, intent.ToString());
        SessionState.SetString(PendingActionSessionKey, action.ToString());
        SessionState.SetBool(PendingWarningLoggedSessionKey, false);
    }

    private static void ClearPendingSetup()
    {
        SessionState.SetBool(PendingSetupSessionKey, false);
        SessionState.SetString(PendingIntentSessionKey, string.Empty);
        SessionState.SetString(PendingActionSessionKey, string.Empty);
        SessionState.SetBool(PendingWarningLoggedSessionKey, false);
    }

    private static void StartPendingSetupPolling()
    {
        EditorApplication.update -= ResumePendingSetupWhenReady;
        EditorApplication.update += ResumePendingSetupWhenReady;
    }

    private static void StopPendingSetupPolling()
    {
        EditorApplication.update -= ResumePendingSetupWhenReady;
    }

    private static void ResumePendingSetupWhenReady()
    {
        if (!HasPendingSetup())
        {
            StopPendingSetupPolling();
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        var intent = GetPendingIntent();
        var context = CreateContinuationContext(intent, GetPendingAction(intent), true);
        if (!context.HasRequiredTypes)
        {
            LogPendingTypeWarningOnce(context);
            return;
        }

        ClearPendingSetup();
        StopPendingSetupPolling();
        DispatchScriptsReady(context);
    }

    private static GCQuickStartSetupIntent GetPendingIntent()
    {
        var value = SessionState.GetString(PendingIntentSessionKey, DefaultIntentValue);
        if (Enum.TryParse(value, out GCQuickStartSetupIntent intent))
        {
            return intent;
        }

        return GCQuickStartSetupIntent.ActiveScene;
    }

    private static GCQuickStartSetupAction GetPendingAction(GCQuickStartSetupIntent intent)
    {
        var value = SessionState.GetString(PendingActionSessionKey, DefaultActionValue);
        if (Enum.TryParse(value, out GCQuickStartSetupAction action) &&
            IsActionValidForIntent(intent, action))
        {
            return action;
        }

        return GetDefaultAction(intent);
    }

    private static GCQuickStartSetupAction GetDefaultAction(GCQuickStartSetupIntent intent)
    {
        return intent == GCQuickStartSetupIntent.QuickStartScene
            ? GCQuickStartSetupAction.QuickStartScene
            : GCQuickStartSetupAction.ActiveSceneMissingPieces;
    }

    private static bool IsActionValidForIntent(GCQuickStartSetupIntent intent, GCQuickStartSetupAction action)
    {
        if (intent == GCQuickStartSetupIntent.QuickStartScene)
        {
            return action == GCQuickStartSetupAction.QuickStartScene;
        }

        return action == GCQuickStartSetupAction.ActiveSceneMissingPieces ||
               action == GCQuickStartSetupAction.ActiveScenePlayerPrefab ||
               action == GCQuickStartSetupAction.ActiveSceneGameListener;
    }

    private static GCQuickStartSetupContinuationContext CreateContinuationContext(
        GCQuickStartSetupIntent intent,
        GCQuickStartSetupAction action,
        bool resumedAfterCompilation
    )
    {
        var spec = GetScriptSetupSpec(intent, action);
        return new GCQuickStartSetupContinuationContext(
            intent,
            action,
            resumedAfterCompilation,
            spec.scriptFolderAssetPath,
            spec.gameScriptAssetPath,
            spec.playerScriptAssetPath,
            spec.playerPrefabAssetPath,
            spec.gameTypeName,
            spec.playerTypeName,
            spec.listenerObjectName,
            FindScriptType(spec.gameScriptAssetPath, spec.gameTypeName, typeof(MonoBehaviour)),
            FindScriptType(spec.playerScriptAssetPath, spec.playerTypeName, typeof(GCPlayer))
        );
    }

    private static void DispatchScriptsReady(GCQuickStartSetupContinuationContext context)
    {
        if (scriptsReadyHandlers == null)
        {
            Debug.Log("GamingCouch quick-start scripts are ready. Later setup tasks can continue from the scripts-ready hook.");
            return;
        }

        var handlers = scriptsReadyHandlers.GetInvocationList();
        for (var index = 0; index < handlers.Length; index++)
        {
            try
            {
                ((Action<GCQuickStartSetupContinuationContext>)handlers[index]).Invoke(context);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    private static void LogPendingTypeWarningOnce(GCQuickStartSetupContinuationContext context)
    {
        if (SessionState.GetBool(PendingWarningLoggedSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(PendingWarningLoggedSessionKey, true);
        Debug.LogWarning("GamingCouch quick-start setup is waiting for compiled types " + context.gameTypeName + " and " + context.playerTypeName + ". Existing scripts will not be overwritten.");
    }

    private static Type FindScriptType(string scriptAssetPath, string typeName, Type requiredBaseType)
    {
        var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptAssetPath);
        if (script == null)
        {
            return null;
        }

        var type = script.GetClass();
        if (type == null ||
            type.Name != typeName ||
            type.IsAbstract ||
            type.ContainsGenericParameters ||
            !requiredBaseType.IsAssignableFrom(type))
        {
            return null;
        }

        return type;
    }

    private static Type FindTypeByName(string typeName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
        {
            Type[] types;
            try
            {
                types = assemblies[assemblyIndex].GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            if (types == null)
            {
                continue;
            }

            for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
            {
                var type = types[typeIndex];
                if (type != null && type.Name == typeName)
                {
                    return type;
                }
            }
        }

        return null;
    }

    private static string BuildGameScriptSource(
        string gameTypeName,
        string playerTypeName,
        bool includeExampleTemplateHeader
    )
    {
        return (includeExampleTemplateHeader ? ExampleTemplateHeader : string.Empty) + @"using System.Collections;
using DSB.GC;
using DSB.GC.Game;
using DSB.GC.Hud;
using UnityEngine;

public class " + gameTypeName + @" : MonoBehaviour
{
    [SerializeField]
    private float roundSeconds = 10.0f;

    [SerializeField]
    private int maxScore = 100;

    private readonly GCPlayerStore<" + playerTypeName + @"> players = new GCPlayerStore<" + playerTypeName + @">();
    private Coroutine roundCoroutine;

    private void GamingCouchSetup(GCSetupOptions options)
    {
        var clampedMaxScore = Mathf.Max(1, maxScore);
        // Configure the game mode and HUD before telling GamingCouch setup is complete.
        GamingCouch.Instance.SetupGameVersus(new GCGameVersusSetupOptions
        {
            maxScore = clampedMaxScore,
            placementCriteria = new[]
            {
                GCPlacementSortCriteria.ScoreDescending,
                GCPlacementSortCriteria.Finished,
                GCPlacementSortCriteria.EliminatedDescending,
            },
            hud = new GCGameHudOptions
            {
                isPlayersAutoUpdateEnabled = true,
                players = new GCHudPlayersConfig
                {
                    valueTypeEnum = PlayersHudValueType.PointsSmall,
                },
            },
        });

        GamingCouch.Instance.SetupDone();
    }

    private void GamingCouchPlay(GCPlayOptions options)
    {
        players.Clear();
        // Spawn the player prefab assigned on the GamingCouch object and keep typed references.
        GamingCouch.Instance.SetupPlayers<" + playerTypeName + @">(options.players, player =>
        {
            players.AddPlayer(player);
            player.ApplyPlayerColor();
        });

        if (roundCoroutine != null)
        {
            StopCoroutine(roundCoroutine);
        }

        roundCoroutine = StartCoroutine(RunRound());
    }

    private IEnumerator RunRound()
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, roundSeconds));

        ApplyRandomFinalScores();
        GamingCouch.Instance.GameOver();
    }

    private void ApplyRandomFinalScores()
    {
        var clampedMaxScore = Mathf.Max(1, maxScore);
        foreach (var player in players.Players)
        {
            player.SetScore(Random.Range(0, clampedMaxScore + 1), ""Example round complete"");
            player.SetFinished(""Example round complete"");
        }
    }
}
";
    }

    private static string BuildPlayerScriptSource(
        string playerTypeName,
        bool includeExampleTemplateHeader
    )
    {
        return (includeExampleTemplateHeader ? ExampleTemplateHeader : string.Empty) + @"using DSB.GC;
using UnityEngine;

public class " + playerTypeName + @" : GCPlayer
{
    [SerializeField]
    private Renderer colorRenderer;

    private void Reset()
    {
        FindColorRenderer();
    }

    private void OnValidate()
    {
        FindColorRenderer();
    }

    private void Start()
    {
        ApplyPlayerColor();
    }

    public void ApplyPlayerColor()
    {
        FindColorRenderer();

        if (colorRenderer != null)
        {
            colorRenderer.material.color = ColorBase;
        }
    }

    public override string GetHudValueText()
    {
        return Score.ToString();
    }

    private void FindColorRenderer()
    {
        if (colorRenderer == null)
        {
            colorRenderer = GetComponentInChildren<Renderer>();
        }
    }
}
";
    }
}
