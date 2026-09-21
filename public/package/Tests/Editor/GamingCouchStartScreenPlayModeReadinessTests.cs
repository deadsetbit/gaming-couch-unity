using System;
using System.Collections;
using DSB.GC;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Play Mode is the one state the Start Screen cannot reach from the active scene's roots:
// GamingCouch.Awake marks the object DontDestroyOnLoad, which moves it into a scene SceneManager
// does not enumerate. These tests drive the real transition rather than simulating it, because the
// move is Unity's, not the package's.
//
// Domain reload is disabled for the duration so the running test survives entering Play Mode. Scene
// reload stays on, so the transition behaves as it does for a developer pressing Play.
public sealed class GamingCouchStartScreenPlayModeReadinessTests
{
    private bool previousEnterPlayModeOptionsEnabled;
    private EnterPlayModeOptions previousEnterPlayModeOptions;
    private EditorBuildSettingsScene[] previousBuildSettingsScenes;

    [SetUp]
    public void SetUp()
    {
        previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
        previousBuildSettingsScenes = EditorBuildSettings.scenes;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
    }

    [UnityTearDown]
    public IEnumerator UnityTearDown()
    {
        if (EditorApplication.isPlaying)
        {
            yield return ExitPlayMode();
        }

        LogAssert.ignoreFailingMessages = false;
        EditorBuildSettings.scenes = previousBuildSettingsScenes ?? Array.Empty<EditorBuildSettingsScene>();
        EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
        EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator WiredSceneStaysReadyWhilePlaying()
    {
        CreateWiredActiveScene();
        AssertGamingCouchChecksPass("before entering Play Mode");

        yield return EnterPlayMode();

        AssertGamingCouchChecksPass("during Play Mode");
        var instanceCheck = InspectGamingCouchInstanceCheck();
        Assert.That(
            instanceCheck.action.id,
            Is.Not.EqualTo(GCStartScreenReadinessActionId.CreateGamingCouch),
            "the Start Screen offered to create a GamingCouch that the playing scene already has"
        );
    }

    [UnityTest]
    public IEnumerator SceneSetupActionsDuringPlayModeRefuseInTheWindow()
    {
        CreateWiredActiveScene();

        yield return EnterPlayMode();

        var gamingCouchesBefore = CountLoadedGamingCouches();
        var result = GamingCouchStartScreenSetupActions.RunSetupAction(
            GCStartScreenReadinessActionId.CreateGamingCouch
        );

        Assert.That(
            GamingCouchStartScreenSetupActions.ShouldDisplayActionResult(result.messageType),
            Is.True,
            "the refusal never reached the window: " + result.message
        );
        Assert.That(result.message, Does.Contain("Play Mode"));

        // Destroy() defers to the end of the frame, so a duplicate would still be counted now.
        yield return null;
        yield return null;
        Assert.That(
            CountLoadedGamingCouches(),
            Is.EqualTo(gamingCouchesBefore),
            "a second GamingCouch was created during Play Mode"
        );
    }

    [UnityTest]
    public IEnumerator SetUpMissingPiecesDuringPlayModeChangesNoBuildSettings()
    {
        CreateWiredActiveScene();
        EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();

        yield return EnterPlayMode();

        var result = GamingCouchStartScreenSetupActions.RunActiveSceneSetup();

        Assert.That(
            GamingCouchStartScreenSetupActions.ShouldDisplayActionResult(result.messageType),
            Is.True,
            "the refusal never reached the window: " + result.message
        );
        Assert.That(result.message, Does.Contain("Play Mode"));
        Assert.That(
            EditorBuildSettings.scenes,
            Is.Empty,
            "Build Settings were rewritten during Play Mode"
        );
    }

    private static int CountLoadedGamingCouches()
    {
        return UnityEngine.Object
            .FindObjectsByType<GamingCouch>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Length;
    }

    private static void CreateWiredActiveScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var gamingCouchObject = new GameObject("GamingCouch");
        gamingCouchObject.SetActive(false);
        var gamingCouch = gamingCouchObject.AddComponent<GamingCouch>();
        GamingCouchSceneWiring.AssignListenerIfMissing(
            gamingCouch,
            GamingCouchEditorTestSupport.CreateCompatibleListener("Game")
        );
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(
            gamingCouch,
            GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Player")
        );
        gamingCouchObject.SetActive(true);
    }

    private static void AssertGamingCouchChecksPass(string phase)
    {
        var readiness = GCStartScreenReadinessService.InspectActiveScene();

        Assert.That(readiness.gamingCouches, Has.Length.EqualTo(1), "GamingCouch objects found " + phase);
        AssertCheckPasses(readiness, GCStartScreenReadinessCheckId.GamingCouchInstance, phase);
        AssertCheckPasses(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, phase);
        AssertCheckPasses(readiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, phase);
    }

    private static void AssertCheckPasses(
        GCStartScreenReadiness readiness,
        GCStartScreenReadinessCheckId id,
        string phase
    )
    {
        var check = readiness.GetCheck(id);
        Assert.That(
            check.state,
            Is.EqualTo(GCStartScreenReadinessCheckState.Pass),
            id + " " + phase + ": " + check.message
        );
    }

    private static GCStartScreenReadinessCheck InspectGamingCouchInstanceCheck()
    {
        return GCStartScreenReadinessService
            .InspectActiveScene()
            .GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance);
    }

    // Entering Play Mode runs the whole GamingCouch runtime, whose logging depends on the host
    // project's local play configuration rather than on anything under test here.
    private static IEnumerator EnterPlayMode()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorApplication.EnterPlaymode();
        yield return WaitForPlayMode(true);
        LogAssert.ignoreFailingMessages = true;
        yield return null;
    }

    private static IEnumerator ExitPlayMode()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorApplication.ExitPlaymode();
        yield return WaitForPlayMode(false);
        LogAssert.ignoreFailingMessages = true;
        yield return null;
    }

    private static IEnumerator WaitForPlayMode(bool playing)
    {
        var deadlineSeconds = Time.realtimeSinceStartup + 30f;
        while (EditorApplication.isPlaying != playing && Time.realtimeSinceStartup < deadlineSeconds)
        {
            yield return null;
        }

        Assert.That(
            EditorApplication.isPlaying,
            Is.EqualTo(playing),
            playing ? "the editor never entered Play Mode" : "the editor never left Play Mode"
        );
    }
}
