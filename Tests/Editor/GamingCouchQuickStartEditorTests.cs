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
        AssertCheck(ready, GCStartScreenReadinessCheckId.GamingCouchInstance, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(ready, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(ready, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Pass);
        AssertCollapsedGamingCouchChecklist(ready);
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
    public void SetupActionAvailabilityIncludesLaunchReadinessRows()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Existing Listener");
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
            gameViewReady
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
            gameViewMismatch
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

    private static void AssertCollapsedGamingCouchChecklist(GCStartScreenReadiness readiness)
    {
        var checklistIds = readiness.checklist.Select(check => check.id).ToArray();
        var gamingCouchRows = readiness.checklist
            .Where(check => check.id == GCStartScreenReadinessCheckId.GamingCouchInstance)
            .ToArray();
        var checklistLabels = readiness.checklist.Select(check => check.label).ToArray();

        Assert.That(gamingCouchRows, Has.Length.EqualTo(1));
        Assert.That(gamingCouchRows[0].label, Is.EqualTo(GCStartScreenReadiness.GamingCouchInstanceCheckLabel));
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ActiveScene, GCStartScreenReadinessCheckState.Pass);
        Assert.That(checklistIds, Has.No.Member(GCStartScreenReadinessCheckId.ActiveScene));
        Assert.That(checklistIds, Has.Member(GCStartScreenReadinessCheckId.GamingCouchInstance));
        Assert.That(checklistIds, Has.No.Member(GCStartScreenReadinessCheckId.SingleGamingCouchInstance));
        Assert.That(HasActiveSceneChecklistLabel(checklistLabels), Is.False);
        Assert.That(checklistLabels, Does.Not.Contain("GamingCouch object exists"));
        Assert.That(checklistLabels, Does.Not.Contain("Exactly one GamingCouch object exists"));
        Assert.That(
            readiness.GetCheck(GCStartScreenReadinessCheckId.SingleGamingCouchInstance),
            Is.SameAs(readiness.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance))
        );
    }

    private static bool HasActiveSceneChecklistLabel(string[] checklistLabels)
    {
        return checklistLabels.Any(label => string.Equals(label, "Active scene is available", StringComparison.Ordinal));
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
