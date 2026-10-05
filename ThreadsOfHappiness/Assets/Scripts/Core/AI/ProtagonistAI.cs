using System.Collections.Generic;
using System.Linq;

namespace Toh.Core
{
    /// <summary>
    /// 主人公方 AI：基于"附身随从三元组候选集合"的约束推理选择监视对象。
    /// 评分思路：若监视的 3 人恰好等于某个候选三元组，则该三元组下所有随从被监视 →
    /// 附身随从无法行动 → 主人公直接获胜；部分监视则会限制行动、制造泄密机会。
    /// </summary>
    public class ProtagonistAI
    {
        public Difficulty Difficulty;
        private readonly System.Random _rng;

        public ProtagonistAI(Difficulty diff, System.Random rng)
        {
            Difficulty = diff;
            _rng = rng ?? new System.Random();
        }

        /// <summary>belief 可为 null（此时退化为位置启发式）。</summary>
        public int[] ChooseMonitors(GameState s, BeliefSet belief)
        {
            var legal = RulesEngine.EnumerateMonitorSets(s);
            if (legal.Count == 0) throw new System.InvalidOperationException("没有可监视的角色");
            if (legal.Count == 1) return legal[0];

            switch (Difficulty)
            {
                case Difficulty.Easy: return ChooseEasy(s, legal, belief);
                case Difficulty.Normal: return ChooseNormal(s, legal, belief);
                default: return ChooseHard(s, legal, belief);
            }
        }

        int[] ChooseEasy(GameState s, List<int[]> legal, BeliefSet belief)
        {
            // 简单：50% 完全随机，50% 选边际概率最高的 3 人（带噪声）
            if (belief != null && _rng.NextDouble() < 0.5)
            {
                var prob = belief.CharProbabilities();
                var pool = RulesEngine.MonitorableChars(s);
                var pick = pool.OrderByDescending(id => prob[id] + _rng.NextDouble() * 0.05)
                               .Take(3).ToArray();
                return pick;
            }
            return legal[_rng.Next(legal.Count)];
        }

        int[] ChooseNormal(GameState s, List<int[]> legal, BeliefSet belief)
        {
            var scored = ScoreAll(s, legal, belief);
            var top = scored.OrderByDescending(x => x.Score).Take(5).ToList();
            // softmax 采样
            double max = top[0].Score;
            var weights = top.Select(x => System.Math.Exp((x.Score - max) * 0.5)).ToList();
            double total = weights.Sum();
            double r = _rng.NextDouble() * total;
            for (int i = 0; i < top.Count; i++)
            {
                r -= weights[i];
                if (r <= 0) return top[i].Set;
            }
            return top[0].Set;
        }

        int[] ChooseHard(GameState s, List<int[]> legal, BeliefSet belief)
        {
            var scored = ScoreAll(s, legal, belief);
            double best = scored.Max(x => x.Score);
            var bests = scored.Where(x => x.Score >= best - 1e-9).ToList();
            return bests[_rng.Next(bests.Count)].Set;
        }

        List<(int[] Set, double Score)> ScoreAll(GameState s, List<int[]> legal, BeliefSet belief)
        {
            var alive = new HashSet<int>(s.Chars.Where(c => s.IsAlive(c.Id)).Select(c => c.Id));
            // 评估用候选集合上限（控制耗时）
            List<int[]> candidates;
            if (belief != null)
                candidates = belief.CloneCapped(600, _rng).Candidates;
            else
                candidates = DefaultCandidates(s, alive);
            double totalWeight = candidates.Count;

            // 倒排索引：角色 → 包含它的候选下标（避免逐个 M 遍历全部候选）
            var byChar = new List<int>[GameConfig.CharCount];
            for (int i = 0; i < byChar.Length; i++) byChar[i] = new List<int>();
            for (int ci = 0; ci < candidates.Count; ci++)
                foreach (var id in candidates[ci])
                    byChar[id].Add(ci);

            // 预计算：每个候选三元组 P，在"其子集 S 被监视"时是否全灭（任何未用预告牌都无法转换）
            // key = (candidate index << 3) | mask of P members monitored
            var zeroMask = new Dictionary<long, bool>();

            var unusedCards = RulesEngine.UnusedCards(s);
            for (int ci = 0; ci < candidates.Count; ci++)
            {
                var p = candidates[ci];
                var pSet = new HashSet<int>(p);
                for (int mask = 0; mask < 8; mask++)
                {
                    var active = new List<int>();
                    for (int k = 0; k < 3; k++)
                        if ((mask & (1 << k)) == 0) active.Add(p[k]);

                    bool zero = true;
                    foreach (var card in unusedCards)
                    {
                        int m = RulesEngine.MaxMatching(card.Gifts, active, (mm, c, t) =>
                            alive.Contains(t) && !pSet.Contains(t) &&
                            RulesEngine.InRange(s.Char(mm), s.Char(t), c));
                        if (m > 0) { zero = false; break; }
                    }
                    zeroMask[(ci << 3) | mask] = zero;
                }
            }

            // mask=0（无人被监视）通常不可能全灭（否则游戏已结束），先验证一次统一处理
            var result = new List<(int[], double)>();
            double[] probCache = belief?.CharProbabilities();
            foreach (var mSet in legal)
            {
                double score = 0;

                // 与本监视组合相关的候选：三个成员的倒排并集
                var related = new HashSet<int>(byChar[mSet[0]]);
                related.UnionWith(byChar[mSet[1]]);
                related.UnionWith(byChar[mSet[2]]);

                foreach (var ci in related)
                {
                    var p = candidates[ci];
                    int mask = 0;
                    if (mSet.Contains(p[0])) mask |= 1;
                    if (mSet.Contains(p[1])) mask |= 2;
                    if (mSet.Contains(p[2])) mask |= 4;

                    if (zeroMask[(ci << 3) | mask])
                        score += 1000.0 / totalWeight;        // 该候选被全灭（全监视=直接获胜情形）
                    else
                        score += System.Math.Pow(2, BitCount(mask)) * 1.0 / totalWeight; // 部分监视限制行动
                }

                // 轻微倾向监视概率高的角色（信息价值）
                if (probCache != null)
                {
                    double psum = 0;
                    foreach (var id in mSet) psum += probCache[id];
                    score += psum * 0.01;
                }
                result.Add((mSet, score));
            }
            return result;
        }

        static int BitCount(int v) { int c = 0; while (v != 0) { c += v & 1; v >>= 1; } return c; }

        /// <summary>无信念时的兜底候选：全组合（用于快速模拟）。</summary>
        static List<int[]> DefaultCandidates(GameState s, HashSet<int> alive)
        {
            var pool = new List<int>();
            for (int i = 0; i < GameConfig.CharCount; i++)
                if (!s.IfRoute || i != GameConfig.MomohanaId)
                    if (!s.IsHappy(i) || s.HappyRound[i] == 0) // 幸福者（含开局百花开）不可能是随从
                        pool.Add(i);
            var list = new List<int[]>();
            for (int a = 0; a < pool.Count; a++)
                for (int b = a + 1; b < pool.Count; b++)
                    for (int c = b + 1; c < pool.Count; c++)
                        list.Add(new[] { pool[a], pool[b], pool[c] });
            return list;
        }
    }
}
