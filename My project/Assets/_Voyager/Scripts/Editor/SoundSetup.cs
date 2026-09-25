using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Crea (o actualiza) Assets/_Voyager/Resources/SoundBank.asset y le asigna los sonidos de
    /// Assets/Audio según su nombre de archivo. Menú: Voyager → Conectar sonidos.
    /// Para cambiar un sonido: reemplazá el archivo con el mismo nombre, o arrastrá otro al SoundBank.
    /// </summary>
    public static class SoundSetup
    {
        const string AudioFolder = "Assets/Audio";
        const string ResourcesFolder = "Assets/_Voyager/Resources";
        const string BankPath = ResourcesFolder + "/SoundBank.asset";

        [MenuItem("Voyager/Conectar sonidos (Assets/Audio)", priority = 7)]
        public static void Connect()
        {
            if (!AssetDatabase.IsValidFolder(AudioFolder))
            {
                EditorUtility.DisplayDialog("Sonidos", $"No existe la carpeta {AudioFolder}.", "OK");
                return;
            }

            Directory.CreateDirectory(ResourcesFolder);
            AssetDatabase.Refresh();

            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null)
                .ToList();

            // Nombre exacto del archivo (sin importar mayúsculas): "Aprobar" ≠ "Desaprobar".
            AudioClip Find(string fileName) => clips.FirstOrDefault(c => c.name.ToLowerInvariant() == fileName.ToLowerInvariant());

            int created = 0;
            var bank = GameContentSetup.LoadOrCreate<SoundBank>(BankPath, _ => { }, ref created);
            Undo.RecordObject(bank, "Conectar sonidos");
            bank.approve  = Find("Aprobar")      ?? bank.approve;
            bank.reject   = Find("Desaprobar")   ?? bank.reject;
            bank.click    = Find("click")        ?? bank.click;
            bank.rollover = Find("rollover")     ?? bank.rollover;
            bank.dayStart = Find("EmpiezaElDia") ?? bank.dayStart;
            bank.dayEnd   = Find("FinalDelDia")  ?? bank.dayEnd;
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssetIfDirty(bank);

            string Show(string label, AudioClip c) => $"  {(c != null ? "✔" : "✘")} {label}: {(c != null ? c.name : "(falta)")}";
            Debug.Log("[Voyager] Sonidos conectados en " + BankPath + "\n" +
                      Show("Aprobar", bank.approve) + "\n" + Show("Desaprobar", bank.reject) + "\n" +
                      Show("Click", bank.click) + "\n" + Show("Rollover", bank.rollover) + "\n" +
                      Show("Empieza el día", bank.dayStart) + "\n" + Show("Final del día", bank.dayEnd));
            Selection.activeObject = bank;
        }
    }
}
