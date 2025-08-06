global using InnoVault;
global using Microsoft.Xna.Framework;
using AncientChineseMythology.NPCs.Boss.Archosaur;
using AncientChineseMythology.NPCs.Boss.BlackBear;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;


namespace AncientChineseMythology
{
    // Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
    public class AncientChineseMythology : Mod
    {
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
            public Vertex(Vector2 position, Vector3 texCoord, Color color) {
                Position = position;
                TexCoord = texCoord;
                Color = color;
            }
            public VertexDeclaration VertexDeclaration {
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

            public override void PostSetupContent() {
                DoBossChecklistIntegration();
            }

            private void DoBossChecklistIntegration() {
                if (!ModLoader.TryGetMod("BossChecklist", out Mod bossChecklist) || bossChecklist.Version < BossChecklistAPIVersion) {
                    return; // 如果未找到模组或版本不匹配
                }
                //第1个Boss第1个Boss第1个Boss第1个Boss第1个Boss
                //***********************************************************************************************************************************
                string internalName_01 = "BlackBear"; // 唯一标识符
                float weight_01 = 0.1f; // 权重
                Func<bool> downed_01 = () => DownedBossSystem.downedBlackBear; // Boss 击败状态

                int bossType_01 = ModContent.NPCType<BlackBear>(); // Boss 的 NPC 类型
                                                                   //int spawnItem = ModContent.ItemType<Items.MyBoss1Summoner>(); // 召唤 Boss 的物品类型

                //List<int> collectibles = new List<int>()
                //{

                //};
                // 自定义图标显示法
                var customPortrait_01 = (SpriteBatch sb, Rectangle rect, Color color) => {
                    Texture2D texture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear").Value;
                    Vector2 centered = new Vector2(rect.X + (rect.Width / 2) - (texture.Width / 2), rect.Y + (rect.Height / 2) - (texture.Height / 2));
                    sb.Draw(texture, centered, color);
                };

                // 注册 Boss 信息
                bossChecklist.Call(
                    "LogBoss",
                    Mod,
                    internalName_01,
                    weight_01,
                    downed_01,
                    bossType_01,
                    new Dictionary<string, object>() {
                        //["spawnItems"] = spawnItem,// 召唤物品
                        ["displayName"] = Language.GetText("黑熊金"),// 显示名称
                        //["spawnInfo"] = Language.GetText(""),// 召唤信息
                        //["collectibles"] = collectibles,// 收集物品
                        ["customPortrait"] = customPortrait_01// 自定义图标显示法

                    }
                );
                //自定义图标显示法
                var customPortrait1_01 = (SpriteBatch sb, Rectangle rect, Color color) => {
                    Texture2D texture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear").Value;
                    Vector2 centered = new Vector2(rect.X + (rect.Width / 2) - (texture.Width / 2), rect.Y + (rect.Height / 2) - (texture.Height / 2));
                    sb.Draw(texture, centered, color);
                };

                //***********************************************************************************************************************************
                string internalName_05 = "Archosaur"; // 唯一标识符
                float weight_05 = 0.1f; // 权重
                Func<bool> downed_05 = () => DownedBossSystem.downedBlackBear; // Boss 击败状态

                int bossType_05 = ModContent.NPCType<ArchosaurBoss>(); // Boss 的 NPC 类型
                                                                       //int spawnItem = ModContent.ItemType<Items.MyBoss1Summoner>(); // 召唤 Boss 的物品类型

                //List<int> collectibles = new List<int>()
                //{

                //};
                // 自定义图标显示法
                var customPortrait_05 = (SpriteBatch sb, Rectangle rect, Color color) => {
                    Texture2D texture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/Archosaur/Archosaur").Value;
                    Vector2 centered = new Vector2(rect.X + (rect.Width / 2) - (texture.Width / 2), rect.Y + (rect.Height / 2) - (texture.Height / 2));
                    sb.Draw(texture, centered, color);
                };

                // 注册 Boss 信息
                bossChecklist.Call(
                    "LogBoss",
                    Mod,
                    internalName_05,
                    weight_05,
                    downed_05,
                    bossType_05,
                    new Dictionary<string, object>() {
                        //["spawnItems"] = spawnItem,// 召唤物品
                        ["displayName"] = Language.GetText("祖龙残魂"),// 显示名称
                        //["spawnInfo"] = Language.GetText(""),// 召唤信息
                        //["collectibles"] = collectibles,// 收集物品
                        ["customPortrait"] = customPortrait_05// 自定义图标显示法

                    }
                );
                //自定义图标显示法
                var customPortrait1_05 = (SpriteBatch sb, Rectangle rect, Color color) => {
                    Texture2D texture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/Archosaur/Archosaur").Value;
                    Vector2 centered = new Vector2(rect.X + (rect.Width / 2) - (texture.Width / 2), rect.Y + (rect.Height / 2) - (texture.Height / 2));
                    sb.Draw(texture, centered, color);
                };
            }
        }

        public class DownedBossSystem : ModSystem
        {
            public static bool downedBlackBear = false; // 跟踪 BlackBear 是否已被击败
            public static bool downedArchosaur = false;

            public override void SaveWorldData(TagCompound tag) {
                tag["downedBlackBear"] = downedBlackBear; // 保存状态
                tag["downedArchosaur"] = downedArchosaur; // 保存状态
            }

            public override void LoadWorldData(TagCompound tag) {
                downedBlackBear = tag.GetBool("downedBlackBear"); // 加载状态
                downedArchosaur = tag.GetBool("downedArchosaur"); // 加载状态
            }

            public override void OnWorldLoad() {
                // 重置所有 Boss 的击败状态
                downedBlackBear = false;
                downedArchosaur = false;
            }
        }


    }
}
