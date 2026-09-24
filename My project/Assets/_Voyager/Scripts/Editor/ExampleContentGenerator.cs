using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Genera el contenido de ejemplo del PASO 7 (3 regiones, 5 propuestas, 1 categoría,
    /// 1 condición, 2 stats, 2 finales) y arma el GameObject de sistemas en la escena.
    /// Es solo una ayuda: todo esto se puede crear a mano con Create → Voyager → ...
    /// </summary>
    public static class ExampleContentGenerator
    {
        const string Root = "Assets/_Voyager/Example";

        [MenuItem("Voyager/Crear contenido de ejemplo", priority = 0)]
        public static void CreateExampleContent()
        {
            if (AssetDatabase.IsValidFolder(Root))
            {
                if (!Application.isBatchMode &&
                    !EditorUtility.DisplayDialog("Contenido de ejemplo",
                        $"La carpeta {Root} ya existe. ¿Borrarla y regenerarla?", "Regenerar", "Cancelar"))
                    return;
                AssetDatabase.DeleteAsset(Root);
            }
            foreach (var sub in new[] { "Regions", "Stats", "Categories", "Proposals", "Conditions", "Endings" })
                Directory.CreateDirectory($"{Root}/{sub}");
            AssetDatabase.Refresh();

            // ---------- Regiones ----------
            var europa = Region("Europa", new Color(0.25f, 0.4f, 0.85f));
            var asia = Region("Asia", new Color(0.85f, 0.3f, 0.3f));
            var sudamerica = Region("America_del_Sur", new Color(0.3f, 0.7f, 0.35f), "América del Sur");

            // ---------- Stats ----------
            var culture = Stat("Culture", "Cultura");
            var diplomacy = Stat("Diplomacy", "Diplomacia");

            // Tolerancia de Europa: a -15 retira al embajador
            europa.criticalThreshold = -15;
            europa.onCritical = new List<Consequence>
            {
                Narr("Europa retira a su embajador del proyecto Voyager en señal de protesta."),
                StatC(diplomacy, -5),
                new Consequence { type = ConsequenceType.SetFlag, flag = "europa_ofendida" }
            };
            Save(europa, "Regions");

            // ---------- Categoría ----------
            var musica = Create<CategoryData>("Musica", "Categories");
            musica.displayName = "Música";
            Save(musica, "Categories");

            // ---------- Propuestas ----------
            Proposal("P_Rock_Argentino", "Rock argentino", sudamerica, musica,
                "Una selección de clásicos del rock nacional. Guitarras, poesía y estadios llenos.",
                new[] { Reg(sudamerica, 10), StatC(culture, 3) },
                new[] { Reg(sudamerica, -5) });

            Proposal("P_Novena_Sinfonia", "Novena Sinfonía de Beethoven", europa, musica,
                "El 'Himno a la alegría': un mensaje de fraternidad universal.",
                new[] { Reg(europa, 10), StatC(culture, 5), StatC(diplomacy, 2) },
                new[] { Reg(europa, -10) });

            Proposal("P_Kpop", "K-pop", asia, musica,
                "Coreografías perfectas y millones de fans. ¿Entenderán los extraterrestres el fandom?",
                new[] { Reg(asia, 10), StatC(culture, 2) },
                new[] { Reg(asia, -5) });

            Proposal("P_Raga", "Raga de la música clásica india", asia, musica,
                "Una tradición milenaria de improvisación y matemática sonora.",
                new[] { Reg(asia, 8), StatC(culture, 4) },
                new[] { Reg(asia, -8), StatC(diplomacy, -2) });

            Proposal("P_Flamenco", "Flamenco", europa, musica,
                "Palmas, guitarra y duende.",
                new[] { Reg(europa, 6), StatC(culture, 3) },
                new[] { Reg(europa, -6), Narr("La delegación española abandona la sala indignada.") });

            // ---------- Condición diaria ----------
            var cond = Create<DailyConditionData>("C_Aprobar2_Rechazar3", "Conditions");
            cond.playerText = "El presupuesto es limitado: debés aprobar exactamente 2 propuestas y rechazar 3.";
            cond.requirements = new RequirementGroup
            {
                mode = LogicMode.All,
                requirements = new List<Requirement>
                {
                    new Requirement { type = RequirementType.ApprovedToday, comparison = Comparison.Equal, value = 2 },
                    new Requirement { type = RequirementType.RejectedToday, comparison = Comparison.Equal, value = 3 }
                }
            };
            cond.onSuccess = new List<Consequence> { StatC(diplomacy, 3), Narr("El Consejo aprueba tu gestión del presupuesto.") };
            cond.onFail = new List<Consequence>
            {
                new Consequence { type = ConsequenceType.ModifyAllRegions, amount = -5 },
                Narr("El Consejo te acusa de desorganización. Todas las regiones pierden confianza.")
            };
            Save(cond, "Conditions");

            // ---------- Finales ----------
            var good = Create<EndingData>("E_Mensaje_Humanidad", "Endings");
            good.displayName = "El mensaje de la humanidad";
            good.description = "La sonda parte con un mensaje rico y diverso. Semanas después, llega una respuesta: \"Los escuchamos. Vamos en camino.\"";
            good.priority = 100;
            good.requirements = new RequirementGroup
            {
                mode = LogicMode.All,
                requirements = new List<Requirement>
                {
                    new Requirement { type = RequirementType.StatValue, stat = culture, comparison = Comparison.GreaterOrEqual, value = 8 },
                    new Requirement { type = RequirementType.StatValue, stat = diplomacy, comparison = Comparison.GreaterOrEqual, value = 3 }
                }
            };
            Save(good, "Endings");

            var def = Create<EndingData>("E_Silencio", "Endings");
            def.displayName = "Silencio en el cosmos";
            def.description = "La Voyager II se pierde en la oscuridad. Nadie responde.";
            def.priority = 0;
            Save(def, "Endings");

            // ---------- Base de datos y campaña ----------
            var db = ScriptableObject.CreateInstance<ProposalDatabase>();
            AssetDatabase.CreateAsset(db, $"{Root}/ProposalDatabase_Example.asset");
            ProposalDatabaseEditor.CollectAll(db);

            var config = ScriptableObject.CreateInstance<CampaignConfig>();
            config.database = db;
            config.regions = new List<RegionData> { europa, asia, sudamerica };
            config.stats = new List<StatData> { culture, diplomacy };
            config.categories = new List<CategoryData> { musica };
            config.conditionPool = new List<DailyConditionData> { cond };
            config.endings = new List<EndingData> { good, def };
            config.defaultEnding = def;
            config.days = new List<DayConfig>
            {
                new DayConfig
                {
                    label = "Día 1 - Música",
                    proposalCount = 5,
                    categoryMode = CategoryMode.Fixed, fixedCategory = musica,
                    conditionMode = ConditionMode.Fixed, fixedCondition = cond,
                    distinctRegions = false, // 3 regiones y 5 propuestas: se repiten sí o sí
                    introText = "Primer día en el Consejo. Hoy se decide la música que viajará en la Voyager II."
                }
            };
            AssetDatabase.CreateAsset(config, $"{Root}/CampaignConfig_Example.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ContentValidator.Validate(config);
            Selection.activeObject = config;
            Debug.Log($"[Voyager] Contenido de ejemplo creado en {Root}. Ahora: Voyager → Crear GameSystems en la escena.");
        }

        [MenuItem("Voyager/Crear GameSystems en la escena", priority = 1)]
        public static void CreateSystemsInScene()
        {
            var existing = Object.FindAnyObjectByType<CampaignManager>();
            if (existing != null) { Selection.activeObject = existing.gameObject; Debug.Log("[Voyager] Ya hay un CampaignManager en la escena."); return; }

            var go = new GameObject("GameSystems");
            Undo.RegisterCreatedObjectUndo(go, "Crear GameSystems");
            var cm = go.AddComponent<CampaignManager>();
            var pm = go.AddComponent<ProposalManager>();

            var so = new SerializedObject(cm);
            so.FindProperty("proposalManager").objectReferenceValue = pm;
            var guids = AssetDatabase.FindAssets("t:CampaignConfig");
            if (guids.Length > 0)
                so.FindProperty("config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CampaignConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            so.ApplyModifiedProperties();

            var spm = new SerializedObject(pm);
            spm.FindProperty("campaign").objectReferenceValue = cm;
            spm.ApplyModifiedProperties();

            Selection.activeObject = go;
        }

        // ------------------------------------------------------------------ helpers
        static T Create<T>(string assetName, string folder) where T : ScriptableObject
        {
            var a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, $"{Root}/{folder}/{assetName}.asset");
            return a;
        }

        static void Save(Object o, string _) => EditorUtility.SetDirty(o);

        static RegionData Region(string assetName, Color color, string display = null)
        {
            var r = Create<RegionData>(assetName, "Regions");
            r.displayName = display ?? assetName;
            r.color = color;
            r.initialRelation = 0;
            r.minRelation = -100;
            r.maxRelation = 100;
            r.criticalThreshold = -50;
            EditorUtility.SetDirty(r);
            return r;
        }

        static StatData Stat(string assetName, string display)
        {
            var s = Create<StatData>(assetName, "Stats");
            s.displayName = display;
            EditorUtility.SetDirty(s);
            return s;
        }

        static void Proposal(string assetName, string display, RegionData region, CategoryData cat, string desc,
                             Consequence[] approve, Consequence[] reject)
        {
            var p = Create<ProposalData>(assetName, "Proposals");
            p.displayName = display;
            p.region = region;
            p.category = cat;
            p.description = desc;
            p.onApprove = new List<Consequence>(approve);
            p.onReject = new List<Consequence>(reject);
            EditorUtility.SetDirty(p);
        }

        static Consequence Reg(RegionData r, int amount) => new Consequence { type = ConsequenceType.ModifyRegion, region = r, amount = amount };
        static Consequence StatC(StatData s, int amount) => new Consequence { type = ConsequenceType.ModifyStat, stat = s, amount = amount };
        static Consequence Narr(string text) => new Consequence { type = ConsequenceType.ShowNarrative, text = text };
    }
}
