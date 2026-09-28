namespace BubbleFruitLoop.Gameplay
{
    public enum FruitType { Apple, Orange, Grape, Lemon, Strawberry }
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BubbleFruitLoop.Gameplay", "Assembly-CSharp", "FruitState")]
    public enum FruitStatus
    {
        Pooled, InsideBubble, Released, Jammed, IntakeWaiting, EnteringLoop, Transient,
        StableOnLoop, Reserved, Collecting,
        EntryCongestion, Admitted, MergingToLane, OnLoop, WaitingFull
    }
    public enum BoxState { Waiting, Active, Receiving, Full, Completed }
    public enum GameResult { Playing, Won, Lost }
}

