using System.Collections.Generic;
using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// Variable global de la partida (Tolerancia del Comité, Cultura, Diplomacia, ...).
    /// Crear una nueva = crear un asset nuevo y agregarlo a CampaignConfig.stats.
    /// </summary>
    [CreateAssetMenu(menuName = "Voyager/Stat (variable global)", fileName = "Stat_")]
    public class StatData : ContentAsset
    {
        [TextArea] public string description;
        public int initialValue = 0;
        public int minValue = -999;
        public int maxValue = 999;

        [Header("Límite crítico (opcional)")]
        [Tooltip("Si está activo, al llegar (o bajar) al límite crítico se ejecutan las consecuencias de abajo. " +
                 "Ej: Tolerancia del Comité llega a 0 → despido.")]
        public bool useCriticalThreshold = false;
        public int criticalThreshold = 0;
        public List<Consequence> onCritical = new List<Consequence>();
    }
}
