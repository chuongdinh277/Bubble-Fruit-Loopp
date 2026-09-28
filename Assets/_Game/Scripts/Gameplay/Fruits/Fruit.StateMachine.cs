using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class Fruit
    {
        private FruitState currentState;
        private bool applyStateConfiguration;
        internal FruitExecutionPhase ExecutionPhase;
        internal FruitLoopManager LoopOwner;
        internal BubbleManager BubbleOwner;
        internal int BubbleIndex;
        internal float BubbleDeltaTime;
        internal float BubbleTime;
        internal FruitLoopData LoopData;
        internal FruitIntakeData IntakeData;

        public FruitState CurrentState => currentState ?? FruitStates.For(state);

        public void ChangeFruitStateTo(FruitState nextState, bool configurePhysics = true)
        {
            currentState?.OnExit(this);
            currentState = nextState;
            state = nextState.Status;
            applyStateConfiguration = configurePhysics;
            currentState.OnEnter(this);
            applyStateConfiguration = false;
        }

        internal void ApplyRequestedStateConfiguration()
        {
            if (!applyStateConfiguration) return;
            ConfigureStatePhysics(state);
            ConfigureStateCollider(state);
        }

        internal void ExecuteState(FruitExecutionPhase phase)
        {
            ExecutionPhase = phase;
            CurrentState.OnExecute(this);
        }
    }
}
