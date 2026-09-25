namespace BubbleFruitLoop.Core
{
    public sealed class GameClock
    {
        public float DeltaTime { get; private set; }
        public float FixedDeltaTime { get; private set; }

        public void SetFrame(float deltaTime) => DeltaTime = deltaTime;
        public void SetPhysicsFrame(float fixedDeltaTime) => FixedDeltaTime = fixedDeltaTime;
    }
}
