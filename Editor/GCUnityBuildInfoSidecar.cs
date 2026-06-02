using System;
using System.Globalization;
using System.IO;
using DSB.GC;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal enum GCUnityBuildInfoSidecarWriteStatus
{
    Written,
    SkippedNonWebGLBuild,
    SkippedTemplate,
}

internal sealed class GCUnityBuildInfoSidecarWriteResult
{
    internal readonly GCUnityBuildInfoSidecarWriteStatus status;
    internal readonly string sidecarPath;

    internal GCUnityBuildInfoSidecarWriteResult(
        GCUnityBuildInfoSidecarWriteStatus status,
        string sidecarPath
    )
    {
        this.status = status;
        this.sidecarPath = sidecarPath;
    }

    internal bool WasWritten
    {
        get { return status == GCUnityBuildInfoSidecarWriteStatus.Written; }
    }
}

internal static class GCUnityBuildInfoSidecarWriter
{
    internal const string SidecarFileName = "gc.unity-build-info.json";

    internal static GCUnityBuildInfoSidecarWriteResult WriteForBuild(
        BuildReport report,
        string webGLTemplate
    )
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        var summary = report.summary;
        if (summary.platform != BuildTarget.WebGL ||
            !string.Equals(webGLTemplate, GamingCouchWebGLExportSetup.ProjectTemplateIdentifier, StringComparison.Ordinal))
        {
            return WriteForBuild(
                summary.platform,
                summary.outputPath,
                webGLTemplate,
                null,
                null,
                null,
                null,
                null
            );
        }

