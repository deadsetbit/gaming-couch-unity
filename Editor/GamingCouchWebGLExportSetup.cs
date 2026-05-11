using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEngine;

internal enum GCWebGLExportSetupStatus
{
    Ready,
    Warning,
    Blocked,
}

internal enum GCWebGLExportTemplateInstallStatus
{
    Ready,
    Blocked,
}

internal sealed class GCWebGLExportTemplateInstallResult
{
    internal readonly GCWebGLExportTemplateInstallStatus status;
    internal readonly bool changed;
    internal readonly string message;
    internal readonly string[] createdPaths;
    internal readonly string[] reusedPaths;
    internal readonly string[] blockedReasons;

    internal GCWebGLExportTemplateInstallResult(
        GCWebGLExportTemplateInstallStatus status,
        bool changed,
        string message,
        string[] createdPaths,
        string[] reusedPaths,
        string[] blockedReasons
    )
    {
        this.status = status;
        this.changed = changed;
        this.message = message;
        this.createdPaths = createdPaths ?? new string[0];
        this.reusedPaths = reusedPaths ?? new string[0];
        this.blockedReasons = blockedReasons ?? new string[0];
    }

    internal bool IsBlocked
    {
        get { return status == GCWebGLExportTemplateInstallStatus.Blocked; }
    }
}

internal sealed class GCWebGLExportReadiness
{
    internal readonly GCWebGLExportSetupStatus status;
    internal readonly bool templateFolderReady;
    internal readonly bool templateFilesReady;
    internal readonly bool templateSelected;
    internal readonly bool releaseSettingsReady;
    internal readonly bool splashSettingsReady;
    internal readonly bool activeBuildTargetIsWebGL;
    internal readonly string message;
    internal readonly string[] details;

    internal GCWebGLExportReadiness(
        GCWebGLExportSetupStatus status,
        bool templateFolderReady,
        bool templateFilesReady,
        bool templateSelected,
        bool releaseSettingsReady,
        bool splashSettingsReady,
        bool activeBuildTargetIsWebGL,
        string message,
        string[] details
    )
    {
        this.status = status;
        this.templateFolderReady = templateFolderReady;
        this.templateFilesReady = templateFilesReady;
        this.templateSelected = templateSelected;
        this.releaseSettingsReady = releaseSettingsReady;
        this.splashSettingsReady = splashSettingsReady;
        this.activeBuildTargetIsWebGL = activeBuildTargetIsWebGL;
        this.message = message;
        this.details = details ?? new string[0];
    }

    internal bool IsReady
    {
        get { return status == GCWebGLExportSetupStatus.Ready; }
    }

    internal bool HasWarning
    {
        get { return status == GCWebGLExportSetupStatus.Warning; }
    }

    internal bool IsBlocked
    {
        get { return status == GCWebGLExportSetupStatus.Blocked; }
    }
}

internal sealed class GCWebGLExportSetupResult
{
    internal readonly GCWebGLExportSetupStatus status;
    internal readonly bool changed;
    internal readonly string message;
    internal readonly string[] details;
    internal readonly GCWebGLExportReadiness readiness;

    internal GCWebGLExportSetupResult(
        GCWebGLExportSetupStatus status,
        bool changed,
        string message,
        string[] details,
        GCWebGLExportReadiness readiness
    )
    {
        this.status = status;
        this.changed = changed;
        this.message = message;
        this.details = details ?? new string[0];
        this.readiness = readiness;
    }

    internal bool IsBlocked
    {
        get { return status == GCWebGLExportSetupStatus.Blocked; }
    }

    internal bool HasWarning
    {
        get { return status == GCWebGLExportSetupStatus.Warning; }
    }
}

internal static class GamingCouchWebGLExportSetup
{
    internal const string TemplateName = "GamingCouch";
    internal const string ProjectTemplateIdentifier = "PROJECT:" + TemplateName;
    internal const string PackageTemplateAssetPath = "Editor/WebGLTemplates/" + TemplateName;
    internal const string ProjectTemplatesFolderAssetPath = "Assets/WebGLTemplates";
    internal const string ProjectTemplateAssetPath = ProjectTemplatesFolderAssetPath + "/" + TemplateName;

    private static readonly string[] ExpectedTemplateFiles = { "index.html" };

