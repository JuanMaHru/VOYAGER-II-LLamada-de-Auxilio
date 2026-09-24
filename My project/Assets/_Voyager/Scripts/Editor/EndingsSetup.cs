using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Configura la campaña real a partir de dos archivos de texto editables:
    ///   Data/Finales.txt    → fichas EndingData (el archivo manda: se sobrescriben)
    ///   Data/Reacciones.txt → ProposalData.alienReaction
    /// Además deja lista la "Tolerancia del Comité" (barra que si llega a 0 te despide al instante).
    /// Menú: Voyager → Actualizar tolerancia, finales y reacciones.
    /// </summary>
    public static class EndingsSetup
    {
        const string Root = "Assets/_Voyager/Data";
        const string EndingsPath = Root + "/Finales.txt";
        const string ReactionsPath = Root + "/Reacciones.txt";
        const string TolerancePath = Root + "/Stats/Stat_Tolerancia.asset";
        const string OldImpressionPath = Root + "/Stats/Stat_Impresion.asset";
        public const string FiredFlag = "despedido";

        // ---- Tolerancia del Comité (valores al crearla por primera vez) ----
        const int InitialTolerance = 40;
        const int MaxTolerance = 100;

        // ---- Puntos de tolerancia pedidos por el equipo ----
        const int ConditionMet = 5, ConditionFailed = -10;
        const int DemandFulfilled = 7, DemandIgnored = -20;

        [MenuItem("Voyager/Actualizar tolerancia, finales y reacciones", priority = 12)]
        public static void RunFromMenu()
        {
            var config = AssetDatabase.LoadAssetAtPath<CampaignConfig>($"{Root}/CampaignConfig.asset");
            if (config == null) { Debug.LogError("[Voyager] Primero ejecutá Voyager → Cargar propuestas desde Propuestas.txt"); return; }
            Apply(config);
            AssetDatabase.SaveAssets();
            ContentValidator.Validate(config);
        }

        public static void Apply(CampaignConfig config)
        {
            var tolerance = EnsureTolerance();
            if (!config.stats.Contains(tolerance)) config.stats.Insert(0, tolerance);

            ConfigureConditionPoints(config, tolerance);
            RemoveRegionFiring(config);
            if (config.database != null) ApplyReactions(config.database);
            ApplyEndings(config);

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
        }

        // =====================================================================
        // Tolerancia
        // =====================================================================
        /// <summary>
        /// Devuelve Stat_Tolerancia. Si todavía existe la vieja Stat_Impresion, la convierte
        /// (así las 303 propuestas que ya la usan pasan a mover la tolerancia sin tocarlas).
        /// </summary>
        public static StatData EnsureTolerance()
        {
            var tol = AssetDatabase.LoadAssetAtPath<StatData>(TolerancePath);
            if (tol != null) return tol;

            var old = AssetDatabase.LoadAssetAtPath<StatData>(OldImpressionPath);
            if (old != null)
            {
                string err = AssetDatabase.RenameAsset(OldImpressionPath, "Stat_Tolerancia");
                if (!string.IsNullOrEmpty(err)) Debug.LogError("[Voyager] No se pudo renombrar Stat_Impresion: " + err);
                tol = old;
                FillTolerance(tol);
                EditorUtility.SetDirty(tol);
                AssetDatabase.SaveAssetIfDirty(tol);
                Debug.Log("[Voyager] 'Impresión' se convirtió en 'Tolerancia del Comité' (Stat_Tolerancia).");
                return tol;
            }

            Directory.CreateDirectory($"{Root}/Stats");
            AssetDatabase.Refresh();
            int created = 0;
            return GameContentSetup.LoadOrCreate<StatData>(TolerancePath, FillTolerance, ref created);
        }

        static void FillTolerance(StatData s)
        {
            s.id = "tolerancia";
            s.displayName = "Tolerancia del Comité";
            s.description = "Paciencia del Comité con tu gestión. Sube y baja con las propuestas que aprobás y " +
                            "con cumplir (o no) las condiciones y exigencias. Si llega a 0, te despiden.";
            s.initialValue = InitialTolerance;
            s.minValue = 0;
            s.maxValue = MaxTolerance;
            s.useCriticalThreshold = true;
            s.criticalThreshold = 0;
            s.onCritical = new List<Consequence>
            {
                new Consequence { type = ConsequenceType.ShowNarrative, text = "El Comité perdió la paciencia con vos." },
                new Consequence { type = ConsequenceType.SetFlag, flag = FiredFlag },
                new Consequence { type = ConsequenceType.EndCampaign, immediate = true }
            };
        }

        static void ConfigureConditionPoints(CampaignConfig config, StatData tol)
        {
            var g = config.conditionGenerator;
            g.onSuccess = new List<Consequence> { Stat(tol, ConditionMet) };
            g.onFail = new List<Consequence>
            {
                Stat(tol, ConditionFailed),
                new Consequence { type = ConsequenceType.ShowNarrative, text = "El Comité no está conforme con tu jornada." }
            };
            g.onDemandFulfilled = new List<Consequence> { Stat(tol, DemandFulfilled) };
            g.onDemandIgnored = new List<Consequence> { Stat(tol, DemandIgnored) };
        }

        static Consequence Stat(StatData s, int amount) => new Consequence { type = ConsequenceType.ModifyStat, stat = s, amount = amount };

        /// <summary>El despido ahora lo decide la Tolerancia del Comité, no las regiones.</summary>
        static void RemoveRegionFiring(CampaignConfig config)
        {
            foreach (var r in config.regions.Where(r => r != null))
            {
                bool hadFiring = r.onCritical.Any(c => c.type == ConsequenceType.SetFlag && c.flag == FiredFlag);
                if (!hadFiring) continue;
                r.onCritical.Clear();
                EditorUtility.SetDirty(r);
                AssetDatabase.SaveAssetIfDirty(r);
            }
        }

        // =====================================================================
        // Reacciones
        // =====================================================================
        static void ApplyReactions(ProposalDatabase db)
        {
            if (!File.Exists(ReactionsPath)) { Debug.LogWarning($"[Voyager] No existe {ReactionsPath}."); return; }

            string category = null;
            int applied = 0, lineNumber = 0;
            foreach (var raw in File.ReadAllLines(ReactionsPath, Encoding.UTF8))
            {
                lineNumber++;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]")) { category = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant(); continue; }

                int colon = line.IndexOf(':');
                int eq = line.IndexOf('=');
                if (colon < 0 || eq < colon) { Debug.LogWarning($"[Voyager] Reacciones.txt línea {lineNumber}: formato 'region: Nombre = reacción'."); continue; }

                string region = line.Substring(0, colon).Trim().ToLowerInvariant();
                string name = Norm(line.Substring(colon + 1, eq - colon - 1));
                string reaction = line.Substring(eq + 1).Trim();

                var p = db.proposals.FirstOrDefault(x => x != null && x.category != null && x.region != null &&
                                                         x.category.Id == category && x.region.Id == region && Norm(x.DisplayName) == name);
                if (p == null)
                {
                    Debug.LogWarning($"[Voyager] Reacciones.txt línea {lineNumber}: no existe la propuesta '{name}' ({category}/{region}).");
                    continue;
                }
                if (p.alienReaction != reaction)
                {
                    p.alienReaction = reaction;
                    EditorUtility.SetDirty(p);
                    AssetDatabase.SaveAssetIfDirty(p);
                }
                applied++;
            }
            Debug.Log($"[Voyager] Reacciones alienígenas: {applied} propuestas con reacción.");
        }

        // =====================================================================
        // Finales
        // =====================================================================
        class EndingDef
        {
            public string key, title;
            public int priority, line;
            public LogicMode mode = LogicMode.All;
            public bool reactions = true, isDefault;
            public readonly List<string> text = new List<string>();
            public readonly List<Requirement> reqs = new List<Requirement>();
        }

        static readonly Regex OpRegex = new Regex(@"^(.*?)\s*(>=|<=|==|!=|>|<)\s*(-?\d+)\s*$");

        static void ApplyEndings(CampaignConfig config)
        {
            if (!File.Exists(EndingsPath)) { Debug.LogWarning($"[Voyager] No existe {EndingsPath}."); return; }

            var defs = ParseEndings(config);
            Directory.CreateDirectory($"{Root}/Endings");
            AssetDatabase.Refresh();

            var endings = new List<EndingData>();
            EndingData defaultEnding = null;
            foreach (var d in defs)
            {
                int created = 0;
                var e = GameContentSetup.LoadOrCreate<EndingData>($"{Root}/Endings/Ending_{d.key}.asset", x => Fill(x, d), ref created);
                if (created == 0) { Fill(e, d); EditorUtility.SetDirty(e); AssetDatabase.SaveAssetIfDirty(e); }
                endings.Add(e);
                if (d.isDefault) defaultEnding = e;
            }

            config.endings = endings;
            if (defaultEnding != null) config.defaultEnding = defaultEnding;
            Debug.Log($"[Voyager] Finales: {endings.Count} cargados desde Finales.txt" +
                      (defaultEnding != null ? $" (por defecto: {defaultEnding.DisplayName})." : ". ⚠ Ninguno tiene 'defecto: si'."));
        }

        static void Fill(EndingData e, EndingDef d)
        {
            e.id = d.key.ToLowerInvariant();
            e.displayName = d.title ?? d.key;
            e.description = string.Join("\n", d.text);
            e.priority = d.priority;
            e.showAlienReactions = d.reactions;
            e.requirements = new RequirementGroup { mode = d.mode, requirements = new List<Requirement>(d.reqs) };
        }

        static List<EndingDef> ParseEndings(CampaignConfig config)
        {
            var defs = new List<EndingDef>();
            EndingDef cur = null;
            int lineNumber = 0;

            foreach (var raw in File.ReadAllLines(EndingsPath, Encoding.UTF8))
            {
                lineNumber++;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string key = Regex.Replace(line.Substring(1, line.Length - 2).Trim(), @"[^A-Za-z0-9_]", "");
                    cur = new EndingDef { key = key, line = lineNumber };
                    defs.Add(cur);
                    continue;
                }
                if (cur == null) continue;

                int colon = line.IndexOf(':');
                if (colon < 0) { Warn(lineNumber, "falta ':'"); continue; }
                string k = line.Substring(0, colon).Trim().ToLowerInvariant();
                string v = line.Substring(colon + 1).Trim();

                switch (k)
                {
                    case "titulo": cur.title = v; break;
                    case "prioridad": int.TryParse(v, out cur.priority); break;
                    case "modo": cur.mode = v.ToLowerInvariant().StartsWith("alg") ? LogicMode.Any : LogicMode.All; break;
                    case "texto": cur.text.Add(v); break;
                    case "reacciones": cur.reactions = !v.ToLowerInvariant().StartsWith("n"); break;
                    case "defecto": cur.isDefault = v.ToLowerInvariant().StartsWith("s"); break;
                    default:
                        var req = ParseRequirement(k, v, config, lineNumber);
                        if (req != null) cur.reqs.Add(req);
                        break;
                }
            }
            return defs;
        }

        static Requirement ParseRequirement(string k, string v, CampaignConfig config, int lineNumber)
        {
            switch (k)
            {
                case "aprobada":
                case "rechazada":
                {
                    var p = FindProposal(config.database, v, lineNumber);
                    if (p == null) return null;
                    return new Requirement { type = k == "aprobada" ? RequirementType.ProposalApproved : RequirementType.ProposalRejected, proposal = p };
                }
                case "flag":     return new Requirement { type = RequirementType.FlagSet, flag = v };
                case "sin_flag": return new Requirement { type = RequirementType.FlagNotSet, flag = v };
            }

            var m = OpRegex.Match(v);
            if (!m.Success) { Warn(lineNumber, $"no entiendo '{v}' (esperaba algo como 'eeuu >= 7')"); return null; }
            string target = m.Groups[1].Value.Trim().ToLowerInvariant();
            var op = ParseOp(m.Groups[2].Value);
            int value = int.Parse(m.Groups[3].Value);

            switch (k)
            {
                case "aprobadas_de_region":
                {
                    var r = config.regions.FirstOrDefault(x => x != null && x.Id == target);
                    if (r == null) { Warn(lineNumber, $"región '{target}' no existe"); return null; }
                    return new Requirement { type = RequirementType.TotalApprovedFromRegion, region = r, comparison = op, value = value };
                }
                case "aprobadas_de_categoria":
                {
                    var c = config.categories.FirstOrDefault(x => x != null && x.Id == target);
                    if (c == null) { Warn(lineNumber, $"categoría '{target}' no existe"); return null; }
                    return new Requirement { type = RequirementType.TotalApprovedInCategory, category = c, comparison = op, value = value };
                }
                case "relacion":
                {
                    var r = config.regions.FirstOrDefault(x => x != null && x.Id == target);
                    if (r == null) { Warn(lineNumber, $"región '{target}' no existe"); return null; }
                    return new Requirement { type = RequirementType.RegionRelation, region = r, comparison = op, value = value };
                }
                case "alguna_region":
                    return new Requirement { type = RequirementType.AnyRegionRelation, comparison = op, value = value };
                case "stat":
                {
                    var s = config.stats.FirstOrDefault(x => x != null && x.Id == target);
                    if (s == null) { Warn(lineNumber, $"stat '{target}' no existe"); return null; }
                    return new Requirement { type = RequirementType.StatValue, stat = s, comparison = op, value = value };
                }
            }
            Warn(lineNumber, $"requisito desconocido '{k}'");
            return null;
        }

        /// <summary>"Nombre" o "categoria/Nombre" (para desambiguar nombres repetidos).</summary>
        static ProposalData FindProposal(ProposalDatabase db, string text, int lineNumber)
        {
            string category = null, name = text;
            int slash = text.IndexOf('/');
            if (slash > 0 && !text.Substring(0, slash).Contains(" "))
            {
                category = text.Substring(0, slash).Trim().ToLowerInvariant();
                name = text.Substring(slash + 1);
            }
            name = Norm(name);

            var matches = db.proposals.Where(p => p != null && Norm(p.DisplayName) == name &&
                                                  (category == null || (p.category != null && p.category.Id == category))).ToList();
            if (matches.Count == 1) return matches[0];
            if (matches.Count == 0) Warn(lineNumber, $"no existe la propuesta '{text}'");
            else Warn(lineNumber, $"'{text}' está repetida ({string.Join(", ", matches.Select(p => p.category?.Id))}). Usá categoria/Nombre");
            return null;
        }

        static Comparison ParseOp(string s)
        {
            switch (s)
            {
                case ">=": return Comparison.GreaterOrEqual;
                case ">":  return Comparison.Greater;
                case "==": return Comparison.Equal;
                case "!=": return Comparison.NotEqual;
                case "<=": return Comparison.LessOrEqual;
                default:   return Comparison.Less;
            }
        }

        static string Norm(string s) => Regex.Replace(s.Trim(), @"\s+", " ").ToLowerInvariant();
        static void Warn(int line, string msg) => Debug.LogWarning($"[Voyager] Finales.txt línea {line}: {msg}.");
    }
}
