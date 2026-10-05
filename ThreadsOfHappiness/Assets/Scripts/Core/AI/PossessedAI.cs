using System.Collections.Generic;
using System.Linq;

namespace Toh.Core
{
    /// <summary>
    /// 附身随从方 AI：
    /// 1. 开局选 3 名随从：启发式 + 蒙特卡洛（与简单主人公 AI 快速对局）评估生存率。
    /// 2. 每回合选预告牌与行动方案：避免泄密（未使用礼物）、维持主人公候选集合的模糊度、
    ///    保证后续回合仍有行动能力（否则下回合主人公直接获胜）。
    /// </summary>
    public class PossessedAI
    {
        public Difficulty Difficulty;
        private readonly System.Random _rng;

        public PossessedAI(Difficulty diff, System.Random rng)
        {
            Difficulty = diff;
            _rng = rng ?? new System.Random();
        }

        // ---------- 开局选人 ----------

        public int[] ChoosePossessed(GameState setupTemplate)
        {
            _ref = setupTemplate; // 供启发式读取坐标
            var pool = new List<int>();
            for (int i = 0; i < GameConfig.CharCount; i++)
            {
                if (!setupTemplate.IfRoute && i == GameConfig.MomohanaId) continue;
                pool.Add(i);
            }

            // 采样候选三元组
            var sampled = new List<int[]>();
            int samples = Difficulty == Difficulty.Easy ? 24 : (Difficulty == Difficulty.Normal ? 40 : 64);
            for (int i = 0; i < samples; i++)
            {
                var pick = pool.OrderBy(_ => _rng.Next()).Take(3).ToArray();
                if (!sampled.Any(x => x.SequenceEqual(pick))) sampled.Add(pick);
            }

            // 困难：每个候选跑少量快速模拟（对手为随机监视的主人公 AI）
            var scored = new List<(int[] Triple, double Score)>();
            var rngFast = new System.Random(_rng.Next());
            foreach (var triple in sampled)
            {
                double score = HeuristicTriple(triple);
                if (Difficulty == Difficulty.Hard)
                    score += 20.0 * MonteCarloWinRate(setupTemplate, triple, rngFast, 8);
                else if (Difficulty == Difficulty.Normal)
                    score += 20.0 * MonteCarloWinRate(setupTemplate, triple, rngFast, 4);
                scored.Add((triple, score + _rng.NextDouble() * 0.5));
            }

            scored = scored.OrderByDescending(x => x.Score).ToList();
            return Difficulty == Difficulty.Hard ? scored[0].Triple
                 : Difficulty == Difficulty.Normal ? scored[_rng.Next(System.Math.Min(3, scored.Count))].Triple
                 : scored[_rng.Next(System.Math.Min(8, scored.Count))].Triple;
        }

        /// <summary>位置启发：成员分散（难以被同时监视）、红色礼物目标充足。</summary>
        double HeuristicTriple(int[] triple)
        {
            double score = 0;
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                {
                    var a = _ref.Chars[triple[i]]; var b = _ref.Chars[triple[j]];
                    int d = System.Math.Max(System.Math.Abs(a.X - b.X), System.Math.Abs(a.Y - b.Y));
                    score += d * 2.5;                       // 分散加分
                    if (a.X == b.X || a.Y == b.Y) score -= 1.0; // 同线略减（易被同一范围波及）
                }
            foreach (var id in triple)
            {
                var c = _ref.Chars[id];
                int neighbors = 0;
                for (int x = c.X - 1; x <= c.X + 1; x++)
                    for (int y = c.Y - 1; y <= c.Y + 1; y++)
                        if (x >= 0 && x < GameConfig.GridW && y >= 0 && y < GameConfig.GridH && (x != c.X || y != c.Y))
                            neighbors++;
                score += neighbors * 0.3;                   // 红色礼物可用目标
            }
            return score;
        }

        GameState _ref; // 仅用于读取角色坐标

        /// <summary>快速模拟：随机监视的主人公 vs 随机方案的随从，返回随从方胜率。</summary>
        double MonteCarloWinRate(GameState setupTemplate, int[] triple, System.Random rng, int games)
        {
            int wins = 0;
            var protAI = new ProtagonistAI(Difficulty.Easy, rng);
            var possAI = new PossessedAI(Difficulty.Easy, rng);
            for (int g = 0; g < games; g++)
            {
                var s = RulesEngine.NewGame(setupTemplate.Chars, setupTemplate.Cards, triple, setupTemplate.IfRoute);
                var winner = SelfPlay.Run(s, protAI, possAI, useBelief: false, maxPlEval: 8);
                if (winner == Faction.Possessed) wins++;
            }
            return (double)wins / games;
        }

        // ---------- 回合内行动 ----------

