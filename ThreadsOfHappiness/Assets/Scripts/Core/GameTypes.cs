using System.Collections.Generic;
using System.Linq;

namespace Toh.Core
{
    /// <summary>礼物颜色：红 = 周围8格；蓝 = 同行同列直线。</summary>
    public enum GiftColor { Red = 0, Blue = 1 }

    /// <summary>角色状态：存活 / 幸福。</summary>
    public enum CharState { Alive = 0, Happy = 1 }

    /// <summary>AI 难度。</summary>
    public enum Difficulty { Easy = 0, Normal = 1, Hard = 2 }

    /// <summary>阵营。</summary>
    public enum Faction { Protagonist = 0, Possessed = 1 }

    /// <summary>一名角色的静态定义（可整体替换为实体卡数据）。</summary>
    public class CharacterDef
    {
        public int Id;          // 0..24
        public string Name;     // 占位名单，可替换
        public int Room;        // 房间号 201..605
        public int X;           // 列 0..4
        public int Y;           // 层 0..4（0 为最下层）

        public override string ToString() => $"{Room} {Name}";
    }

    /// <summary>预告牌：一次附身随从阶段可用的礼物组合。</summary>
    public class PreviewCardDef
    {
        public int Id;
        public List<GiftColor> Gifts = new List<GiftColor>();
        public string Label;

        public int RedCount => Gifts.Count(g => g == GiftColor.Red);
        public int BlueCount => Gifts.Count(g => g == GiftColor.Blue);
        public override string ToString() => Label;
    }

    /// <summary>全局配置与默认数据表。</summary>
    public static class GameConfig
    {
        public const int GridW = 5;
        public const int GridH = 5;
        public const int CharCount = 25;
        public const int MaxRounds = 6;          // 游戏最多进行到第 6 回合主人公阶段结束
        public const int MonitorsPerRound = 3;   // 每回合放 3 个监视指示物
        public const int PossessedCount = 3;     // 附身随从 3 名

        /// <summary>五张预告牌：一蓝一红 / 一蓝一红 / 两蓝 / 两红 / 两蓝一红。</summary>
        public static List<PreviewCardDef> DefaultPreviewCards()
        {
            var list = new List<PreviewCardDef>
            {
                new PreviewCardDef { Id = 0, Gifts = { GiftColor.Blue, GiftColor.Red }, Label = "一蓝一红" },
                new PreviewCardDef { Id = 1, Gifts = { GiftColor.Blue, GiftColor.Red }, Label = "一蓝一红" },
                new PreviewCardDef { Id = 2, Gifts = { GiftColor.Blue, GiftColor.Blue }, Label = "两蓝" },
                new PreviewCardDef { Id = 3, Gifts = { GiftColor.Red, GiftColor.Red }, Label = "两红" },
                new PreviewCardDef { Id = 4, Gifts = { GiftColor.Blue, GiftColor.Blue, GiftColor.Red }, Label = "两蓝一红" },
            };
            return list;
        }

        /// <summary>
        /// 占位角色名单：房间号按旅馆布局，从下层开始 201、202 依次排列（2~6 层，每层 5 间）。
        /// 百花开固定在正中央 403 房。除百花开外的名字均为占位，可替换为实体卡数据。
        /// </summary>
        public static List<CharacterDef> DefaultRoster()
        {
            string[] names =
            {
                "牡丹", "柚子", "莉莉", "沙织", "杏",       // 2 层 201-205
                "阳矢", "由利", "美绪", "铃", "小百合",     // 3 层 301-305
                "桃子", "千岁", "百花开", "绫乃", "茉莉",   // 4 层 401-405（403 正中央）
                "萤", "玛利", "舞", "澪", "椿",             // 5 层 501-505
                "菖蒲", "栞", "樱", "澄", "玲",             // 6 层 601-605
            };
            var list = new List<CharacterDef>();
            int id = 0;
            for (int y = 0; y < GridH; y++)          // y=0 最下层（2 层）
            {
                for (int x = 0; x < GridW; x++)
                {
                    int room = (y + 2) * 100 + (x + 1);
                    list.Add(new CharacterDef { Id = id, Name = names[id], Room = room, X = x, Y = y });
                    id++;
                }
            }
            return list;
        }

        public const int MomohanaId = 12; // 百花开：403 房，正中央（y=2 行, x=2 列 → id = 2*5+2 = 12）
    }
}
