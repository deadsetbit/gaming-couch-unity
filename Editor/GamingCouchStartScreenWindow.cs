using System.Collections.Generic;
using DSB.GC.Dev;
using UnityEditor;
using UnityEngine;

internal sealed class GamingCouchStartScreenWindow : EditorWindow
{
    internal const string WindowTitle = "GamingCouch Start Screen";

    private GCStartScreenReadiness readiness;
    private Vector2 scrollPosition;
    private string actionMessage;
    private string[] actionDetails = new string[0];
    private MessageType actionMessageType = MessageType.Info;

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
        DrawAutoOpenSettings();
        DrawSceneSummary();
        DrawChecklist();
        DrawLocalPlayJsonDetails();
        DrawPendingSetupStatus();
        DrawActions();
        DrawActionResult();
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

    private static void DrawAutoOpenSettings()
    {
        EditorGUILayout.LabelField("Startup", EditorStyles.boldLabel);
        var suppressed = GCStartScreenSettings.SuppressAutoOpen;
        var nextSuppressed = EditorGUILayout.ToggleLeft("Suppress automatic opening for this project", suppressed);
        if (nextSuppressed != suppressed)
        {
            GCStartScreenSettings.SuppressAutoOpen = nextSuppressed;
        }

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
            ? "Scene setup is ready. The active scene has one GamingCouch object with listener and player prefab references. Local Play JSON readiness is shown separately below."
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

    private void DrawChecklistItem(GCStartScreenReadinessCheck check)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(GetChecklistMarker(check.state), GUILayout.Width(28f));
            GUILayout.Label(check.label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(GetStateLabel(check.state), EditorStyles.miniLabel, GUILayout.Width(64f));

            var buttonLabel = GetChecklistActionLabel(check.id);
            if (!string.IsNullOrEmpty(buttonLabel))
            {
                using (new EditorGUI.DisabledScope(IsActiveSceneSetupActionBlocked()))
                {
                    if (GUILayout.Button(buttonLabel, GUILayout.Width(148f)))
                    {
                        RunChecklistAction(check.id);
                    }
                }
            }
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

        EditorGUILayout.LabelField("Play Mode Readiness", EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(readiness.localPlayJson.path))
        {
            EditorGUILayout.LabelField("File", readiness.localPlayJson.path);
        }

        var issues = readiness.localPlayJson.Issues;
        if (issues.Length == 0)
        {
            var message = readiness.localPlayJson.isValid
                ? "gc.dev.json is valid for local Play Mode."
                : string.IsNullOrEmpty(readiness.localPlayJson.message)
                    ? "Local Play Mode is blocked because gc.dev.json is missing or invalid. The Unity package will not create or repair this file."
                    : readiness.localPlayJson.message + " The Unity package will not create or repair this file.";
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
            var message = GCDevJsonIssueFormatter.Format(issue);
            if (issue.severity == GCDevJsonIssueSeverity.Error)
            {
                message += " Local Play Mode remains blocked until DevApp provides valid local play JSON; this screen will not create or repair it.";
            }

            EditorGUILayout.HelpBox(message, messageType);
        }

        EditorGUILayout.Space();
    }

    private static void DrawPendingSetupStatus()
    {
        if (!GamingCouchQuickStartSetup.HasPendingSetup())
        {
            return;
        }

        EditorGUILayout.HelpBox(
            "Quick-start setup is waiting for Unity to compile generated scripts. Setup will continue automatically after compilation finishes.",
            MessageType.Warning
        );
        EditorGUILayout.Space();
    }

    private void DrawActions()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(IsActiveSceneSetupActionBlocked()))
        {
            if (GUILayout.Button("Set up missing pieces"))
            {
                RunActiveSceneSetup();
            }
        }

        using (new EditorGUI.DisabledScope(GamingCouchQuickStartSetup.HasPendingSetup()))
        {
            if (GUILayout.Button("Create new quick-start scene"))
            {
                RunCreateOrOpenQuickStartScene();
            }
        }

