using System;
using System.Linq;
using System.Reflection;
using DSB.GC;
using NUnit.Framework;

public sealed class GCUnityPlayerIdentityApiMigrationTests
{
    [Test]
    public void ActivePlayerOptionsExposeOnlyGameFacingIdentityFields()
    {
        var fieldNames = typeof(GCActivePlayerOptions)
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Select(field => field.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.That(fieldNames, Is.EqualTo(new[] { "color", "playerIndex", "type" }));
        Assert.That(typeof(GCPlayOptions).GetField("players").FieldType, Is.EqualTo(typeof(GCActivePlayerOptions[])));
    }

    [Test]
    public void RemovedPlayerOptionsTypeFailsAtSourceWithMigrationGuidance()
    {
        var removedType = typeof(GCActivePlayerOptions).Assembly.GetType("DSB.GC.GCPlayerOptions");
        Assert.That(removedType, Is.Not.Null);

        var obsolete = removedType.GetCustomAttribute<ObsoleteAttribute>();
        Assert.That(obsolete, Is.Not.Null);
        Assert.That(obsolete.IsError, Is.True);
        Assert.That(obsolete.Message, Does.Contain("GCActivePlayerOptions"));
        Assert.That(obsolete.Message, Does.Contain("playerIndex"));
    }

    [Test]
    public void RemovedIdentityApisFailAtSourceWithMigrationGuidance()
    {
        AssertObsoleteError(
            typeof(GCPlayer).GetProperty("Id"),
            "GCPlayer.Index"
        );
        AssertObsoleteError(
            typeof(GCPlayer).GetProperty("PlayerName"),
            "Player names are platform-owned"
        );
        AssertObsoleteError(
            typeof(GamingCouch).GetMethod("GetInputsByPlayerId"),
            "GetInputsByPlayerIndex"
        );
        AssertObsoleteError(
            typeof(GCPlayerStore<GCPlayer>).GetMethod("GetPlayerById"),
            "GetPlayerByIndex"
        );
    }

    [Test]
    public void IndexNamedApisUsePlayerIndexParameters()
    {
        Assert.That(
            typeof(GamingCouch).GetMethod("GetInputsByPlayerIndex").GetParameters()[0].Name,
            Is.EqualTo("playerIndex")
        );
        Assert.That(
            typeof(GCPlayerStore<GCPlayer>).GetMethod("GetPlayerByIndex").GetParameters()[0].Name,
            Is.EqualTo("playerIndex")
        );
        Assert.That(
            typeof(GCPlayerSetupOptions).IsPublic,
            Is.False
        );
        Assert.That(
            typeof(GCPlayerSetupOptions).GetField("playerIndex", BindingFlags.Instance | BindingFlags.Public),
            Is.Not.Null
        );
        Assert.That(
            typeof(GCPlayerSetupOptions).GetField("index", BindingFlags.Instance | BindingFlags.Public),
            Is.Null
        );
    }

    private static void AssertObsoleteError(MemberInfo member, string expectedGuidance)
    {
        Assert.That(member, Is.Not.Null);

        var obsolete = member.GetCustomAttribute<ObsoleteAttribute>();
        Assert.That(obsolete, Is.Not.Null);
        Assert.That(obsolete.IsError, Is.True);
        Assert.That(obsolete.Message, Does.Contain(expectedGuidance));
    }
}
