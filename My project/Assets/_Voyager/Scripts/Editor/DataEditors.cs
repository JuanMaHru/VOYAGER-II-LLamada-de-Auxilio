using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Voyager.EditorTools
{
    /// <summary>ProposalDatabase: botón para juntar todas las propuestas + resumen por categoría/región.</summary>
    [CustomEditor(typeof(ProposalDatabase))]
    public class ProposalDatabaseEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var db = (ProposalDatabase)target;

            if (GUILayout.Button("Buscar propuestas (esta carpeta y subcarpetas)", GUILayout.Height(28)))
                CollectAll(db);

            var valid = db.proposals.Where(p => p != null).ToList();
            EditorGUILayout.HelpBox($"{valid.Count} propuestas.\n" +
                "Por categoría: " + string.Join(", ", valid.GroupBy(p => p.CategoryName).Select(g => $"{g.Key} ({g.Count()})")) + "\n" +
                "Por región: " + string.Join(", ", valid.GroupBy(p => p.RegionName).Select(g => $"{g.Key} ({g.Count()})")),
                MessageType.Info);

            DrawDefaultInspector();
        }

        /// <summary>
        /// Junta todas las ProposalData que estén en la carpeta de la base de datos y sus subcarpetas.
        /// (Así el contenido de ejemplo y el contenido real no se mezclan.)
        /// </summary>
        public static void CollectAll(ProposalDatabase db)
        {
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(db)).Replace('\\', '/');
            Undo.RecordObject(db, "Buscar propuestas");
            db.proposals = AssetDatabase.FindAssets("t:ProposalData", new[] { folder })
                .Select(g => AssetDatabase.LoadAssetAtPath<ProposalData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null)
                .OrderBy(p => p.CategoryName).ThenBy(p => p.RegionName).ThenBy(p => p.name)
                .ToList();
            EditorUtility.SetDirty(db);
            Debug.Log($"[Voyager] ProposalDatabase: {db.proposals.Count} propuestas encontradas en {folder}/.");
        }
    }

    /// <summary>CampaignConfig: botón de validación.</summary>
    [CustomEditor(typeof(CampaignConfig))]
    public class CampaignConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (CampaignConfig)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validar contenido", GUILayout.Height(26))) ContentValidator.Validate(config);
                if (config.database != null && GUILayout.Button("Actualizar base de propuestas", GUILayout.Height(26)))
                    ProposalDatabaseEditor.CollectAll(config.database);
            }
            EditorGUILayout.HelpBox($"Días totales: {config.TotalDays} · Propuestas necesarias: {config.days.Sum(d => d.proposalCount)} · " +
                                    $"En la base: {(config.database != null ? config.database.proposals.Count : 0)}", MessageType.None);
            DrawDefaultInspector();
        }
    }
}
