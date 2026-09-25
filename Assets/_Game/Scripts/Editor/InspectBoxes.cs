using UnityEngine;
using UnityEditor;
using BubbleFruitLoop.Gameplay;

public static class InspectBoxes
{
    [InitializeOnLoadMethod]
    public static void Inspect()
    {
        var boxes = Object.FindObjectsByType<BoxView>(FindObjectsSortMode.None);
        Debug.Log($"Found {boxes.Length} BoxViews in the scene.");
        foreach (var box in boxes)
        {
            var typeField = typeof(BoxView).GetField("configuredType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var colorField = typeof(BoxView).GetField("configuredColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            FruitType t = (FruitType)typeField.GetValue(box);
            Color c = (Color)colorField.GetValue(box);
            Debug.Log($"Box {box.name} at {box.transform.position}: Type={t}, Color={c}");
        }
    }
}
