using System;
using System.IO;
using DSB.GC;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal enum GCWebGLRuntimeInfoSidecarWriteStatus
{
    Written,
    SkippedNonWebGLBuild,
    SkippedTemplate,
}

internal sealed class GCWebGLRuntimeInfoSidecarWriteResult
{
    internal readonly GCWebGLRuntimeInfoSidecarWriteStatus status;
    internal readonly string sidecarPath;

    internal GCWebGLRuntimeInfoSidecarWriteResult(
        GCWebGLRuntimeInfoSidecarWriteStatus status,
        string sidecarPath
    )
    {
        this.status = status;
        this.sidecarPath = sidecarPath;
    }

    internal bool WasWritten
    {
        get { return status == GCWebGLRuntimeInfoSidecarWriteStatus.Written; }
    }
}

internal static class GCWebGLRuntimeInfoSidecarWriter
{
    internal const string SidecarFileName = "gc.runtime-info.json";

    internal static GCWebGLRuntimeInfoSidecarWriteResult WriteForBuild(
        BuildTarget buildTarget,
        string buildOutputPath,
        string webGLTemplate
    )
    {
        if (buildTarget == BuildTarget.WebGL &&
            string.Equals(webGLTemplate, GamingCouchWebGLExportSetup.ProjectTemplateIdentifier, StringComparison.Ordinal))
        {
            return WriteForBuild(buildTarget, buildOutputPath, webGLTemplate, GCEditorPackageIdentity.Resolve());
        }

        return WriteForBuild(buildTarget, buildOutputPath, webGLTemplate, null);
    }

    internal static GCWebGLRuntimeInfoSidecarWriteResult WriteForBuild(
        BuildTarget buildTarget,
        string buildOutputPath,
        string webGLTemplate,
        GCPackageIdentity packageIdentity
    )
    {
        if (buildTarget != BuildTarget.WebGL)
        {
            return new GCWebGLRuntimeInfoSidecarWriteResult(
                GCWebGLRuntimeInfoSidecarWriteStatus.SkippedNonWebGLBuild,
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

        var outputRootPath = ResolveOutputRootPath(buildOutputPath);
        var sidecarPath = Path.Combine(outputRootPath, SidecarFileName);

        Directory.CreateDirectory(outputRootPath);
        File.WriteAllText(sidecarPath, JsonUtility.ToJson(packageIdentity, true));

        return new GCWebGLRuntimeInfoSidecarWriteResult(
            GCWebGLRuntimeInfoSidecarWriteStatus.Written,
            sidecarPath
        );
    }

    private static GCWebGLRuntimeInfoSidecarWriteResult SkipTemplateBuild(string buildOutputPath)
    {
        var sidecarPath = TryResolveSidecarPath(buildOutputPath);
        if (!string.IsNullOrEmpty(sidecarPath) && File.Exists(sidecarPath))
        {
            File.Delete(sidecarPath);
        }

        return new GCWebGLRuntimeInfoSidecarWriteResult(
            GCWebGLRuntimeInfoSidecarWriteStatus.SkippedTemplate,
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

    private static string ResolveOutputRootPath(string buildOutputPath)
    {
        if (string.IsNullOrWhiteSpace(buildOutputPath))
        {
            throw new InvalidOperationException("WebGL runtime info sidecar output path is empty.");
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
                    "WebGL runtime info sidecar output path has no containing directory: " + buildOutputPath
                );
            }

            return directoryName;
        }

        return fullOutputPath;
    }

    private static InvalidOperationException CreateInvalidOutputPathException(string buildOutputPath, Exception exception)
    {
        return new InvalidOperationException(
            "WebGL runtime info sidecar output path is invalid: " + buildOutputPath,
            exception
        );
    }
}

public sealed class GamingCouchWebGLRuntimeInfoSidecarPostprocess : IPostprocessBuildWithReport
{
    public int callbackOrder
    {
        get { return 0; }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        GCWebGLBuildSidecarPostprocessWriter.WriteForBuild(report, PlayerSettings.WebGL.template);
    }
}

internal static class GCWebGLBuildSidecarPostprocessWriter
{
    internal static void WriteForBuild(BuildReport report, string webGLTemplate)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(
            report.summary.platform,
            report.summary.outputPath,
            webGLTemplate
        );

        GCUnityBuildInfoSidecarWriter.WriteForBuild(
            report,
            webGLTemplate
        );
    }

    internal static void WriteForBuild(
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
        GCWebGLRuntimeInfoSidecarWriter.WriteForBuild(buildTarget, buildOutputPath, webGLTemplate);
        GCUnityBuildInfoSidecarWriter.WriteForBuild(
            buildTarget,
            buildOutputPath,
            webGLTemplate,
            packageIdentity,
            buildSummary,
            webGLSettings,
            unityVersion,
            capturedAtUtc
        );
    }
}
