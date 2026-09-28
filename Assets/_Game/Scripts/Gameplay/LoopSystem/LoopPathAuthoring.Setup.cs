using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class LoopPathAuthoring
    {
        public Vector2 ConstrainFruitCenterToTrack(Vector2 position, Vector2 pathCenter, float fruitRadius)
        {
            if (outerBoundary == null || innerBoundary == null)
                EnsureBoundaryObjects();
            float clearance = Mathf.Max(0.01f, fruitRadius + wallEdgeRadius + 0.025f);
            // Repeat because correcting against one wall can move a large fruit
            // closer to the opposite wall on narrow hand-authored sections.
            for (int pass = 0; pass < 2; pass++)
            {
                position = PushInsideBoundary(position, pathCenter, outerBoundary, clearance);
                position = PushInsideBoundary(position, pathCenter, innerBoundary, clearance);
            }

            return position;
        }

        private static Vector2 PushInsideBoundary(Vector2 position, Vector2 pathCenter, EdgeCollider2D boundary, float clearance)
        {
            if (boundary == null || !boundary.enabled)
                return position;
            Vector2 closest = boundary.ClosestPoint(position);
            Vector2 inward = pathCenter - closest;
            if (inward.sqrMagnitude < 0.000001f)
                inward = pathCenter - position;
            if (inward.sqrMagnitude < 0.000001f)
                return position;
            inward.Normalize();
            float signedClearance = Vector2.Dot(position - closest, inward);
            return signedClearance < clearance ? closest + inward * clearance : position;
        }

        private void OnDrawGizmos()
        {
            if (points == null || points.Count < 2)
                return;
            var cache = BuildPath();
            if (cache == null)
                return;
            var samples = cache.GetSamples();
            Gizmos.color = pathColor;
            for (int i = 0; i < samples.Count - 1; i++)
            {
                Vector3 current = samples[i].position;
                Vector3 next = samples[i + 1].position;
                Gizmos.DrawLine(current, next);
                // Draw occasional arrows
                if (i % 20 == 0)
                {
                    Vector3 direction = (next - current).normalized;
                    Vector3 arrowPosition = Vector3.Lerp(current, next, 0.5f);
                    Vector3 side = Vector3.Cross(direction, Vector3.forward) * pointRadius * 1.6f;
                    Gizmos.DrawLine(arrowPosition, arrowPosition - direction * pointRadius * 2.5f + side);
                    Gizmos.DrawLine(arrowPosition, arrowPosition - direction * pointRadius * 2.5f - side);
                }
            }

            for (int index = 0; index < points.Count; index++)
            {
                if (points[index] != null)
                    Gizmos.DrawSphere(points[index].position, pointRadius);
            }
        }

        private void BuildBoundaryPoints(int validCount, List<Vector2> outer, List<Vector2> inner)
        {
            List<Vector2> localPoints = new(validCount);
            for (int index = 0; index < points.Count; index++)
            {
                if (points[index] != null)
                    localPoints.Add(transform.InverseTransformPoint(points[index].position));
            }

            for (int index = 0; index < localPoints.Count; index++)
            {
                Vector2 previous = localPoints[(index - 1 + localPoints.Count) % localPoints.Count];
                Vector2 current = localPoints[index];
                Vector2 next = localPoints[(index + 1) % localPoints.Count];
                Vector2 tangent = (next - previous).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                outer.Add(current + normal * trackHalfWidth);
                inner.Add(current - normal * trackHalfWidth);
            }

            // EdgeCollider2D is open by definition, so repeat the first point once.
            outer.Add(outer[0]);
            inner.Add(inner[0]);
            outerBoundary.points = outer.ToArray();
            innerBoundary.points = inner.ToArray();
            outerBoundary.edgeRadius = wallEdgeRadius;
            innerBoundary.edgeRadius = wallEdgeRadius;
            ApplyFrictionlessMaterial();
        }
    }
}