    internal static GCWebGLExportSetupResult EnsureCleanWebGLExportSetup()
    {
        var packageTemplatePath = LocatePackageTemplatePath();
        if (string.IsNullOrEmpty(packageTemplatePath))
        {
            var readiness = InspectReadiness();
            return new GCWebGLExportSetupResult(
                GCWebGLExportSetupStatus.Blocked,
                false,
                "Clean WebGL export setup is blocked.",
                new[] { "Could not locate the package-owned GamingCouch WebGL template source." },
                readiness
            );
        }

        var destinationPath = AssetPathToFullPath(ProjectTemplateAssetPath);
        return EnsureCleanWebGLExportSetup(packageTemplatePath, destinationPath, true);
    }

    internal static GCWebGLExportSetupResult EnsureCleanWebGLExportSetup(
        string sourceTemplateDirectoryFullPath,
        string destinationTemplateDirectoryFullPath,
        bool refreshAssetDatabase
    )
    {
        var details = new List<string>();
        var installResult = InstallTemplateFiles(
            sourceTemplateDirectoryFullPath,
            destinationTemplateDirectoryFullPath,
            refreshAssetDatabase
        );

        details.Add(installResult.message);
        details.AddRange(installResult.createdPaths);
        details.AddRange(installResult.reusedPaths);
        if (installResult.IsBlocked)
        {
            details.AddRange(installResult.blockedReasons);
            return new GCWebGLExportSetupResult(
                GCWebGLExportSetupStatus.Blocked,
                installResult.changed,
                "Clean WebGL export setup is blocked.",
                details.ToArray(),
                InspectReadiness(destinationTemplateDirectoryFullPath)
            );
        }

        var changed = installResult.changed;
        changed |= SelectTemplate(details);
        changed |= ApplyCleanReleaseDefaults(details);

        var readiness = InspectReadiness(destinationTemplateDirectoryFullPath);
        details.AddRange(readiness.details);

        return new GCWebGLExportSetupResult(
            readiness.status,
            changed,
            readiness.IsReady
                ? "Clean WebGL export setup is ready."
                : readiness.message,
            details.ToArray(),
            readiness
        );
    }

    internal static GCWebGLExportTemplateInstallResult InstallTemplateFiles(
        string sourceTemplateDirectoryFullPath,
        string destinationTemplateDirectoryFullPath,
        bool refreshAssetDatabase
    )
    {
        var createdPaths = new List<string>();
        var reusedPaths = new List<string>();
        var blockedReasons = new List<string>();

        if (string.IsNullOrEmpty(sourceTemplateDirectoryFullPath) ||
            !Directory.Exists(sourceTemplateDirectoryFullPath))
        {
            blockedReasons.Add("Template source folder does not exist: " + sourceTemplateDirectoryFullPath);
            return CreateInstallResult(
                GCWebGLExportTemplateInstallStatus.Blocked,
                false,
                "Clean WebGL template installation is blocked.",
                createdPaths,
                reusedPaths,
                blockedReasons
            );
        }

        if (string.IsNullOrEmpty(destinationTemplateDirectoryFullPath))
        {
            blockedReasons.Add("Template destination folder path is empty.");
            return CreateInstallResult(
                GCWebGLExportTemplateInstallStatus.Blocked,
                false,
                "Clean WebGL template installation is blocked.",
                createdPaths,
                reusedPaths,
                blockedReasons
            );
        }

        ValidateTemplateSourceFiles(sourceTemplateDirectoryFullPath, blockedReasons);
        ValidateTemplateDestinationPaths(destinationTemplateDirectoryFullPath, blockedReasons);
        if (blockedReasons.Count > 0)
        {
            return CreateInstallResult(
                GCWebGLExportTemplateInstallStatus.Blocked,
                false,
                "Clean WebGL template installation is blocked.",
                createdPaths,
                reusedPaths,
                blockedReasons
            );
        }

        EnsureDirectory(destinationTemplateDirectoryFullPath, createdPaths, reusedPaths, blockedReasons);
        if (blockedReasons.Count == 0)
        {
            foreach (var fileName in ExpectedTemplateFiles)
            {
                EnsureTemplateFile(
                    Path.Combine(sourceTemplateDirectoryFullPath, fileName),
                    Path.Combine(destinationTemplateDirectoryFullPath, fileName),
                    createdPaths,
                    reusedPaths,
                    blockedReasons
                );
            }
        }

        var changed = createdPaths.Count > 0;
        if (changed && refreshAssetDatabase)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        return CreateInstallResult(
            blockedReasons.Count > 0
                ? GCWebGLExportTemplateInstallStatus.Blocked
                : GCWebGLExportTemplateInstallStatus.Ready,
            changed,
            blockedReasons.Count > 0
                ? "Clean WebGL template installation is blocked."
                : changed
                    ? "Installed missing clean WebGL template files."
                    : "Clean WebGL template files already exist; existing files were reused.",
            createdPaths,
            reusedPaths,
            blockedReasons
        );
    }

