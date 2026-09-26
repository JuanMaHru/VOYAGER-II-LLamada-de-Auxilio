using System.Collections.Generic;
using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// Un bloque/región del mundo (Europa, Asia, Argentina, ...).
    /// La relación ACTUAL no se guarda acá: vive en GameState.
    /// </summary>
    [CreateAssetMenu(menuName = "Voyager/Region", fileName = "Region_")]
    public class RegionData : ContentAsset
    {
        [Header("Presentación")]
        public Sprite icon;
        public Color color = Color.white;

        [Header("Relación")]
        public int initialRelation = 0;
        public int minRelation = -100;
        public int maxRelation = 100;

        [Header("Tolerancia")]
        [Tooltip("Si está activo, al llegar (o bajar) al límite crítico se ejecutan las consecuencias de abajo.")]
        public bool useCriticalThreshold = true;

        [Tooltip("La consecuencia se dispara cuando la relación cruza este valor hacia abajo (relación <= límite). " +
                 "Si la relación se recupera por encima, puede volver a dispararse.")]
        public int criticalThreshold = -50;

        public List<Consequence> onCritical = new List<Consequence>();
    }
}
