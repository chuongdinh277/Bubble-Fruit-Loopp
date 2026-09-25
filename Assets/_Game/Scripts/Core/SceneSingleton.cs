using UnityEngine;

namespace BubbleFruitLoop.Core
{
    /// <summary>Explicit singleton registration without any scene search.</summary>
    public abstract class SceneSingleton<T> : MonoBehaviour where T : SceneSingleton<T>
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = (T)this;
            OnSingletonReady();
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        protected abstract void OnSingletonReady();
    }
}