        return WriteForBuild(
            summary.platform,
            summary.outputPath,
            webGLTemplate,
            GCEditorPackageIdentity.Resolve(),
            GCUnityBuildInfoBuildSummaryCapture.Capture(report, ResolveOutputRootPath(summary.outputPath)),
            GCUnityBuildInfoWebGLSettingsCapture.Capture(),
            Application.unityVersion,
            GCUnityBuildInfoCaptureClock.CaptureUtcNow()
        );
    }

    internal static GCUnityBuildInfoSidecarWriteResult WriteForBuild(
        BuildTarget buildTarget,
        string buildOutputPath,
        string webGLTemplate,
        GCPackageIdentity packageIdentity,
        GCUnityBuildInfoBuildSummary buildSummary,
        GCUnityBuildInfoWebGLSettings webGLSettings,
        string unityVersion,
        string capturedAtUtc
    )
    {
        if (buildTarget != BuildTarget.WebGL)
        {
            return new GCUnityBuildInfoSidecarWriteResult(
                GCUnityBuildInfoSidecarWriteStatus.SkippedNonWebGLBuild,
                TryResolveSidecarPath(buildOutputPath)
            );
        }

        if (!string.Equals(webGLTemplate, GamingCouchWebGLExportSetup.ProjectTemplateIdentifier, StringComparison.Ordinal))
        {
            return SkipTemplateBuild(buildOutputPath);
        }

        if (packageIdentity == null)
        {
            throw new ArgumentNullException(nameof(packageIdentity));
        }

        if (buildSummary == null)
        {
            throw new ArgumentNullException(nameof(buildSummary));
        }

        if (webGLSettings == null)
        {
            throw new ArgumentNullException(nameof(webGLSettings));
        }

        var outputRootPath = ResolveOutputRootPath(buildOutputPath);
        var sidecarPath = Path.Combine(outputRootPath, SidecarFileName);
        var sidecar = GCUnityBuildInfoSidecarFactory.Create(
            buildTarget,
            webGLTemplate,
            packageIdentity,
            buildSummary,
            webGLSettings,
            unityVersion,
            capturedAtUtc
        );

        Directory.CreateDirectory(outputRootPath);
        File.WriteAllText(sidecarPath, JsonUtility.ToJson(sidecar, true));

        return new GCUnityBuildInfoSidecarWriteResult(
            GCUnityBuildInfoSidecarWriteStatus.Written,
            sidecarPath
        );
    }

    private static GCUnityBuildInfoSidecarWriteResult SkipTemplateBuild(string buildOutputPath)
    {
        var sidecarPath = TryResolveSidecarPath(buildOutputPath);
        if (!string.IsNullOrEmpty(sidecarPath) && File.Exists(sidecarPath))
        {
            File.Delete(sidecarPath);
        }

        return new GCUnityBuildInfoSidecarWriteResult(
            GCUnityBuildInfoSidecarWriteStatus.SkippedTemplate,
            sidecarPath
        );
    }

    private static string TryResolveSidecarPath(string buildOutputPath)
    {
        if (string.IsNullOrWhiteSpace(buildOutputPath))
        {
            return null;
        }

        try
        {
            return Path.Combine(ResolveOutputRootPath(buildOutputPath), SidecarFileName);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    internal static string ResolveOutputRootPath(string buildOutputPath)
    {
        if (string.IsNullOrWhiteSpace(buildOutputPath))
        {
            throw new InvalidOperationException("Unity build info sidecar output path is empty.");
        }

        string fullOutputPath;
        try
        {
            fullOutputPath = Path.GetFullPath(buildOutputPath);
        }
        catch (ArgumentException exception)
        {
            throw CreateInvalidOutputPathException(buildOutputPath, exception);
        }
        catch (NotSupportedException exception)
        {
            throw CreateInvalidOutputPathException(buildOutputPath, exception);
        }
        catch (PathTooLongException exception)
        {
            throw CreateInvalidOutputPathException(buildOutputPath, exception);
        }

        if (File.Exists(fullOutputPath))
        {
            var directoryName = Path.GetDirectoryName(fullOutputPath);
            if (string.IsNullOrEmpty(directoryName))
            {
                throw new InvalidOperationException(
                    "Unity build info sidecar output path has no containing directory: " + buildOutputPath
                );
            }

            return directoryName;
        }

        return fullOutputPath;
    }

    private static InvalidOperationException CreateInvalidOutputPathException(string buildOutputPath, Exception exception)
    {
        return new InvalidOperationException(
            "Unity build info sidecar output path is invalid: " + buildOutputPath,
            exception
        );
    }
}

internal static class GCUnityBuildInfoSidecarFactory
{
    internal const int SchemaVersion = 1;

    internal static GCUnityBuildInfoSidecar Create(
        BuildTarget buildTarget,
        string webGLTemplate,
        GCPackageIdentity packageIdentity,
        GCUnityBuildInfoBuildSummary buildSummary,
        GCUnityBuildInfoWebGLSettings webGLSettings,
        string unityVersion,
        string capturedAtUtc
    )
    {
        return new GCUnityBuildInfoSidecar
        {
            schemaVersion = SchemaVersion,
            capture = new GCUnityBuildInfoCaptureMetadata
            {
                capturedAtUtc = string.IsNullOrWhiteSpace(capturedAtUtc)
                    ? GCUnityBuildInfoCaptureClock.CaptureUtcNow()
                    : capturedAtUtc,
                generator = "dsb.gamingcouch.unity",
            },
            unity = new GCUnityBuildInfoUnityEditorMetadata
            {
                version = string.IsNullOrWhiteSpace(unityVersion) ? Application.unityVersion : unityVersion,
            },
            package = new GCUnityBuildInfoPackageMetadata
            {
                platform = packageIdentity.platform,
                packageName = packageIdentity.packageName,
                packageVersion = packageIdentity.packageVersion,
                gameProtocolVersion = packageIdentity.gameProtocolVersion,
            },
            build = new GCUnityBuildInfoBuildMetadata
            {
                target = buildTarget.ToString(),
                targetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget).ToString(),
                template = webGLTemplate,
                summary = buildSummary,
            },
            webGLSettings = webGLSettings,
        };
    }
}

internal static class GCUnityBuildInfoCaptureClock
{
    internal static string CaptureUtcNow()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }
}

internal static class GCUnityBuildInfoBuildSummaryCapture
{
    internal static GCUnityBuildInfoBuildSummary Capture(BuildReport report, string buildOutputRootPath)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        var summary = report.summary;
        var normalizedOutputPath = GCUnityBuildInfoPathNormalizer.Normalize(
            summary.outputPath,
            new GCUnityBuildInfoPathNormalizationContext(
                ResolveProjectRootPath(),
                buildOutputRootPath,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            )
        );

