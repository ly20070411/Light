using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Emerge.Battle
{
    public static class BattleLogicTrace
    {
        [Serializable] private sealed class Entry
        { public string utc, sessionId, message; public int version, round, seed; }
        private static bool reportedFailure;
        public static void Record(BattleSession session, string message)
        {
            try
            {
                string root = Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/RuntimeLogs")) : Path.Combine(Application.persistentDataPath, "RuntimeLogs");
                Directory.CreateDirectory(root);
                File.AppendAllText(Path.Combine(root, "battle-logic.jsonl"), JsonUtility.ToJson(new Entry
                { utc = DateTime.UtcNow.ToString("O"), sessionId = session.sessionId, version = session.version, round = session.round, seed = session.seed, message = message }) + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                if (!reportedFailure) { reportedFailure = true; Debug.LogWarning("[战斗链] 文件日志无法写入，控制台继续记录：" + exception.Message); }
            }
        }
    }
}
