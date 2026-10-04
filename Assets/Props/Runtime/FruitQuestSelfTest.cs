using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Emerge.PixelMap;
using PixelPrototype;
using UnityEngine;

namespace Emerge.Props
{
    /// <summary>Exercises the persisted fruit and guide definitions without changing their assets.</summary>
    public sealed class FruitQuestSelfTest : MonoBehaviour
    {
        [Serializable] public sealed class Check
        {
            public string name;
            public bool passed;
            public string observed;
        }
        [Serializable] public sealed class Report
        {
            public string unityVersion;
            public string scene;
            public string completedUtc;
            public bool passed;
            public List<Check> checks = new List<Check>();
        }

        public static event Action<bool> Completed;
        private PropDefinition fruitDefinition;
        private PropDefinition guideDefinition;
        private string initialReport;
        private Report report;
        private readonly List<GameObject> cleanup = new List<GameObject>();
        private PlayerInteractor actor;
        private PropInstance fruit;
        private PropInstance guide;
        private PlayerMovement movement;
        private int guideEvents;
        private int pickupEvents;

        public void Initialize(PropDefinition fruitAsset, PropDefinition guideAsset, string reportJson)
        {
            fruitDefinition = fruitAsset;
            guideDefinition = guideAsset;
            initialReport = reportJson;
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            Time.timeScale = 1f;
            report = string.IsNullOrEmpty(initialReport) ? new Report() : JsonUtility.FromJson<Report>(initialReport);
            report.unityVersion = Application.unityVersion;
            report.scene = gameObject.scene.name;
            var routine = RunChecks();
            // Catch failures in this coroutine so an exception still produces a useful report.
            while (true)
            {
                bool next;
                object waiting;
                try { next = routine.MoveNext(); waiting = next ? routine.Current : null; }
                catch (Exception exception)
                {
                    Add("Runtime verification completed without exception", false, exception.ToString());
                    break;
                }
                if (!next) break;
                yield return waiting;
            }
            if (actor != null) actor.CancelDialogue();
            foreach (var temporary in cleanup) if (temporary != null) Destroy(temporary);
            Finish();
        }

