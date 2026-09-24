using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// Logs de gameplay con formato legible. Se activan/desactivan con
    /// CampaignManager.enableDebugLogs. Los warnings y errores se muestran siempre.
    /// </summary>
    public static class GameLog
    {
        public static bool Enabled = true;

        public static void Info(string message)
        {
            if (Enabled) Debug.Log(message);
        }

        public static void Warn(string message) => Debug.LogWarning("[Voyager] " + message);
        public static void Error(string message) => Debug.LogError("[Voyager] " + message);
    }
}
