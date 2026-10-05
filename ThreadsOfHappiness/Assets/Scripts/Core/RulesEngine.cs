using System.Collections.Generic;
using System.Linq;

namespace Toh.Core
{
    /// <summary>一次《幸福》操作：某附身随从用某色礼物把某人变成幸福状态。</summary>
    public class Assignment
    {
        public int PossessedId;
        public GiftColor Color;
        public int TargetId;

        public Assignment(int possessedId, GiftColor color, int targetId)
        {
            PossessedId = possessedId;
            Color = color;
            TargetId = targetId;
        }
    }

    /// <summary>附身随从阶段的一个完整行动方案（只能是"最大转换数"的方案）。</summary>
    public class Plan
    {
        public int CardId;
        public List<Assignment> Assignments = new List<Assignment>();
        public int UnusedGifts;

        public Plan Clone()
        {
            var p = new Plan { CardId = CardId, UnusedGifts = UnusedGifts };
            p.Assignments.AddRange(Assignments);
            return p;
        }
    }

    /// <summary>规则引擎：全部规则校验与执行。纯逻辑，不依赖 Unity。</summary>
    public static class RulesEngine
    {
        // ---------- 距离 / 范围 ----------

        /// <summary>红色礼物：周围 8 格（切比雪夫距离 1，且不是自己）。</summary>
        public static bool InRedRange(CharacterDef a, CharacterDef b)
            => a.Id != b.Id && System.Math.Abs(a.X - b.X) <= 1 && System.Math.Abs(a.Y - b.Y) <= 1;

        /// <summary>蓝色礼物：同行或同列直线（含整个行列，不含自己）。</summary>
        public static bool InBlueRange(CharacterDef a, CharacterDef b)
            => a.Id != b.Id && (a.X == b.X || a.Y == b.Y);

        public static bool InRange(CharacterDef a, CharacterDef b, GiftColor c)
            => c == GiftColor.Red ? InRedRange(a, b) : InBlueRange(a, b);

        // ---------- 开局 ----------

        /// <summary>创建新对局。possessedIds：附身随从阵营秘密指定的 3 人（非 IF 路线不可选百花开）。</summary>
        public static GameState NewGame(List<CharacterDef> roster, List<PreviewCardDef> cards,
            IReadOnlyCollection<int> possessedIds, bool ifRoute)
        {
            var s = new GameState
            {
                Chars = roster,
                Cards = cards,
                IfRoute = ifRoute,
                Phase = Phase.ProtagonistPhase,
            };
            for (int i = 0; i < s.States.Length; i++)
            {
                s.States[i] = CharState.Alive;
                s.HappyRound[i] = -1;
            }

            if (possessedIds.Count != GameConfig.PossessedCount)
                throw new System.ArgumentException("附身随从必须是 3 人");
            foreach (var id in possessedIds)
            {
                if (!ifRoute && id == GameConfig.MomohanaId)
                    throw new System.ArgumentException("非 IF 路线不能选择百花开");
                s.Possessed.Add(id);
            }

            // 故事发展：百花开开局即变为《幸福》状态（IF 路线中她存活）
            if (!ifRoute)
            {
                int m = GameConfig.MomohanaId;
                s.States[m] = CharState.Happy;
                s.HappyRound[m] = 0;
                s.HappyGift[m] = GiftColor.Red; // 剧情设定，不使用礼物
            }

            BeginRound(s);
            return s;
        }

        static void BeginRound(GameState s)
        {
            s.CurrentMonitors.Clear();
            s.Phase = Phase.ProtagonistPhase;
            // 第 1 回合：百花开开局变幸福导致恐慌，主人公阶段跳过
            if (s.Round == 1)
            {
                s.ProtagonistSkipped = true;
                EnterPossessedPhase(s);
            }
        }

        // ---------- 主人公阶段 ----------

        /// <summary>本回合可放置监视指示物的角色（存活且本回合尚未被监视）。</summary>
        public static List<int> MonitorableChars(GameState s)
            => s.Chars.Where(c => s.CanBeMonitored(c.Id)).Select(c => c.Id).ToList();

        /// <summary>枚举所有合法的监视选择组合（3 人）。</summary>
        public static List<int[]> EnumerateMonitorSets(GameState s)
        {
            var pool = MonitorableChars(s);
            var result = new List<int[]>();
            Combine(pool, GameConfig.MonitorsPerRound, 0, new int[GameConfig.MonitorsPerRound], result);
            return result;
        }

