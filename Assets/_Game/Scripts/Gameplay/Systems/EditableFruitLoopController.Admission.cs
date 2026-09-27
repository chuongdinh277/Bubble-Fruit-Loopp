using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class EditableFruitLoopController
    {
        private void TryAdmitOne()
        {
            if (congestion.Count == 0) return;
            if (IsFull)
            {
                for (int index = 0; index < congestion.Count; index++)
                    if (congestion[index] != null) congestion[index].EnterCongestion(true);
                return;
            }

            int candidateIndex = FindBestCandidateIndex();
            if (candidateIndex < 0) return;
            FruitActor candidate = congestion[candidateIndex];
            congestion.RemoveAt(candidateIndex);
            owned.Add(candidate);
            pathAuthoring.SetOuterBoundaryIgnored(candidate.BodyCollider, true);

            // Capture the real falling motion, then animate the short merge ourselves.
            // This avoids collision impulses at the narrow gate while preserving a
            // continuous trajectory into the loop.
            Vector2 mergeStartPosition = candidate.CachedTransform.position;
            Vector2 mergeStartVelocity = candidate.LinearVelocity;
            candidate.SetState(FruitState.MergingToLane);
            candidate.DisablePhysics();
            // Give the actor lane speed at takeoff. The visual merge below then
            // carries that same motion into the path instead of accelerating only
            // after the landing frame.
            candidate.CurrentSpeed = Mathf.Min(speed, maxComfortableLoopSpeed);
            
            active.Add(new LoopFruit
            {
                Fruit = candidate,
                TargetDistance = entryDistance,
                SpeedVariation = 1f + SignedVariation(candidate.GetInstanceID()) * speedVariation,
                MergeElapsed = 0f,
                MergeStartPosition = mergeStartPosition,
                MergeStartVelocity = Vector2.ClampMagnitude(mergeStartVelocity, maxMergeSpeed)
            });
        }

        private int FindBestCandidateIndex()
        {
            int bestIndex = -1;
            float bestScore = float.PositiveInfinity;
            Vector2 gate = loopStart != null ? loopStart.position : GetEntryPosition();
            for (int index = 0; index < congestion.Count; index++)
            {
                FruitActor fruit = congestion[index];
                if (fruit == null) continue;
                Vector2 offset = (Vector2)fruit.CachedTransform.position - gate;
                if (offset.sqrMagnitude > admissionRadius * admissionRadius) continue;
                float score = offset.sqrMagnitude + Mathf.Max(0f, -offset.y) * 0.25f;
                if (score >= bestScore) continue;
                bestScore = score;
                bestIndex = index;
            }
            return bestIndex;
        }

        private static Vector2 Hermite(Vector2 start, Vector2 end, Vector2 startTangent,
            Vector2 endTangent, float time)
        {
            float t2 = time * time;
            float t3 = t2 * time;
            return (2f * t3 - 3f * t2 + 1f) * start
                + (t3 - 2f * t2 + time) * startTangent
                + (-2f * t3 + 3f * t2) * end
                + (t3 - t2) * endTangent;
        }

    }
}
