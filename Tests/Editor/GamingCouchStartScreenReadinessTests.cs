using System;
using DSB.GC;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GamingCouchStartScreenReadinessTests
{
    private const string TestSceneBuildPath = "Assets/GamingCouchStartScreenReadinessTestScene.unity";

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
        EditorBuildSettings.scenes = previousBuildSettingsScenes ?? Array.Empty<EditorBuildSettingsScene>();

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
    public void MissingGamingCouchOffersCreateGamingCouchAction()
    {
        var readiness = CreateReadiness(null, null, null);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance);

        AssertAction(check, GCStartScreenReadinessActionId.CreateGamingCouch, "Create GamingCouch");
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.GamingCouchInstance), Is.True);
    }

    [Test]
    public void ReadyGamingCouchOffersFocusSceneObjectAction()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var readiness = CreateReadiness(gamingCouch, null, null);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance);

        AssertAction(check, GCStartScreenReadinessActionId.FocusSceneObject, "Focus Scene Object");
        Assert.That(check.action.target, Is.SameAs(gamingCouch.gameObject));
    }

    [Test]
    public void MissingGameScriptOffersCreateAndWireGameAction()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var readiness = CreateReadiness(gamingCouch, null, null);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        AssertAction(check, GCStartScreenReadinessActionId.CreateAndWireGameScript, "Create & Wire Game");
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.ListenerAssigned), Is.True);
    }

    [Test]
    public void CompatibleGameScriptOffersFocusGameScriptAction()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Game");
        var readiness = CreateReadiness(gamingCouch, listener, null);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        AssertAction(check, GCStartScreenReadinessActionId.FocusGameScript, "Focus Game Script");
        Assert.That(check.action.target, Is.SameAs(listener));
    }

    [Test]
    public void IncompatibleGameScriptOffersNoSetupAction()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Incomplete Listener");
        listener.AddComponent<SetupOnlyGameScriptReceiver>();
        var readiness = CreateReadiness(gamingCouch, listener, null);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        AssertNoAction(check);
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.ListenerAssigned), Is.False);
    }

    [Test]
    public void MissingPlayerPrefabOffersWirePlayerPrefabAction()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Game");
        var readiness = CreateReadiness(gamingCouch, listener, null);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.PlayerPrefabAssigned);

        AssertAction(check, GCStartScreenReadinessActionId.WirePlayerPrefab, "Wire Player Prefab");
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.PlayerPrefabAssigned), Is.True);
    }

    [Test]
    public void ReadyPlayerPrefabOffersFocusPrefabAction()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Game");
        var playerPrefab = CreatePlayerPrefabObject("Existing Player Prefab");
        var readiness = CreateReadiness(gamingCouch, listener, playerPrefab);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.PlayerPrefabAssigned);

        AssertAction(check, GCStartScreenReadinessActionId.FocusPrefab, "Focus Prefab");
        Assert.That(check.action.target, Is.SameAs(playerPrefab));
    }

    [Test]
    public void BuildSettingsActionRequiresSettableReadiness()
    {
        var readinessWithSetup = CreateReadySceneReadiness(
            GamingCouchBuildSettingsReadiness.InspectScenePath(
                true,
                TestSceneBuildPath,
                Array.Empty<EditorBuildSettingsScene>()
            ),
            CreateReadyGameViewAspectReadiness(),
            CreateReadyWebGLExportReadiness()
        );
        var readyReadiness = CreateReadySceneReadiness(
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            CreateReadyWebGLExportReadiness()
        );

        AssertAction(
            readinessWithSetup.GetCheck(GCStartScreenReadinessCheckId.ActiveSceneFirstBuildSettingsScene),
            GCStartScreenReadinessActionId.SetFirstBuildSettingsScene,
            "Set First Build Scene"
        );
        AssertNoAction(readyReadiness.GetCheck(GCStartScreenReadinessCheckId.ActiveSceneFirstBuildSettingsScene));
    }

    [Test]
    public void GameViewActionRequiresSafeSelectionAction()
    {
        var mismatchWithSelection = GamingCouchGameViewAspect.InspectSizeEntries(
            new[]
            {
                new GCGameViewSizeEntry(0, "Free Aspect", 0, 0, true),
                new GCGameViewSizeEntry(1, "16:9 Aspect", 16, 9, true),
            },
            0,
            true,
            null
        );
        var mismatchWithoutSelection = GamingCouchGameViewAspect.InspectSizeEntries(
            new[] { new GCGameViewSizeEntry(0, "4:3 Aspect", 4, 3, true) },
            0,
            true,
            null
        );

        var readinessWithSetup = CreateReadySceneReadiness(
            CreateReadyBuildSettingsReadiness(),
            mismatchWithSelection,
            CreateReadyWebGLExportReadiness()
        );
        var readinessWithoutSetup = CreateReadySceneReadiness(
            CreateReadyBuildSettingsReadiness(),
            mismatchWithoutSelection,
            CreateReadyWebGLExportReadiness()
        );

        AssertAction(
            readinessWithSetup.GetCheck(GCStartScreenReadinessCheckId.GameViewAspect16By9),
            GCStartScreenReadinessActionId.Select16By9GameView,
            "Select 16:9"
        );
        AssertNoAction(readinessWithoutSetup.GetCheck(GCStartScreenReadinessCheckId.GameViewAspect16By9));
    }

    [Test]
    public void WebGLExportActionRequiresBlockedReadiness()
    {
        var blockedReadiness = CreateReadySceneReadiness(
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            new GCWebGLExportReadiness(
                GCWebGLExportSetupStatus.Blocked,
                false,
                false,
                false,
                false,
                false,
                true,
                "Clean WebGL export setup is incomplete.",
                Array.Empty<string>()
            )
        );
        var readyReadiness = CreateReadySceneReadiness(
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            CreateReadyWebGLExportReadiness()
        );

        AssertAction(
            blockedReadiness.GetCheck(GCStartScreenReadinessCheckId.WebGLExportSetup),
            GCStartScreenReadinessActionId.SetUpWebGLExport,
            "Set Up WebGL Export"
        );
        AssertNoAction(readyReadiness.GetCheck(GCStartScreenReadinessCheckId.WebGLExportSetup));
    }

    [Test]
    public void LocalPlayJsonRowNeverOffersSetupAction()
    {
        var readiness = CreateReadySceneReadiness(
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            CreateReadyWebGLExportReadiness(),
            new GCStartScreenLocalPlayJsonReadiness(
                false,
                "Library/GamingCouch/gc.dev.json",
                "gc.dev.json is missing or invalid for local Play Mode.",
                null
            )
        );

        AssertNoAction(readiness.GetCheck(GCStartScreenReadinessCheckId.LocalPlayJsonValid));
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.LocalPlayJsonValid), Is.False);
    }

    private GCStartScreenReadiness CreateReadySceneReadiness(
        GCActiveSceneBuildSettingsReadiness buildSettings,
        GCGameViewAspectReadiness gameViewAspect,
        GCWebGLExportReadiness webGLExport,
        GCStartScreenLocalPlayJsonReadiness localPlayJson = null
    )
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Game");
        var playerPrefab = CreatePlayerPrefabObject("Existing Player Prefab");
        return CreateReadiness(gamingCouch, listener, playerPrefab, buildSettings, gameViewAspect, webGLExport, localPlayJson);
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

    private static void AssertAction(
        GCStartScreenReadinessCheck check,
        GCStartScreenReadinessActionId expectedId,
        string expectedLabel
    )
    {
        Assert.That(check, Is.Not.Null);
        Assert.That(check.HasAction, Is.True);
        Assert.That(check.action.id, Is.EqualTo(expectedId));
        Assert.That(check.action.label, Is.EqualTo(expectedLabel));
    }

    private static void AssertNoAction(GCStartScreenReadinessCheck check)
    {
        Assert.That(check, Is.Not.Null);
        Assert.That(check.HasAction, Is.False);
        Assert.That(check.action.id, Is.EqualTo(GCStartScreenReadinessActionId.None));
        Assert.That(check.action.label, Is.Null);
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
