using System.Collections.Generic;
using DSB.GC;
using NUnit.Framework;

public sealed class GamingCouchEditorInputTests
{
    [Test]
    public void KeyboardInputWinsOverExternalInputForEditorControlledPlayer()
    {
        var externalInputs = new GCControllerInputsData
        {
            a0 = -0.75f,
            a1 = 0.25f,
            b0 = 1,
        };
        var keyboardInputs = new GCControllerInputsData
        {
            a0 = 1.0f,
            a1 = 0.0f,
            b0 = 0,
        };

        var resolved = GamingCouch.ResolveEditorInputs(keyboardInputs, externalInputs, 0.15f);

        Assert.That(resolved.a0, Is.EqualTo(1.0f));
        Assert.That(resolved.a1, Is.EqualTo(0.0f));
        Assert.That(resolved.b0, Is.EqualTo(0));
    }

    [Test]
    public void ExternalInputStillAppliesWhenKeyboardInputIsIdle()
    {
        var externalInputs = new GCControllerInputsData
        {
            a0 = -0.75f,
            a1 = 0.25f,
            b0 = 1,
        };
        var keyboardInputs = new GCControllerInputsData();

        var resolved = GamingCouch.ResolveEditorInputs(keyboardInputs, externalInputs, 0.15f);

        Assert.That(resolved.a0, Is.EqualTo(-0.75f));
        Assert.That(resolved.a1, Is.EqualTo(0.25f));
        Assert.That(resolved.b0, Is.EqualTo(1));
    }

    [Test]
    public void ReleasedKeyboardInputDoesNotBecomeExternalFallbackInput()
    {
        const int playerIndex = 1;
        var gameFacingInputsByPlayerIndex = new Dictionary<int, GCControllerInputs>
        {
            [playerIndex] = new GCControllerInputs(new GCControllerInputsData
            {
                a0 = 1.0f,
                b0 = 1,
            }),
        };
        var externalInputsByPlayerIndex = new Dictionary<int, GCControllerInputs>();
        var releasedKeyboardInputs = new GCControllerInputsData();

        GamingCouch.ApplyEditorInputsForPlayer(
            playerIndex,
            releasedKeyboardInputs,
            gameFacingInputsByPlayerIndex,
            externalInputsByPlayerIndex,
            0.15f
        );

        var resolved = gameFacingInputsByPlayerIndex[playerIndex].RawData;
        Assert.That(resolved.a0, Is.EqualTo(0.0f));
        Assert.That(resolved.a1, Is.EqualTo(0.0f));
        Assert.That(resolved.b0, Is.EqualTo(0));
        Assert.That(resolved.b1, Is.EqualTo(0));
    }

    [Test]
    public void ExternalInputFallbackUsesExternalInputCache()
    {
        const int playerIndex = 1;
        var gameFacingInputsByPlayerIndex = new Dictionary<int, GCControllerInputs>
        {
            [playerIndex] = new GCControllerInputs(new GCControllerInputsData
            {
                a0 = 1.0f,
                b0 = 1,
            }),
        };
        var externalInputsByPlayerIndex = new Dictionary<int, GCControllerInputs>
        {
            [playerIndex] = new GCControllerInputs(new GCControllerInputsData
            {
                a0 = -0.75f,
                a1 = 0.25f,
                b1 = 1,
            }),
        };
        var idleKeyboardInputs = new GCControllerInputsData();

        GamingCouch.ApplyEditorInputsForPlayer(
            playerIndex,
            idleKeyboardInputs,
            gameFacingInputsByPlayerIndex,
            externalInputsByPlayerIndex,
            0.15f
        );

        var resolved = gameFacingInputsByPlayerIndex[playerIndex].RawData;
        Assert.That(resolved.a0, Is.EqualTo(-0.75f));
        Assert.That(resolved.a1, Is.EqualTo(0.25f));
        Assert.That(resolved.b0, Is.EqualTo(0));
        Assert.That(resolved.b1, Is.EqualTo(1));
    }
}
