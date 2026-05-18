using UnityEditor;

public class GamingCouchMenuItems
{
  [MenuItem("GamingCouch/Start Screen")]
  static void OpenStartScreen()
  {
    GamingCouchStartScreenWindow.Open();
  }

  [MenuItem("GameObject/GamingCouch", false, 0)]
  [MenuItem("GamingCouch/Create GamingCouch GameObject")]
  static void CreatePrefabInstance()
  {
    var result = GamingCouchSceneWiring.EnsureActiveSceneGamingCouch();
    if (result.IsBlocked)
    {
      EditorUtility.DisplayDialog("Create GamingCouch", result.message, "OK");
      return;
    }

    if (result.gamingCouch != null)
    {
      Selection.activeObject = result.gamingCouch.gameObject;
    }
  }
}