    internal static GCWebGLExportReadiness InspectReadiness()
    {
        return InspectReadiness(AssetPathToFullPath(ProjectTemplateAssetPath));
    }

    internal static GCWebGLExportReadiness InspectReadiness(string destinationTemplateDirectoryFullPath)
    {
        var details = new List<string>();
        var templateFolderPathIsEmpty = string.IsNullOrEmpty(destinationTemplateDirectoryFullPath);
        var templateFolderIsWrongKind = !templateFolderPathIsEmpty &&
                                        File.Exists(destinationTemplateDirectoryFullPath);
        var templateFolderReady = !templateFolderPathIsEmpty &&
                                  Directory.Exists(destinationTemplateDirectoryFullPath);
        if (templateFolderIsWrongKind)
        {
            details.Add("Project-local template path is a file, expected a folder: " + destinationTemplateDirectoryFullPath);
        }
        else if (!templateFolderReady)
        {
            details.Add("Project-local template folder is missing: " + ProjectTemplateAssetPath);
        }

        var templateFilesReady = templateFolderReady;
        if (templateFolderReady)
        {
            foreach (var fileName in ExpectedTemplateFiles)
            {
                var filePath = Path.Combine(destinationTemplateDirectoryFullPath, fileName);
                if (Directory.Exists(filePath))
                {
                    templateFilesReady = false;
                    details.Add("Project-local template path is a folder, expected a file: " + filePath);
                }
                else if (!File.Exists(filePath))
                {
                    templateFilesReady = false;
                    details.Add("Project-local template file is missing: " + ProjectTemplateAssetPath + "/" + fileName);
                }
            }
        }

        var templateSelected = string.Equals(
            PlayerSettings.WebGL.template,
            ProjectTemplateIdentifier,
            StringComparison.Ordinal
        );
        if (!templateSelected)
        {
            details.Add("Selected WebGL template is '" + PlayerSettings.WebGL.template + "', expected '" + ProjectTemplateIdentifier + "'.");
        }

        var releaseSettingsReady = AreReleaseDefaultsApplied(details);
        var splashSettingsReady = AreSplashSettingsApplied(details);
        var activeBuildTargetIsWebGL = EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL;
        if (!activeBuildTargetIsWebGL)
        {
            details.Add("Active build target is " + EditorUserBuildSettings.activeBuildTarget + "; switch to WebGL manually before building.");
        }

        var blockingReady = templateFolderReady &&
                            templateFilesReady &&
                            templateSelected &&
                            releaseSettingsReady &&
                            splashSettingsReady;
        if (!blockingReady)
        {
            return new GCWebGLExportReadiness(
                GCWebGLExportSetupStatus.Blocked,
                templateFolderReady,
                templateFilesReady,
                templateSelected,
                releaseSettingsReady,
                splashSettingsReady,
                activeBuildTargetIsWebGL,
                "Clean WebGL export setup is incomplete.",
                details.ToArray()
            );
        }

        return new GCWebGLExportReadiness(
            activeBuildTargetIsWebGL
                ? GCWebGLExportSetupStatus.Ready
                : GCWebGLExportSetupStatus.Warning,
            templateFolderReady,
            templateFilesReady,
            templateSelected,
            releaseSettingsReady,
            splashSettingsReady,
            activeBuildTargetIsWebGL,
            activeBuildTargetIsWebGL
                ? "Clean WebGL export setup is ready."
                : "Clean WebGL export setup is ready, but the active build target is not WebGL.",
            details.ToArray()
        );
    }

