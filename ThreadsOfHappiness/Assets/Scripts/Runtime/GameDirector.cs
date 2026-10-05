using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Toh.Core;

namespace Toh.Runtime
{
    /// <summary>
    /// 游戏导演：驱动全部对局流程与 UI。
    /// 支持三种模式：单人主人公 vs AI / 单人附身随从 vs AI / 双人同屏热座。
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        enum GameMode { None, SingleProtagonist, SinglePossessed, Hotseat }
        enum UiState { ModeSelect, SetupPossessed, SelectMonitors, PossessPickCard, PossessAssign, Busy, GameOver }

        GameMode _mode = GameMode.None;
        Difficulty _diff = Difficulty.Normal;
        bool _ifRoute;
        UiState _ui = UiState.ModeSelect;

        GameState _state;
        BeliefSet _belief;
        ProtagonistAI _protAI;
        PossessedAI _possAI;
        System.Random _rng;
        readonly List<string> _logs = new List<string>();

        bool _showSuspicion;
        bool _protIsHuman, _possIsHuman;

        // 选区状态
        readonly List<int> _monitorSelection = new List<int>();
        readonly List<int> _setupSelection = new List<int>();
        PreviewCardDef _pendingCard;
        readonly List<Assignment> _pendingAssignments = new List<Assignment>();
        int _carrierSelection = -1;

        // UI 引用
        Canvas _canvas;
        CellView[] _cells;
        Text _statusText, _subText, _hintText, _logText, _monitorRecordText;
        RectTransform _buttonRow;
        RectTransform _cardsPanel;
        readonly List<Button> _dynButtons = new List<Button>();
        RectTransform _cover;
        Text _coverTitle, _coverBody;
        RectTransform _coverButtons;
        Button _suspicionBtn;

        class CellView
        {
            public RectTransform Root;
            public Image Bg;
            public Text Room, Name, Susp;
            public Image MonitorToken; public Text MonitorNum;
            public Image GiftToken; public Text GiftNum;
        }
        class CardSlot { public RectTransform Root; public Image Bg; public Text Label, State; public Button Btn; public RectTransform[] GiftSlots; }
        CardSlot[] _cardSlots;

        // ---------- 构建 UI ----------

        /// <summary>把矩形固定为父容器顶部的水平条带（top/height 自父容器顶边起算，避免锚点语义混乱）。</summary>
        static void TopBand(RectTransform rt, float top, float height, float left = 20f, float right = 20f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(left, -(top + height));
            rt.offsetMax = new Vector2(-right, -top);
        }

        void Awake()
        {
            _canvas = UiKit.CreateCanvas();
            var root = _canvas.transform as RectTransform;
            BuildBoard(root);
            BuildRightPanel(root);
            BuildCover(root);
            ShowModeSelect();
        }

