using System;
using System.Collections.Generic;

namespace Voyager
{
    /// <summary>Lo que se le pide al mazo para un día.</summary>
    public class DrawRequest
    {
        public int count = 5;
        public CategoryData category;              // null = cualquier categoría
        public bool distinctRegions = true;
        public List<RegionData> allowedRegions;    // null o vacío = todas

        // D4: garantías para que la condición del día no sea imposible.
        // Una región repetida = hacen falta varias propuestas de esa región.
        public List<RegionData> mustIncludeRegions = new List<RegionData>();
        public List<CategoryData> mustIncludeCategories = new List<CategoryData>();
    }

    /// <summary>
    /// La "baraja". Filtra la base de datos y sortea las propuestas del día.
    ///
    ///   Base de datos
    ///     → quitar nulas / peso 0
    ///     → quitar ya usadas (UsedProposals)
    ///     → quitar las que no cumplen su "availability"
    ///     → filtro de regiones permitidas del día
    ///     → filtros personalizados (CustomFilters)
    ///     → garantías de la condición
    ///     → categoría + regiones distintas (con relajación progresiva, D5)
    ///     → sorteo ponderado por "weight"
    /// </summary>
    public class ProposalDeck
    {
        readonly ProposalDatabase database;

        /// <summary>
        /// Punto de extensión: agregá acá filtros nuevos sin tocar el resto del mazo.
        /// Ej: deck.CustomFilters.Add((p, state) => !p.name.Contains("Beta"));
        /// </summary>
        public readonly List<Func<ProposalData, GameState, bool>> CustomFilters = new List<Func<ProposalData, GameState, bool>>();

        public ProposalDeck(ProposalDatabase database) => this.database = database;

        /// <summary>Propuestas que podrían salir ahora mismo (sin mirar categoría).</summary>
        public List<ProposalData> GetAvailable(GameState state, List<RegionData> allowedRegions = null)
        {
            var result = new List<ProposalData>();
            if (database == null) return result;

            foreach (var p in database.proposals)
            {
                if (p == null || p.weight <= 0f) continue;
                if (state.IsProposalUsed(p)) continue;
                if (p.region != null && !state.HasRegion(p.region)) continue; // región no activa en esta campaña
                if (allowedRegions != null && allowedRegions.Count > 0 && !allowedRegions.Contains(p.region)) continue;
                if (!p.availability.Evaluate(state)) continue;

                bool ok = true;
                foreach (var f in CustomFilters)
                    if (!f(p, state)) { ok = false; break; }
                if (ok) result.Add(p);
            }
            return result;
        }

        public int CountAvailable(GameState state, CategoryData category, List<RegionData> allowedRegions = null)
        {
            int n = 0;
            foreach (var p in GetAvailable(state, allowedRegions))
                if (category == null || p.category == category) n++;
            return n;
        }

        /// <summary>
        /// Sortea una categoría del pool. Prioriza las que tienen propuestas suficientes
        /// y (opcionalmente) las que todavía no salieron en la partida.
        /// </summary>
        public CategoryData ChooseCategory(IList<CategoryData> pool, GameState state, int needed,
                                           List<RegionData> allowedRegions, bool avoidRepeating, Random rng)
        {
            var enough = new List<CategoryData>();
            var enoughUnused = new List<CategoryData>();
            CategoryData best = null;
            int bestCount = -1;

            foreach (var c in pool)
            {
                if (c == null) continue;
                int n = CountAvailable(state, c, allowedRegions);
                if (n > bestCount) { best = c; bestCount = n; }
                if (n >= needed)
                {
                    enough.Add(c);
                    if (!state.IsCategoryUsed(c)) enoughUnused.Add(c);
                }
            }

            if (avoidRepeating && enoughUnused.Count > 0) return enoughUnused[rng.Next(enoughUnused.Count)];
            if (enough.Count > 0) return enough[rng.Next(enough.Count)];

            if (best != null)
                GameLog.Warn($"Ninguna categoría tiene {needed} propuestas disponibles. Se usa '{best.DisplayName}' ({bestCount}).");
            return best;
        }

