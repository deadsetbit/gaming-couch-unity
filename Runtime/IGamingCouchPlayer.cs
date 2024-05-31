using DSB.GC;
using UnityEngine;

public interface IGamingCouchPlayer
{
    void GamingCouchSetup(GamingCouchPlayerSetupOptions options);
    int GetId();
    string GetName();
    Color GetColor();
    Transform transform { get; }
    GameObject gameObject { get; }
}
