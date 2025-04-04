using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;
using Terraria.ModLoader.IO;
using Microsoft.Xna.Framework;
using AncientChineseMythology.NPCs.Boss;

namespace AncientChineseMythology
{
    // Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
    public class AncientChineseMythology : Mod
    {
        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            AncientChineseMythologyMessageType msgType = (AncientChineseMythologyMessageType)reader.ReadByte();
            switch (msgType)
            {
                case AncientChineseMythologyMessageType.SyncGrowthPlayer:
                    {
                        int playerID = reader.ReadInt32();
                        float bonus = reader.ReadSingle();
                        int count = reader.ReadInt32();
                        var enemyList = new System.Collections.Generic.List<int>();
                        for (int i = 0; i < count; i++)
                        {
                            enemyList.Add(reader.ReadInt32());
                        }
                        // 获取对应的 GrowthPlayer 并更新数据
                        if (playerID >= 0 && playerID < Main.maxPlayers)
                        {
                            var modPlayer = Main.player[playerID].GetModPlayer<Players.GrowthPlayer>();
                            modPlayer.growthBonus = bonus;
                            modPlayer.growthEnemies = enemyList;
                        }
                    }
                    break;
            }
        }
        public struct Vertex : IVertexType
        {
            private static VertexDeclaration _vertexDeclaration = new VertexDeclaration(new VertexElement[3]
            {
            new VertexElement(0,VertexElementFormat.Vector2,VertexElementUsage.Position,0),
            new VertexElement(8,VertexElementFormat.Color,VertexElementUsage.Color,0),
            new VertexElement(12,VertexElementFormat.Vector3,VertexElementUsage.TextureCoordinate,0)
            });
            public Vector2 Position;
            public Color Color;
            public Vector3 TexCoord;
            public Vertex(Vector2 position, Vector3 texCoord, Color color)
            {
                Position = position;
                TexCoord = texCoord;
                Color = color;
            }
            public VertexDeclaration VertexDeclaration
            {
                get => _vertexDeclaration;
            }
        }
        public enum AncientChineseMythologyMessageType : byte
        {
            SyncGrowthPlayer
        }

        public class BossChecklistIntegration : ModSystem
        {
            private static readonly Version BossChecklistAPIVersion = new Version(1, 6); // 版本设置

            public override void PostSetupContent()
            {
                DoBossChecklistIntegration();
            }

            private void DoBossChecklistIntegration()
            {
                if (!ModLoader.TryGetMod("BossChecklist", out Mod bossChecklist) || bossChecklist.Version < BossChecklistAPIVersion)
                {
                    return; // 如果未找到模组或版本不匹配
                }
                //第1个Boss第1个Boss第1个Boss第1个Boss第1个Boss
                //***********************************************************************************************************************************
                string internalName = "BlackBear"; // 唯一标识符
                float weight = 0.1f; // 权重
                Func<bool> downed = () => DownedBossSystem.downedBlackBear; // Boss 击败状态

                int bossType = ModContent.NPCType<BlackBear>(); // Boss 的 NPC 类型
                                                                //int spawnItem = ModContent.ItemType<Items.MyBoss1Summoner>(); // 召唤 Boss 的物品类型

                //List<int> collectibles = new List<int>()
                //{

                //};
                // 自定义图标显示法
                var customPortrait = (SpriteBatch sb, Rectangle rect, Color color) =>
                {
                    Texture2D texture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/BlackBear/BlackBear").Value;
                    Vector2 centered = new Vector2(rect.X + (rect.Width / 2) - (texture.Width / 2), rect.Y + (rect.Height / 2) - (texture.Height / 2));
                    sb.Draw(texture, centered, color);
                };

                // 注册 Boss 信息
                bossChecklist.Call(
                    "LogBoss",
                    Mod,
                    internalName,
                    weight,
                    downed,
                    bossType,
                    new Dictionary<string, object>()
                    {
                        //["spawnItems"] = spawnItem,// 召唤物品
                        ["displayName"] = Language.GetText("黑熊金"),// 显示名称
                        //["spawnInfo"] = Language.GetText(""),// 召唤信息
                        //["collectibles"] = collectibles,// 收集物品
                        ["customPortrait"] = customPortrait// 自定义图标显示法

                    }
                );
                //自定义图标显示法
                var customPortrait1 = (SpriteBatch sb, Rectangle rect, Color color) =>
                {
                    Texture2D texture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/BlackBear/BlackBear").Value;
                    Vector2 centered = new Vector2(rect.X + (rect.Width / 2) - (texture.Width / 2), rect.Y + (rect.Height / 2) - (texture.Height / 2));
                    sb.Draw(texture, centered, color);
                };
            }
        }

        public class DownedBossSystem : ModSystem
        {
            public static bool downedBlackBear = false; // 跟踪 BlackBear 是否已被击败

            public override void SaveWorldData(TagCompound tag)
            {
                tag["downedBlackBear"] = downedBlackBear; // 保存状态
            }

            public override void LoadWorldData(TagCompound tag)
            {
                downedBlackBear = tag.GetBool("downedBlackBear"); // 加载状态
            }

            public override void OnWorldLoad()
            {
                // 重置所有 Boss 的击败状态
                downedBlackBear = false;
            }
        }
    }
}
