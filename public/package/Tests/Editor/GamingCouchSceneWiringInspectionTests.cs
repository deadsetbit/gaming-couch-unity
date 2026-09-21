using DSB.GC;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// The boundaries of what the Start Screen counts as the active scene's GamingCouch objects.
// SelectGamingCouchesForInspection decides on one property alone: whether the object's scene is one
// SceneManager enumerates. A DontDestroyOnLoad scene is not, an additively loaded scene is, and
// these tests drive both through the enumerated-scene set. The real Play Mode move is driven for
// real in GamingCouchStartScreenPlayModeReadinessTests.
public sealed class GamingCouchSceneWiringInspectionTests
{
    private Scene scene;

    [SetUp]
    public void SetUp()
    {
        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [TearDown]
    public void TearDown()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void InspectionCountsAGamingCouchOutsideEveryEnumeratedScene()
    {
        var survivor = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");

        var selected = GamingCouchSceneWiring.SelectGamingCouchesForInspection(
            new GamingCouch[0],
            new[] { survivor },
            new Scene[0],
            true
        );

        Assert.That(selected, Is.EqualTo(new[] { survivor }));
    }

    [Test]
    public void InspectionIgnoresAGamingCouchInAnEnumeratedSceneOtherThanTheActiveOne()
    {
        var elsewhere = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");

        var selected = GamingCouchSceneWiring.SelectGamingCouchesForInspection(
            new GamingCouch[0],
            new[] { elsewhere },
            new[] { scene },
            true
        );

        Assert.That(selected, Is.Empty);
    }

    [Test]
    public void InspectionKeepsTheActiveSceneObjectsOnceEach()
    {
        var inActiveScene = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");

        var selected = GamingCouchSceneWiring.SelectGamingCouchesForInspection(
            new[] { inActiveScene },
            new[] { inActiveScene },
            new Scene[0],
            true
        );

        Assert.That(selected, Is.EqualTo(new[] { inActiveScene }));
    }

    [Test]
    public void InspectionOutsidePlayModeStaysWithTheActiveScene()
    {
        var elsewhere = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");

        var selected = GamingCouchSceneWiring.SelectGamingCouchesForInspection(
            new GamingCouch[0],
            new[] { elsewhere },
            new Scene[0],
            false
        );

        Assert.That(selected, Is.Empty);
    }
}
