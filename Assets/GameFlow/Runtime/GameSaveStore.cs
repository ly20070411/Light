using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Emerge.GameFlow
{
    // Checksummed JSON, atomic replacement, and one previous valid file per slot.
    public sealed class GameSaveStore
    {
        [Serializable]
        private sealed class Envelope
        {
            public int format = 1;
            public string payload;
            public string checksum;
        }

        public string DirectoryPath { get; }
        public GameSaveStore(string directory) { DirectoryPath = directory; }
        public string SlotPath(SaveSlot slot) => Path.Combine(DirectoryPath, slot == SaveSlot.Manual ? "manual.json" : "auto.json");

        public bool TryWrite(SaveSlot slot, GameSaveData data, out string error)
        {
            error = null;
            string path = SlotPath(slot), temporary = path + ".tmp";
            try
            {
                Validate(data);
                Directory.CreateDirectory(DirectoryPath);
                string payload = JsonUtility.ToJson(data);
                string json = JsonUtility.ToJson(new Envelope { payload = payload, checksum = Hash(payload) }, true);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path))
                {
                    // Do not replace a valid backup with a corrupt primary file.
                    string backup = TryReadFile(path, out _, out _) ? path + ".bak" : null;
                    File.Replace(temporary, path, backup);
                }
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception exception)
            {
                error = "保存失败：" + exception.Message;
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
                return false;
            }
        }

        public bool TryRead(SaveSlot slot, out GameSaveData data, out string message)
        {
            string path = SlotPath(slot);
            if (TryReadFile(path, out data, out string primaryError)) { message = ""; return true; }
            if (TryReadFile(path + ".bak", out data, out _))
            { message = "主存档不可用，已恢复上一份备份。"; return true; }
            message = primaryError;
            return false;
        }

        public bool TryLatest(out GameSaveData data, out SaveSlot slot, out string message)
        {
            bool manualOk = TryRead(SaveSlot.Manual, out var manual, out var manualMessage);
            bool autoOk = TryRead(SaveSlot.Auto, out var auto, out var autoMessage);
            slot = manualOk && (!autoOk || manual.savedUtcTicks >= auto.savedUtcTicks) ? SaveSlot.Manual : SaveSlot.Auto;
            data = slot == SaveSlot.Manual ? manual : auto;
            message = slot == SaveSlot.Manual ? manualMessage : autoMessage;
            if (!manualOk && !autoOk) { data = null; message = "没有可读取的有效存档。"; return false; }
            return true;
        }

        private static bool TryReadFile(string path, out GameSaveData data, out string error)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) { error = "暂无存档"; return false; }
                var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path, Encoding.UTF8));
                if (envelope == null || envelope.format != 1 || string.IsNullOrEmpty(envelope.payload) ||
                    !string.Equals(envelope.checksum, Hash(envelope.payload), StringComparison.Ordinal))
                    throw new InvalidDataException("存档校验未通过");
                var parsed = JsonUtility.FromJson<GameSaveData>(envelope.payload);
                Validate(parsed);
                data = parsed;
                error = "";
                return true;
            }
            catch (Exception exception) { error = "读取失败：" + exception.Message; return false; }
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }

        public static bool IsValidData(GameSaveData data)
        { try { Validate(data); return true; } catch (Exception) { return false; } }
        private static void Validate(GameSaveData data)
        {
            if (data == null || data.version != 2 || data.savedUtcTicks <= 0 || data.savedUtcTicks > DateTime.MaxValue.Ticks ||
                string.IsNullOrWhiteSpace(data.scenePath) || !Finite(data.playedSeconds) || data.playedSeconds < 0 ||
                data.actors == null || data.actors.Count == 0 || data.actors.Count > 10000 || string.IsNullOrEmpty(data.playerId))
                throw new InvalidDataException("不支持的存档版本或数据不完整");
            if (data.actors.Any(actor => actor == null || string.IsNullOrWhiteSpace(actor.id) || !Emerge.Props.PropGameState.IsValidSnapshot(actor.propState) ||
                (actor.checkState != null && !Emerge.Checks.CheckActorState.IsValidSnapshot(actor.checkState)) ||
                (actor.battleState != null && !Emerge.Battle.BattleEngine.ValidateSnapshot(actor.battleState)) ||
                !ValidVector(actor.position)) ||
                data.actors.Select(actor => actor.id).Distinct().Count() != data.actors.Count ||
                !data.actors.Any(actor => actor.id == data.playerId))
                throw new InvalidDataException("角色存档数据无效");
            if (!data.actors.Any(actor => actor.id == data.playerId && actor.active) || data.props == null || data.props.Count > 100000 ||
                data.props.Any(prop => prop == null || string.IsNullOrWhiteSpace(prop.instanceId) || string.IsNullOrWhiteSpace(prop.definitionId) ||
                    !ValidVector(prop.position) || !Finite(prop.rotationZ) || !ValidVector(prop.localScale)) ||
                data.props.Select(prop => prop.instanceId).Distinct().Count() != data.props.Count)
                throw new InvalidDataException("道具存档数据无效");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool ValidVector(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
