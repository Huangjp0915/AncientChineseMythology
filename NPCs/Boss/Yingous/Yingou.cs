using AncientChineseMythology.Items.Weapons.Bosses;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;
using static AncientChineseMythology.Projectiles.RuyiStickSpearProjectile_3;

namespace AncientChineseMythology.NPCs.Boss.Yingous
{
    [AutoloadBossHead]
    internal class Yingou : ModNPC
    {
        //====== 新阶段系统 ======
        public enum BossPhase
        {
            Intro,
            PatternSetA,   //基础挥砍 + 火球散射
            SpiralDread,   //螺旋+环绕压迫
            SaberHell,     //大刀地狱(扩展演出)
            RecoverDash,   //回收冲刺（过渡）
        }

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float LocalTimer => ref NPC.ai[2];
        public float PhaseLoopCounter; //计数阶段循环次数

        public AttackAIStyle aitype = AttackAIStyle.Melee; //保留给手部的模式提示
        public enum AttackAIStyle { Idle, Melee, Wave, Circle }

        public int seed = -1;
        public Random random = null;
        public bool spawnHands = true;
        public float circleCounter = 0;
        public float circlespeed = 0;
        internal ref int swordDir => ref otherAI[3];
        private readonly int[] otherAI = new int[aiSlot];
        private const int aiSlot = 4;
        public static int ReelBackTime => Main.masterMode ? 50 : 60;

