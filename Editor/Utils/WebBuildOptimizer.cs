using UnityEditor;
using UnityEditor.Build;

public class WebBuildOptimizer
{
    [MenuItem("GamingCouch/WebGL Build/Apply release build settings (slow build)")]
    public static void OptimizeBuild()
    {
        var namedBuildTarget = NamedBuildTarget.WebGL;

        // Set IL2CPP code generation to Optimize Size 
        PlayerSettings.SetIl2CppCodeGeneration(namedBuildTarget,
                                        Il2CppCodeGeneration.OptimizeSize);

        // Set the Managed Stripping Level to High
        PlayerSettings.SetManagedStrippingLevel(namedBuildTarget,
                                            ManagedStrippingLevel.High);

        // Strip unused mesh components           
        PlayerSettings.stripUnusedMeshComponents = true;

        // Enable data caching
        PlayerSettings.WebGL.dataCaching = true;

        // Set the compression format to Brotli
        // PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;

        // Deactivate exceptions
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;

        // Deactivate debug symbols
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;

#if UNITY_2023_1_OR_NEWER
        //Enable WebAssembly 2023 features
        PlayerSettings.WebGL.wasm2023 = true;
#endif

        // Enable development build
        EditorUserBuildSettings.development = false;

        // Set Platform Settings to optimize for disk size (LTO)
        UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO;
    }


    [MenuItem("GamingCouch/WebGL Build/Apply dev build settings (fast build)")]
    public static void SpeedupBuild()
    {
        var namedBuildTarget = NamedBuildTarget.WebGL;

        // Set IL2CPP code generation to Optimize Size 
        PlayerSettings.SetIl2CppCodeGeneration(namedBuildTarget,
                                        Il2CppCodeGeneration.OptimizeSpeed);

        // Set the Managed Stripping Level to High
        PlayerSettings.SetManagedStrippingLevel(namedBuildTarget,
                                            ManagedStrippingLevel.Disabled);

        // Strip unused mesh components           
        PlayerSettings.stripUnusedMeshComponents = false;

        // Enable data caching
        PlayerSettings.WebGL.dataCaching = false;

        // Set the compression format to Brotli
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

        // Deactivate exceptions
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;

        // Deactivate debug symbols
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Embedded;

#if UNITY_2023_1_OR_NEWER
        //Enable WebAssembly 2023 features
        PlayerSettings.WebGL.wasm2023 = true;
#endif

        // Enable development build
        EditorUserBuildSettings.development = true;

        // Set Platform Settings to optimize for disk size (LTO)
        UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.BuildTimes;
    }

}
