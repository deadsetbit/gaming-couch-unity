using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if GC_HAS_UGUI
using UnityEngine.UI;
#endif

internal enum GCExampleSceneCreationStatus
{
    Created,
    Cancelled,
    Blocked,
}

internal sealed class GCExampleSceneCreationResult
{
    internal readonly GCExampleSceneCreationStatus status;
    internal readonly bool isPendingCompilation;
    internal readonly string scenePath;
    internal readonly string message;
    internal readonly string[] details;
    internal readonly string[] existingScenePaths;

    internal GCExampleSceneCreationResult(
        GCExampleSceneCreationStatus status,
        bool isPendingCompilation,
        string scenePath,
        string message,
        string[] details,
        string[] existingScenePaths
    )
    {
        this.status = status;
        this.isPendingCompilation = isPendingCompilation;
        this.scenePath = scenePath;
        this.message = message;
        this.details = details ?? new string[0];
        this.existingScenePaths = existingScenePaths ?? new string[0];
    }

    internal bool IsCreated { get { return status == GCExampleSceneCreationStatus.Created; } }
    internal bool IsCancelled { get { return status == GCExampleSceneCreationStatus.Cancelled; } }
    internal bool IsBlocked { get { return status == GCExampleSceneCreationStatus.Blocked; } }
    internal bool IsPendingCompilation { get { return isPendingCompilation; } }
}

// Creates a brand new example scene (unlike Active Scene Setup, which wires the scene the
// user already has open). It saves the current scene first, opens a fresh scene with the
// default camera + light, saves it under the GCExample folder, and then reuses Active Scene
// Setup to wire the GamingCouch object and generate/link the example Game listener and player
// prefab. Making the new scene the first Build Settings scene is intentionally left to the
// existing "Set up missing pieces" action so creating an example scene never silently changes
// which scene a build boots into.
internal static class GamingCouchExampleSceneCreation
{
    internal const string ExampleSceneBaseName = "GCExampleScene";
    internal const string ExampleSceneAssetPath =
        GamingCouchActiveSceneSetup.ExampleFolderAssetPath + "/" + ExampleSceneBaseName + ".unity";

