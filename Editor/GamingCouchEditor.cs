using DSB.GC;
using DSB.GC.Dev;
using System;
using System.Collections.Generic;
using UnityEditor;

[CustomEditor(typeof(GamingCouch))]
internal sealed class GamingCouchEditor : Editor
{
    private GCDevJsonInspectorState devJsonState;
    private GCDevJsonInspectorView devJsonView;

    private void OnEnable()
    {
        devJsonState = new GCDevJsonInspectorState();
        devJsonView = new GCDevJsonInspectorView();
        GCDevJsonEditorPlayModeGate.Register(devJsonState);
        EditorApplication.update -= OnEditorApplicationUpdate;
        EditorApplication.update += OnEditorApplicationUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        DisposeDevJsonState();
    }

    public override void OnInspectorGUI()
    {
        if (target == null)
        {
            DisposeDevJsonState();
            return;
        }

        serializedObject.Update();
        GamingCouchInspectorHost.DrawSerializedFields(serializedObject);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (devJsonView != null)
        {
            devJsonView.Draw(devJsonState);
        }
    }

    private void OnEditorApplicationUpdate()
    {
        if (target == null)
        {
            DisposeDevJsonState();
            return;
        }

        if (devJsonState != null && devJsonState.PollForExternalChanges())
        {
            Repaint();
        }
    }

    private void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (devJsonState != null && devJsonState.HandlePlayModeStateChanged(change))
        {
            Repaint();
        }
    }

    private void DisposeDevJsonState()
    {
        EditorApplication.update -= OnEditorApplicationUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        GCDevJsonEditorPlayModeGate.Unregister(devJsonState);
        devJsonState = null;
        devJsonView = null;
    }
}

[InitializeOnLoad]
internal static class GCDevJsonEditorPlayModeGate
{
    private static readonly List<GCDevJsonInspectorState> states = new List<GCDevJsonInspectorState>();

    static GCDevJsonEditorPlayModeGate()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        GCEditorPlayPreflight.RegisterPreflightHandler(RunPreflight);
        GCEditorPlayPreflight.RegisterCaptureSucceededHandler(MarkPlayChangesCaptured);
    }

    internal static void Register(GCDevJsonInspectorState state)
    {
        if (state == null || states.Contains(state))
        {
            return;
        }

        states.Add(state);
    }

    internal static void Unregister(GCDevJsonInspectorState state)
    {
        if (state == null)
        {
            return;
        }

        states.Remove(state);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode)
        {
            return;
        }

        var result = RunPreflight(GCEditorPlayPreflightContext.UnityPlayModeEntry);
        if (result.success)
        {
            return;
        }

        LogBlockedBoundary(result);
        EditorApplication.isPlaying = false;
    }

    private static GCEditorPlayPreflightResult RunPreflight(GCEditorPlayPreflightContext context)
    {
        try
        {
            var snapshot = GetStateSnapshot();
            GCDevJsonInspectorState dirtyState = null;
            for (var index = 0; index < snapshot.Length; index++)
            {
                var state = snapshot[index];
                var stateResult = state.ValidateForPlayBoundary(context);
                if (!stateResult.success)
                {
                    return stateResult;
                }

                if (!state.IsDirty)
                {
                    continue;
                }

                if (dirtyState != null)
                {
                    return FailForMultipleDirtyDrafts(context, dirtyState.DevJsonPath);
                }

                dirtyState = state;
            }

            if (dirtyState != null)
            {
                var applyResult = dirtyState.PrepareForPlayBoundary(context);
                if (!applyResult.success)
                {
                    return applyResult;
                }
            }

            return GCEditorPlayPreflight.ValidateRootJson(context);
        }
        catch (Exception exception)
        {
            var message = GCEditorPlayPreflight.GetBoundaryDisplayName(context) + " blocked because gc.dev.json preflight failed: " + exception.Message;
            return GCEditorPlayPreflightResult.Failed(
                message,
                null,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.ReadError, message, null))
            );
        }
    }

    private static void MarkPlayChangesCaptured()
    {
        var snapshot = GetStateSnapshot();
        for (var index = 0; index < snapshot.Length; index++)
        {
            var state = snapshot[index];
            state.MarkPlayChangesCaptured();
        }
    }

    private static GCDevJsonInspectorState[] GetStateSnapshot()
    {
        for (var index = states.Count - 1; index >= 0; index--)
        {
            if (states[index] == null)
            {
                states.RemoveAt(index);
            }
        }

        return states.ToArray();
    }

    private static GCEditorPlayPreflightResult FailForMultipleDirtyDrafts(GCEditorPlayPreflightContext context, string path)
    {
        var message = GCEditorPlayPreflight.GetBoundaryDisplayName(context) +
                      " blocked because multiple GamingCouch inspectors have unsaved gc.dev.json drafts. Apply, revert, or close duplicate inspectors before continuing.";
        return GCEditorPlayPreflightResult.Failed(
            message,
            path,
            GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.WriteError, message, path))
        );
    }

    private static void LogBlockedBoundary(GCEditorPlayPreflightResult result)
    {
        UnityEngine.Debug.LogError("[GamingCouch] " + result.message);
        var issues = result.validation != null ? result.validation.issues : null;
        if (issues == null || issues.Length == 0)
        {
            return;
        }

        for (var index = 0; index < issues.Length; index++)
        {
            var issue = issues[index];
            if (issue == null)
            {
                continue;
            }

            if (issue.severity == GCDevJsonIssueSeverity.Warning)
            {
                UnityEngine.Debug.LogWarning("[GamingCouch] " + GCDevJsonIssueFormatter.Format(issue));
            }
            else
            {
                UnityEngine.Debug.LogError("[GamingCouch] " + GCDevJsonIssueFormatter.Format(issue));
            }
        }
    }
}
