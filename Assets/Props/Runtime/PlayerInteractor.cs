using System;
using System.Collections.Generic;
using PixelPrototype;
using UnityEngine;

namespace Emerge.Props
{
    [RequireComponent(typeof(PropGameState)), DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        public KeyCode interactKey = KeyCode.E;
        public KeyCode inventoryKey = KeyCode.I;
        public bool keyboardInput = true;
        public bool showUI = true;
        public PropLibrary propLibrary;
        private PropGameState state;
        private PlayerMovement movement;
        private bool movementWasEnabled;
        private bool inventoryOpen;
        private PropInstance current;
        private PropInstance dialogueOwner;
        private List<PropDialogueLine> lines;
        private int lineIndex;
        private Action<bool> dialogueFinished;
        private Action<bool> dialogueChoice;
        private Action<string> optionChosen;
        private List<PropDialogueChoice> optionChoices;
        private string acceptChoiceLabel, declineChoiceLabel;
        public bool IsAwaitingChoice { get; private set; }
        private string feedback;
        private float feedbackUntil;
        private Vector2 inventoryScroll;
        private Vector2 dialogueScroll;
        public PropGameState State { get { if (state == null) state = GetComponent<PropGameState>(); return state; } }
        public bool IsInDialogue => dialogueFinished != null;
        public IReadOnlyList<PropDialogueChoice> DialogueOptions => optionChoices != null
            ? (IReadOnlyList<PropDialogueChoice>)optionChoices : Array.Empty<PropDialogueChoice>();
        public int DialogueIndex => lineIndex;
        public string CurrentDialogueText => lines != null && lineIndex >= 0 && lineIndex < lines.Count && lines[lineIndex] != null ? lines[lineIndex].text : "";
        public string CurrentDialogueSpeaker => lines != null && lineIndex >= 0 && lineIndex < lines.Count && lines[lineIndex] != null ? lines[lineIndex].speaker : "";
        public PropInstance CurrentTarget => current;
        private void Awake() { state = GetComponent<PropGameState>(); movement = GetComponent<PlayerMovement>(); }
        private void OnEnable() { PropGameState.Restored += OnStateRestored; }
        private void OnDisable() { CancelDialogue(); PropGameState.Restored -= OnStateRestored; }
        private void OnStateRestored(PropGameState restored)
        { if (restored == State) { CancelDialogue(); PropInstance.RestoreAll(restored); } }
        private void Update()
        {
            if (IsInDialogue && !HasActiveDialogueOwner()) return;
            if (!Emerge.GameFlow.GameSessionController.GameplayInputAllowed) return;
            if (IsInDialogue)
            {
                if (keyboardInput)
                {
                    if (Input.GetKeyDown(KeyCode.Escape)) CancelDialogue();
                    else if (IsAwaitingChoice)
                    {
                        if (optionChosen != null && optionChoices != null)
                        {
                            for (int i = 0; i < Mathf.Min(9, optionChoices.Count); i++)
                                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)) ||
                                    Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i)))
                                { ChooseDialogueOption(optionChoices[i].id); break; }
                        }
                        else if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ChooseDialogueOption(true);
                        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) ChooseDialogueOption(false);
                    }
                    else if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.Space)) AdvanceDialogue();
                }
                return;
            }
            current = FindNearest();
            if (!keyboardInput) return;
            if (Input.GetKeyDown(inventoryKey)) inventoryOpen = !inventoryOpen;
            if (Input.GetKeyDown(interactKey)) TryInteractNearest();
        }
        public bool TryInteractNearest()
        { if (!Emerge.GameFlow.GameSessionController.GameplayInputAllowed) return false;
          current = FindNearest(); return current != null && current.Interact(this); }
        public PropInstance FindNearest()
        {
            PropInstance nearest = null;
            float closest = float.PositiveInfinity;
            foreach (var prop in PropInstance.Instances)
            {
                if (prop == null || !prop.isActiveAndEnabled || prop.Definition == null ||
                    (prop.Definition.actions == PropActions.None && prop.Definition.checkEvent == null && prop.Definition.battleEncounter == null) || State.IsConsumed(prop.InstanceId)) continue;
                float distance = prop.DistanceTo(transform.position);
                if (distance > prop.Definition.interactionRange || distance >= closest) continue;
                nearest = prop; closest = distance;
            }
            return nearest;
        }
        public bool BeginDialogue(PropInstance owner, List<PropDialogueLine> dialogue, Action<bool> finished)
        {
            if (IsInDialogue || owner == null || dialogue == null || dialogue.Count == 0 || finished == null) return false;
            if (!owner.TryRegisterDialogue(this)) return false;
            dialogueOwner = owner; lines = new List<PropDialogueLine>(dialogue); lineIndex = 0; dialogueFinished = finished; dialogueScroll = Vector2.zero;
            ClearChoices();
            if (movement == null) movement = GetComponent<PlayerMovement>();
            movementWasEnabled = movement != null && movement.enabled;
            if (movementWasEnabled) movement.enabled = false;
            inventoryOpen = false;
            return true;
        }
        public void AdvanceDialogue()
        {
            if (!HasActiveDialogueOwner()) return;
            if (!Emerge.GameFlow.GameSessionController.GameplayInputAllowed) return;
            if (!IsInDialogue || IsAwaitingChoice) return;
            if ((dialogueChoice != null || optionChosen != null) && lineIndex >= lines.Count - 1)
            { IsAwaitingChoice = true; return; }
            lineIndex++;
            dialogueScroll = Vector2.zero;
            if (lineIndex >= lines.Count) FinishDialogue(true);
        }
        public bool BeginChoiceDialogue(PropInstance owner, List<PropDialogueLine> dialogue, string acceptLabel,
            string declineLabel, Action<bool> chosen, Action<bool> finished)
        {
            if (chosen == null || !BeginDialogue(owner, dialogue, finished)) return false;
            dialogueChoice = chosen;
            acceptChoiceLabel = string.IsNullOrWhiteSpace(acceptLabel) ? "交给" : acceptLabel;
            declineChoiceLabel = string.IsNullOrWhiteSpace(declineLabel) ? "不给" : declineLabel;
            return true;
        }
        public bool BeginOptionDialogue(PropInstance owner, List<PropDialogueLine> dialogue,
            List<PropDialogueChoice> choices, Action<string> chosen, Action<bool> finished)
        {
            if (chosen == null || choices == null || choices.Count == 0) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var snapshot = new List<PropDialogueChoice>(choices.Count);
            foreach (var choice in choices)
            {
                if (choice == null || string.IsNullOrWhiteSpace(choice.id) || !ids.Add(choice.id)) return false;
                snapshot.Add(new PropDialogueChoice { id = choice.id,
                    label = string.IsNullOrWhiteSpace(choice.label) ? choice.id : choice.label,
                    enabled = choice.enabled, disabledReason = choice.disabledReason });
            }
            if (!BeginDialogue(owner, dialogue, finished)) return false;
            optionChoices = snapshot;
            optionChosen = chosen;
            return true;
        }
        public bool ChooseDialogueOption(bool accept)
        {
            if (!HasActiveDialogueOwner()) return false;
            if (!Emerge.GameFlow.GameSessionController.GameplayInputAllowed) return false;
            if (!IsInDialogue || !IsAwaitingChoice || dialogueChoice == null) return false;
            var callback = dialogueChoice;
            ClearChoices();
            callback(accept);
            return true;
        }
        public bool ChooseDialogueOption(string id)
        {
            if (!HasActiveDialogueOwner()) return false;
            if (!Emerge.GameFlow.GameSessionController.GameplayInputAllowed) return false;
            if (!IsInDialogue || !IsAwaitingChoice || optionChosen == null || optionChoices == null) return false;
            var choice = optionChoices.Find(option => string.Equals(option.id, id, StringComparison.Ordinal));
            if (choice == null) return false;
            if (!choice.enabled)
            {
                ShowFeedback(string.IsNullOrWhiteSpace(choice.disabledReason) ? "当前无法选择此行动" : choice.disabledReason);
                return false;
            }
            var callback = optionChosen;
            ClearChoices();
            callback(choice.id);
            return true;
        }
        public void ReplaceDialogue(List<PropDialogueLine> dialogue)
        {
            if (!IsInDialogue) return;
            ClearChoices();
            lines = dialogue == null ? new List<PropDialogueLine>() : new List<PropDialogueLine>(dialogue);
            lineIndex = 0; dialogueScroll = Vector2.zero;
            if (lines.Count == 0) FinishDialogue(true);
        }
        public void CancelDialogue() { if (IsInDialogue) FinishDialogue(false); else ClearChoices(); }
        public void CancelDialogueFrom(PropInstance owner) { if (dialogueOwner == owner) CancelDialogue(); }
        private bool HasActiveDialogueOwner()
        {
            if (!IsInDialogue) return false;
            if (dialogueOwner != null && dialogueOwner.isActiveAndEnabled) return true;
            CancelDialogue();
            return false;
        }
        private void ClearChoices()
        {
            dialogueChoice = null;
            optionChosen = null;
            optionChoices = null;
            acceptChoiceLabel = null;
            declineChoiceLabel = null;
            IsAwaitingChoice = false;
        }
        private void FinishDialogue(bool success)
        {
            var callback = dialogueFinished;
            var owner = dialogueOwner;
            dialogueFinished = null; dialogueOwner = null; lines = null;
            ClearChoices();
            if (owner != null) owner.ReleaseDialogue(this);
            if (movement != null && movementWasEnabled) movement.enabled = true;
            movementWasEnabled = false;
            callback?.Invoke(success);
        }
        public void ShowFeedback(string message) { feedback = message; feedbackUntil = Time.unscaledTime + 3f; }

        private void OnGUI()
        {
            if (!showUI || !Emerge.GameFlow.GameSessionController.GameplayInputAllowed) return;
            float width = Mathf.Min(620f, Screen.width - 24f);
            var text = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16 };
            var title = new GUIStyle(text) { fontStyle = FontStyle.Bold };
            if (IsInDialogue && lines != null && lineIndex < lines.Count)
            {
                var line = lines[lineIndex];
                // Snapshot the controls before drawing. A chosen branch may replace or finish this dialogue.
                bool awaitingChoice = IsAwaitingChoice;
                var choices = optionChoices != null ? optionChoices.ToArray() : Array.Empty<PropDialogueChoice>();
                string acceptLabel = acceptChoiceLabel, declineLabel = declineChoiceLabel;
                string requestedOption = null;
                bool? requestedLegacyChoice = null;
                bool advanceRequested = false, cancelRequested = false;
                float height = awaitingChoice && choices.Length > 0
                    ? Mathf.Min(Mathf.Max(180f, Screen.height - 16f), 190f + choices.Length * 34f) : 180f;
                var rect = new Rect((Screen.width - width) / 2f, Mathf.Max(8f, Screen.height - height - 12f), width, height);
                GUI.Box(rect, "");
                GUILayout.BeginArea(new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, rect.height - 24f));
                GUILayout.BeginHorizontal();
                if (line != null && line.portrait != null)
                {
                    Rect portrait = GUILayoutUtility.GetRect(64f, 64f, GUILayout.Width(64f));
                    Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(line.portrait);
                    GUI.DrawTextureWithTexCoords(portrait, line.portrait.texture, new Rect(uv.x, uv.y, uv.z - uv.x, uv.w - uv.y));
                }
                GUILayout.BeginVertical();
                GUILayout.Label(line == null ? "" : line.speaker, title);
                dialogueScroll = GUILayout.BeginScrollView(dialogueScroll);
                GUILayout.Label(line == null ? "" : line.text, text);
                GUILayout.EndScrollView();
                GUILayout.EndVertical(); GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
                if (awaitingChoice && choices.Length > 0)
                {
                    var buttonStyle = new GUIStyle(GUI.skin.button) { wordWrap = true };
                    for (int i = 0; i < choices.Length; i++)
                    {
                        var choice = choices[i];
                        string label = choice.label + (i < 9 ? " [" + (i + 1) + "]" : "");
                        if (!choice.enabled && !string.IsNullOrWhiteSpace(choice.disabledReason))
                            label += " · " + choice.disabledReason;
                        bool wasEnabled = GUI.enabled;
                        GUI.enabled = wasEnabled && choice.enabled;
                        if (GUILayout.Button(new GUIContent(label, choice.disabledReason), buttonStyle, GUILayout.MinHeight(28f)))
                            requestedOption = choice.id;
                        GUI.enabled = wasEnabled;
                    }
                }
                GUILayout.BeginHorizontal();
                if (awaitingChoice && choices.Length == 0)
                {
                    if (GUILayout.Button(acceptLabel + " [1]")) requestedLegacyChoice = true;
                    if (GUILayout.Button(declineLabel + " [2]")) requestedLegacyChoice = false;
                }
                else if (!awaitingChoice && GUILayout.Button("继续 [" + interactKey + "/Space]")) advanceRequested = true;
                if (GUILayout.Button("取消 [Esc]", GUILayout.Width(110f))) cancelRequested = true;
                GUILayout.EndHorizontal(); GUILayout.EndArea();
                // Invoke callbacks only after every layout group for the snapshot has been closed.
                if (cancelRequested) CancelDialogue();
                else if (requestedOption != null) ChooseDialogueOption(requestedOption);
                else if (requestedLegacyChoice.HasValue) ChooseDialogueOption(requestedLegacyChoice.Value);
                else if (advanceRequested) AdvanceDialogue();
            }
            else if (current != null)
            {
                bool allowed = current.CanInteract(this, out string reason);
                string prompt = allowed ? "[" + interactKey + "] " + current.Definition.interactionLabel + " · " + current.Definition.DisplayName : reason;
                GUI.Box(new Rect((Screen.width - width) / 2f, Screen.height - 65f, width, 38f), prompt);
            }
            if (!string.IsNullOrEmpty(feedback) && Time.unscaledTime < feedbackUntil)
                GUI.Box(new Rect((Screen.width - width) / 2f, Screen.height - 240f, width, 42f), feedback);
            GUI.Label(new Rect(16f, 116f, 280f, 24f), interactKey + " 交互 · " + inventoryKey + " 背包 · Shift 加速");
            if (inventoryOpen)
            {
                GUILayout.BeginArea(new Rect(16f, 144f, 280f, Mathf.Max(60f, Mathf.Min(360f, Screen.height - 154f))), GUI.skin.box);
                GUILayout.Label("背包", title);
                inventoryScroll = GUILayout.BeginScrollView(inventoryScroll);
                if (State.Inventory.Count == 0) GUILayout.Label("暂无物品");
                foreach (var item in State.Inventory) GUILayout.Label(item.displayName + " × " + item.amount);
                GUILayout.EndScrollView(); GUILayout.EndArea();
            }
        }
    }
}
