using AncientChineseMythology.Systems;
using AncientChineseMythology.Underworlds.Boss.BAWImpermanences.Items;
using AncientChineseMythology.Underworlds.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences
{
    /// <summary>
    /// 黑无常·范无救 —— 阴·刚: 锁链与镰的近身压迫使 (V3 重做)。
    /// 缠斗岗: B1 锁镰双绞 (瞬发冲刺 + 随体绞镰) / B2 拘魂十字锁 (四向合拢快照笼)。
    /// 控场岗: B3 垂链帘 (Verlet 锁链帘幕) / B4 火签令 (掷签 → 地涌锁链柱)。
    /// 孤使: B5 葬列冲 (teleport-loop 五段对穿 + 延迟链柱)。
    /// 编排/协同/复活/死亡见 <see cref="BAWImpermanenceBase"/>。
    /// </summary>
    [AutoloadBossHead]
    public class BlackImpermanence : BAWImpermanenceBase
    {
        protected override bool ConductorPriority => true;
        protected override int SideSign => -1;
        protected override string SoloAnnounceKey => "SoloRage";
        protected override int PartnerType => ModContent.NPCType<WhiteImpermanence>();
        protected override int YinYangPressureInterval => 50;

        /// <summary>本段冲刺方向 (视觉/发射共用, 各端各自演算)。</summary>
        private Vector2 dashDir = Vector2.UnitX;

        // —— 纯客户端视觉: 腰间双垂链 (次级运动 = 重量感) ——
        private BAWVerletChain[] idleChains;

        #region 初始化

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.TrailCacheLength[Type] = 10;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 70;
            NPC.height = 100;
            NPC.lifeMax = 45000;
            NPC.aiStyle = -1;
            NPC.knockBackResist = 0f;
            NPC.damage = 60;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.netAlways = true;
            NPC.defense = 30;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = Item.buyPrice(0, 8, 0, 0);
            NPC.scale = 1.5f;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.3f);
                NPC.damage = (int)(NPC.damage * 1.2f);
            }
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.HealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<ImpermanenceSoul>(), 1, 2, 4));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<DemonicAnnihilation>(), 2));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<NetherworldSickle>(), 2));
        }

        public override void OnKill() {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                DownedBossSystem.downedBlackImpermanence = true;
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 3; i++) {
                var d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Shadowflame);
                d.noGravity = true;
                d.velocity = new Vector2(hit.HitDirection * 2f, -1f) + Main.rand.NextVector2Circular(2f, 2f);
            }
        }

        #endregion

        #region 公平阀门

        protected override bool ContactDamageActive =>
            State == DuetState.Attack &&
            ((currentAttack == 0 && NPC.velocity.Length() > 16f) ||
             (currentAttack == 5 && NPC.velocity.Length() > 20f));

        #endregion

        #region 招式

        protected override void RunAttack(int id, Player target) {
            switch (id) {
                case 0: Attack_ChainFlailDash(target); break;
                case 1: Attack_CrossLock(target); break;
                case 2: Attack_ChainCurtain(target); break;
                case 3: Attack_FireTally(target); break;
                case 5: Attack_FuneralRush(target); break;
                default: EndAttack(); break;
            }

            // 保底出口: 任何招式 420f 内必须收尾
            if (StateTimer > 420f)
                EndAttack();
        }

        /// <summary>
        /// B1 锁镰双绞: 固定 36f 预警拍 → 瞬发冲刺 (launch is a set) + 复利加速 → 硬刹。
        /// P1 两段 / P2 三段; 首段挂出两把随体绞镰 (ChainSweepProjectile)。
        /// </summary>
        private void Attack_ChainFlailDash(Player target) {
            int dashCount = didP2 || Unleashed ? 3 : 2;
            float t = StateTimer;

            if (t < 30f) {
                // 前摇: 移向侧位, 移动力衰减 (慢启动阀门)
                if (t == 2f)
                    SoundEngine.PlaySound(BeepSound, NPC.Center);
                Vector2 stance = StrikerStance(target);
                SmoothFly(stance, 15f, 0.07f);
                NPC.velocity *= 0.965f;
                FaceTarget(target);
                dashDir = (target.Center + target.velocity * 10f - NPC.Center).SafeNormalize(Vector2.UnitX);
            }
            else if (t < 38f) {
                // 末 8 帧 pow8 反向抽身 —— 冲刺前的"吸气"
                float k = (t - 30f) / 8f;
                dashDir = (target.Center + target.velocity * 10f - NPC.Center).SafeNormalize(Vector2.UnitX);
                NPC.velocity = Vector2.Lerp(NPC.velocity, -dashDir * MathF.Pow(k, 8f) * 14f, 0.4f);
            }
            else if (t == 38f) {
                // 瞬发
                NPC.velocity = dashDir * 34f;
                SoundEngine.PlaySound(DashSound, NPC.Center);
                ACMScreenShakeSystem.Add(5f);

                // 首段挂出双绞镰 (随体旋转, 全程随冲刺)
                if (SubState == 0f && Main.netMode != NetmodeID.MultiplayerClient) {
                    int flail = ModContent.ProjectileType<ChainSweepProjectile>();
                    for (int i = 0; i < 2; i++) {
                        var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            flail, 100, 1f, -1, NPC.whoAmI, i * MathHelper.Pi);
                        p.timeLeft = 40 + (dashCount - (int)SubState) * 72;
                        p.netUpdate = true;
                    }
                }

                if (!Main.dedServ) {
                    for (int i = 0; i < 10; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center, DustID.Shadowflame);
                        d.noGravity = true;
                        d.scale = 1.8f;
                        d.velocity = -dashDir.RotatedByRandom(0.6f) * Main.rand.NextFloat(3f, 9f);
                    }
                }
            }
            else if (t < 60f) {
                // 冲刺: 复利加速; 冲过目标即提前进入刹车 (不等表)
                NPC.velocity *= 1.02f;
                bool passed = Vector2.Dot(target.Center - NPC.Center, dashDir) < -220f;
                if (passed)
                    StateTimer = 60f;
            }
            else if (t < 72f) {
                // 硬刹
                NPC.velocity *= 0.66f;
            }
            else if (t == 72f) {
                SubState++;
                if (SubState < dashCount)
                    StateTimer = 1f; // 直接回到预警拍起点 (固定 36f 节拍可学习)
            }
            else if (t > 92f) {
                NPC.velocity *= 0.9f;
                EndAttack();
            }
            else {
                NPC.velocity *= 0.9f;
            }

            NPC.rotation = NPC.rotation.AngleLerp(Math.Clamp(NPC.velocity.X * 0.02f, -0.45f, 0.45f), 0.14f);
        }

        /// <summary>
        /// B2 拘魂十字锁: 45f 盘链读条 → 四向锁链向玩家快照位合拢 (链弹幕自带 25f 红线预警),
        /// 象限 90° 缝恒在。P2 后追加一发拘魂锁 (ChainPullProjectile) 穿缝试探。
        /// </summary>
        private void Attack_CrossLock(Player target) {
            float t = StateTimer;

            if (t < 45f) {
                if (t == 4f)
                    SoundEngine.PlaySound(ChargeSound, NPC.Center);
                // 上撤 + 移动力衰减读条
                SmoothFly(target.Center + new Vector2(0f, -480f), 13f, 0.06f);
                NPC.velocity *= 0.96f;
                FaceTarget(target);

                // 盘链聚气: 锁链粒子绕身收拢
                if (!Main.dedServ && (int)t % 2 == 0) {
                    float ang = t * 0.35f;
                    Vector2 pos = NPC.Center + ang.ToRotationVector2() * (150f - t * 2.4f);
                    var d = Dust.NewDustPerfect(pos, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.5f;
                    d.velocity = (NPC.Center - pos) * 0.06f;
                }
            }
            else if (t == 45f) {
                // 四向锁链 (快照围笼; 各链自带显形→合拢时序)
                SoundEngine.PlaySound(ChainSound with { Volume = 1f, Pitch = -0.2f }, NPC.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int chain = ModContent.ProjectileType<ChainProjectile>();
                    Vector2 snap = target.Center;
                    float baseAng = MathHelper.PiOver4; // X 形: 缝在正上下左右
                    for (int i = 0; i < 4; i++) {
                        float ang = baseAng + MathHelper.PiOver2 * i;
                        Vector2 from = snap + ang.ToRotationVector2() * 640f;
                        var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), from, Vector2.Zero,
                            chain, 110, 0f, -1, 2f, ang, NPC.whoAmI);
                        p.netUpdate = true;
                    }
                }
            }
            else if (t < 150f) {
                // 收势旁观 + P2 拘魂锁穿缝试探
                SmoothFly(target.Center + new Vector2(0f, -520f), 10f, 0.05f);
                if (t == 92f && didP2 && Main.netMode != NetmodeID.MultiplayerClient) {
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), NPC.Center,
                        (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 13f,
                        ModContent.ProjectileType<ChainPullProjectile>(), 90, 0f, -1, NPC.whoAmI);
                    p.netUpdate = true;
                    SoundEngine.PlaySound(ChainSound, NPC.Center);
                }
            }
            else {
                EndAttack();
            }

            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.1f);
        }

        /// <summary>
        /// B3 垂链帘 (控场): 玩家上空横列 5~6 条 Verlet 垂链, 相位错摆 + 缓降; 帘作为区域压力
        /// 持续存在, 黑无常本体早退回连接拍 (支援岗不抢戏)。
        /// </summary>
        private void Attack_ChainCurtain(Player target) {
            float t = StateTimer;

            if (t < 40f) {
                SmoothFly(target.Center + new Vector2(0f, -560f), 15f, 0.07f);
                if (t == 10f)
                    SoundEngine.PlaySound(ChainSound with { Pitch = -0.4f }, NPC.Center);
                FaceTarget(target);
            }
            else if (t == 40f) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int chain = ModContent.ProjectileType<ChainProjectile>();
                    int count = didP2 || Unleashed ? 6 : 5;
                    float span = (count - 1) * 250f;
                    for (int i = 0; i < count; i++) {
                        Vector2 anchor = new(target.Center.X - span * 0.5f + i * 250f, target.Center.Y - 430f);
                        var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), anchor, Vector2.Zero,
                            chain, 95, 0f, -1, 1f, i, NPC.whoAmI);
                        p.netUpdate = true;
                    }
                }
                ACMScreenShakeSystem.Add(4f);
            }
            else if (t > 80f) {
                EndAttack();
            }
            else {
                NPC.velocity *= 0.94f;
            }

            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.1f);
        }

        /// <summary>
        /// B4 火签令 (控场): 举签 30f (红闪+预警音) → 顺掷 3 支火签 (抛物, 落点红圈 25f)
        /// → 各签位置地涌锁链柱。签距 ≥220px, 恒有走位缝。
        /// </summary>
        private void Attack_FireTally(Player target) {
            float t = StateTimer;

            if (t < 30f) {
                SmoothFly(SupportStance(target), 13f, 0.06f);
                FaceTarget(target);
                if (t == 8f)
                    SoundEngine.PlaySound(BeepSound with { Pitch = 0.1f }, NPC.Center);
                // 举签红闪
                if (!Main.dedServ && Main.rand.NextBool(3)) {
                    Vector2 hand = NPC.Center + new Vector2(NPC.direction * 26f, -30f) * NPC.scale;
                    var d = Dust.NewDustPerfect(hand, DustID.Torch);
                    d.noGravity = true;
                    d.scale = 1.6f;
                    d.velocity = new Vector2(0f, -1.5f);
                }
            }
            else if (t == 30f || t == 42f || t == 54f) {
                int k = ((int)t - 30) / 12 - 1; // -1, 0, 1
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 aim = target.Center + new Vector2(k * 240f + target.velocity.X * 18f, 0f);
                    Vector2 vel = (aim - NPC.Center).SafeNormalize(Vector2.UnitY) * 15f + new Vector2(0f, -5f);
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<ChainProjectile>(), 100, 0f, -1, 3f, 0f, NPC.whoAmI);
                    p.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.3f }, NPC.Center);
                // 掷签后坐
                NPC.velocity -= (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 3f;
            }
            else if (t > 100f) {
                EndAttack();
            }
            else {
                NPC.velocity *= 0.95f;
            }

            NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.015f, 0.1f);
        }

        /// <summary>
        /// B5 葬列冲 (孤使): 五段对穿冲刺, teleport-loop 删除回程死时间 (blink 双端有遮掩),
        /// 每段中点延迟 30f 地涌链柱 (落点先标)。每段独立 20f 预警线。
        /// </summary>
        private void Attack_FuneralRush(Player target) {
            const int segCount = 5;
            float t = StateTimer;

            if (t < 20f) {
                // 段预警: 锁向 + 减速漂移
                if (t == 2f)
                    SoundEngine.PlaySound(BeepSound with { Pitch = -0.2f + (float)SubState * 0.08f }, NPC.Center);
                NPC.velocity *= 0.88f;
                dashDir = (target.Center + target.velocity * 8f - NPC.Center).SafeNormalize(Vector2.UnitX);
                FaceTarget(target);
            }
            else if (t == 20f) {
                NPC.velocity = dashDir * 40f;
                SoundEngine.PlaySound(DashSound with { Pitch = 0.2f }, NPC.Center);
                ACMScreenShakeSystem.Add(5f);

                // 冲线中点延迟链柱 (mode4 自带 30f 落点预警)
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 midPoint = (NPC.Center + target.Center) * 0.5f + new Vector2(0f, 60f);
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), midPoint, Vector2.Zero,
                        ModContent.ProjectileType<ChainProjectile>(), 90, 0f, -1, 4f, 30f, NPC.whoAmI);
                    p.netUpdate = true;
                }
            }
            else if (t < 46f) {
                // 冲过 700px 即 blink 至远侧 (双端粒子遮掩)
                if (Vector2.Dot(target.Center - NPC.Center, dashDir) < -700f || t == 45f) {
                    SpawnBlinkBurst(NPC.Center);
                    float ang = dashDir.ToRotation() + MathHelper.Pi + ((int)SubState % 2 == 0 ? 0.55f : -0.55f);
                    NPC.Center = target.Center + ang.ToRotationVector2() * 620f;
                    NPC.velocity = Vector2.Zero;
                    SpawnBlinkBurst(NPC.Center);
                    NPC.netUpdate = true;

                    SubState++;
                    if (SubState >= segCount) {
                        StateTimer = 60f;
                    }
                    else {
                        StateTimer = 0f;
                    }
                }
            }
            else if (t == 62f) {
                SoundEngine.PlaySound(RoarSound with { Pitch = -0.1f }, NPC.Center);
                ACMScreenShakeSystem.Add(6f);
            }
            else if (t > 84f) {
                EndAttack();
            }
            else {
                NPC.velocity *= 0.9f;
            }

            NPC.rotation = NPC.rotation.AngleLerp(Math.Clamp(NPC.velocity.X * 0.02f, -0.5f, 0.5f), 0.16f);
        }

        private static void SpawnBlinkBurst(Vector2 pos) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 12; i++) {
                var d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(20f, 34f), DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.7f;
                d.velocity = Main.rand.NextVector2Circular(6f, 6f);
            }
        }

        #endregion

        #region 阴阳勾魂压力 (阴域: 垂链缓降)

        protected override void SpawnYinYangPressure(Player target, Vector2 mid, Vector2 tangent, Vector2 myNormal, int beat) {
            // 阴域内随机横位垂链 (快降变体, 自动避让安全缝)
            int chain = ModContent.ProjectileType<ChainProjectile>();
            float lateral = ((beat * 733) % 1300) - 650f; // 确定性伪随机横位
            Vector2 anchor = mid + myNormal * (280f + (beat * 271) % 420) + tangent * lateral - new Vector2(0f, 0f);
            anchor.Y = target.Center.Y - 500f;
            var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), anchor, Vector2.Zero,
                chain, 95, 0f, -1, 1f, 100 + beat % 5, NPC.whoAmI);
            p.timeLeft = 240;
            p.netUpdate = true;
        }

        #endregion

        #region 视觉

        protected override void PostAIVisuals(Player target) {
            Lighting.AddLight(NPC.Center, new Color(60, 45, 110).ToVector3() * 0.6f);

            if (Main.dedServ)
                return;

            // 腰链 Verlet: 身体每一次急动都让链条甩尾 (惯性自带)
            idleChains ??= new BAWVerletChain[] {
                new(7, 11f, NPC.Center),
                new(6, 10f, NPC.Center)
            };
            Vector2 waistL = NPC.Center + new Vector2(-16f * NPC.spriteDirection, 20f).RotatedBy(NPC.rotation) * NPC.scale;
            Vector2 waistR = NPC.Center + new Vector2(9f * NPC.spriteDirection, 24f).RotatedBy(NPC.rotation) * NPC.scale;
            idleChains[0].Step(waistL);
            idleChains[1].Step(waistR);

            // 体表魂焰强度按状态呼吸
            float targetAura = State switch {
                DuetState.Attack when currentAttack == 0 || currentAttack == 5 => 0.85f,
                DuetState.SynergyYinYang or DuetState.SynergyChainLock => 0.8f,
                DuetState.SoloTransform or DuetState.DeathAnim => 1f,
                _ => Unleashed ? 0.7f : 0.5f
            };
            auraIntensity = MathHelper.Lerp(auraIntensity, targetAura, 0.05f);

            // 环境魂点
            if (Main.rand.NextBool(9)) {
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(46f, 60f), DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1f;
                d.velocity = new Vector2(0f, -1.2f);
            }
        }

        public override bool PreDraw(SpriteBatch sb, Vector2 scrPos, Color col) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Rectangle rec = NPC.frame;
            SpriteEffects spe = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Vector2 origin = rec.Size() * 0.5f;
            float speed = NPC.velocity.Length();

            // 体表魂焰罩 (程序魂火, 本体背后)
            if (drawAlpha > 0.3f && auraIntensity > 0.05f) {
                Color edge = Unleashed ? Color.Lerp(BAWFX.YinColor, Color.White, 0.45f) : BAWFX.YinColor;
                BAWFX.DrawSoulFlame(sb, NPC.Center - new Vector2(0f, 14f), new Vector2(150f, 200f) * NPC.scale,
                    new Color(60, 40, 120), edge, NPC.whoAmI * 0.77f, auraIntensity * 0.8f * drawAlpha, 0f, 1.15f);
            }

            // 冲刺残影 (仅爆发帧, 速度门控)
            if (speed > 16f) {
                Color tail = Unleashed ? new Color(200, 190, 230) : new Color(90, 70, 140);
                tail.A = 0;
                for (int i = NPC.oldPos.Length - 1; i >= 1; i--) {
                    float k = 1f - i / (float)NPC.oldPos.Length;
                    Vector2 pos = NPC.oldPos[i] + NPC.Size * 0.5f - scrPos;
                    sb.Draw(tex, pos, rec, tail * (k * 0.4f * drawAlpha), NPC.oldRot[i], origin, NPC.scale * (0.94f + k * 0.06f), spe, 0);
                }
                // 速度流线
                Texture2D shot = ACMAsset.LightShot;
                if (shot != null) {
                    Color streak = new(150, 110, 220, 0);
                    sb.Draw(shot, NPC.Center - scrPos, null, streak * 0.5f, NPC.velocity.ToRotation(),
                        shot.Size() * 0.5f, new Vector2(speed * 0.035f, 0.7f), SpriteEffects.None, 0);
                }
            }

            // 腰链 (本体之下)
            if (idleChains != null && drawAlpha > 0.25f) {
                foreach (var c in idleChains)
                    BAWHelper.DrawVerletChain(sb, c, new Color(48, 44, 62), new Color(120, 90, 200), 0.62f, drawAlpha);
            }

            // 本体: 演出期间 DissolveBurn, 平时普通绘制
            float dissolve = ComputeDissolve();
            Color body = col; body.A = 255;
            if (!(dissolve > 0.02f && BAWFX.DrawDissolveSprite(sb, tex, NPC.Center - scrPos, rec, body,
                    NPC.rotation, origin, NPC.scale, spe, dissolve, BAWFX.BlackDissolveEdge))) {
                sb.Draw(tex, NPC.Center - scrPos, rec, col * drawAlpha, NPC.rotation, origin, NPC.scale, spe, 0);
            }

            // 外发光 (孤使镶白边)
            Color glow = Unleashed ? new Color(220, 220, 240) : new Color(40, 34, 66);
            glow.A = 0;
            sb.Draw(tex, NPC.Center - scrPos, rec, glow * (Unleashed ? 0.5f : 0.4f) * drawAlpha, NPC.rotation,
                origin, NPC.scale * 1.07f, spe, 0);

            return false;
        }

        /// <summary>演出溶解包络 (0=实体)。</summary>
        private float ComputeDissolve() {
            float t = StateTimer;
            return State switch {
                DuetState.Intro or DuetState.BeingRevived => 1f - drawAlpha,
                DuetState.SoloTransform when t > 60f && t < 140f =>
                    MathF.Abs(MathF.Sin(t * 0.11f)) * 0.45f * Utils.GetLerpValue(140f, 100f, t, true),
                DuetState.DeathAnim when t > 24f && t < 150f =>
                    0.12f + 0.14f * MathF.Sin(t * 0.3f) + 0.25f * Utils.GetLerpValue(100f, 150f, t, true),
                DuetState.DeathAnim when t >= 150f => 1f - drawAlpha,
                _ => 1f - drawAlpha
            };
        }

        public override void PostDraw(SpriteBatch sb, Vector2 scrPos, Color col) {
            // 阴阳分屏 (双使任一调用, 内部帧守卫 + 全屏名额)
            BAWFX.DrawYinYangSplit(sb);

            // B1/B5 冲刺预警线
            if (State == DuetState.Attack && (currentAttack == 0 || currentAttack == 5)) {
                float t = StateTimer;
                float telegraphEnd = currentAttack == 0 ? 38f : 20f;
                float telegraphStart = currentAttack == 0 ? 8f : 2f;
                if (t >= telegraphStart && t < telegraphEnd) {
                    float k = Utils.GetLerpValue(telegraphStart, telegraphEnd, t, true);
                    Color c = Color.Lerp(BAWFX.YinColor, TelegraphColors.Lethal, k);
                    ACMShaders.DrawBeam(NPC.Center, NPC.Center + dashDir * 1500f, 10f + k * 8f,
                        c, c * 0.4f, 0.35f + k * 0.5f);
                }
            }

            // 死亡魂焰柱
            if (soulPillar > 0.03f) {
                BAWFX.DrawSoulFlame(sb, NPC.Center - new Vector2(0f, 150f), new Vector2(150f, 440f),
                    new Color(120, 90, 220), BAWFX.YinColor, 3.1f, soulPillar, 0f, 2.6f);
            }

            // 冲击帧/交错白闪
            if (whiteFlash > 0.02f) {
                Texture2D px = TextureAssets.MagicPixel.Value;
                sb.Draw(px, new Rectangle(-200, -200, Main.screenWidth + 400, Main.screenHeight + 400),
                    Color.White * whiteFlash);
            }
        }

        #endregion
    }
}
