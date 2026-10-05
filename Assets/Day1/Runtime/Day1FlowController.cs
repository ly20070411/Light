using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.Characters;
using Emerge.GameFlow;
using Emerge.Props;
using PixelPrototype;
using UnityEngine;

namespace Emerge.Day1
{
    // All lasting progress is in the existing player's saveable flags/inventory.
    // No numeric stage or coroutine position is needed to resume an interrupted event.
    [DefaultExecutionOrder(-1000), DisallowMultipleComponent]
    public sealed class Day1FlowController : MonoBehaviour
    {
        public PlayerInteractor actor;
        public Day1StoryDefinition story;
        public CharacterCatalog characters;
        public List<Day1Interaction> interactions = new List<Day1Interaction>();
        public Transform bufferExit, seaGate, shadowStart, shadowEnd, assembly;
        public GameObject admissionGate, outdoorGate;
        public bool showHUD = true;
        private bool? dispersed;
        private GameObject shadow;
        private SpriteRenderer shadowRenderer;
        private SpriteRenderer[] noise;
        private Texture2D noiseTexture;
        private Sprite noiseSprite;
        private AudioClip noiseClip;
        private float shadowTime;
        private bool shadowResultOpen, movementBeforeShadow, notesOpen, mapOpen;
        private Vector2 notesScroll;
        private PlayerMovement movement;
        private PropGameState State => actor.State;
        public bool IsShadowPlaying => shadow != null || shadowResultOpen;
        public int MetCount => NpcPoints.Count(p => State.HasFlag("day1.met." + p.Prop.Character.Id));
        public bool CanFinishDay => State.HasFlag("day1.free_roam") && State.HasFlag("day1.water_deferred") &&
            NpcPoints.All(p => State.HasFlag("day1.completed." + p.Prop.Character.Id));
        private IEnumerable<Day1Interaction> NpcPoints => interactions.Where(p => p != null && p.Prop.Character != null);
        public Day1Interaction Find(string key) => interactions.Find(p => p != null && p.key == key);
        public string Objective
        {
            get
            {
                if (State.HasFlag("day1.finished")) return "第一日结束 · 第二日流程待制作";
                if (!State.HasFlag("day1.admitted")) return "完成入站认知测试，领取身份认证卡";
                if (!State.HasFlag("day1.rules_read")) return "前往总控室，读取并宣读入驻规则";
                if (!State.HasFlag("day1.environment_recorded")) return "读取环境监测台，将数据档案收入背包";
                if (!State.HasFlag("day1.supplies_received")) return "前往生活区，领取餐桌上的生活物资";
                if (!State.HasFlag("day1.free_roam")) return "确认刚才的动静";
                return CanFinishDay ? "今日作业已完成 · 返回规则终端，确认结束第一日" : "自由探索 · 与六名队员交谈并完成今日准备";
            }
        }
        private void Awake()
        {
            movement = actor.GetComponent<PlayerMovement>();
            actor.scaleInterface = true;
            var battle = actor.GetComponent<Emerge.Battle.BattleController>();
            if (battle != null && battle.character == null) battle.character = characters.Find(CharacterIds.HuanYujian);
            // Scene-local override: opening Day1 and choosing New Game starts this scene.
            // The existing general menu prefab and other scenes keep their original settings.
            if (GameSessionController.Instance != null) GameSessionController.Instance.gameScenePath = gameObject.scene.path;
        }
        private void OnEnable() { PropGameState.Restored += Restored; }
        private void OnDisable() { PropGameState.Restored -= Restored; StopShadow(); }
        private void Restored(PropGameState restored)
        {
            if (actor == null || restored != State) return;
            StopShadow(); dispersed = null;
        }
        private void Update()
        {
            if (actor == null || story == null) return;
            ApplyLayout();
            if (!GameSessionController.GameplayInputAllowed) return;
            if (Input.GetKeyDown(KeyCode.M)) mapOpen = !mapOpen;
            if (shadow != null) { AdvanceShadow(); return; }
            if (actor.IsInDialogue || shadowResultOpen) return;
            if (!State.HasFlag("day1.admitted")) BeginQuiz();
            else if (State.HasFlag("day1.supplies_received") && !State.HasFlag("day1.shadow_seen")) BeginShadow();
        }
        private void ApplyLayout()
        {
            bool next = State.HasFlag("day1.rules_read");
            if (dispersed != next)
            {
                dispersed = next;
                var offsets = new[] { new Vector3(-1, 1), new Vector3(2, 1), new Vector3(-2, 0),
                    new Vector3(3, 0), new Vector3(-1, -2), new Vector3(2, -2) };
                int index = 0;
                foreach (var p in NpcPoints)
                    p.transform.position = next ? p.homePosition : assembly.position + offsets[index++ % offsets.Length];
            }
            if (admissionGate != null) admissionGate.SetActive(!State.HasFlag("day1.admitted"));
            if (outdoorGate != null) outdoorGate.SetActive(!State.HasFlag("day1.free_roam"));
        }
        private void BeginQuiz()
        {
            int index = story.quiz.FindIndex(q => !State.HasFlag("day1.test.q" + (story.quiz.IndexOf(q) + 1)));
            if (index < 0)
            {
                GiveOnce("day1.admitted", "day1.identity-card", "身份认证卡");
                return;
            }
            var q = story.quiz[index];
            bool correct = false;
            actor.BeginOptionDialogue(Find("Rules").Prop,
                Lines("认知检测终端", "入站认知校准 " + (index + 1) + "/" + story.quiz.Count + "\n" + q.prompt),
                Choices("correct", q.correctLabel, "review", q.reviewLabel),
                selected =>
                {
                    correct = selected == "correct";
                    actor.ReplaceDialogue(Lines("认知检测终端", correct ? q.correctFeedback : q.reviewFeedback));
                },
                complete => { if (complete && correct) State.SetFlag("day1.test.q" + (index + 1)); });
        }
        public void HandleInteraction(Day1Interaction point, PlayerInteractor interactor)
        {
            if (interactor != actor || IsShadowPlaying || !GameSessionController.GameplayInputAllowed) return;
            if (State.HasFlag("day1.finished")) { Say(point, "桓玉鉴", "今日的准备已经完成。明日按计划继续。", null); return; }
            if (point.Prop.Character != null) { TalkNpc(point); return; }
            switch (point.key)
            {
                case "Rules":
                    if (!State.HasFlag("day1.admitted")) return;
                    if (!State.HasFlag("day1.rules_read")) Talk(point, story.rules, () => State.SetFlag("day1.rules_read"));
                    else if (State.HasFlag("day1.free_roam")) Closing(point);
                    else Talk(point, story.rules);
                    break;
                case "Environment":
                    if (!Require(point, "day1.rules_read", "先召集全员，宣读入驻规则。")) return;
                    Talk(point, story.environment, () => GiveOnce("day1.environment_recorded", "day1.environment-file", "环境数据档案"));
                    break;
                case "Supplies":
                    if (!Require(point, "day1.environment_recorded", "先在总控室完成环境数据记录。")) return;
                    if (State.HasFlag("day1.supplies_received")) Say(point, "桓玉鉴", "生活物资已经收妥。", null);
                    else Talk(point, story.supplies, () => GiveOnce("day1.supplies_received", "day1.supplies", "生活物资"));
                    break;
                case "SeaGate":
                    Talk(point, story.seaGate, () => State.SetFlag("day1.water_deferred"));
                    break;
                case "Tools":
                    if (State.HasFlag("day1.tools_found")) Say(point, "桓玉鉴", "工具已经收妥，回去找机械师。", null);
                    else Talk(point, story.tools, () => GiveOnce("day1.tools_found", "day1.tools", "检修工具"));
                    break;
                case "Power": Talk(point, State.HasFlag("day1.completed.mechanic") ? story.powerAfter : story.powerBefore); break;
            }
        }
        private void TalkNpc(Day1Interaction point)
        {
            string id = point.Prop.Character.Id;
            var script = story.npcs.Find(p => p.roleId == id);
            if (script == null) { Say(point, point.Prop.Character.DisplayName, "此处对白待补充。", null); return; }
            string quest = "day1.quest." + id, done = "day1.completed." + id;
            if (State.HasFlag(done)) { Talk(point, script.completed); return; }
            if (!State.HasFlag(quest))
            {
                bool accept = false;
                actor.BeginOptionDialogue(point.Prop, script.offer, Choices("accept", "接下今日任务", "later", "稍后再来"),
                    choice => { accept = choice == "accept"; actor.ReplaceDialogue(accept ? script.accepted :
                        Lines(point.Prop.Character.DisplayName, "先去准备吧，我在这里等你。")); },
                    complete =>
                    {
                        if (!complete) return;
                        State.SetFlag("day1.met." + id);
                        if (accept) State.SetFlag(quest);
                    });
                return;
            }
            if (!TaskReady(id)) { Talk(point, script.ongoing); return; }
            bool handover = false;
            actor.BeginOptionDialogue(point.Prop, script.handover, Choices("handover", id == CharacterIds.Hydrologist ? "确认明日采水安排" : "汇报 / 交付任务", "later", "稍后确认"),
                choice => { handover = choice == "handover"; actor.ReplaceDialogue(handover ? script.completed :
                    Lines(point.Prop.Character.DisplayName, "准备妥当后再来确认。")); },
                complete =>
                {
                    if (!complete || !handover || !TaskReady(id)) return;
                    if (id == CharacterIds.Mechanic) State.RemoveItem("day1.tools", 1);
                    State.SetFlag(done);
                });
        }
        private bool TaskReady(string id)
        {
            switch (id)
            {
                case CharacterIds.LinXi: return State.HasFlag("day1.diagnosis_done");
                case CharacterIds.Hydrologist: return State.HasFlag("day1.water_deferred");
                case CharacterIds.Geologist: return State.HasFlag("day1.plant_collected");
                case CharacterIds.YangYinglong: return State.HasFlag("day1.threat_cleared");
                case CharacterIds.ContainmentResearcher: return State.HasFlag("day1.repair_done");
                case CharacterIds.Mechanic: return State.HasFlag("day1.parts_collected") && State.Count("day1.tools") > 0;
                default: return false;
            }
        }
        private void Closing(Day1Interaction point)
        {
            bool finish = false;
            actor.BeginOptionDialogue(point.Prop,
                Lines("桓玉鉴", CanFinishDay ? "各项准备已确认。是否结束今日作业？" : "还有准备工作未完成。先核对队员的任务。"),
                new List<PropDialogueChoice> {
                    new PropDialogueChoice { id = "finish", label = "结束第一日", enabled = CanFinishDay,
                        disabledReason = CanFinishDay ? null : "完成五项作业并确认明日采水安排" },
                    new PropDialogueChoice { id = "later", label = "继续探索" } },
                choice => { finish = choice == "finish"; actor.ReplaceDialogue(finish ? story.closing : Lines("桓玉鉴", "再巡视一遍。")); },
                complete => { if (complete && finish && CanFinishDay) State.SetFlag("day1.finished"); });
        }
        private bool Require(Day1Interaction p, string flag, string hint)
        { if (State.HasFlag(flag)) return true; Say(p, "桓玉鉴", hint, null); return false; }
        private void GiveOnce(string flag, string key, string name)
        { if (State.HasFlag(flag)) return; State.AddItem(key, name, 1); State.SetFlag(flag); }
        private void Talk(Day1Interaction p, List<PropDialogueLine> lines, Action complete = null)
        { actor.BeginDialogue(p.Prop, lines, finished => { if (finished) complete?.Invoke(); }); }
        private void Say(Day1Interaction p, string speaker, string text, Action complete) => Talk(p, Lines(speaker, text), complete);
        private static List<PropDialogueLine> Lines(string speaker, string text)
            => new List<PropDialogueLine> { new PropDialogueLine { speaker = speaker, text = text } };
        private static List<PropDialogueChoice> Choices(string a, string al, string b, string bl)
            => new List<PropDialogueChoice> { new PropDialogueChoice { id = a, label = al }, new PropDialogueChoice { id = b, label = bl } };

