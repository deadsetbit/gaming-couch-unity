using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSB.GC;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GamingCouchQuickStartEditorTests
{
    private const string SuppressAutoOpenKey = "DSB.GC.StartScreen.SuppressAutoOpen";
    private const string TestFolderAssetPathPrefix = "Assets/GamingCouchQuickStartEditorTests_";
    private const string ExistingSceneBuildPath = "Assets/GamingCouchExistingScene.unity";
    private const string TestSceneBuildPath = "Assets/GamingCouchQuickStartEditorTestScene.unity";

    private string previousSuppressAutoOpenConfigValue;
    private EditorBuildSettingsScene[] previousBuildSettingsScenes;
    private string testFolderAssetPath;
    private Scene previousActiveScene;
    private Scene testScene;
    private bool testSceneWasCreatedAdditively;

    [SetUp]
    public void SetUp()
    {
        previousSuppressAutoOpenConfigValue = EditorUserSettings.GetConfigValue(SuppressAutoOpenKey);
        previousBuildSettingsScenes = EditorBuildSettings.scenes;
        testFolderAssetPath = TestFolderAssetPathPrefix + Guid.NewGuid().ToString("N");
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
        var cleanupErrors = new List<Exception>();

        RunCleanup(RestoreActiveSceneAndCloseTestScene, cleanupErrors);
        RunCleanup(DeleteTestAssetFolder, cleanupErrors);
        RunCleanup(RestoreBuildSettings, cleanupErrors);
        RunCleanup(RestoreSuppressAutoOpenSetting, cleanupErrors);
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
        AssertCheck(readiness, GCStartScreenReadinessCheckId.SingleGamingCouchInstance, GCStartScreenReadinessCheckState.Blocked);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Blocked);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Blocked);
    }

    [Test]
    public void ReadinessReportsMissingReferencesOnBareGamingCouch()
    {
        CreateGamingCouch("GamingCouch");

        var readiness = GCStartScreenReadinessService.InspectActiveScene();

        Assert.That(readiness.IsSceneReady, Is.False);
        Assert.That(readiness.gamingCouches, Has.Length.EqualTo(1));
        AssertCheck(readiness, GCStartScreenReadinessCheckId.GamingCouchInstance, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.SingleGamingCouchInstance, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Fail);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Fail);
    }

    [Test]
    public void ReadinessReportsIndividualMissingReferenceStates()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Existing Listener");
        var playerPrefab = CreatePlayerPrefabObject("Existing Player Prefab");

        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);

        var missingPlayerReadiness = GCStartScreenReadinessService.InspectActiveScene();
        Assert.That(missingPlayerReadiness.IsSceneReady, Is.False);
        AssertCheck(missingPlayerReadiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(missingPlayerReadiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Fail);

        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);

        var ready = GCStartScreenReadinessService.InspectActiveScene();
        Assert.That(ready.IsSceneReady, Is.True);
        AssertCheck(ready, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(ready, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Pass);
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
        AssertCheck(readiness, GCStartScreenReadinessCheckId.SingleGamingCouchInstance, GCStartScreenReadinessCheckState.Fail);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Blocked);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Blocked);
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

    private static GamingCouch CreateGamingCouch(string name)
    {
        var gameObject = new GameObject(name);
        gameObject.SetActive(false);
        return gameObject.AddComponent<GamingCouch>();
    }

    private static GameObject CreatePlayerPrefabObject(string name)
    {
        var gameObject = new GameObject(name);
        gameObject.AddComponent<GCPlayer>();
        return gameObject;
    }

    private static void AssertCheck(
        GCStartScreenReadiness readiness,
        GCStartScreenReadinessCheckId id,
        GCStartScreenReadinessCheckState state
    )
    {
        Assert.That(readiness.GetCheck(id).state, Is.EqualTo(state));
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

    private void RestoreBuildSettings()
    {
        EditorBuildSettings.scenes = previousBuildSettingsScenes ?? Array.Empty<EditorBuildSettingsScene>();
    }

    private void RestoreSuppressAutoOpenSetting()
    {
        EditorUserSettings.SetConfigValue(SuppressAutoOpenKey, previousSuppressAutoOpenConfigValue);
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
