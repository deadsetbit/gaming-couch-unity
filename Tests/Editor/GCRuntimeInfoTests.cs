using System;
using System.IO;
using DSB.GC;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

public sealed class GCRuntimeInfoTests
{
    [Test]
    public void RuntimeInfoJsonKeepsCompatibilityWireFields()
    {
        var runtimeInfo = GCRuntimeInfo.Create();
        var json = GCRuntimeInfo.ToJson();

        Assert.That(runtimeInfo.platform, Is.EqualTo("unity"));
        Assert.That(runtimeInfo.packageName, Is.EqualTo("com.dsb.gamingcouch"));
        Assert.That(runtimeInfo.packageVersion, Is.EqualTo("0.1.0-alpha.3"));
        Assert.That(runtimeInfo.gameProtocolVersion, Is.EqualTo(1));
        Assert.That(json, Does.Contain("\"platform\":\"unity\""));
        Assert.That(json, Does.Contain("\"packageName\":\"com.dsb.gamingcouch\""));
        Assert.That(json, Does.Contain("\"packageVersion\":\"0.1.0-alpha.3\""));
        Assert.That(json, Does.Contain("\"gameProtocolVersion\":1"));
    }

    [Test]
    public void RuntimePackageIdentityMatchesPackageManifest()
    {
        var manifest = ReadPackageManifest();

        Assert.That(GCRuntimeInfo.PackageName, Is.EqualTo(manifest.name));
        Assert.That(GCRuntimeInfo.PackageVersion, Is.EqualTo(manifest.version));
    }

    [Test]
    public void WebGLBridgeForwardsRuntimeInfoToBrowserCallback()
    {
        var bridgePath = Path.Combine(FindPackageRootPath(), "Plugins", "GamingCouch.jslib");
        var bridge = File.ReadAllText(bridgePath);

        Assert.That(bridge, Does.Contain("GamingCouchRegisterRuntimeInfo"));
        Assert.That(bridge, Does.Contain("window.gamingCouchRegisterRuntimeInfo"));
        Assert.That(bridge, Does.Contain("JSON.parse(UTF8ToString(runtimeInfoJsonString))"));
        Assert.That(bridge, Does.Contain("window.gamingCouchRegisterRuntimeInfo(runtimeInfo)"));
    }

    private static PackageManifest ReadPackageManifest()
    {
        var manifestPath = Path.Combine(FindPackageRootPath(), "package.json");
        return JsonUtility.FromJson<PackageManifest>(File.ReadAllText(manifestPath));
    }

    private static string FindPackageRootPath()
    {
        var packageInfo = PackageInfo.FindForAssembly(typeof(GCRuntimeInfo).Assembly);
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
