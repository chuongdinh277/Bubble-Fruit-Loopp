using UnityEngine;

namespace BubbleFruitLoop.UI
{
    public class UICanvas : MonoBehaviour
    {
        public bool IsSetup { get; private set; }

        public void Setup()
        {
            if (IsSetup) return;
            IsSetup = true;
            OnInit();
        }

        // Called once when the UI is first instantiated
        public virtual void OnInit()
        {
        }

        // Called every time the UI is opened
        public virtual void OnOpen(object data)
        {
            gameObject.SetActive(true);
        }

        // Called when the UI is closed
        public virtual void OnClose()
        {
            gameObject.SetActive(false);
        }
    }
}
