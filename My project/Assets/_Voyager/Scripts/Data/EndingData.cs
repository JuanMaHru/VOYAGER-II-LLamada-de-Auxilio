using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// Un final posible. Se elige el de MAYOR prioridad cuyos requisitos se cumplan.
    /// A igual prioridad gana el que está antes en CampaignConfig.endings.
    /// </summary>
    [CreateAssetMenu(menuName = "Voyager/Ending", fileName = "Ending_")]
    public class EndingData : ContentAsset
    {
        [TextArea(3, 10)] public string description;
        public Sprite image;
        public int priority = 0;

        [Tooltip("Vacío = siempre válido (útil para el final por defecto).")]
        public RequirementGroup requirements = new RequirementGroup();

        [Header("Reacciones de los alienígenas")]
        [Tooltip("Mostrar, debajo del final, cómo reaccionan los alienígenas a algunas propuestas aprobadas. " +
                 "Desactivalo en finales donde la sonda nunca llega (ej: Despedido).")]
        public bool showAlienReactions = true;
        [Min(0)] public int maxReactions = 3;
    }
}
