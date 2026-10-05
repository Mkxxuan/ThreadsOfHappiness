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
        /// 官方角色名单（来源：asobouyabg.just-play-games.com 人物介绍页）。
        /// 房间号按旅馆布局 201~605，2~6 层每层 5 间；百花开（四月朔日 百花开）固定在正中央 403 房。
        /// Name 用短名便于牌面显示，全名见注释。
        /// </summary>
        public static List<CharacterDef> DefaultRoster()
        {
            string[] names =
            {
                "萤",   "柑奈", "桃子", "丽奈", "从枫",   // 2 层 201-205：马醉木 萤 / 丹羽 柑奈 / 狼谷 桃子 / 马场 丽奈 / 小林 从枫
                "四叶", "三叶", "由利", "艾丽卡", "生霞", // 3 层 301-305：幸草 四叶 / 幸草 三叶 / 樱井 由利 / 越后 艾丽卡 / 江波户 生霞
                "紫阳", "茜叶", "百花开", "丽堇", "阳矢",   // 4 层 401-405：泷 紫阳 / 蒲岛 茜叶 / 四月朔日 百花开 / 岛崎 丽堇 / 日向 阳矢
                "往昔", "茉莉", "牡丹", "菖蒲", "柚子",     // 5 层 501-505：羽梨 往昔 / 福原 茉莉 / 生明 牡丹 / 宫山 菖蒲 / 井之头 柚子
                "伊始", "莉莉", "山茶", "玛利", "雀林",     // 6 层 601-605：铃木 伊始 / 孤挺 莉莉 / 长谷川 山茶 / 大江 玛利 / 矢内原 雀林
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