        private IEnumerator RunChecks()
        {
            bool configured = fruitDefinition != null && guideDefinition != null && guideDefinition.itemHandover != null
                && guideDefinition.itemHandover.enabled;
            Add("Runtime uses configured Fruit.asset and Guide.asset", configured, "Uses the same assets shown in both editors");
            if (!configured) yield break;
            var handover = guideDefinition.itemHandover;
            string key = handover.itemKey;
            string flag = handover.completionFlag;

            var actorObject = new GameObject("果实流程自动测试玩家");
            cleanup.Add(actorObject);
            actorObject.transform.position = new Vector3(50f, 50f, 0f);
            var body = actorObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            actorObject.AddComponent<BoxCollider2D>().size = new Vector2(.4f, .4f);
            movement = actorObject.AddComponent<PlayerMovement>();
            movement.SetScriptedInput(Vector2.zero);
            actor = actorObject.AddComponent<PlayerInteractor>();
            actor.keyboardInput = false;
            actor.showUI = false;

            var root = new GameObject("果实流程自动测试道具");
            cleanup.Add(root);
            fruit = Place(fruitDefinition, root.transform, new Vector3(51f, 50f, 0f));
            guide = Place(guideDefinition, root.transform, new Vector3(50f, 51f, 0f));
            fruit.onPickedUp.AddListener(player => pickupEvents++);
            guide.onInteracted.AddListener(player => guideEvents++);
            Physics2D.SyncTransforms();
            yield return null;

            Add("Fruit and guide have independent persistent instance IDs", !string.IsNullOrEmpty(fruit.InstanceId)
                && !string.IsNullOrEmpty(guide.InstanceId) && fruit.InstanceId != guide.InstanceId, "No runtime mutation of asset IDs");
            var fruitRenderer = fruit.GetComponentInChildren<SpriteRenderer>();
            Add("Fruit instance displays its edited sprite", fruitRenderer != null && fruitRenderer.sprite == fruitDefinition.sprite,
                "Configured sprite used by runtime instance");
            Add("Three guide responses are configured differently", Different(handover.missingItemDialogue, handover.declinedDialogue)
                && Different(handover.missingItemDialogue, handover.acceptedDialogue)
                && Different(handover.declinedDialogue, handover.acceptedDialogue), "No fruit / decline / accept have distinct lines");

            Add("Without fruit the guide starts the missing-item branch", guide.Interact(actor) && actor.IsInDialogue
                && !actor.IsAwaitingChoice, "Inventory is empty");
            AssertAndFinish("Missing-item dialogue matches the editor configuration", handover.missingItemDialogue);
            Add("Missing-item dialogue grants no completion or reward", actor.State.Count(key) == 0 && !actor.State.HasFlag(flag)
                && guideEvents == 0 && movement.enabled, "No choice or consumption without fruit");
            yield return new WaitForSeconds(.35f);

            Add("Picking up fruit stores it in the backpack", fruit.Interact(actor) && actor.State.Count(key) == 1
                && pickupEvents == 1, "Inventory fruit=1");
            Add("Picked fruit hides and records its consumed identity", !fruit.gameObject.activeSelf
                && actor.State.IsConsumed(fruit.InstanceId), "World item removed after pickup");
            Add("A second pickup cannot duplicate fruit", !fruit.Interact(actor) && actor.State.Count(key) == 1
                && pickupEvents == 1, "Pickup event fires once");
            string beforeDelivery = actor.State.CaptureJson();
            var restoredObject = new GameObject("果实流程存档恢复验证");
            cleanup.Add(restoredObject);
            var restoredState = restoredObject.AddComponent<PropGameState>();
            Add("Backpack fruit survives a save-and-restore round trip", restoredState.RestoreJson(beforeDelivery)
                && restoredState.Count(key) == 1 && restoredState.IsConsumed(fruit.InstanceId)
                && !restoredState.HasFlag(flag), "Backpack quantity and consumed world item are both persisted");

            BeginOffer(handover.offerDialogue, "Fruit unlocks the guide's editable offer dialogue");
            actor.AdvanceDialogue();
            actor.AdvanceDialogue();
            Add("Continue cannot bypass the give-or-decline choice", actor.IsAwaitingChoice && actor.IsInDialogue
                && actor.State.Count(key) == 1 && !actor.State.HasFlag(flag), "Explicit choice is required");
            Add("Declining selects its own dialogue without consuming fruit", actor.ChooseDialogueOption(false)
                && !actor.IsAwaitingChoice && actor.State.Count(key) == 1, "Decline action selected");
            AssertAndFinish("Declined dialogue matches the editor configuration", handover.declinedDialogue);
            Add("Finishing a declined exchange preserves backpack and quest", actor.State.Count(key) == 1
                && !actor.State.HasFlag(flag) && guideEvents == 0 && !actor.IsInDialogue && movement.enabled,
                "Declining fires no completion event");
            yield return new WaitForSeconds(.35f);

            BeginOffer(handover.offerDialogue, "The guide can be asked again after declining");
            Add("Accepting starts dialogue before committing the exchange", actor.ChooseDialogueOption(true)
                && actor.IsInDialogue && !actor.IsAwaitingChoice && actor.State.Count(key) == 1
                && !actor.State.HasFlag(flag) && guideEvents == 0 && !movement.enabled, "Pending acceptance is reversible");
            Add("Accepted branch displays the first configured line", CurrentLineMatches(handover.acceptedDialogue, 0),
                actor.CurrentDialogueText);
            actor.CancelDialogue();
            Add("Cancelling accepted dialogue keeps fruit and restores movement", actor.State.Count(key) == 1
                && !actor.State.HasFlag(flag) && guideEvents == 0 && !actor.IsInDialogue && movement.enabled,
                "Cancellation does not commit the handover");
            yield return new WaitForSeconds(.35f);

            BeginOffer(handover.offerDialogue, "The guide can be asked again after cancellation");
            Add("The explicit give choice can be selected again", actor.ChooseDialogueOption(true), "Accept selected");
            AssertAndFinish("Accepted dialogue matches the editor configuration", handover.acceptedDialogue);
            Add("Only finished acceptance consumes one fruit and completes the quest", actor.State.Count(key) == 0
                && actor.State.HasFlag(flag) && guideEvents == 1 && !actor.IsInDialogue && movement.enabled,
                "Fruit=0; completionFlag=" + flag + "; completionEvents=" + guideEvents);
            string afterDelivery = actor.State.CaptureJson();
            Add("Completed delivery survives save and restore", restoredState.RestoreJson(afterDelivery)
                && restoredState.Count(key) == 0 && restoredState.HasFlag(flag)
                && restoredState.IsConsumed(fruit.InstanceId), "Quest flag and fruit consumption persisted");
            yield return new WaitForSeconds(.35f);

            Add("Completed guide interaction opens new dialogue", guide.Interact(actor) && actor.IsInDialogue
                && !actor.IsAwaitingChoice, "Completion flag selects the post-delivery branch");
            AssertAndFinish("Post-delivery dialogue matches the editor configuration", handover.completedDialogue);
            Add("Repeated conversation cannot hand over or reward again", actor.State.Count(key) == 0
                && actor.State.HasFlag(flag) && guideEvents == 1, "No repeated completion event");
            yield return new WaitForSeconds(.35f);

            Add("Restoring the earlier backpack checkpoint restores one fruit", actor.State.RestoreJson(beforeDelivery)
                && actor.State.Count(key) == 1 && !actor.State.HasFlag(flag) && !fruit.gameObject.activeSelf,
                "Backpack rolls back while the picked world fruit stays consumed");
            BeginOffer(handover.offerDialogue, "Restored checkpoint returns to the give-or-decline branch");
            actor.CancelDialogue();
            Add("Restoring the completed checkpoint restores the new dialogue state", actor.State.RestoreJson(afterDelivery)
                && actor.State.Count(key) == 0 && actor.State.HasFlag(flag) && !fruit.gameObject.activeSelf,
                "Stored quest progress determines the branch");
            Add("Restored completed progress opens completed dialogue", guide.Interact(actor) && actor.IsInDialogue
                && !actor.IsAwaitingChoice && CurrentLineMatches(handover.completedDialogue, 0), "Same persisted asset, restored state");
            AssertAndFinish("Restored completed dialogue remains configured", handover.completedDialogue);
            Add("Restoring and talking does not emit duplicate completion", guideEvents == 1 && actor.State.Count(key) == 0,
                "Completion event count remains one");
        }