        //视觉演出参数
        private float introAppear; //0-1 出场插值
        private float spiralPulse; //螺旋脉冲
        private float saberCharge; //大刀地狱充能
        private bool didIntroShock;

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(0, 10000);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
            }
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            LocalTimer = 0;
            introAppear = 0;
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 110;
            NPC.height = 110;
            NPC.damage = 66;
            NPC.defense = 40;
            NPC.lifeMax = 420000;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.Roar;
            NPC.value = 20000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Yingou");
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YingouKnife>()));
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write((int)Phase);
            writer.Write(introAppear);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            Phase = (BossPhase)reader.ReadInt32();
            introAppear = reader.ReadSingle();
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        private void TransitionTo(BossPhase next) {
            Phase = next;
            PhaseTimer = 0;
            LocalTimer = 0;
            NPC.netUpdate = true;
        }

        public override void AI() {
            random ??= new Random(seed);
            if (spawnHands) {
                spawnHands = false;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<YingouHand>(), 0, NPC.whoAmI, 1);
                    NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<YingouHand>(), 0, NPC.whoAmI, -1);
                }
            }

            if (!VaultUtils.isServer && !SkyManager.Instance[YingouSky.name].IsActive()) {
                SkyManager.Instance.Activate(YingouSky.name);
            }

            if (swordDir == 0) swordDir = 1;

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.Alives()) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.Alives()) {
                    NPC.velocity *= 0.98f;
                    return;
                }
            }

            PhaseTimer++;
            LocalTimer++;

            switch (Phase) {
                case BossPhase.Intro:
                    RunIntro(target);
                    break;
                case BossPhase.PatternSetA:
                    RunPatternSetA(target);
                    break;
                case BossPhase.SpiralDread:
                    RunSpiral(target);
                    break;
                case BossPhase.SaberHell:
                    RunSaberHell(target);
                    break;
                case BossPhase.RecoverDash:
                    RunRecoverDash(target);
                    break;
            }
        }

        private void RunIntro(Player target) {
            //出场缓动：从远处扭曲漂移进入
            introAppear = ACMUtils.SineInOut(MathHelper.Clamp(PhaseTimer / 120f, 0, 1));
            Vector2 appearOffset = new Vector2(0, -600).RotatedBy(MathHelper.ToRadians(PhaseTimer * 2));
            Vector2 desired = target.Center + appearOffset * (1 - introAppear) + Vector2.Lerp(new Vector2(-300, -200), new Vector2(0, -120), ACMUtils.QuadOut(introAppear));
            NPC.Center += (desired - NPC.Center) * 0.12f;
            NPC.velocity *= 0.8f;

            //扭曲粒子
            if (!VaultUtils.isServer && PhaseTimer % 4 == 0) {
                for (int i = 0; i < 6; i++) {
                    Vector2 off = Main.rand.NextVector2CircularEdge(80, 80) * (1 - introAppear);
                    int dust = Dust.NewDust(NPC.Center + off, 0, 0, DustID.GoldFlame, 0, 0, 150, default, Main.rand.NextFloat(1.2f, 2.6f));
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -off.SafeNormalize(Vector2.Zero) * 2f + Main.rand.NextVector2Circular(1, 1);
                }
            }

            //屏幕聚焦 + 震动落点
            if (!didIntroShock && introAppear > 0.92f) {
                didIntroShock = true;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(10, 40);
                for (int k = 0; k < 40; k++) {
                    Vector2 vel = Main.rand.NextVector2Circular(12, 12);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Torch, vel.X, vel.Y, 120, default, 2.2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (PhaseTimer > 150) {
                aitype = AttackAIStyle.Melee;
                TransitionTo(BossPhase.PatternSetA);
            }
        }

        private void RunPatternSetA(Player target) {
            //移动：缓慢侧滑逼近 + 偶发腾挪
            Vector2 baseDir = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
            Vector2 lateral = baseDir.RotatedBy(MathHelper.PiOver2 * swordDir) * MathF.Sin(PhaseTimer * 0.04f) * 6f;
            Vector2 desiredVel = baseDir * 10 + lateral;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVel, 0.05f);

            //每段循环内放出一次扇形火球
            if (PhaseTimer % 120 == 60) {
                DoFanFire(target, 9 + (Main.expertMode ? 3 : 0) + (Main.masterMode ? 4 : 0), 70, 18f, 22f);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 18);
            }

            //经过时间转向螺旋阶段
            if (PhaseTimer > 600) {
                TransitionTo(BossPhase.SpiralDread);
                aitype = AttackAIStyle.Circle;
                circleCounter = 0;
                circlespeed = 0;
            }
        }

        private void DoFanFire(Player target, int fireballCount, float totalSpreadDeg, float minSpeed, float maxSpeed) {
            if (VaultUtils.isClient) return;
            float spread = MathHelper.ToRadians(totalSpreadDeg);
            float baseAngle = NPC.DirectionTo(target.Center).ToRotation();
            for (int i = 0; i < fireballCount; i++) {
                float angleOffset = MathHelper.Lerp(-spread / 2, spread / 2, i / (float)(fireballCount - 1));
                float speed = Main.rand.NextFloat(minSpeed, maxSpeed);
                Vector2 velocity = baseAngle.ToRotationVector2().RotatedBy(angleOffset) * speed;
                float power = i * 0.15f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, velocity,
                    ModContent.ProjectileType<YingouFireBall>(), GetBossDamage(), 2f, Main.myPlayer, 0, 0, power);
            }
        }

        private void RunSpiral(Player target) {
            spiralPulse += 0.03f;
            circlespeed = MathHelper.Lerp(circlespeed, 1.4f, 0.01f);
            circleCounter += circlespeed * 0.16f;

            float radius = 1380 + MathF.Sin(spiralPulse * 2) * 90f * ACMUtils.SineInOut(MathF.Sin(spiralPulse));
            Vector2 dest = target.Center + (circleCounter * swordDir).ToRotationVector2() * radius;
            NPC.Center += (dest - NPC.Center) * 0.08f;
            NPC.velocity *= 0.8f;

            if (PhaseTimer % 90 == 20) {
                DoRadialPulseProjectiles(10 + (Main.expertMode ? 4 : 0));
            }
            if (PhaseTimer % 150 == 80) {
                DoTrackingArcFire(target, 6, 46f);
            }

            if (PhaseTimer > 540) {
                TransitionTo(BossPhase.SaberHell);
                aitype = AttackAIStyle.Idle;
                saberCharge = 0;
            }
        }

        private void DoRadialPulseProjectiles(int count) {
            if (VaultUtils.isClient) return;
            for (int i = 0; i < count; i++) {
                float ang = MathHelper.TwoPi * i / count;
                Vector2 vel = ang.ToRotationVector2() * 14f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<YingouFireBall>(), GetBossDamage(0.7f), 2f, Main.myPlayer, 0, 1, Main.rand.NextFloat(1f));
            }
        }

        private void DoTrackingArcFire(Player target, int arcCount, float arcRadius) {
            if (VaultUtils.isClient) return;
            Vector2 baseDir = NPC.DirectionTo(target.Center);
            for (int a = 0; a < arcCount; a++) {
                float t = a / (float)(arcCount - 1);
                float ang = MathHelper.Lerp(-0.8f, 0.8f, t);
                Vector2 offset = baseDir.RotatedBy(ang) * arcRadius;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + offset, (target.Center - (NPC.Center + offset)).SafeNormalize(Vector2.Zero) * 16f,
                    ModContent.ProjectileType<YingouFireBall>(), GetBossDamage(0.8f), 2f, Main.myPlayer, 0, 0, t);
            }
        }

        private void RunSaberHell(Player target) {
            //蓄力 -> 连续多段释放
            saberCharge = MathHelper.Clamp(saberCharge + 0.012f, 0, 1);
            NPC.velocity *= 0.9f;
            Vector2 hover = target.Center + new Vector2(0, -320 + MathF.Sin(PhaseTimer * 0.05f) * 30);
            NPC.Center += (hover - NPC.Center) * 0.05f;

            //充能粒子
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Vector2 off = Main.rand.NextVector2CircularEdge(160, 160) * saberCharge;
                    int dust = Dust.NewDust(NPC.Center + off, 0, 0, DustID.PurpleTorch, 0, 0, 120, default, Main.rand.NextFloat(1.6f, 2.7f));
                    Main.dust[dust].velocity = -off.SafeNormalize(Vector2.Zero) * 4f * Main.rand.NextFloat(0.4f, 1f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (PhaseTimer == 120) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 36);
            }

            if (PhaseTimer > 140 && PhaseTimer % 50 == 0) {
                PerformSaberPattern(target);
            }

            if (PhaseTimer > 420) {
                TransitionTo(BossPhase.RecoverDash);
                aitype = AttackAIStyle.Melee;
                swordDir *= -1;
            }
        }

        private void PerformSaberPattern(Player target) {
            if (VaultUtils.isClient) return;
            Vector2 basePos = target.Center;
            for (int ring = 0; ring < 2; ring++) {
                int slice = 6 + ring * 2;
                for (int i = 0; i < slice; i++) {
                    float ang = MathHelper.TwoPi * i / slice + ring * 0.15f;
                    Vector2 dir = ang.ToRotationVector2();
                    Vector2 spawn = basePos + dir * (260 + ring * 80);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, -dir * 10,
                        ModContent.ProjectileType<SaberHell>(), GetBossDamage(0.9f), 2);
                }
            }
        }

        private void RunRecoverDash(Player target) {
            //强力冲刺 + 回到 PatternSetA
            if (PhaseTimer == 1) {
                Vector2 dashDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY).RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f));
                NPC.velocity = dashDir * 30f;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 24);
            }
            NPC.velocity *= 0.985f;
            if (PhaseTimer > 40) {
                PhaseLoopCounter++;
                if (PhaseLoopCounter % 2 == 1) {
                    TransitionTo(BossPhase.PatternSetA);
                    aitype = AttackAIStyle.Melee;
                }
                else {
                    TransitionTo(BossPhase.SpiralDread);
                    aitype = AttackAIStyle.Circle;
                    circleCounter = 0;
                    circlespeed = 0;
                }
            }
        }

        private int GetBossDamage(float scaling = 1f, bool getOrigDamage = false) {
            int num = getOrigDamage ? NPC.defDamage : NPC.damage;
            return (int)(num * scaling);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[NPC.type].Value;
            float sengs = 0.25f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(mainValue, drawOldPos, null, drawColor * sengs, 0, mainValue.Size() / 2, NPC.scale * (0.9f + 0.1f * sengs), SpriteEffects.None, 0);
                sengs *= 0.75f;
            }
            float introScale = Phase == BossPhase.Intro ? MathHelper.Lerp(0.6f, 1f, ACMUtils.BackOut(introAppear)) : 1f;
            Main.EntitySpriteDraw(mainValue, NPC.Center - Main.screenPosition, null, drawColor, NPC.rotation, mainValue.Size() / 2, NPC.scale * introScale, SpriteEffects.None);
            return false;
        }
    }

    internal class YingouFireBall : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.timeLeft = 220;
            Projectile.tileCollide = false;
        }

        public static void KillAll() {
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != ModContent.ProjectileType<YingouFireBall>()) continue;
                proj.Kill();
                proj.netUpdate = true;
            }
        }

        public override void AI() {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 6; i++) {
                    int dustType = Main.rand.NextBool(2) ? DustID.Torch : DustID.Shadowflame;
                    if (Projectile.ai[1] == 1f) dustType = DustID.Torch;
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                        dustType, Projectile.velocity.X / 2, Projectile.velocity.Y / 2, 150,
                        default, Main.rand.NextFloat(1f, 3.5f));
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity *= 0.6f;
                }
            }

            Projectile.ai[0]++;
            if (Projectile.ai[0] < 80) { //螺旋阶段
                float jitter = (float)Math.Sin(Projectile.ai[0] * 0.3f) * 0.1f;
                Projectile.velocity = Projectile.velocity.RotatedBy((0.025f + jitter) * Projectile.ai[2]);
            }
            else if (Projectile.ai[0] == 80) { //脉冲
                Projectile.velocity *= 0.3f;
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 30; i++) {
                        Vector2 offset = Main.rand.NextVector2Circular(1f, 1f) * 40f;
                        int dust = Dust.NewDust(Projectile.Center + offset, 0, 0,
                            DustID.PurpleTorch, 0f, 0f, 0, default, 2f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity = offset.SafeNormalize(Vector2.Zero) * 4f;
                    }
                }
            }
            else { //追踪
                Player player = Projectile.Center.FindClosestPlayer(3200, true);
                if (player != null) {
                    float speedFactor = 1.2f + 0.3f * (float)Math.Sin(Projectile.ai[0] * 0.15f);
                    Vector2 targetSpeed = Projectile.SafeDirectionTo(player.Center) * Projectile.velocity.Length() * speedFactor;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetSpeed, 0.05f);
                }
            }
        }
    }

    internal class SaberHell : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Projectile.velocity = Projectile.velocity.UnitVector();
            if (Projectile.localAI[0] < 40) {
                if (Projectile.localAI[0] == 0) Projectile.localAI[1] = 30;
                Projectile.localAI[0]++;
                if (Projectile.localAI[0] == 40) {
                    int num = 1000;
                    int num2 = 36;
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(),
                        Projectile.Center + Projectile.velocity * num, Projectile.velocity * -num2,
                        ModContent.ProjectileType<SaberKiller>(), Projectile.damage, Projectile.knockBack,
                        Main.myPlayer, Projectile.Center.X, Projectile.Center.Y);
                    Projectile.velocity *= -1;
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(),
                        Projectile.Center + Projectile.velocity * num, Projectile.velocity * -num2,
                        ModContent.ProjectileType<SaberKiller>(), Projectile.damage, Projectile.knockBack,
                        Main.myPlayer, Projectile.Center.X, Projectile.Center.Y);
                }
            }
            else {
                if (Projectile.localAI[1] > 0) Projectile.localAI[1]--;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D back = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            int width = 4400;
            int height = (int)(Projectile.localAI[0] * 3);
            float alpha = Projectile.localAI[1] / 60f;
            Rectangle rect = new Rectangle(-width / 2, -height / 2, width, height);
            Vector2 origin = new Vector2(rect.Width / 2, rect.Height / 2);
            Color drawColor = VaultUtils.MultiStepColorLerp(Projectile.localAI[0] / 40f, Color.Azure, Color.Red);
            Main.spriteBatch.Draw(back, drawPos, rect, drawColor with { A = 155 } * alpha,
                Projectile.velocity.ToRotation(), origin, 1f, SpriteEffects.None, 0f);
            return false;
        }
    }

    internal class SaberKiller : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Yingous/YingouHand";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 3;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 84;
            Projectile.tileCollide = false;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.timeLeft = 360;
            Projectile.alpha = 255;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 1;
        }
        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Vector2 targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
            if (targetPos.Distance(Projectile.Center) < 120) {
                Projectile.velocity /= 2f;
                if (Projectile.ai[2] == 0) {
                    SoundEngine.PlaySound(SoundID.Item89, targetPos);
                    for (int i = 0; i < 115; i++) {
                        Vector2 sparkPos = targetPos + Main.rand.NextVector2Circular(60, 60);
                        int dust = Dust.NewDust(sparkPos, 0, 0, DustID.Torch, 0, 0);
                        Main.dust[dust].velocity = Main.rand.NextVector2Circular(6, 6) * 1.5f;
                        Main.dust[dust].scale = Main.rand.NextFloat(1.2f, 3f);
                        Main.dust[dust].noGravity = true;
                    }
                }
                Projectile.ai[2] = 1f;
            }
            if (Projectile.ai[2] == 1f) {
                Projectile.alpha -= 5;
                if (Projectile.alpha <= 0f) Projectile.Kill();
                Projectile.alpha = (int)MathHelper.Clamp(Projectile.alpha, 0, 255);
            }
        }
        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Color drawColor = Color.White * (Projectile.alpha / 255f);
            float sengs = 0.3f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                Main.spriteBatch.Draw(value, oldPos, null, drawColor * sengs,
                    Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
                sengs *= 0.9f;
            }
            Main.spriteBatch.Draw(value, Projectile.Center - Main.screenPosition, null, drawColor,
                Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
