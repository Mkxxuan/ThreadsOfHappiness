using System;
using System.Collections.Generic;
using System.Linq;
using Toh.Core;

namespace SimRunner
{
    public static class Program
    {
        static int _passed = 0, _failed = 0;
        static List<string> _failures = new List<string>();

        static void Check(string name, bool cond)
        {
            if (cond) { _passed++; Console.WriteLine($"  [通过] {name}"); }
            else { _failed++; _failures.Add(name); Console.WriteLine($"  [失败] {name}"); }
        }

        public static int Main(string[] args)
        {
            Console.WriteLine("==== 《幸福的丝线》规则内核测试 ====");
            TestRanges();
            TestSetup();
            TestRoundFlow();
            TestForcedMax();
            TestProtagonistWin();
            TestFinalReveal();
            TestBelief();
            TestPlanEnumeration();

            Console.WriteLine();
            Console.WriteLine("==== AI vs AI 模拟 ====");
            TestSelfPlay();

            Console.WriteLine();
            Console.WriteLine($"通过 {_passed} / 失败 {_failed}");
            foreach (var f in _failures) Console.WriteLine("  失败项: " + f);
            return _failed == 0 ? 0 : 1;
        }

        static List<CharacterDef> Roster() => GameConfig.DefaultRoster();
        static List<PreviewCardDef> Cards() => GameConfig.DefaultPreviewCards();

        static void TestRanges()
        {
            Console.WriteLine("-- 范围规则 --");
            var r = Roster();
            var center = r.First(c => c.Id == GameConfig.MomohanaId); // 403 (x2,y2)
            var corner = r.First(c => c.Room == 201);                 // (0,0)
            Check("红色：相邻 1 格在范围内", RulesEngine.InRedRange(center, r.First(c => c.Room == 402)));
            Check("红色：对角在范围内", RulesEngine.InRedRange(center, r.First(c => c.Room == 302)));
            Check("红色：距离 2 不在范围内", !RulesEngine.InRedRange(center, r.First(c => c.Room == 405)));
            Check("红色：同一格不算", !RulesEngine.InRedRange(center, center));
            Check("红色：角落只有 3 个邻居",
                r.Count(c => RulesEngine.InRedRange(corner, c)) == 3);
            Check("蓝色：同行在范围内", RulesEngine.InBlueRange(center, r.First(c => c.Room == 401)));
            Check("蓝色：同列在范围内", RulesEngine.InBlueRange(center, r.First(c => c.Room == 303)));
            Check("蓝色：斜对角不在范围内", !RulesEngine.InBlueRange(center, r.First(c => c.Room == 302)));
        }

        static void TestSetup()
        {
            Console.WriteLine("-- 开局设置 --");
            Check("角色数 = 25", Roster().Count == 25);
            Check("预告牌 = 5 张", Cards().Count == 5);
            Check("预告牌内容正确",
                Cards().Sum(c => c.Gifts.Count) == 2 + 2 + 2 + 2 + 3 &&
                Cards()[4].Label == "两蓝一红");
            Check("百花开在正中央 403", Roster()[GameConfig.MomohanaId].Room == 403);

            var s = RulesEngine.NewGame(Roster(), Cards(), new[] { 0, 5, 10 }, false);
            Check("百花开开局即幸福", s.IsHappy(GameConfig.MomohanaId));
            Check("第 1 回合主人公阶段跳过", s.ProtagonistSkipped && s.Phase == Phase.PossessedPhase);
            Check("房间号 201..605 共 25 间",
                Roster().Select(c => c.Room).Distinct().Count() == 25 &&
                Roster().Min(c => c.Room) == 201 && Roster().Max(c => c.Room) == 605);

            bool threw = false;
            try { RulesEngine.NewGame(Roster(), Cards(), new[] { GameConfig.MomohanaId, 0, 1 }, false); }
            catch (ArgumentException) { threw = true; }
            Check("非 IF 路线不能选百花开", threw);
        }

