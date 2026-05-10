using DSB.GC;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal enum GamingCouchSceneWiringStatus
{
    Succeeded,
    Unchanged,
    Blocked,
}

internal sealed class GamingCouchSceneWiringResult
{
    internal readonly GamingCouchSceneWiringStatus status;
    internal readonly GamingCouch gamingCouch;
    internal readonly bool changed;
    internal readonly string message;

    internal GamingCouchSceneWiringResult(
        GamingCouchSceneWiringStatus status,
        GamingCouch gamingCouch,
        bool changed,
        string message
    )
    {
        this.status = status;
        this.gamingCouch = gamingCouch;
        this.changed = changed;
        this.message = message;
    }

    internal bool IsBlocked
    {
        get { return status == GamingCouchSceneWiringStatus.Blocked; }
    }

    internal static GamingCouchSceneWiringResult SucceededResult(GamingCouch gamingCouch, string message)
    {
        return new GamingCouchSceneWiringResult(GamingCouchSceneWiringStatus.Succeeded, gamingCouch, true, message);
    }

    internal static GamingCouchSceneWiringResult UnchangedResult(GamingCouch gamingCouch, string message)
    {
        return new GamingCouchSceneWiringResult(GamingCouchSceneWiringStatus.Unchanged, gamingCouch, false, message);
    }

    internal static GamingCouchSceneWiringResult BlockedResult(GamingCouch gamingCouch, string message)
    {
        return new GamingCouchSceneWiringResult(GamingCouchSceneWiringStatus.Blocked, gamingCouch, false, message);
    }
}

internal static class GamingCouchSceneWiring
{
    internal const string ListenerPropertyName = "listener";
    internal const string PlayerPrefabPropertyName = "playerPrefab";

    private const string CreateGamingCouchUndoName = "Create GamingCouch";
    private const string AssignListenerUndoName = "Assign GamingCouch Listener";
    private const string AssignPlayerPrefabUndoName = "Assign GamingCouch Player Prefab";

    internal static GamingCouchSceneWiringResult EnsureActiveSceneGamingCouch()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return GamingCouchSceneWiringResult.BlockedResult(
                null,
                "No loaded active scene is available for GamingCouch setup."
            );
        }

        var gamingCouches = FindGamingCouchesInScene(scene);
        if (gamingCouches.Length > 1)
        {
            return GamingCouchSceneWiringResult.BlockedResult(
                null,
                "The active scene contains multiple GamingCouch components. Remove duplicates manually before running setup."
            );
        }

        if (gamingCouches.Length == 1)
        {
            return GamingCouchSceneWiringResult.UnchangedResult(
                gamingCouches[0],
                "The active scene already contains a GamingCouch object."
            );
        }

        var gameObject = new GameObject("GamingCouch");
        Undo.RegisterCreatedObjectUndo(gameObject, CreateGamingCouchUndoName);

        var gamingCouch = gameObject.AddComponent<GamingCouch>();
        if (gameObject.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(gameObject, scene);
        }

        return GamingCouchSceneWiringResult.SucceededResult(
            gamingCouch,
            "Created a GamingCouch object in the active scene."
        );
    }

    internal static GamingCouchSceneWiringResult AssignListenerIfMissing(GamingCouch gamingCouch, GameObject listener)
    {
        return AssignObjectReferenceIfMissing(
            gamingCouch,
            ListenerPropertyName,
            listener,
            AssignListenerUndoName,
            "listener"
        );
    }

    internal static GamingCouchSceneWiringResult AssignPlayerPrefabIfMissing(GamingCouch gamingCouch, GameObject playerPrefab)
    {
        return AssignObjectReferenceIfMissing(
            gamingCouch,
            PlayerPrefabPropertyName,
            playerPrefab,
            AssignPlayerPrefabUndoName,
            "player prefab"
        );
    }

    internal static GamingCouch[] FindActiveSceneGamingCouches()
    {
        return FindGamingCouchesInScene(SceneManager.GetActiveScene());
    }

    internal static GamingCouch[] FindGamingCouchesInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return new GamingCouch[0];
        }

        var gamingCouches = new List<GamingCouch>();
        var roots = scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var root = roots[rootIndex];
            if (root == null)
            {
                continue;
            }

            var components = root.GetComponentsInChildren<GamingCouch>(true);
            for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                var component = components[componentIndex];
                if (component != null)
                {
                    gamingCouches.Add(component);
                }
            }
        }

        return gamingCouches.ToArray();
    }

    internal static UnityEngine.Object ReadObjectReference(GamingCouch gamingCouch, string propertyName)
    {
        if (gamingCouch == null)
        {
            return null;
        }

        var serializedObject = new SerializedObject(gamingCouch);
        serializedObject.Update();
        var property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
        {
            return null;
        }

        return property.objectReferenceValue;
    }

    private static GamingCouchSceneWiringResult AssignObjectReferenceIfMissing(
        GamingCouch gamingCouch,
        string propertyName,
        GameObject reference,
        string undoName,
        string displayName
    )
    {
        if (gamingCouch == null)
        {
            return GamingCouchSceneWiringResult.BlockedResult(
                null,
                "A GamingCouch object is required before assigning the " + displayName + " reference."
            );
        }

        if (reference == null)
        {
            return GamingCouchSceneWiringResult.BlockedResult(
                gamingCouch,
                "A " + displayName + " object is required before assigning the GamingCouch reference."
            );
        }

        var serializedObject = new SerializedObject(gamingCouch);
        serializedObject.Update();
        var property = serializedObject.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
        {
            return GamingCouchSceneWiringResult.BlockedResult(
                gamingCouch,
                "The GamingCouch " + displayName + " serialized field could not be found."
            );
        }

        if (HasSerializedObjectReference(property))
        {
            return GamingCouchSceneWiringResult.UnchangedResult(
                gamingCouch,
                "The GamingCouch " + displayName + " reference already contains a serialized reference."
            );
        }

        Undo.RecordObject(gamingCouch, undoName);
        property.objectReferenceValue = reference;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gamingCouch);
        MarkOwningSceneDirty(gamingCouch);

        return GamingCouchSceneWiringResult.SucceededResult(
            gamingCouch,
            "Assigned the GamingCouch " + displayName + " reference."
        );
    }

    private static bool HasSerializedObjectReference(SerializedProperty property)
    {
        return property.objectReferenceValue != null || property.objectReferenceInstanceIDValue != 0;
    }

    private static void MarkOwningSceneDirty(GamingCouch gamingCouch)
    {
        if (gamingCouch == null || gamingCouch.gameObject == null)
        {
            return;
        }

        var scene = gamingCouch.gameObject.scene;
        if (scene.IsValid() && scene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
