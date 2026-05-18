using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal sealed class GCWebGLPreviewApplyOutcome
{
    internal readonly bool changed;
    internal readonly string message;
    internal readonly MessageType messageType;
    internal readonly string[] details;

    internal GCWebGLPreviewApplyOutcome(
        bool changed,
        string message,
        MessageType messageType,
        string[] details
    )
    {
        this.changed = changed;
        this.message = message;
        this.messageType = messageType;
        this.details = details ?? new string[0];
    }
}

internal sealed class GamingCouchWebGLBuildSettingsPreviewWindow : EditorWindow
{
    private const float WindowWidth = 620f;
    private const float WindowHeight = 480f;

    private string heading;
    private string message;
    private string applyLabel;
    private GCWebGLPreviewRow[] rows = new GCWebGLPreviewRow[0];
    private HashSet<string> selectedSkippableRowIds = new HashSet<string>(StringComparer.Ordinal);
    private Func<string[], GCWebGLPreviewApplyOutcome> applyHandler;
    private Vector2 scrollPosition;
    private bool canApply;

    internal static void OpenReleaseProfile()
    {
        var plan = GamingCouchWebGLBuildSettingsProfiles.BuildReleaseProfilePlan();
        OpenProfilePlan(
            "GamingCouch Release WebGL Build Settings",
            plan,
            "Apply Release Settings",
            selectedIds => FromProfileResult(
                GamingCouchWebGLBuildSettingsProfiles.ApplyReleaseProfile(selectedIds)
            )
        );
    }

    internal static void OpenDevProfile()
    {
        var plan = GamingCouchWebGLBuildSettingsProfiles.BuildDevProfilePlan();
        OpenProfilePlan(
            "GamingCouch Dev WebGL Build Settings",
            plan,
            "Apply Dev Settings",
            selectedIds => FromProfileResult(
                GamingCouchWebGLBuildSettingsProfiles.ApplyDevProfile(selectedIds)
            )
        );
    }

    internal static void OpenCleanExport(Action<GCWebGLExportSetupResult> onApplied)
    {
        var plan = GamingCouchWebGLExportSetup.CreateCleanWebGLExportSetupPlan();
        OpenCleanExport(plan, onApplied);
    }

    internal static void OpenCleanExport(
        GCWebGLExportSetupPlan plan,
        Action<GCWebGLExportSetupResult> onApplied
    )
    {
        var safePlan = plan ?? GamingCouchWebGLExportSetup.CreateCleanWebGLExportSetupPlan();
        OpenWindow(
            "GamingCouch Clean WebGL Export Setup",
            safePlan.message,
            "Apply Clean Export Setup",
            safePlan.rows,
            safePlan.GetDefaultSelectedSkippableRowIds(),
            !safePlan.IsBlocked && safePlan.HasChanges,
            selectedIds =>
            {
                var result = GamingCouchWebGLExportSetup.ApplyCleanWebGLExportSetupPlan(
                    safePlan,
                    selectedIds
                );
                if (onApplied != null)
                {
                    onApplied(result);
                }

                return FromCleanExportResult(result);
            }
        );
    }

    private static void OpenProfilePlan(
        string windowTitle,
        GCWebGLBuildSettingsProfilePlan plan,
        string applyButtonLabel,
        Func<string[], GCWebGLPreviewApplyOutcome> apply
    )
    {
        var safePlan = plan ?? GamingCouchWebGLBuildSettingsProfiles.BuildReleaseProfilePlan();
        OpenWindow(
            windowTitle,
            safePlan.HasChanges
                ? "Review " + safePlan.displayName + " WebGL build setting changes before applying them."
                : safePlan.displayName + " WebGL build settings are already configured.",
            applyButtonLabel,
            safePlan.rows,
            GetDefaultSelectedSkippableRowIds(safePlan.rows),
            safePlan.HasChanges,
            apply
        );
    }

    private static void OpenWindow(
        string windowTitle,
        string message,
        string applyButtonLabel,
        GCWebGLPreviewRow[] rows,
        string[] defaultSelectedSkippableRowIds,
        bool canApply,
        Func<string[], GCWebGLPreviewApplyOutcome> apply
    )
    {
        var window = CreateInstance<GamingCouchWebGLBuildSettingsPreviewWindow>();
        window.titleContent = new GUIContent(windowTitle);
        window.heading = windowTitle;
        window.message = message;
        window.applyLabel = applyButtonLabel;
        window.rows = rows ?? new GCWebGLPreviewRow[0];
        window.selectedSkippableRowIds = new HashSet<string>(
            defaultSelectedSkippableRowIds ?? new string[0],
            StringComparer.Ordinal
        );
        window.canApply = canApply;
        window.applyHandler = apply;
        window.minSize = new Vector2(WindowWidth, WindowHeight);
        window.maxSize = new Vector2(WindowWidth, 800f);
        window.ShowUtility();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(message, GetHeaderMessageType());
        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawRows();
        EditorGUILayout.EndScrollView();

        GUILayout.FlexibleSpace();
        DrawFooterButtons();
    }

