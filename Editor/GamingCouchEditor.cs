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
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        GamingCouchInspectorHost.DrawSerializedFields(serializedObject);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        devJsonView.Draw(devJsonState);
    }
}
