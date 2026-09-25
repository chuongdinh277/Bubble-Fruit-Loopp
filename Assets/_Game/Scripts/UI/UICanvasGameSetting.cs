using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BubbleFruitLoop.Managers;

namespace BubbleFruitLoop.UI
{
    public sealed class UICanvasGameSetting : UICanvas
    {
        [Header("Toggle artwork")]
        [SerializeField] private Sprite toggleBarSprite;
        [SerializeField] private Sprite toggleFruitSprite;

        private SettingToggle musicToggle;
        private SettingToggle soundToggle;
        private SettingToggle vibrationToggle;

        public override void OnInit()
        {
            base.OnInit();
            Button close = FindButton("BTNClose");
            if (close != null) close.onClick.AddListener(OnClose);

            musicToggle = BuildToggle("IconMusic", SettingKind.Music);
            soundToggle = BuildToggle("IconSound", SettingKind.Sound);
            vibrationToggle = BuildToggle("IconPhone", SettingKind.Vibration);
        }

        public override void OnOpen(object data)
        {
            base.OnOpen(data);
            RefreshFromData();
        }

        private void RefreshFromData()
        {
            DataManager manager = DataManager.Instance;
            if (manager == null) return;
            musicToggle?.SetValueWithoutSave(manager.GetMusic());
            soundToggle?.SetValueWithoutSave(manager.GetSound());
            vibrationToggle?.SetValueWithoutSave(manager.GetVibration());
            ApplyAudioSettings(manager);
        }

        private SettingToggle BuildToggle(string rowName, SettingKind kind)
        {
            Transform row = FindChild(transform, rowName);
            Button button = row != null ? row.GetComponentInChildren<Button>(true) : null;
            if (button == null) return null;
            SettingToggle toggle = button.GetComponent<SettingToggle>();
            if (toggle == null) toggle = button.gameObject.AddComponent<SettingToggle>();
            toggle.Configure(kind, OnSettingChanged, toggleBarSprite, toggleFruitSprite);
            return toggle;
        }

        private static void OnSettingChanged(SettingKind kind, bool value)
        {
            DataManager manager = DataManager.Instance;
            if (manager == null) return;
            switch (kind)
            {
                case SettingKind.Music: manager.SetMusic(value); break;
                case SettingKind.Sound: manager.SetSound(value); break;
                case SettingKind.Vibration: manager.SetVibration(value); break;
            }
            ApplyAudioSettings(manager);
        }

        public static void ApplyAudioSettings(DataManager manager)
        {
            if (manager == null) return;
            AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (AudioSource source in sources)
            {
                bool music = source.loop || source.name.ToLowerInvariant().Contains("music");
                source.mute = music ? !manager.GetMusic() : !manager.GetSound();
            }
        }

        private Button FindButton(string objectName)
        {
            Transform target = FindChild(transform, objectName);
            return target != null ? target.GetComponent<Button>() : null;
        }

        private static Transform FindChild(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = FindChild(root.GetChild(index), objectName);
                if (found != null) return found;
            }
            return null;
        }
    }

    public enum SettingKind { Music, Sound, Vibration }

    [DisallowMultipleComponent]
    public sealed class SettingToggle : MonoBehaviour
    {
        private SettingKind kind;
        private System.Action<SettingKind, bool> changed;
        private Image background;
        private Image knob;
        private TMP_Text label;
        private bool value;

        public void Configure(SettingKind settingKind, System.Action<SettingKind, bool> onChanged,
            Sprite barSprite, Sprite fruitSprite)
        {
            kind = settingKind;
            changed = onChanged;
            BuildVisuals(barSprite, fruitSprite);
            Button button = GetComponent<Button>();
            button.onClick.RemoveListener(Toggle);
            button.onClick.AddListener(Toggle);
        }

        public void SetValueWithoutSave(bool isOn)
        {
            value = isOn;
            Refresh();
        }

        private void Toggle()
        {
            value = !value;
            Refresh();
            changed?.Invoke(kind, value);
        }

        private void BuildVisuals(Sprite barSprite, Sprite fruitSprite)
        {
            Transform existingVisual = transform.Find("SwitchVisual");
            if (existingVisual != null)
            {
                background = existingVisual.GetComponent<Image>();
                knob = existingVisual.Find("FruitKnob")?.GetComponent<Image>()
                    ?? existingVisual.Find("Knob")?.GetComponent<Image>();
                label = GetComponentInChildren<TMP_Text>(true);
                return;
            }

            RectTransform buttonRect = (RectTransform)transform;
            buttonRect.sizeDelta = new Vector2(410f, 104f);
            Image hitArea = GetComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0.001f);
            GameObject root = new("SwitchVisual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Transform visual = root.transform;
            visual.SetParent(transform, false);
            RectTransform rect = (RectTransform)visual;
            // The source bar PNG contains generous transparent padding above and
            // below the painted pill. Give the artwork extra visual height without
            // enlarging the clickable row enough to overlap adjacent toggles.
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(410f, 156f);
            background = visual.GetComponent<Image>();
            background.sprite = barSprite != null
                ? barSprite
                : Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            background.type = barSprite != null ? Image.Type.Simple : Image.Type.Sliced;
            background.preserveAspect = false;
            background.raycastTarget = false;

            Transform knobTransform = visual.Find("Knob");
            if (knobTransform == null)
            {
                GameObject knobObject = new("Knob", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                knobTransform = knobObject.transform;
                knobTransform.SetParent(visual, false);
            }
            RectTransform knobRect = (RectTransform)knobTransform;
            knobRect.anchorMin = knobRect.anchorMax = new Vector2(0.5f, 0.5f);
            knobRect.sizeDelta = new Vector2(112f, 112f);
            knob = knobTransform.GetComponent<Image>();
            knob.sprite = fruitSprite != null
                ? fruitSprite
                : Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            knob.preserveAspect = true;
            knob.raycastTarget = false;

            label = GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            label.rectTransform.SetAsLastSibling();
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(34f, 0f);
            label.rectTransform.offsetMax = new Vector2(-105f, 0f);
            label.rectTransform.localScale = Vector3.one;
            label.fontSize = 34f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = Color.white;
            label.outlineWidth = 0.18f;
            label.outlineColor = new Color32(71, 58, 24, 210);
        }

        private void Refresh()
        {
            if (background != null) background.color = value ? Color.white : new Color(1f, 0.42f, 0.38f, 1f);
            if (label != null) label.text = value ? "ON" : "OFF";
            if (knob == null) return;
            knob.color = value ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
            knob.rectTransform.anchoredPosition = new Vector2(value ? 166f : -166f, 0f);
        }
    }
}
