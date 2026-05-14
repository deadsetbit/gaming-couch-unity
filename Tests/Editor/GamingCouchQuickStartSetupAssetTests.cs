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

public sealed class GamingCouchQuickStartSetupAssetTests
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
    private bool createdLegacyQuickStartFolderForPrefabTest;
    private bool createdUnifiedPlayerPrefabForPrefabTest;
    private bool createdLegacyExamplePlayerPrefabForPrefabTest;
    private bool createdLegacyQuickStartPrefabForPrefabTest;
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
        var missingPiecesSpec = GamingCouchQuickStartSetup.GetScriptSetupSpec(
            GCQuickStartSetupIntent.ActiveScene,
            GCQuickStartSetupAction.ActiveSceneMissingPieces
        );
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
        Assert.That(gameSpec.playerPrefabAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCPlayerExample.prefab"));
        Assert.That(gameSpec.gameTypeName, Is.EqualTo("GCGameExample"));
        Assert.That(gameSpec.playerTypeName, Is.EqualTo("GCPlayerExample"));
        Assert.That(gameSpec.listenerObjectName, Is.EqualTo("Game"));
        Assert.That(gameSpec.requiresGeneratedScriptFolder, Is.True);
        Assert.That(gameSpec.requiresQuickStartFolders, Is.False);
        Assert.That(playerPrefabSpec.gameScriptAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.ActiveSceneGameScriptAssetPath));
        Assert.That(playerPrefabSpec.playerScriptAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.ActiveScenePlayerScriptAssetPath));
        Assert.That(playerPrefabSpec.playerPrefabAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.ActiveScenePlayerPrefabAssetPath));
        Assert.That(missingPiecesSpec.gameScriptAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.ActiveSceneGameScriptAssetPath));
        Assert.That(missingPiecesSpec.playerScriptAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.ActiveScenePlayerScriptAssetPath));
        Assert.That(missingPiecesSpec.playerPrefabAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.ActiveScenePlayerPrefabAssetPath));
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
            GamingCouchQuickStartSetup.PlayerPrefabAssetPath,
            nameof(CompatibleGameScriptReceiver),
            nameof(ColorPlaceholderPrefabPlayer),
            "Game",
            typeof(CompatibleGameScriptReceiver),
            typeof(ColorPlaceholderPrefabPlayer)
        );

        var result = GamingCouchQuickStartSetup.EnsureQuickStartPlayerPrefab(context);
        createdUnifiedPlayerPrefabForPrefabTest = result.changed;

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
    public void ActiveScenePlayerPrefabSetupCreatesGCPlayerExamplePrefab()
    {
        ReserveActiveScenePlayerPrefabPathForTest();
        var context = new GCQuickStartSetupContinuationContext(
            GCQuickStartSetupIntent.ActiveScene,
            GCQuickStartSetupAction.ActiveScenePlayerPrefab,
            false,
            GamingCouchQuickStartSetup.ExampleFolderAssetPath,
            GamingCouchQuickStartSetup.ActiveSceneGameScriptAssetPath,
            GamingCouchQuickStartSetup.ActiveScenePlayerScriptAssetPath,
            GamingCouchQuickStartSetup.ActiveScenePlayerPrefabAssetPath,
            nameof(GCGameExample),
            nameof(GCPlayerExample),
            "Game",
            typeof(GCGameExample),
            typeof(GCPlayerExample)
        );

        var result = GamingCouchQuickStartSetup.EnsureQuickStartPlayerPrefab(context);
        createdUnifiedPlayerPrefabForPrefabTest = result.changed;

        Assert.That(result.IsBlocked, Is.False, string.Join("\n", result.blockedReasons));
        Assert.That(result.changed, Is.True);
        Assert.That(result.prefab, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(result.prefab), Is.EqualTo(GamingCouchQuickStartSetup.ActiveScenePlayerPrefabAssetPath));
        Assert.That(result.prefab.GetComponent<GCPlayerExample>(), Is.Not.Null);
    }

    [Test]
    public void ReadinessOffersToReplaceLegacyQuickStartPrefabForActiveSceneGame()
    {
        ReserveLegacyExamplePlayerPrefabPathForTest();
        var legacyPlayerPrefab = CreatePlayerPrefabAsset(
            GamingCouchQuickStartSetup.LegacyQuickStartPlayerPrefabAssetPath,
            typeof(ColorPlaceholderPrefabPlayer)
        );
        createdLegacyExamplePlayerPrefabForPrefabTest = true;
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Game");
        listener.AddComponent<GCGameExample>();
        Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));
        Assert.That(GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, legacyPlayerPrefab).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

        var readiness = GCStartScreenReadinessService.InspectActiveScene();
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.PlayerPrefabAssigned);

        AssertCheck(readiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Fail);
        Assert.That(check.message, Does.Contain("GCPlayerExample"));
        Assert.That(check.message, Does.Contain("legacy"));
        Assert.That(check.message, Does.Contain(GamingCouchQuickStartSetup.ActiveScenePlayerPrefabAssetPath));
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.PlayerPrefabAssigned), Is.True);
    }

    [Test]
    public void ReadinessOffersToReplaceOldQuickStartFolderPlayerPrefabForActiveSceneGame()
    {
        ReserveLegacyQuickStartPrefabPathForTest();
        var legacyPlayerPrefab = CreatePlayerPrefabAsset(
            GamingCouchQuickStartSetup.LegacyPlayerPrefabAssetPath,
            typeof(ColorPlaceholderPrefabPlayer)
        );
        createdLegacyQuickStartPrefabForPrefabTest = true;
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Game");
        listener.AddComponent<GCGameExample>();
        Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));
        Assert.That(GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, legacyPlayerPrefab).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

        var readiness = GCStartScreenReadinessService.InspectActiveScene();
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.PlayerPrefabAssigned);

        AssertCheck(readiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Fail);
        Assert.That(check.message, Does.Contain("GCPlayerExample"));
        Assert.That(check.message, Does.Contain("legacy"));
        Assert.That(check.message, Does.Contain(GamingCouchQuickStartSetup.ActiveScenePlayerPrefabAssetPath));
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.PlayerPrefabAssigned), Is.True);
    }

    [Test]
    public void GeneratedQuickStartSourceUsesUnifiedExampleTypesAndAssetPaths()
    {
        var quickStartSpec = GamingCouchQuickStartSetup.GetScriptSetupSpec(
            GCQuickStartSetupIntent.QuickStartScene,
            GCQuickStartSetupAction.QuickStartScene
        );
        var gameSource = quickStartSpec.BuildGameScriptSource();
        var playerSource = quickStartSpec.BuildPlayerScriptSource();

        Assert.That(quickStartSpec.scriptFolderAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.ExampleFolderAssetPath));
        Assert.That(quickStartSpec.gameScriptAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCGameExample.cs"));
        Assert.That(quickStartSpec.playerScriptAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCPlayerExample.cs"));
        Assert.That(quickStartSpec.playerPrefabAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCPlayerExample.prefab"));
        Assert.That(GamingCouchQuickStartSetup.PlayerPrefabAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCPlayerExample.prefab"));
        Assert.That(GamingCouchQuickStartSetup.GameTypeName, Is.EqualTo("GCGameExample"));
        Assert.That(GamingCouchQuickStartSetup.PlayerTypeName, Is.EqualTo("GCPlayerExample"));
        Assert.That(quickStartSpec.gameTypeName, Is.EqualTo(GamingCouchQuickStartSetup.GameTypeName));
        Assert.That(quickStartSpec.playerTypeName, Is.EqualTo(GamingCouchQuickStartSetup.PlayerTypeName));
        Assert.That(GamingCouchQuickStartSetup.ActiveScenePlayerPrefabAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCPlayerExample.prefab"));
        Assert.That(GamingCouchQuickStartSetup.ActiveScenePlayerPrefabAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.PlayerPrefabAssetPath));
        Assert.That(GamingCouchQuickStartSetup.QuickStartSceneAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GamingCouchQuickStart.unity"));
        AssertGeneratedGameSourceDemonstratesPlayFlow(gameSource, "GCGameExample", "GCPlayerExample");
        AssertGeneratedPlayerSourceSupportsColorPlaceholderPrefab(playerSource, "GCPlayerExample");
    }

    [Test]
    public void QuickStartSceneContinuationUsesUnifiedExampleAssetsForSceneWiring()
    {
        var context = CreateContinuationContextForTest(
            GCQuickStartSetupIntent.QuickStartScene,
            GCQuickStartSetupAction.QuickStartScene,
            false
        );

        Assert.That(context.intent, Is.EqualTo(GCQuickStartSetupIntent.QuickStartScene));
        Assert.That(context.action, Is.EqualTo(GCQuickStartSetupAction.QuickStartScene));
        Assert.That(context.quickStartFolderAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.ExampleFolderAssetPath));
        Assert.That(context.gameScriptAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.GameScriptAssetPath));
        Assert.That(context.playerScriptAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.PlayerScriptAssetPath));
        Assert.That(context.playerPrefabAssetPath, Is.EqualTo(GamingCouchQuickStartSetup.PlayerPrefabAssetPath));
        Assert.That(context.gameTypeName, Is.EqualTo("GCGameExample"));
        Assert.That(context.playerTypeName, Is.EqualTo("GCPlayerExample"));
        Assert.That(context.listenerObjectName, Is.EqualTo("Game"));
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
    public void GameListenerSetupReplacesMissingListenerReference()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var deletedListener = CreateCompatibleListener("Deleted Game");
        var context = CreateGameListenerTestContext();
        Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, deletedListener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

        UnityEngine.Object.DestroyImmediate(deletedListener);

        var listenerResult = GamingCouchQuickStartSetup.EnsureQuickStartGameListener(context, gamingCouch);
        var assignResult = GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listenerResult.listenerObject);

        Assert.That(listenerResult.IsBlocked, Is.False);
        Assert.That(listenerResult.changed, Is.True);
        Assert.That(listenerResult.listenerObject.name, Is.EqualTo("Game"));
        Assert.That(listenerResult.blockedReasons, Is.Empty);
        Assert.That(assignResult.status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));
        Assert.That(assignResult.message, Does.Contain("Replaced the missing"));
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.SameAs(listenerResult.listenerObject));
        Assert.That(GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.True);
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

    private static GameObject CreatePlayerPrefabAsset(string assetPath, Type playerType)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(assetPath));
        try
        {
            root.AddComponent(playerType);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, assetPath, out var success);
            Assert.That(success, Is.True);
            Assert.That(prefab, Is.Not.Null);
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
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
            GamingCouchQuickStartSetup.ActiveScenePlayerPrefabAssetPath,
            gameType.Name,
            nameof(GCPlayer),
            "Game",
            gameType,
            typeof(GCPlayer)
        );
    }

    private static GCQuickStartSetupContinuationContext CreateContinuationContextForTest(
        GCQuickStartSetupIntent intent,
        GCQuickStartSetupAction action,
        bool resumedAfterCompilation
    )
    {
        // Exercise the production factory so path/type mapping changes are covered without duplicating its logic here.
        var method = typeof(GamingCouchQuickStartSetup)
            .GetMethod(
                "CreateContinuationContext",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(GCQuickStartSetupIntent), typeof(GCQuickStartSetupAction), typeof(bool) },
                null
            );
        Assert.That(method, Is.Not.Null);

        return (GCQuickStartSetupContinuationContext)method.Invoke(
            null,
            new object[] { intent, action, resumedAfterCompilation }
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

    private static bool ShouldShowStartScreenActionResult(MessageType messageType)
    {
        var shouldShowActionResult = typeof(GamingCouchStartScreenWindow)
            .GetMethod("ShouldShowActionResult", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(shouldShowActionResult, Is.Not.Null);

        return (bool)shouldShowActionResult.Invoke(null, new object[] { messageType });
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

    private Scene CreateLaunchSceneForActiveSceneSetupTest(out bool launchSceneIsTestScene)
    {
        if (testScene.IsValid() && testScene.isLoaded && string.IsNullOrEmpty(testScene.path))
        {
            launchSceneIsTestScene = true;
            EnsureSceneIsActive(testScene);
            return testScene;
        }

        launchSceneIsTestScene = false;
        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
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
        ReservePlayerPrefabPathForTest(GamingCouchQuickStartSetup.PlayerPrefabAssetPath);
    }

    private void ReserveActiveScenePlayerPrefabPathForTest()
    {
        ReservePlayerPrefabPathForTest(GamingCouchQuickStartSetup.ActiveScenePlayerPrefabAssetPath);
    }

    private void ReserveLegacyExamplePlayerPrefabPathForTest()
    {
        ReservePlayerPrefabPathForTest(GamingCouchQuickStartSetup.LegacyQuickStartPlayerPrefabAssetPath);
    }

    private void ReserveLegacyQuickStartPrefabPathForTest()
    {
        var playerPrefabFullPath = AssetPathToFullPathUnchecked(GamingCouchQuickStartSetup.LegacyPlayerPrefabAssetPath);
        if (Directory.Exists(playerPrefabFullPath) || File.Exists(playerPrefabFullPath))
        {
            Assert.Ignore("Skipping quick-start prefab creation test because " + GamingCouchQuickStartSetup.LegacyPlayerPrefabAssetPath + " already exists on disk.");
        }

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(GamingCouchQuickStartSetup.LegacyPlayerPrefabAssetPath) != null)
        {
            Assert.Ignore("Skipping quick-start prefab creation test because " + GamingCouchQuickStartSetup.LegacyPlayerPrefabAssetPath + " already exists.");
        }

        EnsureAssetFolderForTest(
            GamingCouchQuickStartSetup.ProjectFolderAssetPath,
            "Assets",
            "GamingCouch",
            ref createdExampleProjectFolderForPrefabTest
        );
        EnsureAssetFolderForTest(
            "Assets/GamingCouch/QuickStart",
            GamingCouchQuickStartSetup.ProjectFolderAssetPath,
            "QuickStart",
            ref createdLegacyQuickStartFolderForPrefabTest
        );
    }

    private void ReservePlayerPrefabPathForTest(string playerPrefabAssetPath)
    {
        var playerPrefabFullPath = AssetPathToFullPathUnchecked(playerPrefabAssetPath);
        if (Directory.Exists(playerPrefabFullPath) || File.Exists(playerPrefabFullPath))
        {
            Assert.Ignore("Skipping quick-start prefab creation test because " + playerPrefabAssetPath + " already exists on disk.");
        }

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(playerPrefabAssetPath) != null)
        {
            Assert.Ignore("Skipping quick-start prefab creation test because " + playerPrefabAssetPath + " already exists.");
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
            ref createdUnifiedPlayerPrefabForPrefabTest
        );
        DeleteCreatedAssetFileForTest(
            GamingCouchQuickStartSetup.LegacyQuickStartPlayerPrefabAssetPath,
            ref createdLegacyExamplePlayerPrefabForPrefabTest
        );
        DeleteCreatedAssetFileForTest(
            GamingCouchQuickStartSetup.LegacyPlayerPrefabAssetPath,
            ref createdLegacyQuickStartPrefabForPrefabTest
        );

        DeleteEmptyAssetFolderCreatedForTest(
            "Assets/GamingCouch/QuickStart",
            ref createdLegacyQuickStartFolderForPrefabTest
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
