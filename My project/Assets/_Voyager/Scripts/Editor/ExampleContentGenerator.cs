using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Genera el contenido de ejemplo del PASO 7 (4 regiones, 5 propuestas, 1 categoría,
    /// 1 condición, 2 stats, 2 finales) y arma el GameObject de sistemas en la escena.
    /// Es solo una ayuda: todo esto se puede crear a mano con Create → Voyager → ...
    ///
    /// IMPORTANTE: cada ficha se llena ANTES de CreateAsset (Make&lt;T&gt;), y las fichas
    /// referenciadas se crean antes que las que las referencian. Si se llenan después,
    /// el editor abierto puede recargarlas desde disco y quedan vacías.
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

            // ---------- Stats (primero: las regiones los referencian) ----------
            var culture = Make<StatData>("Culture", "Stats", s => s.displayName = "Cultura");
            var diplomacy = Make<StatData>("Diplomacy", "Stats", s => s.displayName = "Diplomacia");

            // ---------- Regiones ----------
            // Tolerancia de Europa: a -10 retira al embajador.
            var europa = Region("Europa", "Europa", new Color(0.25f, 0.4f, 0.85f), r =>
            {
                r.criticalThreshold = -10;
                r.onCritical = new List<Consequence>
                {
                    Narr("Europa retira a su embajador del proyecto Voyager en señal de protesta."),
                    StatC(diplomacy, -5),
                    new Consequence { type = ConsequenceType.SetFlag, flag = "europa_ofendida" }
                };
            });
            var asia = Region("Asia", "Asia", new Color(0.85f, 0.3f, 0.3f));
            var latam = Region("America_Latina", "América Latina", new Color(0.3f, 0.7f, 0.35f));
            var eeuu = Region("EEUU", "EEUU", new Color(0.9f, 0.75f, 0.2f));

            // ---------- Categoría ----------
            var musica = Make<CategoryData>("Musica", "Categories", c => c.displayName = "Música");

            // ---------- Propuestas ----------
            Proposal("P_Rock_Argentino", "Rock argentino", latam, musica,
                "Una selección de clásicos del rock nacional. Guitarras, poesía y estadios llenos.",
                new[] { Reg(latam, 10), StatC(culture, 3) },
                new[] { Reg(latam, -5) });

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

            Proposal("P_Michael_Jackson", "Michael Jackson", eeuu, musica,
                "El Rey del Pop. Si los extraterrestres no bailan con esto, no bailan con nada.",
                new[] { Reg(eeuu, 10), StatC(culture, 4) },
                new[] { Reg(eeuu, -10), Narr("La delegación de EEUU amenaza con recortar los fondos de la NASA.") });

            // ---------- Condición diaria ----------
            var cond = Make<DailyConditionData>("C_Aprobar2_Rechazar3", "Conditions", c =>
            {
                c.playerText = "El presupuesto es limitado: debés aprobar exactamente 2 propuestas y rechazar 3.";
                c.requirements = new RequirementGroup
                {
                    mode = LogicMode.All,
                    requirements = new List<Requirement>
                    {
                        new Requirement { type = RequirementType.ApprovedToday, comparison = Comparison.Equal, value = 2 },
                        new Requirement { type = RequirementType.RejectedToday, comparison = Comparison.Equal, value = 3 }
                    }
                };
                c.onSuccess = new List<Consequence> { StatC(diplomacy, 3), Narr("El Consejo aprueba tu gestión del presupuesto.") };
                c.onFail = new List<Consequence>
                {
                    new Consequence { type = ConsequenceType.ModifyAllRegions, amount = -5 },
                    Narr("El Consejo te acusa de desorganización. Todas las regiones pierden confianza.")
                };
            });

            // ---------- Finales ----------
            var good = Make<EndingData>("E_Mensaje_Humanidad", "Endings", e =>
            {
                e.displayName = "El mensaje de la humanidad";
                e.description = "La sonda parte con un mensaje rico y diverso. Semanas después, llega una respuesta: \"Los escuchamos. Vamos en camino.\"";
                e.priority = 100;
                e.requirements = new RequirementGroup
                {
                    mode = LogicMode.All,
                    requirements = new List<Requirement>
                    {
                        new Requirement { type = RequirementType.StatValue, stat = culture, comparison = Comparison.GreaterOrEqual, value = 8 },
                        new Requirement { type = RequirementType.StatValue, stat = diplomacy, comparison = Comparison.GreaterOrEqual, value = 3 }
                    }
                };
            });

            var def = Make<EndingData>("E_Silencio", "Endings", e =>
            {
                e.displayName = "Silencio en el cosmos";
                e.description = "La Voyager II se pierde en la oscuridad. Nadie responde.";
                e.priority = 0;
            });

            // ---------- Base de datos y campaña ----------
            var db = Make<ProposalDatabase>("ProposalDatabase_Example", null,
                d => d.proposals = ProposalDatabaseEditor.FindProposalsIn(Root));

            var config = Make<CampaignConfig>("CampaignConfig_Example", null, c =>
            {
                c.database = db;
                c.regions = new List<RegionData> { europa, asia, latam, eeuu };
                c.stats = new List<StatData> { culture, diplomacy };
                c.categories = new List<CategoryData> { musica };
                c.conditionPool = new List<DailyConditionData> { cond };
                c.endings = new List<EndingData> { good, def };
                c.defaultEnding = def;
                c.days = new List<DayConfig>
                {
                    new DayConfig
                    {
                        label = "Día 1 - Música",
                        proposalCount = 5,
                        categoryMode = CategoryMode.Fixed, fixedCategory = musica,
                        conditionMode = ConditionMode.Fixed, fixedCondition = cond,
                        distinctRegions = false, // 4 regiones y 5 propuestas: alguna se repite sí o sí
                        introText = "Primer día en el Consejo. Hoy se decide la música que viajará en la Voyager II."
                    }
                };
            });

            AssetDatabase.SaveAssets();
            ContentValidator.Validate(config);
            Selection.activeObject = config;

            // Al regenerar, el CampaignConfig viejo se borra: si la escena lo usaba, se vuelve a enlazar.
            var cm = Object.FindAnyObjectByType<CampaignManager>();
            if (cm != null)
            {
                var so = new SerializedObject(cm);
                var prop = so.FindProperty("config");
                if (prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = config;
                    so.ApplyModifiedProperties();
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cm.gameObject.scene);
                    Debug.Log("[Voyager] GameSystems volvió a enlazarse con el nuevo CampaignConfig_Example. Guardá la escena (Ctrl+S).");
                }
            }
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
        /// <summary>Crea la instancia, la llena y RECIÉN AHÍ la guarda como asset.</summary>
        static T Make<T>(string assetName, string folder, Action<T> fill) where T : ScriptableObject
        {
            var a = ScriptableObject.CreateInstance<T>();
            fill(a);
            string path = folder == null ? $"{Root}/{assetName}.asset" : $"{Root}/{folder}/{assetName}.asset";
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static RegionData Region(string assetName, string display, Color color, Action<RegionData> extra = null)
            => Make<RegionData>(assetName, "Regions", r =>
            {
                r.displayName = display;
                r.color = color;
                r.initialRelation = 0;
                r.minRelation = -100;
                r.maxRelation = 100;
                r.criticalThreshold = -50;
                extra?.Invoke(r);
            });

        static void Proposal(string assetName, string display, RegionData region, CategoryData cat, string desc,
                             Consequence[] approve, Consequence[] reject)
            => Make<ProposalData>(assetName, "Proposals", p =>
            {
                p.displayName = display;
                p.region = region;
                p.category = cat;
                p.description = desc;
                p.onApprove = new List<Consequence>(approve);
                p.onReject = new List<Consequence>(reject);
            });

        static Consequence Reg(RegionData r, int amount) => new Consequence { type = ConsequenceType.ModifyRegion, region = r, amount = amount };
        static Consequence StatC(StatData s, int amount) => new Consequence { type = ConsequenceType.ModifyStat, stat = s, amount = amount };
        static Consequence Narr(string text) => new Consequence { type = ConsequenceType.ShowNarrative, text = text };
    }
}
