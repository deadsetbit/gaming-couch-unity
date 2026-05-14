using DSB.GC.Dev;
using UnityEditor;
using UnityEngine;

internal sealed class GamingCouchStartScreenWindow : EditorWindow
{
    internal const string WindowTitle = "GamingCouch Start Screen";
    private const float ChecklistRowHeight = 28f;
    private const float ChecklistRowPaddingX = 8f;
    private const float ChecklistStatusWidth = 28f;
    private const float ChecklistMinimumButtonWidth = 96f;
    private const float ChecklistButtonWidth = 148f;
    private const float ChecklistHelpButtonWidth = 22f;
    private const float ChecklistColumnSpacing = 6f;
    private const float ChecklistMessageExtraIndent = 16f;
    private const float ChecklistMessageIndent = ChecklistRowPaddingX + ChecklistStatusWidth + ChecklistColumnSpacing + ChecklistMessageExtraIndent;
    private const float ChecklistStatusIndicatorSize = 10f;

    private GCStartScreenReadiness readiness;
    private Vector2 scrollPosition;
    private string actionMessage;
    private string[] actionDetails = new string[0];
    private MessageType actionMessageType = MessageType.Info;
    private bool hasSelectedChecklistHelp;
    private GCStartScreenReadinessCheckId selectedChecklistHelpId;

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

        using (new EditorGUILayout.VerticalScope())
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            DrawActiveSceneIssue();
            DrawSceneSummary();
            DrawChecklist();
            DrawLocalPlayJsonDetails();
            DrawPendingSetupStatus();
            DrawActions();
            DrawActionResult();
            EditorGUILayout.EndScrollView();

