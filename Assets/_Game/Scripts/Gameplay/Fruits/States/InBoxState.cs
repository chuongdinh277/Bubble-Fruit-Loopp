namespace BubbleFruitLoop.Gameplay
{
    public static class InBoxState
    {
        public static void OnEnter(Fruit fruit) => fruit.ApplyRequestedStateConfiguration();
        public static void OnExecute(Fruit fruit) { }
        public static void OnExit(Fruit fruit) { }
    }
}
