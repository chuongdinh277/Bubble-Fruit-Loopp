using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class FruitLoopManager
    {
        private float CircularSeparation(float first, float second)
        {
            return Mathf.Abs(Mathf.DeltaAngle(first / path.Length * 360f, second / path.Length * 360f)) / 360f * path.Length;
        }

        private void RebuildPath()
        {
            if (pathAuthoring == null)
                pathAuthoring = GetComponent<LoopPathAuthoring>();
            if (loopStart == null)
            {
                GameObject startObject = GameObject.Find("LoopStart");
                if (startObject != null)
                    loopStart = startObject.transform;
            }

            if (waterfallStartPoint == null)
            {
                GameObject startObject = GameObject.Find("Startpoint");
                if (startObject != null)
                    waterfallStartPoint = startObject.transform;
            }

            path = pathAuthoring != null ? pathAuthoring.BuildPath() : null;
            if (path != null)
                entryDistance = path.EntryDistance;
        }

        private Vector3 GetEntryPosition()
        {
            if (path == null)
                return loopStart != null ? loopStart.position : transform.position;
            path.Evaluate(entryDistance, out Vector3 position, out _);
            return position;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = GetWaterfallStartPosition();
            Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.35f);
            Gizmos.DrawCube(center + Vector3.down * entryZoneHeight * 0.175f, new Vector3(entryZoneWidth, entryZoneHeight * 1.65f, 0.05f));
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.75f);
            Gizmos.DrawWireSphere(GetWaterfallStartPosition(), admissionRadius);
        }
    }
}
