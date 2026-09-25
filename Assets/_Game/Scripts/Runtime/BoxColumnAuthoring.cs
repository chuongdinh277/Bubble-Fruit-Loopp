using BubbleFruitLoop.Gameplay;
using UnityEngine;

namespace BubbleFruitLoop.Runtime
{
    public sealed class BoxColumnAuthoring : MonoBehaviour
    {
        [SerializeField] private Transform activePoint;
        [SerializeField] private Transform pickupPoint;
        [SerializeField] private BoxView[] boxes;

        public Transform ActivePoint => activePoint;
        public Transform PickupPoint => pickupPoint;
        public BoxView[] Boxes => boxes;

        public void Configure(Transform active, Transform pickup, BoxView[] orderedBoxes)
        {
            activePoint = active;
            pickupPoint = pickup;
            boxes = orderedBoxes;
        }
    }
}
