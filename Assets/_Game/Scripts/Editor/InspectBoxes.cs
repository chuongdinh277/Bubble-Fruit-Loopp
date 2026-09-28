using UnityEngine;
using UnityEditor;
using BubbleFruitLoop.Gameplay;

public static class InspectBoxes
{
    [MenuItem("BubbleFruit/Inspect Scene Boxes")]
    public static void Inspect()
    {
        var boxes = Object.FindObjectsByType<BoxView>(FindObjectsSortMode.None);
        Debug.Log($"Found {boxes.Length} BoxViews in the scene.");
        foreach (var box in boxes)
        {
            Debug.Log($"Box {box.name} at {box.transform.position}: Type={box.editorFruitType}", box);
        }
    }
}