        static void Combine(List<int> pool, int k, int start, int[] cur, List<int[]> result)
        {
            if (k == 0) { result.Add((int[])cur.Clone()); return; }
            for (int i = start; i <= pool.Count - k; i++)
            {
                cur[cur.Length - k] = pool[i];
                Combine(pool, k - 1, i + 1, cur, result);
            }
        }

        /// <summary>主人公放置本回合监视指示物，并推进到附身随从阶段（或直接终局）。</summary>
        public static void ApplyMonitors(GameState s, IReadOnlyList<int> ids)
        {
            if (s.Phase != Phase.ProtagonistPhase)
                throw new System.InvalidOperationException("当前不是主人公阶段");
            if (ids.Count != GameConfig.MonitorsPerRound || ids.Distinct().Count() != ids.Count)
                throw new System.ArgumentException("必须监视 3 个不同角色");
            foreach (var id in ids)
                if (!s.CanBeMonitored(id))
                    throw new System.ArgumentException($"角色 {id} 不可被监视（幸福状态或已被监视）");

            s.CurrentMonitors = ids.ToList();

            if (s.Round >= GameConfig.MaxRounds)
            {
                // 第 6 回合主人公阶段结束 = 游戏终局，公开附身随从牌核对
                FinalReveal(s);
            }
            else
            {
                EnterPossessedPhase(s);
            }
        }

        // ---------- 附身随从阶段 ----------

        static void EnterPossessedPhase(GameState s)
        {
            s.Phase = Phase.PossessedPhase;
            if (!AnyConversionPossible(s))
            {
                // 主人公阵营胜利：附身随从不能再将任何人变成幸福状态
                s.Winner = Faction.Protagonist;
                s.WinReason = $"第 {s.Round} 回合附身随从阶段：所有附身随从均被监视，或行动范围内无可转换的存活者，" +
                              "《幸福》的连锁被中断。";
                s.Phase = Phase.GameOver;
            }
        }

        public static List<PreviewCardDef> UnusedCards(GameState s)
            => s.Cards.Where(c => !s.UsedCardIds.Contains(c.Id)).ToList();

        /// <summary>目标合法性：存活、不是附身随从、在 carrier 的礼物范围内。</summary>
        public static bool CanConvert(GameState s, int possessedId, int targetId, GiftColor color)
        {
            if (!s.IsAlive(targetId)) return false;
            if (s.IsPossessed(targetId)) return false;
            if (s.IsPossessed(possessedId) == false) return false;
            return InRange(s.Char(possessedId), s.Char(targetId), color);
        }

        /// <summary>某附身随从（未被监视时）用某色礼物可转换的目标列表。</summary>
        public static List<int> LegalTargets(GameState s, int possessedId, GiftColor color)
        {
            var list = new List<int>();
            if (s.IsMonitoredThisRound(possessedId)) return list; // 规则4：被监视的附身随从不能行动
            foreach (var c in s.Chars)
                if (CanConvert(s, possessedId, c.Id, color))
                    list.Add(c.Id);
            return list;
        }

        /// <summary>
        /// 最大匹配：给定礼物序列与"可行动成员"，最多能把几人变成幸福状态。
        /// isTarget(member, color, target) 由调用方提供（真实对局 / 推理假设两种用途）。
        /// </summary>
        public static int MaxMatching(IReadOnlyList<GiftColor> gifts, IReadOnlyList<int> members,
            System.Func<int, GiftColor, int, bool> isTarget)
        {
            return MaxMatchingDfs(gifts, members, 0, new bool[members.Count], new bool[GameConfig.CharCount], isTarget);
        }

        static int MaxMatchingDfs(IReadOnlyList<GiftColor> gifts, IReadOnlyList<int> members,
            int giftIdx, bool[] usedMember, bool[] usedTarget, System.Func<int, GiftColor, int, bool> isTarget)
        {
            if (giftIdx >= gifts.Count) return 0;
            int best = MaxMatchingDfs(gifts, members, giftIdx + 1, usedMember, usedTarget, isTarget); // 这份礼物不用
            for (int m = 0; m < members.Count; m++)
            {
                if (usedMember[m]) continue; // 规则3：一个附身随从一回合只能使 1 人幸福
                foreach (var t in TargetCandidates(members[m], gifts[giftIdx], isTarget))
                {
                    if (usedTarget[t]) continue; // 同一目标一回合只能被转换一次
                    usedMember[m] = true;
                    usedTarget[t] = true;
                    int v = 1 + MaxMatchingDfs(gifts, members, giftIdx + 1, usedMember, usedTarget, isTarget);
                    usedMember[m] = false;
                    usedTarget[t] = false;
                    if (v > best) best = v;
                }
            }
            return best;
        }

