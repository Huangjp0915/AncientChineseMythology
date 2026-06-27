using AncientChineseMythology.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialDragons
{
    /// <summary>
    /// 天庭巡卫金龙 - 月球领主后的蠕虫类Boss (V2 三幕「天规」重做)。
    /// 继承BasicWorm以使用正确的蠕虫跟随系统; 贴图朝向: 右边向前(正方向)。
    ///
    /// V2 设计 (替换 V1 的 HP 比例密度档反模式):
    ///   ● 三幕脚本 (规则升级, 非数值加速):
    ///       巡天 (Act0, &gt;60%): 航点巡航 + 路径预警, 低密度; 天命赐福区 (Mandate Zone) 登场。
    ///       敕令 (Act1, ≤60%): DragonAuthority 成为强制目标 —— 玩家必须摧毁场地边缘的<b>敕令法标</b>来终止雷雨 (目标导向)。
    ///       天罚 (Act2, ≤25%): 破标后<b>每周期一次</b>解锁全屏天剑雨终结技, 随后强制巡航喘息 (非永久喷射)。
    ///   ● 天界身份机制: 天命赐福区 (风险/回报)、逆鳞脱落 (无敌金鳞, 可拆弹反弹光束)、敕令幕 = 可破目标谜题。
    ///   ● 蛇身即机制: 大圆环绕时体节周期性「充能」(金脉冲) 造成额外接触伤害。
    ///   ● 表现: DrawRadialBloomAt 金芒泛光 / ArenaRunic 法阵地纹 / DrawBeam 权威光束系带 / ElementalScreenTint 金芒底色。
    /// 幕跨越带 i-frame 节拍 (dontTakeDamage); 红=致命预警, 金=安全/权威。
    /// </summary>
    public abstract class CelestialDragons : BasicWorm
    {
        // 贴图尺寸常量
        protected const int HeadTextureWidth = 382;
        protected const int HeadTextureHeight = 256;
        protected const int BodyTextureWidth = 152;
        protected const int BodyTextureHeight = 92;
        protected const int TailTextureWidth = 412;
        protected const int TailTextureHeight = 124;

        // 体节覆盖比例（40%覆盖）
        protected const float SegmentOverlapRatio = 0.40f;

        // ===== V2 三幕状态机 =====
        public const int StateCruise = 0;       // 巡天 (低密度航点巡航)
        public const int StateSword = 1;        // 剑气喷吐 (8字形)
        public const int StateDive = 2;         // 俯冲穿越 + 逆鳞脱落
        public const int StateCircle = 3;       // 大圆环绕 + 体节充能
        public const int StateEdict = 4;        // 敕令 (法标可破目标 + 雷雨)
        public const int StateFullScreen = 5;   // 天罚终结技 (一次性)
        public const int StateTransition = 6;   // 幕跨越 i-frame 节拍
        public const int StateRecovery = 7;     // 天罚后强制巡航喘息

        // 头部专用状态 (实例字段; 关键项经 SendExtraAI/ReceiveExtraAI 同步)
        private int storedAct = -1;     // 当前已进入的幕 (用于检测跨幕)
        private int rotIndex;           // 攻击轮替索引 (规则化轮转)
        private bool fullScreenArmed;   // 天罚: 破标后解锁一次全屏
        private bool sealsSpawned;      // 本次敕令是否已布标
        private bool edictBroken;       // 本次敕令是否已破

        // 表现层 (纯本地)
        private float bloomPulse;       // 金芒径向泛光强度
        private float tintLevel;        // 金芒屏幕底色强度

        // 体节充能 (Body/Tail; 由 UpdateSegmentCharge 读头部状态计算)
        public bool Charging;
        private float chargeVis;

        /// <summary>不使用SpriteDirection翻转，我们手动处理</summary>
        public override bool IsUseSpriteDirection => false;

        /// <summary>目标玩家</summary>
        public Player Target {
            get {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers ||
                    Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[NPC.type] = 3;
            NPCID.Sets.TrailCacheLength[NPC.type] = 10;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.damage = 280;
            NPC.defense = 110;
            NPC.lifeMax = 1800000;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = 500000f;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.netAlways = true;
            SummonMax = 50;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            base.SendExtraAI(writer);
            writer.Write(storedAct);
            writer.Write(rotIndex);
            writer.Write(fullScreenArmed);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            storedAct = reader.ReadInt32();
            rotIndex = reader.ReadInt32();
            fullScreenArmed = reader.ReadBoolean();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            if (NPCWormType != WormType.Head)
                return false;
            return null;
        }

        public override void BossHeadRotation(ref float rotation) {
            rotation = NPC.velocity.ToRotation();
        }

        public override void AI() {
            base.AI();

            if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active) {
                NPC.dontTakeDamage = Main.npc[NPC.realLife].dontTakeDamage;
            }

            if (NPCWormType == WormType.Head)
                HeadAI();
            else
                UpdateSegmentCharge();

            // 金色粒子效果
            if (!Main.dedServ && Main.rand.NextBool(12)) {
                int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height,
                    DustID.GoldFlame, NPC.velocity.X * 0.1f, NPC.velocity.Y * 0.1f, 100, default, 0.8f);
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(NPC.Center, 0.5f, 0.4f, 0.1f);
        }

        /// <summary>重写位置计算 - 体节被头部牵引，不是插值跟随</summary>
        public override void ChangePos() {
            if (FatherNPC == null) return;

            float segmentWidth = GetSegmentWidth();
            float parentWidth = GetParentSegmentWidth();
            float targetDistance = (parentWidth + segmentWidth) * 0.5f * (1f - SegmentOverlapRatio);

            Vector2 directionFromParent = (NPC.Center - FatherNPC.Center).SafeNormalize(Vector2.UnitX);
            NPC.Center = FatherNPC.Center + directionFromParent * targetDistance;

            NPC.velocity = (FatherNPC.Center - NPC.Center).SafeNormalize(Vector2.Zero) * FatherNPC.velocity.Length();

            NPC.rotation = (FatherNPC.Center - NPC.Center).ToRotation();
        }

        protected virtual float GetSegmentWidth() {
            return NPCWormType switch {
                WormType.Head => HeadTextureWidth * 0.5f,
                WormType.Body => BodyTextureWidth,
                WormType.Tail => TailTextureWidth * 0.4f,
                _ => BodyTextureWidth
            };
        }

        protected float GetParentSegmentWidth() {
            if (FatherNPC?.ModNPC is CelestialDragons parentDragon) {
                return parentDragon.GetSegmentWidth();
            }
            return HeadTextureWidth * 0.5f;
        }

        // ============================================================
        //  头部三幕状态机
        // ============================================================

        private static int DesiredAct(float lifeRatio) => lifeRatio <= 0.25f ? 2 : (lifeRatio <= 0.6f ? 1 : 0);

        private void HeadAI() {
            Player player = Target;
            if (!player.active || player.dead) {
                NPC.TargetClosest();
                player = Target;
                if (!player.active || player.dead) {
                    NPC.velocity.Y -= 0.5f;
                    if (NPC.timeLeft > 10)
                        NPC.timeLeft = 10;
                    return;
                }
            }

            if (NPC.localAI[3] == 0f) {
                NPC.localAI[3] = 1f;
                NPC.ai[3] = Main.rand.NextBool() ? 1f : -1f;
                NPC.ai[0] = StateCruise;
                storedAct = DesiredAct((float)NPC.life / NPC.lifeMax);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.2f, Pitch = 0.3f }, NPC.Center);
            }

            NPC.localAI[0]++;
            float lifeRatio = (float)NPC.life / NPC.lifeMax;
            int act = DesiredAct(lifeRatio);

            // —— 跨幕 i-frame 节拍: 仅在血量跌破阈值且非过场时触发 ——
            if (act > storedAct && (int)NPC.ai[0] != StateTransition) {
                NPC.ai[0] = StateTransition;
                NPC.ai[1] = 0;
                NPC.netUpdate = true;
            }

            switch ((int)NPC.ai[0]) {
                case StateTransition: RunTransition(player, act); break;
                case StateCruise: RunCruise(player, act); break;
                case StateSword: RunSwordBreath(player, act); break;
                case StateDive: RunDive(player, act); break;
                case StateCircle: RunCircle(player, act); break;
                case StateEdict: RunEdict(player, act); break;
                case StateFullScreen: RunFullScreen(player, act); break;
                case StateRecovery: RunRecovery(player, act); break;
                default: NPC.ai[0] = StateCruise; break;
            }

            NPC.ai[1]++;

            PublishPresentation(player, act);

            NPC.rotation = NPC.velocity.ToRotation();
        }

        /// <summary>切换到指定状态并重置计时/航点; 翻转巡航方向。</summary>
        private void SetState(int s) {
            NPC.ai[0] = s;
            NPC.ai[1] = 0;
            NPC.ai[2] = 0;
            NPC.localAI[1] = 0;
            NPC.localAI[2] = 0;
            NPC.ai[3] *= -1f;
            sealsSpawned = false;
            edictBroken = false;
            NPC.netUpdate = true;
        }

        /// <summary>按幕的规则化轮替选择下一状态 (规则不同, 而非密度不同)。</summary>
        private void AdvanceState(int act) {
            rotIndex++;
            if (act == 2 && fullScreenArmed) {
                fullScreenArmed = false;
                SetState(StateFullScreen);
                return;
            }
            int next;
            if (act == 0)
                next = (rotIndex % 4) switch { 0 => StateSword, 1 => StateDive, 2 => StateCircle, _ => StateCruise };
            else if (act == 1)
                next = (rotIndex % 4) switch { 0 => StateEdict, 1 => StateDive, 2 => StateCircle, _ => StateCruise };
            else
                next = (rotIndex % 3) switch { 0 => StateEdict, 1 => StateDive, _ => StateCircle };
            SetState(next);
        }

        /// <summary>幕跨越: 上升盘旋 + 无敌节拍 + 渐强震屏/泛光, 结束后进入敕令。</summary>
        private void RunTransition(Player player, int act) {
            NPC.dontTakeDamage = true;

            Vector2 target = player.Center + new Vector2(NPC.ai[3] * 700f, -650f);
            ApplyMovement(target, 18f, 0.03f, 12f);

            if ((int)NPC.ai[1] == 1)
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.4f, Pitch = -0.2f }, NPC.Center);

            float p = MathHelper.Clamp(NPC.ai[1] / 70f, 0f, 1f);
            ACMUtils.AddScreenShake(2f + p * 8f);
            bloomPulse = MathHelper.Max(bloomPulse, 0.4f + p * 0.5f);

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 v = Main.rand.NextVector2CircularEdge(10, 10);
                int d = Dust.NewDust(NPC.Center + v * 6f, 0, 0, DustID.GoldFlame, -v.X, -v.Y, 100, default, 2f);
                Main.dust[d].noGravity = true;
            }

            if (NPC.ai[1] >= 75) {
                storedAct = act;
                NPC.dontTakeDamage = false;
                rotIndex = 0;
                SetState(StateEdict); // 升幕首发: 敕令登场
            }
        }

        /// <summary>巡天 - 大范围航点巡航 + 路径预警 + 天命赐福区; 低密度, 无天雷。</summary>
        private void RunCruise(Player player, int act) {
            float direction = NPC.ai[3];
            int wp = (int)NPC.localAI[1];
            const float horizontalDist = 1400f;
            const float verticalRange = 520f;
            const float baseHeight = 350f;

            Vector2 targetPos = (wp % 4) switch {
                0 => player.Center + new Vector2(direction * horizontalDist, -baseHeight - verticalRange),
                1 => player.Center + new Vector2(-direction * horizontalDist, -baseHeight + verticalRange * 0.5f),
                2 => player.Center + new Vector2(-direction * horizontalDist, -baseHeight - verticalRange * 0.8f),
                _ => player.Center + new Vector2(direction * horizontalDist, -baseHeight + verticalRange * 0.3f)
            };
            if (Vector2.Distance(NPC.Center, targetPos) < 250f)
                NPC.localAI[1]++;
            ApplyMovement(targetPos, 25f, 0.025f, 21f);

            // 路径预警
            if ((int)NPC.ai[1] % 100 == 50 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 futurePos = NPC.Center + NPC.velocity * 50f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, NPC.velocity.SafeNormalize(Vector2.Zero),
                    ModContent.ProjectileType<CelestialPathWarning>(), 0, 0f, Main.myPlayer, futurePos.X, futurePos.Y);
            }
            // 低密度辐射弹
            if ((int)NPC.ai[1] % 70 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 4;
                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi * i / count;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, angle.ToRotationVector2() * 6f,
                        ModContent.ProjectileType<GoldenEnergy>(), NPC.damage / 4, 3f, Main.myPlayer);
                }
            }

            if ((int)NPC.ai[1] == 90)
                TrySpawnMandateZone(player, 420f, 300f);

            if (NPC.ai[1] >= 300)
                AdvanceState(act);
        }

        /// <summary>剑气喷吐 - 8字形航点, 朝赐福区/玩家喷剑气。</summary>
        private void RunSwordBreath(Player player, int act) {
            float direction = NPC.ai[3];
            int wp = (int)NPC.localAI[2];
            const float horizontalDist = 900f;
            const float verticalDist = 420f;
            const float baseHeight = 300f;

            Vector2 targetPos = (wp % 4) switch {
                0 => player.Center + new Vector2(direction * horizontalDist, -baseHeight - verticalDist),
                1 => player.Center + new Vector2(-direction * horizontalDist * 0.5f, -baseHeight + verticalDist * 0.3f),
                2 => player.Center + new Vector2(-direction * horizontalDist, -baseHeight - verticalDist * 0.8f),
                _ => player.Center + new Vector2(direction * horizontalDist * 0.5f, -baseHeight + verticalDist * 0.5f)
            };
            if (Vector2.Distance(NPC.Center, targetPos) < 200f)
                NPC.localAI[2]++;
            ApplyMovement(targetPos, 23f, 0.028f, 19f);

            if ((int)NPC.ai[1] % 30 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toAim = (AimPoint(player) - NPC.Center).SafeNormalize(Vector2.Zero);
                int projectileCount = 4;
                const float spread = 0.5f;
                for (int i = 0; i < projectileCount; i++) {
                    float angle = spread * ((i - (projectileCount - 1) / 2f) / (projectileCount - 1));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + toAim * 50, toAim.RotatedBy(angle) * 14f,
                        ModContent.ProjectileType<GoldenSwordAura>(), NPC.damage / 4, 3f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = 0.3f }, NPC.Center);
            }

            if (NPC.ai[1] >= 240)
                AdvanceState(act);
        }

        /// <summary>俯冲穿越 - 高速穿越并沿途脱落无敌金鳞 (Scale Shedding)。</summary>
        private void RunDive(Player player, int act) {
            int subPhase = (int)NPC.ai[2];
            float side = NPC.ai[3];

            if (subPhase == 0) { // 准备: 飞到一侧高处
                Vector2 targetPos = player.Center + new Vector2(side * 1200f, -800f);
                ApplyMovement(targetPos, 28f, 0.03f, 22f);
                if (Vector2.Distance(NPC.Center, targetPos) < 200f) {
                    NPC.ai[2] = 1;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 diveTarget = player.Center + new Vector2(-side * 1000f, 300f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<CelestialPathWarning>(), 0, 0f, Main.myPlayer, diveTarget.X, diveTarget.Y);
                    }
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1f, Pitch = 0.5f }, NPC.Center);
                }
            }
            else if (subPhase == 1) { // 俯冲: 脱落金鳞
                Vector2 targetPos = player.Center + new Vector2(-side * 1000f, 300f);
                ApplyMovement(targetPos, 38f, 0.02f, 32f);

                if ((int)NPC.ai[1] % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, NPC.velocity * 0.15f,
                        ModContent.ProjectileType<CelestialScale>(), NPC.damage / 4, 0f, Main.myPlayer);
                }

                if (Vector2.Distance(NPC.Center, targetPos) < 200f ||
                    (NPC.Center.Y > player.Center.Y + 400f && MathF.Abs(NPC.Center.X - player.Center.X) > 600f)) {
                    NPC.ai[2] = 2;
                }
            }
            else { // 回升
                Vector2 targetPos = player.Center + new Vector2(-side * 800f, -500f);
                ApplyMovement(targetPos, 22f, 0.025f, 18f);
                if (Vector2.Distance(NPC.Center, targetPos) < 220f || NPC.ai[1] >= 220)
                    AdvanceState(act);
            }
        }

        /// <summary>大圆环绕 - 椭圆轨道环绕; 蛇身体节周期充能 (体节即机制, 见 UpdateSegmentCharge)。</summary>
        private void RunCircle(Player player, int act) {
            const float radius = 780f;
            const float angularSpeed = 0.02f;
            float angle = NPC.ai[1] * angularSpeed * NPC.ai[3];
            Vector2 targetPos = player.Center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * 0.6f - 150f);
            ApplyMovement(targetPos, 23f, 0.035f, 19f);

            if ((int)NPC.ai[1] % 18 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toAim = (AimPoint(player) - NPC.Center).SafeNormalize(Vector2.Zero);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, toAim * 9f,
                    ModContent.ProjectileType<GoldenEnergy>(), NPC.damage / 4, 3f, Main.myPlayer);
            }

            // 体节充能波的预警提示音 (体节自身在 UpdateSegmentCharge 读头部计时点亮)
            if ((int)NPC.ai[1] % 90 == 28 && !Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 0.5f }, NPC.Center);

            if (NPC.ai[1] >= 360)
                AdvanceState(act);
        }

        /// <summary>敕令 - 强制目标: 布下边缘可破法标, 法标在场即降雷雨; 全破即停 (天罚解锁全屏一次)。</summary>
        private void RunEdict(Player player, int act) {
            float direction = NPC.ai[3];
            int wp = (int)NPC.localAI[2] % 4;
            const float radius = 700f;
            const float baseHeight = 450f;
            Vector2 targetPos = wp switch {
                0 => player.Center + new Vector2(direction * radius, -baseHeight),
                1 => player.Center + new Vector2(0, -baseHeight - 200f),
                2 => player.Center + new Vector2(-direction * radius, -baseHeight),
                _ => player.Center + new Vector2(0, -baseHeight + 100f)
            };
            if (Vector2.Distance(NPC.Center, targetPos) < 180f)
                NPC.localAI[2]++;
            ApplyMovement(targetPos, 17f, 0.03f, 14f);

            // 布标 (一次, 玩家四周边缘)
            if (!sealsSpawned && NPC.ai[1] >= 60) {
                sealsSpawned = true;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.1f, Volume = 1f }, player.Center);
                    ACMUtils.AddScreenShake(7f);
                }
                bloomPulse = MathHelper.Max(bloomPulse, 0.7f);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int seals = 4;
                    for (int i = 0; i < seals; i++) {
                        float a = MathHelper.TwoPi * i / seals + MathHelper.PiOver4;
                        Vector2 pos = player.Center + a.ToRotationVector2() * 720f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                            ModContent.ProjectileType<EdictBeacon>(), NPC.damage / 4, 0f, Main.myPlayer, NPC.damage / 4, NPC.whoAmI);
                    }
                }
            }

            // 持续辐射剑气 (中低密度, 朝赐福区/玩家)
            if (sealsSpawned && (int)NPC.ai[1] % 45 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toAim = (AimPoint(player) - NPC.Center).SafeNormalize(Vector2.Zero);
                for (int i = -1; i <= 1; i++)
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, toAim.RotatedBy(i * 0.25f) * 12f,
                        ModContent.ProjectileType<GoldenSwordAura>(), NPC.damage / 4, 3f, Main.myPlayer);
            }

            // 破标: 全部法标被摧毁 → 雷雨止, 奖励节拍 (天罚解锁一次全屏)
            if (sealsSpawned && !edictBroken && CountSeals() == 0) {
                edictBroken = true;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.2f, Volume = 1f }, NPC.Center);
                    ACMUtils.AddScreenShake(6f);
                }
                bloomPulse = MathHelper.Max(bloomPulse, 0.9f);
                if (act == 2)
                    fullScreenArmed = true;
                AdvanceState(act);
                return;
            }

            // 容错超时: 久未破标 → 强制清标并结束 (避免无解卡幕)
            if (sealsSpawned && NPC.ai[1] >= 1500) {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    KillAllSeals();
                AdvanceState(act);
            }
        }

        /// <summary>天罚 - 全屏天剑雨终结技 (act2 破标后一次性), 金剑逐列扫格落下; 结束强制进入巡航喘息。</summary>
        private void RunFullScreen(Player player, int act) {
            float direction = NPC.ai[3];
            int wp = (int)NPC.localAI[2] % 4;
            const float radius = 650f;
            const float baseHeight = 500f;
            Vector2 targetPos = wp switch {
                0 => player.Center + new Vector2(direction * radius, -baseHeight),
                1 => player.Center + new Vector2(0, -baseHeight - 150f),
                2 => player.Center + new Vector2(-direction * radius, -baseHeight),
                _ => player.Center + new Vector2(0, -baseHeight + 50f)
            };
            if (Vector2.Distance(NPC.Center, targetPos) < 180f)
                NPC.localAI[2]++;
            ApplyMovement(targetPos, 18f, 0.025f, 14f);

            // 蓄力可读: 金芒泛光 + 渐强震屏
            if (NPC.ai[1] < 90) {
                bloomPulse = MathHelper.Max(bloomPulse, NPC.ai[1] / 90f * 0.8f);
                ACMUtils.AddScreenShake(NPC.ai[1] / 90f * 5f);
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 v = Main.rand.NextVector2CircularEdge(8, 8);
                    int d = Dust.NewDust(NPC.Center + v * 50, 0, 0, DustID.GoldFlame, -v.X * 2, -v.Y * 2, 100, default, 2f);
                    Main.dust[d].noGravity = true;
                }
            }
            if ((int)NPC.ai[1] == 90) {
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.5f, Pitch = -0.3f }, NPC.Center);
                ACMUtils.AddScreenShake(12f);
                bloomPulse = 1f;
            }

            // 环形闪电预警 → 闪电
            if ((int)NPC.ai[1] == 100 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 14;
                for (int i = 0; i < count; i++) {
                    float ang = MathHelper.TwoPi * i / count;
                    Vector2 spawnPos = player.Center + ang.ToRotationVector2() * 800;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, Vector2.Zero,
                        ModContent.ProjectileType<CelestialPathWarning>(), 0, 0f, Main.myPlayer, player.Center.X, player.Center.Y);
                }
            }
            if ((int)NPC.ai[1] == 140 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 14;
                for (int i = 0; i < count; i++) {
                    float ang = MathHelper.TwoPi * i / count;
                    Vector2 spawnPos = player.Center + ang.ToRotationVector2() * 800;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, -ang.ToRotationVector2() * 12f,
                        ModContent.ProjectileType<CelestialLightning>(), NPC.damage / 3, 5f, Main.myPlayer);
                }
            }

            // 天降金剑 - 逐列扫格 (左→右), 玩家顺安全格推进
            if (NPC.ai[1] >= 120 && NPC.ai[1] <= 220 && (int)NPC.ai[1] % 6 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float colX = player.Center.X - 800f + (NPC.ai[1] - 120f) / 100f * 1600f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(colX, player.Center.Y - 700f), new Vector2(0, 16f),
                    ModContent.ProjectileType<FallingSword>(), NPC.damage / 3, 3f, Main.myPlayer);
            }

            if (NPC.ai[1] >= 250)
                SetState(StateRecovery);
        }

        /// <summary>天罚后强制巡航喘息 - 仅巡航无攻击; 偶发赐福区作为喘息奖励。</summary>
        private void RunRecovery(Player player, int act) {
            float direction = NPC.ai[3];
            Vector2 targetPos = player.Center + new Vector2(direction * 1000f, -500f);
            ApplyMovement(targetPos, 20f, 0.03f, 14f);

            if ((int)NPC.ai[1] == 60)
                TrySpawnMandateZone(player, 380f, 260f);

            if (NPC.ai[1] >= 240)
                AdvanceState(act);
        }

        // ============================================================
        //  表现层 + 工具
        // ============================================================

        private void PublishPresentation(Player player, int act) {
            if (Main.dedServ)
                return;

            float targetTint = 0.16f + act * 0.14f;
            if ((int)NPC.ai[0] == StateTransition)
                targetTint += 0.2f;
            tintLevel = MathHelper.Lerp(tintLevel, targetTint, 0.05f);

            float runic = (int)NPC.ai[0] == StateEdict && sealsSpawned ? 0.7f : 0f;

            CelestialScreenSystem.Publish(player.Center, tintLevel, runic, 740f, (float)Main.GlobalTimeWrappedHourly);

            bloomPulse = MathHelper.Lerp(bloomPulse, 0f, 0.06f);
        }

        /// <summary>若玩家正站在某个天命赐福区内, 金龙优先朝该区中心攻击 (风险/回报)。</summary>
        private Vector2 AimPoint(Player player) {
            int type = ModContent.ProjectileType<MandateZone>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile mp = Main.projectile[i];
                if (!mp.active || mp.type != type)
                    continue;
                float r = mp.ai[0] <= 0 ? 200f : mp.ai[0];
                if (Vector2.DistanceSquared(player.Center, mp.Center) < r * r)
                    return mp.Center;
            }
            return player.Center;
        }

        private void TrySpawnMandateZone(Player player, float spreadX, float spreadY) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (CountProjectiles(ModContent.ProjectileType<MandateZone>()) >= 2)
                return;
            Vector2 pos = player.Center + Main.rand.NextVector2CircularEdge(spreadX, spreadY);
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<MandateZone>(), 0, 0f, Main.myPlayer, 200f);
        }

        private static int CountProjectiles(int type) {
            int c = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
                if (Main.projectile[i].active && Main.projectile[i].type == type)
                    c++;
            return c;
        }

        private int CountSeals() {
            int type = ModContent.ProjectileType<EdictBeacon>();
            int c = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
                if (Main.projectile[i].active && Main.projectile[i].type == type && (int)Main.projectile[i].ai[1] == NPC.whoAmI)
                    c++;
            return c;
        }

        private void KillAllSeals() {
            int type = ModContent.ProjectileType<EdictBeacon>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile sp = Main.projectile[i];
                if (sp.active && sp.type == type && (int)sp.ai[1] == NPC.whoAmI)
                    sp.Kill();
            }
        }

        /// <summary>体节充能波 (Body-as-Mechanic): 大圆环绕时, 充能波沿体节自头向尾波动传递。</summary>
        private void UpdateSegmentCharge() {
            Charging = false;
            if (NPCWormType == WormType.Head)
                return;
            if (NPC.realLife < 0 || !Main.npc[NPC.realLife].active) {
                chargeVis = MathHelper.Lerp(chargeVis, 0f, 0.1f);
                return;
            }
            NPC head = Main.npc[NPC.realLife];
            if (head.ModNPC is not CelestialDragons || (int)head.ai[0] != StateCircle) {
                chargeVis = MathHelper.Lerp(chargeVis, 0f, 0.1f);
                return;
            }

            float local = head.ai[1] - SummonCount * 4f;
            if (local < 0) {
                chargeVis = MathHelper.Lerp(chargeVis, 0f, 0.1f);
                return;
            }
            float cyc = local % 90f;
            // 0~30 预警渐亮 → 30~60 充能危险 → 60~90 冷却
            Charging = cyc >= 30f && cyc < 60f;
            chargeVis = cyc < 30f ? cyc / 30f : (cyc < 60f ? 1f : MathHelper.Clamp(1f - (cyc - 60f) / 30f, 0f, 1f));

            if (Charging && !Main.dedServ && Main.rand.NextBool(4)) {
                int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, 0, 0, 100, default, 1.5f);
                Main.dust[d].noGravity = true;
            }
        }

        /// <summary>充能体节造成额外接触伤害 (体节即机制)。</summary>
        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) {
            if (Charging)
                modifiers.SourceDamage *= 1.5f;
        }

        /// <summary>确保蠕虫保持最小速度并使用宽转弯</summary>
        private void ApplyMovement(Vector2 targetPos, float baseSpeed, float turnRate, float minSpeed) {
            Vector2 toTarget = targetPos - NPC.Center;
            float distToTarget = toTarget.Length();

            Vector2 desiredDirection = toTarget.SafeNormalize(NPC.velocity.SafeNormalize(Vector2.UnitX));

            float currentAngle = NPC.velocity.ToRotation();
            float targetAngle = desiredDirection.ToRotation();
            float angleDiff = MathHelper.WrapAngle(targetAngle - currentAngle);

            float maxTurnPerFrame = turnRate;
            angleDiff = MathHelper.Clamp(angleDiff, -maxTurnPerFrame, maxTurnPerFrame);

            float newAngle = currentAngle + angleDiff;
            Vector2 newDirection = newAngle.ToRotationVector2();

            float targetSpeed = baseSpeed;
            if (distToTarget < 200f)
                targetSpeed = Math.Max(minSpeed, baseSpeed * 0.8f);

            float currentSpeed = NPC.velocity.Length();
            float newSpeed = MathHelper.Lerp(currentSpeed, targetSpeed, 0.05f);
            newSpeed = Math.Max(newSpeed, minSpeed);

            NPC.velocity = newDirection * newSpeed;
        }

        // ============================================================
        //  绘制
        // ============================================================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 origin = texture.Size() / 2;
            SpriteEffects effects = SpriteEffects.None;

            if (NPC.velocity.X < 0)
                effects = SpriteEffects.FlipVertically;

            // 发光层 (充能时金芒更盛)
            float glowMul = 0.35f + chargeVis * 0.5f;
            Color glowColor = Color.Gold * glowMul;
            glowColor.A = 0;
            spriteBatch.Draw(texture, NPC.Center - screenPos, null, glowColor, NPC.rotation, origin,
                NPC.scale * (1.08f + chargeVis * 0.05f), effects, 0f);

            // 本体
            spriteBatch.Draw(texture, NPC.Center - screenPos, null, drawColor, NPC.rotation, origin, NPC.scale, effects, 0f);

            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;

            if (NPCWormType == WormType.Head) {
                Texture2D sparkTex = ACMAsset.Sparkle;
                if (sparkTex != null) {
                    Color sparkColor = Color.Gold;
                    sparkColor.A = 0;
                    float pulseScale = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.15f;
                    spriteBatch.Draw(sparkTex, NPC.Center - screenPos, null, sparkColor * 0.2f,
                        Main.GlobalTimeWrappedHourly * 0.5f, sparkTex.Size() / 2f, NPC.scale * 0.35f * pulseScale, SpriteEffects.None, 0f);
                }

                // 敕令法标光束系带 (DrawBeam, 金=权威/安全; 标示"这些法标供给雷雨", 破标即断束)
                if ((int)NPC.ai[0] == StateEdict) {
                    int type = ModContent.ProjectileType<EdictBeacon>();
                    for (int i = 0; i < Main.maxProjectiles; i++) {
                        Projectile sp = Main.projectile[i];
                        if (!sp.active || sp.type != type || (int)sp.ai[1] != NPC.whoAmI)
                            continue;
                        ACMShaders.DrawBeam(NPC.Center, sp.Center, 10f, TelegraphColors.Gold, TelegraphColors.Holy, 0.7f, 1.4f, 2.0f);
                    }
                }

                // 金芒径向泛光 (DrawRadialBloomAt) - 蓄力/破标/过场瞬间
                if (bloomPulse > 0.02f)
                    ACMShaders.DrawRadialBloomAt(NPC.Center, 0.22f, MathHelper.Clamp(bloomPulse, 0f, 1f), TelegraphColors.Gold, 12f, 2.4f);
            }
            else if (chargeVis > 0.02f) {
                // 体节充能金脉冲
                Texture2D glow = ACMAsset.SoftGlow;
                if (glow != null) {
                    Color c = Color.Lerp(TelegraphColors.Gold, Color.White, chargeVis * 0.5f);
                    c.A = 0;
                    float sc = NPC.scale * (1.1f + chargeVis * 0.5f);
                    spriteBatch.Draw(glow, NPC.Center - screenPos, null, c * (0.5f * chargeVis), 0f, glow.Size() / 2f, sc, SpriteEffects.None, 0f);
                }
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (!Main.dedServ) {
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(3, 3);
                    int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (NPC.life <= 0 && NPCWormType == WormType.Head) {
                if (!Main.dedServ) {
                    for (int i = 0; i < 80; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(12, 12);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 2.5f);
                        Main.dust[dust].noGravity = true;
                    }
                    ACMUtils.AddScreenShake(16f);
                }
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Volume = 1.5f, Pitch = -0.5f }, NPC.Center);
            }
        }

        public override bool CheckActive() => false;

        public override void BossLoot(ref string name, ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }
    }
}
