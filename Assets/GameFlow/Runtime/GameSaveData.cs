using System;
using System.Collections.Generic;
using Emerge.Props;
using UnityEngine;

namespace Emerge.GameFlow
{
    [Serializable] public sealed class SavedActor
    {
        public string id;
        public Vector3 position;
        public bool active = true;
        public PropGameState.Snapshot propState;
        // Optional for older version-2 saves and actors without checks.
        [SerializeReference] public Emerge.Checks.CheckActorState.Snapshot checkState;
        [SerializeReference] public Emerge.Battle.BattleSnapshot battleState;
        [SerializeReference] public Emerge.Battle.PointBattleSnapshot pointBattleState;
    }
    [Serializable] public sealed class SavedProp
    {
        public string instanceId;
        public string definitionId;
        public Vector3 position;
        public float rotationZ;
        public Vector3 localScale = Vector3.one;
        public bool active = true;
    }
    [Serializable] public sealed class GameSaveData
    {
        public int version = 2;
        public long savedUtcTicks;
        public string scenePath;
        public float playedSeconds;
        public string reason;
        public string playerId;
        public List<SavedActor> actors = new List<SavedActor>();
        public List<SavedProp> props = new List<SavedProp>();
    }
    public enum SaveSlot { Manual, Auto }
    public enum GameSessionPhase { MainMenu, Loading, Playing, Settings, CharacterCreation }
}
