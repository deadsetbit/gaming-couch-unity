using System;
using System.IO;
using System.Reflection;
using DSB.GC;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

public sealed class GCRuntimeInfoTests
{
    [Test]
    public void RuntimeInfoShapeCanSerializePackageIdentityFields()
    {
        var manifest = ReadPackageManifest();
        var runtimeInfo = GCEditorPackageIdentity.Resolve().ToRuntimeInfo();
        var json = JsonUtility.ToJson(runtimeInfo);

        Assert.That(runtimeInfo.platform, Is.EqualTo(GCEditorPackageIdentity.Platform));
        Assert.That(runtimeInfo.packageName, Is.EqualTo(manifest.name));
        Assert.That(runtimeInfo.packageVersion, Is.EqualTo(manifest.version));
        Assert.That(runtimeInfo.gameProtocolVersion, Is.EqualTo(GCEditorPackageIdentity.GameProtocolVersion));
        Assert.That(json, Does.Contain("\"platform\":\"" + GCEditorPackageIdentity.Platform + "\""));
        Assert.That(json, Does.Contain("\"packageName\":\"" + manifest.name + "\""));
        Assert.That(json, Does.Contain("\"packageVersion\":\"" + manifest.version + "\""));
        Assert.That(json, Does.Contain("\"gameProtocolVersion\":" + GCEditorPackageIdentity.GameProtocolVersion));
    }

    [Test]
    public void RuntimeInfoIsSerializableDtoOnly()
    {
        var fields = typeof(GCRuntimeInfo).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly
        );
        var staticFields = typeof(GCRuntimeInfo).GetFields(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
        );
        var declaredMethods = typeof(GCRuntimeInfo).GetMethods(
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
        );

        Assert.That(Attribute.IsDefined(typeof(GCRuntimeInfo), typeof(SerializableAttribute)), Is.True);
        Assert.That(Array.ConvertAll(fields, field => field.Name), Is.EquivalentTo(new[]
        {
            "platform",
            "packageName",
            "packageVersion",
            "gameProtocolVersion",
        }));
        Assert.That(typeof(GCRuntimeInfo).GetField("platform").FieldType, Is.EqualTo(typeof(string)));
        Assert.That(typeof(GCRuntimeInfo).GetField("packageName").FieldType, Is.EqualTo(typeof(string)));
        Assert.That(typeof(GCRuntimeInfo).GetField("packageVersion").FieldType, Is.EqualTo(typeof(string)));
        Assert.That(typeof(GCRuntimeInfo).GetField("gameProtocolVersion").FieldType, Is.EqualTo(typeof(int)));
        Assert.That(staticFields, Is.Empty);
        Assert.That(declaredMethods, Is.Empty);
    }

    [Test]
    public void WebGLRuntimeCallbackPathIsRemoved()
    {
        var bridgePath = Path.Combine(FindPackageRootPath(), "Plugins", "GamingCouch.jslib");
        var bridge = File.ReadAllText(bridgePath);
        var runtimePath = Path.Combine(FindPackageRootPath(), "Runtime", "GamingCouch.cs");
        var runtime = File.ReadAllText(runtimePath);

        Assert.That(runtime, Does.Not.Contain("GamingCouchRegisterRuntimeInfo"));
        Assert.That(runtime, Does.Not.Contain("SendRuntimeInfo"));
        Assert.That(runtime, Does.Not.Contain("GCRuntimeInfo.ToJson"));
        Assert.That(bridge, Does.Not.Contain("GamingCouchRegisterRuntimeInfo"));
        Assert.That(bridge, Does.Not.Contain("gamingCouchRegisterRuntimeInfo"));
        Assert.That(bridge, Does.Not.Contain("runtimeInfoJsonString"));
    }

    private static PackageManifest ReadPackageManifest()
    {
        var manifestPath = Path.Combine(FindPackageRootPath(), "package.json");
        return JsonUtility.FromJson<PackageManifest>(File.ReadAllText(manifestPath));
    }

    private static string FindPackageRootPath()
    {
        var packageInfo = PackageInfo.FindForAssembly(typeof(GamingCouch).Assembly);
        if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
        {
            return packageInfo.resolvedPath;
        }

        throw new InvalidOperationException("Could not resolve Gaming Couch package root.");
    }

    [Serializable]
    private sealed class PackageManifest
    {
        public string name;
        public string version;
    }
}
