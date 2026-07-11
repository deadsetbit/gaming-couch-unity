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

public sealed class GamingCouchActiveSceneSetupAssetTests
{
    private const string SuppressAutoOpenKey = "DSB.GC.StartScreen.SuppressAutoOpen";
    private const string TestFolderAssetPathPrefix = "Assets/GamingCouchActiveSceneSetupAssetTests_";
    private const string ExistingSceneBuildPath = "Assets/GamingCouchExistingScene.unity";
    private const string TestSceneBuildPath = "Assets/GamingCouchActiveSceneSetupAssetTestScene.unity";
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
    // Held as object because UnityEditor.WebGL.WasmCodeOptimization ships with the
    // optional WebGL Build Support module; GCWebGLBuildSupport reads/writes it via
    // reflection so this test assembly compiles with or without the module.
    private object previousWebGLCodeOptimization;
    private bool previousDevelopmentBuild;
    private Il2CppCodeGeneration previousIl2CppCodeGeneration;
    private ManagedStrippingLevel previousManagedStrippingLevel;
    private bool previousStripUnusedMeshComponents;
    private bool previousSplashScreenShow;
    private bool previousSplashScreenShowUnityLogo;
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
        var missingPiecesSpec = GamingCouchActiveSceneSetup.GetScriptSetupSpec(
            GCActiveSceneSetupIntent.ActiveScene,
            GCActiveSceneSetupAction.ActiveSceneMissingPieces
        );
        var gameSpec = GamingCouchActiveSceneSetup.GetScriptSetupSpec(
            GCActiveSceneSetupIntent.ActiveScene,
            GCActiveSceneSetupAction.ActiveSceneGameListener
        );
        var playerPrefabSpec = GamingCouchActiveSceneSetup.GetScriptSetupSpec(
            GCActiveSceneSetupIntent.ActiveScene,
            GCActiveSceneSetupAction.ActiveScenePlayerPrefab
        );

