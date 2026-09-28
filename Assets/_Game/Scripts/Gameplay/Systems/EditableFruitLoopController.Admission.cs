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
            if (!HasFinishedIntakeFlight(candidate)) return;
            congestion.RemoveAt(candidateIndex);
            intakeFlights.Remove(candidate);
            owned.Add(candidate);
            pathAuthoring.SetOuterBoundaryIgnored(candidate.BodyCollider, true);

            // Land at P00 first, then glide a short distance to the right while
            // physics is still disabled. Real contacts begin only after this merge.
            Vector2 mergeStartPosition = candidate.CachedTransform.position;
            float laneSpeed = Mathf.Min(speed, maxComfortableLoopSpeed);
            candidate.CurrentSpeed = laneSpeed;
            candidate.SetState(FruitState.MergingToLane);
            candidate.DisablePhysics();
            float mergeTargetDistance = Mathf.Repeat(
                entryDistance + laneMergeAdvance, path.Length);
            
            active.Add(new LoopFruit
            {
                Fruit = candidate,
                MergeStartPosition = mergeStartPosition,
                MergeElapsed = 0f,
                MergeTargetDistance = mergeTargetDistance
            });
        }

        private bool HasFinishedIntakeFlight(FruitActor fruit) =>
            intakeFlights.TryGetValue(fruit, out IntakeFlight flight)
            && flight.Elapsed >= flight.Duration;

        private int FindBestCandidateIndex()
        {
            int bestIndex = -1;
            float bestScore = float.PositiveInfinity;
            Vector2 gate = GetEntryPosition();
            for (int index = 0; index < congestion.Count; index++)
            {
                FruitActor fruit = congestion[index];
                if (fruit == null) continue;
                if (!HasFinishedIntakeFlight(fruit)) continue;
                Vector2 offset = (Vector2)fruit.CachedTransform.position - gate;
                if (offset.sqrMagnitude > admissionRadius * admissionRadius) continue;
                float score = offset.sqrMagnitude + Mathf.Max(0f, -offset.y) * 0.25f;
                if (score >= bestScore) continue;
                bestScore = score;
                bestIndex = index;
            }
            return bestIndex;
        }

    }
}