        static void TestRoundFlow()
        {
            Console.WriteLine("-- 回合流程 --");
            var s = RulesEngine.NewGame(Roster(), Cards(), new[] { 0, 5, 10 }, false);

            // 第 1 回合随从阶段：用第 0 张牌（一蓝一红）
            var card0 = Cards()[0];
            var plans = RulesEngine.EnumeratePlans(s, card0);
            Check("第 1 回合存在合法方案", plans.Count > 0);
            var plan = plans[0];
            int before = s.Chars.Count(c => s.IsHappy(c.Id));
            RulesEngine.ApplyPlan(s, plan);
            int after = s.Chars.Count(c => s.IsHappy(c.Id));
            Check("转换数量 = 方案数", after - before == plan.Assignments.Count);
            Check("预告牌 0 已使用", s.UsedCardIds.Contains(0));

            RulesEngine.EndRound(s);
            Check("进入第 2 回合主人公阶段", s.Round == 2 && s.Phase == Phase.ProtagonistPhase);

            // 第 2 回合：监视 3 人
            var sets = RulesEngine.EnumerateMonitorSets(s);
            Check("第 2 回合监视组合数量 = C(22,3) = 1540", sets.Count == 1540);
            RulesEngine.ApplyMonitors(s, sets[0]);
            Check("监视后进入随从阶段", s.Phase == Phase.PossessedPhase);
            Check("被监视的随从不能行动",
                s.CurrentMonitors.Where(id => s.IsPossessed(id))
                    .All(id => RulesEngine.LegalTargets(s, id, GiftColor.Blue).Count == 0 &&
                               RulesEngine.LegalTargets(s, id, GiftColor.Red).Count == 0));

            // 不可监视幸福角色 / 重复监视（在处于主人公阶段的新对局上验证）
            var s2 = RulesEngine.NewGame(Roster(), Cards(), new[] { 0, 5, 10 }, false);
            RulesEngine.ApplyPlan(s2, RulesEngine.EnumeratePlans(s2, Cards()[0])[0]);
            RulesEngine.EndRound(s2);
            bool threw = false;
            try { RulesEngine.ApplyMonitors(s2, new[] { GameConfig.MomohanaId, 1, 2 }); }
            catch (ArgumentException) { threw = true; }
            Check("不能监视幸福状态角色", threw);
            threw = false;
            try { RulesEngine.ApplyMonitors(s2, new[] { 1, 2, 2 }); }
            catch (ArgumentException) { threw = true; }
            Check("不能重复监视同一角色", threw);
        }

        static void TestForcedMax()
        {
            Console.WriteLine("-- 强制满额 / 泄密规则 --");
            // 监视 3 名随从中的 2 名，只留 1 人可行动：两红预告牌最多只能转换 1 人 → 必有 1 份礼物未使用
            var s = RulesEngine.NewGame(Roster(), Cards(), new[] { 0, 5, 10 }, false);
            RulesEngine.ApplyPlan(s, RulesEngine.EnumeratePlans(s, Cards()[0])[0]); // 第 1 回合正常行动
            RulesEngine.EndRound(s);
            // 第 2 回合：监视 0、5 和一个无关角色
            RulesEngine.ApplyMonitors(s, new[] { 0, 5, 24 });
            var twoRed = Cards()[3];
            int max = RulesEngine.MaxConversionsReal(s, twoRed);
            Check("只剩 1 名随从可行动时最大转换 = 1", max == 1);
            var plans = RulesEngine.EnumeratePlans(s, twoRed);
            Check("所有方案均为 1 转换 + 1 未使用礼物（泄密）",
                plans.All(p => p.Assignments.Count == 1 && p.UnusedGifts == 1) && plans.Count > 0);
            Check("ApplyPlan 拒绝未满额方案", AssertThrows(() =>
                RulesEngine.ApplyPlan(s, new Plan { CardId = twoRed.Id, UnusedGifts = 2 })));
        }

        static bool AssertThrows(Action a) { try { a(); return false; } catch { return true; } }

        static void TestProtagonistWin()
        {
            Console.WriteLine("-- 主人公胜利（同时监视 3 名随从） --");
            var s = RulesEngine.NewGame(Roster(), Cards(), new[] { 0, 5, 10 }, false);
            // 第 1 回合随从阶段先正常行动
            var card = Cards()[0];
            RulesEngine.ApplyPlan(s, RulesEngine.EnumeratePlans(s, card)[0]);
            RulesEngine.EndRound(s);
            // 第 2 回合直接监视 3 名随从
            RulesEngine.ApplyMonitors(s, new[] { 0, 5, 10 });
            Check("3 名随从同时被监视 → 主人公胜利", s.Winner == Faction.Protagonist);
            Check("游戏结束", s.Phase == Phase.GameOver);
        }

        static void TestFinalReveal()
        {
            Console.WriteLine("-- 第 6 回合终局核对 --");
            // 随从胜利路径：每回合监视都避开随从
            var s = RulesEngine.NewGame(Roster(), Cards(), new[] { 0, 5, 10 }, false);
            while (s.Winner == null)
            {
                if (s.Phase == Phase.PossessedPhase)
                {
                    var card = RulesEngine.UnusedCards(s)[0];
                    RulesEngine.ApplyPlan(s, RulesEngine.EnumeratePlans(s, card)[0]);
                    RulesEngine.EndRound(s);
                }
                else if (s.Phase == Phase.ProtagonistPhase)
                {
                    var avoid = RulesEngine.EnumerateMonitorSets(s)
                        .First(m => !m.Any(id => s.IsPossessed(id)));
                    RulesEngine.ApplyMonitors(s, avoid);
                }
                else break;
            }
            Check("随从撑到第 6 回合且未被全监视 → 随从胜利", s.Winner == Faction.Possessed);

            // 主人公胜利路径（第 6 回合监视全部随从）
            var s2 = RulesEngine.NewGame(Roster(), Cards(), new[] { 0, 5, 10 }, false);
            while (s2.Winner == null)
            {
                if (s2.Phase == Phase.PossessedPhase)
                {
                    var card = RulesEngine.UnusedCards(s2)[0];
                    RulesEngine.ApplyPlan(s2, RulesEngine.EnumeratePlans(s2, card)[0]);
                    RulesEngine.EndRound(s2);
                }
                else if (s2.Phase == Phase.ProtagonistPhase)
                {
                    if (s2.Round == 6)
                        RulesEngine.ApplyMonitors(s2, new[] { 0, 5, 10 });
                    else
                        RulesEngine.ApplyMonitors(s2, RulesEngine.EnumerateMonitorSets(s2)[0]);
                }
                else break;
            }
            Check("第 6 回合 3 名随从全被监视 → 主人公胜利", s2.Winner == Faction.Protagonist);
        }

