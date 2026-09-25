using UnityEngine;
using BubbleFruitLoop.Core;
using BubbleFruitLoop.Data;

namespace BubbleFruitLoop.Managers
{
    [DefaultExecutionOrder(-3000)]
    public class DataManager : SceneSingleton<DataManager>
    {
        private const string DATA_KEY = "BubbleFruitLoop_PlayerData";
        
        [SerializeField] private PlayerData data;

        protected override void OnSingletonReady()
        {
            LoadData();
        }

        private void LoadData()
        {
            if (PlayerPrefs.HasKey(DATA_KEY))
            {
                string json = PlayerPrefs.GetString(DATA_KEY);
                data = JsonUtility.FromJson<PlayerData>(json);
            }
            else
            {
                data = new PlayerData();
            }
            
            data.Validate();
            // Prototype currently cycles only the two authored test levels.
            data.level = data.level == 2 ? 2 : 1;
            SaveData();
        }

        public void SaveData()
        {
            if (data == null) return;
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(DATA_KEY, json);
            PlayerPrefs.Save();
        }

        // --- Data Accessors ---

        public int GetLevel() => data.level;
        public void SetLevel(int level)
        {
            data.level = level;
            SaveData();
        }
        
        public void AddLevel(int amount)
        {
            data.level += amount;
            SaveData();
        }

        public int GetCoin() => data.coin;
        public void SetCoin(int coin)
        {
            data.coin = coin;
            SaveData();
        }

        public void AddCoin(int amount)
        {
            data.coin += amount;
            SaveData();
        }

        public bool GetMusic() => data.isMusicOn;
        public void SetMusic(bool isOn)
        {
            data.isMusicOn = isOn;
            SaveData();
        }

        public bool GetSound() => data.isSoundOn;
        public void SetSound(bool isOn)
        {
            data.isSoundOn = isOn;
            SaveData();
        }

        public bool GetVibration() => data.isVibrationOn;
        public void SetVibration(bool isOn)
        {
            data.isVibrationOn = isOn;
            SaveData();
        }
    }
}
