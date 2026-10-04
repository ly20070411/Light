using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Emerge.PixelMap;
using PixelPrototype;
using UnityEngine;

namespace Emerge.Props
{
    public sealed class PropSelfTest : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name; public bool passed; public string observed; }
        [Serializable] public sealed class Report
        {
            public string unityVersion, scene, completedUtc;
            public bool passed;
            public List<Check> checks = new List<Check>();
        }
        public static string InitialReportJson;
        public static event Action<bool> Completed;
        private Report report;
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            Time.timeScale = 1f;
            report = string.IsNullOrEmpty(InitialReportJson) ? new Report() : JsonUtility.FromJson<Report>(InitialReportJson);
            report.unityVersion = Application.unityVersion; report.scene = gameObject.scene.name;
            var actorObject = new GameObject("道具自动测试玩家"); cleanup.Add(actorObject);
            actorObject.transform.position = new Vector3(50, 50, 0);
            var body = actorObject.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            actorObject.AddComponent<BoxCollider2D>().size = new Vector2(.4f, .4f);
            var movement = actorObject.AddComponent<PlayerMovement>(); movement.SetScriptedInput(Vector2.zero);
            var actor = actorObject.AddComponent<PlayerInteractor>(); actor.keyboardInput = false; actor.showUI = false;
            var root = new GameObject("自动测试道具"); cleanup.Add(root);
            var pickup = Definition("test_pickup", PropActions.Pickup);
            pickup.pickupAmount = 2; pickup.inventoryKey = "test_item";
            var item = Place(pickup, root.transform, new Vector3(51, 50, 0));
            Add("Non-solid prop has no blocking collider", item.SolidCollider == null || !item.SolidCollider.enabled, "Pickup interaction uses prop registry");
            Add("Nearest interaction finds non-solid props", actor.FindNearest() == item, "No trigger collider is required");
            actorObject.transform.position = new Vector3(45, 50, 0); Physics2D.SyncTransforms();
            Add("Out-of-range interaction is rejected", !item.Interact(actor) && actor.State.Count("test_item") == 0, "Distance gate");
            actorObject.transform.position = new Vector3(50, 50, 0); Physics2D.SyncTransforms();
            pickup.requiredFlags = new[] { "test_read" };
            Add("Story condition blocks pickup", !item.Interact(actor) && actor.State.Count("test_item") == 0, "Missing test_read flag");
            var conversation = Definition("test_dialogue", PropActions.Dialogue | PropActions.Custom);
            conversation.grantedFlags = new[] { "test_read" }; conversation.cooldown = 0;
            conversation.dialogue.Add(new PropDialogueLine { speaker = "Test", text = "First" });
            conversation.dialogue.Add(new PropDialogueLine { speaker = "Test", text = "Second" });
            var terminal = Place(conversation, root.transform, new Vector3(50, 51, 0));
            int events = 0; terminal.onInteracted.AddListener(player => events++);
            Add("Dialogue begins and freezes movement", terminal.Interact(actor) && actor.IsInDialogue && !movement.enabled && body.velocity.sqrMagnitude < .001f, "Movement disabled during dialogue");
            actor.AdvanceDialogue();
            Add("Dialogue advances without premature reward", actor.DialogueIndex == 1 && !actor.State.HasFlag("test_read") && events == 0, "Two-line sequence");
            actor.CancelDialogue();
            Add("Cancelled dialogue restores movement without reward", !actor.IsInDialogue && movement.enabled && !actor.State.HasFlag("test_read") && events == 0, "No flags, pickups or events on cancel");
            terminal.Interact(actor); actor.AdvanceDialogue(); actor.AdvanceDialogue();
            Add("Completed dialogue grants flag and event once", actor.State.HasFlag("test_read") && events == 1 && !actor.IsInDialogue && movement.enabled, "Dialogue completion");
            int picked = 0; item.onPickedUp.AddListener(player => picked++);
            Add("Pickup adds configured quantity and hides instance", item.Interact(actor) && actor.State.Count("test_item") == 2 && !item.gameObject.activeSelf && picked == 1, "Quantity=2");
            Add("Repeated pickup cannot duplicate inventory", !item.Interact(actor) && actor.State.Count("test_item") == 2 && picked == 1, "Consumed instance ID");
            var combined = Definition("test_combo", PropActions.Dialogue | PropActions.Pickup);
            combined.inventoryKey = "combo"; combined.dialogue.Add(new PropDialogueLine { text = "Take this." });
            var combo = Place(combined, root.transform, new Vector3(50, 50.5f, 0));
            combo.Interact(actor); actor.CancelDialogue();
            Add("Dialogue-pickup combination cancels atomically", actor.State.Count("combo") == 0 && combo.gameObject.activeSelf, "No pickup on cancellation");
            combo.Interact(actor); actor.AdvanceDialogue();
            Add("Dialogue-pickup combination completes in order", actor.State.Count("combo") == 1 && !combo.gameObject.activeSelf, "Pickup happens after dialogue");
            var cost = Definition("test_cost", PropActions.Custom); cost.requiredItemKey = "test_item"; cost.requiredItemAmount = 2; cost.consumeRequiredItem = true;
            cost.grantedFlags = new[] { "paid" }; cost.singleUse = true;
            var switchProp = Place(cost, root.transform, new Vector3(49.5f, 50, 0));
            Add("Required inventory is consumed on completion", switchProp.Interact(actor) && actor.State.Count("test_item") == 0 && actor.State.HasFlag("paid"), "Consumes exactly two test_item");
            Add("Single-use custom action rejects a second interaction", !switchProp.Interact(actor), "Same instance cannot spend again");
            string saved = actor.State.CaptureJson();
            var savedObject = new GameObject("存档恢复测试"); cleanup.Add(savedObject);
            var restored = savedObject.AddComponent<PropGameState>();
            Add("Inventory flags and consumed IDs round-trip", restored.RestoreJson(saved) && restored.Count("combo") == 1 && restored.HasFlag("test_read") && restored.IsConsumed(item.InstanceId), "Versioned JSON snapshot");
            Add("Invalid save preserves existing state", !restored.RestoreJson("not-json") && restored.Count("combo") == 1, "Malformed input is rejected");
            PropInstance.RestoreAll(restored);
            Add("Restored consumed pickup stays hidden", !item.gameObject.activeSelf && !combo.gameObject.activeSelf, "Scene instances use stable IDs");
            var emptyObject = new GameObject("空存档测试"); cleanup.Add(emptyObject);
            PropInstance.RestoreAll(emptyObject.AddComponent<PropGameState>());
            Add("Restoring earlier checkpoint re-enables pickups", item.gameObject.activeSelf && combo.gameObject.activeSelf, "Hidden instances retained for rollback");

            // Use real fixed-step collision, far from the playable demo room.
            root.SetActive(false); yield return null;
            var solidDefinition = Definition("test_solid", PropActions.None); solidDefinition.isSolid = true;
            solidDefinition.colliderSize = Vector2.one; solidDefinition.colliderOffset = Vector2.zero;
            var solid = Place(solidDefinition, null, new Vector3(52, 50, 0)); cleanup.Add(solid.gameObject);
            body.position = new Vector2(50, 50); movement.SetScriptedInput(Vector2.right); Physics2D.SyncTransforms();
            for (int i = 0; i < 45; i++) yield return new WaitForFixedUpdate();
            float blockedX = body.position.x;
            Add("Solid props block real Rigidbody2D movement", blockedX > 51f && blockedX < 51.4f, "Player X=" + blockedX.ToString("F3") + "; wall center=52");
            movement.SetScriptedInput(Vector2.zero); solidDefinition.isSolid = false; solid.ApplyDefinition();
            body.position = new Vector2(50, 50); body.velocity = Vector2.zero; movement.SetScriptedInput(Vector2.right); Physics2D.SyncTransforms();
            for (int i = 0; i < 45; i++) yield return new WaitForFixedUpdate();
            Add("Non-solid props allow passage", body.position.x > 53.1f, "Player X=" + body.position.x.ToString("F3"));
            movement.SetScriptedInput(Vector2.zero);
            var guide = FindDemoDefinition("向导");
            Add("Demo includes frame animation assets", guide != null && guide.idleFrames.Length >= 2 && guide.idleFrames[0] != null, "Existing point-filtered sprite art");
            if (guide != null)
            {
                var animated = Place(guide, null, new Vector3(60, 50, 0)); cleanup.Add(animated.gameObject);
                var frames = animated.GetComponentInChildren<PropSpriteAnimator>();
                var visual = animated.GetComponentInChildren<SpriteRenderer>();
                Sprite first = visual.sprite; Vector2 size = animated.SolidCollider.size;
                yield return new WaitForSeconds(.31f);
                Add("Frame animation advances in Play Mode", frames.FrameIndex > 0 && visual.sprite != first, "Frame=" + frames.FrameIndex);
                Add("Animation preserves collider shape", animated.SolidCollider.size == size && animated.SolidCollider.enabled, "Independent feet collision");
            }
            foreach (var obj in cleanup) if (obj != null) Destroy(obj);
            Finish();
        }
        private PropDefinition FindDemoDefinition(string name)
        {
            foreach (var prop in PropInstance.Instances) if (prop != null && prop.Definition != null && prop.Definition.DisplayName == name) return prop.Definition;
            return null;
        }
        private PropDefinition Definition(string name, PropActions actions)
        { var d = ScriptableObject.CreateInstance<PropDefinition>(); d.displayName = name; d.actions = actions; cleanup.Add(d); return d; }
        private PropInstance Place(PropDefinition definition, Transform root, Vector3 position)
        {
            var obj = new GameObject(definition.DisplayName); obj.transform.SetParent(root); obj.transform.position = position;
            var prop = obj.AddComponent<PropInstance>(); prop.Configure(definition); return prop;
        }
        private void Add(string name, bool passed, string observed)
        { report.checks.Add(new Check { name = name, passed = passed, observed = observed }); Debug.Log("PROPS_CHECK " + (passed ? "PASS " : "FAIL ") + name + ": " + observed); }
        private void Finish()
        {
            report.passed = report.checks.TrueForAll(check => check.passed); report.completedUtc = DateTime.UtcNow.ToString("O");
            string path = Path.Combine(Application.dataPath, "../Validation/props-results.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("PROPS_VALIDATION_" + (report.passed ? "PASS" : "FAIL") + " checks=" + report.checks.Count);
            Completed?.Invoke(report.passed);
        }
    }
}