        public List<ProposalData> Draw(DrawRequest req, GameState state, Random rng)
        {
            var basePool = GetAvailable(state, req.allowedRegions);
            var picks = new List<ProposalData>();

            // 1) Garantías (D4)
            foreach (var region in req.mustIncludeRegions)
            {
                if (picks.Count >= req.count) break;
                var r = region;
                var p = PickOne(basePool, picks, req, x => x.region == r, rng, ignoreDistinct: true);
                if (p != null) picks.Add(p);
                else GameLog.Warn($"No hay propuestas disponibles de '{r?.DisplayName}' para garantizar la condición del día.");
            }
            foreach (var category in req.mustIncludeCategories)
            {
                if (picks.Count >= req.count) break;
                var c = category;
                var p = PickOne(basePool, picks, req, x => x.category == c, rng, ignoreDistinct: true);
                if (p != null) picks.Add(p);
                else GameLog.Warn($"No hay propuestas disponibles de '{c?.DisplayName}' para garantizar la condición del día.");
            }

            // 2) Relleno normal
            while (picks.Count < req.count)
            {
                var p = PickOne(basePool, picks, req, null, rng, ignoreDistinct: false);
                if (p == null) break;
                picks.Add(p);
            }

            if (picks.Count < req.count)
                GameLog.Warn($"Solo se pudieron generar {picks.Count}/{req.count} propuestas: el mazo no tiene más disponibles.");

            Shuffle(picks, rng); // para que las garantizadas no queden siempre primeras
            return picks;
        }

        /// <summary>
        /// Elige una propuesta relajando filtros en orden (D5):
        /// 0) categoría + región distinta  1) categoría  2) región distinta  3) sin filtros.
        /// Nunca repite propuestas.
        /// </summary>
        ProposalData PickOne(List<ProposalData> basePool, List<ProposalData> picks, DrawRequest req,
                             Func<ProposalData, bool> extra, Random rng, bool ignoreDistinct)
        {
            for (int stage = 0; stage < 4; stage++)
            {
                bool useCategory = stage < 2;
                bool distinct = stage % 2 == 0;

                if (distinct && (ignoreDistinct || !req.distinctRegions)) continue;
                if (!useCategory && req.category == null) continue; // sería igual a un stage anterior

                var candidates = new List<ProposalData>();
                foreach (var p in basePool)
                {
                    if (picks.Contains(p)) continue;
                    if (extra != null && !extra(p)) continue;
                    if (useCategory && req.category != null && p.category != req.category) continue;
                    if (distinct && ContainsRegion(picks, p.region)) continue;
                    candidates.Add(p);
                }

                if (candidates.Count == 0) continue;

                if (extra == null && stage > 0)
                {
                    bool relaxedDistinct = req.distinctRegions && !distinct;
                    bool relaxedCategory = req.category != null && !useCategory;
                    if (relaxedDistinct || relaxedCategory)
                        GameLog.Warn("Mazo: filtros relajados para completar el día → " +
                                     (relaxedDistinct ? "se permiten regiones repetidas " : "") +
                                     (relaxedCategory ? $"se usan propuestas fuera de '{req.category.DisplayName}'" : ""));
                }
                return WeightedPick(candidates, rng);
            }
            return null;
        }

        static bool ContainsRegion(List<ProposalData> list, RegionData region)
        {
            foreach (var p in list) if (p.region == region) return true;
            return false;
        }

        static ProposalData WeightedPick(List<ProposalData> list, Random rng)
        {
            float total = 0f;
            foreach (var p in list) total += p.weight;
            float roll = (float)rng.NextDouble() * total;
            foreach (var p in list)
            {
                roll -= p.weight;
                if (roll <= 0f) return p;
            }
            return list[list.Count - 1];
        }

        static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
