#if ENABLE_PROFILER
using DSB.GC.Dev;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor entry point for the dev-only GC allocation profiler. Creates a GameObject in the active
// scene carrying GCManagedAllocProbe so the probe can be wired up without hand-editing scene YAML.
// Compiled only when ENABLE_PROFILER is defined (editor + Development builds), matching the probe.
public static class GamingCouchManagedAllocProbeMenu
{
  private const string ProbeObjectName = "GC Managed Alloc Probe";

  [MenuItem("GamingCouch/Dev/Add Managed Alloc Probe", false, 200)]
  private static void AddManagedAllocProbe()
  {
    var existing = Object.FindFirstObjectByType<GCManagedAllocProbe>(FindObjectsInactive.Include);
    if (existing != null)
    {
      Selection.activeGameObject = existing.gameObject;
      EditorGUIUtility.PingObject(existing.gameObject);
      Debug.Log($"[GamingCouch] Managed Alloc Probe already present on '{existing.gameObject.name}'. Selected it.");
      return;
    }

    var probe = ObjectFactory.CreateGameObject(ProbeObjectName, typeof(GCManagedAllocProbe));
    Undo.SetCurrentGroupName("Add Managed Alloc Probe");
    EditorSceneManager.MarkSceneDirty(probe.scene);
    Selection.activeGameObject = probe;
    EditorGUIUtility.PingObject(probe);

    if (EditorApplication.isPlayingOrWillChangePlaymode)
    {
      Debug.LogWarning(
        $"[GamingCouch] Added '{ProbeObjectName}' during Play mode; it will not be saved into the scene. " +
        "Add it in Edit mode if you want it to persist.");
      return;
    }

    Debug.Log(
      $"[GamingCouch] Added '{ProbeObjectName}' to scene '{probe.scene.name}'. " +
      "Set its scenario label in the Inspector, save the scene, then enter Play mode to log per-frame managed allocations.");
  }
}
#endif
