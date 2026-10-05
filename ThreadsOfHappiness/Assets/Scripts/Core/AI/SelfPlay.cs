using System.Collections.Generic;

namespace Toh.Core
{
    /// <summary>AI vs AI 自动对局引擎：用于平衡性验证、回归测试与随从开局选人的蒙特卡洛评估。</summary>
    public static class SelfPlay
    {
        /// <summary>
        /// 运行一整局 AI 对战，返回获胜阵营（对局一定会分出胜负）。
        /// useBelief=false 时主人公 AI 不做候选集推理（快速模式）。
        /// </summary>
        public static Faction Run(GameState s, ProtagonistAI protAI, PossessedAI possAI,
            bool useBelief = true, int maxPlEval = 60)
        {
            BeliefSet belief = null;
            if (useBelief)
            {
                belief = BeliefSet.Initial(s);
                var public0 = PossessedAI.MakePublicView(s);
                belief.FilterWithHistory(public0);
            }

            int guard = 0;
            while (s.Winner == null && guard++ < 100)
            {
                switch (s.Phase)
                {
                    case Phase.ProtagonistPhase:
                    {
                        var monitors = protAI.ChooseMonitors(s, belief);
                        RulesEngine.ApplyMonitors(s, monitors);
                        break;
                    }
                    case Phase.PossessedPhase:
                    {
                        // 随从 AI 直接复用"主人公视角"的信念集合评估自己的隐蔽度
                        var (cardId, plan) = possAI.ChooseAction(s, belief);
                        RulesEngine.ApplyPlan(s, plan);
                        RulesEngine.EndRound(s);

                        if (useBelief)
                        {
                            var publicAfter = PossessedAI.MakePublicView(s);
                            belief.FilterWithHistory(publicAfter);
                        }
                        break;
                    }
                    default:
                        return s.Winner ?? Faction.Protagonist;
                }
            }
            return s.Winner ?? Faction.Protagonist;
        }

        /// <summary>批量模拟：返回 (主人公胜场, 随从胜场)。</summary>
        public static (int protWins, int possWins) Simulate(List<CharacterDef> roster, List<PreviewCardDef> cards,
            bool ifRoute, int games, Difficulty protDiff, Difficulty possDiff, int seed, bool useBelief = true,
            int possSetupEvalGames = 0)
        {
            var rng = new System.Random(seed);
            var protAI = new ProtagonistAI(protDiff, rng);
            var possAI = new PossessedAI(possDiff, rng);
            int prot = 0, poss = 0;
            for (int g = 0; g < games; g++)
            {
                var template = RulesEngine.NewGame(roster, cards, new[] { 0, 1, 2 }, ifRoute); // 占位模板
                var triple = possAI.ChoosePossessed(template);
                var s = RulesEngine.NewGame(roster, cards, triple, ifRoute);
                var w = Run(s, protAI, possAI, useBelief);
                if (w == Faction.Protagonist) prot++; else poss++;
            }
            return (prot, poss);
        }
    }
}
