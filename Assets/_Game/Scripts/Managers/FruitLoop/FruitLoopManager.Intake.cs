using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class FruitLoopManager
    {
        private int FindNearestUnlaunchedCandidate()
        {
            int bestIndex = -1;
            float bestScore = float.PositiveInfinity;
            Vector2 entry = GetWaterfallStartPosition();
            for (int index = 0; index < congestion.Count; index++)
            {
                Fruit fruit = congestion[index];
                if (fruit == null || intakeFlights.ContainsKey(fruit))
                    continue;
                Vector2 offset = (Vector2)fruit.CachedTransform.position - entry;
                if (!IsInsideEntryZone(fruit.CachedTransform.position))
                    continue;
                if (Mathf.Abs(offset.x) > entryZoneWidth * 0.4f)
                    continue;
                float score = offset.sqrMagnitude + Mathf.Max(0f, -offset.y) * 0.25f;
                if (score >= bestScore)
                    continue;
                bestScore = score;
                bestIndex = index;
            }

            return bestIndex;
        }

        private FruitIntakeData CreateIntakeFlight(Fruit fruit)
        {
            Vector2 start = fruit.CachedTransform.position;
            Vector2 landing = GetWaterfallStartPosition();
            float travelTime = Mathf.Max(intakeFallSmoothDuration, Vector2.Distance(start, landing) * 1.5f / Mathf.Max(0.1f, intakeFlightSpeed));
            // Do not extend flights to wait for earlier arrivals or a lane gap.
            intakeLaunchTimer = intakeLaunchInterval;
            return new FruitIntakeData
            {
                StartPosition = start,
                LandingPosition = landing,
                StartVelocity = fruit.LinearVelocity,
                ArrivalVelocity = EnteringLoopState.GetWaterfallArrivalVelocity(start, landing, travelTime, Mathf.Min(speed, maxComfortableLoopSpeed)),
                Elapsed = -Time.deltaTime,
                Duration = travelTime
            };
        }

        private Vector3 GetWaterfallStartPosition() => waterfallStartPoint != null ? waterfallStartPoint.position : GetEntryPosition();
        private bool IsInsideEntryZone(Vector3 position)
        {
            Vector2 center = GetWaterfallStartPosition();
            Vector2 offset = (Vector2)position - center;
            return Mathf.Abs(offset.x) <= entryZoneWidth * 0.5f && offset.y <= entryZoneHeight * 0.65f && offset.y >= -entryZoneHeight;
        }

        private void RemoveFinishedCongestion()
        {
            for (int index = congestion.Count - 1; index >= 0; index--)
            {
                Fruit fruit = congestion[index];
                if (fruit == null || owned.Contains(fruit))
                {
                    intakeFlights.Remove(fruit);
                    congestion.RemoveAt(index);
                    continue;
                }

                if (intakeFlights.ContainsKey(fruit))
                    continue;
                if (IsInsideEntryZone(fruit.CachedTransform.position))
                    continue;
                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, false);
                congestion.RemoveAt(index);
            }
        }

        private void FindNewCongestionFruits()
        {
            Fruit[] fruits = FindObjectsByType<Fruit>(FindObjectsSortMode.None);
            for (int index = 0; index < fruits.Length; index++)
            {
                Fruit fruit = fruits[index];
                if (fruit == null || owned.Contains(fruit) || congestion.Contains(fruit))
                    continue;
                if (fruit.State != FruitStatus.Released && fruit.State != FruitStatus.EntryCongestion && fruit.State != FruitStatus.WaitingFull && fruit.State != FruitStatus.IntakeWaiting && fruit.State != FruitStatus.EnteringLoop && fruit.State != FruitStatus.Transient)
                    continue;
                if (!IsInsideEntryZone(fruit.CachedTransform.position))
                    continue;
                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, false);
                congestion.Add(fruit);
            }
        }

        private void IgnoreCongestionContacts()
        {
            for (int index = 0; index < congestion.Count; index++)
            {
                Fruit fruit = congestion[index];
                if (fruit == null || intakeFlights.ContainsKey(fruit))
                    continue;
                if (fruit.State != FruitStatus.WaitingFull)
                    fruit.EnterCongestion(true);
                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, true);
            }
        }

        private void SteerCongestionFruits()
        {
            for (int index = 0; index < congestion.Count; index++)
            {
                Fruit fruit = congestion[index];
                if (fruit == null || intakeFlights.ContainsKey(fruit))
                    continue;
                if (fruit.State == FruitStatus.WaitingFull)
                    fruit.EnterCongestion(false);
                pathAuthoring.SetOuterBoundaryIgnored(fruit.BodyCollider, false);
            }
        }
    }
}
