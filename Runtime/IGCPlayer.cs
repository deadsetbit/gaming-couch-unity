using DSB.GC;
using UnityEngine;

public interface IGCPlayer
{
    void GamingCouchSetup(GCPlayerSetupOptions options);
    int GetId();
    string GetName();
    Color GetColor();
    Transform transform { get; }
    GameObject gameObject { get; }
}