        Assert.That(gameSpec.scriptFolderAssetPath, Is.EqualTo(GamingCouchActiveSceneSetup.ExampleFolderAssetPath));
        Assert.That(gameSpec.gameScriptAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCGameExample.cs"));
        Assert.That(gameSpec.playerScriptAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCPlayerExample.cs"));
        Assert.That(gameSpec.playerPrefabAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCPlayerExample.prefab"));
        Assert.That(gameSpec.gameTypeName, Is.EqualTo("GCGameExample"));
        Assert.That(gameSpec.playerTypeName, Is.EqualTo("GCPlayerExample"));
        Assert.That(gameSpec.listenerObjectName, Is.EqualTo("Game"));
        Assert.That(gameSpec.requiresGeneratedScriptFolder, Is.True);
        Assert.That(playerPrefabSpec.gameScriptAssetPath, Is.EqualTo(GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath));
        Assert.That(playerPrefabSpec.playerScriptAssetPath, Is.EqualTo(GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath));
        Assert.That(playerPrefabSpec.playerPrefabAssetPath, Is.EqualTo(GamingCouchActiveSceneSetup.ActiveScenePlayerPrefabAssetPath));
        Assert.That(missingPiecesSpec.gameScriptAssetPath, Is.EqualTo(GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath));
        Assert.That(missingPiecesSpec.playerScriptAssetPath, Is.EqualTo(GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath));
        Assert.That(missingPiecesSpec.playerPrefabAssetPath, Is.EqualTo(GamingCouchActiveSceneSetup.ActiveScenePlayerPrefabAssetPath));
    }

    [Test]
    public void GeneratedActiveSceneGameSourceUsesGCGameExampleAndDemonstratesPlayFlow()
    {
        var gameSpec = GamingCouchActiveSceneSetup.GetScriptSetupSpec(
            GCActiveSceneSetupIntent.ActiveScene,
            GCActiveSceneSetupAction.ActiveSceneGameListener
        );
        var source = gameSpec.BuildGameScriptSource();

        AssertHasExampleTemplateHeader(source);
        AssertGeneratedGameSourceDemonstratesPlayFlow(source, "GCGameExample", "GCPlayerExample");
    }

    [Test]
    public void GeneratedActiveScenePlayerSourceUsesGCPlayerExampleAndSupportsColorPlaceholderPrefab()
    {
        var gameSpec = GamingCouchActiveSceneSetup.GetScriptSetupSpec(
            GCActiveSceneSetupIntent.ActiveScene,
            GCActiveSceneSetupAction.ActiveSceneGameListener
        );
        var source = gameSpec.BuildPlayerScriptSource();

        AssertHasExampleTemplateHeader(source);
        AssertGeneratedPlayerSourceSupportsColorPlaceholderPrefab(source, "GCPlayerExample");
    }

    [Test]
    public void ExamplePlayerPrefabWiresVisiblePlaceholderRendererToPlayerColorField()
    {
        // Generate into the per-test temp folder (cleaned up by DeleteTestAssetFolder) so the test
        // never creates assets in the real Assets/GamingCouch/GCExample and cannot corrupt or reuse a
        // real example prefab.
        EnsureTestAssetFolder();
        var playerPrefabAssetPath = testFolderAssetPath + "/" + nameof(ColorPlaceholderPrefabPlayer) + ".prefab";
        var context = new GCExampleAssetSetupContinuationContext(
            GCActiveSceneSetupIntent.ActiveScene,
            GCActiveSceneSetupAction.ActiveScenePlayerPrefab,
            false,
            testFolderAssetPath,
            testFolderAssetPath + "/GameScript.cs",
            testFolderAssetPath + "/PlayerScript.cs",
            playerPrefabAssetPath,
            nameof(CompatibleGameScriptReceiver),
            nameof(ColorPlaceholderPrefabPlayer),
            "Game",
            typeof(CompatibleGameScriptReceiver),
            typeof(ColorPlaceholderPrefabPlayer)
        );

        var result = GamingCouchActiveSceneSetup.EnsureExamplePlayerPrefab(context);

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
    public void ActiveScenePlayerPrefabSetupCreatesPlayerPrefabForCompiledPlayerType()
    {
        // Generate into the per-test temp folder (cleaned up by DeleteTestAssetFolder) so the test
        // never creates assets in the real Assets/GamingCouch/GCExample.
        EnsureTestAssetFolder();
        var playerPrefabAssetPath = testFolderAssetPath + "/" + nameof(GCExamplePlayerFixture) + ".prefab";
        var context = new GCExampleAssetSetupContinuationContext(
            GCActiveSceneSetupIntent.ActiveScene,
            GCActiveSceneSetupAction.ActiveScenePlayerPrefab,
            false,
            testFolderAssetPath,
            testFolderAssetPath + "/GameScript.cs",
            testFolderAssetPath + "/PlayerScript.cs",
            playerPrefabAssetPath,
            nameof(GCExampleGameFixture),
            nameof(GCExamplePlayerFixture),
            "Game",
            typeof(GCExampleGameFixture),
            typeof(GCExamplePlayerFixture)
        );

        var result = GamingCouchActiveSceneSetup.EnsureExamplePlayerPrefab(context);

        Assert.That(result.IsBlocked, Is.False, string.Join("\n", result.blockedReasons));
        Assert.That(result.changed, Is.True);
        Assert.That(result.prefab, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(result.prefab), Is.EqualTo(playerPrefabAssetPath));
        Assert.That(result.prefab.GetComponent<GCExamplePlayerFixture>(), Is.Not.Null);
    }

    [Test]
    public void GameListenerSetupCreatesNamedGameObjectAndLeavesPlayerPrefabEmpty()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var context = CreateGameListenerTestContext();

        var listenerResult = GamingCouchActiveSceneSetup.EnsureActiveSceneGameListener(context, gamingCouch);
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

        var listenerResult = GamingCouchActiveSceneSetup.EnsureActiveSceneGameListener(context, gamingCouch);
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

        var listenerResult = GamingCouchActiveSceneSetup.EnsureActiveSceneGameListener(context, gamingCouch);

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

        var listenerResult = GamingCouchActiveSceneSetup.EnsureActiveSceneGameListener(context, gamingCouch);

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

        var listenerResult = GamingCouchActiveSceneSetup.EnsureActiveSceneGameListener(context, gamingCouch);

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

        var listenerResult = GamingCouchActiveSceneSetup.EnsureActiveSceneGameListener(context, gamingCouch);

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
            GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath,
            ref createdExampleGameScriptPathCollisionForTest
        );
        CreateScriptPathCollisionDirectory(
            GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath,
            ref createdExamplePlayerScriptPathCollisionForTest
        );
        // Deliberately not imported into the AssetDatabase: a folder whose name ends in ".cs" sends
        // Unity's script importer into a re-import loop. The generator blocks on a raw Directory.Exists
        // check, so importing the collision folders is unnecessary.
        CreateGamingCouch("GamingCouch");

        var result = GamingCouchActiveSceneSetup.EnsureActiveSceneGameListenerReference();

        Assert.That(result.IsBlocked, Is.True);
        Assert.That(result.changed, Is.False);
        Assert.That(result.status, Is.EqualTo(GCActiveSceneSetupStatus.Blocked));
        AssertHasEntryContaining(result.details, "Cannot create the example script Assets/GamingCouch/GCExample/GCGameExample.cs because a folder (not a script file) already exists at that path.");
        AssertHasEntryContaining(result.details, "Cannot create the example script Assets/GamingCouch/GCExample/GCPlayerExample.cs because a folder (not a script file) already exists at that path.");
        Assert.That(testScene.GetRootGameObjects().Any(root => root != null && root.name == "Game"), Is.False);
    }

    [Test]
    public void RemoveBlockingExampleAssetFoldersClearsScriptPathCollisions()
    {
        ReserveGCExampleGameAndPlayerScriptPathsForCollisionTest();
        CreateScriptPathCollisionDirectory(
            GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath,
            ref createdExampleGameScriptPathCollisionForTest
        );
        CreateScriptPathCollisionDirectory(
            GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath,
            ref createdExamplePlayerScriptPathCollisionForTest
        );
        // Deliberately not imported into the AssetDatabase (a ".cs"-named folder loops Unity's
        // importer); detection and removal both operate on the raw filesystem.

        Assert.That(
            GamingCouchActiveSceneSetup.FindBlockingExampleAssetFolders(),
            Is.EquivalentTo(new[]
            {
                GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath,
                GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath,
            })
        );

        var cleanup = GamingCouchActiveSceneSetup.RemoveBlockingExampleAssetFolders();

        Assert.That(cleanup.IsBlocked, Is.False);
        Assert.That(cleanup.changed, Is.True);
        Assert.That(
            cleanup.removedAssetPaths,
            Is.EquivalentTo(new[]
            {
                GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath,
                GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath,
            })
        );
        Assert.That(GamingCouchActiveSceneSetup.FindBlockingExampleAssetFolders(), Is.Empty);
        Assert.That(Directory.Exists(AssetPathToFullPath(GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath)), Is.False);
        Assert.That(Directory.Exists(AssetPathToFullPath(GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath)), Is.False);
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

        GamingCouchActiveSceneSetup.EnsureGeneratedAssetFileWithoutOverwrite(
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
            AssertHasEntryContaining(readiness.details, "WebGL compression: Gzip -> Disabled");
        }
        finally
        {
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void WebGLBuildProfilesGenerateDiffRowsAndApplyFromSpecs()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.dataCaching = false;

        var plan = GamingCouchWebGLBuildSettingsProfiles.BuildReleaseProfilePlan();
        var result = GamingCouchWebGLBuildSettingsProfiles.ApplyReleaseProfile();
        var readinessDetails = new List<string>();

        Assert.That(plan.HasChanges, Is.True);
        AssertHasPreviewRow(
            plan.rows,
            GamingCouchWebGLBuildSettingsProfiles.WebGLCompressionSettingId,
            "WebGL compression: Gzip -> Disabled",
            true
        );
        Assert.That(result.changed, Is.True);
        AssertHasEntryContaining(result.details, "Applied WebGL compression: Gzip -> Disabled");
        Assert.That(
            GamingCouchWebGLBuildSettingsProfiles.IsReleaseProfileApplied(readinessDetails),
            Is.True
        );
        Assert.That(readinessDetails, Is.Empty);
    }

    [Test]
    public void WebGLBuildProfileSelectedApplySkipsUnselectedSettingForCurrentRun()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.dataCaching = false;

        var result = GamingCouchWebGLBuildSettingsProfiles.ApplyReleaseProfile(new[]
        {
            GamingCouchWebGLBuildSettingsProfiles.WebGLCompressionSettingId,
        });
        var readinessDetails = new List<string>();

        Assert.That(result.changed, Is.True);
        Assert.That(PlayerSettings.WebGL.compressionFormat, Is.EqualTo(WebGLCompressionFormat.Disabled));
        Assert.That(PlayerSettings.WebGL.dataCaching, Is.False);
        AssertHasEntryContaining(result.details, "Skipped WebGL data caching: Disabled -> Enabled");
        Assert.That(
            GamingCouchWebGLBuildSettingsProfiles.IsReleaseProfileApplied(readinessDetails),
            Is.False
        );
        AssertHasEntryContaining(readinessDetails.ToArray(), "WebGL data caching: Disabled -> Enabled");
    }

    [Test]
    public void WebGLExportPlanReportsTemplateBlockersBeforeMutating()
    {
        var sourceDirectory = CreateTemporaryWebGLTemplateSource("template source");
        var destinationAsFile = CreateTemporaryPath("WebGLTemplateDestinationFile");

        try
        {
            File.WriteAllText(destinationAsFile, "not a folder");

            var plan = GamingCouchWebGLExportSetup.CreateWebGLExportSetupPlan(
                sourceDirectory,
                destinationAsFile,
                false
            );

            Assert.That(plan.IsBlocked, Is.True);
            AssertHasPreviewRow(
                plan.rows,
                "web-export-template-folder",
                "Project-local web export template folder: File -> Folder",
                false
            );
            Assert.That(File.ReadAllText(destinationAsFile), Is.EqualTo("not a folder"));
        }
        finally
        {
            DeleteTemporaryPath(sourceDirectory);
            DeleteTemporaryPath(destinationAsFile);
        }
    }

    [Test]
    public void WebGLExportPlanKeepsTemplateAndTargetRowsRequired()
    {
        var sourceDirectory = CreateTemporaryWebGLTemplateSource("template source");
        var destinationDirectory = CreateTemporaryPath("WebGLTemplateDestination");

        try
        {
            var plan = GamingCouchWebGLExportSetup.CreateWebGLExportSetupPlan(
                sourceDirectory,
                destinationDirectory,
                false
            );

            Assert.That(FindPreviewRow(plan.rows, "web-export-template-folder").isSkippable, Is.False);
            Assert.That(FindPreviewRow(plan.rows, GamingCouchWebGLExportSetup.TemplateSelectionRowId).isSkippable, Is.False);
            Assert.That(FindPreviewRow(plan.rows, GamingCouchWebGLExportSetup.ActiveBuildTargetRowId).isSkippable, Is.False);
            Assert.That(
                FindPreviewRow(
                    plan.rows,
                    GamingCouchWebGLBuildSettingsProfiles.WebGLCompressionSettingId
                ).isSkippable,
                Is.True
            );
        }
        finally
        {
            DeleteTemporaryPath(sourceDirectory);
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void WebGLDocsDoNotDuplicateManualBuildSettingLists()
    {
        var packageRoot = GetPackageRootPath();
        var documentPaths = new[]
        {
            "README.md",
            "Documentation~/README.md",
        };
        var forbiddenExactSettingPhrases = new[]
        {
            "development build off",
            "WebGL debug symbols off",
            "high managed stripping",
            "IL2CPP optimize size",
            "WebAssembly 2023 where available",
            "disk-size LTO",
            "data caching on",
            "WebGL compression disabled.",
            "Debug symbols disabled.",
            "Managed stripping level set to high.",
            "Disk-size LTO enabled.",
        };

        foreach (var documentPath in documentPaths)
        {
            Assert.That(documentPath, Is.Not.Null.And.Not.Empty);
            var fullPath = Path.Combine(packageRoot, documentPath);
            var text = File.ReadAllText(fullPath);
            foreach (var phrase in forbiddenExactSettingPhrases)
            {
                Assert.That(text, Does.Not.Contain(phrase), documentPath);
            }
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

            var result = GamingCouchWebGLExportSetup.EnsureWebGLExportSetup(sourceDirectory, destinationDirectory, false);

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
            AssertHasEntryContaining(readiness.details, "run Gaming Couch web export settings or switch to WebGL");
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

    private static GCExampleAssetSetupContinuationContext CreateGameListenerTestContext()
    {
        return CreateGameListenerTestContext(typeof(CompatibleGameScriptReceiver));
    }

    private static GCExampleAssetSetupContinuationContext CreateGameListenerTestContext(Type gameType)
    {
        return new GCExampleAssetSetupContinuationContext(
            GCActiveSceneSetupIntent.ActiveScene,
            GCActiveSceneSetupAction.ActiveSceneGameListener,
            false,
            GamingCouchActiveSceneSetup.ExampleFolderAssetPath,
            GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath,
            GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath,
            GamingCouchActiveSceneSetup.ActiveScenePlayerPrefabAssetPath,
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

    private static void AssertHasPreviewRow(
        GCWebGLPreviewRow[] rows,
        string id,
        string expectedDiffText,
        bool expectedSkippable
    )
    {
        var row = FindPreviewRow(rows, id);
        Assert.That(row.DiffText, Is.EqualTo(expectedDiffText));
        Assert.That(row.isSkippable, Is.EqualTo(expectedSkippable));
    }

    private static GCWebGLPreviewRow FindPreviewRow(GCWebGLPreviewRow[] rows, string id)
    {
        var row = rows != null
            ? rows.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.id, id, StringComparison.Ordinal)
            )
            : null;
        Assert.That(row, Is.Not.Null, "Expected preview row " + id + ".");
        return row;
    }

    private static void AssertGeneratedGameSourceDemonstratesPlayFlow(
        string source,
        string gameTypeName,
        string playerTypeName
    )
    {
        Assert.That(source, Does.Contain("public class " + gameTypeName + " : MonoBehaviour"));
        Assert.That(source, Does.Contain("private void GamingCouchSetup(GCSetupOptions options)"));
        Assert.That(source, Does.Contain("Debug.Log(\"GamingCouch setup received."));
        Assert.That(source, Does.Contain("GamingCouch.Instance.SetupGameVersus(new GCGameVersusSetupOptions"));
        Assert.That(source, Does.Contain("valueTypeEnum = PlayersHudValueType.PointsSmall"));
        Assert.That(source, Does.Contain("meterTypeEnum = PlayersHudMeterType.Bar"));
        Assert.That(source, Does.Contain("GamingCouch.Instance.SetupDone();"));
        Assert.That(source, Does.Contain("private void GamingCouchPlay(GCPlayOptions options)"));
        Assert.That(source, Does.Contain("Debug.Log(\"GamingCouch play received for \" + options.players.Length + \" players.\""));
        Assert.That(source, Does.Contain("GamingCouch.Instance.SetupPlayers<" + playerTypeName + ">(options.players"));
        Assert.That(source, Does.Contain("players.AddPlayer(player);"));
        Assert.That(source, Does.Contain("player.ApplyPlayerColor();"));
        Assert.That(source, Does.Contain("player.SetLives(3, \"Example play start\")"));
        Assert.That(source, Does.Contain("player.SetStatus(GCPlayerStatus.Pending, \"Ready\", \"Example play start\")"));
        Assert.That(source, Does.Contain("player.SetMeter(0, \"Example play start\")"));
        Assert.That(source, Does.Contain("roundCoroutine = StartCoroutine(RunRound());"));
        Assert.That(source, Does.Contain("private void Update()"));
        Assert.That(source, Does.Contain("GamingCouch.Instance.Status != GCStatus.Playing"));
        Assert.That(source, Does.Contain("PollInputByPlayerIndex(player);"));
        Assert.That(source, Does.Contain("private void PollInputByPlayerIndex(" + playerTypeName + " player)"));
        Assert.That(source, Does.Contain("GamingCouch.Instance.GetInputsByPlayerIndex(player.Index)"));
        Assert.That(source, Does.Contain("player.AddScore(1, \"Primary input\")"));
        Assert.That(source, Does.Contain("if (!player.IsFinished)"));
        Assert.That(source, Does.Contain("player.SetFinishedRevokable(\"Primary input\")"));
        Assert.That(source, Does.Contain("if (player.IsFinishedRevokable)"));
        Assert.That(source, Does.Contain("player.SetRevokeFinished(\"Secondary input\")"));
        Assert.That(source, Does.Contain("if (input.alt && !player.IsEliminated)"));
        Assert.That(source, Does.Contain("player.SetEliminatedRevokable(\"Alt input\")"));
        Assert.That(source, Does.Contain("private IEnumerator RunRound()"));
        Assert.That(source, Does.Contain("UpdateRuntimeStateForHud();"));
        Assert.That(source, Does.Contain("EmitDiagnosticLogExample();"));
        Assert.That(source, Does.Contain("private void UpdateRuntimeStateForHud()"));
        Assert.That(source, Does.Contain("player.SetStatus(GCPlayerStatus.Pending, \"Halfway\", \"Example runtime state\")"));
        Assert.That(source, Does.Contain("player.SetMeter(50, \"Example runtime state\")"));
        Assert.That(source, Does.Contain("private void EmitDiagnosticLogExample()"));
        Assert.That(source, Does.Contain("Example diagnostic checkpoint: runtime state and HUD updated"));
        Assert.That(source, Does.Contain("private void ApplyRandomFinalScores()"));
        Assert.That(source, Does.Contain("Random.Range(0, clampedMaxScore + 1)"));
        Assert.That(source, Does.Contain("player.SetScore("));
        Assert.That(source, Does.Contain("player.SetEliminatedPermanent(\"Example round complete\")"));
        Assert.That(source, Does.Contain("player.SetFinishedPermanent("));
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
        return GCStartScreenReadiness.FromFacts(new GCStartScreenReadinessFacts(
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
        ));
    }

    private GCStartScreenReadiness CreateReadyStartScreenReadiness(
        GamingCouch gamingCouch,
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab
    )
    {
        return GCStartScreenReadiness.FromFacts(new GCStartScreenReadinessFacts(
            testScene,
            new[] { gamingCouch },
            gamingCouch,
            listener,
            playerPrefab,
            CreateValidLocalPlayJsonReadiness(),
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            CreateReadyWebGLExportReadiness()
        ));
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
            "Gaming Couch web export settings are ready.",
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

    private static string GetPackageRootPath()
    {
        var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
            typeof(GamingCouchWebGLExportSetup).Assembly
        );
        if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
        {
            return packageInfo.resolvedPath;
        }

        return Directory.GetCurrentDirectory();
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
        ReserveGeneratedScriptPathForCollisionTest(GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath);
        ReserveGeneratedScriptPathForCollisionTest(GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath);
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

    private static void EnsureGCExampleFolderForTest(
        ref bool createdProjectFolder,
        ref bool createdExampleFolder
    )
    {
        EnsureAssetFolderForTest(
            GamingCouchActiveSceneSetup.ProjectFolderAssetPath,
            "Assets",
            "GamingCouch",
            ref createdProjectFolder
        );
        EnsureAssetFolderForTest(
            GamingCouchActiveSceneSetup.ExampleFolderAssetPath,
            GamingCouchActiveSceneSetup.ProjectFolderAssetPath,
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

    private void DeleteGeneratedScriptPathCollisionTestAssets()
    {
        DeleteGeneratedScriptPathCollisionTestAsset(
            GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath,
            ref createdExampleGameScriptPathCollisionForTest
        );
        DeleteGeneratedScriptPathCollisionTestAsset(
            GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath,
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
            GamingCouchActiveSceneSetup.ExampleFolderAssetPath,
            ref createdExampleFolder
        );
        DeleteEmptyAssetFolderCreatedForTest(
            GamingCouchActiveSceneSetup.ProjectFolderAssetPath,
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
        GCWebGLBuildSupport.TryGetCodeOptimization(out previousWebGLCodeOptimization);
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
        GCWebGLBuildSupport.SetCodeOptimization(previousWebGLCodeOptimization);
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
