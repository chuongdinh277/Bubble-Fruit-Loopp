namespace BubbleFruitLoop.Gameplay
{
    public enum FruitExecutionPhase { Recovery, InsideBubble, Intake, Merge, LoopAdvance, LoopPose }

    public static class FruitManager
    {
        internal static void ExecuteLoop(Fruit fruit, FruitLoopManager loop, FruitExecutionPhase phase)
        {
            fruit.LoopOwner = loop;
            fruit.ExecuteState(phase);
        }

        internal static void ExecuteBubble(Fruit fruit, BubbleManager bubble, int index, float dt, float time)
        {
            fruit.BubbleOwner = bubble;
            fruit.BubbleIndex = index;
            fruit.BubbleDeltaTime = dt;
            fruit.BubbleTime = time;
            fruit.ExecutionPhase = FruitExecutionPhase.InsideBubble;
            // The original bubble motion iterates its configured array, not a
            // status-filtered registry. Keep that exact scheduling contract.
            FruitStates.InsideBubble.OnExecute(fruit);
        }

        internal static void ExecuteRecovery(Fruit fruit)
        {
            fruit.ExecutionPhase = FruitExecutionPhase.Recovery;
            // Preserve recovery's original guards, including legacy Jammed.
            FruitStates.Released.OnExecute(fruit);
        }
    }
}
