using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    [ExecuteAlways]
    public sealed partial class LoopPathAuthoring : MonoBehaviour
    {
        [SerializeField]
        private List<Transform> points = new();
        [SerializeField]
        private Color pathColor = new(0.2f, 1f, 0.55f, 0.9f);
        [SerializeField, Min(0.05f)]
        private float pointRadius = 0.13f;
        [Header("Physical Track Walls")]
        [Tooltip("Báº­t: biÃªn tá»± cháº¡y theo cÃ¡c Ä‘iá»ƒm P00-P19. Táº¯t: cÃ³ thá»ƒ chá»‰nh Edge Collider thá»§ cÃ´ng.")]
        [SerializeField]
        private bool autoFitBoundaries = true;
        [SerializeField, Min(0.15f)]
        private float trackHalfWidth = 0.43f;
        [SerializeField, Min(0f)]
        private float wallEdgeRadius = 0.05f;
        private EdgeCollider2D outerBoundary;
        private EdgeCollider2D innerBoundary;
        private PhysicsMaterial2D frictionlessWallMaterial;
        private int pointsHash;
        public IReadOnlyList<Transform> Points => points;
        public bool AutoFitBoundaries => autoFitBoundaries;

        public void SetPoints(List<Transform> pathPoints)
        {
            points = pathPoints;
            if (autoFitBoundaries)
                RebuildBoundaries();
        }

        public void SetBoundaryEditingMode(bool autoFit)
        {
            autoFitBoundaries = autoFit;
            if (autoFitBoundaries)
                RebuildBoundaries();
        }

        public void RebuildTrackBoundaries() => RebuildBoundaries();
#if UNITY_EDITOR
        [Sirenix.OdinInspector.Button("Reverse Path", Sirenix.OdinInspector.ButtonSizes.Large)]
        [Sirenix.OdinInspector.GUIColor(1f, 0.6f, 0.2f)]
        private void ReversePath()
        {
            if (points == null)
                return;
            points.Reverse();
            if (autoFitBoundaries)
                RebuildBoundaries();
            UnityEditor.EditorUtility.SetDirty(this);
            if (gameObject.scene != null)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

#endif
        private void OnEnable()
        {
            EnsureBoundaryObjects();
            if (autoFitBoundaries)
                RebuildBoundaries();
        }

        private void Update()
        {
            if (Application.isPlaying || !autoFitBoundaries)
                return;
            int currentHash = CalculatePointsHash();
            if (currentHash == pointsHash)
                return;
            RebuildBoundaries();
        }

        private void OnValidate()
        {
            if (autoFitBoundaries)
                RebuildBoundaries();
        }

        [ContextMenu("Boundary/Auto Fit From Path Points")]
        private void EnableAutoFit()
        {
            SetBoundaryEditingMode(true);
        }

        [ContextMenu("Boundary/Manual Edit - Keep Current Shape")]
        private void EnableManualEditing()
        {
            SetBoundaryEditingMode(false);
        }

        [ContextMenu("Boundary/Rebuild Track Boundaries Now")]
        private void RebuildBoundariesNow() => RebuildBoundaries();
        private void RebuildBoundaries()
        {
            if (points == null || points.Count < 4)
                return;
            EnsureBoundaryObjects();
            Vector2 center = Vector2.zero;
            int validCount = 0;
            for (int index = 0; index < points.Count; index++)
            {
                if (points[index] == null)
                    continue;
                center += (Vector2)transform.InverseTransformPoint(points[index].position);
                validCount++;
            }

            if (validCount < 4)
                return;
            center /= validCount;
            List<Vector2> outer = new();
            List<Vector2> inner = new();
            // Collider editing must stay practical. The movement path can keep its
            // dense Catmull-Rom samples, but physical boundaries only need one
            // vertex per authored control point (plus the closing vertex).
            BuildBoundaryPoints(validCount, outer, inner);
            pointsHash = CalculatePointsHash();
        }

        private void EnsureBoundaryObjects()
        {
            Transform walls = transform.Find("Loop Track Colliders");
            if (walls == null)
            {
                walls = new GameObject("Loop Track Colliders").transform;
                walls.SetParent(transform, false);
            }

            Transform oldOuter = walls.Find("Outer Boundary (Intake Gap)");
            if (oldOuter != null)
                oldOuter.name = "Outer Boundary (Closed)";
            outerBoundary = GetOrCreateEdge(walls, "Outer Boundary (Closed)");
            innerBoundary = GetOrCreateEdge(walls, "Inner Boundary");
            ApplyFrictionlessMaterial();
        }

        private void ApplyFrictionlessMaterial()
        {
            if (frictionlessWallMaterial == null)
            {
                frictionlessWallMaterial = new PhysicsMaterial2D("Loop Wall - Zero Friction")
                {
                    friction = 0f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine2D.Minimum,
                    bounceCombine = PhysicsMaterialCombine2D.Minimum
                };
                frictionlessWallMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            if (outerBoundary != null)
                outerBoundary.sharedMaterial = frictionlessWallMaterial;
            if (innerBoundary != null)
                innerBoundary.sharedMaterial = frictionlessWallMaterial;
        }

        private void OnDestroy()
        {
            if (frictionlessWallMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(frictionlessWallMaterial);
                else
                    DestroyImmediate(frictionlessWallMaterial);
            }
        }

        private static EdgeCollider2D GetOrCreateEdge(Transform parent, string objectName)
        {
            Transform child = parent.Find(objectName);
            if (child == null)
            {
                child = new GameObject(objectName).transform;
                child.SetParent(parent, false);
            }

            EdgeCollider2D edge = child.GetComponent<EdgeCollider2D>();
            return edge != null ? edge : child.gameObject.AddComponent<EdgeCollider2D>();
        }

        private int CalculatePointsHash()
        {
            unchecked
            {
                int hash = 17;
                for (int index = 0; index < points.Count; index++)
                    hash = hash * 31 + (points[index] != null ? points[index].position.GetHashCode() : 0);
                return hash;
            }
        }

        public LoopPathCache BuildPath()
        {
            List<Vector3> positions = new(points.Count);
            for (int index = 0; index < points.Count; index++)
                if (points[index] != null)
                    positions.Add(points[index].position);
            return positions.Count >= 3 ? new LoopPathCache(positions) : null;
        }

        public void SetOuterBoundaryIgnored(Collider2D fruitCollider, bool ignored)
        {
            if (fruitCollider == null)
                return;
            if (outerBoundary == null)
                EnsureBoundaryObjects();
            if (outerBoundary != null)
                Physics2D.IgnoreCollision(outerBoundary, fruitCollider, ignored);
        }
    }
}
