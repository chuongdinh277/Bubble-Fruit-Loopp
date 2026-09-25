namespace BubbleFruitLoop.Gameplay
{
    public enum FruitType { Apple, Orange, Grape, Lemon, Strawberry }
    public enum FruitState
    {
        Pooled, InsideBubble, Released, Jammed, IntakeWaiting, EnteringLoop, Transient,
        StableOnLoop, Reserved, Collecting,
        EntryCongestion, Admitted, MergingToLane, OnLoop, WaitingFull
    }
    public enum BoxState { Waiting, Active, Receiving, Full, Completed }
    public enum GameResult { Playing, Won, Lost }
}

