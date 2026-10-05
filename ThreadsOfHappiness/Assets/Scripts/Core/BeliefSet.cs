using System.Collections.Generic;
using System.Linq;

namespace Toh.Core
{
    /// <summary>
    /// 主人公方的信念状态：维护所有与公开信息一致的"附身随从三元组"候选集合。
    /// 候选总数最多 C(25,3)=2300，逐回合过滤开销可忽略。
    /// </summary>
    public class BeliefSet
    {
        public List<int[]> Candidates = new List<int[]>();

        public int Count => Candidates.Count;

        /// <summary>开局：全部可能的三元组（受 IF 路线与百花开限制）。</summary>
        public static BeliefSet Initial(GameState s)
        {
            var b = new BeliefSet();
            var pool = new List<int>();
            for (int i = 0; i < GameConfig.CharCount; i++)
            {
                if (!s.IfRoute && i == GameConfig.MomohanaId) continue; // 百花开开局幸福，必然不是随从
                pool.Add(i);
            }
            Combine(pool, 0, 0, new int[GameConfig.PossessedCount], b.Candidates);
            return b;
        }

        static void Combine(List<int> pool, int start, int depth, int[] cur, List<int[]> result)
        {
            if (depth == cur.Length) { result.Add((int[])cur.Clone()); return; }
            for (int i = start; i <= pool.Count - (cur.Length - depth); i++)
            {
                cur[depth] = pool[i];
                Combine(pool, i + 1, depth + 1, cur, result);
            }
        }

        /// <summary>根据当前全部公开记录过滤候选集合。可重复调用（幂等基于初始候选）。</summary>
        public void FilterWithHistory(GameState s)
        {
            Candidates = Candidates.Where(p => Consistent(s, p)).ToList();
        }

        /// <summary>三元组 p 是否与所有回合公开记录一致。</summary>
        public static bool Consistent(GameState s, int[] p)
        {
            var pSet = new HashSet<int>(p);

            // 被转换过的角色必然不是附身随从（规则2）
            for (int i = 0; i < GameConfig.CharCount; i++)
                if (s.IsHappy(i) && s.HappyRound[i] >= 1 && pSet.Contains(i))
                    return false;

            foreach (var rec in s.History)
            {
                if (rec.UsedCardId < 0) continue;
                var card = s.Cards[rec.UsedCardId];
                var convCount = rec.Conversions.Count;

                // 本回合之前已幸福的集合（公开信息）
                var happyBefore = new HashSet<int>();
                for (int i = 0; i < GameConfig.CharCount; i++)
                    if (s.IsHappy(i) && s.HappyRound[i] >= 1 && s.HappyRound[i] < rec.Round)
                        happyBefore.Add(i);

                // 可行动成员 = 候选 - 本回合被监视者
                var active = p.Where(id => !rec.Monitored.Contains(id)).ToList();

                // 条件A：观察到的转换数 == 该候选下的最大可转换数（强制满额规则）
                int max = RulesEngine.MaxMatching(card.Gifts, active, (m, c, t) =>
                    !happyBefore.Contains(t) && !pSet.Contains(t) &&
                    RulesEngine.InRange(s.Char(m), s.Char(t), c));
                if (max != convCount) return false;

                // 条件B：观察到的转换必须能被该候选"解释"（存在合法指派覆盖全部转换）
                if (!Explains(s, card, active, pSet, happyBefore, rec.Conversions))
                    return false;
            }
            return true;
        }

        /// <summary>能否把观察到的转换逐一分派给候选成员（每人至多 1 个、颜色/范围匹配）。</summary>
        static bool Explains(GameState s, PreviewCardDef card, List<int> active, HashSet<int> pSet,
            HashSet<int> happyBefore, List<Conversion> conversions)
        {
            if (conversions.Count == 0) return true;
            var gifts = card.Gifts;
            // 尝试把每个转换映射到 (成员, 一份对应颜色的礼物)
            return ExplainDfs(s, active, conversions, gifts, new bool[active.Count],
                new bool[gifts.Count], 0, pSet, happyBefore);
        }

        static bool ExplainDfs(GameState s, List<int> active, List<Conversion> conv, List<GiftColor> gifts,
            bool[] usedMember, bool[] usedGift, int convIdx, HashSet<int> pSet, HashSet<int> happyBefore)
        {
            if (convIdx >= conv.Count) return true;
            var color = conv[convIdx].Color;
            for (int m = 0; m < active.Count; m++)
            {
                if (usedMember[m]) continue;
                if (!RulesEngine.InRange(s.Char(active[m]), s.Char(conv[convIdx].TargetId), color))
                    continue;
                for (int gi = 0; gi < gifts.Count; gi++)
                {
                    if (usedGift[gi] || gifts[gi] != color) continue;
                    usedMember[m] = true;
                    usedGift[gi] = true;
                    if (ExplainDfs(s, active, conv, gifts, usedMember, usedGift, convIdx + 1, pSet, happyBefore))
                        return true;
                    usedMember[m] = false;
                    usedGift[gi] = false;
                }
            }
            return false;
        }

        /// <summary>每个角色是附身随从的边际概率（基于当前候选集合）。</summary>
        public double[] CharProbabilities()
        {
            var prob = new double[GameConfig.CharCount];
            if (Candidates.Count == 0) return prob;
            foreach (var p in Candidates)
                foreach (var id in p)
                    prob[id] += 1.0;
            for (int i = 0; i < prob.Length; i++) prob[i] /= Candidates.Count;
            return prob;
        }

        /// <summary>裁剪到最多 n 个候选（AI 评估用，均匀抽样）。</summary>
        public BeliefSet CloneCapped(int n, System.Random rng)
        {
            var b = new BeliefSet();
            if (Candidates.Count <= n) { b.Candidates = new List<int[]>(Candidates); return b; }
            var idxs = Enumerable.Range(0, Candidates.Count).OrderBy(_ => rng.Next()).Take(n);
            b.Candidates = idxs.Select(i => Candidates[i]).ToList();
            return b;
        }

        public BeliefSet Clone()
        {
            var b = new BeliefSet { Candidates = new List<int[]>(Candidates) };
            return b;
        }
    }
}
