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
    /// 白无常·谢必安 —— 阳·柔: 幽魂与引魂灯的飘忽控场使 (V3 重做)。
    /// 缠斗岗: W1 无常三迎 (锁线瞬步对穿 + 沿路休眠幽魂) / W2 引魂灯扑 (灯落充能 → 环波+径向)。
    /// 控场岗: W3 幽魂潮 (扇形波次) / W4 摄魂帷 (锚定结界, ArenaRunic) / W5 汲魂链 (P2, 可挣断)。
    /// 孤使: W6 百鬼引 (八灯环阵顺次点射)。
    /// 编排/协同/复活/死亡见 <see cref="BAWImpermanenceBase"/>。
    /// </summary>
    [AutoloadBossHead]
    public class WhiteImpermanence : BAWImpermanenceBase
    {
        protected override bool ConductorPriority => false;
        protected override int SideSign => 1;
        protected override string SoloAnnounceKey => "SoloRage";
        protected override int PartnerType => ModContent.NPCType<BlackImpermanence>();
        protected override int YinYangPressureInterval => 45;

        private static readonly SoundStyle GhostSound = SoundID.Item8 with { PitchVariance = 0.3f };
        private static readonly SoundStyle SpellSound = SoundID.Item73 with { Volume = 0.8f };

        /// <summary>本段瞬步方向 (视觉/判定共用)。</summary>
        private Vector2 dashDir = Vector2.UnitX;

        // —— 纯客户端视觉: 幡带 Verlet 飘带 + 浮沉呼吸 ——
        private BAWVerletChain streamer;
        private float bobPhase;

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
            NPC.lifeMax = 40000;
            NPC.aiStyle = -1;
            NPC.knockBackResist = 0f;
            NPC.damage = 50;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.netAlways = true;
            NPC.defense = 25;
            NPC.HitSound = SoundID.NPCHit54;
            NPC.DeathSound = SoundID.NPCDeath52;
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
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Ferryman>(), 2));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<DemonSoulStaff>(), 2));
        }

        public override void OnKill() {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                DownedBossSystem.downedWhiteImpermanence = true;
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 3; i++) {
                var d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.SpectreStaff);
                d.noGravity = true;
                d.velocity = new Vector2(hit.HitDirection * 2f, -1f) + Main.rand.NextVector2Circular(2f, 2f);
            }
        }

        #endregion

        #region 公平阀门与选招

        protected override bool ContactDamageActive =>
            State == DuetState.Attack && currentAttack == 0 && NPC.velocity.Length() > 20f;

        protected override int PickSupportAttack(int idx) {
            // P1: 幽魂潮 / 摄魂帷 交替; P2 起加入汲魂链三轮转
            if (!didP2)
                return 2 + idx % 2;
            return (idx % 3) switch {
                0 => 2,
                1 => 3,
                _ => 4
            };
        }

        #endregion

        #region 招式

        protected override void RunAttack(int id, Player target) {
            switch (id) {
                case 0: Attack_TripleFlicker(target); break;
                case 1: Attack_LanternPounce(target); break;
                case 2: Attack_GhostTide(target); break;
                case 3: Attack_SpiritVeil(target); break;
                case 4: Attack_SoulTether(target); break;
                case 5: Attack_HundredGhosts(target); break;
                default: EndAttack(); break;
            }

            // 保底出口
            if (StateTimer > 420f)
                EndAttack();
        }

        /// <summary>
        /// W1 无常三迎: 三段瞬步对穿 (走位移而非瞬移), 每段独立 24f 锁线预警,
        /// 沿路撒休眠幽魂 (45f 后才缓追)。接触伤害仅爆发帧。
        /// </summary>
        private void Attack_TripleFlicker(Player target) {
            float t = StateTimer;
            int seg = (int)SubState;

            if (t < 16f) {
                // 归位: 三段分别从左/右/上入线
                Vector2 slot = seg switch {
                    0 => target.Center + new Vector2(-540f, -70f),
                    1 => target.Center + new Vector2(540f, -70f),
                    _ => target.Center + new Vector2(0f, -560f)
                };
                SmoothFly(slot, 24f, 0.12f);
                FaceTarget(target);
                dashDir = (target.Center + target.velocity * 6f - NPC.Center).SafeNormalize(Vector2.UnitX);
            }
            else if (t < 24f) {
                // 锁线: 停驻读条
                if (t == 16f)
                    SoundEngine.PlaySound(BeepSound with { Pitch = 0.2f }, NPC.Center);
                NPC.velocity *= 0.8f;
                dashDir = (target.Center + target.velocity * 6f - NPC.Center).SafeNormalize(Vector2.UnitX);
            }
            else if (t == 24f) {
                // 瞬步对穿
                NPC.velocity = dashDir * 44f;
                SoundEngine.PlaySound(GhostSound with { Pitch = 0.3f }, NPC.Center);
                ACMScreenShakeSystem.Add(4f);
            }
            else if (t < 32f) {
                // 沿路撒休眠幽魂
                if (Main.netMode != NetmodeID.MultiplayerClient && (int)t % 2 == 0) {
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<GhostProjectile>(), 90, 0f, -1, 1f, 0f, NPC.whoAmI);
                    p.netUpdate = true;
                }
            }
            else if (t < 42f) {
                NPC.velocity *= 0.7f;
            }
            else if (t == 42f) {
                SubState++;
                if (SubState < 3f)
                    StateTimer = 0f;
            }
            else if (t > 64f) {
                EndAttack();
            }
            else {
                NPC.velocity *= 0.92f;
            }

            NPC.rotation = NPC.rotation.AngleLerp(Math.Clamp(NPC.velocity.X * 0.018f, -0.4f, 0.4f), 0.15f);
        }

        /// <summary>
        /// W2 引魂灯扑: 掷灯至玩家附近 (抛物可见) → 灯自理 60f 充能 (收缩环读条)
        /// → 爆: 幽魂环波 (4.5px/f 可跑赢) + 5 发径向幽魂 (72° 均布有缝)。
        /// </summary>
        private void Attack_LanternPounce(Player target) {
            float t = StateTimer;

            if (t < 10f) {
                SmoothFly(StrikerStance(target), 16f, 0.08f);
                FaceTarget(target);
            }
            else if (t == 10f) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 aim = target.Center + target.velocity * 20f;
                    Vector2 vel = (aim - NPC.Center).SafeNormalize(Vector2.UnitY) * 13f + new Vector2(0f, -6f);
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<SpiritCircleProjectile>(), 100, 0f, -1, 0f, 0f, NPC.whoAmI);
                    p.netUpdate = true;
                }
                SoundEngine.PlaySound(SpellSound, NPC.Center);
                NPC.velocity -= (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 4f; // 掷灯后坐
            }
            else if (t > 70f) {
                EndAttack();
            }
            else {
                // 侧移旁观
                SmoothFly(target.Center + new Vector2(SideSign * 500f, -260f), 11f, 0.05f);
            }

            NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.015f, 0.1f);
        }

        /// <summary>
        /// W3 幽魂潮 (控场): 26f 聚气 → 3 波 × 5(P2 6) 发 55° 扇形幽魂 (弱追踪可甩),
        /// 每波明确后坐 (重量感), 波间 26f 呼吸。
        /// </summary>
        private void Attack_GhostTide(Player target) {
            float t = StateTimer;
            int perWave = didP2 || Unleashed ? 6 : 5;

            if (t < 26f) {
                SmoothFly(SupportStance(target), 12f, 0.06f);
                FaceTarget(target);
                // 聚气收束粒子
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 from = NPC.Center + Main.rand.NextVector2CircularEdge(110f, 110f);
                    var d = Dust.NewDustPerfect(from, DustID.SpectreStaff);
                    d.noGravity = true;
                    d.scale = 1.3f;
                    d.velocity = (NPC.Center - from) * 0.08f;
                }
            }
            else if (t == 26f || t == 52f || t == 78f) {
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    float spread = MathHelper.ToRadians(55f);
                    for (int i = 0; i < perWave; i++) {
                        float ang = -spread * 0.5f + spread / (perWave - 1) * i;
                        var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), NPC.Center,
                            dir.RotatedBy(ang) * 8.5f, ModContent.ProjectileType<GhostProjectile>(),
                            95, 0f, -1, 0f, 0f, NPC.whoAmI);
                        p.netUpdate = true;
                    }
                }
                SoundEngine.PlaySound(SpellSound with { Pitch = (t - 26f) / 100f }, NPC.Center);
                NPC.velocity -= dir * 6f; // 波次后坐
            }
            else if (t > 112f) {
                EndAttack();
            }
            else {
                NPC.velocity *= 0.94f;
            }

            NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.015f, 0.1f);
        }

        /// <summary>
        /// W4 摄魂帷 (控场): 40f 展帷 (边界先亮) → 锚定玩家当前位的 420px 结界驻留 300f
        /// (域内 10% 减速 + 周期魂蚀); 不追人、出域即解 —— 替代旧"永久跟身减速场"。
        /// </summary>
        private void Attack_SpiritVeil(Player target) {
            float t = StateTimer;

            if (t < 40f) {
                SmoothFly(target.Center + new Vector2(SideSign * 560f, -320f), 12f, 0.06f);
                FaceTarget(target);
                if (t == 6f)
                    SoundEngine.PlaySound(GhostSound with { Pitch = -0.3f }, NPC.Center);
            }
            else if (t == 40f) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<SpiritCircleProjectile>(), 0, 0f, -1, 2f, 0f, NPC.whoAmI);
                    p.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f }, NPC.Center);
                ACMScreenShakeSystem.Add(4f);
            }
            else if (t > 74f) {
                EndAttack();
            }
            else {
                NPC.velocity *= 0.94f;
            }

            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.1f);
        }

        /// <summary>
        /// W5 汲魂链 (P2 控场): 缓速魂梭追踪 → 命中挂链 (叠魂蚀 + 白使回血);
        /// **挣断机制**: 拉开 >640px 即断 —— 反制即玩法。
        /// </summary>
        private void Attack_SoulTether(Player target) {
            float t = StateTimer;

            if (t < 24f) {
                SmoothFly(SupportStance(target), 12f, 0.06f);
                FaceTarget(target);
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 hand = NPC.Center + new Vector2(NPC.direction * 22f, -16f) * NPC.scale;
                    var d = Dust.NewDustPerfect(hand + Main.rand.NextVector2Circular(10f, 10f), DustID.SpectreStaff);
                    d.noGravity = true;
                    d.scale = 1.4f;
                    d.velocity = new Vector2(0f, -1f);
                }
            }
            else if (t == 24f) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), NPC.Center,
                        (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 12f,
                        ModContent.ProjectileType<SoulDrainProjectile>(), 80, 0f, -1, NPC.whoAmI);
                    p.netUpdate = true;
                }
                SoundEngine.PlaySound(SpellSound with { Pitch = -0.3f }, NPC.Center);
            }
            else if (t > 60f) {
                EndAttack();
            }
            else {
                NPC.velocity *= 0.95f;
            }

            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.1f);
        }

        /// <summary>
        /// W6 百鬼引 (孤使): 绕玩家 460px 八灯环阵错峰显形 → 顺时针依次锁线点射
        /// (矢向锁定显形线, 不追预判); 灯间距大, 环内恒有走位缝。
        /// </summary>
        private void Attack_HundredGhosts(Player target) {
            float t = StateTimer;

            if (t == 1f) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int lantern = ModContent.ProjectileType<SpiritCircleProjectile>();
                    for (int i = 0; i < 8; i++) {
                        float ang = MathHelper.TwoPi / 8f * i;
                        Vector2 pos = target.Center + ang.ToRotationVector2() * 460f;
                        var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                            lantern, 105, 0f, -1, 1f, i, NPC.whoAmI);
                        p.netUpdate = true;
                    }
                }
                SoundEngine.PlaySound(GhostSound with { Pitch = -0.5f, Volume = 1.1f }, NPC.Center);
            }
            else if (t == 60f) {
                // 环阵之间白使补一小扇 (维持自身存在感)
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = -1; i <= 1; i++) {
                        var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), NPC.Center,
                            dir.RotatedBy(i * 0.24f) * 7.5f, ModContent.ProjectileType<GhostProjectile>(),
                            95, 0f, -1, 0f, 0f, NPC.whoAmI);
                        p.netUpdate = true;
                    }
                }
                SoundEngine.PlaySound(SpellSound, NPC.Center);
            }
            else if (t > 200f) {
                EndAttack();
            }

            // 全程高位缓绕
            SmoothFly(target.Center + new Vector2(MathF.Sin(t * 0.02f) * 420f, -520f), 10f, 0.04f);
            NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.012f, 0.1f);
        }

        #endregion

        #region 阴阳勾魂压力 (阳域: 幽魂缓潮)

        protected override void SpawnYinYangPressure(Player target, Vector2 mid, Vector2 tangent, Vector2 myNormal, int beat) {
            // 阳域边缘生成慢速幽魂, 向安全缝方向缓推 (mode2 自动在缝前 150px 熄灭)
            int ghost = ModContent.ProjectileType<GhostProjectile>();
            float lateral = ((beat * 547) % 1300) - 650f; // 确定性伪随机横位
            Vector2 pos = mid + myNormal * 680f + tangent * lateral;
            var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), pos, -myNormal * 5f,
                ghost, 95, 0f, -1, 2f, 0f, NPC.whoAmI);
            p.timeLeft = 260;
            p.netUpdate = true;
        }

        #endregion

        #region 视觉

        protected override void PostAIVisuals(Player target) {
            Lighting.AddLight(NPC.Center, new Color(200, 200, 255).ToVector3() * 0.5f);

            if (Main.dedServ)
                return;

            bobPhase += 0.045f;

            // 幡带 Verlet 飘带: 轻质 (低重力) + 侧风摆动
            streamer ??= new BAWVerletChain(8, 13f, NPC.Center) { Gravity = 0.14f, Damping = 0.97f };
            Vector2 hand = NPC.Center + new Vector2(-20f * NPC.spriteDirection, -34f).RotatedBy(NPC.rotation) * NPC.scale;
            streamer.ApplyImpulse(4, new Vector2(MathF.Sin(bobPhase * 1.7f) * 0.14f, 0f));
            streamer.Step(hand);

            float targetAura = State switch {
                DuetState.Attack when currentAttack == 0 => 0.85f,
                DuetState.SynergyYinYang or DuetState.SynergyChainLock => 0.8f,
                DuetState.SoloTransform or DuetState.DeathAnim => 1f,
                _ => Unleashed ? 0.7f : 0.5f
            };
            auraIntensity = MathHelper.Lerp(auraIntensity, targetAura, 0.05f);

            if (Main.rand.NextBool(9)) {
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(46f, 60f), DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1f;
                d.velocity = new Vector2(0f, -1.4f);
            }
        }

        public override bool PreDraw(SpriteBatch sb, Vector2 scrPos, Color col) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Rectangle rec = NPC.frame;
            SpriteEffects spe = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Vector2 origin = rec.Size() * 0.5f;
            float speed = NPC.velocity.Length();

            // 浮沉呼吸 (纯绘制偏移, 不碰物理)
            Vector2 bob = new(0f, MathF.Sin(bobPhase) * 6f);
            Vector2 drawPos = NPC.Center + bob - scrPos;

            // 体表魂焰罩
            if (drawAlpha > 0.3f && auraIntensity > 0.05f) {
                Color edge = Unleashed ? Color.Lerp(BAWFX.YangColor, new Color(80, 60, 120), 0.35f) : BAWFX.YangColor;
                BAWFX.DrawSoulFlame(sb, NPC.Center + bob - new Vector2(0f, 14f), new Vector2(150f, 200f) * NPC.scale,
                    new Color(235, 240, 255), edge, NPC.whoAmI * 0.77f + 5f, auraIntensity * 0.7f * drawAlpha, 0f, 1.15f);
            }

            // 瞬步残影 (仅爆发帧)
            if (speed > 20f) {
                Color tail = Unleashed ? new Color(120, 100, 160) : new Color(190, 210, 255);
                tail.A = 0;
                for (int i = NPC.oldPos.Length - 1; i >= 1; i--) {
                    float k = 1f - i / (float)NPC.oldPos.Length;
                    Vector2 pos = NPC.oldPos[i] + NPC.Size * 0.5f - scrPos;
                    sb.Draw(tex, pos, rec, tail * (k * 0.45f * drawAlpha), NPC.oldRot[i], origin, NPC.scale * (0.94f + k * 0.06f), spe, 0);
                }
                Texture2D shot = ACMAsset.LightShot;
                if (shot != null) {
                    Color streak = new(220, 230, 255, 0);
                    sb.Draw(shot, NPC.Center - scrPos, null, streak * 0.55f, NPC.velocity.ToRotation(),
                        shot.Size() * 0.5f, new Vector2(speed * 0.035f, 0.7f), SpriteEffects.None, 0);
                }
            }

            // 幡带 (本体之下, 白绫)
            if (streamer != null && drawAlpha > 0.25f) {
                Texture2D glowTex = ACMAsset.SoftGlow;
                if (glowTex != null) {
                    for (int i = 0; i < streamer.Count - 1; i++) {
                        float k = 1f - i / (float)streamer.Count;
                        Vector2 a = streamer.Pos[i];
                        Vector2 b = streamer.Pos[i + 1];
                        Vector2 seg = b - a;
                        Color c = new Color(240, 244, 255, 0) * (0.5f * k * drawAlpha);
                        sb.Draw(glowTex, a - scrPos, null, c, seg.ToRotation(),
                            glowTex.Size() * 0.5f, new Vector2(seg.Length() / glowTex.Width * 1.4f, 0.16f + 0.1f * k), SpriteEffects.None, 0);
                    }
                }
            }

            // 本体
            float dissolve = ComputeDissolve();
            Color body = col; body.A = 255;
            if (!(dissolve > 0.02f && BAWFX.DrawDissolveSprite(sb, tex, drawPos, rec, body,
                    NPC.rotation, origin, NPC.scale, spe, dissolve, BAWFX.WhiteDissolveEdge))) {
                sb.Draw(tex, drawPos, rec, col * drawAlpha, NPC.rotation, origin, NPC.scale, spe, 0);
            }

            // 外发光 (孤使泛黑蕊)
            Color glow = Unleashed ? new Color(90, 70, 130) : new Color(200, 200, 255);
            glow.A = 0;
            sb.Draw(tex, drawPos, rec, glow * 0.45f * drawAlpha, NPC.rotation, origin, NPC.scale * 1.08f, spe, 0);

            return false;
        }

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

            // W1 瞬步锁线预警
            if (State == DuetState.Attack && currentAttack == 0 && StateTimer >= 16f && StateTimer < 24f) {
                float k = Utils.GetLerpValue(16f, 24f, StateTimer, true);
                Color c = Color.Lerp(BAWFX.YangColor, TelegraphColors.Lethal, k);
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + dashDir * 1400f, 9f + k * 7f,
                    c, c * 0.4f, 0.35f + k * 0.5f);
            }

            // 死亡魂焰柱
            if (soulPillar > 0.03f) {
                BAWFX.DrawSoulFlame(sb, NPC.Center - new Vector2(0f, 150f), new Vector2(150f, 440f),
                    Color.White, BAWFX.YangColor, 7.7f, soulPillar, 0f, 2.6f);
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
