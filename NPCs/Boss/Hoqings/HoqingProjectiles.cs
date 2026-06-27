using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hoqings
{
    //=========================================================
    // 幽火仆从（角色化）：枪兵 / 爆兵 / 疫医
    //=========================================================
    internal class GhostFire : ModNPC
    {
        private int frame;
        private const int maxFrame = 4;
        private int spawnTick;             //现形溶解计时 (DissolveBurn 1→0)
        private const int SpawnDissolve = 32;

        //角色由 ai[1] 推导：0 枪兵(lancer) / 1 爆兵(bomber) / 2 疫医(healer)
        private int Role => (int)NPC.ai[1] % 3;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 4;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.width = 40;
            NPC.height = 140;
            NPC.defense = 20;
            NPC.damage = 50;
            NPC.value = Item.buyPrice(0, 5, 0, 0);
            NPC.lifeMax = 25000;
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
            NPC.lifeMax = 25000;
            if (Main.expertMode) {
                NPC.lifeMax += 5000;
            }
            if (Main.masterMode) {
                NPC.lifeMax += 5000;
            }
        }

        private Color RoleColor() => Role switch {
            0 => new Color(150, 255, 160), //枪兵：尸绿
            1 => new Color(255, 150, 60),  //爆兵：疫橙
            _ => new Color(255, 120, 200), //疫医：腐粉
        };

        public override void AI() {
            NPC boss = Main.npc[(int)NPC.ai[0]];
            if (!boss.active || boss.ModNPC is not Hoqing) {
                NPC.active = false;
                return;
            }

            if (spawnTick < SpawnDissolve) {
                spawnTick++;
            }

            //绕 Boss 飘忽轨道（保留原灵异手感）
            float orbitRadius = 130f + 20f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 0.5f + NPC.ai[1]);
            float time = Main.GlobalTimeWrappedHourly;
            float angleOffset = NPC.ai[1];
            float baseAngle = time * 1.2f + angleOffset;
            Vector2 orbitPos = baseAngle.ToRotationVector2() * orbitRadius;
            float floatX = (float)Math.Sin(time * 2f + angleOffset * 2f) * 10f;
            float floatY = (float)Math.Cos(time * 1.5f + angleOffset * 3f) * 30f;
            NPC.scale = 1.0f + 0.1f * (float)Math.Sin(time * 0.3f + angleOffset);
            Vector2 targetPos = boss.Center + orbitPos + new Vector2(floatX, floatY);
            NPC.Center = Vector2.Lerp(NPC.Center, targetPos, 1f / 20f);
            NPC.rotation = 0f;
            NPC.position += boss.velocity;

            Player player = Main.player[NPC.target];
            if (!player.Alives()) {
                NPC.TargetClosest();
                player = Main.player[NPC.target];
            }

            NPC.ai[2]++;
            switch (Role) {
                case 0: //枪兵：定期朝玩家发射制导幽火
                    if (NPC.ai[2] > 70 + NPC.ai[1] * 6) {
                        NPC.ai[2] = 0;
                        SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.2f }, NPC.Center);
                        if (!VaultUtils.isClient && player.Alives()) {
                            Vector2 ver = NPC.Center.To(player.Center).UnitVector() * 13;
                            Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, ver
                                , ModContent.ProjectileType<GhostFireProj>(), NPC.damage / 2, 2);
                        }
                    }
                    break;
                case 1: //爆兵：投掷缓慢的湮灭火球（弧线压制走位）
                    if (NPC.ai[2] > 120 + NPC.ai[1] * 6) {
                        NPC.ai[2] = 0;
                        SoundEngine.PlaySound(SoundID.Item20, NPC.Center);
                        if (!VaultUtils.isClient && player.Alives()) {
                            Vector2 ver = NPC.Center.To(player.Center).UnitVector() * 7f;
                            Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, ver
                                , ModContent.ProjectileType<OblivionFireOrb>(), NPC.damage / 2, 2);
                        }
                    }
                    break;
                default: //疫医：治疗 Boss（须优先击杀）
                    if (NPC.ai[2] > 90) {
                        NPC.ai[2] = 0;
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f }, NPC.Center);
                        if (!VaultUtils.isClient && boss.life < boss.lifeMax) {
                            boss.life = Math.Min(boss.life + 2000, boss.lifeMax);
                            boss.netUpdate = true;
                        }
                        //治疗光束粒子
                        if (!VaultUtils.isServer) {
                            Vector2 dir = NPC.Center.To(boss.Center).UnitVector();
                            for (int i = 0; i < 14; i++) {
                                Vector2 p = NPC.Center + dir * (i * (Vector2.Distance(NPC.Center, boss.Center) / 14f));
                                Dust d = Dust.NewDustPerfect(p, DustID.GreenTorch, dir * 2f, 120, new Color(255, 130, 200), 1.3f);
                                d.noGravity = true;
                            }
                        }
                    }
                    break;
            }

            VaultUtils.ClockFrame(ref frame, 5, maxFrame - 1);
        }

        public override void OnKill() {
            //死亡: 尸火噪声崩解 (DissolveBurn 0→1)。服务端权威生成一次, 同步给各端纯视觉绘制。
            if (!VaultUtils.isClient) {
                Projectile.NewProjectile(NPC.GetSource_Death(), NPC.Center, Vector2.Zero
                    , ModContent.ProjectileType<HoqingGhostDissolve>(), 0, 0f, Main.myPlayer
                    , ai0: Role, ai1: frame);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            Rectangle rectangle = VaultUtils.GetRectangle(mainValue, frame, maxFrame);
            Color tint = RoleColor();

            //现形: 尸火噪声聚成形 (DissolveBurn threshold 1→0); 期间不画拖尾, 焰边发光
            if (spawnTick < SpawnDissolve) {
                float threshold = 1f - spawnTick / (float)SpawnDissolve;
                WeaponVFX.ApplyDissolveBurn(mainValue, NPC.Center, rectangle, tint,
                    NPC.rotation, rectangle.Size() / 2f, NPC.scale,
                    threshold: threshold, intensity: 1f,
                    edgeColor: new Color(TelegraphColors.GhostGreen.R, TelegraphColors.GhostGreen.G, TelegraphColors.GhostGreen.B, (byte)220),
                    edgeWidth: 0.12f, noiseScale: 2.6f);
                return false;
            }

            float sengs = 0.2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(mainValue, drawOldPos, rectangle, tint * sengs
                    , 0, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                sengs *= 0.8f;
            }
            spriteBatch.Draw(mainValue, NPC.Center - Main.screenPosition, rectangle, tint
                , NPC.rotation, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index) {
            Main.instance.DrawCacheNPCProjectiles.Add(index);
        }
    }

    //=========================================================
    // 幽火死亡崩解 (DissolveBurn 0→1, 纯视觉; 复用 GhostFire 贴图与角色色)
    //=========================================================
    internal class HoqingGhostDissolve : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hoqings/GhostFire";
        private const int Life = 34;

        // ai[0] = role(0/1/2), ai[1] = frame
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Life;
        }

        private Color RoleColor() => ((int)Projectile.ai[0] % 3) switch {
            0 => new Color(150, 255, 160),
            1 => new Color(255, 150, 60),
            _ => new Color(255, 120, 200),
        };

        public override void AI() {
            Projectile.velocity *= 0.92f;
            if (!VaultUtils.isServer && Main.rand.NextBool()) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20, 20),
                    DustID.GreenTorch, new Vector2(0, -Main.rand.NextFloat(0.5f, 2.5f)), 120,
                    TelegraphColors.GhostGreen, 1.4f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rect = VaultUtils.GetRectangle(tex, (int)Projectile.ai[1] % 4, 4);
            float threshold = 1f - Projectile.timeLeft / (float)Life; // 0→1 崩解
            WeaponVFX.ApplyDissolveBurn(tex, Projectile.Center, rect, RoleColor(),
                Projectile.rotation, rect.Size() / 2f, Projectile.scale,
                threshold: threshold, intensity: 1f,
                edgeColor: new Color(TelegraphColors.GhostGreen.R, TelegraphColors.GhostGreen.G, TelegraphColors.GhostGreen.B, (byte)230),
                edgeWidth: 0.13f, noiseScale: 2.6f, direction: new Vector2(0, -1f), sweepStrength: 0.4f);
            return false;
        }
    }

    //=========================================================
    // 制导幽火（Boss 扇形/脉冲 + 仆从枪兵 + 召唤武器复用）
    //=========================================================
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

            for (int i = 0; i < 2; i++) {
                Vector2 offset = Projectile.velocity * -0.2f * i;
                int dust = Dust.NewDust(Projectile.position + offset, Projectile.width, Projectile.height, DustID.GreenTorch,
                    0f, 0f, 150, Color.Lerp(Color.Lime, Color.Cyan, Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 2.4f));
                Main.dust[dust].velocity *= 0.1f;
                Main.dust[dust].noGravity = true;
            }

            Projectile.position += Main.rand.NextVector2Circular(0.5f, 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rect = VaultUtils.GetRectangle(tex, Projectile.frame, 4);
            Vector2 origin = rect.Size() / 2f;

            Color baseColor = Color.Lerp(Color.LimeGreen, Color.Cyan, 0.5f);
            float scale = Projectile.scale;

            float alpha = 0.4f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float fade = alpha * (1f - i / (float)Projectile.oldPos.Length);
                Main.spriteBatch.Draw(tex, pos, rect, baseColor * fade, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, drawPos, rect, baseColor, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, rect, baseColor * 0.3f, Projectile.rotation, origin, scale * 1.4f, SpriteEffects.None, 0f);
            return false;
        }
    }

    //=========================================================
    // 湮灭火球（疫风间隙 / 爆兵投掷）
    //=========================================================
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
            Projectile.rotation += 0.1f;
            Projectile.velocity += new Vector2(
                (float)Math.Sin(Projectile.ai[0] + Projectile.whoAmI) * 0.05f,
                (float)Math.Cos(Projectile.ai[0] + Projectile.whoAmI) * 0.05f);
            Projectile.ai[0] += 0.05f;

            if (!VaultUtils.isServer) {
                Vector2 dustOffset = Projectile.velocity * -0.5f;
                int dust = Dust.NewDust(Projectile.Center + dustOffset, 0, 0, DustID.Shadowflame, 0, 0, 150, default, Main.rand.NextFloat(1.2f, 2.2f));
                Main.dust[dust].velocity *= 0.3f;
                Main.dust[dust].noGravity = true;
                Main.dust[dust].fadeIn = 1f;
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

    //=========================================================
    // 冲撞残影（幕一列阵冲撞）
    //=========================================================
    internal class HoqingShadow : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hoqings/Hoqing";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 100;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 30;
        }

        public override void AI() {
            if (Projectile.ai[0] == 0) {
                Projectile.localAI[0] = 1f;
            }
            Projectile.ai[0]++;
            Projectile.localAI[0] *= 0.9f;
            if (Projectile.localAI[0] < 0.05f) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = VaultUtils.GetRectangle(mainValue, 0, 4);
            Main.spriteBatch.Draw(mainValue, Projectile.Center - Main.screenPosition, rectangle
                , new Color(120, 255, 150) * Projectile.localAI[0]
                , Projectile.rotation, rectangle.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    //=========================================================
    // 尸坑（预告 → 喷发的地面危害）
    //=========================================================
    internal class CorpsePit : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hoqings/GhostFire";
        private const int TelegraphTime = 75;
        private const int ActiveTime = 150;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 150;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TelegraphTime + ActiveTime + 40;
        }

        private float T => Projectile.ai[0];

        public override bool CanHitPlayer(Player target) {
            return T >= TelegraphTime && T < TelegraphTime + ActiveTime;
        }

        public override void AI() {
            Projectile.ai[0]++;

            if ((int)T == TelegraphTime) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.4f }, Projectile.Center);
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 24; i++) {
                        Vector2 v = Main.rand.NextVector2Circular(8, 8);
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch, v, 100, new Color(140, 255, 150), 2.4f);
                        d.noGravity = true;
                    }
                }
            }

            if (!VaultUtils.isServer) {
                if (T < TelegraphTime) {
                    //预告环：收束的尘环
                    float p = T / TelegraphTime;
                    for (int i = 0; i < 2; i++) {
                        float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                        Vector2 e = ang.ToRotationVector2() * (Projectile.width / 2f) * (1.2f - p);
                        Dust d = Dust.NewDustPerfect(Projectile.Center + e, DustID.GreenTorch, -e.SafeNormalize(Vector2.Zero) * 2f, 150, new Color(120, 255, 130), 1.2f);
                        d.noGravity = true;
                    }
                }
                else if (T < TelegraphTime + ActiveTime) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 e = Main.rand.NextVector2Circular(Projectile.width / 2f, Projectile.height / 2f);
                        Dust d = Dust.NewDustPerfect(Projectile.Center + e, DustID.GreenTorch, new Vector2(0, -Main.rand.NextFloat(2f, 5f)), 100, new Color(150, 255, 160), 1.8f);
                        d.noGravity = true;
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rect = VaultUtils.GetRectangle(tex, 0, 4);
            Vector2 origin = rect.Size() / 2f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            if (T < TelegraphTime) {
                float p = T / TelegraphTime;
                //统一预警色: 尸绿呼吸圈 (TelegraphColors.GhostGreen, §2.1 持续危险)
                Color c = TelegraphColors.GhostGreen * (0.25f + 0.35f * p);
                c.A = 0;
                float scale = Projectile.width / (float)rect.Width * (0.6f + 0.6f * p);
                Main.spriteBatch.Draw(tex, pos, rect, c, 0f, origin, scale, SpriteEffects.None, 0);
            }
            else if (T < TelegraphTime + ActiveTime) {
                float fade = 1f;
                int active = (int)(T - TelegraphTime);
                if (active < 20) {
                    fade = active / 20f;
                }
                else if (active > ActiveTime - 30) {
                    fade = (ActiveTime - active) / 30f;
                }
                Color c = TelegraphColors.GhostGreen * (0.6f * fade);
                c.A = 0;
                float pulse = 1f + 0.06f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f);
                float scale = Projectile.width / (float)rect.Width * 1.2f * pulse;
                Main.spriteBatch.Draw(tex, pos, rect, c, 0f, origin, scale, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    //=========================================================
    // 脓潭（持续绿池，强迫走位）
    //=========================================================
    internal class SputumPool : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hoqings/OblivionFireOrb";
        private const int WindUp = 20;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 120;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 420;
        }

        public override bool CanHitPlayer(Player target) {
            return Projectile.ai[0] >= WindUp;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<Buffs.HoqingDecline>(), 180);
            target.GetModPlayer<Players.HoqingDeclinePlayer>().AddDecline();
        }

        public override void AI() {
            Projectile.ai[0]++;
            Projectile.velocity *= 0.92f;
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 2; i++) {
                    Vector2 e = Main.rand.NextVector2Circular(Projectile.width / 2f, Projectile.height / 2f);
                    Dust d = Dust.NewDustPerfect(Projectile.Center + e, DustID.GreenTorch, new Vector2(0, -Main.rand.NextFloat(0.5f, 2f)), 120, new Color(120, 255, 120), 1.4f);
                    d.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            float fade = 1f;
            if (Projectile.ai[0] < WindUp) {
                fade = Projectile.ai[0] / WindUp;
            }
            else if (Projectile.timeLeft < 40) {
                fade = Projectile.timeLeft / 40f;
            }

            float pulse = 1f + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f + Projectile.whoAmI);
            float scale = Projectile.width / (float)tex.Width * 1.6f * pulse;
            //统一预警色: 尸绿呼吸毒潭 (TelegraphColors.GhostGreen)
            Color c = TelegraphColors.GhostGreen * (0.55f * fade);
            c.A = 0;
            Main.spriteBatch.Draw(tex, pos, null, c, Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(tex, pos, null, new Color(160, 255, 150, 0) * (0.4f * fade), Projectile.rotation, origin, scale * 0.6f, SpriteEffects.None, 0);
            return false;
        }
    }

    //=========================================================
    // 尸链（命中则在落点复生一名幽火仆从）
    //=========================================================
    internal class HoqingCorpseChain : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hoqings/OblivionFireOrb";

        // ai[0] = boss whoAmI, ai[1] = canRevive(1/0), localAI[0] = landed
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 36;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 110;
        }

        public override void AI() {
            Projectile.rotation += 0.2f;
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 2; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch, Main.rand.NextVector2Circular(2, 2), 120, new Color(180, 255, 180), 1.6f);
                    d.noGravity = true;
                }
            }

            //命中判定：靠近任意玩家即视为"链落"
            if (Projectile.localAI[0] == 0) {
                foreach (Player p in Main.ActivePlayers) {
                    if (p.Alives() && p.WithinRange(Projectile.Center, 70f)) {
                        Projectile.localAI[0] = 1f;
                        break;
                    }
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isClient) {
                return;
            }
            if (Projectile.localAI[0] == 1f && Projectile.ai[1] == 1f) {
                int boss = (int)Projectile.ai[0];
                if (boss >= 0 && boss < Main.maxNPCs && Main.npc[boss].active
                    && Main.npc[boss].ModNPC is Hoqing) {
                    NPC.NewNPCDirect(Projectile.GetSource_Death(), Projectile.Center
                        , ModContent.NPCType<GhostFire>(), ai0: boss, ai1: Main.rand.Next(3), target: Main.npc[boss].target);
                    SoundEngine.PlaySound(SoundID.Item104 with { Pitch = 0.3f }, Projectile.Center);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            float trailOpacity = 0.5f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float fade = trailOpacity * (1f - i / (float)Projectile.oldPos.Length);
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, new Color(160, 255, 170) * fade, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            }
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, new Color(200, 255, 200), Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}

namespace AncientChineseMythology.Buffs
{
    //衰朽：万鬼夜行蓄力时近身叠加；减速 + 持续掉血。
    public class HoqingDecline : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_20";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
            Main.pvpBuff[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex) {
            //具体效果在 HoqingDeclinePlayer 中处理（含叠层）。
        }
    }
}

namespace AncientChineseMythology.Players
{
    public class HoqingDeclinePlayer : ModPlayer
    {
        public int declineStacks;
        private int decayTimer;

        public void AddDecline() {
            declineStacks = Math.Min(declineStacks + 1, 10);
        }

        public override void PostUpdate() {
            bool has = Player.HasBuff(ModContent.BuffType<Buffs.HoqingDecline>());
            if (has) {
                decayTimer = 0;
            }
            else if (declineStacks > 0) {
                if (++decayTimer > 120) {
                    decayTimer = 0;
                    declineStacks--;
                }
            }
        }

        public override void UpdateBadLifeRegen() {
            if (!Player.HasBuff(ModContent.BuffType<Buffs.HoqingDecline>())) {
                return;
            }
            int s = Math.Max(1, declineStacks);
            if (Player.lifeRegen > 0) {
                Player.lifeRegen = 0;
            }
            Player.lifeRegenTime = 0;
            Player.lifeRegen -= 6 * s;
        }

        public override void PostUpdateEquips() {
            if (!Player.HasBuff(ModContent.BuffType<Buffs.HoqingDecline>())) {
                return;
            }
            int s = Math.Max(1, declineStacks);
            Player.moveSpeed -= 0.04f * s;
        }
    }
}
