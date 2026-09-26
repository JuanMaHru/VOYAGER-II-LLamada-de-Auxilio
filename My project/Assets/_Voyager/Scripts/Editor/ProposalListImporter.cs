using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Lee Assets/_Voyager/Data/Propuestas.txt y crea una ficha ProposalData por cada nombre.
    ///
    /// PUNTOS: se sortean UNA sola vez por propuesta, usando su id como semilla. Por eso son
    /// fijos: siempre los mismos en cada partida y aunque se vuelva a ejecutar el importador.
    /// Las fichas que ya existen no se tocan (si alguien ajustó los puntos a mano, se respetan).
    ///
    /// Al aprobar, cada propuesta mueve la "Tolerancia del Comité" (ver EndingsSetup), que también
    /// configura finales y reacciones alienígenas. Además deja lista la campaña real:
    /// regiones, días con condiciones generadas y una tabla Propuestas_Puntos.csv.
    /// </summary>
    public static class ProposalListImporter
    {
        const string Root = "Assets/_Voyager/Data";
        const string ListPath = Root + "/Propuestas.txt";
        const string CsvPath = Root + "/Propuestas_Puntos.csv";

        // ---- Tabla de puntos posibles (se sortea un valor de cada lista por propuesta) ----
        static readonly int[] ImpressionIfApproved = { -5, -3, 2, 4, 6, 8, 10 };
        static readonly int[] RelationGainIfApproved = { 5, 10, 15 };  // +X a su región
        static readonly int[] RelationLossIfRejected = { 5, 10 };      // -X a su región

        [MenuItem("Voyager/Cargar propuestas desde Propuestas.txt", priority = 11)]
        public static void Import()
        {
            if (!File.Exists(ListPath)) { Debug.LogError($"[Voyager] No existe {ListPath}."); return; }

            GameContentSetup.CreateBase(); // asegura regiones, categorías, base de datos, config y días
            var config = AssetDatabase.LoadAssetAtPath<CampaignConfig>($"{Root}/CampaignConfig.asset");
            var regions = config.regions.Where(r => r != null).ToDictionary(r => r.Id);
            var categories = config.categories.Where(c => c != null).ToDictionary(c => c.Id);
            int created = 0;

            // ---------- Stat que mueven las propuestas: Tolerancia del Comité ----------
            var tolerance = EndingsSetup.EnsureTolerance();

            // ---------- Propuestas ----------
            var entries = Parse(regions, categories);
            foreach (var cat in entries.Select(e => e.category).Distinct())
                Directory.CreateDirectory($"{Root}/Proposals/{FolderName(cat)}");
            AssetDatabase.Refresh();

            int newProposals = 0;
            foreach (var e in entries)
            {
                string path = $"{Root}/Proposals/{FolderName(e.category)}/P_{e.region.Id}_{e.slug}.asset";
                int before = created;
                GameContentSetup.LoadOrCreate<ProposalData>(path, p =>
                {
                    var rng = new System.Random(StableHash(e.id));
                    int approveRelation = RelationGainIfApproved[rng.Next(RelationGainIfApproved.Length)];
                    int approveImpression = ImpressionIfApproved[rng.Next(ImpressionIfApproved.Length)];
                    int rejectRelation = -RelationLossIfRejected[rng.Next(RelationLossIfRejected.Length)];

                    p.id = e.id;
                    p.displayName = e.name;
                    p.description = $"Propuesta del bloque {e.region.DisplayName}.";
                    p.region = e.region;
                    p.category = e.category;
                    p.onApprove = new List<Consequence>
                    {
                        new Consequence { type = ConsequenceType.ModifyRegion, region = e.region, amount = approveRelation },
                        new Consequence { type = ConsequenceType.ModifyStat, stat = tolerance, amount = approveImpression }
                    };
                    p.onReject = new List<Consequence>
                    {
                        new Consequence { type = ConsequenceType.ModifyRegion, region = e.region, amount = rejectRelation }
                    };
                }, ref created);
                if (created > before) newProposals++;
            }

            // ---------- Días: condición generada; puede haber varias de un país y ninguna de otro ----------
            foreach (var d in config.days)
            {
                d.distinctRegions = false;
                d.conditionMode = ConditionMode.Generated;
            }

            var db = config.database;
            ProposalDatabaseEditor.CollectAll(db);

            // ---------- Tolerancia, puntos de condiciones, reacciones y finales ----------
            EndingsSetup.Apply(config);
            WriteCsv(config, db, tolerance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Voyager] Propuestas cargadas: {entries.Count} en la lista, {newProposals} fichas nuevas, " +
                      $"{db.proposals.Count} en la base. Tabla de puntos: {CsvPath}");
            ContentValidator.Validate(config);
        }

        // ------------------------------------------------------------------ lectura de la lista
        struct Entry
        {
            public CategoryData category;
            public RegionData region;
            public string name, slug, id;
        }

        static List<Entry> Parse(Dictionary<string, RegionData> regions, Dictionary<string, CategoryData> categories)
        {
            var result = new List<Entry>();
            var usedIds = new HashSet<string>();
            CategoryData category = null;
            int lineNumber = 0;

            foreach (var raw in File.ReadAllLines(ListPath, Encoding.UTF8))
            {
                lineNumber++;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string key = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                    if (!categories.TryGetValue(key, out category))
                        Debug.LogError($"[Voyager] Propuestas.txt línea {lineNumber}: la categoría '{key}' no existe. Opciones: {string.Join(", ", categories.Keys)}");
                    continue;
                }

                int colon = line.IndexOf(':');
                if (colon < 0) { Debug.LogWarning($"[Voyager] Propuestas.txt línea {lineNumber}: falta 'region:' → se ignora."); continue; }
                if (category == null) continue;

                string regionKey = line.Substring(0, colon).Trim().ToLowerInvariant();
                if (!regions.TryGetValue(regionKey, out var region))
                {
                    Debug.LogError($"[Voyager] Propuestas.txt línea {lineNumber}: la región '{regionKey}' no existe. Opciones: {string.Join(", ", regions.Keys)}");
                    continue;
                }

                foreach (var item in line.Substring(colon + 1).Split(','))
                {
                    string name = System.Text.RegularExpressions.Regex.Replace(item.Trim().TrimEnd('.').Trim(), @"\s+", " ");
                    if (name.Length == 0) continue;

                    string slug = Slug(name);
                    string id = $"{category.Id}_{region.Id}_{slug}";
                    int n = 2;
                    while (usedIds.Contains(id)) { id = $"{category.Id}_{region.Id}_{slug}_{n}"; n++; }
                    if (n > 2) slug = $"{slug}_{n - 1}";
                    usedIds.Add(id);

                    result.Add(new Entry { category = category, region = region, name = name, slug = slug, id = id });
                }
            }
            return result;
        }

        // ------------------------------------------------------------------ tabla de puntos
        static void WriteCsv(CampaignConfig config, ProposalDatabase db, StatData impression)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tema (día);Región;Propuesta;APROBAR: relación con su región;APROBAR: Tolerancia del Comité;RECHAZAR: relación con su región;RECHAZAR: Tolerancia del Comité;Reacción alienígena");

            var ordered = db.proposals.Where(p => p != null)
                .OrderBy(p => config.categories.IndexOf(p.category))
                .ThenBy(p => config.regions.IndexOf(p.region))
                .ThenBy(p => p.DisplayName);

            foreach (var p in ordered)
            {
                sb.AppendLine(string.Join(";",
                    Csv(p.CategoryName), Csv(p.RegionName), Csv(p.DisplayName),
                    Signed(Sum(p.onApprove, ConsequenceType.ModifyRegion, p.region, null)),
                    Signed(Sum(p.onApprove, ConsequenceType.ModifyStat, null, impression)),
                    Signed(Sum(p.onReject, ConsequenceType.ModifyRegion, p.region, null)),
                    Signed(Sum(p.onReject, ConsequenceType.ModifyStat, null, impression)),
                    Csv(p.alienReaction ?? "")));
            }
            File.WriteAllText(CsvPath, sb.ToString(), new UTF8Encoding(true)); // con BOM: Excel muestra bien las tildes
            AssetDatabase.ImportAsset(CsvPath);
        }

        static int Sum(List<Consequence> list, ConsequenceType type, RegionData region, StatData stat)
            => list.Where(c => c.type == type && (region == null || c.region == region) && (stat == null || c.stat == stat)).Sum(c => c.amount);

        static string Signed(int v) => v > 0 ? "+" + v : v.ToString();
        static string Csv(string s) => s.Contains(";") || s.Contains("\"") ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

        // ------------------------------------------------------------------ utilidades
        static string FolderName(CategoryData c) => c.name.StartsWith("Category_") ? c.name.Substring("Category_".Length) : c.name;

        static string Slug(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
            }
            string slug = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
            return slug.Length > 0 ? slug : "propuesta";
        }

        /// <summary>Hash FNV-1a: siempre da el mismo número para el mismo texto (a diferencia de GetHashCode).</summary>
        static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (byte b in Encoding.UTF8.GetBytes(s)) { h ^= b; h *= 16777619; }
                return (int)h;
            }
        }
    }
}
