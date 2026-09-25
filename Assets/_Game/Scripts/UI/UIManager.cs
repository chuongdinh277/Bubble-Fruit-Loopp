using System.Collections.Generic;
using UnityEngine;
using BubbleFruitLoop.Core;

namespace BubbleFruitLoop.UI
{
    public class UIManager : SceneSingleton<UIManager>
    {
        private Dictionary<System.Type, UICanvas> uiCanvases = new Dictionary<System.Type, UICanvas>();
        
        [Header("Root for UI")]
        public Transform CanvasParent; 

        protected override void OnSingletonReady()
        {
            // Initialize if needed
        }

        public T OpenUI<T>(object data = null) where T : UICanvas
        {
            UICanvas canvas = GetUI<T>();
            if (canvas != null)
            {
                canvas.Setup();
                canvas.OnOpen(data);
            }
            return canvas as T;
        }

        public void CloseUI<T>() where T : UICanvas
        {
            if (IsLoaded<T>())
            {
                uiCanvases[typeof(T)].OnClose();
            }
        }

        public void CloseAll()
        {
            foreach (var canvas in uiCanvases.Values)
            {
                if (canvas != null && canvas.gameObject.activeSelf)
                {
                    canvas.OnClose();
                }
            }
        }

        public bool IsLoaded<T>() where T : UICanvas
        {
            return uiCanvases.ContainsKey(typeof(T)) && uiCanvases[typeof(T)] != null;
        }

        public T GetUI<T>() where T : UICanvas
        {
            if (!IsLoaded<T>())
            {
                // Must ensure prefabs are in Resources/UI folder and have the exact script name
                T prefab = Resources.Load<T>($"UI/{typeof(T).Name}");
                if (prefab == null)
                {
                    Debug.LogError($"Cannot find prefab UI/{typeof(T).Name} in Resources!");
                    return null;
                }
                
                Transform parent = CanvasParent != null ? CanvasParent : transform;
                T instance = Instantiate(prefab, parent);
                uiCanvases[typeof(T)] = instance;
            }
            return uiCanvases[typeof(T)] as T;
        }
    }
}