        void BuildBoard(RectTransform root)
        {
            var panel = UiKit.CreatePanel(root, "BoardPanel", UiKit.PanelBg,
                new Vector2(0f, 0f), new Vector2(0.56f, 1f), new Vector2(15, 15), new Vector2(-15, -15));
            _cells = new CellView[GameConfig.CharCount];
            const float cell = 190f, gap = 10f, pad = 15f;
            foreach (var c in GameConfig.DefaultRoster())
            {
                var go = new GameObject("Cell_" + c.Room);
                var rt = go.AddComponent<RectTransform>();
                rt.SetParent(panel, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(cell, cell);
                rt.anchoredPosition = new Vector2(pad + c.X * (cell + gap), pad + c.Y * (cell + gap));
                var bg = go.AddComponent<Image>();
                bg.color = UiKit.CellAlive;

                var room = UiKit.CreateText(rt, "Room", c.Room.ToString(), 26, new Color(0.45f, 0.42f, 0.4f), TextAnchor.UpperCenter);
                room.rectTransform.offsetMin = new Vector2(0, cell - 40);
                room.rectTransform.offsetMax = new Vector2(-4, -4);

                var name = UiKit.CreateText(rt, "Name", c.Name, 44, UiKit.TextDark, TextAnchor.MiddleCenter);
                name.rectTransform.offsetMin = new Vector2(4, 30);
                name.rectTransform.offsetMax = new Vector2(-4, -44);

                var susp = UiKit.CreateText(rt, "Susp", "", 26, new Color(0.5f, 0.2f, 0.2f), TextAnchor.LowerCenter);
                susp.rectTransform.offsetMin = new Vector2(0, 4);
                susp.rectTransform.offsetMax = new Vector2(0, 40);

                var mon = new GameObject("MonToken");
                var monRt = mon.AddComponent<RectTransform>();
                monRt.SetParent(rt, false);
                monRt.anchorMin = monRt.anchorMax = new Vector2(0f, 1f);
                monRt.pivot = new Vector2(0.5f, 0.5f);
                monRt.sizeDelta = new Vector2(64, 64);
                monRt.anchoredPosition = new Vector2(36, -36);
                var monImg = mon.AddComponent<Image>();
                monImg.color = UiKit.MonitorColor;
                monImg.raycastTarget = false;
                var monNum = UiKit.CreateText(monRt, "N", "", 30, Color.white, TextAnchor.MiddleCenter);
                monNum.raycastTarget = false;

                var gift = new GameObject("GiftToken");
                var giftRt = gift.AddComponent<RectTransform>();
                giftRt.SetParent(rt, false);
                giftRt.anchorMin = giftRt.anchorMax = new Vector2(1f, 1f);
                giftRt.pivot = new Vector2(0.5f, 0.5f);
                giftRt.sizeDelta = new Vector2(64, 64);
                giftRt.anchoredPosition = new Vector2(-36, -36);
                var giftImg = gift.AddComponent<Image>();
                giftImg.color = UiKit.GiftRed;
                giftImg.raycastTarget = false;
                var giftNum = UiKit.CreateText(giftRt, "N", "", 30, Color.white, TextAnchor.MiddleCenter);
                giftNum.raycastTarget = false;

                var btn = go.AddComponent<Button>();
                int id = c.Id;
                btn.onClick.AddListener(() => OnCellClick(id));

                _cells[id] = new CellView
                {
                    Root = rt, Bg = bg, Room = room, Name = name, Susp = susp,
                    MonitorToken = monImg, MonitorNum = monNum,
                    GiftToken = giftImg, GiftNum = giftNum,
                };
            }
        }

        void BuildRightPanel(RectTransform root)
        {
            var panel = UiKit.CreatePanel(root, "RightPanel", UiKit.PanelBg,
                new Vector2(0.56f, 0f), new Vector2(1f, 1f), new Vector2(12, 15), new Vector2(-15, -15));

            var statusGo = UiKit.CreateText(panel, "Status", "", 44, UiKit.TextDark);
            TopBand(statusGo.rectTransform, 10, 70);
            _statusText = statusGo;

            var subGo = UiKit.CreateText(panel, "Sub", "", 24, new Color(0.35f, 0.35f, 0.4f));
            TopBand(subGo.rectTransform, 84, 40);
            _subText = subGo;

            var hint = UiKit.CreateText(panel, "Hint", "", 34, new Color(0.25f, 0.3f, 0.45f));
            TopBand(hint.rectTransform, 134, 100);
            _hintText = hint;

            var menuBtn = UiKit.CreateButton(panel, "MenuBtn", "回到菜单", 30,
                new Color(0.85f, 0.85f, 0.85f), UiKit.TextDark, new Vector2(210, 60));
            var menuRt = menuBtn.GetComponent<RectTransform>();
            menuRt.anchorMin = menuRt.anchorMax = new Vector2(1f, 1f);
            menuRt.pivot = new Vector2(1f, 1f);
            menuRt.anchoredPosition = new Vector2(-15, -15);
            menuBtn.onClick.AddListener(ConfirmBackToMenu);

            _suspicionBtn = UiKit.CreateButton(panel, "SuspBtn", "怀疑度:关", 30,
                new Color(0.85f, 0.85f, 0.85f), UiKit.TextDark, new Vector2(210, 60));
            var srt = _suspicionBtn.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 1f);
            srt.anchoredPosition = new Vector2(-240, -15);
            _suspicionBtn.onClick.AddListener(ToggleSuspicion);

            _buttonRow = UiKit.CreatePanel(panel, "ButtonRow", Color.clear,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            TopBand(_buttonRow, 244, 100);
            var hlg = _buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandHeight = true;
            hlg.childControlHeight = true;
            hlg.childControlWidth = false;

            _cardsPanel = UiKit.CreatePanel(panel, "CardsPanel", new Color(0f, 0f, 0f, 0.03f),
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            TopBand(_cardsPanel, 354, 210);
            var cardsTitle = UiKit.CreateText(_cardsPanel, "T", "预告牌（主人公可见信息）", 24, new Color(0.4f, 0.4f, 0.4f));
            cardsTitle.rectTransform.offsetMin = new Vector2(8, 178);
            cardsTitle.rectTransform.offsetMax = new Vector2(-8, 0);
            BuildCardSlots();

            var monPanel = UiKit.CreatePanel(panel, "MonitorRecordPanel", new Color(0f, 0f, 0f, 0.03f),
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            TopBand(monPanel, 574, 180);
            var monTitle = UiKit.CreateText(monPanel, "T", "监视记录（回合：被监视者）", 20, new Color(0.4f, 0.4f, 0.4f));
            monTitle.rectTransform.offsetMin = new Vector2(8, 152);
            monTitle.rectTransform.offsetMax = new Vector2(-8, 0);
            var monText = UiKit.CreateText(monPanel, "Rec", "", 20, UiKit.TextDark, TextAnchor.UpperLeft);
            monText.rectTransform.offsetMin = new Vector2(10, 6);
            monText.rectTransform.offsetMax = new Vector2(-10, -30);
            _monitorRecordText = monText;

            var logPanel = UiKit.CreatePanel(panel, "LogPanel", new Color(0f, 0f, 0f, 0.04f),
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            TopBand(logPanel, 764, 271);
            var log = UiKit.CreateText(logPanel, "Log", "", 26, UiKit.TextDark, TextAnchor.UpperLeft);
            log.rectTransform.offsetMin = new Vector2(10, 8);
            log.rectTransform.offsetMax = new Vector2(-10, -8);
            _logText = log;
        }

        void BuildCardSlots()
        {
            _cardSlots = new CardSlot[5];
            var cards = GameConfig.DefaultPreviewCards();
            float w = 140f, step = 150f;
            for (int i = 0; i < cards.Count; i++)
            {
                var go = new GameObject("Card_" + i);
                var rt = go.AddComponent<RectTransform>();
                rt.SetParent(_cardsPanel, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(w, 160);
                rt.anchoredPosition = new Vector2(10 + i * step, 10);
                var bg = go.AddComponent<Image>();
                bg.color = Color.white;
                var label = UiKit.CreateText(rt, "L", cards[i].Label, 30, UiKit.TextDark, TextAnchor.UpperCenter);
                label.rectTransform.offsetMin = new Vector2(2, 116);
                label.rectTransform.offsetMax = new Vector2(-2, -6);
                var st = UiKit.CreateText(rt, "S", "", 24, new Color(0.4f, 0.4f, 0.4f), TextAnchor.UpperCenter);
                st.rectTransform.offsetMin = new Vector2(2, 40);
                st.rectTransform.offsetMax = new Vector2(-2, -48);

                // 泄密礼物指示物：受到阻碍时未使用的礼物显示在牌面下方
                var giftSlots = new RectTransform[3];
                for (int k = 0; k < 3; k++)
                {
                    var gt = new GameObject("Leak_" + k);
                    var grt = gt.AddComponent<RectTransform>();
                    grt.SetParent(rt, false);
                    grt.anchorMin = grt.anchorMax = new Vector2(0f, 0f);
                    grt.pivot = new Vector2(0.5f, 0.5f);
                    grt.sizeDelta = new Vector2(28, 28);
                    grt.anchoredPosition = new Vector2(70, 22);
                    var gimg = gt.AddComponent<Image>();
                    gimg.raycastTarget = false;
                    gt.SetActive(false);
                    giftSlots[k] = grt;
                }

                var btn = go.AddComponent<Button>();
                int id = i;
                btn.onClick.AddListener(() => OnCardClick(id));
                _cardSlots[i] = new CardSlot { Root = rt, Bg = bg, Label = label, State = st, Btn = btn, GiftSlots = giftSlots };
            }
        }

        void BuildCover(RectTransform root)
        {
            _cover = UiKit.CreatePanel(root, "Cover", new Color(0.12f, 0.13f, 0.18f, 1f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _cover.SetAsLastSibling();

            // 标题固定在顶部条带，避免坠入按钮区
            _coverTitle = UiKit.CreateText(_cover, "Title", "", 80, Color.white, TextAnchor.MiddleCenter);
            TopBand(_coverTitle.rectTransform, 40, 130, 100, 100);

            // 说明文字固定在中部条带
            _coverBody = UiKit.CreateText(_cover, "Body", "", 40, new Color(0.85f, 0.86f, 0.9f), TextAnchor.UpperCenter);
            TopBand(_coverBody.rectTransform, 190, 350, 160, 160);

            // 按钮区固定在底部
            _coverButtons = UiKit.CreatePanel(_cover, "CoverButtons", Color.clear,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-380, 20), new Vector2(380, 570));
            var vlg = _coverButtons.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
        }

        // ---------- 通用 UI 操作 ----------

        void ClearButtons()
        {
            foreach (var b in _dynButtons)
                if (b != null) Destroy(b.gameObject);
            _dynButtons.Clear();
        }

        Button AddButton(string label, System.Action cb, Color? bg = null, Vector2? size = null)
        {
            var b = UiKit.CreateButton(_buttonRow, "Btn_" + label, label, 34,
                bg ?? new Color(0.3f, 0.5f, 0.85f), Color.white, size ?? new Vector2(260, 90));
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.minWidth = size?.x ?? 260;
            le.preferredWidth = size?.x ?? 260;
            b.onClick.AddListener(() => cb());
            _dynButtons.Add(b);
            return b;
        }

        void ShowCover(string title, string body, (string label, System.Action cb)? main = null,
            (string label, System.Action cb)? sec = null)
        {
            _cover.gameObject.SetActive(true);
            _coverTitle.text = title;
            _coverBody.text = body;
            foreach (Transform t in _coverButtons) Destroy(t.gameObject);
            if (main != null)
            {
                var b = UiKit.CreateButton(_coverButtons, "Main", main.Value.label, 40,
                    new Color(0.3f, 0.55f, 0.9f), Color.white, new Vector2(680, 96));
                b.onClick.AddListener(() => main.Value.cb());
            }
            if (sec != null)
            {
                var b = UiKit.CreateButton(_coverButtons, "Sec", sec.Value.label, 34,
                    new Color(0.55f, 0.55f, 0.6f), Color.white, new Vector2(680, 80));
                b.onClick.AddListener(() => sec.Value.cb());
            }
        }

        void HideCover() => _cover.gameObject.SetActive(false);

        void Log(string line)
        {
            _logs.Add(line);
            if (_logs.Count > 8) _logs.RemoveAt(0);
            _logText.text = string.Join("\n", _logs);
        }

        void ToggleSuspicion()
        {
            _showSuspicion = !_showSuspicion;
            _suspicionBtn.GetComponentInChildren<Text>().text = _showSuspicion ? "怀疑度:开" : "怀疑度:关";
            RenderBoard();
        }

        void ConfirmBackToMenu()
        {
            ShowCover("回到菜单？", "当前对局进度将丢失。",
                ("确认", () => ShowModeSelect()),
                ("继续游戏", HideCover));
        }

        // ---------- 流程 ----------

        void ShowModeSelect()
        {
            _mode = GameMode.None;
            _state = null;
            _ui = UiState.ModeSelect;
            _logs.Clear();
            _logText.text = "";
            _showSuspicion = false;
            if (_suspicionBtn != null)
                _suspicionBtn.GetComponentInChildren<Text>().text = "怀疑度:关";

            _statusText.text = "《幸福的丝线》";
            _subText.text = "选择模式开始游戏。";
            _hintText.text = "";
            ClearButtons();
            RenderBoardEmpty();

            ShowCover("幸福的丝线",
                "由 25 名少女编织出的“幸福”故事\n\n主人公阵营：监视学园，找出 3 名附身随从，中断《幸福》的连锁\n附身随从阵营：隐藏自己，让《幸福》的连锁持续到最后\n\n（下方可调整 AI 难度与 IF 路线）",
                ("单人 · 玩主人公方（对抗 AI 随从）", () => StartGame(GameMode.SingleProtagonist)),
                ("单人 · 玩附身随从方（对抗 AI 主人公）", () => StartGame(GameMode.SinglePossessed)));
            // 第三个按钮（热座）+ 设置按钮：手动加进封面按钮区
            var hot = UiKit.CreateButton(_coverButtons, "Hot", "双人 · 同屏热座对战", 40,
                new Color(0.3f, 0.55f, 0.9f), Color.white, new Vector2(680, 96));
            hot.onClick.AddListener(() => StartGame(GameMode.Hotseat));
            var diffBtn = UiKit.CreateButton(_coverButtons, "Diff", "AI 难度：普通", 32,
                new Color(0.45f, 0.45f, 0.5f), Color.white, new Vector2(680, 76));
            diffBtn.onClick.AddListener(() =>
            {
                _diff = (Difficulty)(((int)_diff + 1) % 3);
                diffBtn.GetComponentInChildren<Text>().text = "AI 难度：" + DiffName(_diff);
            });
            var ifBtn = UiKit.CreateButton(_coverButtons, "If", "IF 路线：关", 32,
                new Color(0.45f, 0.45f, 0.5f), Color.white, new Vector2(680, 76));
            ifBtn.onClick.AddListener(() =>
            {
                _ifRoute = !_ifRoute;
                ifBtn.GetComponentInChildren<Text>().text = "IF 路线：" + (_ifRoute ? "开（百花开可成为随从）" : "关");
            });
        }

        string DiffName(Difficulty d) => d == Difficulty.Easy ? "简单" : d == Difficulty.Normal ? "普通" : "困难";

        void StartGame(GameMode mode)
        {
            _mode = mode;
            _rng = new System.Random();
            _protAI = new ProtagonistAI(_diff, _rng);
            _possAI = new PossessedAI(_diff, _rng);
            // 单人主人公 → 主人公是人；单人随从 → 随从是人；热座 → 都是人
            _protIsHuman = mode != GameMode.SinglePossessed;
            _possIsHuman = mode != GameMode.SingleProtagonist;

            _monitorSelection.Clear();
            _setupSelection.Clear();
            _pendingAssignments.Clear();
            _pendingCard = null;
            _carrierSelection = -1;

            if (_possIsHuman)
            {
                // 人类随从：先遮屏防偷看，再选择 3 名附身随从
                ShowCover("对局设置",
                    _protIsHuman
                        ? "请把屏幕交给附身随从玩家。\n主人公玩家请移开视线！"
                        : "你将扮演附身随从阵营。\n接下来秘密指定 3 名附身随从。",
                    ("开始设置", () =>
                    {
                        HideCover();
                        _ui = UiState.SetupPossessed;
                        Log("对局开始：请附身随从阵营选择 3 名附身随从。");
                        RefreshAll();
                    }));
            }
            else
            {
                // AI 随从：秘密选人
                var template = RulesEngine.NewGame(GameConfig.DefaultRoster(), GameConfig.DefaultPreviewCards(), new[] { 0, 1, 2 }, _ifRoute);
                var triple = _possAI.ChoosePossessed(template);
                BeginWithPossessed(triple);
            }
            RefreshAll();
        }

        void BeginWithPossessed(int[] triple)
        {
            _state = RulesEngine.NewGame(GameConfig.DefaultRoster(), GameConfig.DefaultPreviewCards(), triple, _ifRoute);
            _belief = BeliefSet.Initial(_state);
            _belief.FilterWithHistory(PossessedAI.MakePublicView(_state));
            _ui = UiState.Busy;
            HideCover();
            Log("对局开始。第 1 回合主人公阶段因恐慌跳过。");
            Log("百花开已变为《幸福》状态。");
            RefreshAll();
            DrivePhase();
        }

        void DrivePhase()
        {
            if (_state == null || _state.Winner != null)
            {
                ShowGameOver();
                return;
            }
            RefreshAll();
            ClearButtons();

            if (_state.Phase == Phase.ProtagonistPhase)
            {
                if (_protIsHuman)
                {
                    _ui = UiState.SelectMonitors;
                    _monitorSelection.Clear();
                    _hintText.text = $"主人公阶段：点击 {GameConfig.MonitorsPerRound} 张角色牌放置监视指示物。";
                    AddButton("确认监视", ConfirmMonitors);
                }
                else
                {
                    _ui = UiState.Busy;
                    _hintText.text = "AI 主人公正在推理……";
                    StartCoroutine(AiProtagonistTurn());
                }
            }
            else if (_state.Phase == Phase.PossessedPhase)
            {
                if (_possIsHuman)
                {
                    ShowCover("附身随从回合",
                        _protIsHuman
                            ? "请把屏幕交给附身随从玩家。\n主人公玩家请移开视线！"
                            : "现在轮到你（附身随从阵营）行动。",
                        ("开始行动", EnterPossessPickCard));
                }
                else
                {
                    _ui = UiState.Busy;
                    _hintText.text = "附身随从阵营正在行动……";
                    StartCoroutine(AiPossessedTurn());
                }
            }
        }

        IEnumerator AiProtagonistTurn()
        {
            yield return new WaitForSeconds(0.7f);
            var monitors = _protAI.ChooseMonitors(_state, _belief);
            RulesEngine.ApplyMonitors(_state, monitors);
            // 监视去向已常驻显示在右侧「监视记录」栏，不再写进日志
            if (_state.Winner != null) { ShowGameOver(); yield break; }
            DrivePhase();
        }

        IEnumerator AiPossessedTurn()
        {
            yield return new WaitForSeconds(0.8f);
            var (cardId, plan) = _possAI.ChooseAction(_state, _belief);
            var card = _state.Cards[cardId];
            RulesEngine.ApplyPlan(_state, plan);
            Log($"第 {_state.Round} 回合：随从方使用预告牌【{card.Label}】。");
            foreach (var a in plan.Assignments)
                Log($"　{_state.Char(a.TargetId).Name} 被{(a.Color == GiftColor.Red ? "红色" : "蓝色")}礼物变为《幸福》状态。");
            if (plan.UnusedGifts > 0)
                Log($"　有 {plan.UnusedGifts} 份礼物未使用——随从方似乎受到了阻碍！");
            AfterPossessedAction();
        }

        void AfterPossessedAction()
        {
            _belief.FilterWithHistory(PossessedAI.MakePublicView(_state));
            _ui = UiState.Busy;
            RefreshAll();
            ClearButtons();
            _hintText.text = "结束阶段：指示物翻面。";
            AddButton("进入下一回合", () =>
            {
                RulesEngine.EndRound(_state);
                DrivePhase();
            }, new Color(0.3f, 0.5f, 0.85f), new Vector2(330, 90));
        }

        void ConfirmMonitors()
        {
            if (_monitorSelection.Count != GameConfig.MonitorsPerRound)
            {
                _hintText.text = $"需要恰好选择 {GameConfig.MonitorsPerRound} 个监视目标（当前 {_monitorSelection.Count}）。";
                return;
            }
            RulesEngine.ApplyMonitors(_state, _monitorSelection);
            // 监视去向已常驻显示在右侧「监视记录」栏，不再写进日志
            _monitorSelection.Clear();
            if (_state.Winner != null) { ShowGameOver(); return; }
            DrivePhase();
        }

        // ---------- 附身随从（人类）交互 ----------

        void EnterPossessPickCard()
        {
            HideCover();
            _ui = UiState.PossessPickCard;
            _pendingCard = null;
            _pendingAssignments.Clear();
            _carrierSelection = -1;
            RefreshAll();
            ClearButtons();
            _hintText.text = "附身随从阶段：先选择 1 张预告牌（点击下方卡牌）。";
        }

        void OnCardClick(int cardId)
        {
            if (_ui != UiState.PossessPickCard) return;
            if (_state.UsedCardIds.Contains(cardId)) return;
            _pendingCard = _state.Cards[cardId];
            _ui = UiState.PossessAssign;
            _pendingAssignments.Clear();
            _carrierSelection = -1;
            ClearButtons();
            _hintText.text = $"使用【{_pendingCard.Label}】：依次点击你的随从（紫色）作为礼物持有者，再点击高亮目标。";
            AddButton("撤销", UndoAssignment, new Color(0.6f, 0.6f, 0.65f), new Vector2(150, 90));
            AddButton("确认行动", ConfirmPossessAction, new Color(0.3f, 0.55f, 0.9f), new Vector2(210, 90));
            RefreshAll();
        }

        List<int> MyUnmonitoredPossessed()
            => _state.Possessed.Where(p => _state.IsAlive(p) && !_state.IsMonitoredThisRound(p)).ToList();

        List<int> PendingLegalTargets(int carrier, GiftColor color)
        {
            var used = new HashSet<int>(_pendingAssignments.Select(a => a.TargetId));
            return RulesEngine.LegalTargets(_state, carrier, color).Where(t => !used.Contains(t)).ToList();
        }

        void UndoAssignment()
        {
            if (_pendingAssignments.Count == 0) return;
            _pendingAssignments.RemoveAt(_pendingAssignments.Count - 1);
            _carrierSelection = -1;
            RefreshAll();
        }

        void ConfirmPossessAction()
        {
            int max = RulesEngine.MaxConversionsReal(_state, _pendingCard);
            if (_pendingAssignments.Count != max)
            {
                _hintText.text = max > _pendingAssignments.Count
                    ? $"规则要求尽可能转换到预告人数：还可转换 {max - _pendingAssignments.Count} 人（规则 3/4 的限制下）。"
                    : "方案数量异常。";
                return;
            }
            var plan = new Plan { CardId = _pendingCard.Id, UnusedGifts = _pendingCard.Gifts.Count - _pendingAssignments.Count };
            plan.Assignments.AddRange(_pendingAssignments);
            RulesEngine.ApplyPlan(_state, plan);
            var card = _pendingCard;
            Log($"第 {_state.Round} 回合：随从方使用预告牌【{card.Label}】。");
            foreach (var a in plan.Assignments)
                Log($"　{_state.Char(a.TargetId).Name} 被{(a.Color == GiftColor.Red ? "红色" : "蓝色")}礼物变为《幸福》状态。");
            if (plan.UnusedGifts > 0)
                Log($"　有 {plan.UnusedGifts} 份礼物未使用——被主人公监视阻碍了！");
            _pendingCard = null;
            _pendingAssignments.Clear();
            AfterPossessedAction();
        }

        // ---------- 棋盘点击 ----------

        void OnCellClick(int id)
        {
            switch (_ui)
            {
                case UiState.SetupPossessed: SetupClick(id); break;
                case UiState.SelectMonitors: MonitorClick(id); break;
                case UiState.PossessAssign: PossessAssignClick(id); break;
            }
        }

        void SetupClick(int id)
        {
            if (!_ifRoute && id == GameConfig.MomohanaId)
            {
                _hintText.text = "百花开在故事中开局已变为《幸福》状态，不能选择（IF 路线除外）。";
                return;
            }
            if (_setupSelection.Contains(id)) _setupSelection.Remove(id);
            else if (_setupSelection.Count < GameConfig.PossessedCount) _setupSelection.Add(id);
            else _hintText.text = "最多选择 3 名附身随从，点击已选角色可取消。";

            ClearButtons();
            if (_setupSelection.Count == GameConfig.PossessedCount)
            {
                _hintText.text = "确认这 3 名附身随从？确认后将对局正式开始。";
                AddButton("确认随从", () =>
                {
                    BeginWithPossessed(_setupSelection.ToArray());
                }, new Color(0.75f, 0.4f, 0.75f), new Vector2(240, 90));
            }
            else
            {
                _hintText.text = $"选择 {GameConfig.PossessedCount} 名附身随从（当前已选 {_setupSelection.Count}）。";
            }
            RenderBoard();
        }

        void MonitorClick(int id)
        {
            if (!_state.CanBeMonitored(id))
            {
                if (_state.IsHappy(id)) _hintText.text = "不能监视《幸福》状态的角色。";
                return;
            }
            if (_monitorSelection.Contains(id)) _monitorSelection.Remove(id);
            else if (_monitorSelection.Count < GameConfig.MonitorsPerRound) _monitorSelection.Add(id);
            RenderBoard();
        }

        void PossessAssignClick(int id)
        {
            if (_pendingCard == null) return;

            // 尚未确定当前礼物？→ 按礼物顺序处理
            if (_carrierSelection < 0)
            {
                // 点击的是自己的随从 → 选为持有者
                if (_state.Possessed.Contains(id))
                {
                    if (_state.IsMonitoredThisRound(id))
                    {
                        _hintText.text = $"{_state.Char(id).Name} 正被监视，本回合不能行动（规则 4）。";
                        return;
                    }
                    if (_pendingAssignments.Any(a => a.PossessedId == id))
                    {
                        _hintText.text = $"{_state.Char(id).Name} 本回合已经行动过了（规则 3）。";
                        return;
                    }
                    GiftColor color = CurrentGiftColor();
                    if (PendingLegalTargets(id, color).Count == 0)
                    {
                        _hintText.text = $"{_state.Char(id).Name} 的{(color == GiftColor.Red ? "红色" : "蓝色")}礼物范围内没有可转换的存活者。";
                        return;
                    }
                    _carrierSelection = id;
                    _hintText.text = $"{_state.Char(id).Name} 持有{(color == GiftColor.Red ? "红" : "蓝")}色礼物：点击一个高亮目标。";
                    RenderBoard();
                }
                else
                {
                    _hintText.text = "先点击你的随从（紫色高亮）作为礼物持有者。";
                }
            }
            else
            {
                GiftColor color = CurrentGiftColor();
                if (PendingLegalTargets(_carrierSelection, color).Contains(id))
                {
                    _pendingAssignments.Add(new Assignment(_carrierSelection, color, id));
                    _carrierSelection = -1;
                    int max = RulesEngine.MaxConversionsReal(_state, _pendingCard);
                    int remainingGifts = _pendingCard.Gifts.Count - _pendingAssignments.Count;
                    if (_pendingAssignments.Count >= max)
                        _hintText.text = $"已达到本回合最大转换数（{max}），可确认行动。";
                    else
                        _hintText.text = $"已转换 {_pendingAssignments.Count}/{max}。继续分配剩余 {remainingGifts} 份礼物。";
                    RenderBoard();
                }
                else
                {
                    _hintText.text = "该目标不在持有者的礼物范围内，或已被转换。";
                }
            }
        }

        GiftColor CurrentGiftColor()
        {
            // 依次消耗预告牌上的礼物（蓝/红按牌面顺序），跳过已被用完的颜色
            int red = _pendingAssignments.Count(a => a.Color == GiftColor.Red);
            int blue = _pendingAssignments.Count(a => a.Color == GiftColor.Blue);
            foreach (var g in _pendingCard.Gifts)
            {
                if (g == GiftColor.Red && red > 0) { red--; continue; }
                if (g == GiftColor.Blue && blue > 0) { blue--; continue; }
                return g;
            }
            return GiftColor.Red; // 不会到达
        }

        // ---------- 结算 ----------

        void ShowGameOver()
        {
            _ui = UiState.GameOver;
            RefreshAll();
            ClearButtons();
            string winner = _state.Winner == Faction.Protagonist ? "主人公阵营胜利！" : "附身随从阵营胜利！";
            string reveal = "附身随从是：" + string.Join("、", _state.Possessed.Select(id => _state.Char(id).Name));
            Log(winner + " " + reveal);
            ShowCover(winner,
                _state.WinReason + "\n\n" + reveal,
                ("再来一局", ShowModeSelect));
        }

        // ---------- 渲染 ----------

        void RenderBoardEmpty()
        {
            foreach (var c in GameConfig.DefaultRoster())
            {
                var v = _cells[c.Id];
                v.Bg.color = UiKit.CellAlive;
                v.MonitorToken.gameObject.SetActive(false);
                v.GiftToken.gameObject.SetActive(false);
                v.Susp.text = "";
            }
            if (_monitorRecordText != null) _monitorRecordText.text = "";
        }

        void UpdateMonitorRecord()
        {
            if (_monitorRecordText == null) return;
            if (_state == null) { _monitorRecordText.text = ""; return; }
            var lines = new List<string>();
            foreach (var rec in _state.History)
                if (rec.Monitored != null && rec.Monitored.Count > 0)
                    lines.Add($"第{rec.Round}回：" + string.Join("、", rec.Monitored.Select(id => _state.Char(id).Name)));
            _monitorRecordText.text = lines.Count > 0 ? string.Join("\n", lines) : "（尚无监视记录）";
        }

        void RenderBoard()
        {
            if (_state == null) { RenderBoardEmpty(); return; }

            var prob = (_showSuspicion && _belief != null) ? _belief.CharProbabilities() : null;
            var setupPool = (_ui == UiState.SetupPossessed) ? new HashSet<int>(
                GameConfig.DefaultRoster().Where(c => _ifRoute || c.Id != GameConfig.MomohanaId).Select(c => c.Id)) : null;

            // 当前选中持有者的合法目标
            HashSet<int> legalTargets = null;
            HashSet<int> myPossessed = null;
            if (_ui == UiState.PossessAssign && _pendingCard != null)
            {
                myPossessed = new HashSet<int>(_state.Possessed.Where(p => _state.IsAlive(p)));
                if (_carrierSelection >= 0)
                    legalTargets = new HashSet<int>(PendingLegalTargets(_carrierSelection, CurrentGiftColor()));
            }

            for (int i = 0; i < GameConfig.CharCount; i++)
            {
                var v = _cells[i];
                var def = _state.Char(i);

                // 底色
                Color col = _state.IsHappy(i) ? UiKit.CellHappy : UiKit.CellAlive;
                if (_ui == UiState.SetupPossessed && setupPool.Contains(i)) col = new Color(0.96f, 0.93f, 0.98f);
                if (myPossessed != null && myPossessed.Contains(i) && !_state.IsMonitoredThisRound(i)) col = UiKit.CellPossessed;
                if (legalTargets != null && legalTargets.Contains(i)) col = UiKit.CellTarget;
                if (_monitorSelection.Contains(i)) col = UiKit.CellSelect;
                if (_setupSelection.Contains(i)) col = UiKit.CellPossessed;
                v.Bg.color = col;

                // 监视指示物：当前回合生效中（黄色），历史回合失效（灰色保持不变）并显示当时回合数
                if (_state.IsMonitoredThisRound(i))
                {
                    v.MonitorToken.gameObject.SetActive(true);
                    v.MonitorToken.color = UiKit.MonitorActive;
                    v.MonitorNum.color = new Color(0.25f, 0.2f, 0.05f);
                    v.MonitorNum.text = _state.Round.ToString();
                }
                else
                {
                    int monRound = -1;
                    foreach (var rec in _state.History)
                        if (rec.Monitored.Contains(i)) monRound = rec.Round;
                    if (monRound > 0)
                    {
                        v.MonitorToken.gameObject.SetActive(true);
                        v.MonitorToken.color = UiKit.MonitorExpired;
                        v.MonitorNum.color = new Color(0.97f, 0.97f, 0.97f);
                        v.MonitorNum.text = monRound.ToString();
                    }
                    else v.MonitorToken.gameObject.SetActive(false);
                }

                // 礼物指示物（公开信息）
                if (_state.IsHappy(i) && _state.HappyRound[i] >= 1)
                {
                    v.GiftToken.gameObject.SetActive(true);
                    v.GiftToken.color = _state.HappyGift[i] == GiftColor.Red ? UiKit.GiftRed : UiKit.GiftBlue;
                    v.GiftNum.text = _state.HappyRound[i].ToString();
                }
                else v.GiftToken.gameObject.SetActive(false);

                v.Susp.text = prob != null ? $"{prob[i] * 100f:F0}%" : "";
            }

            RenderCards();
        }

        void RenderCards()
        {
            if (_state == null)
            {
                for (int i = 0; i < _cardSlots.Length; i++)
                {
                    _cardSlots[i].Bg.color = Color.white;
                    _cardSlots[i].State.text = "";
                    RenderSlotGifts(_cardSlots[i], null);
                    _cardSlots[i].Btn.interactable = false;
                }
                return;
            }
            for (int i = 0; i < _cardSlots.Length; i++)
            {
                var slot = _cardSlots[i];
                var rec = _state.History.FirstOrDefault(r => r.UsedCardId == i);
                if (rec != null)
                {
                    slot.Bg.color = new Color(0.82f, 0.82f, 0.84f);
                    var leftover = UnusedGiftColors(rec);
                    slot.State.text = $"第{rec.Round}回合用\n" + (leftover.Count > 0 ? "礼物泄密" : "无泄密");
                    RenderSlotGifts(slot, leftover);
                    slot.Btn.interactable = false;
                }
                else
                {
                    bool pickable = _ui == UiState.PossessPickCard;
                    slot.Bg.color = pickable ? new Color(1f, 0.96f, 0.75f) : Color.white;
                    slot.State.text = "未使用";
                    RenderSlotGifts(slot, null);
                    slot.Btn.interactable = pickable;
                }
                if (_pendingCard != null && _pendingCard.Id == i)
                    slot.Bg.color = new Color(0.75f, 0.9f, 1f);
            }
        }

        /// <summary>计算某回合预告牌上未使用（泄密）礼物的颜色序列：牌面礼物减去实际消耗。</summary>
        List<GiftColor> UnusedGiftColors(RoundRecord rec)
        {
            var used = rec.Conversions.Select(x => x.Color).ToList();
            var res = new List<GiftColor>();
            foreach (var g in _state.Cards[rec.UsedCardId].Gifts)
                if (!used.Remove(g)) res.Add(g);
            return res;
        }

        /// <summary>把泄密礼物指示物摆到牌面下方的指示区（居中分布）。</summary>
        void RenderSlotGifts(CardSlot slot, List<GiftColor> gifts)
        {
            int n = gifts?.Count ?? 0;
            float[] xs = n == 1 ? new[] { 70f } : n == 2 ? new[] { 48f, 92f } : new[] { 32f, 70f, 108f };
            for (int k = 0; k < slot.GiftSlots.Length; k++)
            {
                var grt = slot.GiftSlots[k];
                if (k < n)
                {
                    grt.gameObject.SetActive(true);
                    grt.anchoredPosition = new Vector2(xs[k], 22);
                    grt.GetComponent<Image>().color = gifts[k] == GiftColor.Red ? UiKit.GiftRed : UiKit.GiftBlue;
                }
                else grt.gameObject.SetActive(false);
            }
        }

        void RefreshAll()
        {
            UpdateMonitorRecord();
            if (_state == null)
            {
                RenderBoardEmpty();
                RenderCards();
                return;
            }
            string modeName = _mode == GameMode.SingleProtagonist ? "单人·主人公方"
                : _mode == GameMode.SinglePossessed ? "单人·附身随从方" : "双人热座";
            _statusText.text = $"第 {_state.Round} / {GameConfig.MaxRounds} 回合";
            _subText.text = $"{modeName}　难度:{DiffName(_diff)}" + (_ifRoute ? "　IF路线" : "");
            RenderBoard();
        }
    }
}
