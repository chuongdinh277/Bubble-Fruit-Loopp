using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class BubbleActor
    {
        private void Start()
        {
            // Stable per-bubble offset keeps the board moving organically instead of
            // making every bubble sway in exactly the same direction at once.
            idlePhase = Mathf.Repeat(transform.position.x * 0.83f + transform.position.y * 1.37f, Mathf.PI * 2f);

            // The bubbles should drift through a viscous medium, not rebound like
            // rubber balls. Apply a safe floor here so older serialized scenes also
            // receive the softer motion without needing to be rebuilt.
            if (physicsBody != null)
            {
                // Keep the shell floaty, while making its fall responsive enough
                // that contact deformation reads as a soft landing instead of a stop.
                physicsBody.gravityScale = Mathf.Clamp(physicsBody.gravityScale, 0.38f, 0.42f);
                idleBuoyancy = Mathf.Min(idleBuoyancy, 0.54f);
                idleBuoyancyPulse = Mathf.Min(idleBuoyancyPulse, 0.13f);
                physicsBody.linearDamping = Mathf.Clamp(physicsBody.linearDamping, 0.5f, 0.8f);
                physicsBody.angularDamping = Mathf.Clamp(physicsBody.angularDamping, 1.0f, 1.6f);
            }

            if (visualRoot == null && visualRenderers != null && visualRenderers.Length > 0 && visualRenderers[0] != null)
            {
                // Fallback: If no visual root assigned, but we have multiple renderers under a parent, use the parent
                if (visualRenderers.Length > 1 && visualRenderers[0].transform.parent != transform)
                    visualRoot = visualRenderers[0].transform.parent;
                else
                    visualRoot = visualRenderers[0].transform;
            }

            if (visualRoot != null)
            {
                originalVisualScale = visualRoot.localScale;
            }
            CacheDeformMeshes();
            
            popped = false;
            if (fruits.Count == 0)
            {
                fruits.AddRange(GetComponentsInChildren<FruitActor>(true));
            }

            if (!popped && fruitMotion != null) fruitMotion.StartMotion();
            
            if (innerBoundary is EdgeCollider2D edge && edge.edgeRadius < 0.05f)
            {
                edge.edgeRadius = 0.1f;
            }

            IsolateFromOtherFruits();
        }

    }
}