        /// <summary>返回 (cardId, plan)。调用前需确认 AnyConversionPossible 为真。</summary>
        public (int CardId, Plan Plan) ChooseAction(GameState s, BeliefSet belief)
        {
            var cards = RulesEngine.UnusedCards(s);

            // 枚举所有 (card, plan)
            var options = new List<(PreviewCardDef Card, Plan Plan)>();
            foreach (var card in cards)
                foreach (var plan in RulesEngine.EnumeratePlans(s, card))
                    options.Add((card, plan));

            if (options.Count == 0)
                throw new System.InvalidOperationException("没有可行动方案（应已被主人公胜利判定拦截）");

            switch (Difficulty)
            {
                case Difficulty.Easy: return PickEasy(options);
                case Difficulty.Normal: return PickScored(s, belief, options, topK: 3, evalPlans: 20, capCandidates: 150);
                default: return PickScored(s, belief, options, topK: 1, evalPlans: 60, capCandidates: 250);
            }
        }

        (int, Plan) PickEasy(List<(PreviewCardDef, Plan)> options)
        {
            // 简单：随机选牌；在同类方案里轻微偏好不泄密
            var card = options[_rng.Next(options.Count)].Item1;
            var plans = options.Where(o => o.Item1.Id == card.Id).Select(o => o.Item2).ToList();
            var noLeak = plans.Where(p => p.UnusedGifts == 0).ToList();
            var plan = (noLeak.Count > 0 && _rng.NextDouble() < 0.6) ? noLeak[_rng.Next(noLeak.Count)] : plans[_rng.Next(plans.Count)];
            return (card.Id, plan);
        }

        (int, Plan) PickScored(GameState s, BeliefSet belief, List<(PreviewCardDef Card, Plan Plan)> options, int topK, int evalPlans, int capCandidates)
        {
            // 随机下采样，控制推理耗时
            var eval = options.ToList();
            if (eval.Count > evalPlans)
                eval = eval.OrderBy(_ => _rng.Next()).Take(evalPlans).ToList();

            var scored = new List<(Plan Plan, double Score)>();
            foreach (var (card, plan) in eval)
                scored.Add((plan, ScorePlan(s, belief, card, plan, capCandidates)));

            scored = scored.OrderByDescending(x => x.Score).ToList();
            var pick = topK == 1
                ? scored[_rng.Next(System.Math.Min(2, scored.Count))] // 并列最优随机
                : scored[_rng.Next(System.Math.Min(topK, scored.Count))];
            return (pick.Plan.CardId, pick.Plan);
        }

        double ScorePlan(GameState s, BeliefSet belief, PreviewCardDef card, Plan plan, int capCandidates)
        {
            double score = 0;

            // 1) 泄密惩罚：未使用礼物是给主人公的强情报
            score -= plan.UnusedGifts * 50;

            // 2) 推演：应用方案后主人公的候选集合大小（越大越模糊越好）
            var hypo = RulesEngine.Clone(s);
            RulesEngine.ApplyPlan(hypo, plan);

            if (belief != null && belief.Candidates.Count > 0)
            {
                var capped = belief.CloneCapped(capCandidates, _rng);
                var hypoPublic = MakePublicView(hypo);
                capped.FilterWithHistory(hypoPublic);
                double ambiguity = capped.Candidates.Count;
                score += ambiguity * (600.0 / capCandidates);
            }

            // 3) 后续生存力：若行动后所有剩余预告牌都无法转换 → 下回合主人公直接获胜
            double minFuture = double.MaxValue;
            foreach (var c in RulesEngine.UnusedCards(hypo))
            {
                if (c.Id == card.Id) continue;
                minFuture = System.Math.Min(minFuture, RulesEngine.MaxConversionsReal(hypo, c));
            }
            if (minFuture == 0) score -= 2000;
            else score += minFuture * 10;

            // 4) 不把幸福扩散到自家随从旁太近（减少主人公缩小范围的机会）——轻量启发
            foreach (var a in plan.Assignments)
            {
                var t = s.Char(a.TargetId);
                foreach (var p in s.Possessed)
                {
                    var pc = s.Char(p);
                    if (System.Math.Abs(t.X - pc.X) <= 1 && System.Math.Abs(t.Y - pc.Y) <= 1)
                        score -= 2; // 在随从身边转换会让主人公怀疑邻居
                }
            }

            return score + _rng.NextDouble() * 0.1;
        }

        /// <summary>构造公开视图：抹去真实随从信息，避免推理引擎"作弊"。</summary>
        public static GameState MakePublicView(GameState s)
        {
            var v = RulesEngine.Clone(s);
            v.Possessed.Clear(); // 公开视角不知道谁是随从
            return v;
        }
    }
}