        private void BeginShadow()
        {
            shadowTime = 0; movementBeforeShadow = movement != null && movement.enabled;
            if (movement != null) movement.enabled = false;
            shadow = new GameObject("无名影子（谭礿占位外观，仅制作层标识）");
            shadow.transform.position = shadowStart.position;
            shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = characters.Find(CharacterIds.TanYue).mapSprite;
            shadowRenderer.color = Color.black;
            shadow.transform.localScale = Vector3.one * 1.1f;
            noiseTexture = new Texture2D(1, 1); noiseTexture.SetPixel(0, 0, Color.white); noiseTexture.Apply();
            noiseSprite = Sprite.Create(noiseTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            noise = new SpriteRenderer[12];
            for (int i = 0; i < noise.Length; i++)
            {
                var pixel = new GameObject("雪花噪点"); pixel.transform.SetParent(shadow.transform, false);
                pixel.transform.localScale = new Vector3(.06f, .045f, 1);
                noise[i] = pixel.AddComponent<SpriteRenderer>(); noise[i].sprite = noiseSprite;
            }
            // Short quiet procedural static placeholder; replace with authored SFX later.
            noiseClip = AudioClip.Create("影子噪声（占位）", 4410, 1, 22050, false);
            var samples = new float[4410]; var random = new System.Random(31);
            for (int i = 0; i < samples.Length; i++) samples[i] = ((float)random.NextDouble() * 2 - 1) * .07f * (1 - i / (float)samples.Length);
            noiseClip.SetData(samples, 0);
            var source = shadow.AddComponent<AudioSource>(); source.clip = noiseClip; source.volume = .35f; source.Play();
        }
        private void AdvanceShadow()
        {
            shadowTime += Time.deltaTime;
            shadow.transform.position = Vector3.Lerp(shadowStart.position, shadowEnd.position, Mathf.Clamp01(shadowTime / 1.5f));
            int order = Mathf.RoundToInt(-shadow.transform.position.y * 32) + 3;
            shadowRenderer.sortingOrder = order;
            shadowRenderer.color = new Color(.03f, .04f, .07f, .6f + .35f * Mathf.Abs(Mathf.Sin(shadowTime * 39)));
            for (int i = 0; i < noise.Length; i++)
            {
                // Bounded to the existing human silhouette's head/torso, not a full-screen flash.
                float x = Mathf.Sin(shadowTime * 41 + i * 8) * .14f;
                float y = .4f + Mathf.Abs(Mathf.Sin(shadowTime * 29 + i * 13)) * .55f;
                noise[i].transform.localPosition = new Vector3(x, y);
                noise[i].sortingOrder = order + 1;
                noise[i].enabled = ((int)(shadowTime * 30) + i) % 3 == 0;
            }
            if (shadowTime < 1.5f) return;
            StopShadow(); shadowResultOpen = true;
            bool started = actor.BeginDialogue(Find("Supplies").Prop, story.shadowAfter, complete =>
            {
                shadowResultOpen = false;
                if (!complete) return;
                State.SetFlag("day1.shadow_seen"); State.SetFlag("day1.free_roam");
            });
            if (!started) shadowResultOpen = false;
        }
        private void StopShadow()
        {
            bool hadShadow = shadow != null;
            if (shadow != null) Destroy(shadow);
            shadow = null; shadowResultOpen = false;
            if (noiseSprite != null) Destroy(noiseSprite);
            if (noiseTexture != null) Destroy(noiseTexture);
            if (noiseClip != null) Destroy(noiseClip);
            if (hadShadow && movement != null) movement.enabled = movementBeforeShadow;
        }

        private string Area
        {
            get
            {
                Vector3 p = actor.transform.position;
                if (p.y < -10) return "码头";
                if (p.x < -9) return "科考站附近";
                if (p.x > 7) return "仓储区";
                if (p.y < -3) return "科研区 · 化学分析室";
                if (p.x < -3) return p.y > 3 ? "认知缓冲间" : "机房";
                return p.y > 5 ? "生活区 · 餐厨走廊" : "总控室";
            }
        }
        private void OnGUI()
        {
            if (!showHUD || actor == null || !GameSessionController.GameplayInputAllowed) return;
            float scale = Mathf.Max(1f, Mathf.Sqrt(Screen.width / 1280f * Screen.height / 720f));
            float screenWidth = Screen.width / scale, screenHeight = Screen.height / scale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1)) * previousMatrix;
            try
            {
            var label = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 15 };
            GUI.Box(new Rect(12, 12, Mathf.Min(500, screenWidth - 24), 95), "");
            GUI.Label(new Rect(24, 19, 460, 25), "第一日 · " + Area, new GUIStyle(label) { fontStyle = FontStyle.Bold });
            GUI.Label(new Rect(24, 48, Mathf.Min(470, screenWidth - 48), 49), Objective, label);
            // Reserve the top-right 66px for the existing session settings button.
            if (GUI.Button(new Rect(screenWidth - 220, 76, 100, 28), "区域图 [M]")) mapOpen = !mapOpen;
            if (GUI.Button(new Rect(screenWidth - 110, 76, 98, 28), "制作备注")) notesOpen = !notesOpen;
            if (State.HasFlag("day1.free_roam") && !actor.IsInDialogue && screenWidth >= 800)
            {
                GUI.Box(new Rect(screenWidth - 245, 112, 233, 210), "今日准备 · 相识 " + MetCount + "/6");
                int i = 0;
                foreach (var p in NpcPoints)
                {
                    string id = p.Prop.Character.Id;
                    string status = State.HasFlag("day1.completed." + id) ? (id == CharacterIds.Hydrologist ? "明日采水" : "已完成") :
                        !State.HasFlag("day1.quest." + id) ? "待交谈" : TaskReady(id) ? "待回报" : "进行中";
                    GUI.Label(new Rect(screenWidth - 232, 144 + i++ * 27, 220, 24), p.Prop.Character.DisplayName + " · " + status, label);
                }
            }
            if (mapOpen && !actor.IsInDialogue) DrawMap(label);
            if (notesOpen)
            {
                GUILayout.BeginArea(new Rect(Mathf.Max(12, screenWidth - 445), 340, 430, Mathf.Max(90, screenHeight - 360)), GUI.skin.box);
                GUILayout.Label("Day1 制作占位（可在配置资产替换）", label);
                notesScroll = GUILayout.BeginScrollView(notesScroll);
                GUILayout.Label(story.planningNotes, label);
                GUILayout.EndScrollView(); GUILayout.EndArea();
            }
            }
            finally { GUI.matrix = previousMatrix; }
        }
        private void DrawMap(GUIStyle label)
        {
            var rect = new Rect(16, 160, 430, 315); GUI.Box(rect, "第一日区域 · 上方为北");
            float ox = rect.x + 28, oy = rect.y + 45;
            MapBox(ox, oy + 10, 100, 170, "室外\n地质学家\n杨应隆", label);
            MapBox(ox, oy + 186, 100, 60, "码头\n机械师", label);
            MapBox(ox + 106, oy + 20, 91, 60, "缓冲间", label);
            MapBox(ox + 106, oy + 85, 91, 82, "机房\n林溪", label);
            MapBox(ox + 203, oy, 94, 70, "生活区", label);
            MapBox(ox + 203, oy + 75, 94, 100, "总控室\n规则 / 环境", label);
            MapBox(ox + 203, oy + 180, 94, 68, "化学室\n水文学家", label);
            MapBox(ox + 303, oy + 75, 70, 100, "仓储区\n收容安保", label);
        }
        private static void MapBox(float x, float y, float w, float h, string text, GUIStyle label)
        { GUI.Box(new Rect(x, y, w, h), ""); GUI.Label(new Rect(x + 5, y + 6, w - 10, h - 8), text, label); }
    }
}
