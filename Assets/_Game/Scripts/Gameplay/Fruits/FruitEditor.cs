#if UNITY_EDITOR
using BubbleFruitLoop.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BubbleFruitLoop.Editor
{
    [CustomEditor(typeof(Fruit)), CanEditMultipleObjects]
    public sealed class FruitEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawFruitSizeEditor();
            EditorGUILayout.Space(8f);
            DrawDefaultInspector();
        }

        private void DrawFruitSizeEditor()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Fruit Size", EditorStyles.boldLabel);

            Fruit first = target as Fruit;
            float currentDiameter = GetWorldDiameter(first);

            EditorGUI.showMixedValue = HasMixedDiameter(currentDiameter);
            EditorGUI.BeginChangeCheck();
            float requestedDiameter = EditorGUILayout.DelayedFloatField(
                new GUIContent("World Diameter", "Kích thước hiển thị thực tế của fruit trong scene."),
                currentDiameter);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;

            if (changed)
            {
                requestedDiameter = Mathf.Max(0.05f, requestedDiameter);
                ApplyDiameterToSelection(requestedDiameter);
            }

            EditorGUILayout.LabelField(
                targets.Length > 1
                    ? $"Đang chỉnh {targets.Length} fruit cùng lúc"
                    : "Nhập số rồi nhấn Enter để áp dụng ngay",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private bool HasMixedDiameter(float firstDiameter)
        {
            for (int index = 1; index < targets.Length; index++)
            {
                if (targets[index] is not Fruit fruit) continue;
                if (!Mathf.Approximately(GetWorldDiameter(fruit), firstDiameter)) return true;
            }
            return false;
        }

        private void ApplyDiameterToSelection(float requestedDiameter)
        {
            Undo.SetCurrentGroupName("Resize Selected Fruits");
            int undoGroup = Undo.GetCurrentGroup();

            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] is not Fruit fruit) continue;
                SpriteRenderer renderer = fruit.VisualSpriteRenderer;
                float currentDiameter = GetWorldDiameter(fruit);
                if (renderer == null || currentDiameter <= 0.0001f) continue;

                Undo.RecordObject(fruit.transform, "Resize Fruit");
                float multiplier = requestedDiameter / currentDiameter;
                Vector3 scale = fruit.transform.localScale;
                fruit.transform.localScale = new Vector3(
                    scale.x * multiplier,
                    scale.y * multiplier,
                    scale.z);
                EditorUtility.SetDirty(fruit.transform);
                if (fruit.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(fruit.gameObject.scene);
            }

            Undo.CollapseUndoOperations(undoGroup);
            SceneView.RepaintAll();
        }

        private static float GetWorldDiameter(Fruit fruit)
        {
            SpriteRenderer renderer = fruit != null ? fruit.VisualSpriteRenderer : null;
            return renderer != null
                ? Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y)
                : 0f;
        }
    }
}
#endif