    internal static GCExampleSceneCreationResult CreateExampleScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return Blocked(null, "Exit Play Mode before creating a new example scene.", null, null);
        }

        if (GamingCouchActiveSceneSetup.HasPendingSetup())
        {
            return Blocked(
                null,
                "Active Scene Setup is still finishing after generating example scripts. Wait for it to complete, then create the new example scene.",
                null,
                null
            );
        }

        var existingScenePaths = FindExistingExampleScenePaths();
        if (existingScenePaths.Length > 0 && !Application.isBatchMode)
        {
            var confirmed = EditorUtility.DisplayDialog(
                "Create New Example Scene",
                "This project already has " + existingScenePaths.Length +
                    " example scene(s) under " + GamingCouchActiveSceneSetup.ExampleFolderAssetPath +
                    ". A new example scene will be created alongside them.\n\n" +
                    DescribeExistingScenes(existingScenePaths),
                "Create New Scene",
                "Cancel"
            );
            if (!confirmed)
            {
                return Cancelled(existingScenePaths);
            }
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return Cancelled(existingScenePaths);
        }

        var blockedReasons = new List<string>();
        if (!EnsureFolder(GamingCouchActiveSceneSetup.ExampleFolderAssetPath, blockedReasons))
        {
            return Blocked(
                null,
                "Could not create the example folder for the new scene.",
                blockedReasons.ToArray(),
                existingScenePaths
            );
        }

        var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var scenePath = AssetDatabase.GenerateUniqueAssetPath(ExampleSceneAssetPath);
        if (!EditorSceneManager.SaveScene(newScene, scenePath))
        {
            return Blocked(scenePath, "Could not save the new example scene to " + scenePath + ".", null, existingScenePaths);
        }

        var setupResult = GamingCouchActiveSceneSetup.EnsureActiveSceneSetup(false);

        // Add an on-screen label so the otherwise-empty Game View (players only spawn at runtime)
        // points the user at the generated example scripts.
        CreateSceneInfoOverlay(newScene);

        // Persist the GamingCouch object (and any synchronously wired references) so they survive
        // the domain reload that first-run example-script generation triggers. The listener and
        // player-prefab references are wired by the post-compilation continuation and left for the
        // user to save, matching how Active Scene Setup already behaves.
        EditorSceneManager.SaveScene(newScene);

        var details = new List<string>();
        AddExistingSceneDetail(details, existingScenePaths);
        AppendRange(details, setupResult != null ? setupResult.details : null);

        if (setupResult != null && setupResult.IsBlocked)
        {
            return new GCExampleSceneCreationResult(
                GCExampleSceneCreationStatus.Blocked,
                false,
                scenePath,
                "Created " + scenePath + ", but Active Scene Setup is blocked.",
                details.ToArray(),
                existingScenePaths
            );
        }

        var pending = setupResult != null && setupResult.IsPendingCompilation;
        var message = pending
            ? "Created " + scenePath + ". Active Scene Setup will finish wiring the scene after Unity compiles the generated example scripts."
            : "Created " + scenePath + " and completed Active Scene Setup.";

        Debug.Log(
            BuildGeneratedFilesGuidance(message),
            AssetDatabase.LoadMainAssetAtPath(GamingCouchActiveSceneSetup.ExampleGameScriptAssetPath)
        );

        return new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Created,
            pending,
            scenePath,
            message,
            details.ToArray(),
            existingScenePaths
        );
    }

    internal static string[] FindExistingExampleScenePaths()
    {
        var folder = GamingCouchActiveSceneSetup.ExampleFolderAssetPath;
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return new string[0];
        }

        var guids = AssetDatabase.FindAssets("t:Scene", new[] { folder });
        var paths = new List<string>();
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".unity", StringComparison.Ordinal))
            {
                paths.Add(path);
            }
        }

        paths.Sort(StringComparer.Ordinal);
        return paths.ToArray();
    }

    private static bool EnsureFolder(string folderAssetPath, List<string> blockedReasons)
    {
        if (string.IsNullOrEmpty(folderAssetPath) || AssetDatabase.IsValidFolder(folderAssetPath))
        {
            return true;
        }

        var parent = Path.GetDirectoryName(folderAssetPath);
        parent = string.IsNullOrEmpty(parent) ? "Assets" : parent.Replace('\\', '/');
        var name = Path.GetFileName(folderAssetPath);

        if (!AssetDatabase.IsValidFolder(parent) && !EnsureFolder(parent, blockedReasons))
        {
            return false;
        }

        var guid = AssetDatabase.CreateFolder(parent, name);
        if (string.IsNullOrEmpty(guid))
        {
            blockedReasons.Add("Could not create folder " + folderAssetPath + ".");
            return false;
        }

        return true;
    }

    private static string DescribeExistingScenes(string[] existingScenePaths)
    {
        var text = "Existing:";
        for (var i = 0; i < existingScenePaths.Length; i++)
        {
            text += "\n- " + existingScenePaths[i];
        }

        return text;
    }

    private static void AddExistingSceneDetail(List<string> details, string[] existingScenePaths)
    {
        if (existingScenePaths == null || existingScenePaths.Length == 0)
        {
            return;
        }

        details.Add(
            "Kept " + existingScenePaths.Length + " existing example scene(s); created a new one alongside them."
        );
    }

    private static void AppendRange(List<string> target, string[] source)
    {
        if (source == null)
        {
            return;
        }

        for (var i = 0; i < source.Length; i++)
        {
            target.Add(source[i]);
        }
    }

    private static void CreateSceneInfoOverlay(Scene scene)
    {
#if GC_HAS_UGUI
        var root = new GameObject("GamingCouch Example Info", typeof(Canvas), typeof(CanvasScaler));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("Panel", typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.6f);
        panelImage.raycastTarget = false;
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -24f);
        panelRect.sizeDelta = new Vector2(1040f, 210f);

        var textObject = new GameObject("Text", typeof(Text));
        textObject.transform.SetParent(panel.transform, false);
        var text = textObject.GetComponent<Text>();
        text.font = GetBuiltinFont();
        text.text = BuildSceneInfoText();
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 28;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 16f);
        textRect.offsetMax = new Vector2(-24f, -16f);

        if (root.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(root, scene);
        }
#endif
    }

#if GC_HAS_UGUI
    private static Font GetBuiltinFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
#endif

    internal static string BuildSceneInfoText()
    {
        return
            "GamingCouch example scene\n" +
            "Example scripts: " + GamingCouchActiveSceneSetup.ExampleFolderAssetPath + "\n" +
            "GCGameExample.cs (game) and GCPlayerExample.cs (player)\n" +
            "Players spawn at runtime on the Gaming Couch platform.\n" +
            "(You can delete this label.)";
    }

    internal static string BuildGeneratedFilesGuidance(string headline)
    {
        return
            "GamingCouch: " + headline + "\n" +
            "Browse the generated example files and grow them into your game:\n" +
            "  - " + GamingCouchActiveSceneSetup.ExampleGameScriptAssetPath + "  (game listener)\n" +
            "  - " + GamingCouchActiveSceneSetup.ExamplePlayerScriptAssetPath + "  (player)\n" +
            "  - " + GamingCouchActiveSceneSetup.ExamplePlayerPrefabAssetPath + "  (player prefab)";
    }

    private static GCExampleSceneCreationResult Cancelled(string[] existingScenePaths)
    {
        return new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Cancelled,
            false,
            null,
            "Create New Example Scene was cancelled.",
            null,
            existingScenePaths
        );
    }

    private static GCExampleSceneCreationResult Blocked(
        string scenePath,
        string message,
        string[] details,
        string[] existingScenePaths
    )
    {
        return new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Blocked,
            false,
            scenePath,
            message,
            details,
            existingScenePaths
        );
    }
}
