using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// Ponelo en cualquier AudioSource de música o efectos: su volumen se multiplica por el del canal
    /// elegido en Opciones → Sonido (el volumen maestro ya se aplica a todo).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioVolume : MonoBehaviour
    {
        public enum Channel { Music, Effects }
        public Channel channel = Channel.Effects;

        AudioSource source;
        float baseVolume;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            baseVolume = source.volume;
        }

        void OnEnable() { GameAudio.OnChanged += Apply; Apply(); }
        void OnDisable() => GameAudio.OnChanged -= Apply;

        void Apply() => source.volume = baseVolume * (channel == Channel.Music ? GameAudio.Music : GameAudio.Effects);
    }
}
