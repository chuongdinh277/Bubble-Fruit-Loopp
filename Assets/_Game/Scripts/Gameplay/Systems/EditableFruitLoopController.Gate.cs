using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class EditableFruitLoopController
    {
        private void EnsureFullGate()
        {
            if (!Application.isPlaying || fullGateCollider != null) return;
            Transform existing = transform.Find("Full Loop Intake Gate");
            GameObject gate = existing != null
                ? existing.gameObject
                : new GameObject("Full Loop Intake Gate");
            if (existing == null) gate.transform.SetParent(transform, true);
            fullGateCollider = gate.GetComponent<BoxCollider2D>();
            if (fullGateCollider == null) fullGateCollider = gate.AddComponent<BoxCollider2D>();
            fullGateCollider.isTrigger = false;
            fullGateCollider.enabled = false;
            UpdateFullGateTransform();
        }

        private void UpdateFullGate()
        {
            EnsureFullGate();
            if (fullGateCollider == null) return;
            UpdateFullGateTransform();
            fullGateCollider.enabled = IsFull;
        }

        private void UpdateFullGateTransform()
        {
            if (fullGateCollider == null) return;
            Vector3 entry = loopStart != null ? loopStart.position : GetEntryPosition();
            fullGateCollider.transform.position = entry + Vector3.up * fullGateYOffset;
            fullGateCollider.transform.rotation = Quaternion.identity;
            fullGateCollider.transform.localScale = Vector3.one;
            fullGateCollider.size = new Vector2(fullGateWidth, fullGateHeight);
        }


    }
}
