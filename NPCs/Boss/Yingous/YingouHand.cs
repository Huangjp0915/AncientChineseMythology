using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using static AncientChineseMythology.Projectiles.RuyiStickSpearProjectile_3;

namespace AncientChineseMythology.NPCs.Boss.Yingous
{
    internal class YingouHand : ModNPC
    {
        [VaultLoaden("AncientChineseMythology/NPCs/Boss/Yingous/")]
        private static Asset<Texture2D> SwordSlashTexture;
        public List<Vector2> oldPos = new();
        public List<float> oldRots = new();
        public float trailOffset = 0;
        public int attackCd = 0;
        public Player handPlayer = null;
        public int handPlayerTime = 0;
        public int counter1 = 6;
        public float circleDist = 0;
        public bool circle = false;

        //===== 新挥舞参数 =====
        private int swingState; //0=空闲 1=预挥准备 2=挥舞中 3=收招
        private float swingProgress; //0-1
        private int swingDuration;
        private Vector2 swingStart;
        private Vector2 swingPivot;
        private Vector2 swingEnd;
        private float impactFlash;

        // ===== FrenzyDash 专用参数 =====
        private int frenzyDashHandState; // 0=空闲 1=展开 2=冲刺挥砍 3=收招
        private float frenzyDashProgress; // 0-1 当前动作进度
        private float frenzyDashTargetAngle; // 目标角度（相对于Boss->Player方向）
        private float frenzyDashCurrentAngle; // 当前角度
        private float frenzySlashFlash; // 斩击闪光

        public float swingAngle; //保留（旧）
        public float swingPhase; //保留（旧）

        public int Direction {
            get => (int)NPC.ai[1];
            set => NPC.ai[1] = value;
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.NPCBestiaryDrawModifiers nPCBestiaryDrawModifiers = new();
            nPCBestiaryDrawModifiers.Hide = true;
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = nPCBestiaryDrawModifiers;
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.TrailCacheLength[Type] = 18; //加长拖尾
        }

        public override void SetDefaults() {
            NPC.width = 76;
            NPC.height = 76;
            NPC.damage = 0;
            NPC.defense = 60;
            NPC.lifeMax = 60000;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCHit1;
            NPC.value = 20000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
            NPC.dontTakeDamage = true;
        }

        public static Rectangle getRectCentered(Vector2 center, float w, float h) => new((int)(center.X - w / 2), (int)(center.Y - h / 2), (int)w, (int)h);
        public static float getDistance(Vector2 v1, Vector2 v2) => Vector2.Distance(v1, v2);

        public override bool ModifyCollisionData(Rectangle victimHitbox, ref int immunityCooldownSlot, ref MultipliableFloat damageMultiplier, ref Rectangle npcHitbox) {
            npcHitbox = getRectCentered(NPC.Center + NPC.rotation.ToRotationVector2() * 120 * NPC.scale, NPC.width * NPC.scale, NPC.height * NPC.scale);
            return true;
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => handPlayerTime <= 0 && swingState == 2;

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            if (handPlayerTime <= 0) {
                handPlayer = target;
                handPlayerTime = 8;
            }
        }

