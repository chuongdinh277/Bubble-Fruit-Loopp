using System;
namespace BubbleFruitLoop.Gameplay
{
    public sealed class FruitState
    {
        public readonly FruitStatus Status;
        public readonly Action<Fruit> OnEnter;
        public readonly Action<Fruit> OnExecute;
        public readonly Action<Fruit> OnExit;
        public FruitState(FruitStatus status, Action<Fruit> onEnter, Action<Fruit> onExecute, Action<Fruit> onExit)
        {
            Status = status;
            OnEnter = onEnter;
            OnExecute = onExecute;
            OnExit = onExit;
        }
    }
    public static class FruitStates
    {
        // Docked fruit used Collecting in the original enum. Keep that numeric
        // status for existing callers/assets while exposing a distinct FSM state.
        public static readonly FruitState InBox = new(FruitStatus.Collecting, InBoxState.OnEnter, InBoxState.OnExecute, InBoxState.OnExit);
        public static readonly FruitState Pooled = new(FruitStatus.Pooled, PooledState.OnEnter, PooledState.OnExecute, PooledState.OnExit);
        public static readonly FruitState InsideBubble = new(FruitStatus.InsideBubble, InsideBubbleState.OnEnter, InsideBubbleState.OnExecute, InsideBubbleState.OnExit);
        public static readonly FruitState Released = new(FruitStatus.Released, ReleasedState.OnEnter, ReleasedState.OnExecute, ReleasedState.OnExit);
        public static readonly FruitState Jammed = new(FruitStatus.Jammed, JammedState.OnEnter, JammedState.OnExecute, JammedState.OnExit);
        public static readonly FruitState IntakeWaiting = new(FruitStatus.IntakeWaiting, IntakeWaitingState.OnEnter, IntakeWaitingState.OnExecute, IntakeWaitingState.OnExit);
        public static readonly FruitState EnteringLoop = new(FruitStatus.EnteringLoop, EnteringLoopState.OnEnter, EnteringLoopState.OnExecute, EnteringLoopState.OnExit);
        public static readonly FruitState Transient = new(FruitStatus.Transient, TransientState.OnEnter, TransientState.OnExecute, TransientState.OnExit);
        public static readonly FruitState StableOnLoop = new(FruitStatus.StableOnLoop, StableOnLoopState.OnEnter, StableOnLoopState.OnExecute, StableOnLoopState.OnExit);
        public static readonly FruitState Reserved = new(FruitStatus.Reserved, ReservedState.OnEnter, ReservedState.OnExecute, ReservedState.OnExit);
        public static readonly FruitState Collecting = new(FruitStatus.Collecting, CollectingState.OnEnter, CollectingState.OnExecute, CollectingState.OnExit);
        public static readonly FruitState EntryCongestion = new(FruitStatus.EntryCongestion, EntryCongestionState.OnEnter, EntryCongestionState.OnExecute, EntryCongestionState.OnExit);
        public static readonly FruitState Admitted = new(FruitStatus.Admitted, AdmittedState.OnEnter, AdmittedState.OnExecute, AdmittedState.OnExit);
        public static readonly FruitState MergingToLane = new(FruitStatus.MergingToLane, MergingToLaneState.OnEnter, MergingToLaneState.OnExecute, MergingToLaneState.OnExit);
        public static readonly FruitState OnLoop = new(FruitStatus.OnLoop, OnLoopState.OnEnter, OnLoopState.OnExecute, OnLoopState.OnExit);
        public static readonly FruitState WaitingFull = new(FruitStatus.WaitingFull, WaitingFullState.OnEnter, WaitingFullState.OnExecute, WaitingFullState.OnExit);
        public static FruitState For(FruitStatus status) => status switch
        {
            FruitStatus.Pooled => Pooled,
            FruitStatus.InsideBubble => InsideBubble,
            FruitStatus.Released => Released,
            FruitStatus.Jammed => Jammed,
            FruitStatus.IntakeWaiting => IntakeWaiting,
            FruitStatus.EnteringLoop => EnteringLoop,
            FruitStatus.Transient => Transient,
            FruitStatus.StableOnLoop => StableOnLoop,
            FruitStatus.Reserved => Reserved,
            FruitStatus.Collecting => Collecting,
            FruitStatus.EntryCongestion => EntryCongestion,
            FruitStatus.Admitted => Admitted,
            FruitStatus.MergingToLane => MergingToLane,
            FruitStatus.OnLoop => OnLoop,
            FruitStatus.WaitingFull => WaitingFull,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }
}
