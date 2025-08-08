using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hoqings
{
    [AutoloadBossHead]
    [VaultLoaden("AncientChineseMythology/NPCs/Boss/Hoqings/")]
    internal class Hoqing : ModNPC
    {
        private int frame;
        private int frame2;
        private const int maxFrame = 4;
        private readonly int[] otherAI = new int[aiSlot];
        private const int aiSlot = 4;
        internal static Asset<Texture2D> HoqingGlow;
        internal static Asset<Texture2D> HoqingEmmd;
        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = maxFrame;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.npcSlots = 14f;
            NPC.width = 140;
            NPC.height = 140;
            NPC.defense = 25;
            NPC.damage = 60;
            NPC.value = Item.buyPrice(0, 50, 0, 0);
            NPC.lifeMax = 400000;
            NPC.aiStyle = -1;
            AIType = -1;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath14;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Hanba");
        }

        public override bool CheckActive() {
            return false;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            return base.DrawHealthBar(hbPosition, ref scale, ref position);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override void AI() {
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.Alives()) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.Alives()) {
                    NPC.ai[0] = -1;
                    NPC.ai[1] = 0f;
                    NPC.ai[2] = 0f;
                }
            }

            Lighting.AddLight(NPC.Center, Color.BlueViolet.ToVector3() * NPC.scale);

            int targetFrame = 0;
            ref float generalTimer = ref NPC.ai[2];
            ref float attackTimer = ref NPC.ai[1];
            ref float state = ref NPC.ai[0];
            bool setNPCRot = true;

            if (generalTimer == 0) {
                if (!VaultUtils.isServer && !SkyManager.Instance[HoqingSky.name].IsActive()) {
                    SkyManager.Instance.Activate(HoqingSky.name);
                }
            }

            switch (state) {
                //失去目标，脱战
                case -1f:
                    NPC.velocity = new Vector2(0, 60);

                    attackTimer++;

                    if (attackTimer > 180) {
                        NPC.active = false;
                        NPC.netUpdate = true;
                    }

                    break;
                //召唤小弟，然后追逐
                case 0f:
                    Vector2 toPlayer = target.Center - NPC.Center;
                    float distance = toPlayer.Length();
                    toPlayer.Normalize();

                    if (attackTimer == 0 && !VaultUtils.isClient) {
                        for (int i = 0; i < 6; i++) {
                            NPC.NewNPCDirect(NPC.FromObjectGetParent(), NPC.Center
                                , ModContent.NPCType<GhostFire>(), ai0: NPC.whoAmI, ai1: i, target: NPC.target);
                        }
                        HoqingRingFire.AllVanish(NPC.whoAmI);
                        for (int i = 0; i < 28; i++) {
                            Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, Vector2.Zero
                                        , ModContent.ProjectileType<HoqingRingFire>(), NPC.damage, 2
                                        , ai0: NPC.whoAmI, ai2: MathHelper.TwoPi / 28 * i);
                        }
                    }

                    attackTimer++;

                    //每隔一定时间进行一次猛冲突进
                    if (attackTimer % 180 == 0) {
                        //加强冲刺：直接设置速度指向玩家
                        float dashSpeed = 38f;
                        NPC.velocity = toPlayer * dashSpeed;

                        //可选：震动屏幕、产生粒子、播放音效等
                        if (Main.netMode != NetmodeID.Server) {
                            SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
                            for (int i = 0; i < 15; i++) {
                                Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GreenTorch, Scale: 1.5f).noGravity = true;
                            }
                        }

                        if (otherAI[1] > 0) {
                            otherAI[2] = 15;
                        }

                        NPC.netUpdate = true;
                    }
                    else {
                        if (attackTimer % 5 == 0) {
                            if (otherAI[2] > 0) {
                                otherAI[2]--;
                                if (!VaultUtils.isClient) {
                                    Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center
                                        , new Vector2(Main.rand.Next(-13, 13), Main.rand.Next(-13, 13))
                                        , ModContent.ProjectileType<HoqingShadow>(), NPC.damage, 2, ai2: NPC.whoAmI);

                                    if (otherAI[2] == 0 && otherAI[1] > 0) {
                                        TeleportNearTarget(target);
                                    }
                                }
                            }
                        }

                        //平时持续小幅追踪，模拟压迫感逼近
                        float baseSpeed = 10f;
                        float inertia = 20f;
                        Vector2 desiredVelocity = toPlayer * baseSpeed;
                        NPC.velocity = (NPC.velocity * (inertia - 1) + desiredVelocity) / inertia;
                    }

                    //朝向玩家
                    NPC.spriteDirection = NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;

                    if (!NPC.AnyNPCs(ModContent.NPCType<GhostFire>())) {
                        attackTimer = 0;
                        state = 1f;
                        HoqingRingFire.AllVanish(NPC.whoAmI);
                    }
                    break;
                //瞎勾巴甩弹幕
                case 1f:
                    if (attackTimer == 0) {
                        targetFrame = 3;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, NPC.Center);
                    }

                    if (attackTimer <= 60) {
                        targetFrame = 3;
                        NPC.velocity *= 0.85f;

                        //中心聚能特效（每隔几帧释放粒子）
                        if (!VaultUtils.isServer && attackTimer % 5 == 0) {
                            for (int i = 0; i < 8; i++) {
                                Vector2 offset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * Main.rand.NextFloat(40f, 80f);
                                Dust dust = Dust.NewDustDirect(NPC.Center + offset, 0, 0, DustID.Torch, 0, 0, 100, Color.Orange, 1.5f);
                                dust.velocity = -offset.SafeNormalize(Vector2.Zero) * 1.5f;
                                dust.noGravity = true;
                            }
                        }
                    }

                    else if (attackTimer > 60 && attackTimer <= 180) {
                        targetFrame = 4;

                        if (!VaultUtils.isClient && attackTimer % 20 == 0) {
                            int total = 3;
                            for (int i = 0; i < total; i++) {
                                float angle = MathHelper.TwoPi * i / total + Main.rand.NextFloat(-0.1f, 0.1f);
                                Vector2 velocity = angle.ToRotationVector2() * 9f;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, velocity,
                                    ModContent.ProjectileType<OblivionFireOrb>(), NPC.damage / 2, 2f);
                            }
                        }

                        //悬停朝玩家上方缓缓移动
                        NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(target.Center + new Vector2(0, -300)) * 23f, 0.1f);
                    }

                    else if (attackTimer > 180 && attackTimer <= 240) {
                        if (!VaultUtils.isClient && attackTimer % 20 == 0) {
                            //向玩家方向发射跟踪弹
                            Vector2 toTarget = NPC.SafeDirectionTo(target.Center);
                            Vector2 perturbed = toTarget.RotatedByRandom(MathHelper.ToRadians(6));
                            Vector2 vel = perturbed * 13f;

                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                ModContent.ProjectileType<OblivionFireOrb>(), NPC.damage / 2, 2f);
                        }

                        //缓缓下降压近
                        NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0, 6), 0.1f);
                    }

                    if (attackTimer == 250) {
                        if (!VaultUtils.isClient) {
                            //大火焰环围绕Boss爆开
                            int fireCount = 12;
                            for (int i = 0; i < fireCount; i++) {
                                float angle = MathHelper.TwoPi * i / fireCount;
                                Vector2 vel = angle.ToRotationVector2() * 7f;

                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                    ModContent.ProjectileType<OblivionFireOrb>(), NPC.damage, 3f);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.5f }, NPC.Center);
                        //粒子冲击波（客户端）
                        if (!VaultUtils.isServer) {
                            for (int i = 0; i < 30; i++) {
                                Vector2 dir = Main.rand.NextVector2Unit();
                                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.FireworkFountain_Red, dir.X * 3, dir.Y * 3, 100, Color.Orange, 2f);
                                Main.dust[dust].noGravity = true;
                            }
                        }
                    }

                    attackTimer++;

                    //结束阶段
                    if (attackTimer > 270) {
                        attackTimer = 0;

                        if (++otherAI[0] > 4) {
                            otherAI[0] = 0;
                            otherAI[1]++;
                            state = 0;
                        }

                        NPC.netUpdate = true;
                    }
                    break;
            }

            generalTimer++;
            if (setNPCRot) {
                NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.velocity.X * 0.02f, 0.1f);
            }

            FindFrame(targetFrame);
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (NPC.life > 0) {
                return;
            }
            int Hoqing_Buttom = Mod.Find<ModGore>("Hoqing_Buttom").Type;
            int Hoqing_Left = Mod.Find<ModGore>("Hoqing_Left").Type;
            int Hoqing_Nose = Mod.Find<ModGore>("Hoqing_Nose").Type;
            int Hoqing_Top = Mod.Find<ModGore>("Hoqing_Top").Type;

            var entitySource = NPC.GetSource_Death();

            Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hoqing_Nose);
            for (int i = 0; i < 2; i++) {
                Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hoqing_Buttom);
                Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hoqing_Left);
                Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hoqing_Top);
            }
        }

        private void TeleportNearTarget(Player target) {
            Vector2 offset = Main.rand.NextVector2Unit() * Main.rand.Next(300, 500);
            NPC.position = target.Center + offset - NPC.Size / 2f;

            //粒子效果
            for (int i = 0; i < 30; i++) {
                Vector2 dustVel = Main.rand.NextVector2Circular(3f, 3f);
                Dust.NewDustPerfect(NPC.Center, DustID.GreenTorch, dustVel, 100, Color.Magenta, 1.5f).noGravity = true;
            }

            //音效
            SoundEngine.PlaySound(SoundID.Item8, NPC.Center);//魔法瞬移声
        }

        private new void FindFrame(int targetFrame) {
            if (++NPC.frameCounter > 5) {
                NPC.frameCounter = 0;
                if (frame > targetFrame) {
                    frame--;
                }
                else if (frame < targetFrame) {
                    frame++;
                }
                if (++frame2 >= maxFrame) {
                    frame2 = 0;
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            Texture2D glowValue = HoqingGlow.Value;
            Texture2D emmdValue = HoqingEmmd.Value;
            Rectangle rectangle = VaultUtils.GetRectangle(mainValue, frame, maxFrame);
            Rectangle rectangle2 = VaultUtils.GetRectangle(glowValue, frame2, maxFrame);
            float sengs = 0.2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(mainValue, drawOldPos, rectangle, drawColor * sengs
                    , 0, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                spriteBatch.Draw(glowValue, drawOldPos, rectangle2, Color.White * sengs
                    , 0, rectangle2.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                sengs *= 0.8f;
            }
            spriteBatch.Draw(mainValue, NPC.Center - Main.screenPosition, rectangle, drawColor
                , NPC.rotation, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
            spriteBatch.Draw(glowValue, NPC.Center - Main.screenPosition, rectangle2, Color.White
                , NPC.rotation, rectangle2.Size() / 2, NPC.scale, SpriteEffects.None, 0);
            spriteBatch.Draw(emmdValue, NPC.Center - Main.screenPosition, rectangle2, drawColor
                , NPC.rotation, rectangle2.Size() / 2, NPC.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class HoqingRingFire : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hoqings/GhostFire";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 3;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public static void AllVanish(int npc) {
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != ModContent.ProjectileType<HoqingRingFire>() || proj.ai[0] != npc) {
                    continue;
                }
                proj.localAI[1] = 1f;
                proj.netUpdate = true;
            }
        }

        public override void AI() {
            NPC boss = Main.npc[(int)Projectile.ai[0]];
            if (boss.Alives()) {
                Projectile.timeLeft = 2;
                Projectile.ai[1] += 0.2f;
                Vector2 targetPos = boss.Center + (Projectile.ai[1] + Projectile.ai[2]).ToRotationVector2() * Projectile.localAI[0];
                Projectile.velocity = Projectile.Center.To(targetPos);
                Projectile.rotation = 0;
                if (Projectile.localAI[0] < 900 && Projectile.localAI[1] == 0) {
                    Projectile.localAI[0] += 10;
                }
            }
            else {
                Projectile.localAI[1] = 1f;
            }

            if (Projectile.localAI[1] == 1) {
                if (Projectile.localAI[0] > 0) {
                    Projectile.localAI[0] -= 10;
                }
                else {
                    Projectile.Kill();
                }
            }

            if (VaultUtils.isServer) {
                return;
            }

            VaultUtils.ClockFrame(ref Projectile.frame, 5, 3);

            //粒子拖尾（多种颜色/变化大小）
            for (int i = 0; i < 2; i++) {
                Vector2 offset = Projectile.velocity * -0.2f * i;
                int dust = Dust.NewDust(Projectile.position + offset, Projectile.width, Projectile.height, DustID.GreenTorch,
                    0f, 0f, 150, Color.Lerp(Color.Lime, Color.Cyan, Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 2.4f));
                Main.dust[dust].velocity *= 0.1f;
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rect = VaultUtils.GetRectangle(tex, Projectile.frame, 4);
            Vector2 origin = rect.Size() / 2f;

            Color baseColor = Color.Lerp(Color.LimeGreen, Color.Cyan, 0.5f);
            float scale = Projectile.scale;

            //绘制残影（幽光拖尾）
            float alpha = 0.4f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float fade = alpha * (1f - i / (float)Projectile.oldPos.Length);
                Main.spriteBatch.Draw(tex, pos, rect, baseColor * fade, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
            }

            //主体 + 发光外层
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, drawPos, rect, baseColor, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);

            //外层发光（更大的，半透明）
            Main.spriteBatch.Draw(tex, drawPos, rect, baseColor * 0.3f, Projectile.rotation, origin, scale * 1.4f, SpriteEffects.None, 0f);

            return false;
        }
    }

    internal class HoqingShadow : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hoqings/Hoqing";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
        }

        public override void AI() {
            if (Projectile.ai[0] == 0) {
                Projectile.ai[1] = 1f;
                Projectile.frame = Main.rand.Next(4);
            }

            Projectile.velocity *= 0.98f;

            Projectile.position += Main.npc[(int)Projectile.ai[2]].velocity;

            Projectile.ai[0]++;

            Projectile.ai[1] *= 0.9f;
            if (Projectile.ai[1] < 0.05f) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = VaultUtils.GetRectangle(mainValue, Projectile.frame, 4);
            Main.spriteBatch.Draw(mainValue, Projectile.Center - Main.screenPosition, rectangle, Color.White * Projectile.ai[1]
                , Projectile.rotation, rectangle.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class OblivionFireOrb : ModProjectile
    {
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            //自旋转
            Projectile.rotation += 0.1f;

            //微漂浮扰动轨迹
            Projectile.velocity += new Vector2(
                (float)Math.Sin(Projectile.ai[0] + Projectile.whoAmI) * 0.05f,
                (float)Math.Cos(Projectile.ai[0] + Projectile.whoAmI) * 0.05f);

            //增加颜色周期变化
            Projectile.ai[0] += 0.05f;

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 1; i++) {
                    Vector2 dustOffset = Projectile.velocity * -0.5f;
                    int dust = Dust.NewDust(Projectile.Center + dustOffset, 0, 0, DustID.Shadowflame, 0, 0, 150, default, Main.rand.NextFloat(1.2f, 2.2f));
                    Main.dust[dust].velocity *= 0.3f;
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].fadeIn = 1f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            Color coreColor = Color.Lerp(Color.MediumPurple, Color.DeepPink, (float)Math.Sin(Projectile.ai[0]) * 0.5f + 0.5f);

            float trailOpacity = 0.35f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float fade = trailOpacity * (1f - i / (float)Projectile.oldPos.Length);
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color color = Color.Lerp(coreColor, Color.Black, i / (float)Projectile.oldPos.Length) * fade;
                Main.spriteBatch.Draw(tex, pos, null, color, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            }

            float glowScale = 1.4f + 0.1f * (float)Math.Sin(Projectile.ai[0] * 2);
            Color glowColor = coreColor * 0.25f;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, Projectile.scale * glowScale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, coreColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }
    }

    internal class GhostFireProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hoqings/GhostFire";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 3;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (VaultUtils.isServer) {
                return;
            }

            VaultUtils.ClockFrame(ref Projectile.frame, 5, 3);

            //粒子拖尾（多种颜色/变化大小）
            for (int i = 0; i < 2; i++) {
                Vector2 offset = Projectile.velocity * -0.2f * i;
                int dust = Dust.NewDust(Projectile.position + offset, Projectile.width, Projectile.height, DustID.GreenTorch,
                    0f, 0f, 150, Color.Lerp(Color.Lime, Color.Cyan, Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 2.4f));
                Main.dust[dust].velocity *= 0.1f;
                Main.dust[dust].noGravity = true;
            }

            //抖动灵动感
            Projectile.position += Main.rand.NextVector2Circular(0.5f, 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rect = VaultUtils.GetRectangle(tex, Projectile.frame, 4);
            Vector2 origin = rect.Size() / 2f;

            Color baseColor = Color.Lerp(Color.LimeGreen, Color.Cyan, 0.5f);
            float scale = Projectile.scale;

            //绘制残影（幽光拖尾）
            float alpha = 0.4f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float fade = alpha * (1f - i / (float)Projectile.oldPos.Length);
                Main.spriteBatch.Draw(tex, pos, rect, baseColor * fade, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
            }

            //主体 + 发光外层
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, drawPos, rect, baseColor, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);

            //外层发光（更大的，半透明）
            Main.spriteBatch.Draw(tex, drawPos, rect, baseColor * 0.3f, Projectile.rotation, origin, scale * 1.4f, SpriteEffects.None, 0f);

            return false;
        }
    }

    internal class GhostFire : ModNPC
    {
        private int frame;
        private const int maxFrame = 4;
        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 4;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }
        public override void SetDefaults() {
            NPC.width = 40;
            NPC.height = 140;
            NPC.defense = 25;
            NPC.damage = 60;
            NPC.value = Item.buyPrice(0, 5, 0, 0);
            NPC.lifeMax = 120000;
            NPC.aiStyle = -1;
            AIType = -1;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.HitSound = SoundID.NPCHit9;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.hide = true;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = 120000;
            if (Main.expertMode) {
                NPC.lifeMax += 5000;
            }
            if (Main.masterMode) {
                NPC.lifeMax += 5000;
            }
        }

        public override void AI() {
            //获取Boss实体
            NPC boss = Main.npc[(int)NPC.ai[0]];
            if (!boss.active || boss.ModNPC is not Hoqing hoqing) {
                NPC.active = false;
                return;
            }

            //轨道参数
            float orbitRadius = 100f + 20f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 0.5f + NPC.ai[1]); //动态半径变化
            float orbitSpeed = 1.2f;     //转速
            float verticalRange = 30f;   //上下活动范围
            float wobbleStrength = 10f;  //左右/上下扰动
            float twistFrequency = 0.3f;

            float time = Main.GlobalTimeWrappedHourly;
            float angleOffset = NPC.ai[1]; //唯一轨迹偏移
            float baseAngle = time * orbitSpeed + angleOffset;

            //基础旋转轨迹（绕Boss）
            Vector2 orbitPos = baseAngle.ToRotationVector2() * orbitRadius;

            //加入飘忽扰动（上下/左右浮动、微随机）
            float floatX = (float)Math.Sin(time * 2f + angleOffset * 2f) * wobbleStrength;
            float floatY = (float)Math.Cos(time * 1.5f + angleOffset * 3f) * verticalRange;

            Vector2 floatOffset = new Vector2(floatX, floatY);

            //模拟Z轴偏移（远近感）
            float scaleZ = 1.0f + 0.1f * (float)Math.Sin(time * twistFrequency + angleOffset);
            NPC.scale = scaleZ;

            //最终目标位置 = Boss中心 + 旋转偏移 + 漂浮扰动
            Vector2 targetPos = boss.Center + orbitPos + floatOffset;

            //平滑漂移过去，营造灵异感
            float inertia = 20f;
            NPC.Center = Vector2.Lerp(NPC.Center, targetPos, 1f / inertia);

            //禁止旋转，强制朝向
            NPC.rotation = 0f;

            NPC.position += boss.velocity;

            if (++NPC.ai[2] > 60 + NPC.ai[1] * 10) {
                NPC.ai[2] = 0;
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.2f }, NPC.Center);
                if (!VaultUtils.isClient) {
                    Player player = Main.player[NPC.target];
                    Vector2 ver = NPC.Center.To(player.Center).UnitVector() * 13;
                    Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, ver
                        , ModContent.ProjectileType<GhostFireProj>(), NPC.damage / 2, 2);
                }
            }

            //帧动画更新
            VaultUtils.ClockFrame(ref frame, 5, maxFrame - 1);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            Rectangle rectangle = VaultUtils.GetRectangle(mainValue, frame, maxFrame);
            float sengs = 0.2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(mainValue, drawOldPos, rectangle, drawColor * sengs
                    , 0, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                sengs *= 0.8f;
            }
            spriteBatch.Draw(mainValue, NPC.Center - Main.screenPosition, rectangle, Color.White
                , NPC.rotation, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index) {
            Main.instance.DrawCacheNPCProjectiles.Add(index);
        }
    }

    internal class HoqingSky : CustomSky
    {
        private bool active;
        private float intensity;
        private float maxIntensity = 0.8f; //更高压迫感
        private Color skyColor;

        internal static string name;
        internal static Asset<Texture2D> HanbaSkySun;
        internal static Asset<Texture2D> HanbaSkyColorBar;

        public static void LoadInstance() {
            name = "AncientChineseMythology:HoqingSky";
            SkyManager.Instance[name] = new HoqingSky();
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Reset() {
            active = false;
            intensity = 0.01f;
        }

        public override bool IsActive() => active;

        public override void Update(GameTime gameTime) {
            if (NPC.AnyNPCs(ModContent.NPCType<Hoqing>())) {
                NPC boss = null;
                foreach (var npc in Main.ActiveNPCs) {
                    if (npc.type == ModContent.NPCType<Hoqing>()) {
                        boss = npc;
                        break;
                    }
                }

                if (boss != null) {
                    float distance = Main.LocalPlayer.Distance(boss.Center);
                    float t = MathHelper.Clamp(distance / 1600f, 0f, 1f);

                    //亡灵风格多重色阶：深靛 -> 幽蓝紫 -> 淡蓝魂光
                    skyColor = VaultUtils.MultiStepColorLerp(t,
                        new Color(20, 20, 40),   //深靛（最压迫）
                        new Color(40, 60, 90),   //幽蓝紫
                        new Color(80, 130, 160)); //魂蓝（近Boss时）

                    if (intensity < maxIntensity)
                        intensity += 0.01f;

                    active = true;
                }
            }
            else {
                intensity -= 0.01f;
                if (intensity <= 0f) {
                    intensity = 0f;
                    Deactivate();
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            Vector2 shake = Main.rand.NextVector2Circular(1.5f * intensity, 1.5f * intensity); //更幽柔的震颤

            //背景主色调（幽蓝调）
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle((int)shake.X, (int)shake.Y, Main.screenWidth, Main.screenHeight),
                skyColor * intensity);

            //渐变冷色雾气层（由 HanbaSkyColorBar 替代使用）
            if (HanbaSkyColorBar?.Value != null) {
                Color mistColor = VaultUtils.MultiStepColorLerp(0.4f, Color.Cyan, Color.Blue);
                spriteBatch.Draw(HanbaSkyColorBar.Value,
                    new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                    mistColor * intensity);
            }

            //冥日替代图（可命名为幽月/冥眼等）
            if (HanbaSkySun?.Value != null) {
                Vector2 sunPos = new Vector2(Main.screenWidth / 2f, 140);
                Color sunColor = new Color(100, 150, 255, 0) * intensity * 1.5f;

                spriteBatch.Draw(HanbaSkySun.Value,
                    sunPos, null, sunColor, 0f, HanbaSkySun.Size() / 2f, 1.8f, SpriteEffects.None, 0f);
            }
        }

        public override Color OnTileColor(Color inColor) {
            //所有地表颜色变冷/失色
            Color desaturated = Color.Lerp(inColor, Color.DarkSlateGray, 0.4f);
            return Color.Lerp(inColor, desaturated, intensity);
        }
    }

}
