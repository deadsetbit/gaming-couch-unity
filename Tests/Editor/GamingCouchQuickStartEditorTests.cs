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

        var listener = new GameObject("Existing Game");
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
        var listener = new GameObject("Existing Game");
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
    public void StartScreenReadinessSummaryReportsNoPendingItemsWhenReady()
    {
        var gamingCouch = CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Existing Listener");
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
        var listener = new GameObject("Existing Listener");
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
        var listener = new GameObject("Existing Listener");
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
        var listener = new GameObject("Existing Listener");
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

    private static void AssertHasEntryContaining(IEnumerable<string> entries, string expectedSubstring)
    {
        Assert.That(
            entries != null && entries.Any(entry =>
                entry != null && entry.IndexOf(expectedSubstring, StringComparison.Ordinal) >= 0
            ),
            Is.True
        );
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
