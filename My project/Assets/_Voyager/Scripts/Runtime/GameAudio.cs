using System;
using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// Volúmenes del juego (0 a 1), guardados en PlayerPrefs para que se recuerden entre partidas.
    ///   Master  → volumen general (AudioListener).
    ///   Music / Effects → los usan los AudioSource que tengan el componente AudioVolume.
    /// </summary>
    public static class GameAudio
    {
        const string MasterKey = "voyager.volume.master";
        const string EffectsKey = "voyager.volume.effects";
        const string MusicKey = "voyager.volume.music";

        public const float DefaultMaster = 0.7f, DefaultEffects = 0.9f, DefaultMusic = 0.6f;

        public static event Action OnChanged;

        public static float Master
        {
            get => PlayerPrefs.GetFloat(MasterKey, DefaultMaster);
            set { PlayerPrefs.SetFloat(MasterKey, Mathf.Clamp01(value)); Apply(); }
        }

        public static float Effects
        {
            get => PlayerPrefs.GetFloat(EffectsKey, DefaultEffects);
            set { PlayerPrefs.SetFloat(EffectsKey, Mathf.Clamp01(value)); Apply(); }
        }

        public static float Music
        {
            get => PlayerPrefs.GetFloat(MusicKey, DefaultMusic);
            set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); Apply(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init() => AudioListener.volume = Master;

        static void Apply()
        {
            AudioListener.volume = Master;
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }
}