            DrawAutoOpenSettings();
        }
    }

    private void Refresh()
    {
        readiness = GCStartScreenReadinessService.InspectActiveScene();
    }

    private void DrawActiveSceneIssue()
    {
        if (readiness == null)
        {
            return;
        }

        var check = FindReadinessCheck(GCStartScreenReadinessCheckId.ActiveScene);
        if (check == null || check.IsSatisfied)
        {
            return;
        }

        var message = string.IsNullOrEmpty(check.message)
            ? "No loaded active scene is available for GamingCouch setup inspection."
            : check.message;
        EditorGUILayout.HelpBox(message, GetMessageType(check.state));
        EditorGUILayout.Space();
    }

    private static void DrawAutoOpenSettings()
    {
        EditorGUILayout.Space();
        var suppressed = GCStartScreenSettings.SuppressAutoOpen;
        var nextSuppressed = EditorGUILayout.ToggleLeft("Never open this again on startup", suppressed);
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
            DrawChecklistItem(readiness.checklist[index], index);
        }

        EditorGUILayout.Space();
    }

    private void DrawChecklistItem(GCStartScreenReadinessCheck check, int index)
    {
        if (check == null)
        {
            return;
        }

        var rowRect = EditorGUILayout.GetControlRect(false, ChecklistRowHeight);
        DrawChecklistRowBackground(rowRect, index);

        var contentRect = new Rect(
            rowRect.x + ChecklistRowPaddingX,
            rowRect.y + 4f,
            Mathf.Max(0f, rowRect.width - ChecklistRowPaddingX * 2f),
            EditorGUIUtility.singleLineHeight
        );

        var buttonLabel = check.HasAction ? check.action.label : null;
        var helpContent = GetChecklistHelpContent(check);
        var statusRect = new Rect(
            contentRect.x,
            contentRect.y,
            Mathf.Min(ChecklistStatusWidth, contentRect.width),
            contentRect.height
        );

        var contentLeft = statusRect.width > 0f
            ? statusRect.xMax + ChecklistColumnSpacing
            : contentRect.x;
        var contentRight = contentRect.xMax;
        var helpRect = Rect.zero;
        if (helpContent != null)
        {
            var availableHelpWidth = contentRight - contentLeft;
            if (availableHelpWidth >= ChecklistHelpButtonWidth)
            {
                helpRect = new Rect(
                    contentRight - ChecklistHelpButtonWidth,
                    contentRect.y,
                    ChecklistHelpButtonWidth,
                    contentRect.height
                );
                contentRight = helpRect.x - ChecklistColumnSpacing;
            }
        }

        var buttonRect = Rect.zero;
        if (!string.IsNullOrEmpty(buttonLabel))
        {
            var availableButtonWidth = contentRight - contentLeft;
            if (availableButtonWidth >= ChecklistMinimumButtonWidth)
            {
                var buttonWidth = Mathf.Min(ChecklistButtonWidth, availableButtonWidth);
                buttonRect = new Rect(
                    contentRight - buttonWidth,
                    contentRect.y,
                    buttonWidth,
                    contentRect.height
                );
                contentRight = buttonRect.x - ChecklistColumnSpacing;
            }
        }

        DrawChecklistStatusIndicator(statusRect, check.state);

        var labelRect = new Rect(
            contentLeft,
            contentRect.y,
            Mathf.Max(0f, contentRight - contentLeft),
            contentRect.height
        );
        if (HasVisibleRect(labelRect))
        {
            GUI.Label(labelRect, check.label, GetChecklistLabelStyle());
        }

        if (HasVisibleRect(buttonRect))
        {
            using (new EditorGUI.DisabledScope(IsChecklistActionDisabled(check)))
            {
                if (GUI.Button(buttonRect, buttonLabel))
                {
                    RunChecklistAction(check);
                }
            }
        }

        if (HasVisibleRect(helpRect))
        {
            if (GUI.Button(helpRect, helpContent, EditorStyles.iconButton))
            {
                ToggleChecklistHelp(check);
            }
        }

        if (check.state != GCStartScreenReadinessCheckState.Pass && !string.IsNullOrEmpty(check.message))
        {
            DrawChecklistMessage(check.message, GetMessageType(check.state));
        }

        if (IsChecklistHelpSelected(check))
        {
            DrawChecklistMessage(check.helpText, MessageType.Info);
        }
    }

    private static void DrawChecklistMessage(string message, MessageType messageType)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(ChecklistMessageIndent);
            EditorGUILayout.HelpBox(message, messageType);
        }
    }

    private void DrawLocalPlayJsonDetails()
    {
        if (readiness == null)
        {
            return;
        }

        var localPlayJson = readiness.localPlayJson;
        if (!ShouldShowLocalPlayJsonDetails(localPlayJson))
        {
            return;
        }

        EditorGUILayout.LabelField("Play Mode Readiness", EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(localPlayJson.path))
        {
            EditorGUILayout.LabelField("File", localPlayJson.path);
        }

        var issues = localPlayJson.Issues;
        if (!HasDisplayableIssues(issues))
        {
            if (localPlayJson.isValid)
            {
                var warningCount = GetWarningCount(localPlayJson);
                var warningMessage = "gc.dev.json is valid with " + warningCount + " warning" + (warningCount == 1 ? string.Empty : "s") + ".";
                EditorGUILayout.HelpBox(warningMessage, MessageType.Warning);
                EditorGUILayout.Space();
                return;
            }

            EditorGUILayout.HelpBox(GetLocalPlayJsonBlockedMessage(localPlayJson), MessageType.Error);
            EditorGUILayout.Space();
            return;
        }

        var displayedError = false;
        for (var index = 0; index < issues.Length; index++)
        {
            var issue = issues[index];
            if (issue == null)
            {
                continue;
            }

            var messageType = issue.severity == GCDevJsonIssueSeverity.Error ? MessageType.Error : MessageType.Warning;
            var message = GCDevJsonIssueFormatter.Format(issue);
            if (string.IsNullOrEmpty(message))
            {
                message = issue.severity == GCDevJsonIssueSeverity.Error
                    ? "gc.dev.json has a validation error."
                    : "gc.dev.json has a validation warning.";
            }

            if (issue.severity == GCDevJsonIssueSeverity.Error)
            {
                displayedError = true;
                message += " Local Play Mode remains blocked until DevApp provides valid local play JSON; this screen will not create or repair it.";
            }

            EditorGUILayout.HelpBox(message, messageType);
        }

        if (!localPlayJson.isValid && !displayedError)
        {
            EditorGUILayout.HelpBox(GetLocalPlayJsonBlockedMessage(localPlayJson), MessageType.Error);
        }

        EditorGUILayout.Space();
    }

    private static bool ShouldShowLocalPlayJsonDetails(GCStartScreenLocalPlayJsonReadiness localPlayJson)
    {
        if (localPlayJson == null)
        {
            return false;
        }

        if (!localPlayJson.isValid)
        {
            return true;
        }

        return HasDisplayableIssues(localPlayJson.Issues) || GetWarningCount(localPlayJson) > 0;
    }

    private static bool HasDisplayableIssues(GCDevJsonIssue[] issues)
    {
        if (issues == null)
        {
            return false;
        }

        for (var index = 0; index < issues.Length; index++)
        {
            if (issues[index] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetWarningCount(GCStartScreenLocalPlayJsonReadiness localPlayJson)
    {
        return localPlayJson != null && localPlayJson.validation != null ? localPlayJson.validation.WarningCount : 0;
    }

    private static string GetLocalPlayJsonBlockedMessage(GCStartScreenLocalPlayJsonReadiness localPlayJson)
    {
        return localPlayJson == null || string.IsNullOrEmpty(localPlayJson.message)
            ? "Local Play Mode is blocked because gc.dev.json is missing or invalid. The Unity package will not create or repair this file."
            : localPlayJson.message + " The Unity package will not create or repair this file.";
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
        if (!ShouldShowActiveSceneSetupAction())
        {
            return;
        }

        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(IsActiveSceneSetupActionBlocked()))
        {
            if (GUILayout.Button("Set up missing pieces"))
            {
                RunActiveSceneSetup();
            }
        }

        EditorGUILayout.Space();
    }

    private bool ShouldShowActiveSceneSetupAction()
    {
        if (readiness == null)
        {
            return false;
        }

        if (!IsReadinessCheckSatisfied(GCStartScreenReadinessCheckId.ActiveScene))
        {
            return false;
        }

        return readiness.HasSafeAutomatableSetupActions;
    }

    private bool IsReadinessCheckSatisfied(GCStartScreenReadinessCheckId id)
    {
        var check = FindReadinessCheck(id);
        return check != null && check.IsSatisfied;
    }

    private GCStartScreenReadinessCheck FindReadinessCheck(GCStartScreenReadinessCheckId id)
    {
        if (readiness == null)
        {
            return null;
        }

        GCStartScreenReadinessCheck check;
        return readiness.TryGetCheck(id, out check) ? check : null;
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

        return !IsReadinessCheckSatisfied(GCStartScreenReadinessCheckId.ActiveScene) ||
               !readiness.HasSafeAutomatableSetupActions;
    }

    private void DrawActionResult()
    {
        if (!ShouldShowActionResult(actionMessageType))
        {
            ClearActionResult();
            return;
        }

        if (string.IsNullOrEmpty(actionMessage))
        {
            return;
        }

        EditorGUILayout.HelpBox(FormatActionMessage(actionMessage, actionDetails), actionMessageType);
        EditorGUILayout.Space();
    }

    private void RunChecklistAction(GCStartScreenReadinessCheck check)
    {
        if (check == null || !check.HasAction)
        {
            SetActionResult("No checklist item is available for this action.", MessageType.Warning, null);
            return;
        }

        var action = check.action;
        if (action.isFocusAction)
        {
            FocusChecklistTarget(action);
            return;
        }

        switch (action.id)
        {
            case GCStartScreenReadinessActionId.CreateGamingCouch:
                RunEnsureGamingCouch();
                break;
            case GCStartScreenReadinessActionId.CreateAndWireGameScript:
                RunEnsureGameListener();
                break;
            case GCStartScreenReadinessActionId.WirePlayerPrefab:
                RunEnsurePlayerPrefab();
                break;
            case GCStartScreenReadinessActionId.SetFirstBuildSettingsScene:
                RunEnsureActiveSceneFirstBuildSettingsScene();
                break;
            case GCStartScreenReadinessActionId.Select16By9GameView:
                RunEnsureGameViewAspect16By9();
                break;
            case GCStartScreenReadinessActionId.SetUpWebGLExport:
                RunEnsureWebGLExportSetup();
                break;
            default:
                SetActionResult("No setup action is available for this checklist item.", MessageType.Info, null);
                break;
        }
    }

    private bool IsChecklistActionDisabled(GCStartScreenReadinessCheck check)
    {
        if (check == null || !check.HasAction)
        {
            return true;
        }

        if (check.HasFocusAction)
        {
            return check.action.target == null;
        }

        return IsChecklistSetupActionBlocked(check);
    }

    private bool IsChecklistSetupActionBlocked(GCStartScreenReadinessCheck check)
    {
        if (check == null || !check.HasSetupAction)
        {
            return true;
        }

        if (GamingCouchQuickStartSetup.HasPendingSetup())
        {
            return true;
        }

        if (readiness == null)
        {
            return true;
        }

        return check.action.requiresLoadedActiveScene &&
               !IsReadinessCheckSatisfied(GCStartScreenReadinessCheckId.ActiveScene);
    }

    private void FocusChecklistTarget(GCStartScreenReadinessAction action)
    {
        var target = action != null ? action.target : null;
        if (target == null)
        {
            SetActionResult("No checklist target is available to select.", MessageType.Warning, null);
            return;
        }

        Selection.activeObject = target;
        EditorGUIUtility.PingObject(target);
        SetActionResult("Focused " + target.name + ".", MessageType.Info, null);
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

    private void RunEnsureActiveSceneFirstBuildSettingsScene()
    {
        var result = GamingCouchBuildSettingsReadiness.EnsureActiveSceneFirstEnabled();
        SetActionResult(result.message, GetBuildSettingsResultMessageType(result), result.details);
        Refresh();
        Repaint();
    }

    private void RunEnsureGameViewAspect16By9()
    {
        var result = GamingCouchGameViewAspect.SelectExisting16By9Size();
        SetActionResult(result.message, GetGameViewAspectResultMessageType(result), result.details);
        Refresh();
        Repaint();
    }

    private void RunEnsureWebGLExportSetup()
    {
        var result = GamingCouchWebGLExportSetup.EnsureCleanWebGLExportSetup();
        SetActionResult(result.message, GetWebGLExportSetupResultMessageType(result), result.details);
        Refresh();
        Repaint();
    }

    private void SetActionResult(string message, MessageType messageType, string[] details)
    {
        if (!ShouldShowActionResult(messageType))
        {
            ClearActionResult();
            return;
        }

        actionMessage = message;
        actionMessageType = messageType;
        actionDetails = details ?? new string[0];
    }

    private void ClearActionResult()
    {
        actionMessage = null;
        actionMessageType = MessageType.Info;
        actionDetails = new string[0];
    }

    private static bool ShouldShowActionResult(MessageType messageType)
    {
        return messageType == MessageType.Warning || messageType == MessageType.Error;
    }

    private static MessageType GetActiveSceneResultMessageType(GCQuickStartActiveSceneSetupResult result)
    {
        if (result.IsBlocked)
        {
            return MessageType.Error;
        }

        return result.IsPendingCompilation ? MessageType.Warning : MessageType.Info;
    }

    private static MessageType GetBuildSettingsResultMessageType(GCActiveSceneBuildSettingsSetupResult result)
    {
        return result.IsBlocked ? MessageType.Error : MessageType.Info;
    }

    private static MessageType GetGameViewAspectResultMessageType(GCGameViewAspectSetupResult result)
    {
        return result.IsBlocked ? MessageType.Warning : MessageType.Info;
    }

    private static MessageType GetWebGLExportSetupResultMessageType(GCWebGLExportSetupResult result)
    {
        if (result == null)
        {
            return MessageType.Error;
        }

        if (result.IsBlocked)
        {
            return MessageType.Error;
        }

        return result.HasWarning ? MessageType.Warning : MessageType.Info;
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

    private static GUIContent GetChecklistHelpContent(GCStartScreenReadinessCheck check)
    {
        if (check == null || string.IsNullOrEmpty(check.helpText))
        {
            return null;
        }

        var content = EditorGUIUtility.IconContent("_Help");
        return new GUIContent(content.image, check.helpText);
    }

    private void ToggleChecklistHelp(GCStartScreenReadinessCheck check)
    {
        if (check == null || string.IsNullOrEmpty(check.helpText))
        {
            hasSelectedChecklistHelp = false;
            return;
        }

        var checkId = GCStartScreenReadiness.NormalizeCheckId(check.id);
        if (hasSelectedChecklistHelp && selectedChecklistHelpId == checkId)
        {
            hasSelectedChecklistHelp = false;
            return;
        }

        selectedChecklistHelpId = checkId;
        hasSelectedChecklistHelp = true;
    }

    private bool IsChecklistHelpSelected(GCStartScreenReadinessCheck check)
    {
        if (check == null || string.IsNullOrEmpty(check.helpText) || !hasSelectedChecklistHelp)
        {
            return false;
        }

        return selectedChecklistHelpId == GCStartScreenReadiness.NormalizeCheckId(check.id);
    }

    private static void DrawChecklistRowBackground(Rect rowRect, int index)
    {
        EditorGUI.DrawRect(rowRect, GetChecklistRowColor(index));

        var dividerColor = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.06f)
            : new Color(0f, 0f, 0f, 0.08f);
        EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), dividerColor);
    }

    private static Color GetChecklistRowColor(int index)
    {
        if (EditorGUIUtility.isProSkin)
        {
            return index % 2 == 0
                ? new Color(1f, 1f, 1f, 0.045f)
                : new Color(1f, 1f, 1f, 0.025f);
        }

        return index % 2 == 0
            ? new Color(0f, 0f, 0f, 0.045f)
            : new Color(0f, 0f, 0f, 0.02f);
    }

    private static void DrawChecklistStatusIndicator(Rect statusRect, GCStartScreenReadinessCheckState state)
    {
        if (!HasVisibleRect(statusRect))
        {
            return;
        }

        var indicatorSize = Mathf.Min(ChecklistStatusIndicatorSize, Mathf.Min(statusRect.width, statusRect.height));
        if (indicatorSize <= 0f)
        {
            return;
        }

        var indicatorRect = new Rect(
            statusRect.x + (statusRect.width - indicatorSize) * 0.5f,
            statusRect.y + (statusRect.height - indicatorSize) * 0.5f,
            indicatorSize,
            indicatorSize
        );
        var outlineColor = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.22f)
            : new Color(0f, 0f, 0f, 0.18f);

        EditorGUI.DrawRect(
            new Rect(indicatorRect.x - 1f, indicatorRect.y - 1f, indicatorRect.width + 2f, indicatorRect.height + 2f),
            outlineColor
        );
        EditorGUI.DrawRect(indicatorRect, GetChecklistStateColor(state));
        GUI.Label(statusRect, new GUIContent(string.Empty, GetStateTooltip(state)), GUIStyle.none);
    }

    private static bool HasVisibleRect(Rect rect)
    {
        return rect.width > 0f && rect.height > 0f;
    }

    private static Color GetChecklistStateColor(GCStartScreenReadinessCheckState state)
    {
        switch (state)
        {
            case GCStartScreenReadinessCheckState.Pass:
                return new Color(0.22f, 0.72f, 0.34f, 1f);
            case GCStartScreenReadinessCheckState.Warning:
            case GCStartScreenReadinessCheckState.Blocked:
                return new Color(0.95f, 0.62f, 0.18f, 1f);
            case GCStartScreenReadinessCheckState.Fail:
                return new Color(0.86f, 0.26f, 0.24f, 1f);
            default:
                return EditorGUIUtility.isProSkin
                    ? new Color(0.72f, 0.72f, 0.72f, 1f)
                    : new Color(0.38f, 0.38f, 0.38f, 1f);
        }
    }

    private static string GetStateTooltip(GCStartScreenReadinessCheckState state)
    {
        switch (state)
        {
            case GCStartScreenReadinessCheckState.Pass:
                return "Ready";
            case GCStartScreenReadinessCheckState.Warning:
                return "Warning";
            case GCStartScreenReadinessCheckState.Blocked:
                return "Blocked";
            case GCStartScreenReadinessCheckState.Fail:
                return "Error";
            default:
                return "Unknown";
        }
    }

    private static GUIStyle GetChecklistLabelStyle()
    {
        return new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft
        };
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
