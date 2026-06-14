using System.Collections.Generic;
using System.Reflection;
using DSB.GC;
using NUnit.Framework;
using UnityEngine;

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

    [Test]
    public void DevAppInputApplyPreservesB2AndAcceptsNeutralRelease()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch devapp input test");
        try
        {
            SetPrivateField(
                gamingCouch,
                "playerIndexMapping",
                GCActiveRunProjection.Create(CreatePlayOptions(123, GCPlayerType.player)).PlayerIndexMapping
            );

            gamingCouch.ApplyDevAppInput(
                0,
                new GCControllerInputsData
                {
                    a0 = 0.75f,
                    a1 = -0.5f,
                    b0 = 1,
                    b2 = 1,
                }
            );

            var activeInputs = gamingCouch.GetInputsByPlayerIndex(0).RawData;
            Assert.That(activeInputs.a0, Is.EqualTo(0.75f));
            Assert.That(activeInputs.a1, Is.EqualTo(-0.5f));
            Assert.That(activeInputs.b0, Is.EqualTo(1));
            Assert.That(activeInputs.b2, Is.EqualTo(1));

            gamingCouch.ApplyDevAppInput(0, new GCControllerInputsData());

            var neutralInputs = gamingCouch.GetInputsByPlayerIndex(0).RawData;
            Assert.That(neutralInputs.a0, Is.EqualTo(0f));
            Assert.That(neutralInputs.a1, Is.EqualTo(0f));
            Assert.That(neutralInputs.b0, Is.EqualTo(0));
            Assert.That(neutralInputs.b1, Is.EqualTo(0));
            Assert.That(neutralInputs.b2, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(gamingCouch.gameObject);
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target
            .GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static GCPlayOptions CreatePlayOptions(int seed, params GCPlayerType[] playerTypes)
    {
        var players = new GCPlayerOptions[playerTypes.Length];
        for (var index = 0; index < playerTypes.Length; index++)
        {
            players[index] = new GCPlayerOptions
            {
                playerIndex = index,
                type = playerTypes[index].ToString(),
                color = GCPlayerColor.blue.ToString(),
            };
        }

        return new GCPlayOptions
        {
            players = players,
            seed = seed,
        };
    }
}
