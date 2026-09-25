using UnityEngine;
using BubbleFruitLoop.Core;
using BubbleFruitLoop.UI;

namespace BubbleFruitLoop.Managers
{
    public enum GameState
    {
        MainMenu,
        Gameplay,
        Pause,
        GameOver
    }

    public class GameManager : SceneSingleton<GameManager>
    {
        public GameState CurrentState { get; private set; }

        protected override void OnSingletonReady()
        {
            // Do nothing on awake to avoid missing UIManager.Instance
        }

        private void Start()
        {
            UICanvasGameSetting.ApplyAudioSettings(DataManager.Instance);
            // Start game directly in gameplay for this prototype
            ChangeState(GameState.Gameplay);
        }

        public void ChangeState(GameState newState)
        {
            CurrentState = newState;
            
            // Handle state transitions
            switch (newState)
            {
                case GameState.MainMenu:
                    // if (UIManager.Instance != null) { ... }
                    break;
                case GameState.Gameplay:
                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.CloseAll();
                        UIManager.Instance.OpenUI<UICanvasGameplay>();
                    }
                    break;
                case GameState.Pause:
                    break;
                case GameState.GameOver:
                    break;
            }
        }
    }
}
