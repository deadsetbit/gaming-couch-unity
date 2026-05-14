using DSB.GC;
using UnityEngine;

internal static class GamingCouchEditorTestSupport
{
    internal static GamingCouch CreateGamingCouch(string name)
    {
        var gameObject = new GameObject(name);
        gameObject.SetActive(false);
        return gameObject.AddComponent<GamingCouch>();
    }

    internal static GameObject CreateCompatibleListener(string name)
    {
        var gameObject = new GameObject(name);
        gameObject.AddComponent<CompatibleGameScriptReceiver>();
        return gameObject;
    }

    internal static GameObject CreatePlayerPrefabObject(string name)
    {
        var gameObject = new GameObject(name);
        gameObject.AddComponent<GCPlayer>();
        return gameObject;
    }
}

internal sealed class CompatibleGameScriptReceiver : MonoBehaviour
{
    private void GamingCouchSetup(GCSetupOptions options)
    {
    }

    private void GamingCouchPlay(GCPlayOptions options)
    {
    }
}

internal sealed class SetupOnlyGameScriptReceiver : MonoBehaviour
{
    private void GamingCouchSetup(GCSetupOptions options)
    {
    }
}

internal abstract class CompatibleGameScriptReceiverBase : MonoBehaviour
{
    public void GamingCouchSetup(GCSetupOptions options)
    {
    }

    public void GamingCouchPlay(GCPlayOptions options)
    {
    }
}

internal sealed class InheritedCompatibleGameScriptReceiver : CompatibleGameScriptReceiverBase
{
}

internal sealed class ColorPlaceholderPrefabPlayer : GCPlayer
{
    [SerializeField]
    private Renderer colorRenderer;
}

internal sealed class GCGameExample : MonoBehaviour
{
    private void GamingCouchSetup(GCSetupOptions options)
    {
    }

    private void GamingCouchPlay(GCPlayOptions options)
    {
    }
}

internal sealed class GCPlayerExample : GCPlayer
{
}

internal sealed class WrongSignatureGameScriptReceiver : MonoBehaviour
{
    public void GamingCouchSetup()
    {
    }

    public void GamingCouchPlay(string options)
    {
    }
}
