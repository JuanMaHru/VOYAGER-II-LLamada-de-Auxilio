using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Voyager.UI;

namespace Voyager
{
    /// <summary>
    /// Reproduce los sonidos del SoundBank sin configurar nada en las escenas:
    ///   · Crea solo un objeto invisible que dura todo el juego (el sonido no se corta al cambiar de escena).
    ///   · En cada escena engancha todos los botones (rollover y click) y el inicio/fin de cada día.
    ///   · Los sellos APROBAR / DESAPROBAR suenan con su propio sonido en vez del click.
    /// Usa el volumen de efectos de Opciones → Sonido.
    /// </summary>
    public static class GameSounds
    {
        public enum Sound { Approve, Reject, DayStart, DayEnd, Click, Rollover }

        static SoundBank bank;
        static AudioSource source;

        /// <summary>Último sonido reproducido (útil para probar).</summary>
        public static string LastPlayed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            bank = Resources.Load<SoundBank>("SoundBank");
            if (bank == null)
            {
                Debug.LogWarning("[Voyager] No hay SoundBank en Resources: ejecutá Voyager → Conectar sonidos.");
                return;
            }

            var host = new GameObject("GameSounds");
            Object.DontDestroyOnLoad(host);
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = 1f;
            host.AddComponent<AudioVolume>().channel = AudioVolume.Channel.Effects;

            SceneManager.sceneLoaded -= OnSceneLoaded; // por si el editor no recarga el dominio entre Plays
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public static void Play(Sound sound)
        {
            if (bank == null || source == null) return;
            AudioClip clip; float volume;
            switch (sound)
            {
                case Sound.Approve:  clip = bank.approve;  volume = bank.approveVolume;  break;
                case Sound.Reject:   clip = bank.reject;   volume = bank.rejectVolume;   break;
                case Sound.DayStart: clip = bank.dayStart; volume = bank.dayStartVolume; break;
                case Sound.DayEnd:   clip = bank.dayEnd;   volume = bank.dayEndVolume;   break;
                case Sound.Click:    clip = bank.click;    volume = bank.clickVolume;    break;
                default:             clip = bank.rollover; volume = bank.rolloverVolume; break;
            }
            if (clip == null) return;
            source.PlayOneShot(clip, volume);
            LastPlayed = clip.name;
        }

        // Se llama después de Awake/OnEnable de la escena y antes de Start (antes de que empiece el día 1).
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Sellos: su propio sonido, sin click
            foreach (var stamps in Object.FindObjectsByType<StampButtonsUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Hook(stamps.approveButton, false).onClick.AddListener(() => Play(Sound.Approve));
                Hook(stamps.rejectButton, false).onClick.AddListener(() => Play(Sound.Reject));
            }

            // Todos los demás botones: rollover + click
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Hook(b, true);

            // Días
            foreach (var cm in Object.FindObjectsByType<CampaignManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                cm.OnDayStarted += _ => Play(Sound.DayStart);
                cm.OnDayEnded += _ => Play(Sound.DayEnd);
            }
        }

        static Button Hook(Button button, bool clickSound)
        {
            if (button == null) return null;
            if (button.GetComponent<ButtonSounds>() == null)
                button.gameObject.AddComponent<ButtonSounds>().playClick = clickSound;
            return button;
        }
    }
}
