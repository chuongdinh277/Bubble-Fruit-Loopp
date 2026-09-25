namespace BubbleFruitLoop.Pooling
{
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }
}
