using DSB.GC;
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
        devJsonState = null;
        devJsonView = null;
    }
}