    internal static string LocatePackageTemplatePath()
    {
        var packageInfo = PackageInfo.FindForAssembly(typeof(GamingCouchWebGLExportSetup).Assembly);
        if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
        {
            var packageTemplatePath = Path.Combine(packageInfo.resolvedPath, PackageTemplateAssetPath);
            if (Directory.Exists(packageTemplatePath))
            {
                return packageTemplatePath;
            }
        }

        var scriptPath = LocateOwnScriptAssetPath();
        if (!string.IsNullOrEmpty(scriptPath))
        {
            var packageRootPath = Path.GetFullPath(Path.Combine(AssetPathToFullPath(scriptPath), ".."));
            var templatePath = Path.Combine(packageRootPath, "WebGLTemplates", TemplateName);
            if (Directory.Exists(templatePath))
            {
                return templatePath;
            }
        }

        var workingTreeTemplatePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), PackageTemplateAssetPath));
        return Directory.Exists(workingTreeTemplatePath) ? workingTreeTemplatePath : null;
    }

    private static bool SelectTemplate(List<string> details)
    {
        if (string.Equals(PlayerSettings.WebGL.template, ProjectTemplateIdentifier, StringComparison.Ordinal))
        {
            details.Add("WebGL template selection already uses " + ProjectTemplateIdentifier + ".");
            return false;
        }

        PlayerSettings.WebGL.template = ProjectTemplateIdentifier;
        details.Add("Selected WebGL template " + ProjectTemplateIdentifier + ".");
        return true;
    }

    private static bool ApplyCleanReleaseDefaults(List<string> details)
    {
        var changed = false;
        var namedBuildTarget = NamedBuildTarget.WebGL;

        changed |= SetIl2CppCodeGeneration(namedBuildTarget, Il2CppCodeGeneration.OptimizeSize, details);
        changed |= SetManagedStrippingLevel(namedBuildTarget, ManagedStrippingLevel.High, details);
        changed |= SetStripUnusedMeshComponents(true, details);
        changed |= SetWebGLDataCaching(true, details);
        changed |= SetWebGLCompression(WebGLCompressionFormat.Disabled, details);
        changed |= SetWebGLExceptionSupport(WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly, details);
        changed |= SetWebGLDebugSymbolMode(WebGLDebugSymbolMode.Off, details);
#if UNITY_2023_1_OR_NEWER
        changed |= SetWebGLWasm2023(true, details);
#endif
        changed |= SetDevelopmentBuild(false, details);
        changed |= SetWebGLCodeOptimization(UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO, details);
        changed |= SetSplashScreen(false, false, details);

        return changed;
    }

    private static bool AreReleaseDefaultsApplied(List<string> details)
    {
        var ready = true;
        var namedBuildTarget = NamedBuildTarget.WebGL;

        ready &= Expect(
            PlayerSettings.GetIl2CppCodeGeneration(namedBuildTarget) == Il2CppCodeGeneration.OptimizeSize,
            "IL2CPP code generation is not Optimize Size.",
            details
        );
        ready &= Expect(
            PlayerSettings.GetManagedStrippingLevel(namedBuildTarget) == ManagedStrippingLevel.High,
            "Managed stripping level is not High.",
            details
        );
        ready &= Expect(
            PlayerSettings.stripUnusedMeshComponents,
            "Unused mesh component stripping is not enabled.",
            details
        );
        ready &= Expect(
            PlayerSettings.WebGL.dataCaching,
            "WebGL data caching is not enabled.",
            details
        );
        ready &= Expect(
            PlayerSettings.WebGL.compressionFormat == WebGLCompressionFormat.Disabled,
            "WebGL compression is not disabled.",
            details
        );
        ready &= Expect(
            PlayerSettings.WebGL.exceptionSupport == WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly,
            "WebGL exception support is not ExplicitlyThrownExceptionsOnly.",
            details
        );
        ready &= Expect(
            PlayerSettings.WebGL.debugSymbolMode == WebGLDebugSymbolMode.Off,
            "WebGL debug symbols are not off.",
            details
        );
#if UNITY_2023_1_OR_NEWER
        ready &= Expect(
            PlayerSettings.WebGL.wasm2023,
            "WebAssembly 2023 features are not enabled.",
            details
        );
#endif
        ready &= Expect(
            !EditorUserBuildSettings.development,
            "Development build is enabled.",
            details
        );
        ready &= Expect(
            UnityEditor.WebGL.UserBuildSettings.codeOptimization == UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO,
            "WebGL code optimization is not Disk Size LTO.",
            details
        );

        return ready;
    }

    private static bool AreSplashSettingsApplied(List<string> details)
    {
        var ready = true;
        ready &= Expect(
            !PlayerSettings.SplashScreen.show,
            "Unity splash screen is not disabled.",
            details
        );
        ready &= Expect(
            !PlayerSettings.SplashScreen.showUnityLogo,
            "Unity splash logo is not disabled.",
            details
        );
        return ready;
    }

    private static void EnsureDirectory(
        string directoryPath,
        List<string> createdPaths,
        List<string> reusedPaths,
        List<string> blockedReasons
    )
    {
        var fullDirectoryPath = Path.GetFullPath(directoryPath);
        if (Directory.Exists(fullDirectoryPath))
        {
            reusedPaths.Add("Reused folder: " + fullDirectoryPath);
            return;
        }

        var missingDirectories = new Stack<string>();
        var currentPath = fullDirectoryPath;
        while (!string.IsNullOrEmpty(currentPath) && !Directory.Exists(currentPath))
        {
            if (File.Exists(currentPath))
            {
                blockedReasons.Add("Expected a folder, but a file already exists: " + currentPath);
                return;
            }

            missingDirectories.Push(currentPath);
            currentPath = Path.GetDirectoryName(currentPath);
        }

        while (missingDirectories.Count > 0)
        {
            var directoryToCreate = missingDirectories.Pop();
            Directory.CreateDirectory(directoryToCreate);
            createdPaths.Add("Created folder: " + directoryToCreate);
        }
    }

    private static void ValidateTemplateSourceFiles(
        string sourceTemplateDirectoryFullPath,
        List<string> blockedReasons
    )
    {
        foreach (var fileName in ExpectedTemplateFiles)
        {
            var sourcePath = Path.Combine(sourceTemplateDirectoryFullPath, fileName);
            if (Directory.Exists(sourcePath))
            {
                blockedReasons.Add("Expected template source file, but a folder exists: " + sourcePath);
            }
            else if (!File.Exists(sourcePath))
            {
                blockedReasons.Add("Expected template source file is missing: " + sourcePath);
            }
        }
    }

    private static void ValidateTemplateDestinationPaths(
        string destinationTemplateDirectoryFullPath,
        List<string> blockedReasons
    )
    {
        var fullDirectoryPath = Path.GetFullPath(destinationTemplateDirectoryFullPath);
        if (File.Exists(fullDirectoryPath))
        {
            blockedReasons.Add("Expected a folder, but a file already exists: " + fullDirectoryPath);
            return;
        }

        var currentPath = fullDirectoryPath;
        while (!string.IsNullOrEmpty(currentPath) && !Directory.Exists(currentPath))
        {
            if (File.Exists(currentPath))
            {
                blockedReasons.Add("Expected a folder, but a file already exists: " + currentPath);
                return;
            }

            currentPath = Path.GetDirectoryName(currentPath);
        }

        foreach (var fileName in ExpectedTemplateFiles)
        {
            var destinationPath = Path.Combine(fullDirectoryPath, fileName);
            if (Directory.Exists(destinationPath))
            {
                blockedReasons.Add("Expected a file, but a folder already exists: " + destinationPath);
            }
        }
    }

    private static void EnsureTemplateFile(
        string sourcePath,
        string destinationPath,
        List<string> createdPaths,
        List<string> reusedPaths,
        List<string> blockedReasons
    )
    {
        if (!File.Exists(sourcePath))
        {
            blockedReasons.Add("Expected template source file is missing: " + sourcePath);
            return;
        }

        if (Directory.Exists(destinationPath))
        {
            blockedReasons.Add("Expected a file, but a folder already exists: " + destinationPath);
            return;
        }

        if (File.Exists(destinationPath))
        {
            reusedPaths.Add("Reused file: " + destinationPath);
            return;
        }

        File.Copy(sourcePath, destinationPath, false);
        createdPaths.Add("Created file: " + destinationPath);
    }

    private static GCWebGLExportTemplateInstallResult CreateInstallResult(
        GCWebGLExportTemplateInstallStatus status,
        bool changed,
        string message,
        List<string> createdPaths,
        List<string> reusedPaths,
        List<string> blockedReasons
    )
    {
        return new GCWebGLExportTemplateInstallResult(
            status,
            changed,
            message,
            createdPaths.ToArray(),
            reusedPaths.ToArray(),
            blockedReasons.ToArray()
        );
    }

    private static bool SetIl2CppCodeGeneration(
        NamedBuildTarget namedBuildTarget,
        Il2CppCodeGeneration expected,
        List<string> details
    )
    {
        if (PlayerSettings.GetIl2CppCodeGeneration(namedBuildTarget) == expected)
        {
            return false;
        }

        PlayerSettings.SetIl2CppCodeGeneration(namedBuildTarget, expected);
        details.Add("Set IL2CPP code generation to " + expected + ".");
        return true;
    }

    private static bool SetManagedStrippingLevel(
        NamedBuildTarget namedBuildTarget,
        ManagedStrippingLevel expected,
        List<string> details
    )
    {
        if (PlayerSettings.GetManagedStrippingLevel(namedBuildTarget) == expected)
        {
            return false;
        }

        PlayerSettings.SetManagedStrippingLevel(namedBuildTarget, expected);
        details.Add("Set managed stripping level to " + expected + ".");
        return true;
    }

    private static bool SetStripUnusedMeshComponents(bool expected, List<string> details)
    {
        if (PlayerSettings.stripUnusedMeshComponents == expected)
        {
            return false;
        }

        PlayerSettings.stripUnusedMeshComponents = expected;
        details.Add("Set unused mesh component stripping to " + expected + ".");
        return true;
    }

    private static bool SetWebGLDataCaching(bool expected, List<string> details)
    {
        if (PlayerSettings.WebGL.dataCaching == expected)
        {
            return false;
        }

        PlayerSettings.WebGL.dataCaching = expected;
        details.Add("Set WebGL data caching to " + expected + ".");
        return true;
    }

    private static bool SetWebGLCompression(WebGLCompressionFormat expected, List<string> details)
    {
        if (PlayerSettings.WebGL.compressionFormat == expected)
        {
            return false;
        }

        PlayerSettings.WebGL.compressionFormat = expected;
        details.Add("Set WebGL compression to " + expected + ".");
        return true;
    }

    private static bool SetWebGLExceptionSupport(WebGLExceptionSupport expected, List<string> details)
    {
        if (PlayerSettings.WebGL.exceptionSupport == expected)
        {
            return false;
        }

        PlayerSettings.WebGL.exceptionSupport = expected;
        details.Add("Set WebGL exception support to " + expected + ".");
        return true;
    }

    private static bool SetWebGLDebugSymbolMode(WebGLDebugSymbolMode expected, List<string> details)
    {
        if (PlayerSettings.WebGL.debugSymbolMode == expected)
        {
            return false;
        }

        PlayerSettings.WebGL.debugSymbolMode = expected;
        details.Add("Set WebGL debug symbols to " + expected + ".");
        return true;
    }

