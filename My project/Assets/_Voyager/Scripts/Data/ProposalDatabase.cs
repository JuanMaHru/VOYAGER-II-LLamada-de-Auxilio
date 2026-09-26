using System.Collections.Generic;
using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// La "baraja" completa: todas las propuestas del juego.
    /// No hace falta arrastrarlas a mano: en el Inspector hay un botón que junta
    /// todas las ProposalData de la carpeta de este asset y sus subcarpetas.
    /// </summary>
    [CreateAssetMenu(menuName = "Voyager/Proposal Database", fileName = "ProposalDatabase")]
    public class ProposalDatabase : ScriptableObject
    {
        public List<ProposalData> proposals = new List<ProposalData>();

        public ProposalData FindById(string proposalId)
        {
            foreach (var p in proposals)
                if (p != null && p.Id == proposalId) return p;
            return null;
        }
    }
}
