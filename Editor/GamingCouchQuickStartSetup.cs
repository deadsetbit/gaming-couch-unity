using DSB.GC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

internal enum GCQuickStartSetupIntent
{
    ActiveScene,
    QuickStartScene,
}

internal enum GCQuickStartScriptSetupStatus
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

internal sealed class GCQuickStartSetupContinuationContext
{
    internal readonly GCQuickStartSetupIntent intent;
    internal readonly bool resumedAfterCompilation;
    internal readonly string quickStartFolderAssetPath;
    internal readonly string gameScriptAssetPath;
    internal readonly string playerScriptAssetPath;
    internal readonly Type gameType;
    internal readonly Type playerType;

    internal GCQuickStartSetupContinuationContext(
        GCQuickStartSetupIntent intent,
        bool resumedAfterCompilation,
        string quickStartFolderAssetPath,
        string gameScriptAssetPath,
        string playerScriptAssetPath,
        Type gameType,
        Type playerType
    )
    {
        this.intent = intent;
        this.resumedAfterCompilation = resumedAfterCompilation;
        this.quickStartFolderAssetPath = quickStartFolderAssetPath;
        this.gameScriptAssetPath = gameScriptAssetPath;
        this.playerScriptAssetPath = playerScriptAssetPath;
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
    internal const string QuickStartFolderAssetPath = ProjectFolderAssetPath + "/QuickStart";
    internal const string GameTypeName = "GCQuickStartGame";
    internal const string PlayerTypeName = "GCQuickStartPlayer";
    internal const string GameScriptAssetPath = QuickStartFolderAssetPath + "/" + GameTypeName + ".cs";
    internal const string PlayerScriptAssetPath = QuickStartFolderAssetPath + "/" + PlayerTypeName + ".cs";

    private const string PendingSetupSessionKey = "DSB.GC.QuickStart.PendingSetup.v1";
    private const string PendingIntentSessionKey = "DSB.GC.QuickStart.PendingIntent.v1";
    private const string PendingWarningLoggedSessionKey = "DSB.GC.QuickStart.PendingWarningLogged.v1";
    private const string DefaultIntentValue = "ActiveScene";

    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);
    private static Action<GCQuickStartSetupContinuationContext> scriptsReadyHandlers;

    static GamingCouchQuickStartSetup()
    {
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
        var createdAssetPaths = new List<string>();
        var reusedAssetPaths = new List<string>();
        var blockedReasons = new List<string>();

        EnsureProjectFolder(ProjectFolderAssetPath, "Assets", "GamingCouch", blockedReasons);
        EnsureProjectFolder(QuickStartFolderAssetPath, ProjectFolderAssetPath, "QuickStart", blockedReasons);
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

        EnsureScriptAsset(GameScriptAssetPath, BuildGameScriptSource(), createdAssetPaths, reusedAssetPaths, blockedReasons);
        EnsureScriptAsset(PlayerScriptAssetPath, BuildPlayerScriptSource(), createdAssetPaths, reusedAssetPaths, blockedReasons);
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
            PersistPendingSetup(intent);
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

        var context = CreateContinuationContext(intent, false);
        if (!context.HasRequiredTypes)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                PersistPendingSetup(intent);
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

            blockedReasons.Add("Quick-start script assets already exist, but compiled types " + GameTypeName + " and " + PlayerTypeName + " are not both available. Existing scripts were left untouched.");
            return CreateResult(
                GCQuickStartScriptSetupStatus.Blocked,
                false,
                "Quick-start setup cannot continue until the existing script assets compile with the expected type names.",
                createdAssetPaths,
                reusedAssetPaths,
                blockedReasons
            );
        }

        DispatchScriptsReady(context);
        return CreateResult(
            GCQuickStartScriptSetupStatus.Ready,
            false,
            "Quick-start scripts already exist and compiled types are available.",
            createdAssetPaths,
            reusedAssetPaths,
            blockedReasons
        );
    }

    internal static bool HasPendingSetup()
    {
        return SessionState.GetBool(PendingSetupSessionKey, false);
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
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            reusedAssetPaths.Add(assetPath);
            return;
        }

        if (FindTypeByName(typeName) != null)
        {
            blockedReasons.Add("Cannot create " + assetPath + " because a compiled type named " + typeName + " already exists outside the generated script path.");
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

    private static string AssetPathToFullPath(string assetPath)
    {
        const string assetsPrefix = "Assets/";
        if (!assetPath.StartsWith(assetsPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("Expected a project Assets path: " + assetPath, nameof(assetPath));
        }

        return Path.Combine(Application.dataPath, assetPath.Substring(assetsPrefix.Length));
    }

    private static void PersistPendingSetup(GCQuickStartSetupIntent intent)
    {
        SessionState.SetBool(PendingSetupSessionKey, true);
        SessionState.SetString(PendingIntentSessionKey, intent.ToString());
        SessionState.SetBool(PendingWarningLoggedSessionKey, false);
    }

    private static void ClearPendingSetup()
    {
        SessionState.SetBool(PendingSetupSessionKey, false);
        SessionState.SetString(PendingIntentSessionKey, string.Empty);
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

        var context = CreateContinuationContext(GetPendingIntent(), true);
        if (!context.HasRequiredTypes)
        {
            LogPendingTypeWarningOnce();
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

    private static GCQuickStartSetupContinuationContext CreateContinuationContext(
        GCQuickStartSetupIntent intent,
        bool resumedAfterCompilation
    )
    {
        return new GCQuickStartSetupContinuationContext(
            intent,
            resumedAfterCompilation,
            QuickStartFolderAssetPath,
            GameScriptAssetPath,
            PlayerScriptAssetPath,
            FindScriptType(GameScriptAssetPath, GameTypeName, typeof(MonoBehaviour)),
            FindScriptType(PlayerScriptAssetPath, PlayerTypeName, typeof(GCPlayer))
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

    private static void LogPendingTypeWarningOnce()
    {
        if (SessionState.GetBool(PendingWarningLoggedSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(PendingWarningLoggedSessionKey, true);
        Debug.LogWarning("GamingCouch quick-start setup is waiting for compiled types " + GameTypeName + " and " + PlayerTypeName + ". Existing scripts will not be overwritten.");
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

    private static string BuildGameScriptSource()
    {
        return @"using System.Collections;
using DSB.GC;
using DSB.GC.Game;
using DSB.GC.Hud;
using UnityEngine;

public class GCQuickStartGame : MonoBehaviour
{
    [SerializeField]
    private float roundSeconds = 10.0f;

    [SerializeField]
    private int maxScore = 100;

    private readonly GCPlayerStore<GCQuickStartPlayer> players = new GCPlayerStore<GCQuickStartPlayer>();
    private Coroutine roundCoroutine;

    private void GamingCouchSetup(GCSetupOptions options)
    {
        var clampedMaxScore = Mathf.Max(1, maxScore);
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
        GamingCouch.Instance.SetupPlayers<GCQuickStartPlayer>(options.players, player =>
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

        var clampedMaxScore = Mathf.Max(1, maxScore);
        foreach (var player in players.Players)
        {
            player.SetScore(Random.Range(0, clampedMaxScore + 1), ""Quick-start round complete"");
            player.SetFinished(""Quick-start round complete"");
        }

        GamingCouch.Instance.GameOver();
    }
}
";
    }

    private static string BuildPlayerScriptSource()
    {
        return @"using DSB.GC;
using UnityEngine;

public class GCQuickStartPlayer : GCPlayer
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
