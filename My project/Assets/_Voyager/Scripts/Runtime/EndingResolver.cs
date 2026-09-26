using System.Collections.Generic;
using System.Text;

namespace Voyager
{
    /// <summary>
    /// Elige el final: ordena por prioridad (mayor primero; a igualdad, orden de la lista)
    /// y devuelve el primero cuyos requisitos se cumplen. Si ninguno, el final por defecto.
    /// </summary>
    public static class EndingResolver
    {
        public static EndingData Resolve(IList<EndingData> endings, EndingData defaultEnding, GameState state)
        {
            var ordered = new List<EndingData>();
            foreach (var e in endings) if (e != null) ordered.Add(e);

            // Ordenamiento estable: List.Sort no lo es, así que desempatamos por índice original.
            var index = new Dictionary<EndingData, int>();
            for (int i = 0; i < ordered.Count; i++) index[ordered[i]] = i;
            ordered.Sort((a, b) => a.priority != b.priority ? b.priority.CompareTo(a.priority) : index[a].CompareTo(index[b]));

            var log = new StringBuilder("<b>[FINALES]</b> Evaluando por prioridad:\n");
            EndingData chosen = null;

            foreach (var e in ordered)
            {
                bool ok = e.requirements.Evaluate(state);
                log.AppendLine($"{(ok ? "✔" : "✘")} {e.DisplayName} (prioridad {e.priority})");
                log.AppendLine(e.requirements.DescribeWithResults(state));
                if (ok) { chosen = e; break; }
            }

            if (chosen == null)
            {
                chosen = defaultEnding;
                log.AppendLine($"Ningún final se cumplió → final por defecto: {(chosen != null ? chosen.DisplayName : "(¡no asignado!)")}");
                if (chosen == null) GameLog.Error("CampaignConfig no tiene defaultEnding asignado.");
            }
            else
            {
                log.AppendLine($"→ Final elegido: {chosen.DisplayName}");
            }

            GameLog.Info(log.ToString());
            return chosen;
        }
    }
}
