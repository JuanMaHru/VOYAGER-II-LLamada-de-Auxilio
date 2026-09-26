using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// Los sonidos del juego. Vive en Assets/_Voyager/Resources/SoundBank.asset para que el juego
    /// lo encuentre solo (Voyager → Conectar sonidos lo crea y le asigna los archivos de Assets/Audio).
    /// </summary>
    [CreateAssetMenu(menuName = "Voyager/Sound Bank", fileName = "SoundBank")]
    public class SoundBank : ScriptableObject
    {
        [Header("Gameplay")]
        [Tooltip("Botón SELLO APROBAR.")] public AudioClip approve;
        [Tooltip("Botón SELLO DESAPROBAR.")] public AudioClip reject;
        [Tooltip("Empieza un día.")] public AudioClip dayStart;
        [Tooltip("Termina un día.")] public AudioClip dayEnd;

        [Header("Interfaz")]
        [Tooltip("Click en cualquier botón de UI (menos los sellos, que tienen el suyo).")] public AudioClip click;
        [Tooltip("Pasar el mouse por encima de un botón.")] public AudioClip rollover;

        [Header("Volumen de cada uno (se multiplica por el volumen de efectos)")]
        [Range(0f, 1f)] public float approveVolume = 1f;
        [Range(0f, 1f)] public float rejectVolume = 1f;
        [Range(0f, 1f)] public float dayStartVolume = 1f;
        [Range(0f, 1f)] public float dayEndVolume = 1f;
        [Range(0f, 1f)] public float clickVolume = 1f;
        [Range(0f, 1f)] public float rolloverVolume = 0.6f;
    }
}
