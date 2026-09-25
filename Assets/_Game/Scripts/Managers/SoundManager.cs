using UnityEngine;
using BubbleFruitLoop.Core;

namespace BubbleFruitLoop.Managers
{
    public enum BgmID
    {
        Menu = 0,
        GamePlay = 1,
    }

    public enum FxID
    {
        ButtonClick = 0,
        BubblePop = 1,
        FruitDrop = 2,
        FruitMerge = 3,
        FruitCollect = 4,
        BoxComplete = 5,
        Win = 6,
        Lose = 7,
    }

    public class SoundManager : SceneSingleton<SoundManager>
    {
        private AudioSource bgmSource;
        private AudioSource fxSource;
        private readonly float[] lastFxPlayTimes = new float[8];

        [Header("BGM")]
        [SerializeField] private AudioClip[] bgmClips;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.55f;
        [SerializeField] private bool playBgmOnStart = true;
        [SerializeField] private BgmID startupBgm = BgmID.GamePlay;

        [Header("Sound FX")]
        [SerializeField] private AudioClip[] fxClips;
        [SerializeField, Range(0f, 1f)] private float fxVolume = 0.9f;

        private bool isLoaded = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallWhenMissing()
        {
            if (FindFirstObjectByType<SoundManager>() != null) return;
            new GameObject("SoundManager").AddComponent<SoundManager>();
        }

        protected override void OnSingletonReady()
        {
            EnsureDefaultClips();
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.spatialBlend = 0f;
            bgmSource.volume = bgmVolume;

            fxSource = gameObject.AddComponent<AudioSource>();
            fxSource.loop = false;
            fxSource.playOnAwake = false;
            fxSource.spatialBlend = 0f;
            fxSource.volume = fxVolume;
        }

        private void Start()
        {
            Invoke(nameof(OnLoad), 0.5f);
        }

        private void OnLoad()
        {
            isLoaded = true;
            if (playBgmOnStart) PlayBGM(startupBgm);
        }

        public void PlayBGM(BgmID id)
        {
            int index = (int)id;
            if (bgmClips == null || index < 0 || index >= bgmClips.Length || bgmClips[index] == null)
            {
                bgmSource.Stop();
                return;
            }
            bgmSource.clip = bgmClips[index];
            if (!IsMusicOn()) 
            { 
                bgmSource.Stop(); 
                return; 
            }
            
            if (bgmSource.clip == bgmClips[index] && bgmSource.isPlaying) return;
            bgmSource.Play();
        }

        public void StopBGM()
        {
            bgmSource.Stop();
        }

        public void PlayFX(FxID id)
        {
            int index = (int)id;
            float cooldown = id is FxID.FruitDrop or FxID.FruitCollect ? 0.045f : 0.02f;
            if (index >= 0 && index < lastFxPlayTimes.Length
                && Time.unscaledTime - lastFxPlayTimes[index] < cooldown) return;
            if (index >= 0 && index < lastFxPlayTimes.Length)
                lastFxPlayTimes[index] = Time.unscaledTime;
            PlayClip(fxClips, index);
        }

        private void PlayClip(AudioClip[] clips, int index)
        {
            if (!isLoaded || !IsSoundOn()) return; 
            if (clips == null || index < 0 || index >= clips.Length || clips[index] == null) return;

            if (fxSource == null) return;
            fxSource.PlayOneShot(clips[index]);
        }

        public static void TryPlayFX(FxID id)
        {
            if (Instance != null) Instance.PlayFX(id);
        }

        private void EnsureDefaultClips()
        {
            AudioClip music = Resources.Load<AudioClip>("Sound/music");
            AudioClip pickup = Resources.Load<AudioClip>("Sound/PickUp");
            AudioClip booster = Resources.Load<AudioClip>("Sound/booster");

            if (bgmClips == null || bgmClips.Length < 2)
                bgmClips = new[] { music, music };
            if (fxClips != null && fxClips.Length >= 8) return;
            fxClips = new[]
            {
                pickup, pickup, pickup, pickup, pickup,
                booster, booster, booster
            };
        }

        private bool IsMusicOn()
        {
            return DataManager.Instance != null && DataManager.Instance.GetMusic();
        }

        private bool IsSoundOn()
        {
            return DataManager.Instance != null && DataManager.Instance.GetSound();
        }

        public void OnSettingsChanged()
        {
            if (bgmSource == null) return;
            bgmSource.mute = !IsMusicOn();
            if (fxSource != null) fxSource.mute = !IsSoundOn();
            if (!IsMusicOn())
            {
                StopBGM();
            }
            else if (!bgmSource.isPlaying && bgmSource.clip != null)
            {
                bgmSource.Play();
            }
        }
    }
}
