using System;
using System.IO;
using DSB.GC;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class GCUnityBuildInfoSidecarWriterTests
{
    [Test]
    public void WritesBuildInfoSidecarForGamingCouchWebGLTemplate()
    {
        var outputRootPath = CreateTemporaryBuildOutputRoot();
        var runtimeSidecarPath = Path.Combine(outputRootPath, GCWebGLRuntimeInfoSidecarWriter.SidecarFileName);
        File.WriteAllText(runtimeSidecarPath, "{\"runtime\":\"unchanged\"}");

        try
        {
            var result = GCUnityBuildInfoSidecarWriter.WriteForBuild(
                BuildTarget.WebGL,
                outputRootPath,
                GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
                CreatePackageIdentity(),
                CreateBuildSummary(outputRootPath),
                CreateWebGLSettings(),
                "6000.0.0f1-test",
                "2026-01-02T03:04:05Z"
            );

            Assert.That(result.WasWritten, Is.True);
            Assert.That(result.status, Is.EqualTo(GCUnityBuildInfoSidecarWriteStatus.Written));
            Assert.That(result.sidecarPath, Is.EqualTo(Path.Combine(outputRootPath, GCUnityBuildInfoSidecarWriter.SidecarFileName)));
            Assert.That(File.ReadAllText(runtimeSidecarPath), Is.EqualTo("{\"runtime\":\"unchanged\"}"));

            var json = File.ReadAllText(result.sidecarPath);
            var sidecar = JsonUtility.FromJson<GCUnityBuildInfoSidecar>(json);

            Assert.That(sidecar.schemaVersion, Is.EqualTo(GCUnityBuildInfoSidecarFactory.SchemaVersion));
            Assert.That(sidecar.capture.capturedAtUtc, Is.EqualTo("2026-01-02T03:04:05Z"));
            Assert.That(sidecar.capture.generator, Is.EqualTo("dsb.gamingcouch.unity"));
            Assert.That(sidecar.unity.version, Is.EqualTo("6000.0.0f1-test"));
            Assert.That(sidecar.package.platform, Is.EqualTo(GCEditorPackageIdentity.Platform));
            Assert.That(sidecar.package.packageName, Is.EqualTo("com.test.build-info"));
            Assert.That(sidecar.package.packageVersion, Is.EqualTo("4.5.6-test.0"));
            Assert.That(sidecar.package.gameProtocolVersion, Is.EqualTo(GCEditorPackageIdentity.GameProtocolVersion));
            Assert.That(sidecar.build.target, Is.EqualTo("WebGL"));
            Assert.That(sidecar.build.targetGroup, Is.EqualTo("WebGL"));
            Assert.That(sidecar.build.template, Is.EqualTo(GamingCouchWebGLExportSetup.ProjectTemplateIdentifier));
            Assert.That(sidecar.build.summary.result, Is.EqualTo("Succeeded"));
            Assert.That(sidecar.build.summary.totalSizeBytes, Is.EqualTo(4096));
            Assert.That(sidecar.build.summary.totalTimeSeconds, Is.EqualTo(1.25d));
            Assert.That(sidecar.build.summary.totalWarnings, Is.EqualTo(2));
            Assert.That(sidecar.build.summary.totalErrors, Is.EqualTo(0));
            Assert.That(sidecar.build.summary.outputPath, Is.EqualTo("."));
            Assert.That(sidecar.build.summary.outputPathKind, Is.EqualTo("buildOutputRelative"));
            Assert.That(sidecar.build.summary.outputPathRedacted, Is.False);
            Assert.That(sidecar.webGLSettings.il2CppCodeGeneration, Is.EqualTo("OptimizeSize"));
            Assert.That(sidecar.webGLSettings.managedStrippingLevel, Is.EqualTo("High"));
            Assert.That(sidecar.webGLSettings.stripUnusedMeshComponents, Is.True);
            Assert.That(sidecar.webGLSettings.dataCaching, Is.True);
            Assert.That(sidecar.webGLSettings.compressionFormat, Is.EqualTo("Disabled"));
            Assert.That(sidecar.webGLSettings.exceptionSupport, Is.EqualTo("ExplicitlyThrownExceptionsOnly"));
            Assert.That(sidecar.webGLSettings.debugSymbolMode, Is.EqualTo("Off"));
#if UNITY_2023_1_OR_NEWER
            Assert.That(sidecar.webGLSettings.webAssembly2023, Is.True);
#endif
            Assert.That(sidecar.webGLSettings.developmentBuild, Is.False);
            Assert.That(sidecar.webGLSettings.codeOptimization, Is.EqualTo("DiskSizeLTO"));
            Assert.That(json, Does.Contain("\"schemaVersion\""));
            Assert.That(json, Does.Contain("\"webGLSettings\""));
            Assert.That(json, Does.Contain("\"outputPathKind\""));
        }
        finally
        {
            DeleteTemporaryPath(outputRootPath);
        }
    }

    [Test]
    public void WritesBuildInfoSidecarNextToIndexWhenBuildOutputPathIsIndexFile()
    {
        var outputRootPath = CreateTemporaryBuildOutputRoot();

        try
        {
            var indexPath = Path.Combine(outputRootPath, "index.html");
            var result = GCUnityBuildInfoSidecarWriter.WriteForBuild(
                BuildTarget.WebGL,
                indexPath,
                GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
                CreatePackageIdentity(),
                CreateBuildSummary(outputRootPath),
                CreateWebGLSettings(),
                "6000.0.0f1-test",
                "2026-01-02T03:04:05Z"
            );

            Assert.That(result.WasWritten, Is.True);
            Assert.That(result.sidecarPath, Is.EqualTo(Path.Combine(outputRootPath, GCUnityBuildInfoSidecarWriter.SidecarFileName)));
        }
        finally
        {
            DeleteTemporaryPath(outputRootPath);
        }
    }

    [Test]
    public void SkipsNonWebGLBuildWithoutWritingBuildInfoSidecar()
    {
        var outputRootPath = CreateTemporaryBuildOutputRoot();

        try
        {
            var result = GCUnityBuildInfoSidecarWriter.WriteForBuild(
                BuildTarget.StandaloneOSX,
                outputRootPath,
                GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
                null,
                null,
                null,
                null,
                null
            );
            var sidecarPath = Path.Combine(outputRootPath, GCUnityBuildInfoSidecarWriter.SidecarFileName);

            Assert.That(result.WasWritten, Is.False);
            Assert.That(result.status, Is.EqualTo(GCUnityBuildInfoSidecarWriteStatus.SkippedNonWebGLBuild));
            Assert.That(result.sidecarPath, Is.EqualTo(sidecarPath));
            Assert.That(File.Exists(sidecarPath), Is.False);
        }
        finally
        {
            DeleteTemporaryPath(outputRootPath);
        }
    }

    [Test]
    public void SkipsOtherWebGLTemplatesAndRemovesStaleBuildInfoSidecar()
    {
        var outputRootPath = CreateTemporaryBuildOutputRoot();

        try
        {
            var sidecarPath = Path.Combine(outputRootPath, GCUnityBuildInfoSidecarWriter.SidecarFileName);
            File.WriteAllText(sidecarPath, "{\"stale\":true}");

            var result = GCUnityBuildInfoSidecarWriter.WriteForBuild(
                BuildTarget.WebGL,
                outputRootPath,
                "PROJECT:OtherTemplate",
                null,
                null,
                null,
                null,
                null
            );

            Assert.That(result.WasWritten, Is.False);
            Assert.That(result.status, Is.EqualTo(GCUnityBuildInfoSidecarWriteStatus.SkippedTemplate));
            Assert.That(result.sidecarPath, Is.EqualTo(sidecarPath));
            Assert.That(File.Exists(sidecarPath), Is.False);
        }
        finally
        {
            DeleteTemporaryPath(outputRootPath);
        }
    }

    [Test]
    public void OverwritesStaleBuildInfoSidecarOnGamingCouchWebGLRebuild()
    {
        var outputRootPath = CreateTemporaryBuildOutputRoot();

        try
        {
            var sidecarPath = Path.Combine(outputRootPath, GCUnityBuildInfoSidecarWriter.SidecarFileName);
            File.WriteAllText(sidecarPath, "{\"stale\":true}");

            var result = GCUnityBuildInfoSidecarWriter.WriteForBuild(
                BuildTarget.WebGL,
                outputRootPath,
                GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
                CreatePackageIdentity(),
                CreateBuildSummary(outputRootPath),
                CreateWebGLSettings(),
                "6000.0.0f1-test",
                "2026-01-02T03:04:05Z"
            );

            Assert.That(result.WasWritten, Is.True);
            Assert.That(File.ReadAllText(sidecarPath), Does.Not.Contain("\"stale\""));
        }
        finally
        {
            DeleteTemporaryPath(outputRootPath);
        }
    }

    [Test]
    public void CapturesRepresentativeTypedWebGLSettings()
    {
        var settings = GCUnityBuildInfoWebGLSettingsCapture.Capture();

        Assert.That(settings.il2CppCodeGeneration, Is.Not.Empty);
        Assert.That(settings.managedStrippingLevel, Is.Not.Empty);
        Assert.That(settings.compressionFormat, Is.Not.Empty);
        Assert.That(settings.exceptionSupport, Is.Not.Empty);
        Assert.That(settings.debugSymbolMode, Is.Not.Empty);
        Assert.That(settings.codeOptimization, Is.Not.Empty);
    }

    [Test]
    public void NormalizesBuildOutputProjectUserHomeAndRelativePaths()
    {
        var context = new GCUnityBuildInfoPathNormalizationContext(
            "/Users/alice/game",
            "/Users/alice/game/BuildOutput",
            "/Users/alice"
        );

        AssertNormalized(
            "/Users/alice/game/BuildOutput/Build/game.wasm",
            context,
            GCUnityBuildInfoNormalizedPathKind.BuildOutputRelative,
            "Build/game.wasm"
        );
        AssertNormalized(
            "/Users/alice/game/Assets/Scenes/Main.unity",
            context,
            GCUnityBuildInfoNormalizedPathKind.ProjectRelative,
            "Assets/Scenes/Main.unity"
        );
        AssertNormalized(
            "/Users/alice/Library/Unity/cache",
            context,
            GCUnityBuildInfoNormalizedPathKind.UserHomeRelative,
            GCUnityBuildInfoPathNormalizer.UserHomeToken + "/Library/Unity/cache"
        );
        AssertNormalized(
            "Build/game.framework.js",
            context,
            GCUnityBuildInfoNormalizedPathKind.Relative,
            "Build/game.framework.js"
        );
    }

    [Test]
    public void NormalizesWindowsPathsAndRedactsUnknownAbsolutePaths()
    {
        var context = new GCUnityBuildInfoPathNormalizationContext(
            @"C:\Users\Ada\Game",
            @"C:\Users\Ada\Game\BuildOutput",
            @"C:\Users\Ada"
        );

        AssertNormalized(
            @"c:\users\ada\game\BuildOutput\Build\game.data",
            context,
            GCUnityBuildInfoNormalizedPathKind.BuildOutputRelative,
            "Build/game.data"
        );
        AssertNormalized(
            @"C:\Users\Ada\Game\Assets\Main.unity",
            context,
            GCUnityBuildInfoNormalizedPathKind.ProjectRelative,
            "Assets/Main.unity"
        );
        AssertNormalized(
            @"C:\Users\Ada\AppData\Local\Unity\cache",
            context,
            GCUnityBuildInfoNormalizedPathKind.UserHomeRelative,
            GCUnityBuildInfoPathNormalizer.UserHomeToken + "/AppData/Local/Unity/cache"
        );

        var normalized = GCUnityBuildInfoPathNormalizer.Normalize(@"D:\Secrets\machine.txt", context);
        Assert.That(normalized.kind, Is.EqualTo(GCUnityBuildInfoNormalizedPathKind.UnknownAbsolute));
        Assert.That(normalized.value, Is.Null);
        Assert.That(normalized.WasRedacted, Is.True);
        Assert.That(normalized.redactionReason, Is.EqualTo(GCUnityBuildInfoPathNormalizer.UnknownAbsolutePathRedactionReason));
    }

    [Test]
    public void RedactsUnknownUnixAbsolutePaths()
    {
        var context = new GCUnityBuildInfoPathNormalizationContext(
            "/Users/alice/game",
            "/Users/alice/game/BuildOutput",
            "/Users/alice"
        );

        var normalized = GCUnityBuildInfoPathNormalizer.Normalize("/private/secret/build/index.html", context);

        Assert.That(normalized.kind, Is.EqualTo(GCUnityBuildInfoNormalizedPathKind.UnknownAbsolute));
        Assert.That(normalized.value, Is.Null);
        Assert.That(normalized.ShouldEmitValue, Is.False);
        Assert.That(normalized.WasRedacted, Is.True);
    }

    [Test]
    public void BuildSummaryCaptureNormalizesPathsBeforeJsonSerialization()
    {
        var projectRoot = "/Users/alice/game";
        var buildOutputRoot = "/Users/alice/game/BuildOutput";
        var userHome = "/Users/alice";
        var unknownAbsolutePath = "/private/secret/build/index.html";
        var pathContext = new GCUnityBuildInfoPathNormalizationContext(projectRoot, buildOutputRoot, userHome);
        var summary = GCUnityBuildInfoBuildSummaryCapture.Create(
            "Succeeded",
            2048,
            TimeSpan.FromSeconds(3.5d),
            1,
            0,
            "build-guid",
            buildOutputRoot + "/Build/game.wasm",
            pathContext
        );
        var redactedSummary = GCUnityBuildInfoBuildSummaryCapture.Create(
            "Succeeded",
            2048,
            TimeSpan.FromSeconds(3.5d),
            1,
            0,
            "build-guid",
            unknownAbsolutePath,
            pathContext
        );

        Assert.That(summary.outputPath, Is.EqualTo("Build/game.wasm"));
        Assert.That(summary.outputPathKind, Is.EqualTo("buildOutputRelative"));
        Assert.That(redactedSummary.outputPath, Is.Null);
        Assert.That(redactedSummary.outputPathRedacted, Is.True);

        var sidecar = GCUnityBuildInfoSidecarFactory.Create(
            BuildTarget.WebGL,
            GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
            CreatePackageIdentity(),
            summary,
            CreateWebGLSettings(),
            "6000.0.0f1-test",
            "2026-01-02T03:04:05Z"
        );
        var redactedSidecar = GCUnityBuildInfoSidecarFactory.Create(
            BuildTarget.WebGL,
            GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
            CreatePackageIdentity(),
            redactedSummary,
            CreateWebGLSettings(),
            "6000.0.0f1-test",
            "2026-01-02T03:04:05Z"
        );
        var json = JsonUtility.ToJson(sidecar, true);
        var redactedJson = JsonUtility.ToJson(redactedSidecar, true);

        Assert.That(json, Does.Not.Contain(projectRoot));
        Assert.That(json, Does.Not.Contain(buildOutputRoot));
        Assert.That(json, Does.Not.Contain(userHome));
        Assert.That(redactedJson, Does.Not.Contain(unknownAbsolutePath));
        Assert.That(redactedJson, Does.Contain("\"outputPathRedacted\""));
    }

    private static void AssertNormalized(
        string path,
        GCUnityBuildInfoPathNormalizationContext context,
        GCUnityBuildInfoNormalizedPathKind expectedKind,
        string expectedValue
    )
    {
        var normalized = GCUnityBuildInfoPathNormalizer.Normalize(path, context);

        Assert.That(normalized.kind, Is.EqualTo(expectedKind));
        Assert.That(normalized.value, Is.EqualTo(expectedValue));
        Assert.That(normalized.ShouldEmitValue, Is.True);
        Assert.That(normalized.WasRedacted, Is.False);
    }

    private static GCPackageIdentity CreatePackageIdentity()
    {
        return GCEditorPackageIdentity.ResolveFromPackageMetadata("com.test.build-info", "4.5.6-test.0");
    }

    private static GCUnityBuildInfoBuildSummary CreateBuildSummary(string outputRootPath)
    {
        return GCUnityBuildInfoBuildSummaryCapture.Create(
            "Succeeded",
            4096,
            TimeSpan.FromSeconds(1.25d),
            2,
            0,
            "test-build-guid",
            outputRootPath,
            new GCUnityBuildInfoPathNormalizationContext(
                "/Users/test/project",
                outputRootPath,
                "/Users/test"
            )
        );
    }

    private static GCUnityBuildInfoWebGLSettings CreateWebGLSettings()
    {
        return new GCUnityBuildInfoWebGLSettings
        {
            il2CppCodeGeneration = "OptimizeSize",
            managedStrippingLevel = "High",
            stripUnusedMeshComponents = true,
            dataCaching = true,
            compressionFormat = "Disabled",
            exceptionSupport = "ExplicitlyThrownExceptionsOnly",
            debugSymbolMode = "Off",
#if UNITY_2023_1_OR_NEWER
            webAssembly2023 = true,
#endif
            developmentBuild = false,
            codeOptimization = "DiskSizeLTO",
        };
    }

    private static string CreateTemporaryBuildOutputRoot()
    {
        var outputRootPath = Path.Combine(
            Path.GetTempPath(),
            "GCUnityBuildInfoSidecarWriterTests_" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(outputRootPath);
        File.WriteAllText(Path.Combine(outputRootPath, "index.html"), "<!doctype html>");
        return outputRootPath;
    }

    private static void DeleteTemporaryPath(string path)
    {
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
}
