using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSB.GC;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public sealed class GamingCouchQuickStartEditorTests
{
    private const string SuppressAutoOpenKey = "DSB.GC.StartScreen.SuppressAutoOpen";
    private const string TestFolderAssetPathPrefix = "Assets/GamingCouchQuickStartEditorTests_";
    private const string ExistingSceneBuildPath = "Assets/GamingCouchExistingScene.unity";
    private const string OtherSceneBuildPath = "Assets/GamingCouchOtherScene.unity";

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
    public void ActiveSceneSetupStillNormalizesBuildSettingsWhenGamingCouchWiringBlocked()
    {
        EnsureTestAssetFolder();
        var sceneAssetPath = testFolderAssetPath + "/DuplicateGamingCouchScene.unity";
        var previousScene = SceneManager.GetActiveScene();
        var launchScene = CreateLaunchSceneForActiveSceneSetupTest(out var launchSceneIsTestScene);

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
            if (launchSceneIsTestScene)
            {
                if (launchScene.IsValid() && launchScene.isLoaded)
                {
                    ClearSceneRootObjects(launchScene);
                    if (!string.IsNullOrEmpty(launchScene.path))
                    {
                        EditorSceneManager.SaveScene(launchScene);
                    }
                }

                if (!testSceneWasCreatedAdditively)
                {
                    testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
            else
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
    }

    private static GamingCouch CreateGamingCouch(string name)
    {
        return GamingCouchEditorTestSupport.CreateGamingCouch(name);
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
        const string AssetsPrefix = "Assets/";
        Assert.That(assetPath.StartsWith(AssetsPrefix, StringComparison.Ordinal), Is.True);
        return Path.Combine(UnityEngine.Application.dataPath, assetPath.Substring(AssetsPrefix.Length));
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

    private void DeleteTestAssetFolder()
    {
        if (string.IsNullOrEmpty(testFolderAssetPath) ||
            !testFolderAssetPath.StartsWith(TestFolderAssetPathPrefix, StringComparison.Ordinal))
        {
            return;
        }

        var deletedThroughAssetDatabase = AssetDatabase.IsValidFolder(testFolderAssetPath) &&
                                          AssetDatabase.DeleteAsset(testFolderAssetPath);
        var fullPath = AssetPathToFullPath(testFolderAssetPath);
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
