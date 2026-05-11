using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;

internal sealed class GCWebGLBuildSettingsProfileResult
{
    internal readonly bool changed;
    internal readonly string[] details;

    internal GCWebGLBuildSettingsProfileResult(bool changed, string[] details)
    {
        this.changed = changed;
        this.details = details ?? new string[0];
    }
}

internal static class GamingCouchWebGLBuildSettingsProfiles
{
    internal static GCWebGLBuildSettingsProfileResult ApplyReleaseProfile()
    {
        var details = new List<string>();
        var changed = ApplyReleaseProfile(details);
        return new GCWebGLBuildSettingsProfileResult(changed, details.ToArray());
    }

    internal static bool ApplyReleaseProfile(List<string> details)
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

        return changed;
    }

    internal static GCWebGLBuildSettingsProfileResult ApplyDevProfile()
    {
        var details = new List<string>();
        var changed = ApplyDevProfile(details);
        return new GCWebGLBuildSettingsProfileResult(changed, details.ToArray());
    }

    internal static bool ApplyDevProfile(List<string> details)
    {
        var changed = false;
        var namedBuildTarget = NamedBuildTarget.WebGL;

        changed |= SetIl2CppCodeGeneration(namedBuildTarget, Il2CppCodeGeneration.OptimizeSpeed, details);
        changed |= SetManagedStrippingLevel(namedBuildTarget, ManagedStrippingLevel.Disabled, details);
        changed |= SetStripUnusedMeshComponents(false, details);
        changed |= SetWebGLDataCaching(false, details);
        changed |= SetWebGLCompression(WebGLCompressionFormat.Disabled, details);
        changed |= SetWebGLExceptionSupport(WebGLExceptionSupport.FullWithStacktrace, details);
        changed |= SetWebGLDebugSymbolMode(WebGLDebugSymbolMode.Embedded, details);
#if UNITY_2023_1_OR_NEWER
        changed |= SetWebGLWasm2023(true, details);
#endif
        changed |= SetDevelopmentBuild(true, details);
        changed |= SetWebGLCodeOptimization(UnityEditor.WebGL.WasmCodeOptimization.BuildTimes, details);

        return changed;
    }

    internal static bool IsReleaseProfileApplied(List<string> details)
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

    private static bool Expect(bool condition, string detail, List<string> details)
    {
        if (condition)
        {
            return true;
        }

        details.Add(detail);
        return false;
    }
}