        static void TestBelief()
        {
            Console.WriteLine("-- 信念推理 --");
            var s = RulesEngine.NewGame(Roster(), Cards(), new[] { 0, 5, 10 }, false);
            var belief = BeliefSet.Initial(s);
            var publicView = PossessedAI.MakePublicView(s);
            belief.FilterWithHistory(publicView);
            Check("开局候选 = C(24,3) = 2024", belief.Count == 2024);

            // 真实随从行动后，真实三元组必须仍在候选中（信息一致性）
            var card = Cards()[0];
            RulesEngine.ApplyPlan(s, RulesEngine.EnumeratePlans(s, card)[0]);
            RulesEngine.EndRound(s);
            var publicAfter = PossessedAI.MakePublicView(s);
            belief.FilterWithHistory(publicAfter);
            Check("真实三元组仍在候选中", belief.Candidates.Any(c => c.SequenceEqual(new[] { 0, 5, 10 })));
            Check("候选数量减少（信息有效）", belief.Count < 2024 && belief.Count > 0);

            // 被转换角色不可能在候选中
            var converted = s.Chars.Where(c => s.IsHappy(c.Id) && s.HappyRound[c.Id] >= 1).Select(c => c.Id);
            Check("被转换者不在任何候选中",
                belief.Candidates.All(c => !c.Any(id => converted.Contains(id))));
        }

        static void TestPlanEnumeration()
        {
            Console.WriteLine("-- 方案枚举合法性 --");
            var s = RulesEngine.NewGame(Roster(), Cards(), new[] { 0, 5, 10 }, false);
            foreach (var card in Cards())
            {
                var plans = RulesEngine.EnumeratePlans(s, card);
                int max = RulesEngine.MaxConversionsReal(s, card);
                bool ok = plans.All(p =>
                {
                    var members = p.Assignments.Select(a => a.PossessedId).Distinct().ToList();
                    var targets = p.Assignments.Select(a => a.TargetId).Distinct().ToList();
                    return members.Count == p.Assignments.Count &&
                           targets.Count == p.Assignments.Count &&
                           p.Assignments.Count == max &&
                           p.UnusedGifts == card.Gifts.Count - p.Assignments.Count &&
                           p.Assignments.All(a => RulesEngine.CanConvert(s, a.PossessedId, a.TargetId, a.Color)) &&
                           p.Assignments.Count(a => a.Color == GiftColor.Red) <= card.RedCount &&
                           p.Assignments.Count(a => a.Color == GiftColor.Blue) <= card.BlueCount;
                });
                Check($"预告牌[{card.Label}] 的 {plans.Count} 个方案全部合法（最大转换 {max}）", ok && plans.Count > 0);
            }
        }

        static void TestSelfPlay()
        {
            Console.WriteLine("-- 快速模式（无信念） --");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var (p1, q1) = SelfPlay.Simulate(Roster(), Cards(), false, 60, Difficulty.Easy, Difficulty.Easy, 42, useBelief: false);
            sw.Stop();
            Console.WriteLine($"  简单 vs 简单 ×60：主人公 {p1} 胜 / 随从 {q1} 胜（{sw.ElapsedMilliseconds}ms）");
            Check("全部对局正常结束", p1 + q1 == 60);

            Console.WriteLine("-- 信念推理模式 --");
            sw.Restart();
            var (p2, q2) = SelfPlay.Simulate(Roster(), Cards(), false, 12, Difficulty.Normal, Difficulty.Normal, 7, useBelief: true);
            sw.Stop();
            Console.WriteLine($"  普通 vs 普通 ×12：主人公 {p2} 胜 / 随从 {q2} 胜（{sw.ElapsedMilliseconds}ms）");
            Check("信念模式全部对局正常结束", p2 + q2 == 12);

            sw.Restart();
            var (p3, q3) = SelfPlay.Simulate(Roster(), Cards(), false, 4, Difficulty.Hard, Difficulty.Hard, 99, useBelief: true);
            sw.Stop();
            Console.WriteLine($"  困难 vs 困难 ×4：主人公 {p3} 胜 / 随从 {q3} 胜（{sw.ElapsedMilliseconds}ms）");
            Check("困难模式全部对局正常结束", p3 + q3 == 4);

            Console.WriteLine("-- IF 路线 --");
            var (p4, q4) = SelfPlay.Simulate(Roster(), Cards(), true, 8, Difficulty.Normal, Difficulty.Normal, 5, useBelief: true);
            Console.WriteLine($"  IF 路线 普通 vs 普通 ×8：主人公 {p4} 胜 / 随从 {q4} 胜");
            Check("IF 路线对局正常结束", p4 + q4 == 8);
        }
    }
}
