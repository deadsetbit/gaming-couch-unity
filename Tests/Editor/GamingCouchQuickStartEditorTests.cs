using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DSB.GC;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GamingCouchQuickStartEditorTests
{
    private const string SuppressAutoOpenKey = "DSB.GC.StartScreen.SuppressAutoOpen";
    private const string TestFolderAssetPathPrefix = "Assets/GamingCouchQuickStartEditorTests_";
    private const string ExistingSceneBuildPath = "Assets/GamingCouchExistingScene.unity";
    private const string TestSceneBuildPath = "Assets/GamingCouchQuickStartEditorTestScene.unity";
    private const string OtherSceneBuildPath = "Assets/GamingCouchOtherScene.unity";

    private string previousSuppressAutoOpenConfigValue;
    private EditorBuildSettingsScene[] previousBuildSettingsScenes;
    private string testFolderAssetPath;
    private Scene previousActiveScene;
    private Scene testScene;
    private bool testSceneWasCreatedAdditively;
    private string previousWebGLTemplate;
    private WebGLCompressionFormat previousWebGLCompressionFormat;
    private bool previousWebGLDataCaching;
    private WebGLExceptionSupport previousWebGLExceptionSupport;
    private WebGLDebugSymbolMode previousWebGLDebugSymbolMode;
#if UNITY_2023_1_OR_NEWER
    private bool previousWebGLWasm2023;
#endif
    private UnityEditor.WebGL.WasmCodeOptimization previousWebGLCodeOptimization;
    private bool previousDevelopmentBuild;
    private Il2CppCodeGeneration previousIl2CppCodeGeneration;
    private ManagedStrippingLevel previousManagedStrippingLevel;
    private bool previousStripUnusedMeshComponents;
    private bool previousSplashScreenShow;
    private bool previousSplashScreenShowUnityLogo;
    private bool createdExampleProjectFolderForPrefabTest;
    private bool createdExampleFolderForPrefabTest;
    private bool createdQuickStartPrefabForPrefabTest;
    private bool createdExampleProjectFolderForCollisionTest;
    private bool createdExampleFolderForCollisionTest;
    private bool createdExampleGameScriptPathCollisionForTest;
    private bool createdExamplePlayerScriptPathCollisionForTest;

    [SetUp]
    public void SetUp()
    {
        previousSuppressAutoOpenConfigValue = EditorUserSettings.GetConfigValue(SuppressAutoOpenKey);
        previousBuildSettingsScenes = EditorBuildSettings.scenes;
        testFolderAssetPath = TestFolderAssetPathPrefix + Guid.NewGuid().ToString("N");
        previousActiveScene = SceneManager.GetActiveScene();
        SaveWebGLSettings();

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
        var cleanupErrors = new List<Exception>();

        RunCleanup(RestoreActiveSceneAndCloseTestScene, cleanupErrors);
        RunCleanup(DeleteGeneratedScriptPathCollisionTestAssets, cleanupErrors);
        RunCleanup(DeleteQuickStartPrefabTestAssets, cleanupErrors);
        RunCleanup(DeleteTestAssetFolder, cleanupErrors);
        RunCleanup(RestoreBuildSettings, cleanupErrors);
        RunCleanup(RestoreSuppressAutoOpenSetting, cleanupErrors);
        RunCleanup(RestoreWebGLSettings, cleanupErrors);
        if (cleanupErrors.Count > 0)
        {
            throw new AggregateException(cleanupErrors);
        }
    }

    [Test]
    public void ReadinessReportsMissingGamingCouchInEmptyScene()
    {
        var readiness = GCStartScreenReadinessService.InspectActiveScene();

        Assert.That(readiness.IsSceneReady, Is.False);
        Assert.That(readiness.gamingCouches, Has.Length.EqualTo(0));
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ActiveScene, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.GamingCouchInstance, GCStartScreenReadinessCheckState.Fail);
        Assert.That(readiness.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance).message, Does.Contain("Create a GamingCouch object"));
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Blocked);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Blocked);
        AssertCollapsedGamingCouchChecklist(readiness);
    }

    [Test]
    public void ReadinessKeepsActiveSceneGuardOutOfDisplayedChecklist()
    {
        var readiness = new GCStartScreenReadiness(default(Scene), null, null, null, null, null);
        GCStartScreenReadinessCheck activeSceneCheck;
        var checklistIds = readiness.checklist.Select(check => check.id).ToArray();
        var checklistLabels = readiness.checklist.Select(check => check.label).ToArray();

        Assert.That(readiness.IsSceneReady, Is.False);
        Assert.That(readiness.sceneName, Is.EqualTo("Untitled"));
        Assert.That(readiness.scenePath, Is.Null);
        Assert.That(readiness.TryGetCheck(GCStartScreenReadinessCheckId.ActiveScene, out activeSceneCheck), Is.True);
        Assert.That(activeSceneCheck, Is.SameAs(readiness.GetCheck(GCStartScreenReadinessCheckId.ActiveScene)));
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ActiveScene, GCStartScreenReadinessCheckState.Fail);
        Assert.That(readiness.GetCheck(GCStartScreenReadinessCheckId.ActiveScene).message, Does.Contain("No loaded active scene"));
        Assert.That(checklistIds, Has.No.Member(GCStartScreenReadinessCheckId.ActiveScene));
        Assert.That(HasActiveSceneChecklistLabel(checklistLabels), Is.False);
    }

    [Test]
    public void ReadinessProvidesHelpTextForDisplayedChecklistRows()
    {
        var readiness = new GCStartScreenReadiness(default(Scene), null, null, null, null, null);
        var displayableChecks = readiness.checklist.Where(check => check != null).ToArray();

        Assert.That(displayableChecks, Is.Not.Empty);
        Assert.That(displayableChecks.All(check => !string.IsNullOrEmpty(check.helpText)), Is.True);
        Assert.That(displayableChecks.All(check => check.helpText != check.label), Is.True);
        Assert.That(displayableChecks.All(check => check.helpText != check.message), Is.True);
        Assert.That(displayableChecks.All(check => check.helpText.Length <= 90), Is.True);
    }

    [Test]
    public void ReadinessUsesGameScriptCopyForListenerChecklistRow()
    {
        var blockedReadiness = new GCStartScreenReadiness(default(Scene), null, null, null, null, null);
        var blockedCheck = blockedReadiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        Assert.That(blockedCheck.label, Is.EqualTo("Game script is ready"));
        Assert.That(blockedCheck.message, Does.Contain("Game script readiness"));
        Assert.That(blockedCheck.helpText, Does.Contain("Game script"));

        var gamingCouch = CreateGamingCouch("GamingCouch");
        var missingReadiness = GCStartScreenReadinessService.InspectActiveScene();
        var missingCheck = missingReadiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        Assert.That(missingCheck.label, Is.EqualTo("Game script is ready"));
        Assert.That(missingCheck.message, Does.Contain("Game script object"));

        var listener = CreateCompatibleListener("Existing Game");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        var readyReadiness = GCStartScreenReadinessService.InspectActiveScene();
        var readyCheck = readyReadiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        Assert.That(readyCheck.label, Is.EqualTo("Game script is ready"));
        Assert.That(readyCheck.message, Does.Contain("Game script reference"));
        Assert.That(readyCheck.message, Does.Contain("Existing Game"));
    }

    [Test]
    public void StartScreenWindowUsesGameScriptActionAndFocusLabels()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var missingReadiness = GCStartScreenReadinessService.InspectActiveScene();
        var missingCheck = missingReadiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);
        var listener = CreateCompatibleListener("Existing Game");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        var readyReadiness = GCStartScreenReadinessService.InspectActiveScene();
        var readyCheck = readyReadiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);
        var window = EditorWindow.CreateInstance<GamingCouchStartScreenWindow>();

        try
        {
            SetStartScreenWindowReadiness(window, missingReadiness);
            Assert.That(GetChecklistActionLabel(window, missingCheck), Is.EqualTo("Create & Wire Game"));

            SetStartScreenWindowReadiness(window, readyReadiness);
            Assert.That(GetChecklistActionLabel(window, readyCheck), Is.EqualTo("Focus Game Script"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(window);
        }
    }

    [Test]
    public void StartScreenWindowDoesNotOfferGameScriptSetupActionForAssignedIncompatibleListener()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Incomplete Listener");
        listener.AddComponent<SetupOnlyGameScriptReceiver>();
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        var readiness = GCStartScreenReadinessService.InspectActiveScene();
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);
        var window = EditorWindow.CreateInstance<GamingCouchStartScreenWindow>();

        try
        {
            SetStartScreenWindowReadiness(window, readiness);

            Assert.That(GetChecklistActionLabel(window, check), Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(window);
        }
    }

    [Test]
    public void ReadinessReportsMissingReferencesOnBareGamingCouch()
    {
        CreateGamingCouch("GamingCouch");

        var readiness = GCStartScreenReadinessService.InspectActiveScene();

        Assert.That(readiness.IsSceneReady, Is.False);
        Assert.That(readiness.gamingCouches, Has.Length.EqualTo(1));
        AssertCheck(readiness, GCStartScreenReadinessCheckId.GamingCouchInstance, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Fail);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Fail);
        AssertCollapsedGamingCouchChecklist(readiness);
    }

    [Test]
    public void ReadinessReportsIndividualMissingReferenceStates()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Listener");
        var playerPrefab = CreatePlayerPrefabObject("Existing Player Prefab");

        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);

        var missingPlayerReadiness = GCStartScreenReadinessService.InspectActiveScene();
        Assert.That(missingPlayerReadiness.IsSceneReady, Is.False);
        AssertCheck(missingPlayerReadiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(missingPlayerReadiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Fail);

        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);

        var ready = GCStartScreenReadinessService.InspectActiveScene();
        Assert.That(ready.IsSceneReady, Is.True);
        AssertCheck(ready, GCStartScreenReadinessCheckId.GamingCouchInstance, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(ready, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(ready, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Pass);
        AssertCollapsedGamingCouchChecklist(ready);
    }

    [Test]
    public void ReadinessAcceptsCompatibleCustomListenerNotNamedGame()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Round Coordinator");

        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);

        var readiness = GCStartScreenReadinessService.InspectActiveScene();
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Pass);
        Assert.That(check.message, Does.Contain("Round Coordinator"));
    }

    [Test]
    public void ReadinessAcceptsPublicInheritedReceiverMethods()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Inherited Listener");
        listener.AddComponent<InheritedCompatibleGameScriptReceiver>();

        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);

        var readiness = GCStartScreenReadinessService.InspectActiveScene();

        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Pass);
    }

    [Test]
    public void ReadinessRejectsAssignedListenerWithoutSetupAndPlayReceivers()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Incomplete Listener");
        listener.AddComponent<SetupOnlyGameScriptReceiver>();

        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);

        var readiness = GCStartScreenReadinessService.InspectActiveScene();
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Fail);
        Assert.That(check.message, Does.Contain("Incomplete Listener"));
        Assert.That(check.message, Does.Contain("GamingCouchSetup(GCSetupOptions)"));
        Assert.That(check.message, Does.Contain("GamingCouchPlay(GCPlayOptions)"));
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.ListenerAssigned), Is.False);
    }

    [Test]
    public void ReadinessRejectsReceiverMethodsWithWrongSignatures()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Wrong Signature Listener");
        listener.AddComponent<WrongSignatureGameScriptReceiver>();

        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);

        var readiness = GCStartScreenReadinessService.InspectActiveScene();
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Fail);
        Assert.That(check.message, Does.Contain("Wrong Signature Listener"));
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.ListenerAssigned), Is.False);
    }

    [Test]
    public void ReadinessReportsUnresolvedSerializedListenerReferenceGuidance()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var readiness = new GCStartScreenReadiness(
            testScene,
            new[] { gamingCouch },
            gamingCouch,
            null,
            null,
            CreateValidLocalPlayJsonReadiness(),
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            CreateReadyWebGLExportReadiness(),
            true
        );
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Fail);
        Assert.That(check.message, Does.Contain("could not be resolved"));
        Assert.That(check.message, Does.Contain("deleted object"));
        Assert.That(check.message, Does.Contain("unloaded asset"));
        Assert.That(check.message, Does.Contain("script that no longer compiles"));
        Assert.That(check.message, Does.Contain("Clear or replace"));
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.ListenerAssigned), Is.False);
    }

    [Test]
    public void ReadinessReportsMissingSerializedListenerReferenceAndOffersSetupAction()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Deleted Game");
        Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

        UnityEngine.Object.DestroyImmediate(listener);

        var readiness = GCStartScreenReadinessService.InspectActiveScene();
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        Assert.That(readiness.hasMissingSerializedListenerReference, Is.True);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Fail);
        Assert.That(check.message, Does.Contain("missing GameObject"));
        Assert.That(check.message, Does.Contain("Create & Wire Game"));
        Assert.That(check.message.Contains("broken"), Is.False);
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.ListenerAssigned), Is.True);
    }

    [Test]
    public void ReadinessBlocksWhenActiveSceneHasMultipleGamingCouches()
    {
        CreateGamingCouch("GamingCouch A");
        CreateGamingCouch("GamingCouch B");

        var readiness = GCStartScreenReadinessService.InspectActiveScene();

        Assert.That(readiness.IsSceneReady, Is.False);
        Assert.That(readiness.gamingCouches, Has.Length.EqualTo(2));
        Assert.That(readiness.gamingCouch, Is.Null);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.GamingCouchInstance, GCStartScreenReadinessCheckState.Fail);
        Assert.That(readiness.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance).message, Does.Contain("multiple GamingCouch"));
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Blocked);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Blocked);
        AssertCollapsedGamingCouchChecklist(readiness);
    }

    [Test]
    public void SceneWiringPreservesExistingListenerAndPlayerPrefabReferences()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var existingListener = new GameObject("Existing Listener");
        var replacementListener = new GameObject("Replacement Listener");
        var existingPlayerPrefab = CreatePlayerPrefabObject("Existing Player Prefab");
        var replacementPlayerPrefab = CreatePlayerPrefabObject("Replacement Player Prefab");

        Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, existingListener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));
        Assert.That(GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, existingPlayerPrefab).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

        var listenerRerun = GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, replacementListener);
        var playerPrefabRerun = GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, replacementPlayerPrefab);

        Assert.That(listenerRerun.status, Is.EqualTo(GamingCouchSceneWiringStatus.Unchanged));
        Assert.That(playerPrefabRerun.status, Is.EqualTo(GamingCouchSceneWiringStatus.Unchanged));
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.SameAs(existingListener));
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName), Is.SameAs(existingPlayerPrefab));
    }

    [Test]
    public void SceneWiringReplacesMissingListenerReference()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var deletedListener = CreateCompatibleListener("Deleted Game");
        var replacementListener = CreateCompatibleListener("Replacement Game");
        Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, deletedListener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

        UnityEngine.Object.DestroyImmediate(deletedListener);

        Assert.That(GamingCouchSceneWiring.HasMissingObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.True);

        var result = GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, replacementListener);

        Assert.That(result.status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));
        Assert.That(result.changed, Is.True);
        Assert.That(result.message, Does.Contain("Replaced the missing"));
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.SameAs(replacementListener));
        Assert.That(GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.True);
    }

    [Test]
    public void CreateAndWireGameScriptSetupUsesGCExampleGameAndPlayerAssets()
    {
        var gameSpec = GamingCouchQuickStartSetup.GetScriptSetupSpec(
            GCQuickStartSetupIntent.ActiveScene,
            GCQuickStartSetupAction.ActiveSceneGameListener
        );
        var playerPrefabSpec = GamingCouchQuickStartSetup.GetScriptSetupSpec(
            GCQuickStartSetupIntent.ActiveScene,
            GCQuickStartSetupAction.ActiveScenePlayerPrefab
        );

        Assert.That(gameSpec.scriptFolderAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.ExampleFolderAssetPath));
        Assert.That(gameSpec.gameScriptAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCGameExample.cs"));
        Assert.That(gameSpec.playerScriptAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCPlayerExample.cs"));
        Assert.That(gameSpec.gameTypeName, Is.EqualTo("GCGameExample"));
        Assert.That(gameSpec.playerTypeName, Is.EqualTo("GCPlayerExample"));
        Assert.That(gameSpec.listenerObjectName, Is.EqualTo("Game"));
        Assert.That(gameSpec.requiresGeneratedScriptFolder, Is.True);
        Assert.That(gameSpec.requiresQuickStartFolders, Is.False);
        Assert.That(playerPrefabSpec.gameScriptAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.GameScriptAssetPath));
        Assert.That(playerPrefabSpec.playerScriptAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.PlayerScriptAssetPath));
    }

    [Test]
    public void GeneratedActiveSceneGameSourceUsesGCGameExampleAndDemonstratesPlayFlow()
    {
        var gameSpec = GamingCouchQuickStartSetup.GetScriptSetupSpec(
            GCQuickStartSetupIntent.ActiveScene,
            GCQuickStartSetupAction.ActiveSceneGameListener
        );
        var source = gameSpec.BuildGameScriptSource();

        AssertHasExampleTemplateHeader(source);
        AssertGeneratedGameSourceDemonstratesPlayFlow(source, "GCGameExample", "GCPlayerExample");
    }

    [Test]
    public void GeneratedActiveScenePlayerSourceUsesGCPlayerExampleAndSupportsColorPlaceholderPrefab()
    {
        var gameSpec = GamingCouchQuickStartSetup.GetScriptSetupSpec(
            GCQuickStartSetupIntent.ActiveScene,
            GCQuickStartSetupAction.ActiveSceneGameListener
        );
        var source = gameSpec.BuildPlayerScriptSource();

        AssertHasExampleTemplateHeader(source);
        AssertGeneratedPlayerSourceSupportsColorPlaceholderPrefab(source, "GCPlayerExample");
    }

    [Test]
    public void QuickStartPlayerPrefabWiresVisiblePlaceholderRendererToPlayerColorField()
    {
        ReserveQuickStartPrefabPathForTest();
        var context = new GCQuickStartSetupContinuationContext(
            GCQuickStartSetupIntent.ActiveScene,
            GCQuickStartSetupAction.ActiveScenePlayerPrefab,
            false,
            GamingCouchQuickStartSetup.QuickStartFolderAssetPath,
            GamingCouchQuickStartSetup.GameScriptAssetPath,
            GamingCouchQuickStartSetup.PlayerScriptAssetPath,
            nameof(CompatibleGameScriptReceiver),
            nameof(ColorPlaceholderPrefabPlayer),
            "Game",
            typeof(CompatibleGameScriptReceiver),
            typeof(ColorPlaceholderPrefabPlayer)
        );

        var result = GamingCouchQuickStartSetup.EnsureQuickStartPlayerPrefab(context);
        createdQuickStartPrefabForPrefabTest = result.changed;

        Assert.That(result.IsBlocked, Is.False, string.Join("\n", result.blockedReasons));
        Assert.That(result.changed, Is.True);
        Assert.That(result.prefab, Is.Not.Null);
        Assert.That(result.prefab.name, Is.EqualTo(nameof(ColorPlaceholderPrefabPlayer)));

        var player = result.prefab.GetComponent<ColorPlaceholderPrefabPlayer>();
        var visual = result.prefab.transform.Find("Visual");
        Assert.That(player, Is.Not.Null);
        Assert.That(visual, Is.Not.Null);

        var renderer = visual.GetComponent<Renderer>();
        Assert.That(renderer, Is.Not.Null);

        var serializedPlayer = new SerializedObject(player);
        var colorRendererProperty = serializedPlayer.FindProperty("colorRenderer");
        Assert.That(colorRendererProperty, Is.Not.Null);
        Assert.That(colorRendererProperty.objectReferenceValue, Is.SameAs(renderer));
    }

    [Test]
    public void GeneratedQuickStartSourceKeepsLegacyTypeNamesAndGCExampleAssetPaths()
    {
        var quickStartSpec = GamingCouchQuickStartSetup.GetScriptSetupSpec(
            GCQuickStartSetupIntent.QuickStartScene,
            GCQuickStartSetupAction.QuickStartScene
        );
        var gameSource = quickStartSpec.BuildGameScriptSource();
        var playerSource = quickStartSpec.BuildPlayerScriptSource();

        Assert.That(quickStartSpec.scriptFolderAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.ExampleFolderAssetPath));
        Assert.That(quickStartSpec.gameScriptAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCQuickStartGame.cs"));
        Assert.That(quickStartSpec.playerScriptAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCQuickStartPlayer.cs"));
        Assert.That(GamingCouchQuickStartSetup.PlayerPrefabAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCQuickStartPlayer.prefab"));
        Assert.That(GamingCouchQuickStartSetup.QuickStartSceneAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GamingCouchQuickStart.unity"));
        AssertGeneratedGameSourceDemonstratesPlayFlow(gameSource, "GCQuickStartGame", "GCQuickStartPlayer");
        AssertGeneratedPlayerSourceSupportsColorPlaceholderPrefab(playerSource, "GCQuickStartPlayer");
    }

    [Test]
    public void GameListenerSetupCreatesNamedGameObjectAndLeavesPlayerPrefabEmpty()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var context = CreateGameListenerTestContext();

        var listenerResult = GamingCouchQuickStartSetup.EnsureQuickStartGameListener(context, gamingCouch);
        var assignResult = GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listenerResult.listenerObject);

        Assert.That(listenerResult.IsBlocked, Is.False);
        Assert.That(listenerResult.changed, Is.True);
        Assert.That(listenerResult.listenerObject.name, Is.EqualTo("Game"));
        Assert.That(listenerResult.listenerObject.GetComponent<CompatibleGameScriptReceiver>(), Is.Not.Null);
        Assert.That(assignResult.status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.SameAs(listenerResult.listenerObject));
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName), Is.Null);
    }

    [Test]
    public void GameListenerSetupReusesNamedGameObjectBeforeAddingComponent()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var existingGame = new GameObject("Game");
        var context = CreateGameListenerTestContext();

        var listenerResult = GamingCouchQuickStartSetup.EnsureQuickStartGameListener(context, gamingCouch);

        Assert.That(listenerResult.IsBlocked, Is.False);
        Assert.That(listenerResult.changed, Is.True);
        Assert.That(listenerResult.listenerObject, Is.SameAs(existingGame));
        Assert.That(existingGame.GetComponent<CompatibleGameScriptReceiver>(), Is.Not.Null);
    }

    [Test]
    public void GameListenerSetupBlocksExistingGameComponentOnWrongObjectName()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var existingListener = CreateCompatibleListener("Existing Listener");
        var context = CreateGameListenerTestContext();

        var listenerResult = GamingCouchQuickStartSetup.EnsureQuickStartGameListener(context, gamingCouch);

        Assert.That(listenerResult.IsBlocked, Is.True);
        Assert.That(listenerResult.changed, Is.False);
        Assert.That(listenerResult.listenerObject, Is.Null);
        Assert.That(string.Join("\n", listenerResult.blockedReasons), Does.Contain("scene object named Game"));
        Assert.That(existingListener.GetComponent<CompatibleGameScriptReceiver>(), Is.Not.Null);
        Assert.That(testScene.GetRootGameObjects().Any(root => root != null && root.name == "Game"), Is.False);
    }

    [Test]
    public void GameListenerSetupPreservesOccupiedListenerField()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var existingListener = CreateCompatibleListener("Existing Listener");
        var context = CreateGameListenerTestContext();

        Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, existingListener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

        var listenerResult = GamingCouchQuickStartSetup.EnsureQuickStartGameListener(context, gamingCouch);

        Assert.That(listenerResult.IsBlocked, Is.False);
        Assert.That(listenerResult.changed, Is.False);
        Assert.That(listenerResult.listenerObject, Is.Null);
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.SameAs(existingListener));
        Assert.That(testScene.GetRootGameObjects().Any(root => root != null && root.name == "Game"), Is.False);
    }

    [Test]
    public void GameListenerSetupBlocksIncompatibleGameComponentType()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var context = CreateGameListenerTestContext(typeof(SetupOnlyGameScriptReceiver));

        var listenerResult = GamingCouchQuickStartSetup.EnsureQuickStartGameListener(context, gamingCouch);

        Assert.That(listenerResult.IsBlocked, Is.True);
        Assert.That(listenerResult.changed, Is.False);
        Assert.That(listenerResult.listenerObject, Is.Null);
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.Null);
        Assert.That(testScene.GetRootGameObjects().Any(root => root != null && root.name == "Game"), Is.False);
    }

    [Test]
    public void CreateAndWireGameBlocksGCExampleScriptPathCollisionsWithoutOverwrite()
    {
        ReserveGCExampleGameAndPlayerScriptPathsForCollisionTest();
        CreateScriptPathCollisionDirectory(
            GamingCouchQuickStartSetup.ActiveSceneGameScriptAssetPath,
            ref createdExampleGameScriptPathCollisionForTest
        );
        CreateScriptPathCollisionDirectory(
            GamingCouchQuickStartSetup.ActiveScenePlayerScriptAssetPath,
            ref createdExamplePlayerScriptPathCollisionForTest
        );
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CreateGamingCouch("GamingCouch");

        var result = GamingCouchQuickStartSetup.EnsureActiveSceneQuickStartGameListenerReference();

        Assert.That(result.IsBlocked, Is.True);
        Assert.That(result.changed, Is.False);
        Assert.That(result.status, Is.EqualTo(GCQuickStartActiveSceneSetupStatus.Blocked));
        AssertHasEntryContaining(result.details, "Cannot create script Assets/GamingCouch/GCExample/GCGameExample.cs because a folder exists at that path.");
        AssertHasEntryContaining(result.details, "Cannot create script Assets/GamingCouch/GCExample/GCPlayerExample.cs because a folder exists at that path.");
        Assert.That(testScene.GetRootGameObjects().Any(root => root != null && root.name == "Game"), Is.False);
    }

    [Test]
    public void CreateAndWireGameResultHandlingKeepsPendingCompilationVisibleAndReadySilent()
    {
        var getMessageType = typeof(GamingCouchStartScreenWindow)
            .GetMethod("GetActiveSceneResultMessageType", BindingFlags.Static | BindingFlags.NonPublic);
        var formatActionMessage = typeof(GamingCouchStartScreenWindow)
            .GetMethod("FormatActionMessage", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(getMessageType, Is.Not.Null);
        Assert.That(formatActionMessage, Is.Not.Null);
        var pendingResult = new GCQuickStartActiveSceneSetupResult(
            GCQuickStartActiveSceneSetupStatus.PendingCompilation,
            true,
            "Created missing quick-start scripts and queued setup continuation after Unity compiles them.",
            new[]
            {
                "Created: Assets/GamingCouch/GCExample/GCGameExample.cs",
                "Created: Assets/GamingCouch/GCExample/GCPlayerExample.cs",
            }
        );

        var pendingMessageType = (MessageType)getMessageType.Invoke(null, new object[] { pendingResult });
        var formattedPendingMessage = (string)formatActionMessage.Invoke(
            null,
            new object[] { pendingResult.message, pendingResult.details }
        );

        Assert.That(pendingMessageType, Is.EqualTo(MessageType.Warning));
        Assert.That(ShouldShowStartScreenActionResult(pendingMessageType), Is.True);
        AssertHasEntryContaining(pendingResult.details, "Created: Assets/GamingCouch/GCExample/GCGameExample.cs");
        AssertHasEntryContaining(pendingResult.details, "Created: Assets/GamingCouch/GCExample/GCPlayerExample.cs");
        Assert.That(formattedPendingMessage, Does.Contain("queued setup continuation"));
        Assert.That(formattedPendingMessage, Does.Contain("- Created: Assets/GamingCouch/GCExample/GCGameExample.cs"));
        Assert.That(formattedPendingMessage, Does.Contain("- Created: Assets/GamingCouch/GCExample/GCPlayerExample.cs"));

        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Game");
        var window = EditorWindow.CreateInstance<GamingCouchStartScreenWindow>();

        try
        {
            Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

            InvokeStartScreenRunEnsureGameListener(window);

            Assert.That(GetStartScreenActionMessage(window), Is.Null);
            Assert.That(GetStartScreenActionDetails(window), Is.Empty);
            Assert.That(GetStartScreenActionMessageType(window), Is.EqualTo(MessageType.Info));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(window);
        }
    }

    [Test]
    public void GeneratedAssetCreationReusesExistingFileWithoutOverwriting()
    {
        EnsureTestAssetFolder();
        var assetPath = testFolderAssetPath + "/ExistingGeneratedAsset.txt";
        var fullPath = AssetPathToFullPath(assetPath);
        const string OriginalContent = "user edits stay";
        var createdAssetPaths = new List<string>();
        var reusedAssetPaths = new List<string>();
        var blockedReasons = new List<string>();

        File.WriteAllText(fullPath, OriginalContent);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        GamingCouchQuickStartSetup.EnsureGeneratedAssetFileWithoutOverwrite(
            assetPath,
            "generated replacement",
            createdAssetPaths,
            reusedAssetPaths,
            blockedReasons
        );

        Assert.That(blockedReasons, Is.Empty);
        Assert.That(createdAssetPaths, Is.Empty);
        Assert.That(reusedAssetPaths, Is.EquivalentTo(new[] { assetPath }));
        Assert.That(File.ReadAllText(fullPath), Is.EqualTo(OriginalContent));
    }

    [Test]
    public void SuppressAutoOpenSettingPersistsTrueAndFalse()
    {
        GCStartScreenSettings.SuppressAutoOpen = false;
        Assert.That(GCStartScreenSettings.SuppressAutoOpen, Is.False);

        GCStartScreenSettings.SuppressAutoOpen = true;
        Assert.That(GCStartScreenSettings.SuppressAutoOpen, Is.True);

        GCStartScreenSettings.SuppressAutoOpen = false;
        Assert.That(GCStartScreenSettings.SuppressAutoOpen, Is.False);
    }

    [Test]
    public void BuildSettingsInsertionAddsQuickStartSceneWithoutDuplicates()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ExistingSceneBuildPath, false),
        };

        var firstInsertChanged = GamingCouchQuickStartSetup.AddSceneToBuildSettingsIfMissing(TestSceneBuildPath);
        var secondInsertChanged = GamingCouchQuickStartSetup.AddSceneToBuildSettingsIfMissing(TestSceneBuildPath);
        var matchingScenes = EditorBuildSettings.scenes
            .Where(scene => scene != null && scene.path == TestSceneBuildPath)
            .ToArray();
        var existingScenes = EditorBuildSettings.scenes
            .Where(scene => scene != null && scene.path == ExistingSceneBuildPath)
            .ToArray();

        Assert.That(firstInsertChanged, Is.True);
        Assert.That(secondInsertChanged, Is.False);
        Assert.That(matchingScenes, Has.Length.EqualTo(1));
        Assert.That(matchingScenes[0].enabled, Is.True);
        Assert.That(existingScenes, Has.Length.EqualTo(1));
        Assert.That(existingScenes[0].enabled, Is.False);
    }

    [Test]
    public void BuildSettingsReadinessReportsActiveSceneFirstEnabledState()
    {
        var readiness = GamingCouchBuildSettingsReadiness.InspectScenePath(
            true,
            TestSceneBuildPath,
            new[] { new EditorBuildSettingsScene(TestSceneBuildPath, true) }
        );

        Assert.That(readiness.IsReady, Is.True);
        Assert.That(readiness.status, Is.EqualTo(GCActiveSceneBuildSettingsStatus.Ready));
        Assert.That(readiness.firstMatchingIndex, Is.EqualTo(0));
        Assert.That(readiness.matchingEntryCount, Is.EqualTo(1));
    }

    [Test]
    public void BuildSettingsReadinessReportsNonReadyStates()
    {
        Assert.That(
            GamingCouchBuildSettingsReadiness.InspectScenePath(true, null, Array.Empty<EditorBuildSettingsScene>()).status,
            Is.EqualTo(GCActiveSceneBuildSettingsStatus.UnsavedActiveScene)
        );
        Assert.That(
            GamingCouchBuildSettingsReadiness.InspectScenePath(
                true,
                TestSceneBuildPath,
                new[] { new EditorBuildSettingsScene(ExistingSceneBuildPath, true) }
            ).status,
            Is.EqualTo(GCActiveSceneBuildSettingsStatus.Missing)
        );
        Assert.That(
            GamingCouchBuildSettingsReadiness.InspectScenePath(
                true,
                TestSceneBuildPath,
                new[] { new EditorBuildSettingsScene(TestSceneBuildPath, false) }
            ).status,
            Is.EqualTo(GCActiveSceneBuildSettingsStatus.Disabled)
        );
        Assert.That(
            GamingCouchBuildSettingsReadiness.InspectScenePath(
                true,
                TestSceneBuildPath,
                new[]
                {
                    new EditorBuildSettingsScene(ExistingSceneBuildPath, true),
                    new EditorBuildSettingsScene(TestSceneBuildPath, true),
                }
            ).status,
            Is.EqualTo(GCActiveSceneBuildSettingsStatus.NotFirst)
        );
        Assert.That(
            GamingCouchBuildSettingsReadiness.InspectScenePath(
                true,
                TestSceneBuildPath,
                new[]
                {
                    new EditorBuildSettingsScene(TestSceneBuildPath, true),
                    new EditorBuildSettingsScene(OtherSceneBuildPath, true),
                    new EditorBuildSettingsScene(TestSceneBuildPath, false),
                }
            ).status,
            Is.EqualTo(GCActiveSceneBuildSettingsStatus.Duplicate)
        );
    }

    [Test]
    public void BuildSettingsSetupMovesActiveSceneFirstEnabledAndPreservesUnrelatedScenes()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ExistingSceneBuildPath, false),
            new EditorBuildSettingsScene(TestSceneBuildPath, false),
            new EditorBuildSettingsScene(OtherSceneBuildPath, true),
            new EditorBuildSettingsScene(TestSceneBuildPath, true),
        };

        var result = GamingCouchBuildSettingsReadiness.SetSceneFirstEnabled(TestSceneBuildPath);
        var scenes = EditorBuildSettings.scenes;

        Assert.That(result.IsBlocked, Is.False);
        Assert.That(result.changed, Is.True);
        Assert.That(scenes.Select(scene => scene.path).ToArray(), Is.EqualTo(new[]
        {
            TestSceneBuildPath,
            ExistingSceneBuildPath,
            OtherSceneBuildPath,
        }));
        Assert.That(scenes.Select(scene => scene.enabled).ToArray(), Is.EqualTo(new[]
        {
            true,
            false,
            true,
        }));
    }

    [Test]
    public void ActiveSceneSetupStillNormalizesBuildSettingsWhenGamingCouchWiringBlocked()
    {
        EnsureTestAssetFolder();
        var sceneAssetPath = testFolderAssetPath + "/DuplicateGamingCouchScene.unity";
        var previousScene = SceneManager.GetActiveScene();
        var launchScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        try
        {
            EnsureSceneIsActive(launchScene);
            Assert.That(EditorSceneManager.SaveScene(launchScene, sceneAssetPath), Is.True);
            CreateGamingCouch("GamingCouch A");
            CreateGamingCouch("GamingCouch B");
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ExistingSceneBuildPath, false),
                new EditorBuildSettingsScene(sceneAssetPath, false),
                new EditorBuildSettingsScene(OtherSceneBuildPath, true),
                new EditorBuildSettingsScene(sceneAssetPath, true),
            };

            var result = GamingCouchQuickStartSetup.EnsureActiveSceneQuickStartSetup();
            var scenes = EditorBuildSettings.scenes;

            Assert.That(result.IsBlocked, Is.True);
            Assert.That(result.changed, Is.True);
            Assert.That(
                result.details.Any(detail => detail != null && detail.IndexOf("multiple GamingCouch", StringComparison.Ordinal) >= 0),
                Is.True
            );
            Assert.That(scenes.Select(scene => scene.path).ToArray(), Is.EqualTo(new[]
            {
                sceneAssetPath,
                ExistingSceneBuildPath,
                OtherSceneBuildPath,
            }));
            Assert.That(scenes.Select(scene => scene.enabled).ToArray(), Is.EqualTo(new[]
            {
                true,
                false,
                true,
            }));
        }
        finally
        {
            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousScene);
            }
            else if (launchScene.IsValid() && launchScene.isLoaded)
            {
                SetAnyLoadedSceneActiveExcept(launchScene);
            }

            if (launchScene.IsValid() && launchScene.isLoaded)
            {
                ClearSceneRootObjects(launchScene);
                EditorSceneManager.CloseScene(launchScene, true);
            }
        }
    }

    [Test]
    public void GameViewAspectLogicReportsReadyMismatchAndUnknownStates()
    {
        var entries = new[]
        {
            new GCGameViewSizeEntry(0, "Free Aspect", 0, 0, false),
            new GCGameViewSizeEntry(1, "16:9 Aspect", 16, 9, true),
            new GCGameViewSizeEntry(2, "1920x1080", 0, 0, false),
        };

        var ready = GamingCouchGameViewAspect.InspectSizeEntries(entries, 1, true, null);
        var mismatch = GamingCouchGameViewAspect.InspectSizeEntries(entries, 0, true, null);
        var unknown = GamingCouchGameViewAspect.InspectSizeEntries(entries, -1, true, "Game View is closed.");
        var mismatchWithoutExisting16By9 = GamingCouchGameViewAspect.InspectSizeEntries(
            new[] { new GCGameViewSizeEntry(0, "4:3 Aspect", 4, 3, true) },
            0,
            true,
            null
        );

        Assert.That(ready.status, Is.EqualTo(GCGameViewAspectStatus.Ready));
        Assert.That(ready.HasSafeSelectionAction, Is.False);
        Assert.That(mismatch.status, Is.EqualTo(GCGameViewAspectStatus.Mismatch));
        Assert.That(mismatch.HasSafeSelectionAction, Is.True);
        Assert.That(mismatch.existing16By9Entry.index, Is.EqualTo(1));
        Assert.That(unknown.status, Is.EqualTo(GCGameViewAspectStatus.Unknown));
        Assert.That(unknown.HasSafeSelectionAction, Is.True);
        Assert.That(mismatchWithoutExisting16By9.status, Is.EqualTo(GCGameViewAspectStatus.Mismatch));
        Assert.That(mismatchWithoutExisting16By9.HasSafeSelectionAction, Is.False);
        Assert.That(GamingCouchGameViewAspect.Is16By9(entries[2]), Is.True);
    }

    [Test]
    public void BlockingChecklistReadinessIgnoresWarningRows()
    {
        var warningOnly = new[]
        {
            new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.GameViewAspect16By9,
                "Game View uses 16:9 preview",
                GCStartScreenReadinessCheckState.Warning,
                "Game View could not be inspected."
            ),
        };
        var blocking = new[]
        {
            new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.ActiveSceneFirstBuildSettingsScene,
                "Active scene is first Build Settings scene",
                GCStartScreenReadinessCheckState.Fail,
                "The active scene is not in Build Settings."
            ),
        };

        Assert.That(GCStartScreenReadiness.HasBlockingChecklistIssues(warningOnly), Is.False);
        Assert.That(GCStartScreenReadiness.HasBlockingChecklistIssues(blocking), Is.True);
    }

    [Test]
    public void StartScreenReadinessSummaryReportsNoPendingItemsWhenReady()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Listener");
        var playerPrefab = CreatePlayerPrefabObject("Existing Player Prefab");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);
        var readiness = CreateReadyStartScreenReadiness(gamingCouch, listener, playerPrefab);

        var summary = GCStartScreenReadinessSummary.Create(readiness, false);

        Assert.That(summary.state, Is.EqualTo(GCStartScreenReadinessSummaryState.Ready));
        Assert.That(summary.HasPendingItems, Is.False);
        Assert.That(summary.blockerCount, Is.EqualTo(0));
        Assert.That(summary.warningCount, Is.EqualTo(0));
        Assert.That(summary.actionableSetupCount, Is.EqualTo(0));
        Assert.That(summary.message, Is.EqualTo("Start Screen: no pending setup items."));
    }

    [Test]
    public void StartScreenReadinessSummaryCountsBlockersWarningsAndActions()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var warningReadiness = new GCWebGLExportReadiness(
            GCWebGLExportSetupStatus.Warning,
            true,
            true,
            true,
            true,
            true,
            false,
            "Clean WebGL export setup is ready, but the active build target is not WebGL.",
            Array.Empty<string>()
        );
        var readiness = new GCStartScreenReadiness(
            testScene,
            new[] { gamingCouch },
            gamingCouch,
            null,
            null,
            CreateValidLocalPlayJsonReadiness(),
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            warningReadiness
        );

        var summary = GCStartScreenReadinessSummary.Create(readiness, false);

        Assert.That(summary.state, Is.EqualTo(GCStartScreenReadinessSummaryState.Actionable));
        Assert.That(summary.HasPendingItems, Is.True);
        Assert.That(summary.blockerCount, Is.EqualTo(0));
        Assert.That(summary.warningCount, Is.EqualTo(1));
        Assert.That(summary.actionableSetupCount, Is.EqualTo(2));
        Assert.That(summary.message, Is.EqualTo("Start Screen: 2 setup actions, 1 warning."));
    }

    [Test]
    public void StartScreenReadinessSummaryPrioritizesPendingCompilation()
    {
        var summary = GCStartScreenReadinessSummary.Create(null, true);

        Assert.That(summary.state, Is.EqualTo(GCStartScreenReadinessSummaryState.PendingCompilation));
        Assert.That(summary.HasPendingItems, Is.True);
        Assert.That(summary.hasPendingCompilation, Is.True);
        Assert.That(summary.message, Does.Contain("waiting for Unity"));
    }

    [Test]
    public void WebGLExportChecklistRowReportsWarningWithoutSceneSetupAction()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Listener");
        var playerPrefab = CreatePlayerPrefabObject("Existing Player Prefab");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);

        var warningReadiness = new GCWebGLExportReadiness(
            GCWebGLExportSetupStatus.Warning,
            true,
            true,
            true,
            true,
            true,
            false,
            "Clean WebGL export setup is ready, but the active build target is not WebGL.",
            Array.Empty<string>()
        );

        var readiness = new GCStartScreenReadiness(
            testScene,
            new[] { gamingCouch },
            gamingCouch,
            listener,
            playerPrefab,
            CreateValidLocalPlayJsonReadiness(),
            GamingCouchBuildSettingsReadiness.InspectScenePath(
                true,
                TestSceneBuildPath,
                new[] { new EditorBuildSettingsScene(TestSceneBuildPath, true) }
            ),
            GamingCouchGameViewAspect.InspectSizeEntries(
                new[] { new GCGameViewSizeEntry(0, "16:9 Aspect", 16, 9, true) },
                0,
                true,
                null
            ),
            warningReadiness
        );

        AssertCheck(readiness, GCStartScreenReadinessCheckId.WebGLExportSetup, GCStartScreenReadinessCheckState.Warning);
        Assert.That(readiness.GetCheck(GCStartScreenReadinessCheckId.WebGLExportSetup).IsSatisfied, Is.True);
        Assert.That(readiness.HasBlockingVisibleChecklistIssues, Is.False);
        Assert.That(readiness.HasSafeAutomatableSetupActions, Is.False);
    }

    [Test]
    public void WebGLExportChecklistRowBlockedDoesNotEnterGlobalSceneSetupAction()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Listener");
        var playerPrefab = CreatePlayerPrefabObject("Existing Player Prefab");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);

        var blockedReadiness = new GCWebGLExportReadiness(
            GCWebGLExportSetupStatus.Blocked,
            false,
            false,
            false,
            false,
            false,
            true,
            "Clean WebGL export setup is incomplete.",
            Array.Empty<string>()
        );

        var readiness = new GCStartScreenReadiness(
            testScene,
            new[] { gamingCouch },
            gamingCouch,
            listener,
            playerPrefab,
            CreateValidLocalPlayJsonReadiness(),
            GamingCouchBuildSettingsReadiness.InspectScenePath(
                true,
                TestSceneBuildPath,
                new[] { new EditorBuildSettingsScene(TestSceneBuildPath, true) }
            ),
            GamingCouchGameViewAspect.InspectSizeEntries(
                new[] { new GCGameViewSizeEntry(0, "16:9 Aspect", 16, 9, true) },
                0,
                true,
                null
            ),
            blockedReadiness
        );

        AssertCheck(readiness, GCStartScreenReadinessCheckId.WebGLExportSetup, GCStartScreenReadinessCheckState.Blocked);
        Assert.That(readiness.HasBlockingVisibleChecklistIssues, Is.True);
        Assert.That(readiness.HasSafeAutomatableSetupActions, Is.False);
        Assert.That(readiness.AvailableChecklistSetupActionCount, Is.EqualTo(1));
    }

    [Test]
    public void WebGLExportChecklistRowMapsReadyAndNullReadinessStates()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Listener");
        var playerPrefab = CreatePlayerPrefabObject("Existing Player Prefab");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);

        var readyReadiness = CreateStartScreenReadinessWithWebGL(
            gamingCouch,
            listener,
            playerPrefab,
            CreateReadyWebGLExportReadiness()
        );
        var uninspectableReadiness = CreateStartScreenReadinessWithWebGL(
            gamingCouch,
            listener,
            playerPrefab,
            null
        );
        var readyWebGLCheck = readyReadiness.GetCheck(GCStartScreenReadinessCheckId.WebGLExportSetup);
        var uninspectableWebGLCheck = uninspectableReadiness.GetCheck(GCStartScreenReadinessCheckId.WebGLExportSetup);

        AssertCheck(readyReadiness, GCStartScreenReadinessCheckId.WebGLExportSetup, GCStartScreenReadinessCheckState.Pass);
        Assert.That(readyWebGLCheck.label, Is.EqualTo("WebGL export settings configured"));
        Assert.That(readyWebGLCheck.IsSatisfied, Is.True);
        AssertCheck(uninspectableReadiness, GCStartScreenReadinessCheckId.WebGLExportSetup, GCStartScreenReadinessCheckState.Fail);
        Assert.That(uninspectableWebGLCheck.label, Is.EqualTo("WebGL export settings configured"));
        Assert.That(uninspectableWebGLCheck.message, Does.Contain("could not be inspected"));
    }

    [Test]
    public void WebGLTemplateInstallationCreatesMissingProjectTemplateFiles()
    {
        var sourceDirectory = CreateTemporaryWebGLTemplateSource("fresh template source");
        var destinationDirectory = CreateTemporaryPath("WebGLTemplateDestination");

        try
        {
            var result = GamingCouchWebGLExportSetup.InstallTemplateFiles(sourceDirectory, destinationDirectory, false);

            Assert.That(result.IsBlocked, Is.False);
            Assert.That(result.changed, Is.True);
            Assert.That(Directory.Exists(destinationDirectory), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(destinationDirectory, "index.html")), Is.EqualTo("fresh template source"));
            Assert.That(result.createdPaths.Any(path => path.Contains("index.html")), Is.True);
        }
        finally
        {
            DeleteTemporaryPath(sourceDirectory);
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void WebGLTemplateInstallationRerunReusesExistingFilesWithoutOverwriting()
    {
        var sourceDirectory = CreateTemporaryWebGLTemplateSource("generated replacement");
        var destinationDirectory = CreateTemporaryPath("WebGLTemplateDestination");

        try
        {
            Directory.CreateDirectory(destinationDirectory);
            File.WriteAllText(Path.Combine(destinationDirectory, "index.html"), "user edits stay");

            var result = GamingCouchWebGLExportSetup.InstallTemplateFiles(sourceDirectory, destinationDirectory, false);

            Assert.That(result.IsBlocked, Is.False);
            Assert.That(result.changed, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(destinationDirectory, "index.html")), Is.EqualTo("user edits stay"));
            Assert.That(result.reusedPaths.Any(path => path.Contains("index.html")), Is.True);
        }
        finally
        {
            DeleteTemporaryPath(sourceDirectory);
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void WebGLTemplateInstallationBlocksFileAndFolderCollisions()
    {
        var sourceDirectory = CreateTemporaryWebGLTemplateSource("template source");
        var destinationAsFile = CreateTemporaryPath("WebGLTemplateDestinationFile");
        var destinationWithFileParent = CreateTemporaryPath("WebGLTemplateParentCollision");
        var destinationWithFileCollision = CreateTemporaryPath("WebGLTemplateFileCollision");

        try
        {
            File.WriteAllText(destinationAsFile, "not a folder");
            File.WriteAllText(destinationWithFileParent, "not a parent folder");
            Directory.CreateDirectory(destinationWithFileCollision);
            Directory.CreateDirectory(Path.Combine(destinationWithFileCollision, "index.html"));

            var fileDestinationResult = GamingCouchWebGLExportSetup.InstallTemplateFiles(sourceDirectory, destinationAsFile, false);
            var parentCollisionResult = GamingCouchWebGLExportSetup.InstallTemplateFiles(
                sourceDirectory,
                Path.Combine(destinationWithFileParent, "GamingCouch"),
                false
            );
            var fileCollisionResult = GamingCouchWebGLExportSetup.InstallTemplateFiles(sourceDirectory, destinationWithFileCollision, false);

            Assert.That(fileDestinationResult.IsBlocked, Is.True);
            Assert.That(parentCollisionResult.IsBlocked, Is.True);
            Assert.That(fileCollisionResult.IsBlocked, Is.True);
            AssertHasEntryContaining(fileDestinationResult.blockedReasons, "Expected a folder");
            AssertHasEntryContaining(parentCollisionResult.blockedReasons, "Expected a folder");
            AssertHasEntryContaining(fileCollisionResult.blockedReasons, "Expected a file");
        }
        finally
        {
            DeleteTemporaryPath(sourceDirectory);
            DeleteTemporaryPath(destinationAsFile);
            DeleteTemporaryPath(destinationWithFileParent);
            DeleteTemporaryPath(destinationWithFileCollision);
        }
    }

    [Test]
    public void WebGLReadinessReportsSelectedTemplateAndWrongTemplateFailure()
    {
        var destinationDirectory = CreateTemporaryInstalledWebGLTemplate("installed template");

        try
        {
            ApplyReadyWebGLExportSettings();
            PlayerSettings.WebGL.template = "PROJECT:OtherTemplate";

            var wrongTemplateReadiness = GamingCouchWebGLExportSetup.InspectReadiness(destinationDirectory);

            Assert.That(wrongTemplateReadiness.IsBlocked, Is.True);
            Assert.That(wrongTemplateReadiness.templateSelected, Is.False);
            AssertHasEntryContaining(wrongTemplateReadiness.details, "PROJECT:OtherTemplate");

            PlayerSettings.WebGL.template = GamingCouchWebGLExportSetup.ProjectTemplateIdentifier;

            var selectedTemplateReadiness = GamingCouchWebGLExportSetup.InspectReadiness(destinationDirectory);

            Assert.That(selectedTemplateReadiness.IsBlocked, Is.False);
            Assert.That(selectedTemplateReadiness.templateSelected, Is.True);
        }
        finally
        {
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void WebGLReadinessReportsReleaseSettingDrift()
    {
        var destinationDirectory = CreateTemporaryInstalledWebGLTemplate("installed template");

        try
        {
            ApplyReadyWebGLExportSettings();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;

            var readiness = GamingCouchWebGLExportSetup.InspectReadiness(destinationDirectory);

            Assert.That(readiness.IsBlocked, Is.True);
            Assert.That(readiness.releaseSettingsReady, Is.False);
            AssertHasEntryContaining(readiness.details, "WebGL compression is not disabled.");
        }
        finally
        {
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void WebGLSetupInspectsAcceptedSplashAndLogoValues()
    {
        var sourceDirectory = CreateTemporaryWebGLTemplateSource("template source");
        var destinationDirectory = CreateTemporaryPath("WebGLTemplateDestination");

        try
        {
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = true;

            var result = GamingCouchWebGLExportSetup.EnsureCleanWebGLExportSetup(sourceDirectory, destinationDirectory, false);

            Assert.That(result.IsBlocked, Is.False);
            Assert.That(result.readiness.splashSettingsReady, Is.True);
            Assert.That(PlayerSettings.SplashScreen.show, Is.False);
            Assert.That(PlayerSettings.SplashScreen.showUnityLogo, Is.False);
        }
        finally
        {
            DeleteTemporaryPath(sourceDirectory);
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void WebGLReadinessWarnsButDoesNotBlockWhenActiveBuildTargetIsNotWebGL()
    {
        var destinationDirectory = CreateTemporaryInstalledWebGLTemplate("installed template");

        try
        {
            ApplyReadyWebGLExportSettings();

            var readiness = GamingCouchWebGLExportSetup.InspectReadiness(destinationDirectory, BuildTarget.NoTarget);

            Assert.That(readiness.status, Is.EqualTo(GCWebGLExportSetupStatus.Warning));
            Assert.That(readiness.IsBlocked, Is.False);
            Assert.That(readiness.activeBuildTargetIsWebGL, Is.False);
            AssertHasEntryContaining(readiness.details, "switch to WebGL manually");
            Assert.That(readiness.templateFolderReady, Is.True);
            Assert.That(readiness.templateFilesReady, Is.True);
            Assert.That(readiness.templateSelected, Is.True);
            Assert.That(readiness.releaseSettingsReady, Is.True);
            Assert.That(readiness.splashSettingsReady, Is.True);
        }
        finally
        {
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void SetupActionAvailabilityIncludesLaunchReadinessRows()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = CreateCompatibleListener("Existing Listener");
        var playerPrefab = CreatePlayerPrefabObject("Existing Player Prefab");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);

        var buildSettingsMissing = GamingCouchBuildSettingsReadiness.InspectScenePath(
            true,
            TestSceneBuildPath,
            Array.Empty<EditorBuildSettingsScene>()
        );
        var gameViewReady = GamingCouchGameViewAspect.InspectSizeEntries(
            new[] { new GCGameViewSizeEntry(0, "16:9 Aspect", 16, 9, true) },
            0,
            true,
            null
        );
        var buildSettingsOnlyReadiness = new GCStartScreenReadiness(
            testScene,
            new[] { gamingCouch },
            gamingCouch,
            listener,
            playerPrefab,
            null,
            buildSettingsMissing,
            gameViewReady,
            CreateReadyWebGLExportReadiness()
        );

        var buildSettingsReady = GamingCouchBuildSettingsReadiness.InspectScenePath(
            true,
            TestSceneBuildPath,
            new[] { new EditorBuildSettingsScene(TestSceneBuildPath, true) }
        );
        var gameViewMismatch = GamingCouchGameViewAspect.InspectSizeEntries(
            new[]
            {
                new GCGameViewSizeEntry(0, "4:3 Aspect", 4, 3, true),
                new GCGameViewSizeEntry(1, "16:9 Aspect", 16, 9, true),
            },
            0,
            true,
            null
        );
        var gameViewOnlyReadiness = new GCStartScreenReadiness(
            testScene,
            new[] { gamingCouch },
            gamingCouch,
            listener,
            playerPrefab,
            null,
            buildSettingsReady,
            gameViewMismatch,
            CreateReadyWebGLExportReadiness()
        );

        Assert.That(buildSettingsOnlyReadiness.HasSafeAutomatableSetupActions, Is.True);
        Assert.That(gameViewOnlyReadiness.HasSafeAutomatableSetupActions, Is.True);
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

    private static GCQuickStartSetupContinuationContext CreateGameListenerTestContext()
    {
        return CreateGameListenerTestContext(typeof(CompatibleGameScriptReceiver));
    }

    private static GCQuickStartSetupContinuationContext CreateGameListenerTestContext(Type gameType)
    {
        return new GCQuickStartSetupContinuationContext(
            GCQuickStartSetupIntent.ActiveScene,
            GCQuickStartSetupAction.ActiveSceneGameListener,
            false,
            GamingCouchQuickStartSetup.ExampleFolderAssetPath,
            GamingCouchQuickStartSetup.ActiveSceneGameScriptAssetPath,
            GamingCouchQuickStartSetup.ActiveScenePlayerScriptAssetPath,
            gameType.Name,
            nameof(GCPlayer),
            "Game",
            gameType,
            typeof(GCPlayer)
        );
    }

    private static void AssertCheck(
        GCStartScreenReadiness readiness,
        GCStartScreenReadinessCheckId id,
        GCStartScreenReadinessCheckState state
    )
    {
        Assert.That(readiness.GetCheck(id).state, Is.EqualTo(state));
    }

    private static void AssertHasEntryContaining(IEnumerable<string> entries, string expectedSubstring)
    {
        Assert.That(
            entries != null && entries.Any(entry =>
                entry != null && entry.IndexOf(expectedSubstring, StringComparison.Ordinal) >= 0
            ),
            Is.True
        );
    }

    private static void AssertGeneratedGameSourceDemonstratesPlayFlow(
        string source,
        string gameTypeName,
        string playerTypeName
    )
    {
        Assert.That(source, Does.Contain("public class " + gameTypeName + " : MonoBehaviour"));
        Assert.That(source, Does.Contain("private void GamingCouchSetup(GCSetupOptions options)"));
        Assert.That(source, Does.Contain("GamingCouch.Instance.SetupGameVersus(new GCGameVersusSetupOptions"));
        Assert.That(source, Does.Contain("GamingCouch.Instance.SetupDone();"));
        Assert.That(source, Does.Contain("private void GamingCouchPlay(GCPlayOptions options)"));
        Assert.That(source, Does.Contain("GamingCouch.Instance.SetupPlayers<" + playerTypeName + ">(options.players"));
        Assert.That(source, Does.Contain("players.AddPlayer(player);"));
        Assert.That(source, Does.Contain("player.ApplyPlayerColor();"));
        Assert.That(source, Does.Contain("roundCoroutine = StartCoroutine(RunRound());"));
        Assert.That(source, Does.Contain("private IEnumerator RunRound()"));
        Assert.That(source, Does.Contain("yield return new WaitForSeconds(Mathf.Max(0.1f, roundSeconds));"));
        Assert.That(source, Does.Contain("private void ApplyRandomFinalScores()"));
        Assert.That(source, Does.Contain("Random.Range(0, clampedMaxScore + 1)"));
        Assert.That(source, Does.Contain("player.SetScore("));
        Assert.That(source, Does.Contain("player.SetFinished("));
        Assert.That(source, Does.Contain("GamingCouch.Instance.GameOver();"));
    }

    private static void AssertGeneratedPlayerSourceSupportsColorPlaceholderPrefab(
        string source,
        string playerTypeName
    )
    {
        Assert.That(source, Does.Contain("public class " + playerTypeName + " : GCPlayer"));
        Assert.That(source, Does.Contain("private Renderer colorRenderer;"));
        Assert.That(source, Does.Contain("private void Reset()"));
        Assert.That(source, Does.Contain("private void OnValidate()"));
        Assert.That(source, Does.Contain("private void Start()"));
        Assert.That(source, Does.Contain("public void ApplyPlayerColor()"));
        Assert.That(source, Does.Contain("colorRenderer.material.color = ColorBase;"));
        Assert.That(source, Does.Contain("colorRenderer = GetComponentInChildren<Renderer>();"));
        Assert.That(source, Does.Contain("public override string GetHudValueText()"));
        Assert.That(source, Does.Contain("return Score.ToString();"));
    }

    private static void AssertHasExampleTemplateHeader(string source)
    {
        Assert.That(source, Does.StartWith("/*\n * GamingCouch example template file."));
        Assert.That(source, Does.Contain("Move this script into your project's own scripts folder"));
        Assert.That(source, Does.Contain("rename the file and class to fit your project"));
        Assert.That(source, Does.Contain("Game.cs/Game"));
        Assert.That(source, Does.Contain("Player.cs/Player"));
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

    private GCStartScreenReadiness CreateStartScreenReadinessWithWebGL(
        GamingCouch gamingCouch,
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab,
        GCWebGLExportReadiness webGLExport
    )
    {
        return new GCStartScreenReadiness(
            testScene,
            new[] { gamingCouch },
            gamingCouch,
            listener,
            playerPrefab,
            CreateValidLocalPlayJsonReadiness(),
            GamingCouchBuildSettingsReadiness.InspectScenePath(
                true,
                TestSceneBuildPath,
                new[] { new EditorBuildSettingsScene(TestSceneBuildPath, true) }
            ),
            GamingCouchGameViewAspect.InspectSizeEntries(
                new[] { new GCGameViewSizeEntry(0, "16:9 Aspect", 16, 9, true) },
                0,
                true,
                null
            ),
            webGLExport
        );
    }

    private GCStartScreenReadiness CreateReadyStartScreenReadiness(
        GamingCouch gamingCouch,
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab
    )
    {
        return new GCStartScreenReadiness(
            testScene,
            new[] { gamingCouch },
            gamingCouch,
            listener,
            playerPrefab,
            CreateValidLocalPlayJsonReadiness(),
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            CreateReadyWebGLExportReadiness()
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

    private string CreateTemporaryWebGLTemplateSource(string indexContent)
    {
        var sourceDirectory = CreateTemporaryProjectPath("WebGLTemplateSource");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "index.html"), indexContent);
        return sourceDirectory;
    }

    private string CreateTemporaryInstalledWebGLTemplate(string indexContent)
    {
        var destinationDirectory = CreateTemporaryProjectPath("WebGLTemplateDestination");
        Directory.CreateDirectory(destinationDirectory);
        File.WriteAllText(Path.Combine(destinationDirectory, "index.html"), indexContent);
        return destinationDirectory;
    }

    private string CreateTemporaryPath(string prefix)
    {
        return CreateTemporaryProjectPath(prefix);
    }

    private string CreateTemporaryProjectPath(string prefix)
    {
        EnsureTestAssetFolder();
        return Path.Combine(
            AssetPathToFullPathUnchecked(testFolderAssetPath),
            prefix + "_" + Guid.NewGuid().ToString("N")
        );
    }

    private static void DeleteTemporaryPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void ApplyReadyWebGLExportSettings()
    {
        PlayerSettings.WebGL.template = GamingCouchWebGLExportSetup.ProjectTemplateIdentifier;
        GamingCouchWebGLBuildSettingsProfiles.ApplyReleaseProfile();
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
    }

    private static void AssertCollapsedGamingCouchChecklist(GCStartScreenReadiness readiness)
    {
        var checklistIds = readiness.checklist.Select(check => check.id).ToArray();
        var gamingCouchRows = readiness.checklist
            .Where(check => check.id == GCStartScreenReadinessCheckId.GamingCouchInstance)
            .ToArray();
        var checklistLabels = readiness.checklist.Select(check => check.label).ToArray();

        Assert.That(gamingCouchRows, Has.Length.EqualTo(1));
        Assert.That(gamingCouchRows[0].label, Is.EqualTo("GamingCouch game object in scene"));
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ActiveScene, GCStartScreenReadinessCheckState.Pass);
        Assert.That(checklistIds, Has.No.Member(GCStartScreenReadinessCheckId.ActiveScene));
        Assert.That(checklistIds, Has.Member(GCStartScreenReadinessCheckId.GamingCouchInstance));
        Assert.That(checklistIds, Has.No.Member(GCStartScreenReadinessCheckId.SingleGamingCouchInstance));
        Assert.That(HasActiveSceneChecklistLabel(checklistLabels), Is.False);
        Assert.That(
            readiness.GetCheck(GCStartScreenReadinessCheckId.SingleGamingCouchInstance),
            Is.SameAs(readiness.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance))
        );
    }

    private static bool HasActiveSceneChecklistLabel(string[] checklistLabels)
    {
        return checklistLabels.Any(label => string.Equals(label, "Active scene is available", StringComparison.Ordinal));
    }

    private static void SetStartScreenWindowReadiness(
        GamingCouchStartScreenWindow window,
        GCStartScreenReadiness readiness
    )
    {
        typeof(GamingCouchStartScreenWindow)
            .GetField("readiness", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(window, readiness);
    }

    private static string GetChecklistActionLabel(
        GamingCouchStartScreenWindow window,
        GCStartScreenReadinessCheck check
    )
    {
        return (string)typeof(GamingCouchStartScreenWindow)
            .GetMethod("GetChecklistActionLabel", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(window, new object[] { check });
    }

    private static bool ShouldShowStartScreenActionResult(MessageType messageType)
    {
        var shouldShowActionResult = typeof(GamingCouchStartScreenWindow)
            .GetMethod("ShouldShowActionResult", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(shouldShowActionResult, Is.Not.Null);

        return (bool)shouldShowActionResult.Invoke(null, new object[] { messageType });
    }

    private static void InvokeStartScreenRunEnsureGameListener(GamingCouchStartScreenWindow window)
    {
        var method = typeof(GamingCouchStartScreenWindow)
            .GetMethod("RunEnsureGameListener", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        method.Invoke(window, null);
    }

    private static string GetStartScreenActionMessage(GamingCouchStartScreenWindow window)
    {
        var field = typeof(GamingCouchStartScreenWindow)
            .GetField("actionMessage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);

        return (string)field.GetValue(window);
    }

    private static MessageType GetStartScreenActionMessageType(GamingCouchStartScreenWindow window)
    {
        var field = typeof(GamingCouchStartScreenWindow)
            .GetField("actionMessageType", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);

        return (MessageType)field.GetValue(window);
    }

    private static string[] GetStartScreenActionDetails(GamingCouchStartScreenWindow window)
    {
        var field = typeof(GamingCouchStartScreenWindow)
            .GetField("actionDetails", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);

        return (string[])field.GetValue(window);
    }

    private void ReserveGCExampleGameAndPlayerScriptPathsForCollisionTest()
    {
        ReserveGeneratedScriptPathForCollisionTest(GamingCouchQuickStartSetup.ActiveSceneGameScriptAssetPath);
        ReserveGeneratedScriptPathForCollisionTest(GamingCouchQuickStartSetup.ActiveScenePlayerScriptAssetPath);
        EnsureGCExampleFolderForTest(
            ref createdExampleProjectFolderForCollisionTest,
            ref createdExampleFolderForCollisionTest
        );
    }

    private static void ReserveGeneratedScriptPathForCollisionTest(string assetPath)
    {
        var fullPath = AssetPathToFullPathUnchecked(assetPath);
        if (Directory.Exists(fullPath) || File.Exists(fullPath))
        {
            Assert.Ignore("Skipping generated script path collision test because " + assetPath + " already exists on disk.");
        }

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
        {
            Assert.Ignore("Skipping generated script path collision test because " + assetPath + " already exists.");
        }
    }

    private static void CreateScriptPathCollisionDirectory(string assetPath, ref bool created)
    {
        Directory.CreateDirectory(AssetPathToFullPathUnchecked(assetPath));
        created = true;
    }

    private void EnsureTestAssetFolder()
    {
        if (AssetDatabase.IsValidFolder(testFolderAssetPath))
        {
            return;
        }

        var folderName = testFolderAssetPath.Substring("Assets/".Length);
        var guid = AssetDatabase.CreateFolder("Assets", folderName);
        Assert.That(guid, Is.Not.Empty);
        Assert.That(AssetDatabase.IsValidFolder(testFolderAssetPath), Is.True);
    }

    private static string AssetPathToFullPath(string assetPath)
    {
        const string assetsPrefix = "Assets/";
        Assert.That(assetPath.StartsWith(assetsPrefix, StringComparison.Ordinal), Is.True);
        return AssetPathToFullPathUnchecked(assetPath);
    }

    private static string AssetPathToFullPathUnchecked(string assetPath)
    {
        const string assetsPrefix = "Assets/";
        return Path.Combine(Application.dataPath, assetPath.Substring(assetsPrefix.Length));
    }

    private void RestoreActiveSceneAndCloseTestScene()
    {
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

    private void ReserveQuickStartPrefabPathForTest()
    {
        var playerPrefabFullPath = AssetPathToFullPathUnchecked(GamingCouchQuickStartSetup.PlayerPrefabAssetPath);
        if (Directory.Exists(playerPrefabFullPath) || File.Exists(playerPrefabFullPath))
        {
            Assert.Ignore("Skipping quick-start prefab creation test because " + GamingCouchQuickStartSetup.PlayerPrefabAssetPath + " already exists on disk.");
        }

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(GamingCouchQuickStartSetup.PlayerPrefabAssetPath) != null)
        {
            Assert.Ignore("Skipping quick-start prefab creation test because " + GamingCouchQuickStartSetup.PlayerPrefabAssetPath + " already exists.");
        }

        EnsureGCExampleFolderForTest(
            ref createdExampleProjectFolderForPrefabTest,
            ref createdExampleFolderForPrefabTest
        );
    }

    private static void EnsureGCExampleFolderForTest(
        ref bool createdProjectFolder,
        ref bool createdExampleFolder
    )
    {
        EnsureAssetFolderForTest(
            GamingCouchQuickStartSetup.ProjectFolderAssetPath,
            "Assets",
            "GamingCouch",
            ref createdProjectFolder
        );
        EnsureAssetFolderForTest(
            GamingCouchQuickStartSetup.ExampleFolderAssetPath,
            GamingCouchQuickStartSetup.ProjectFolderAssetPath,
            "GCExample",
            ref createdExampleFolder
        );
    }

    private static void EnsureAssetFolderForTest(
        string assetPath,
        string parentAssetPath,
        string folderName,
        ref bool created
    )
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        var fullPath = AssetPathToFullPath(assetPath);
        if (Directory.Exists(fullPath) ||
            File.Exists(fullPath) ||
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
        {
            Assert.Ignore("Skipping GCExample asset test because " + assetPath + " exists but is not a Unity asset folder.");
        }

        var guid = AssetDatabase.CreateFolder(parentAssetPath, folderName);
        Assert.That(string.IsNullOrEmpty(guid), Is.False);
        Assert.That(AssetDatabase.IsValidFolder(assetPath), Is.True);
        created = true;
    }

    private void DeleteTestAssetFolder()
    {
        if (string.IsNullOrEmpty(testFolderAssetPath) ||
            !testFolderAssetPath.StartsWith(TestFolderAssetPathPrefix, StringComparison.Ordinal))
        {
            return;
        }

        var deletedThroughAssetDatabase = AssetDatabase.IsValidFolder(testFolderAssetPath) &&
                                          AssetDatabase.DeleteAsset(testFolderAssetPath);
        var fullPath = AssetPathToFullPathUnchecked(testFolderAssetPath);
        if (!deletedThroughAssetDatabase && Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, true);
        }

        var metaPath = fullPath + ".meta";
        if (!deletedThroughAssetDatabase && File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private void DeleteQuickStartPrefabTestAssets()
    {
        DeleteCreatedAssetFileForTest(
            GamingCouchQuickStartSetup.PlayerPrefabAssetPath,
            ref createdQuickStartPrefabForPrefabTest
        );

        DeleteCreatedGCExampleFolders(
            ref createdExampleProjectFolderForPrefabTest,
            ref createdExampleFolderForPrefabTest
        );

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void DeleteCreatedAssetFileForTest(string assetPath, ref bool created)
    {
        if (!created)
        {
            return;
        }

        var fullPath = AssetPathToFullPath(assetPath);
        if (Directory.Exists(fullPath))
        {
            return;
        }

        var deletedThroughAssetDatabase = AssetDatabase.DeleteAsset(assetPath);
        if (!deletedThroughAssetDatabase && File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        var metaPath = fullPath + ".meta";
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath) && File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }

        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            created = false;
        }
    }

    private void DeleteGeneratedScriptPathCollisionTestAssets()
    {
        DeleteGeneratedScriptPathCollisionTestAsset(
            GamingCouchQuickStartSetup.ActiveSceneGameScriptAssetPath,
            ref createdExampleGameScriptPathCollisionForTest
        );
        DeleteGeneratedScriptPathCollisionTestAsset(
            GamingCouchQuickStartSetup.ActiveScenePlayerScriptAssetPath,
            ref createdExamplePlayerScriptPathCollisionForTest
        );
        DeleteCreatedGCExampleFolders(
            ref createdExampleProjectFolderForCollisionTest,
            ref createdExampleFolderForCollisionTest
        );
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void DeleteCreatedGCExampleFolders(
        ref bool createdProjectFolder,
        ref bool createdExampleFolder
    )
    {
        DeleteEmptyAssetFolderCreatedForTest(
            GamingCouchQuickStartSetup.ExampleFolderAssetPath,
            ref createdExampleFolder
        );
        DeleteEmptyAssetFolderCreatedForTest(
            GamingCouchQuickStartSetup.ProjectFolderAssetPath,
            ref createdProjectFolder
        );
    }

    private static void DeleteEmptyAssetFolderCreatedForTest(string assetPath, ref bool created)
    {
        if (!created)
        {
            return;
        }

        var fullPath = AssetPathToFullPath(assetPath);
        if (Directory.Exists(fullPath))
        {
            var nestedEntries = Directory.GetFileSystemEntries(fullPath);
            if (nestedEntries.Length > 0)
            {
                return;
            }

            var deletedThroughAssetDatabase = AssetDatabase.IsValidFolder(assetPath) &&
                                              AssetDatabase.DeleteAsset(assetPath);
            if (!deletedThroughAssetDatabase && Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, false);
            }
        }

        var metaPath = fullPath + ".meta";
        if (!Directory.Exists(fullPath) && !File.Exists(fullPath) && File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }

        if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
        {
            created = false;
        }
    }

    private static void DeleteGeneratedScriptPathCollisionTestAsset(string assetPath, ref bool created)
    {
        if (!created)
        {
            return;
        }

        var fullPath = AssetPathToFullPathUnchecked(assetPath);
        if (Directory.Exists(fullPath))
        {
            var nestedEntries = Directory.GetFileSystemEntries(fullPath);
            if (nestedEntries.Length > 0)
            {
                throw new InvalidOperationException(
                    "Refusing to recursively delete non-empty collision test folder " + assetPath + "."
                );
            }

            var deletedThroughAssetDatabase = AssetDatabase.DeleteAsset(assetPath);
            if (!deletedThroughAssetDatabase && Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, false);
            }
        }

        var metaPath = fullPath + ".meta";
        if (!Directory.Exists(fullPath) && File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }

        created = false;
    }

    private static bool IsAssetFolderEmpty(string assetPath)
    {
        var fullPath = AssetPathToFullPathUnchecked(assetPath);
        return Directory.Exists(fullPath) &&
               Directory.GetFileSystemEntries(fullPath).Length == 0;
    }

    private void RestoreBuildSettings()
    {
        EditorBuildSettings.scenes = previousBuildSettingsScenes ?? Array.Empty<EditorBuildSettingsScene>();
    }

    private void RestoreSuppressAutoOpenSetting()
    {
        EditorUserSettings.SetConfigValue(SuppressAutoOpenKey, previousSuppressAutoOpenConfigValue);
    }

    private void SaveWebGLSettings()
    {
        var webGLTarget = NamedBuildTarget.WebGL;
        previousWebGLTemplate = PlayerSettings.WebGL.template;
        previousWebGLCompressionFormat = PlayerSettings.WebGL.compressionFormat;
        previousWebGLDataCaching = PlayerSettings.WebGL.dataCaching;
        previousWebGLExceptionSupport = PlayerSettings.WebGL.exceptionSupport;
        previousWebGLDebugSymbolMode = PlayerSettings.WebGL.debugSymbolMode;
#if UNITY_2023_1_OR_NEWER
        previousWebGLWasm2023 = PlayerSettings.WebGL.wasm2023;
#endif
        previousWebGLCodeOptimization = UnityEditor.WebGL.UserBuildSettings.codeOptimization;
        previousDevelopmentBuild = EditorUserBuildSettings.development;
        previousIl2CppCodeGeneration = PlayerSettings.GetIl2CppCodeGeneration(webGLTarget);
        previousManagedStrippingLevel = PlayerSettings.GetManagedStrippingLevel(webGLTarget);
        previousStripUnusedMeshComponents = PlayerSettings.stripUnusedMeshComponents;
        previousSplashScreenShow = PlayerSettings.SplashScreen.show;
        previousSplashScreenShowUnityLogo = PlayerSettings.SplashScreen.showUnityLogo;
    }

    private void RestoreWebGLSettings()
    {
        var webGLTarget = NamedBuildTarget.WebGL;
        PlayerSettings.WebGL.template = previousWebGLTemplate;
        PlayerSettings.WebGL.compressionFormat = previousWebGLCompressionFormat;
        PlayerSettings.WebGL.dataCaching = previousWebGLDataCaching;
        PlayerSettings.WebGL.exceptionSupport = previousWebGLExceptionSupport;
        PlayerSettings.WebGL.debugSymbolMode = previousWebGLDebugSymbolMode;
#if UNITY_2023_1_OR_NEWER
        PlayerSettings.WebGL.wasm2023 = previousWebGLWasm2023;
#endif
        UnityEditor.WebGL.UserBuildSettings.codeOptimization = previousWebGLCodeOptimization;
        EditorUserBuildSettings.development = previousDevelopmentBuild;
        PlayerSettings.SetIl2CppCodeGeneration(webGLTarget, previousIl2CppCodeGeneration);
        PlayerSettings.SetManagedStrippingLevel(webGLTarget, previousManagedStrippingLevel);
        PlayerSettings.stripUnusedMeshComponents = previousStripUnusedMeshComponents;
        PlayerSettings.SplashScreen.show = previousSplashScreenShow;
        PlayerSettings.SplashScreen.showUnityLogo = previousSplashScreenShowUnityLogo;
    }

    private static void RunCleanup(Action cleanup, List<Exception> cleanupErrors)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            cleanupErrors.Add(exception);
        }
    }
}

internal sealed class CompatibleGameScriptReceiver : MonoBehaviour
{
    private void GamingCouchSetup(GCSetupOptions options)
    {
    }

    private void GamingCouchPlay(GCPlayOptions options)
    {
    }
}

internal sealed class SetupOnlyGameScriptReceiver : MonoBehaviour
{
    private void GamingCouchSetup(GCSetupOptions options)
    {
    }
}

internal abstract class CompatibleGameScriptReceiverBase : MonoBehaviour
{
    public void GamingCouchSetup(GCSetupOptions options)
    {
    }

    public void GamingCouchPlay(GCPlayOptions options)
    {
    }
}

internal sealed class InheritedCompatibleGameScriptReceiver : CompatibleGameScriptReceiverBase
{
}

internal sealed class ColorPlaceholderPrefabPlayer : GCPlayer
{
    [SerializeField]
    private Renderer colorRenderer;
}

internal sealed class WrongSignatureGameScriptReceiver : MonoBehaviour
{
    public void GamingCouchSetup()
    {
    }

    public void GamingCouchPlay(string options)
    {
    }
}