        return new GCUnityBuildInfoBuildSummary
        {
            result = summary.result.ToString(),
            totalSizeBytes = ClampToInt64(summary.totalSize),
            totalTimeSeconds = Math.Round(summary.totalTime.TotalSeconds, 3),
            totalWarnings = summary.totalWarnings,
            totalErrors = summary.totalErrors,
            guid = summary.guid.ToString(),
            outputPath = normalizedOutputPath.ShouldEmitValue ? normalizedOutputPath.value : null,
            outputPathKind = FormatPathKind(normalizedOutputPath.kind),
            outputPathRedacted = normalizedOutputPath.WasRedacted,
            outputPathRedactionReason = normalizedOutputPath.redactionReason,
        };
    }

    private static long ClampToInt64(ulong value)
    {
        return value > long.MaxValue ? long.MaxValue : (long)value;
    }

    private static string ResolveProjectRootPath()
    {
        if (string.IsNullOrWhiteSpace(Application.dataPath))
        {
            return null;
        }

        var parent = Directory.GetParent(Application.dataPath);
        return parent != null ? parent.FullName : null;
    }

    private static string FormatPathKind(GCUnityBuildInfoNormalizedPathKind kind)
    {
        switch (kind)
        {
            case GCUnityBuildInfoNormalizedPathKind.Empty:
                return "empty";
            case GCUnityBuildInfoNormalizedPathKind.Relative:
                return "relative";
            case GCUnityBuildInfoNormalizedPathKind.ProjectRelative:
                return "projectRelative";
            case GCUnityBuildInfoNormalizedPathKind.BuildOutputRelative:
                return "buildOutputRelative";
            case GCUnityBuildInfoNormalizedPathKind.UserHomeRelative:
                return "userHomeRelative";
            case GCUnityBuildInfoNormalizedPathKind.UnknownAbsolute:
                return "unknownAbsolute";
            default:
                return "unknown";
        }
    }
}

internal static class GCUnityBuildInfoWebGLSettingsCapture
{
    internal static GCUnityBuildInfoWebGLSettings Capture()
    {
        return new GCUnityBuildInfoWebGLSettings
        {
            il2CppCodeGeneration = PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL).ToString(),
            managedStrippingLevel = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL).ToString(),
            stripUnusedMeshComponents = PlayerSettings.stripUnusedMeshComponents,
            dataCaching = PlayerSettings.WebGL.dataCaching,
            compressionFormat = PlayerSettings.WebGL.compressionFormat.ToString(),
            exceptionSupport = PlayerSettings.WebGL.exceptionSupport.ToString(),
            debugSymbolMode = PlayerSettings.WebGL.debugSymbolMode.ToString(),
#if UNITY_2023_1_OR_NEWER
            webAssembly2023 = PlayerSettings.WebGL.wasm2023,
#endif
            developmentBuild = EditorUserBuildSettings.development,
            codeOptimization = UnityEditor.WebGL.UserBuildSettings.codeOptimization.ToString(),
        };
    }
}

[Serializable]
internal sealed class GCUnityBuildInfoSidecar
{
    public int schemaVersion;
    public GCUnityBuildInfoCaptureMetadata capture;
    public GCUnityBuildInfoUnityEditorMetadata unity;
    public GCUnityBuildInfoPackageMetadata package;
    public GCUnityBuildInfoBuildMetadata build;
    public GCUnityBuildInfoWebGLSettings webGLSettings;
}

[Serializable]
internal sealed class GCUnityBuildInfoCaptureMetadata
{
    public string capturedAtUtc;
    public string generator;
}

[Serializable]
internal sealed class GCUnityBuildInfoUnityEditorMetadata
{
    public string version;
}

[Serializable]
internal sealed class GCUnityBuildInfoPackageMetadata
{
    public string platform;
    public string packageName;
    public string packageVersion;
    public int gameProtocolVersion;
}

[Serializable]
internal sealed class GCUnityBuildInfoBuildMetadata
{
    public string target;
    public string targetGroup;
    public string template;
    public GCUnityBuildInfoBuildSummary summary;
}

[Serializable]
internal sealed class GCUnityBuildInfoBuildSummary
{
    public string result;
    public long totalSizeBytes;
    public double totalTimeSeconds;
    public int totalWarnings;
    public int totalErrors;
    public string guid;
    public string outputPath;
    public string outputPathKind;
    public bool outputPathRedacted;
    public string outputPathRedactionReason;
}

[Serializable]
internal sealed class GCUnityBuildInfoWebGLSettings
{
    public string il2CppCodeGeneration;
    public string managedStrippingLevel;
    public bool stripUnusedMeshComponents;
    public bool dataCaching;
    public string compressionFormat;
    public string exceptionSupport;
    public string debugSymbolMode;
#if UNITY_2023_1_OR_NEWER
    public bool webAssembly2023;
#endif
    public bool developmentBuild;
    public string codeOptimization;
}
