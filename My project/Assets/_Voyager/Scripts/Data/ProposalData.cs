using System.Collections.Generic;
using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// Una propuesta que llega al escritorio del presidente.
    /// Agregar contenido = Create → Voyager → Proposal, completar y listo.
    /// </summary>
    [CreateAssetMenu(menuName = "Voyager/Proposal", fileName = "Proposal_")]
    public class ProposalData : ContentAsset
    {
        [Header("Contenido")]
        [TextArea(3, 8)] public string description;
        public RegionData region;
        public CategoryData category;
        public Sprite image;

        [Tooltip("Texto opcional para eventos especiales (la UI decide cómo mostrarlo).")]
        [TextArea] public string specialEventText;

        [Tooltip("Qué hacen los alienígenas cuando encuentran esta propuesta en la sonda. " +
                 "Se muestra en la pantalla final si fue aprobada. Se carga desde Reacciones.txt.")]
        [TextArea] public string alienReaction;

        [Header("Efectos")]
        [Tooltip("Qué pasa al APROBAR. Ej: Modificar región Europa +10, Modificar stat Culture +5.")]
        public List<Consequence> onApprove = new List<Consequence>();

        [Tooltip("Qué pasa al RECHAZAR.")]
        public List<Consequence> onReject = new List<Consequence>();

        [Header("Aparición en el mazo")]
        [Tooltip("Probabilidad relativa de salir sorteada. 1 = normal, 2 = el doble, 0 = nunca por sorteo.")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("Requisitos para que la propuesta pueda aparecer (ej: Día actual >= 3, Flag 'x' activo). " +
                 "Vacío = siempre disponible.")]
        public RequirementGroup availability = new RequirementGroup();

        public List<Consequence> GetEffects(bool approved) => approved ? onApprove : onReject;

        public string RegionName => region != null ? region.DisplayName : "(sin región)";
        public string CategoryName => category != null ? category.DisplayName : "(sin categoría)";
    }
}
