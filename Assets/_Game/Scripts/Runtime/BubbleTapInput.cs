using BubbleFruitLoop.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BubbleFruitLoop.Runtime
{
    public sealed class BubbleTapInput : MonoBehaviour
    {
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private BubbleActor bubble;
        [SerializeField] private Collider2D tapArea;

        public void Configure(Camera cameraReference, BubbleActor target, Collider2D inputArea)
        {
            gameplayCamera = cameraReference;
            bubble = target;
            tapArea = inputArea;
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame || bubble.IsPopped) return;
            Vector3 screen = pointer.position.ReadValue();
            Vector2 world = gameplayCamera.ScreenToWorldPoint(screen);
            if (tapArea.OverlapPoint(world)) bubble.Pop();
        }
    }
}