    private void DrawRows()
    {
        var drewRow = false;
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (row == null || !row.isChanged)
            {
                continue;
            }

            drewRow = true;
            DrawRow(row);
        }

        if (!drewRow)
        {
            EditorGUILayout.HelpBox("No WebGL build setting changes are needed.", MessageType.Info);
        }
    }

    private void DrawRow(GCWebGLPreviewRow row)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (row.isSkippable)
            {
                var selected = selectedSkippableRowIds.Contains(row.id);
                var nextSelected = EditorGUILayout.ToggleLeft(row.DiffText, selected);
                if (nextSelected)
                {
                    selectedSkippableRowIds.Add(row.id);
                }
                else
                {
                    selectedSkippableRowIds.Remove(row.id);
                }

                return;
            }

            EditorGUILayout.LabelField(row.DiffText, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(
                row.isBlocked ? "Blocked" : "Required",
                row.isBlocked ? EditorStyles.boldLabel : EditorStyles.miniLabel
            );
        }
    }

    private void DrawFooterButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(90f)))
            {
                Close();
            }

            using (new EditorGUI.DisabledScope(!canApply || applyHandler == null))
            {
                if (GUILayout.Button(applyLabel, GUILayout.Width(180f)))
                {
                    Apply();
                }
            }
        }
    }

    private void Apply()
    {
        var outcome = applyHandler(GetSelectedSkippableRowIds());
        LogOutcome(outcome);
        Close();
    }

    private string[] GetSelectedSkippableRowIds()
    {
        var selectedIds = new List<string>();
        foreach (var id in selectedSkippableRowIds)
        {
            selectedIds.Add(id);
        }

        return selectedIds.ToArray();
    }

    private MessageType GetHeaderMessageType()
    {
        for (var index = 0; index < rows.Length; index++)
        {
            if (rows[index] != null && rows[index].isBlocked)
            {
                return MessageType.Error;
            }
        }

        return canApply ? MessageType.Info : MessageType.None;
    }

    private static string[] GetDefaultSelectedSkippableRowIds(GCWebGLPreviewRow[] rows)
    {
        var selectedIds = new List<string>();
        for (var index = 0; rows != null && index < rows.Length; index++)
        {
            if (rows[index] != null &&
                rows[index].isChanged &&
                rows[index].isSkippable)
            {
                selectedIds.Add(rows[index].id);
            }
        }

        return selectedIds.ToArray();
    }

    private static GCWebGLPreviewApplyOutcome FromProfileResult(GCWebGLBuildSettingsProfileResult result)
    {
        if (result == null)
        {
            return new GCWebGLPreviewApplyOutcome(
                false,
                "WebGL build settings did not return a result.",
                MessageType.Error,
                null
            );
        }

        return new GCWebGLPreviewApplyOutcome(
            result.changed,
            result.message,
            MessageType.Info,
            result.details
        );
    }

    private static GCWebGLPreviewApplyOutcome FromCleanExportResult(GCWebGLExportSetupResult result)
    {
        if (result == null)
        {
            return new GCWebGLPreviewApplyOutcome(
                false,
                "Clean WebGL export setup did not return a result.",
                MessageType.Error,
                null
            );
        }

        return new GCWebGLPreviewApplyOutcome(
            result.changed,
            result.message,
            GetCleanExportMessageType(result),
            result.details
        );
    }

    private static MessageType GetCleanExportMessageType(GCWebGLExportSetupResult result)
    {
        if (result.IsBlocked)
        {
            return MessageType.Error;
        }

        return result.HasWarning ? MessageType.Warning : MessageType.Info;
    }

    private static void LogOutcome(GCWebGLPreviewApplyOutcome outcome)
    {
        if (outcome == null)
        {
            return;
        }

        var feedback = FormatFeedback(outcome.message, outcome.details);
        if (outcome.messageType == MessageType.Error)
        {
            Debug.LogError(feedback);
            EditorUtility.DisplayDialog("GamingCouch WebGL Build Settings", feedback, "OK");
            return;
        }

        if (outcome.messageType == MessageType.Warning)
        {
            Debug.LogWarning(feedback);
            EditorUtility.DisplayDialog("GamingCouch WebGL Build Settings", feedback, "OK");
            return;
        }

        if (outcome.changed || outcome.details.Length > 0)
        {
            Debug.Log(feedback);
        }
    }

    private static string FormatFeedback(string message, string[] details)
    {
        if (details == null || details.Length == 0)
        {
            return message;
        }

        return message + "\n\n" + string.Join("\n", details);
    }
}
