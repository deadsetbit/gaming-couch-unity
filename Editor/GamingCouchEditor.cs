using UnityEditor;
using UnityEngine;
using DSB.GC;

public class GamingCouchEditor
{
  [MenuItem("GameObject/GamingCouch", false, 0)]
  [MenuItem("Assets/Create/GamingCouch", false, 0)]
  [MenuItem("GamingCouch/Create GamingCouch GameObject")]
  static void CreatePrefabInstance()
  {
    GameObject go = new GameObject();
    go.AddComponent<GamingCouch>();
    if (go == null)
    {
      return;
    }

    go.name = "GamingCouch";

    Selection.activeObject = go;
  }
}
