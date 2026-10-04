using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Emerge.Checks.Divination
{
    [Serializable]
    public sealed class CheckTraceEntry
    {
        public int sequence;
        public string stage;
        public string message;
        public string utc;
    }

    public static class CheckLogicTrace
    {
        [Serializable] private sealed class LogRow
        {
            public string sessionId, eventId, contextId;
            public CheckTraceEntry entry;
        }
        private static bool fileWarningShown;
        public static string LogPath
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/RuntimeLogs/check-logic.jsonl"));
#else
                return Path.Combine(Application.persistentDataPath, "Logs/check-logic.jsonl");
#endif
            }
        }

        // One row per logical transition. Animation frames never generate new game results or log rows.
        public static void Record(CheckSession session, string stage, string message)
        {
            if (session == null) return;
            if (session.logicTrace == null) session.logicTrace = new List<CheckTraceEntry>();
            var entry = new CheckTraceEntry { sequence = session.logicTrace.Count + 1, stage = stage,
                message = message, utc = DateTime.UtcNow.ToString("O") };
            session.logicTrace.Add(entry);
            string identity = string.IsNullOrEmpty(session.sessionId) ? "?" : session.sessionId.Substring(0, Math.Min(8, session.sessionId.Length));
            Debug.Log("[检定链 " + identity + "][" + stage + "] " + message);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.AppendAllText(LogPath, JsonUtility.ToJson(new LogRow { sessionId = session.sessionId,
                    eventId = session.eventId, contextId = session.contextId, entry = entry }) + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                if (fileWarningShown) return;
                fileWarningShown = true;
                Debug.LogWarning("[检定链] 日志文件写入失败，Console 仍记录逻辑链：" + exception.Message);
            }
        }
    }
}