#if UNITY_2023_1_OR_NEWER
    private static bool SetWebGLWasm2023(bool expected, List<string> details)
    {
        if (PlayerSettings.WebGL.wasm2023 == expected)
        {
            return false;
        }

        PlayerSettings.WebGL.wasm2023 = expected;
        details.Add("Set WebAssembly 2023 features to " + expected + ".");
        return true;
    }
#endif

    private static bool SetDevelopmentBuild(bool expected, List<string> details)
    {
        if (EditorUserBuildSettings.development == expected)
        {
            return false;
        }

        EditorUserBuildSettings.development = expected;
        details.Add("Set development build to " + expected + ".");
        return true;
    }

    private static bool SetWebGLCodeOptimization(
        UnityEditor.WebGL.WasmCodeOptimization expected,
        List<string> details
    )
    {
        if (UnityEditor.WebGL.UserBuildSettings.codeOptimization == expected)
        {
            return false;
        }

        UnityEditor.WebGL.UserBuildSettings.codeOptimization = expected;
        details.Add("Set WebGL code optimization to " + expected + ".");
        return true;
    }

    private static bool SetSplashScreen(bool showSplash, bool showUnityLogo, List<string> details)
    {
        var changed = false;
        if (PlayerSettings.SplashScreen.show != showSplash)
        {
            PlayerSettings.SplashScreen.show = showSplash;
            details.Add("Set Unity splash screen visibility to " + showSplash + ".");
            changed = true;
        }

        if (PlayerSettings.SplashScreen.showUnityLogo != showUnityLogo)
        {
            PlayerSettings.SplashScreen.showUnityLogo = showUnityLogo;
            details.Add("Set Unity splash logo visibility to " + showUnityLogo + ".");
            changed = true;
        }

        return changed;
    }

    private static bool Expect(bool condition, string detail, List<string> details)
    {
        if (condition)
        {
            return true;
        }

        details.Add(detail);
        return false;
    }

    private static string LocateOwnScriptAssetPath()
    {
        var scriptGuids = AssetDatabase.FindAssets("GamingCouchWebGLExportSetup t:Script");
        for (var index = 0; index < scriptGuids.Length; index++)
        {
            var path = AssetDatabase.GUIDToAssetPath(scriptGuids[index]);
            if (path.EndsWith("GamingCouchWebGLExportSetup.cs", StringComparison.Ordinal))
            {
                return path;
            }
        }

        return null;
    }

    private static string AssetPathToFullPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return null;
        }

        if (Path.IsPathRooted(assetPath))
        {
            return Path.GetFullPath(assetPath);
        }

        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }
}
