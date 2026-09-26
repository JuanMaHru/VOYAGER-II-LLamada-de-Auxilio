using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// Base de todo el contenido del juego (regiones, stats, categorías, propuestas, condiciones, finales).
    /// Aporta un ID estable (para guardado y logs) y un nombre visible.
    ///
    /// REGLA: los ScriptableObjects son datos de SOLO LECTURA en runtime.
    /// El estado que cambia durante la partida vive en GameState, nunca acá.
    /// </summary>
    public abstract class ContentAsset : ScriptableObject
    {
        [Tooltip("ID único y estable. Se usa para guardar partidas. Si queda vacío se usa el nombre del asset " +
                 "(en ese caso, renombrar el asset rompería partidas guardadas).")]
        public string id;

        [Tooltip("Nombre que ve el jugador. Si queda vacío se usa el nombre del asset.")]
        public string displayName;

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public override string ToString() => DisplayName;
    }
}
