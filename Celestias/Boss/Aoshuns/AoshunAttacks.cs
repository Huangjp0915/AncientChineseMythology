using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    /// <summary>
    /// 敖顺全新攻击体系 - 与敖闰完全差异化
    /// 核心设计：地形交互、环境威胁制造、空间控制
    /// 
    /// 攻击列表：
    /// 1. ChainLightning     - 雷链穿刺：节点间连锁放电
    /// 2. AbyssalAmbush      - 深渊伏击：由AI状态机处理（Submerge→Emerge）
    /// 3. DragonScaleStorm   - 龙鳞风暴：身体段抛射带电鳞片
    /// 4. TornadoEnsnare     - 龙卷缠绕：盘旋释放追踪龙卷风
    /// 5. ThunderSeal        - 天雷印：标记延迟落雷
    /// 6. DragonKingRoar     - 龙王怒啸：全屏debuff波+冲击波
    /// 7. EyeOfTheStorm      - 风暴之眼：缩小安全区
    /// 8. ThunderChainCharge - 雷霆连环冲：由AI状态机处理
    /// </summary>
    public static class AoshunAttacks
    {
        #region 1. 雷链穿刺 - 在玩家周围生成闪电节点，节点间电弧连锁

        /// <summary>
        /// 在玩家周围半随机位置生成闪电节点
        /// 节点存在一段时间后，相邻节点之间产生电弧伤害
        /// 玩家需要在节点间穿梭躲避
        /// </summary>
        public static void SpawnChainLightningNodes(NPC npc, Player player, int nodeCount) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = Main.expertMode ? npc.damage / 4 : npc.damage / 3;
            float spreadRadius = 400f;

            // 计算节点位置 - 环绕玩家分布
            for (int i = 0; i < nodeCount; i++) {
                float angle = MathHelper.TwoPi * i / nodeCount + Main.rand.NextFloat(-0.3f, 0.3f);
                float dist = spreadRadius * (0.5f + Main.rand.NextFloat(0.5f));
                Vector2 offset = angle.ToRotationVector2() * dist;
                Vector2 spawnPos = player.Center + offset;

                // ai[0] = 节点编号, ai[1] = 总节点数
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    spawnPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<AoshunLightningNode>(),
                    damage, 0f, Main.myPlayer,
                    ai0: i, ai1: nodeCount
                );
            }
        }

        #endregion

        #region 3. 龙鳞风暴 - 从蠕虫身体段喷射带电龙鳞

        /// <summary>
        /// 遍历蠕虫身体段，从随机几个段向外射出带电龙鳞弹幕
        /// 每个龙鳞向身体段法线方向(垂直于蠕虫身体)飞出
        /// </summary>
        public static void ShootDragonScales(NPC headNpc, int scalesPerBurst) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = Main.expertMode ? headNpc.damage / 4 : headNpc.damage / 3;

            // 收集所有活跃身体段
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC seg = Main.npc[i];
                if (!seg.active || seg.realLife != headNpc.whoAmI) continue;
                if (seg.whoAmI == headNpc.whoAmI) continue; // 跳过头部
                if (seg.type == ModContent.NPCType<AoshunTail>()) continue; // 跳过尾部

                // 每个段有概率发射
                if (!Main.rand.NextBool(5)) continue;

                for (int s = 0; s < scalesPerBurst; s++) {
                    // 垂直于身体的法线方向
                    float bodyAngle = seg.rotation - MathHelper.PiOver2; // 身体朝向
                    float normalAngle = bodyAngle + MathHelper.PiOver2 * (Main.rand.NextBool() ? 1 : -1);
                    normalAngle += Main.rand.NextFloat(-0.4f, 0.4f); // 随机偏移

                    float speed = 6f + Main.rand.NextFloat(4f);
                    Vector2 vel = normalAngle.ToRotationVector2() * speed;

                    Projectile.NewProjectile(
                        headNpc.GetSource_FromAI(),
                        seg.Center,
                        vel,
                        ModContent.ProjectileType<AoshunDragonScale>(),
                        damage, 1f
                    );
                }
            }
        }

        #endregion

        #region 4. 龙卷缠绕 - 释放追踪龙卷风

        /// <summary>
        /// 在Boss当前位置生成一个缓慢追踪玩家的龙卷风弹幕
        /// 龙卷风对区域内玩家造成持续伤害和击退
        /// </summary>
        public static void SpawnTornado(NPC npc, Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = Main.expertMode ? npc.damage / 5 : npc.damage / 4;

            // 初始速度朝向玩家
            Vector2 toPlayer = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            Vector2 vel = toPlayer * 3f;

            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                npc.Center,
                vel,
                ModContent.ProjectileType<AoshunTornado>(),
                damage, 5f
            );
        }

        #endregion

        #region 5. 天雷印 - 标记位置后延迟落雷

        /// <summary>
        /// 在指定位置生成天雷预警标记
        /// delay帧后，该位置落下高伤害雷击
        /// 预警期间地面显示电弧标记
        /// </summary>
        public static void SpawnThunderSealMarker(NPC npc, Vector2 position, int delay) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = Main.expertMode ? npc.damage / 3 : npc.damage / 2;

            // ai[0] = 延迟帧数
            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                position,
                Vector2.Zero,
                ModContent.ProjectileType<AoshunThunderSeal>(),
                damage, 0f, Main.myPlayer,
                ai0: delay
            );
        }

        #endregion

        #region 6. 冲击波 - 环形向外扩散

        /// <summary>
        /// 从Boss中心向所有方向释放冲击波弹幕
        /// 用于深渊伏击出击和龙王怒啸
        /// </summary>
        public static void SpawnShockwave(NPC npc, int count) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = Main.expertMode ? npc.damage / 4 : npc.damage / 3;
            float speed = 8f;

            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 vel = angle.ToRotationVector2() * speed;

                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    vel,
                    ModContent.ProjectileType<AoshunShockwave>(),
                    damage, 3f
                );
            }
        }

        #endregion

        #region 7. 风暴之眼 - 缩小安全区

        /// <summary>
        /// 以指定位置为中心生成一个持续缩小的风暴眼
        /// 风暴眼内部安全，外部持续受伤
        /// </summary>
        public static void SpawnStormEye(NPC npc, Vector2 center, int duration) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = Main.expertMode ? npc.damage / 5 : npc.damage / 4;

            // ai[0] = 总持续时间
            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                center,
                Vector2.Zero,
                ModContent.ProjectileType<AoshunStormEye>(),
                damage, 0f, Main.myPlayer,
                ai0: duration
            );
        }

        #endregion

        #region 8. 电痕 - 雷霆连环冲留下的持续伤害

        /// <summary>
        /// 在Boss当前位置留下电痕弹幕
        /// 电痕持续存在一段时间，接触时造成伤害
        /// </summary>
        public static void SpawnElectricTrail(NPC npc) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = Main.expertMode ? npc.damage / 5 : npc.damage / 4;

            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                npc.Center,
                Vector2.Zero,
                ModContent.ProjectileType<AoshunElectricTrail>(),
                damage, 0f
            );
        }

        #endregion
    }
}
