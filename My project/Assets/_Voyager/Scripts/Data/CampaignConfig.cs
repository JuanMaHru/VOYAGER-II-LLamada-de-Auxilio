using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voyager
{
    public enum CategoryMode
    {
        RandomFromPool, // se sortea entre CampaignConfig.categories (D3)
        Fixed,          // se usa DayConfig.fixedCategory
        AnyCategory     // sin filtro de categoría: propuestas mezcladas
    }

    public enum ConditionMode
    {
        RandomFromPool, // se sortea entre CampaignConfig.conditionPool sin repetir (D3)
        Fixed,          // se usa DayConfig.fixedCondition
        None,           // día sin condición
        Generated       // se genera a partir de las propuestas que salieron: siempre cumplible (ConditionGenerator)
    }

    /// <summary>Configuración de UN día. La lista de días define la duración de la campaña.</summary>
    [Serializable]
    public class DayConfig
    {
        [Tooltip("Solo para organizarte en el Inspector (ej: \"Día 1 - Tutorial\").")]
        public string label;

        [Min(1)] public int proposalCount = 5;

        public CategoryMode categoryMode = CategoryMode.RandomFromPool;
        public CategoryData fixedCategory;

        public ConditionMode conditionMode = ConditionMode.RandomFromPool;
        public DailyConditionData fixedCondition;

        [Tooltip("Intentar que cada propuesta del día sea de una región distinta (D2). " +
                 "Si no alcanzan las regiones, se permiten repetidas.")]
        public bool distinctRegions = true;

        [Tooltip("Vacío = todas las regiones de la campaña.")]
        public List<RegionData> allowedRegions = new List<RegionData>();

        [Tooltip("Texto narrativo opcional al comenzar el día.")]
        [TextArea] public string introText;
    }

    /// <summary>
    /// Punto central de configuración: qué regiones, stats, categorías, condiciones y finales
    /// existen, y cómo es cada día. Es el único asset que el CampaignManager necesita.
    /// </summary>
    [CreateAssetMenu(menuName = "Voyager/Campaign Config", fileName = "CampaignConfig")]
    public class CampaignConfig : ScriptableObject
    {
        [Header("Contenido")]
        public ProposalDatabase database;
        public List<RegionData> regions = new List<RegionData>();
        public List<StatData> stats = new List<StatData>();

        [Tooltip("Categorías que pueden sortearse para los días en modo RandomFromPool.")]
        public List<CategoryData> categories = new List<CategoryData>();

        [Tooltip("Condiciones que pueden sortearse para los días en modo RandomFromPool.")]
        public List<DailyConditionData> conditionPool = new List<DailyConditionData>();

        [Tooltip("Ajustes de las condiciones que se arman solas (días en modo Generated).")]
        public ConditionGeneratorSettings conditionGenerator = new ConditionGeneratorSettings();

        [Header("Días (la cantidad de elementos = días totales)")]
        public List<DayConfig> days = new List<DayConfig>();

        [Header("Finales")]
        public List<EndingData> endings = new List<EndingData>();
        [Tooltip("Se usa si ningún final cumple sus requisitos.")]
        public EndingData defaultEnding;

        [Header("Sorteo")]
        [Tooltip("Al sortear categorías, evitar las que ya salieron en la partida mientras queden otras con propuestas suficientes.")]
        public bool avoidRepeatingCategories = true;

        [Tooltip("Semilla fija para reproducir exactamente la misma partida (útil para testear).")]
        public bool useFixedSeed = false;
        public int seed = 12345;

        public int TotalDays => days.Count;
    }
}
