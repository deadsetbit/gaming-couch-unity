using DSB.GC.Dev;
using UnityEditor;
using UnityEngine;

internal sealed class GamingCouchStartScreenWindow : EditorWindow
{
    internal const string WindowTitle = "GamingCouch Start Screen";

    private GCStartScreenReadiness readiness;
    private Vector2 scrollPosition;

    internal static GamingCouchStartScreenWindow Open()
    {
        var window = GetWindow<GamingCouchStartScreenWindow>(false, WindowTitle);
        window.Refresh();
        window.Show();
        return window;
    }

    private void OnEnable()
    {
        titleContent = new GUIContent(WindowTitle);
        minSize = new Vector2(420f, 360f);
        Refresh();
    }

    private void OnFocus()
    {
        Refresh();
    }

    private void OnHierarchyChange()
    {
        Refresh();
        Repaint();
    }

    private void OnProjectChange()
    {
        Refresh();
        Repaint();
    }

    private void OnGUI()
    {
        if (readiness == null)
        {
            Refresh();
        }

        DrawHeader();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawSceneSummary();
        DrawChecklist();
        DrawLocalPlayJsonDetails();
        DrawStubActions();
        EditorGUILayout.EndScrollView();
    }

    private void Refresh()
    {
        readiness = GCStartScreenReadinessService.InspectActiveScene();
    }

    private static void DrawHeader()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Quick-start readiness for the active scene.", EditorStyles.miniLabel);
        EditorGUILayout.Space();
    }

    private void DrawSceneSummary()
    {
        if (readiness == null)
        {
            EditorGUILayout.HelpBox("Readiness state is unavailable.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Active Scene", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Name", readiness.sceneName);
        if (!string.IsNullOrEmpty(readiness.scenePath))
        {
            EditorGUILayout.LabelField("Path", readiness.scenePath);
        }

        var messageType = readiness.IsSceneReady ? MessageType.Info : MessageType.Warning;
        var message = readiness.IsSceneReady
            ? "The active scene has the required GamingCouch object, listener, and player prefab references."
            : "The active scene is missing required GamingCouch setup.";

        if (readiness.gamingCouches.Length > 1)
        {
            messageType = MessageType.Error;
            message = "The active scene has multiple GamingCouch components. Resolve this manually before setup actions are enabled.";
        }

        EditorGUILayout.HelpBox(message, messageType);
        EditorGUILayout.Space();
    }

    private void DrawChecklist()
    {
        if (readiness == null)
        {
            return;
        }

        EditorGUILayout.LabelField("Checklist", EditorStyles.boldLabel);
        for (var index = 0; index < readiness.checklist.Length; index++)
        {
            DrawChecklistItem(readiness.checklist[index]);
        }

        EditorGUILayout.Space();
    }

    private static void DrawChecklistItem(GCStartScreenReadinessCheck check)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(GetChecklistMarker(check.state), GUILayout.Width(28f));
            GUILayout.Label(check.label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(GetStateLabel(check.state), EditorStyles.miniLabel, GUILayout.Width(64f));
        }

        if (check.state != GCStartScreenReadinessCheckState.Pass && !string.IsNullOrEmpty(check.message))
        {
            EditorGUILayout.HelpBox(check.message, GetMessageType(check.state));
        }
    }

    private void DrawLocalPlayJsonDetails()
    {
        if (readiness == null || readiness.localPlayJson == null)
        {
            return;
        }

        EditorGUILayout.LabelField("Local Play JSON", EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(readiness.localPlayJson.path))
        {
            EditorGUILayout.LabelField("File", readiness.localPlayJson.path);
        }

        var issues = readiness.localPlayJson.Issues;
        if (issues.Length == 0)
        {
            var message = readiness.localPlayJson.isValid
                ? "No gc.dev.json issues detected."
                : string.IsNullOrEmpty(readiness.localPlayJson.message)
                    ? "gc.dev.json is missing or invalid for local Play Mode."
                    : readiness.localPlayJson.message;
            var messageType = readiness.localPlayJson.isValid ? MessageType.Info : MessageType.Error;
            EditorGUILayout.HelpBox(message, messageType);
            EditorGUILayout.Space();
            return;
        }

        for (var index = 0; index < issues.Length; index++)
        {
            var issue = issues[index];
            if (issue == null)
            {
                continue;
            }

            var messageType = issue.severity == GCDevJsonIssueSeverity.Error ? MessageType.Error : MessageType.Warning;
            EditorGUILayout.HelpBox(GCDevJsonIssueFormatter.Format(issue), messageType);
        }

        EditorGUILayout.Space();
    }

    private static void DrawStubActions()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            GUILayout.Button("Set Up Quick Start");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Button("Create GamingCouch");
                GUILayout.Button("Create Listener");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Button("Create Player Prefab");
                GUILayout.Button("Create Quick Start Scene");
            }
        }

        EditorGUILayout.HelpBox("Setup actions are placeholders for later quick-start tasks.", MessageType.Info);
    }

    private static string GetChecklistMarker(GCStartScreenReadinessCheckState state)
    {
        switch (state)
        {
            case GCStartScreenReadinessCheckState.Pass:
                return "[x]";
            case GCStartScreenReadinessCheckState.Warning:
                return "[!]";
            case GCStartScreenReadinessCheckState.Blocked:
                return "[-]";
            default:
                return "[ ]";
        }
    }

    private static string GetStateLabel(GCStartScreenReadinessCheckState state)
    {
        switch (state)
        {
            case GCStartScreenReadinessCheckState.Pass:
                return "Ready";
            case GCStartScreenReadinessCheckState.Warning:
                return "Warning";
            case GCStartScreenReadinessCheckState.Blocked:
                return "Blocked";
            default:
                return "Missing";
        }
    }

    private static MessageType GetMessageType(GCStartScreenReadinessCheckState state)
    {
        switch (state)
        {
            case GCStartScreenReadinessCheckState.Warning:
                return MessageType.Warning;
            case GCStartScreenReadinessCheckState.Blocked:
                return MessageType.Warning;
            case GCStartScreenReadinessCheckState.Fail:
                return MessageType.Error;
            default:
                return MessageType.Info;
        }
    }
}