        private void BeginOffer(List<PropDialogueLine> expected, string checkName)
        {
            bool started = guide.Interact(actor) && actor.IsInDialogue;
            bool matches = expected != null && expected.Count > 0;
            if (expected != null) for (int i = 0; i < expected.Count; i++)
            {
                matches &= actor.IsInDialogue && !actor.IsAwaitingChoice && CurrentLineMatches(expected, i);
                actor.AdvanceDialogue();
            }
            Add(checkName, started && matches && actor.IsAwaitingChoice, "Offer lines end with an explicit choice");
        }

        private void AssertAndFinish(string checkName, List<PropDialogueLine> expected)
        {
            bool matches = expected != null && expected.Count > 0;
            if (expected != null) for (int i = 0; i < expected.Count; i++)
            {
                matches &= actor.IsInDialogue && !actor.IsAwaitingChoice && CurrentLineMatches(expected, i);
                actor.AdvanceDialogue();
            }
            Add(checkName, matches && !actor.IsInDialogue, "Compared every line and speaker against the saved definition");
        }

        private bool CurrentLineMatches(List<PropDialogueLine> expected, int index)
        {
            if (expected == null || index < 0 || index >= expected.Count || expected[index] == null) return false;
            return actor.CurrentDialogueText == expected[index].text && actor.CurrentDialogueSpeaker == expected[index].speaker;
        }

        private static bool Different(List<PropDialogueLine> a, List<PropDialogueLine> b)
        {
            if (a == null || b == null || a.Count == 0 || b.Count == 0) return false;
            if (a.Count != b.Count) return true;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != null && b[i] != null && (a[i].speaker != b[i].speaker || a[i].text != b[i].text)) return true;
            return false;
        }

        private static PropInstance Place(PropDefinition definition, Transform root, Vector3 position)
        {
            var placed = new GameObject(definition.DisplayName);
            placed.transform.SetParent(root, false);
            placed.transform.position = position;
            var instance = placed.AddComponent<PropInstance>();
            instance.Configure(definition, MapPlacementMode.Free);
            return instance;
        }

        private void Add(string name, bool passed, string observed)
        {
            report.checks.Add(new Check { name = name, passed = passed, observed = observed });
            Debug.Log("FRUIT_QUEST_CHECK " + (passed ? "PASS " : "FAIL ") + name + ": " + observed);
        }

        private void Finish()
        {
            report.passed = report.checks.Count > 0 && report.checks.TrueForAll(check => check.passed);
            report.completedUtc = DateTime.UtcNow.ToString("O");
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/fruit-quest-results.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("FRUIT_QUEST_VALIDATION_" + (report.passed ? "PASS" : "FAIL") + " checks=" + report.checks.Count);
            Completed?.Invoke(report.passed);
        }
    }
}
