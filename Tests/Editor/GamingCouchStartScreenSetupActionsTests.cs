using System;
using System.Linq;
using System.Reflection;
using DSB.GC;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GamingCouchStartScreenSetupActionsTests
{
    private const string TestSceneBuildPath = "Assets/GamingCouchStartScreenSetupActionsTestScene.unity";

    private EditorBuildSettingsScene[] previousBuildSettingsScenes;
    private Scene previousActiveScene;
    private Scene testScene;
    private bool testSceneWasCreatedAdditively;

    [SetUp]
    public void SetUp()
    {
        previousBuildSettingsScenes = EditorBuildSettings.scenes;
        previousActiveScene = SceneManager.GetActiveScene();

        if (CanReuseActiveSceneAsTestScene(previousActiveScene))
        {
            testScene = previousActiveScene;
            testSceneWasCreatedAdditively = false;
        }
        else
        {
            testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            testSceneWasCreatedAdditively = true;
        }

        ClearSceneRootObjects(testScene);
        EnsureSceneIsActive(testScene);
    }

    [TearDown]
    public void TearDown()
    {
        CloseWebGLPreviewWindows();
        EditorBuildSettings.scenes = previousBuildSettingsScenes ?? Array.Empty<EditorBuildSettingsScene>();
        Selection.activeObject = null;

        if (testScene.IsValid() && testScene.isLoaded)
        {
            ClearSceneRootObjects(testScene);
        }

        if (!testSceneWasCreatedAdditively)
        {
            return;
        }

        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
        {
            SceneManager.SetActiveScene(previousActiveScene);
        }
        else if (testScene.IsValid() && testScene.isLoaded)
        {
            SetAnyLoadedSceneActiveExcept(testScene);
        }

        if (testScene.IsValid() && testScene.isLoaded)
        {
            EditorSceneManager.CloseScene(testScene, true);
        }
    }

    [Test]
    public void NullChecklistActionReturnsWarningWithoutSceneChanges()
    {
        var result = GamingCouchStartScreenSetupActions.RunChecklistAction(null);

        Assert.That(result.messageType, Is.EqualTo(MessageType.Warning));
        Assert.That(result.message, Does.Contain("No checklist item"));
        Assert.That(result.details, Is.Empty);
        Assert.That(result.focusTarget, Is.Null);
        Assert.That(result.shouldRefreshAndRepaint, Is.False);
        Assert.That(FindGamingCouchesInTestScene(), Is.Empty);
    }

    [Test]
    public void CreateGamingCouchActionUsesSceneWiringAndReturnsSelectionTarget()
    {
        var readiness = CreateReadiness(null, null, null);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance);

        var result = GamingCouchStartScreenSetupActions.RunChecklistAction(check);
        var gamingCouches = FindGamingCouchesInTestScene();

        Assert.That(result.messageType, Is.EqualTo(MessageType.Info));
        Assert.That(result.message, Does.Contain("GamingCouch"));
        Assert.That(result.shouldRefreshAndRepaint, Is.True);
        Assert.That(result.shouldPingFocusTarget, Is.False);
        Assert.That(gamingCouches, Has.Length.EqualTo(1));
        Assert.That(result.focusTarget, Is.SameAs(gamingCouches[0].gameObject));
    }

    [Test]
    public void FocusActionReturnsTargetWithoutRunningSetup()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Game");
        var playerPrefab = CreatePlayerPrefabObject("Existing Player Prefab");
        var readiness = CreateReadiness(gamingCouch, listener, playerPrefab);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.PlayerPrefabAssigned);

        var result = GamingCouchStartScreenSetupActions.RunChecklistAction(check);

        Assert.That(result.messageType, Is.EqualTo(MessageType.Info));
        Assert.That(result.message, Does.Contain("Focused Existing Player Prefab"));
        Assert.That(result.focusTarget, Is.SameAs(playerPrefab));
        Assert.That(result.shouldPingFocusTarget, Is.True);
        Assert.That(result.shouldRefreshAndRepaint, Is.False);
        Assert.That(FindGamingCouchesInTestScene(), Has.Length.EqualTo(1));
    }

    [Test]
    public void WindowAppliesFocusActionSelectionFromRunnerResult()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Game");
        var playerPrefab = CreatePlayerPrefabObject("Existing Player Prefab");
        var readiness = CreateReadiness(gamingCouch, listener, playerPrefab);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.PlayerPrefabAssigned);
        var window = EditorWindow.CreateInstance<GamingCouchStartScreenWindow>();

        try
        {
            InvokeWindowChecklistAction(window, check);

            Assert.That(Selection.activeObject, Is.SameAs(playerPrefab));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(window);
        }
    }

    [Test]
    public void WebGLBuildMenuCommandOpensSharedPreviewWithoutApplying()
    {
        CloseWebGLPreviewWindows();

        GamingCouchWebGLBuildMenu.PreviewReleaseBuildSettings();

        Assert.That(FindWebGLPreviewWindows(), Is.Not.Empty);
    }

    [Test]
    public void WebGLProfileMenuCommandsOpenSelectorWithRequestedInitialProfile()
    {
        CloseWebGLPreviewWindows();

        GamingCouchWebGLBuildMenu.PreviewDevBuildSettings();
        Assert.That(
            GetSelectedProfile(AssertSingleWebGLPreviewWindow()),
            Is.EqualTo(GCWebGLBuildSettingsProfileId.Dev)
        );

        CloseWebGLPreviewWindows();

        GamingCouchWebGLBuildMenu.PreviewReleaseBuildSettings();
        Assert.That(
            GetSelectedProfile(AssertSingleWebGLPreviewWindow()),
            Is.EqualTo(GCWebGLBuildSettingsProfileId.Release)
        );
    }

    [Test]
    public void StartScreenBuildSettingsProfileActionOpensSharedPreview()
    {
        CloseWebGLPreviewWindows();

        var result = GamingCouchStartScreenSetupActions.OpenWebGLBuildSettingsProfilePreview(null);

        Assert.That(result.messageType, Is.EqualTo(MessageType.Info));
        Assert.That(result.message, Does.Contain("WebGL build settings preview opened"));
        Assert.That(
            GetSelectedProfile(AssertSingleWebGLPreviewWindow()),
            Is.EqualTo(GCWebGLBuildSettingsProfileId.Dev)
        );
    }

    [Test]
    public void StartScreenWebGLChecklistActionOpensSharedPreview()
    {
        CloseWebGLPreviewWindows();
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Game");
        var playerPrefab = CreatePlayerPrefabObject("Existing Player Prefab");
        var readiness = CreateReadiness(
            gamingCouch,
            listener,
            playerPrefab,
            webGLExport: new GCWebGLExportReadiness(
                GCWebGLExportSetupStatus.Blocked,
                false,
                false,
                false,
                false,
                false,
                false,
                "Clean WebGL export setup is incomplete.",
                Array.Empty<string>()
            )
        );
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.WebGLExportSetup);
        var window = EditorWindow.CreateInstance<GamingCouchStartScreenWindow>();

        try
        {
            InvokeWindowChecklistAction(window, check);

            Assert.That(FindWebGLPreviewWindows(), Is.Not.Empty);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(window);
        }
    }

    [Test]
    public void CreateAndWireGameActionBlocksWithoutGamingCouchAndDoesNotCreateGameObject()
    {
        var result = GamingCouchStartScreenSetupActions.RunSetupAction(
            GCStartScreenReadinessActionId.CreateAndWireGameScript
        );

        Assert.That(result.messageType, Is.EqualTo(MessageType.Error));
        Assert.That(result.message, Does.Contain("blocked"));
        AssertHasEntryContaining(result.details, "Create or reuse a GamingCouch object");
        Assert.That(FindRootObject("Game"), Is.Null);
        Assert.That(result.shouldRefreshAndRepaint, Is.True);
    }

    [Test]
    public void ReadySetupActionResultsRemainSilentForWindowDisplay()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Game");
        Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

        var result = GamingCouchStartScreenSetupActions.RunSetupAction(
            GCStartScreenReadinessActionId.CreateAndWireGameScript
        );

        Assert.That(result.messageType, Is.EqualTo(MessageType.Info));
        Assert.That(GamingCouchStartScreenSetupActions.ShouldDisplayActionResult(result.messageType), Is.False);
        Assert.That(result.message, Does.Contain("already complete"));
        Assert.That(result.shouldRefreshAndRepaint, Is.True);
    }

    [Test]
    public void PendingCompilationResultReturnsWarningAndPreservesDetails()
    {
        var pendingResult = new GCActiveSceneSetupResult(
            GCActiveSceneSetupStatus.PendingCompilation,
            true,
            "Active Scene Setup created missing example scripts and queued setup continuation after Unity compiles them.",
            new[]
            {
                "Created: Assets/GamingCouch/GCExample/GCGameExample.cs",
                "Created: Assets/GamingCouch/GCExample/GCPlayerExample.cs",
            }
        );

        var result = GamingCouchStartScreenSetupActions.FromActiveSceneSetupResult(pendingResult);
        var formatted = GamingCouchStartScreenSetupActions.FormatActionMessage(result.message, result.details);

        Assert.That(result.messageType, Is.EqualTo(MessageType.Warning));
        Assert.That(GamingCouchStartScreenSetupActions.ShouldDisplayActionResult(result.messageType), Is.True);
        AssertHasEntryContaining(result.details, "Created: Assets/GamingCouch/GCExample/GCGameExample.cs");
        AssertHasEntryContaining(result.details, "Created: Assets/GamingCouch/GCExample/GCPlayerExample.cs");
        Assert.That(formatted, Does.Contain("queued setup continuation"));
        Assert.That(formatted, Does.Contain("- Created: Assets/GamingCouch/GCExample/GCGameExample.cs"));
        Assert.That(formatted, Does.Contain("- Created: Assets/GamingCouch/GCExample/GCPlayerExample.cs"));
    }

    [Test]
    public void ActiveSceneSetupNullResultUsesActiveSceneTerminology()
    {
        var result = GamingCouchStartScreenSetupActions.FromActiveSceneSetupResult(null);

        Assert.That(result.messageType, Is.EqualTo(MessageType.Error));
        Assert.That(result.message, Is.EqualTo("Active Scene Setup did not return a result."));
    }

    [Test]
    public void PendingSetupDisplayNameUsesActiveSceneTerminology()
    {
        Assert.That(
            GamingCouchActiveSceneSetup.GetSetupDisplayName(GCActiveSceneSetupIntent.ActiveScene),
            Is.EqualTo("Active Scene Setup")
        );
    }

    [Test]
    public void SetupActionAvailabilityUsesReadinessRules()
    {
        var readinessWithoutActiveScene = new GCStartScreenReadiness(
            default(Scene),
            null,
            null,
            null,
            null,
            CreateValidLocalPlayJsonReadiness(),
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            CreateReadyWebGLExportReadiness()
        );
        var missingGamingCouchCheck = readinessWithoutActiveScene.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance);
        var gameViewReadiness = CreateReadiness(
            CreateGamingCouch("GamingCouch"),
            CreateCompatibleListener("Existing Game"),
            CreatePlayerPrefabObject("Existing Player Prefab"),
            CreateReadyBuildSettingsReadiness(),
            GamingCouchGameViewAspect.InspectSizeEntries(
                new[]
                {
                    new GCGameViewSizeEntry(0, "Free Aspect", 0, 0, true),
                    new GCGameViewSizeEntry(1, "16:9 Aspect", 16, 9, true),
                },
                0,
                true,
                null
            ),
            CreateReadyWebGLExportReadiness()
        );
        var gameViewCheck = gameViewReadiness.GetCheck(GCStartScreenReadinessCheckId.GameViewAspect16By9);

        Assert.That(
            GamingCouchStartScreenSetupActions.IsChecklistActionDisabled(
                missingGamingCouchCheck,
                readinessWithoutActiveScene
            ),
            Is.True
        );
        Assert.That(
            GamingCouchStartScreenSetupActions.IsChecklistActionDisabled(gameViewCheck, gameViewReadiness),
            Is.False
        );
    }

    private GCStartScreenReadiness CreateReadiness(
        GamingCouch gamingCouch,
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab,
        GCActiveSceneBuildSettingsReadiness buildSettings = null,
        GCGameViewAspectReadiness gameViewAspect = null,
        GCWebGLExportReadiness webGLExport = null,
        GCStartScreenLocalPlayJsonReadiness localPlayJson = null
    )
    {
        return new GCStartScreenReadiness(
            testScene,
            gamingCouch != null ? new[] { gamingCouch } : new GamingCouch[0],
            gamingCouch,
            listener,
            playerPrefab,
            localPlayJson ?? CreateValidLocalPlayJsonReadiness(),
            buildSettings ?? CreateReadyBuildSettingsReadiness(),
            gameViewAspect ?? CreateReadyGameViewAspectReadiness(),
            webGLExport ?? CreateReadyWebGLExportReadiness()
        );
    }

    private static GamingCouch CreateGamingCouch(string name)
    {
        var gameObject = new GameObject(name);
        gameObject.SetActive(false);
        return gameObject.AddComponent<GamingCouch>();
    }

    private static GameObject CreateCompatibleListener(string name)
    {
        var gameObject = new GameObject(name);
        gameObject.AddComponent<CompatibleGameScriptReceiver>();
        return gameObject;
    }

    private static GameObject CreatePlayerPrefabObject(string name)
    {
        var gameObject = new GameObject(name);
        gameObject.AddComponent<GCPlayer>();
        return gameObject;
    }

    private GamingCouch[] FindGamingCouchesInTestScene()
    {
        return testScene.GetRootGameObjects()
            .Select(root => root != null ? root.GetComponent<GamingCouch>() : null)
            .Where(gamingCouch => gamingCouch != null)
            .ToArray();
    }

    private GameObject FindRootObject(string name)
    {
        return testScene.GetRootGameObjects()
            .FirstOrDefault(root => root != null && string.Equals(root.name, name, StringComparison.Ordinal));
    }

    private static GCStartScreenLocalPlayJsonReadiness CreateValidLocalPlayJsonReadiness()
    {
        return new GCStartScreenLocalPlayJsonReadiness(
            true,
            "Library/GamingCouch/gc.dev.json",
            "gc.dev.json is valid for local Play Mode.",
            null
        );
    }

    private static GCActiveSceneBuildSettingsReadiness CreateReadyBuildSettingsReadiness()
    {
        return GamingCouchBuildSettingsReadiness.InspectScenePath(
            true,
            TestSceneBuildPath,
            new[] { new EditorBuildSettingsScene(TestSceneBuildPath, true) }
        );
    }

    private static GCGameViewAspectReadiness CreateReadyGameViewAspectReadiness()
    {
        return GamingCouchGameViewAspect.InspectSizeEntries(
            new[] { new GCGameViewSizeEntry(0, "16:9 Aspect", 16, 9, true) },
            0,
            true,
            null
        );
    }

    private static GCWebGLExportReadiness CreateReadyWebGLExportReadiness()
    {
        return new GCWebGLExportReadiness(
            GCWebGLExportSetupStatus.Ready,
            true,
            true,
            true,
            true,
            true,
            true,
            "Clean WebGL export setup is ready.",
            Array.Empty<string>()
        );
    }

    private static void AssertHasEntryContaining(string[] entries, string expectedSubstring)
    {
        Assert.That(
            entries != null && entries.Any(entry =>
                entry != null && entry.IndexOf(expectedSubstring, StringComparison.Ordinal) >= 0
            ),
            Is.True
        );
    }

    private static void InvokeWindowChecklistAction(
        GamingCouchStartScreenWindow window,
        GCStartScreenReadinessCheck check
    )
    {
        var method = typeof(GamingCouchStartScreenWindow)
            .GetMethod("RunChecklistAction", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        method.Invoke(window, new object[] { check });
    }

    private static GamingCouchWebGLBuildSettingsPreviewWindow[] FindWebGLPreviewWindows()
    {
        return Resources.FindObjectsOfTypeAll<GamingCouchWebGLBuildSettingsPreviewWindow>();
    }

    private static GamingCouchWebGLBuildSettingsPreviewWindow AssertSingleWebGLPreviewWindow()
    {
        var windows = FindWebGLPreviewWindows();
        Assert.That(windows, Has.Length.EqualTo(1));
        return windows[0];
    }

    private static GCWebGLBuildSettingsProfileId GetSelectedProfile(
        GamingCouchWebGLBuildSettingsPreviewWindow window
    )
    {
        var field = typeof(GamingCouchWebGLBuildSettingsPreviewWindow)
            .GetField("selectedProfileId", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);

        return (GCWebGLBuildSettingsProfileId)field.GetValue(window);
    }

    private static void CloseWebGLPreviewWindows()
    {
        var windows = FindWebGLPreviewWindows();
        for (var index = 0; index < windows.Length; index++)
        {
            if (windows[index] != null)
            {
                windows[index].Close();
            }
        }
    }

    private static bool CanReuseActiveSceneAsTestScene(Scene scene)
    {
        return scene.IsValid() &&
               scene.isLoaded &&
               string.IsNullOrEmpty(scene.path);
    }

    private static void EnsureSceneIsActive(Scene scene)
    {
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.handle == scene.handle)
        {
            return;
        }

        Assert.That(SceneManager.SetActiveScene(scene), Is.True);
    }

    private static void ClearSceneRootObjects(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (var index = 0; index < roots.Length; index++)
        {
            if (roots[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(roots[index]);
            }
        }
    }

    private static void SetAnyLoadedSceneActiveExcept(Scene excludedScene)
    {
        for (var index = 0; index < SceneManager.sceneCount; index++)
        {
            var scene = SceneManager.GetSceneAt(index);
            if (scene.IsValid() && scene.isLoaded && scene != excludedScene)
            {
                SceneManager.SetActiveScene(scene);
                return;
            }
        }
    }
}