        // 目标候选的惰性枚举（真实对局用）
        static IEnumerable<int> TargetCandidates(int member, GiftColor color, System.Func<int, GiftColor, int, bool> isTarget)
        {
            for (int t = 0; t < GameConfig.CharCount; t++)
                if (isTarget(member, color, t))
                    yield return t;
        }

        /// <summary>真实对局下的最大可转换数（秘密信息，仅供附身随从方/引擎内部使用）。</summary>
        public static int MaxConversionsReal(GameState s, PreviewCardDef card)
        {
            var members = s.Possessed.Where(p => !s.IsMonitoredThisRound(p)).ToList();
            return MaxMatching(card.Gifts, members,
                (m, c, t) => CanConvert(s, m, t, c));
        }

        /// <summary>是否存在任意一张未用预告牌能让附身随从方行动（主人公胜利判定）。</summary>
        public static bool AnyConversionPossible(GameState s)
        {
            foreach (var card in UnusedCards(s))
                if (MaxConversionsReal(s, card) > 0)
                    return true;
            return false;
        }

        /// <summary>
        /// 枚举某张预告牌下所有"最大转换数"的行动方案。
        /// 规则：必须尽可能转换到预告人数；做不到的部分将作为未使用礼物交给主人公（泄密）。
        /// </summary>
        public static List<Plan> EnumeratePlans(GameState s, PreviewCardDef card)
        {
            var members = s.Possessed.Where(p => !s.IsMonitoredThisRound(p)).ToList();
            int max = MaxMatching(card.Gifts, members, (m, c, t) => CanConvert(s, m, t, c));

            var result = new List<Plan>();
            if (max == 0)
            {
                // 全部礼物未使用（无方案也能行动的情形：0 转换也是一种"方案"，但正常流程不会走到这）
                result.Add(new Plan { CardId = card.Id, UnusedGifts = card.Gifts.Count });
                return result;
            }

            EnumeratePlanDfs(s, card, card.Gifts, members, 0, new bool[members.Count],
                new bool[GameConfig.CharCount], new List<Assignment>(), max, result);
            return result;
        }

        static void EnumeratePlanDfs(GameState s, PreviewCardDef card, IReadOnlyList<GiftColor> gifts,
            List<int> members, int giftIdx, bool[] usedMember, bool[] usedTarget, List<Assignment> cur, int need,
            List<Plan> result)
        {
            int remainingGifts = gifts.Count - giftIdx;
            if (cur.Count + remainingGifts < need) return; // 剪枝：凑不满最大转换数

            if (giftIdx >= gifts.Count)
            {
                if (cur.Count == need)
                {
                    var plan = new Plan { CardId = card.Id, UnusedGifts = gifts.Count - cur.Count };
                    plan.Assignments.AddRange(cur);
                    result.Add(plan);
                }
                return;
            }

            var color = gifts[giftIdx];

            // 本份礼物分给某个未使用的成员 + 某个合法目标
            for (int m = 0; m < members.Count; m++)
            {
                if (usedMember[m]) continue;
                foreach (var t in TargetCandidatesReal(s, members[m], color))
                {
                    if (usedTarget[t]) continue;
                    usedMember[m] = true;
                    usedTarget[t] = true;
                    cur.Add(new Assignment(members[m], color, t));
                    EnumeratePlanDfs(s, card, gifts, members, giftIdx + 1, usedMember, usedTarget, cur, need, result);
                    cur.RemoveAt(cur.Count - 1);
                    usedTarget[t] = false;
                    usedMember[m] = false;
                }
            }

            // 本份礼物不用（只有当剩余能力无法满足 need 时才会出现在最终方案里，由 need 校验保证）
            EnumeratePlanDfs(s, card, gifts, members, giftIdx + 1, usedMember, usedTarget, cur, need, result);
        }

        static IEnumerable<int> TargetCandidatesReal(GameState s, int member, GiftColor color)
        {
            for (int t = 0; t < GameConfig.CharCount; t++)
                if (CanConvert(s, member, t, color))
                    yield return t;
        }