        EditorGUILayout.Space();
    }

    private bool IsActiveSceneSetupActionBlocked()
    {
        if (GamingCouchQuickStartSetup.HasPendingSetup())
        {
            return true;
        }

        if (readiness == null)
        {
            return true;
        }

        return !readiness.GetCheck(GCStartScreenReadinessCheckId.ActiveScene).IsSatisfied ||
               readiness.gamingCouches.Length > 1;
    }

    private void DrawActionResult()
    {
        if (string.IsNullOrEmpty(actionMessage))
        {
            return;
        }

        EditorGUILayout.HelpBox(FormatActionMessage(actionMessage, actionDetails), actionMessageType);
        EditorGUILayout.Space();
    }

    private void RunChecklistAction(GCStartScreenReadinessCheckId id)
    {
        switch (id)
        {
            case GCStartScreenReadinessCheckId.GamingCouchInstance:
                RunEnsureGamingCouch();
                break;
            case GCStartScreenReadinessCheckId.ListenerAssigned:
                RunEnsureGameListener();
                break;
            case GCStartScreenReadinessCheckId.PlayerPrefabAssigned:
                RunEnsurePlayerPrefab();
                break;
            default:
                SetActionResult("No setup action is available for this checklist item.", MessageType.Info, null);
                break;
        }
    }

    private void RunEnsureGamingCouch()
    {
        var result = GamingCouchSceneWiring.EnsureActiveSceneGamingCouch();
        if (result.gamingCouch != null)
        {
            Selection.activeObject = result.gamingCouch.gameObject;
        }

        SetActionResult(
            result.message,
            result.IsBlocked ? MessageType.Error : MessageType.Info,
            result.changed ? null : new[] { "No scene changes were needed; the existing GamingCouch object was reused." }
        );
        Refresh();
        Repaint();
    }

    private void RunEnsurePlayerPrefab()
    {
        var result = GamingCouchQuickStartSetup.EnsureActiveSceneQuickStartPlayerPrefabReference();
        SetActionResult(result.message, GetActiveSceneResultMessageType(result), result.details);
        Refresh();
        Repaint();
    }

    private void RunEnsureGameListener()
    {
        var result = GamingCouchQuickStartSetup.EnsureActiveSceneQuickStartGameListenerReference();
        SetActionResult(result.message, GetActiveSceneResultMessageType(result), result.details);
        Refresh();
        Repaint();
    }

    private void RunActiveSceneSetup()
    {
        var result = GamingCouchQuickStartSetup.EnsureActiveSceneQuickStartSetup();
        SetActionResult(result.message, GetActiveSceneResultMessageType(result), result.details);
        Refresh();
        Repaint();
    }

    private void RunCreateOrOpenQuickStartScene()
    {
        var result = GamingCouchQuickStartSetup.CreateOrOpenQuickStartScene();
        var message = result.message;
        var details = new List<string>();

        if (!string.IsNullOrEmpty(result.sceneAssetPath))
        {
            details.Add("Scene: " + result.sceneAssetPath);
        }

        for (var index = 0; index < result.blockedReasons.Length; index++)
        {
            if (!string.IsNullOrEmpty(result.blockedReasons[index]))
            {
                details.Add(result.blockedReasons[index]);
            }
        }

        if (result.status == GCQuickStartSceneSetupStatus.Ready && !result.changed)
        {
            details.Add("No scene changes were needed; existing quick-start assets and references were reused.");
        }

        SetActionResult(message, GetSceneResultMessageType(result), details.ToArray());
        Refresh();
        Repaint();
    }

    private void SetActionResult(string message, MessageType messageType, string[] details)
    {
        actionMessage = message;
        actionMessageType = messageType;
        actionDetails = details ?? new string[0];
    }

    private static MessageType GetActiveSceneResultMessageType(GCQuickStartActiveSceneSetupResult result)
    {
        if (result.IsBlocked)
        {
            return MessageType.Error;
        }

        return result.IsPendingCompilation ? MessageType.Warning : MessageType.Info;
    }

    private static MessageType GetSceneResultMessageType(GCQuickStartSceneSetupResult result)
    {
        if (result.IsBlocked)
        {
            return MessageType.Error;
        }

        return result.IsPendingCompilation ? MessageType.Warning : MessageType.Info;
    }

    private static string FormatActionMessage(string message, string[] details)
    {
        if (details == null || details.Length == 0)
        {
            return message;
        }

        var formatted = message;
        for (var index = 0; index < details.Length; index++)
        {
            if (!string.IsNullOrEmpty(details[index]))
            {
                formatted += "\n- " + details[index];
            }
        }

        return formatted;
    }

    private static string GetChecklistActionLabel(GCStartScreenReadinessCheckId id)
    {
        switch (id)
        {
            case GCStartScreenReadinessCheckId.GamingCouchInstance:
                return "Create or Reuse";
            case GCStartScreenReadinessCheckId.ListenerAssigned:
                return "Wire Listener";
            case GCStartScreenReadinessCheckId.PlayerPrefabAssigned:
                return "Wire Player Prefab";
            default:
                return null;
        }
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
