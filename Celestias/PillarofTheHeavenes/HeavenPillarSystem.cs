using InnoVault.Actors;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes
{
    /// <summary>
    /// 天柱系统管理器
    /// 负责天柱的生成、持久化保存和世界状态管理
    /// </summary>
    public class HeavenPillarSystem : ModSystem
    {
        #region 常量配置
        /// <summary>天柱数量</summary>
        public const int PillarCount = 4;

        /// <summary>天柱间距（地图宽度的比例）</summary>
        public const float PillarSpacing = 0.2f;

        /// <summary>天柱生成的垂直偏移（从地表向上，适应放大后的天柱）</summary>
        public const int VerticalOffset = 600;
        #endregion

        #region 状态数据
        /// <summary>天柱是否已降临</summary>
        public static bool PillarsDescended { get; private set; } = false;

        /// <summary>四根天柱的世界位置</summary>
        public static Vector2[] PillarPositions { get; private set; } = new Vector2[PillarCount];

        /// <summary>四根天柱的Actor索引</summary>
        public static int[] PillarActorIndices { get; private set; } = new int[PillarCount];

        /// <summary>天柱降临的时间戳</summary>
        public static double DescendTime { get; private set; } = 0;
        #endregion

        #region 生命周期
        public override void OnWorldLoad() {
            // 重置状态
            PillarsDescended = false;
            PillarPositions = new Vector2[PillarCount];
            PillarActorIndices = new int[PillarCount];
            for (int i = 0; i < PillarCount; i++) {
                PillarActorIndices[i] = -1;
            }
            DescendTime = 0;
        }

        public override void OnWorldUnload() {
            // 清理状态
            PillarsDescended = false;
            for (int i = 0; i < PillarCount; i++) {
                PillarPositions[i] = Vector2.Zero;
                PillarActorIndices[i] = -1;
            }
        }

        public override void PostUpdateWorld() {
            // 如果天柱已降临但Actor不存在，需要重新生成
            if (PillarsDescended && Main.netMode != NetmodeID.MultiplayerClient) {
                ValidateAndRestorePillars();
            }
        }
        #endregion

        #region 天柱生成
        /// <summary>
        /// 触发天柱降临事件
        /// 在击败月球领主后调用
        /// </summary>
        /// <param name="epicenterX">月球领主死亡位置X坐标（世界坐标）</param>
        public static void TriggerPillarDescend(float epicenterX) {
            if (PillarsDescended) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            PillarsDescended = true;
            DescendTime = Main.GameUpdateCount;

            // 计算四根天柱的位置
            CalculatePillarPositions(epicenterX);

            // 生成天柱Actor
            SpawnPillarActors();

            // 广播消息
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.WorldData);
            }

            // 显示降临消息
            Main.NewText("天柱降临，四方神圣显现...", 220, 200, 100);
        }

        /// <summary>
        /// 计算四根天柱的世界位置
        /// </summary>
        private static void CalculatePillarPositions(float epicenterX) {
            float worldWidth = Main.maxTilesX * 16f;

            // 基于月球领主死亡位置计算天柱区域中心
            float centerX = MathHelper.Clamp(epicenterX, worldWidth * 0.2f, worldWidth * 0.8f);

            // 天柱横向分布范围
            float totalSpread = worldWidth * PillarSpacing * 3; // 三个间隔
            float spacing = totalSpread / 3;

            // 从中心向两侧分布
            float startX = centerX - totalSpread / 2;

            for (int i = 0; i < PillarCount; i++) {
                float pillarX = startX + spacing * i;

                // 确保不超出世界边界
                pillarX = MathHelper.Clamp(pillarX, 200 * 16f, (Main.maxTilesX - 200) * 16f);

                // 找到该位置的地表高度
                int tileX = (int)(pillarX / 16f);
                int surfaceY = FindSurfaceY(tileX);

                // 天柱位置（稍微在地表上方）
                PillarPositions[i] = new Vector2(pillarX, surfaceY * 16f - VerticalOffset);
            }
        }

        /// <summary>
        /// 查找指定X坐标的地表Y坐标
        /// </summary>
        private static int FindSurfaceY(int tileX) {
            // 从天空开始向下扫描
            for (int y = 100; y < Main.maxTilesY - 200; y++) {
                Tile tile = Main.tile[tileX, y];
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return y;
                }
            }
            return (int)(Main.worldSurface);
        }

        /// <summary>
        /// 生成天柱Actor实体
        /// </summary>
        private static void SpawnPillarActors() {
            for (int i = 0; i < PillarCount; i++) {
                Vector2 pos = PillarPositions[i];
                if (pos == Vector2.Zero) continue;

                // 使用ActorLoader生成Actor
                int actorId = ActorLoader.GetActorID<HeavenPillarActor>();
                int slot = ActorLoader.NewActor(actorId, pos, Vector2.Zero);

                if (slot >= 0 && slot < ActorLoader.MaxActorCount) {
                    PillarActorIndices[i] = slot;

                    // 设置天柱样式索引
                    Actor actor = ActorLoader.Actors[slot];
                    if (actor is HeavenPillarActor pillar) {
                        pillar.PillarStyleIndex = i;
                    }
                }
            }
        }

        /// <summary>
        /// 验证并恢复天柱（如果Actor丢失）
        /// </summary>
        private static void ValidateAndRestorePillars() {
            for (int i = 0; i < PillarCount; i++) {
                int actorIndex = PillarActorIndices[i];
                Vector2 pos = PillarPositions[i];

                if (pos == Vector2.Zero) continue;

                bool needsRestore = false;

                if (actorIndex < 0 || actorIndex >= ActorLoader.MaxActorCount) {
                    needsRestore = true;
                }
                else {
                    Actor actor = ActorLoader.Actors[actorIndex];
                    if (actor == null || !actor.Active || actor is not HeavenPillarActor) {
                        needsRestore = true;
                    }
                }

                if (needsRestore) {
                    // 重新生成天柱
                    int actorId = ActorLoader.GetActorID<HeavenPillarActor>();
                    int slot = ActorLoader.NewActor(actorId, pos, Vector2.Zero);

                    if (slot >= 0 && slot < ActorLoader.MaxActorCount) {
                        PillarActorIndices[i] = slot;

                        Actor actor = ActorLoader.Actors[slot];
                        if (actor is HeavenPillarActor pillar) {
                            pillar.PillarStyleIndex = i;
                            pillar.HasDescended = true; // 直接设置为已降临状态
                        }
                    }
                }
            }
        }
        #endregion

        #region 数据持久化
        public override void SaveWorldData(TagCompound tag) {
            tag["PillarsDescended"] = PillarsDescended;
            tag["DescendTime"] = DescendTime;

            // 保存天柱位置
            List<float> positionData = [];
            for (int i = 0; i < PillarCount; i++) {
                positionData.Add(PillarPositions[i].X);
                positionData.Add(PillarPositions[i].Y);
            }
            tag["PillarPositions"] = positionData;
        }

        public override void LoadWorldData(TagCompound tag) {
            PillarsDescended = tag.GetBool("PillarsDescended");
            DescendTime = tag.GetDouble("DescendTime");

            // 加载天柱位置
            if (tag.TryGet("PillarPositions", out List<float> positionData)) {
                for (int i = 0; i < PillarCount && i * 2 + 1 < positionData.Count; i++) {
                    PillarPositions[i] = new Vector2(positionData[i * 2], positionData[i * 2 + 1]);
                }
            }

            // 重置Actor索引（需要在世界加载后重新验证）
            for (int i = 0; i < PillarCount; i++) {
                PillarActorIndices[i] = -1;
            }
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 获取距离指定位置最近的天柱
        /// </summary>
        public static int GetNearestPillarIndex(Vector2 position) {
            if (!PillarsDescended) return -1;

            int nearest = -1;
            float minDist = float.MaxValue;

            for (int i = 0; i < PillarCount; i++) {
                if (PillarPositions[i] == Vector2.Zero) continue;

                float dist = Vector2.Distance(position, PillarPositions[i]);
                if (dist < minDist) {
                    minDist = dist;
                    nearest = i;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 检查指定位置是否在天柱影响范围内
        /// </summary>
        public static bool IsInPillarRange(Vector2 position, float range = HeavenPillarActor.EffectRadius) {
            if (!PillarsDescended) return false;

            for (int i = 0; i < PillarCount; i++) {
                if (PillarPositions[i] == Vector2.Zero) continue;

                if (Vector2.Distance(position, PillarPositions[i]) <= range) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取指定位置受到的天柱影响强度（0-1）
        /// </summary>
        public static float GetPillarInfluence(Vector2 position) {
            if (!PillarsDescended) return 0f;

            float maxInfluence = 0f;

            for (int i = 0; i < PillarCount; i++) {
                if (PillarPositions[i] == Vector2.Zero) continue;

                float dist = Vector2.Distance(position, PillarPositions[i]);
                if (dist <= HeavenPillarActor.EffectRadius) {
                    float influence = 1f - (dist / HeavenPillarActor.EffectRadius);
                    if (influence > maxInfluence) {
                        maxInfluence = influence;
                    }
                }
            }

            return maxInfluence;
        }
        #endregion
    }
}