        /// <summary>执行附身随从行动方案，写入回合记录。plan 必须达到最大转换数。</summary>
        public static void ApplyPlan(GameState s, Plan plan)
        {
            if (s.Phase != Phase.PossessedPhase)
                throw new System.InvalidOperationException("当前不是附身随从阶段");
            var card = s.Cards[plan.CardId];
            if (s.UsedCardIds.Contains(plan.CardId))
                throw new System.InvalidOperationException("该预告牌已使用");

            int max = MaxConversionsReal(s, card);
            if (plan.Assignments.Count != max)
                throw new System.ArgumentException("方案未达到最大转换数，违反规则（必须尽可能转换到预告人数）");

            // 校验方案合法性
            var seenMembers = new HashSet<int>();
            int red = 0, blue = 0;
            foreach (var a in plan.Assignments)
            {
                if (a.Color == GiftColor.Red) red++; else blue++;
                if (!seenMembers.Add(a.PossessedId))
                    throw new System.ArgumentException("同一附身随从一回合只能转换 1 人");
                if (!CanConvert(s, a.PossessedId, a.TargetId, a.Color))
                    throw new System.ArgumentException("非法转换目标");
            }
            if (red > card.RedCount || blue > card.BlueCount)
                throw new System.ArgumentException("礼物数量超出预告牌");

            foreach (var a in plan.Assignments)
            {
                s.States[a.TargetId] = CharState.Happy;
                s.HappyRound[a.TargetId] = s.Round;
                s.HappyGift[a.TargetId] = a.Color;
            }

            s.History.Add(new RoundRecord
            {
                Round = s.Round,
                UsedCardId = plan.CardId,
                Monitored = s.CurrentMonitors.ToList(),
                Conversions = plan.Assignments.Select(a => new Conversion(a.TargetId, a.Color)).ToList(),
                UnusedGifts = card.Gifts.Count - plan.Assignments.Count,
            });
            s.UsedCardIds.Add(plan.CardId);
            s.Phase = Phase.PossessedPhase; // 等待 EndRound
        }

        /// <summary>结束阶段：翻面并进入下一回合（或终局）。</summary>
        public static void EndRound(GameState s)
        {
            if (s.Winner != null) { s.Phase = Phase.GameOver; return; }
            if (s.Phase != Phase.PossessedPhase && s.History.Count < s.Round - 1 && s.Round > 1)
                throw new System.InvalidOperationException("本回合附身随从尚未行动");

            if (s.Round >= GameConfig.MaxRounds)
            {
                FinalReveal(s);
                return;
            }
            s.Round++;
            BeginRound(s);
        }

        static void FinalReveal(GameState s)
        {
            // 第 6 回合主人公阶段后：至少 1 名附身随从未被监视 → 附身随从阵营胜利
            bool escaped = s.Possessed.Any(p => !s.CurrentMonitors.Contains(p));
            s.Winner = escaped ? Faction.Possessed : Faction.Protagonist;
            s.WinReason = escaped
                ? $"第 6 回合主人公阶段结束：仍有 {s.Possessed.Count(p => !s.CurrentMonitors.Contains(p))} 名附身随从逃离监视，《幸福》的连锁未被阻止。"
                : "第 6 回合主人公阶段结束：3 名附身随从全部被监视识破，主人公阵营获胜。";
            s.Phase = Phase.GameOver;
        }

        // ---------- 克隆（AI 推演用） ----------

        public static GameState Clone(GameState s)
        {
            var c = new GameState
            {
                Chars = s.Chars,
                Cards = s.Cards,
                IfRoute = s.IfRoute,
                Round = s.Round,
                Phase = s.Phase,
                ProtagonistSkipped = s.ProtagonistSkipped,
                Winner = s.Winner,
                WinReason = s.WinReason,
            };
            c.States = (CharState[])s.States.Clone();
            c.HappyRound = (int[])s.HappyRound.Clone();
            c.HappyGift = (GiftColor[])s.HappyGift.Clone();
            c.Possessed = new HashSet<int>(s.Possessed);
            c.UsedCardIds = new HashSet<int>(s.UsedCardIds);
            c.CurrentMonitors = s.CurrentMonitors.ToList();
            foreach (var r in s.History)
            {
                c.History.Add(new RoundRecord
                {
                    Round = r.Round,
                    UsedCardId = r.UsedCardId,
                    Monitored = r.Monitored.ToList(),
                    Conversions = r.Conversions.Select(x => new Conversion(x.TargetId, x.Color)).ToList(),
                    UnusedGifts = r.UnusedGifts,
                });
            }
            return c;
        }
    }
}