        public override void AI() {
            if (counter1-- > 0) return;

            NPC boss = Main.npc[(int)NPC.ai[0]];
            if (!boss.Alives() || boss.ModNPC is not Yingou yBoss) {
                NPC.active = false;
                NPC.netUpdate = true;
                return;
            }
            Player target = Main.player[boss.target];

            NPC.realLife = boss.whoAmI;
            NPC.target = boss.target;

            //根据 Boss 当前模式决定手部逻辑
            var phase = (Yingou.BossPhase)(int)boss.ai[0];

            switch (phase) {
                case Yingou.BossPhase.Intro:
                    DoIntroOrbit(boss, target, yBoss);
                    break;
                case Yingou.BossPhase.PatternSetA:
                    DoMeleeSwingSystem(boss, target, yBoss);
                    break;
                case Yingou.BossPhase.SpiralDread:
                    DoSpiralAssist(boss, target, yBoss);
                    break;
                case Yingou.BossPhase.SaberHell:
                    DoSaberChargePose(boss, target, yBoss);
                    break;
                case Yingou.BossPhase.FrenzyDash:
                    DoFrenzyDashBlades(boss, target, yBoss);
                    break;
                case Yingou.BossPhase.BladeScatter:
                    DoBladeScatterPose(boss, target, yBoss);
                    break;
                case Yingou.BossPhase.RecoverDash:
                    DoRecoverFollow(boss, target, yBoss);
                    break;
            }

            //绑住玩家逻辑
            if (handPlayerTime > 0 && handPlayer != null) {
                handPlayer.Center = NPC.Center + NPC.rotation.ToRotationVector2() * 86;
                handPlayer.velocity *= 0f;
                handPlayerTime--;
                if (handPlayerTime == 0) {
                    handPlayer.velocity = (boss.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 20f;
                }
            }

            //防止离散
            if (getDistance(boss.Center, NPC.Center) > 4800)
                NPC.Center = boss.Center;

            oldPos.Add(NPC.Center);
            oldRots.Add(NPC.rotation);
            if (oldPos.Count > 30) { oldPos.RemoveAt(0); oldRots.RemoveAt(0); }
        }

        private void DoIntroOrbit(NPC boss, Player target, Yingou yBoss) {
            float t = MathHelper.Clamp(boss.ai[1] / 120f, 0, 1);
            float radius = MathHelper.Lerp(420, 140, ACMUtils.SineInOut(t));
            float ang = (boss.ai[1] * 0.05f + (Direction > 0 ? 0 : MathHelper.Pi)) * (Direction > 0 ? 1 : -1);
            Vector2 desired = boss.Center + ang.ToRotationVector2() * radius;
            NPC.Center += (desired - NPC.Center) * 0.18f;
            NPC.rotation = (NPC.Center - boss.Center).ToRotation();
        }

        private void DoMeleeSwingSystem(NPC boss, Player target, Yingou yBoss) {
            //若未在挥舞则尝试进入新挥舞
            if (swingState == 0) {
                if (attackCd-- <= 0) {
                    swingState = 1;
                    swingProgress = 0;
                    swingDuration = Main.rand.Next(46, 58);

                    swingStart = NPC.Center;
                    Vector2 toTarget = (target.Center - boss.Center).SafeNormalize(Vector2.UnitX);
                    float side = Direction; //左右手镜像
                    swingPivot = boss.Center + toTarget.RotatedBy(side * 0.9f) * 220 + new Vector2(0, -60 * side);
                    swingEnd = target.Center + toTarget.RotatedBy(-side * 0.7f) * 280 + new Vector2(0, 40 * side);

                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1f, PitchVariance = 0.15f }, NPC.Center);
                }
                else {
                    //Idle 跟随
                    Vector2 idleOffset = new Vector2(Direction * 120, -40).RotatedBy(MathF.Sin(Main.GameUpdateCount * 0.04f) * 0.2f * Direction);
                    Vector2 desired = boss.Center + idleOffset;
                    NPC.Center += (desired - NPC.Center) * 0.18f;
                    NPC.rotation = (NPC.Center - boss.Center).ToRotation();
                }
            }
            else if (swingState == 1) //预挥 telegraph
            {
                swingProgress += 1f / (swingDuration * 0.38f);
                float t = MathHelper.Clamp(swingProgress, 0, 1);
                float ease = ACMUtils.QuadOut(t);
                Vector2 teleOffset = Vector2.Lerp(swingStart, swingPivot, ease);
                NPC.Center += (teleOffset - NPC.Center) * 0.55f;
                NPC.rotation = (teleOffset - boss.Center).ToRotation();
                SpawnSlashWind(t * 0.4f);
                if (t >= 1f) {
                    swingState = 2;
                    swingProgress = 0;
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.1f, Volume = 1.1f }, NPC.Center);
                }
            }
            else if (swingState == 2) //挥舞主体
            {
                swingProgress += 1f / (swingDuration * 0.62f);
                float t = MathHelper.Clamp(swingProgress, 0, 1);
                float arcEase = ACMUtils.SineInOut(t);
                Vector2 pos = ACMUtils.BezierQuad(swingPivot, Vector2.Lerp(swingPivot, swingEnd, 0.5f) + new Vector2(0, Direction * 120), swingEnd, arcEase);
                NPC.Center += (pos - NPC.Center) * 0.7f;
                Vector2 tangent = (swingEnd - swingPivot).SafeNormalize(Vector2.UnitX).RotatedBy(Direction * (1 - t) * 0.9f);
                NPC.rotation = tangent.ToRotation();
                SpawnSlashWind(0.4f + t * 0.6f);

                if (t > 0.85f && impactFlash == 0) {
                    impactFlash = 1;
                    ImpactEffects();
                }
                if (t >= 1f) {
                    swingState = 3;
                    swingProgress = 0;
                    attackCd = Main.rand.Next(40, 70);
                }
            }
            else if (swingState == 3) //收招
            {
                swingProgress += 0.08f;
                float t = ACMUtils.QuadOut(MathHelper.Clamp(swingProgress, 0, 1));
                Vector2 restPos = boss.Center + new Vector2(Direction * 140, -40);
                NPC.Center += (restPos - NPC.Center) * 0.25f;
                NPC.rotation = (NPC.Center - boss.Center).ToRotation();
                if (swingProgress >= 1) {
                    swingState = 0;
                    impactFlash = 0;
                }
            }
        }

        private void ImpactEffects() {
            //冲击粒子 + 震动
            Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(7, 20);
            SoundEngine.PlaySound(SoundID.Item89 with { Volume = 1.1f, Pitch = -0.3f }, NPC.Center);
            for (int i = 0; i < 40; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8, 8);
                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 120, default, Main.rand.NextFloat(1.4f, 2.4f));
                Main.dust[dust].noGravity = true;
            }
        }

        private void SpawnSlashWind(float power) {
            if (Main.rand.NextFloat() < 0.3f && !VaultUtils.isServer) {
                Vector2 off = NPC.rotation.ToRotationVector2() * Main.rand.NextFloat(40, 160) * NPC.scale;
                int dust = Dust.NewDust(NPC.Center + off, 0, 0, DustID.Torch, 0, 0, 140, default, 1.2f + power * 0.8f);
                Main.dust[dust].velocity = NPC.rotation.ToRotationVector2().RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * 6f;
                Main.dust[dust].noGravity = true;
            }
        }

        private void DoSpiralAssist(NPC boss, Player target, Yingou yBoss) {
            float angBase = yBoss.circleCounter * yBoss.swordDir + (Direction > 0 ? 0 : MathHelper.Pi);
            float radius = 260 + MathF.Sin(Main.GameUpdateCount * 0.05f + Direction) * 40;
            Vector2 desired = boss.Center + angBase.ToRotationVector2() * radius;
            NPC.Center += (desired - NPC.Center) * 0.25f;
            NPC.rotation = (NPC.Center - boss.Center).ToRotation();
            //间隔发射辅助火球
            if (Main.GameUpdateCount % 40 == 0 && !VaultUtils.isClient) {
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir * 14f,
                    ModContent.ProjectileType<YingouFireBall>(), boss.damage / 2, 2f);
            }
        }

        private void DoSaberChargePose(NPC boss, Player target, Yingou yBoss) {
            float t = MathF.Sin(boss.ai[1] * 0.05f) * 0.5f + 0.5f;
            Vector2 baseOffset = new Vector2(Direction * 160, -120 + t * 30);
            Vector2 desired = boss.Center + baseOffset;
            NPC.Center += (desired - NPC.Center) * 0.18f;
            NPC.rotation = (NPC.Center - boss.Center).ToRotation();
            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.PurpleTorch, 0, 0, 140, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }

        private void DoFrenzyDashBlades(NPC boss, Player target, Yingou yBoss) {
            // 获取Boss的冲刺状态信息（通过反射或访问公开字段）
            var bossYingou = boss.ModNPC as Yingou;
            if (bossYingou == null) return;

            // 通过访问Boss的AI变量推断当前冲刺状态
            int bossFrenzyState = GetBossFrenzyState(boss);
            int bossFrenzyStateTimer = GetBossFrenzyStateTimer(boss);

            Vector2 bossToTarget = (target.Center - boss.Center).SafeNormalize(Vector2.UnitX);
            float baseAngleToTarget = bossToTarget.ToRotation();

            switch (bossFrenzyState) {
                case 0: // Telegraph 展开阶段
                    HandleFrenzyTelegraph(boss, target, baseAngleToTarget, bossFrenzyStateTimer);
                    break;
                case 1: // Dash 收束斩击阶段
                    HandleFrenzyDash(boss, target, baseAngleToTarget, bossFrenzyStateTimer);
                    break;
                case 2: // Recover 收招阶段
                    HandleFrenzyRecover(boss, target, baseAngleToTarget, bossFrenzyStateTimer);
                    break;
            }

            // 防止离散
            if (getDistance(boss.Center, NPC.Center) > 600) {
                Vector2 clampPos = boss.Center + (NPC.Center - boss.Center).SafeNormalize(Vector2.Zero) * 600;
                NPC.Center = clampPos;
            }
        }

        private void HandleFrenzyTelegraph(NPC boss, Player target, float baseAngle, int stateTimer) {
            if (frenzyDashHandState != 1) {
                // 进入展开状态
                frenzyDashHandState = 1;
                frenzyDashProgress = 0;
                // 设置展开目标角度：左手-90°，右手+90°
                frenzyDashTargetAngle = baseAngle + (Direction > 0 ? MathHelper.PiOver2 : -MathHelper.PiOver2);
                frenzyDashCurrentAngle = (NPC.Center - boss.Center).ToRotation();
            }

            // 展开动画：使用ElasticOut缓动，营造刀刃张力感
            frenzyDashProgress = MathHelper.Clamp(stateTimer / 32f, 0, 1);
            float easeT = ACMUtils.ElasticOut(frenzyDashProgress);
            
            // 角度插值
            float targetAngle = LerpAngle(frenzyDashCurrentAngle, frenzyDashTargetAngle, easeT);
            
            // 距离随展开程度变化：向外扩展
            float expandRadius = MathHelper.Lerp(120, 200, ACMUtils.SineInOut(frenzyDashProgress));
            Vector2 desiredPos = boss.Center + targetAngle.ToRotationVector2() * expandRadius;
            
            NPC.Center += (desiredPos - NPC.Center) * 0.25f;
            NPC.rotation = targetAngle;

            // 展开过程的气流粒子
            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                Vector2 sparkPos = NPC.Center + NPC.rotation.ToRotationVector2() * Main.rand.NextFloat(60, 120);
                int dust = Dust.NewDust(sparkPos, 0, 0, DustID.GoldFlame, 0, 0, 140, default, 1.4f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = NPC.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2 * Direction) * 3f;
            }
        }

        private void HandleFrenzyDash(NPC boss, Player target, float baseAngle, int stateTimer) {
            if (frenzyDashHandState != 2) {
                // 进入冲刺斩击状态
                frenzyDashHandState = 2;
                frenzyDashProgress = 0;
                frenzyDashCurrentAngle = NPC.rotation;
                frenzyDashTargetAngle = baseAngle; // 目标：Boss到玩家的方向
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.2f, Pitch = 0.3f }, NPC.Center);
            }

            // 快速收束斩击：使用QuadIn突然加速
            frenzyDashProgress = MathHelper.Clamp(stateTimer / 20f, 0, 1);
            float slashEase = ACMUtils.QuadIn(frenzyDashProgress);
            
            // 角度快速插值到目标
            float currentAngle = LerpAngle(frenzyDashCurrentAngle, frenzyDashTargetAngle, slashEase);
            
            // 距离在斩击中先突进再稍微回拉
            float dashRadius = MathHelper.Lerp(200, 140, ACMUtils.SineInOut(frenzyDashProgress));
            if (frenzyDashProgress > 0.7f) {
                dashRadius += 30f * (float)Math.Sin((frenzyDashProgress - 0.7f) * MathHelper.Pi / 0.3f); // 斩击突进
            }
            
            Vector2 desiredPos = boss.Center + currentAngle.ToRotationVector2() * dashRadius;
            NPC.Center += (desiredPos - NPC.Center) * 0.65f;
            NPC.rotation = currentAngle;

            // 斩击冲击效果
            if (frenzyDashProgress > 0.6f && frenzySlashFlash == 0) {
                frenzySlashFlash = 1f;
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(5, 12);
                SoundEngine.PlaySound(SoundID.Item89 with { Volume = 1f, Pitch = -0.2f }, NPC.Center);
                
                // 斩击光效与粒子爆发
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 25; i++) {
                        Vector2 vel = NPC.rotation.ToRotationVector2().RotatedByRandom(0.8f) * Main.rand.NextFloat(8, 16);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Torch, vel.X, vel.Y, 120, default, Main.rand.NextFloat(1.8f, 2.8f));
                        Main.dust[dust].noGravity = true;
                    }
                    
                    // 发射几个斩击波
                    if (!VaultUtils.isClient && Direction > 0) { // 只让右手发射避免重复
                        for (int w = 0; w < 3; w++) {
                            Vector2 waveVel = currentAngle.ToRotationVector2().RotatedBy(MathHelper.Lerp(-0.3f, 0.3f, w / 2f)) * 20f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, waveVel,
                                ModContent.ProjectileType<YingouFireBall>(), boss.damage / 3, 1f, Main.myPlayer, 0, 1, Main.rand.NextFloat(0.8f));
                        }
                    }
                }
            }
        }

        private void HandleFrenzyRecover(NPC boss, Player target, float baseAngle, int stateTimer) {
            if (frenzyDashHandState != 3) {
                // 进入收招状态
                frenzyDashHandState = 3;
                frenzyDashProgress = 0;
                frenzyDashCurrentAngle = NPC.rotation;
            }

            // 收招：回到休息位置，使用BackOut缓动
            frenzyDashProgress = MathHelper.Clamp(stateTimer / 18f, 0, 1);
            float recoverEase = ACMUtils.BackOut(frenzyDashProgress);
            
            Vector2 restOffset = new Vector2(Direction * 120, -40);
            Vector2 restPos = boss.Center + restOffset;
            float restAngle = (restPos - boss.Center).ToRotation();
            
            float currentAngle = LerpAngle(frenzyDashCurrentAngle, restAngle, recoverEase);
            NPC.Center += (restPos - NPC.Center) * (0.15f + recoverEase * 0.2f);
            NPC.rotation = currentAngle;

            // 重置状态
            if (frenzyDashProgress >= 1f) {
                frenzyDashHandState = 0;
                frenzySlashFlash = 0;
            }
        }

        // 角度插值辅助函数
        private float LerpAngle(float from, float to, float t) {
            float diff = to - from;
            while (diff > MathHelper.Pi) diff -= MathHelper.TwoPi;
            while (diff < -MathHelper.Pi) diff += MathHelper.TwoPi;
            return from + diff * t;
        }

        // 推断Boss的冲刺状态（通过AI变量模式识别）
        private int GetBossFrenzyState(NPC boss) {
            var yingou = boss.ModNPC as Yingou;
            return yingou?.FrenzyDashState ?? 0;
        }

        private int GetBossFrenzyStateTimer(NPC boss) {
            var yingou = boss.ModNPC as Yingou;
            return yingou?.FrenzyDashStateTimer ?? 0;
        }

        private void DoRecoverFollow(NPC boss, Player target, Yingou yBoss) {
            Vector2 desired = boss.Center + new Vector2(Direction * 130, -50);
            NPC.Center += (desired - NPC.Center) * 0.3f;
            NPC.rotation = (NPC.Center - boss.Center).ToRotation();
        }

        // ====== BladeScatter 蓄力姿态 ======
        private void DoBladeScatterPose(NPC boss, Player target, Yingou yBoss) {
            float chargeProgress = MathHelper.Clamp(boss.ai[1] / 90f, 0, 1);
            
            // 蓄力时双刀向上举起并向外张开
            float chargeAngle = MathHelper.Lerp(-MathHelper.PiOver4, -MathHelper.PiOver2 - 0.3f, ACMUtils.SineInOut(chargeProgress));
            chargeAngle += Direction * 0.4f; // 左右手差异
            
            float chargeRadius = MathHelper.Lerp(140, 180, chargeProgress);
            Vector2 chargePos = boss.Center + chargeAngle.ToRotationVector2() * chargeRadius;
            
            NPC.Center += (chargePos - NPC.Center) * 0.2f;
            NPC.rotation = chargeAngle;

            // 蓄力粒子效果
            if (!VaultUtils.isServer && Main.rand.NextBool(6)) {
                Vector2 sparkPos = NPC.Center + Main.rand.NextVector2Circular(30, 30);
                int dust = Dust.NewDust(sparkPos, 0, 0, DustID.GoldFlame, 0, 0, 140, default, 1.2f + chargeProgress);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (boss.Center - sparkPos).SafeNormalize(Vector2.Zero) * 2f;
            }
        }

        public override bool CheckActive() => false;

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            trailOffset += 0.06f;
            SpriteBatch sb = Main.spriteBatch;
            var gd = Main.graphics.GraphicsDevice;

            //切换到加色绘制拖尾
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            void DrawTrail(Texture2D texture, Color startColor, Color endColor, float widen = 1f) {
                List<ColoredVertex> vertices = new();
                int count = oldRots.Count;
                for (int i = 0; i < count; i++) {
                    float t = i / (float)count;
                    Color c = Color.Lerp(startColor * 0.05f, endColor, t) * 1f;
                    Vector2 basePos = oldPos[i] - Main.screenPosition;
                    Vector2 rotVec = (oldRots[i] + (Direction > 0 ? MathHelper.ToRadians(18) : MathHelper.ToRadians(-18))).ToRotationVector2();
                    float scaleFactor = 1 - t;
                    float offset1 = 16 + 220 * NPC.scale * scaleFactor * 0.5f * widen;
                    float offset2 = 16 + 220 * NPC.scale - 60 * NPC.scale * scaleFactor * 0.5f * widen;
                    vertices.Add(new ColoredVertex(basePos + rotVec * offset1, new Vector3(t + trailOffset, 1, 1), c));
                    vertices.Add(new ColoredVertex(basePos + rotVec * offset2, new Vector3(t + trailOffset, 0, 1), c));
                }
                if (vertices.Count >= 3) {
                    gd.Textures[0] = texture;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
                }
            }

            //主体拖尾层次
            DrawTrail(VaultAsset.placeholder2.Value, Color.OrangeRed, Color.Red, impactFlash > 0 ? 1.4f : 1f);
            DrawTrail(SwordSlashTexture.Value, Color.White, Color.White);

            // FrenzyDash 特殊拖尾效果
            if (frenzySlashFlash > 0) {
                Color flashColor = Color.Lerp(Color.Gold, Color.White, frenzySlashFlash);
                DrawTrail(VaultAsset.placeholder2.Value, flashColor, flashColor * 0.6f, 1.8f);
                frenzySlashFlash *= 0.85f;
                if (frenzySlashFlash < 0.05f) frenzySlashFlash = 0;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = Direction > 0 ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
            float rotation = NPC.rotation + (Direction > 0 ? MathHelper.ToRadians(18) : MathHelper.ToRadians(-18 + 180));
            SpriteEffects effects = Direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            float sengs = 0.22f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                float rot = NPC.oldRot[i] + (Direction > 0 ? MathHelper.ToRadians(18) : MathHelper.ToRadians(-18 + 180));
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                Main.EntitySpriteDraw(tex, drawOldPos, null, Color.White * sengs, rot, origin, NPC.scale * (sengs + 0.8f), effects);
                sengs *= 0.9f;
            }

            //斩击冲击高亮 + FrenzyDash特殊效果
            float totalFlash = Math.Max(impactFlash, frenzySlashFlash);
            Color bodyColor = drawColor * (totalFlash > 0 ? (1.3f + totalFlash * 0.4f) : 1f);
            if (frenzySlashFlash > 0) {
                bodyColor = Color.Lerp(bodyColor, Color.Gold, frenzySlashFlash * 0.7f);
            }
            Main.EntitySpriteDraw(tex, NPC.Center - Main.screenPosition, null, bodyColor, rotation, origin, NPC.scale, effects);

            if (impactFlash > 0) {
                impactFlash *= 0.9f;
                if (impactFlash < 0.05f) impactFlash = 0;
            }

            return false;
        }
    }
}
