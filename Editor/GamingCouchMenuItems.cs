using UnityEditor;

internal static class GamingCouchMenuPriorities
{
  internal const int StartScreen = 0;
  internal const int WebGLBuild = 20;
  internal const int CreateGamingCouchGameObject = 21;
}

public class GamingCouchMenuItems
{
  [MenuItem("GamingCouch/Start Screen", false, GamingCouchMenuPriorities.StartScreen)]
  static void OpenStartScreen()
  {
    GamingCouchStartScreenWindow.Open();
  }

  [MenuItem("GameObject/GamingCouch", false, 0)]
  [MenuItem("GamingCouch/Create GamingCouch GameObject", false, GamingCouchMenuPriorities.CreateGamingCouchGameObject)]
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
