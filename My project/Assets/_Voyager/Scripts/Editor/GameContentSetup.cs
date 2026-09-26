using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Crea la base del contenido REAL del juego en Assets/_Voyager/Data:
    /// las 4 regiones y las 8 categorías del documento de diseño, un día por categoría,
    /// la ProposalDatabase
    /// y el CampaignConfig. NUNCA pisa fichas que ya existan: se puede ejecutar varias veces.
    /// </summary>
    public static class GameContentSetup
    {
        const string Root = "Assets/_Voyager/Data";

        // (nombre del archivo, id, nombre visible, color)
        static readonly (string file, string id, string display, Color color)[] Regions =
        {
            ("Region_Asia",          "asia",           "Asia",           new Color(0.85f, 0.30f, 0.30f)),
            ("Region_EEUU",          "eeuu",           "EEUU",           new Color(0.90f, 0.75f, 0.20f)),
            ("Region_AmericaLatina", "america_latina", "América Latina", new Color(0.30f, 0.70f, 0.35f)),
            ("Region_Europa",        "europa",         "Europa",         new Color(0.25f, 0.40f, 0.85f)),
        };

        static readonly (string file, string id, string display)[] Categories =
        {
            ("Category_Musica",               "musica",                "Música"),
            ("Category_BellezaHumana",        "belleza_humana",        "Ejemplo de belleza humana"),
            ("Category_PeliculasSeries",      "peliculas_series",      "Películas y series"),
            ("Category_LugaresIconicos",      "lugares_iconicos",      "Lugares icónicos"),
            ("Category_Comida",               "comida",                "Comida"),
            ("Category_Animales",             "animales",              "Animales"),
            ("Category_PersonasInfluyentes",  "personas_influyentes",  "Personas influyentes"),
            ("Category_Objetos",              "objetos",               "Objetos"),
        };

        [MenuItem("Voyager/Crear base del juego real (regiones, categorías y días)", priority = 10)]
        public static void CreateBase()
        {
            foreach (var sub in new[] { "Regions", "Stats", "Categories", "Proposals", "Conditions", "Endings" })
                Directory.CreateDirectory($"{Root}/{sub}");
            AssetDatabase.Refresh();

            int created = 0;
            var regions = new List<RegionData>();
            foreach (var r in Regions)
            {
                var asset = LoadOrCreate<RegionData>($"{Root}/Regions/{r.file}.asset", a =>
                {
                    a.id = r.id;
                    a.displayName = r.display;
                    a.color = r.color;
                    a.initialRelation = 0;
                    a.minRelation = -100;
                    a.maxRelation = 100;
                    a.useCriticalThreshold = true;
                    a.criticalThreshold = -50;
                }, ref created);
                regions.Add(asset);
            }

            var categories = new List<CategoryData>();
            foreach (var c in Categories)
            {
                var asset = LoadOrCreate<CategoryData>($"{Root}/Categories/{c.file}.asset", a =>
                {
                    a.id = c.id;
                    a.displayName = c.display;
                }, ref created);
                categories.Add(asset);
            }

            var db = LoadOrCreate<ProposalDatabase>($"{Root}/ProposalDatabase.asset", _ => { }, ref created);
            ProposalDatabaseEditor.CollectAll(db);

            var config = LoadOrCreate<CampaignConfig>($"{Root}/CampaignConfig.asset", _ => { }, ref created);
            if (config.database == null) config.database = db;
            foreach (var r in regions) if (!config.regions.Contains(r)) config.regions.Add(r);
            foreach (var c in categories) if (!config.categories.Contains(c)) config.categories.Add(c);

            // Un día por categoría, en el orden de la lista Categories. Solo si todavía no hay días.
            string daysMsg;
            if (config.days.Count == 0)
            {
                for (int i = 0; i < categories.Count; i++)
                {
                    var cat = categories[i];
                    config.days.Add(new DayConfig
                    {
                        label = $"Día {i + 1} - {cat.DisplayName}",
                        proposalCount = regions.Count,          // un sobre por región
                        categoryMode = CategoryMode.Fixed,
                        fixedCategory = cat,
                        conditionMode = ConditionMode.RandomFromPool,
                        distinctRegions = true,
                        introText = $"Día {i + 1}. Hoy los bloques discuten sobre: {cat.DisplayName}."
                    });
                }
                daysMsg = $"Se crearon {categories.Count} días (uno por categoría).";
            }
            else daysMsg = $"CampaignConfig ya tenía {config.days.Count} días: no se tocaron.";
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);

            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            Debug.Log($"[Voyager] Base del juego lista en {Root}: {created} fichas nuevas " +
                      $"(las que ya existían no se tocaron). {daysMsg} " +
                      "Faltan: stats, propuestas, condiciones y finales.");
        }

        /// <summary>Si la ficha existe la devuelve sin tocarla; si no, la crea, la llena y RECIÉN AHÍ la guarda.</summary>
        internal static T LoadOrCreate<T>(string path, System.Action<T> fillIfNew, ref int created) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            fillIfNew(asset);
            AssetDatabase.CreateAsset(asset, path);
            created++;
            return asset;
        }
    }
}
