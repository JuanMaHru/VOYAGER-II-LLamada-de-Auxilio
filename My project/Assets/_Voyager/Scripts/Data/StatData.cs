using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// Variable global de la partida (Humanity, Culture, Diplomacy, Chaos, ...).
    /// Crear una nueva = crear un asset nuevo y agregarlo a CampaignConfig.stats.
    /// </summary>
    [CreateAssetMenu(menuName = "Voyager/Stat (variable global)", fileName = "Stat_")]
    public class StatData : ContentAsset
    {
        [TextArea] public string description;
        public int initialValue = 0;
        public int minValue = -999;
        public int maxValue = 999;
    }
}
