using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public struct LoopSample
    {
        public Vector2 position;
        public Vector2 tangent;
        public Vector2 normal;
        public float distance;
    }

    public sealed class LoopPathCache
    {
        private readonly LoopSample[] samples;
        public float Length { get; }
        public float EntryDistance { get; }

        public LoopPathCache(IReadOnlyList<Vector3> controlPoints, int samplesPerSegment = 20)
        {
            if (controlPoints == null || controlPoints.Count < 3)
                throw new ArgumentException("A closed loop needs at least three points.", nameof(controlPoints));

            int pointCount = controlPoints.Count;
            int totalSamples = pointCount * samplesPerSegment;
            samples = new LoopSample[totalSamples + 1];

            float currentDistance = 0f;
            Vector2 lastPos = controlPoints[0];

            for (int i = 0; i < pointCount; i++)
            {
                Vector2 p0 = controlPoints[(i - 1 + pointCount) % pointCount];
                Vector2 p1 = controlPoints[i];
                Vector2 p2 = controlPoints[(i + 1) % pointCount];
                Vector2 p3 = controlPoints[(i + 2) % pointCount];

                for (int j = 0; j < samplesPerSegment; j++)
                {
                    float t = j / (float)samplesPerSegment;
                    Vector2 pos = GetCatmullRomPosition(t, p0, p1, p2, p3);
                    Vector2 tangent = GetCatmullRomTangent(t, p0, p1, p2, p3).normalized;
                    Vector2 normal = new Vector2(-tangent.y, tangent.x); // rotated 90 degrees

                    if (i == 0 && j == 0)
                    {
                        samples[0] = new LoopSample { position = pos, tangent = tangent, normal = normal, distance = 0f };
                    }
                    else
                    {
                        int index = i * samplesPerSegment + j;
                        currentDistance += Vector2.Distance(lastPos, pos);
                        samples[index] = new LoopSample { position = pos, tangent = tangent, normal = normal, distance = currentDistance };
                    }
                    lastPos = pos;
                }
            }

            // Final sample wraps to the first one for seamless interpolation
            Vector2 finalPos = controlPoints[0];
            currentDistance += Vector2.Distance(lastPos, finalPos);
            samples[totalSamples] = new LoopSample 
            { 
                position = finalPos, 
                tangent = samples[0].tangent, 
                normal = samples[0].normal, 
                distance = currentDistance 
            };

            Length = currentDistance;
            EntryDistance = 0f; // P00_ENTRY is at index 0
        }

        // Backward compatibility constructor
        public LoopPathCache(Vector3 center, float radiusX, float radiusY, int samplesCount)
        {
            List<Vector3> pts = new List<Vector3>();
            int ptsCount = 12; // Approximation
            for(int i = 0; i < ptsCount; i++) {
                float angle = i * Mathf.PI * 2f / ptsCount;
                pts.Add(center + new Vector3(Mathf.Cos(angle) * radiusX, -Mathf.Sin(angle) * radiusY, 0));
            }
            
            var cache = new LoopPathCache(pts, samplesCount / ptsCount + 1);
            samples = cache.samples;
            Length = cache.Length;
            EntryDistance = 0f;
        }

        private Vector2 GetCatmullRomPosition(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private Vector2 GetCatmullRomTangent(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float t2 = t * t;
            return 0.5f * (
                (-p0 + p2) +
                2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t +
                3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2
            );
        }

        public void Evaluate(float distance, out Vector3 position, out Quaternion rotation)
        {
            EvaluateDistance(distance, out Vector2 pos, out Vector2 tang, out Vector2 norm);
            position = pos;
            rotation = Quaternion.LookRotation(Vector3.forward, tang);
        }

        public void EvaluateDistance(float distance, out Vector2 position, out Vector2 tangent, out Vector2 normal)
        {
            if (Length <= 0f)
            {
                position = tangent = normal = Vector2.zero;
                return;
            }

            distance = Mathf.Repeat(distance, Length);

            // Binary search to find the correct sample segment
            int lower = 0;
            int upper = samples.Length - 1;
            while (lower <= upper)
            {
                int mid = (lower + upper) / 2;
                if (samples[mid].distance < distance)
                    lower = mid + 1;
                else
                    upper = mid - 1;
            }

            int indexUpper = Mathf.Clamp(lower, 1, samples.Length - 1);
            int indexLower = indexUpper - 1;

            LoopSample s1 = samples[indexLower];
            LoopSample s2 = samples[indexUpper];

            float segmentDist = s2.distance - s1.distance;
            float t = segmentDist > 0f ? (distance - s1.distance) / segmentDist : 0f;

            position = Vector2.Lerp(s1.position, s2.position, t);
            tangent = Vector2.Lerp(s1.tangent, s2.tangent, t).normalized;
            normal = Vector2.Lerp(s1.normal, s2.normal, t).normalized;
        }

        public float FindClosestDistance(Vector3 worldPosition)
        {
            float bestSqrDist = float.PositiveInfinity;
            float bestDistance = 0f;
            Vector2 wp = worldPosition;

            for (int i = 0; i < samples.Length - 1; i++)
            {
                Vector2 s1 = samples[i].position;
                Vector2 s2 = samples[i+1].position;
                Vector2 segment = s2 - s1;
                float lengthSqr = segment.sqrMagnitude;
                float t = lengthSqr > 0f ? Mathf.Clamp01(Vector2.Dot(wp - s1, segment) / lengthSqr) : 0f;
                Vector2 closest = s1 + segment * t;
                float sqrDist = (wp - closest).sqrMagnitude;
                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    bestDistance = samples[i].distance + Mathf.Sqrt(lengthSqr) * t;
                }
            }
            return bestDistance;
        }
        
        public IReadOnlyList<LoopSample> GetSamples() => samples;
    }
}
