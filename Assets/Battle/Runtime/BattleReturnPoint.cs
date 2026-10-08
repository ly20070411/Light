using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.Checks;
using Emerge.GameFlow;
using Emerge.Props;
using PixelPrototype;
using UnityEngine;

namespace Emerge.Battle
{
    [Serializable]
    public sealed class BattleReturnPoint
    {
        public int version = 1;
        public string scenePath;
        public Vector3 position, localScale;
        public float rotationZ;
        public PropGameState.Snapshot propState;
        [SerializeReference] public CheckActorState.Snapshot checkState;
        [SerializeReference] public GameSaveData world;
        public List<SavedProp> props = new List<SavedProp>();

        public static BattleReturnPoint Capture(Component actor)
        {
            var point = new BattleReturnPoint { scenePath = actor.gameObject.scene.path, position = actor.transform.position,
                rotationZ = actor.transform.eulerAngles.z, localScale = actor.transform.localScale,
                propState = actor.GetComponent<PropGameState>().CaptureSnapshot(),
                checkState = actor.GetComponent<CheckActorState>()?.CaptureSnapshot() };
            var body = actor.GetComponent<Rigidbody2D>(); if (body != null) { point.position.x = body.position.x; point.position.y = body.position.y; }
            if (GameSessionController.Instance != null) point.world = GameSessionController.Instance.CaptureGame("进入战斗前的回退点");
            else foreach (var prop in PropInstance.Instances.Where(p => p != null && p.gameObject.scene == actor.gameObject.scene && p.Definition != null))
                point.props.Add(new SavedProp { instanceId = prop.InstanceId, definitionId = prop.Definition.Id, position = prop.transform.position,
                    rotationZ = prop.transform.eulerAngles.z, localScale = prop.transform.localScale, active = prop.gameObject.activeSelf });
            return point;
        }
        public static bool Validate(BattleReturnPoint point)
        {
            if (point == null || point.version != 1 || string.IsNullOrWhiteSpace(point.scenePath) || !VectorValid(point.position) ||
                !VectorValid(point.localScale) || !Finite(point.rotationZ) || !PropGameState.IsValidSnapshot(point.propState) ||
                (point.checkState != null && !CheckActorState.IsValidSnapshot(point.checkState)) || point.props == null || point.props.Count > 100000 ||
                point.props.Any(p => p == null || string.IsNullOrEmpty(p.instanceId) || string.IsNullOrEmpty(p.definitionId) ||
                    !VectorValid(p.position) || !VectorValid(p.localScale) || !Finite(p.rotationZ)) ||
                point.props.Select(p => p.instanceId).Distinct().Count() != point.props.Count) return false;
            // A return point is always outside combat. Reject recursive save graphs before validating.
            return point.world == null || (point.world.scenePath == point.scenePath && point.world.actors != null &&
                point.world.actors.All(a => a != null && a.battleState == null && a.pointBattleState == null) && GameSaveStore.IsValidData(point.world));
        }
        public void RestoreLocal(Component actor)
        {
            var state = actor.GetComponent<PropGameState>();
            if (!state.RestoreSnapshot(propState)) throw new InvalidOperationException("战前背包状态无效");
            var checks = actor.GetComponent<CheckActorState>();
            if (checkState != null && checks == null) checks = actor.gameObject.AddComponent<CheckActorState>();
            if (checks != null && !checks.RestoreSnapshot(checkState ?? new CheckActorState.Snapshot { attributes = checks.attributes.Clone() }))
                throw new InvalidOperationException("战前剧情检定状态无效");
            actor.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 0, rotationZ)); actor.transform.localScale = localScale;
            var body = actor.GetComponent<Rigidbody2D>(); if (body != null) { body.position = position; body.velocity = Vector2.zero; body.angularVelocity = 0; }
            actor.GetComponent<PlayerMovement>()?.ResumeKeyboardInput();
            var objects = PropInstance.Instances.Where(p => p != null && p.gameObject.scene == actor.gameObject.scene && p.Definition != null)
                .GroupBy(p => p.InstanceId).ToDictionary(g => g.Key, g => g.First());
            foreach (var saved in props)
            {
                if (!objects.TryGetValue(saved.instanceId, out var prop) || prop.Definition.Id != saved.definitionId) continue;
                prop.transform.SetPositionAndRotation(saved.position, Quaternion.Euler(0, 0, saved.rotationZ));
                prop.transform.localScale = saved.localScale; prop.gameObject.SetActive(saved.active);
            }
            PropInstance.RestoreAll(state); Physics2D.SyncTransforms();
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<CameraFollow>()) camera.SnapToTarget();
        }
        private static bool VectorValid(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
