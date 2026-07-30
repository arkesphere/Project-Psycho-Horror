using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HierarchyOrganizer
{
    // name keyword -> category parent name
    private static readonly (string keyword, string category)[] Rules =
    {
        ("door", "Doors"),
        ("wall", "Walls"),
        ("floor", "Floors"),
        ("ceiling", "Ceilings"),
        ("light", "Lights"),
        ("lamp", "Lights"),
        ("window", "Windows"),
        ("roof", "Roofs"),
        ("stair", "Stairs"),
        ("prop", "Props"),
        ("furniture", "Furniture"),
        ("table", "Furniture"),
        ("chair", "Furniture"),
    };

    [MenuItem("Tools/Organize Hierarchy By Name")]
    public static void OrganizeActiveScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();

        var categoryParents = new Dictionary<string, Transform>();
        int movedCount = 0;

        foreach (var go in roots.ToList())
        {
            string lowerName = go.name.ToLowerInvariant();
            string matchedCategory = null;

            foreach (var (keyword, category) in Rules)
            {
                if (lowerName.Contains(keyword))
                {
                    matchedCategory = category;
                    break;
                }
            }

            if (matchedCategory == null)
                continue;

            if (!categoryParents.TryGetValue(matchedCategory, out var parentTransform))
            {
                var existing = roots.FirstOrDefault(r => r.name == matchedCategory);
                GameObject parentGO = existing != null ? existing : new GameObject(matchedCategory);
                parentTransform = parentGO.transform;
                categoryParents[matchedCategory] = parentTransform;
            }

            if (go.transform.parent == parentTransform)
                continue;

            Undo.SetTransformParent(go.transform, parentTransform, "Organize Hierarchy");
            movedCount++;
        }

        EditorSceneManager.MarkSceneDirty(scene); // marks unsaved changes, does NOT save to disk
        Debug.Log($"Hierarchy organized: moved {movedCount} object(s) into {categoryParents.Count} category group(s). Scene not saved — save manually if you want to keep it.");
    }
}
