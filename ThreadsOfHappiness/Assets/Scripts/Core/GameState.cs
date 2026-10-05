using System.Collections.Generic;

namespace Toh.Core
{
    /// <summary>一次《幸福》转换：目标角色 + 使用的礼物颜色。</summary>
    public class Conversion
    {
        public int TargetId;
        public GiftColor Color;

        public Conversion(int targetId, GiftColor color)
        {
            TargetId = targetId;
            Color = color;
        }
    }

    /// <summary>单个回合的公开记录（主人公方可见的全部信息）。</summary>
    public class RoundRecord
    {
        public int Round;
        public int UsedCardId = -1;                    // 本回合使用的预告牌（-1 = 未使用，即主人公提前获胜）
        public List<int> Monitored = new List<int>();  // 本回合被监视的角色（第 1 回合为空）
        public List<Conversion> Conversions = new List<Conversion>(); // 本回合的《幸福》转换
        public int UnusedGifts;                        // 交给主人公的未使用礼物数（泄密信号）
    }

    public enum Phase
    {
        Setup,              // 设置（选附身随从）
        ProtagonistPhase,   // 主人公阶段：放监视指示物
        PossessedPhase,     // 附身随从阶段：选预告牌并行动
        GameOver
    }

    /// <summary>整局游戏状态。Possessed 集合为秘密信息，主人公方逻辑严禁直接读取。</summary>
    public class GameState
    {
        public List<CharacterDef> Chars;
        public CharState[] States = new CharState[GameConfig.CharCount];
        public int[] HappyRound = new int[GameConfig.CharCount];       // 变成幸福状态的回合，-1 = 存活
        public GiftColor[] HappyGift = new GiftColor[GameConfig.CharCount]; // 转换时使用的礼物颜色

        public HashSet<int> Possessed = new HashSet<int>();  // 秘密！
        public List<PreviewCardDef> Cards;                   // 5 张预告牌
        public HashSet<int> UsedCardIds = new HashSet<int>();

        public int Round = 1;                // 当前回合 1..6
        public Phase Phase = Phase.Setup;
        public List<int> CurrentMonitors = new List<int>();  // 本回合正面朝上的监视指示物
        public bool ProtagonistSkipped;      // 第 1 回合主人公阶段被跳过

        public List<RoundRecord> History = new List<RoundRecord>();

        public Faction? Winner;              // null = 未结束
        public string WinReason = "";
        public bool IfRoute;

        public bool IsAlive(int id) => States[id] == CharState.Alive;
        public bool IsHappy(int id) => States[id] == CharState.Happy;
        public bool IsPossessed(int id) => Possessed.Contains(id);
        public bool IsMonitoredThisRound(int id) => CurrentMonitors.Contains(id);
        public bool CanBeMonitored(int id) => IsAlive(id) && !IsMonitoredThisRound(id);

        public CharacterDef Char(int id) => Chars[id];
    }
}
