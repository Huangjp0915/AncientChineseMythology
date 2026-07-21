using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hoqings
{
    //=========================================================
    // 幽火仪仗（角色化仆从）：枪兵×3 / 爆兵×2 / 疫医×1
    // 编队跟随 Boss（V 形前锋 / 后卫 / 贴身），齐射仪式由 Boss 以 ai[3] 下令。
    //=========================================================
    internal class GhostFire : ModNPC
    {
        private int frame;
        private const int maxFrame = 4;
        private int spawnTick;             //现形溶解计时 (DissolveBurn 1→0)
        private const int SpawnDissolve = 32;

        //角色由编队位次推导：0/1/2 枪兵(lancer)  3/4 爆兵(bomber)  5 疫医(healer)
        private int Slot => (int)NPC.ai[1] % 6;
        private int Role => Slot <= 2 ? 0 : (Slot <= 4 ? 1 : 2);

        //疫医治疗引导计时（各端一致推进：由 ai[2] 驱动）
        private const int HealCooldown = 130;
        private const int HealChannel = 40;

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

        //编队位（相对 Boss，dir 为 Boss 面向）：枪兵 V 形前锋 / 爆兵后卫上方 / 疫医贴身下侧
        private Vector2 FormationOffset(int dir) => Role switch {
            0 => new Vector2(dir * (150f + Slot * 55f), -60f + Slot * 42f),
            1 => new Vector2(-dir * (90f + (Slot - 3) * 70f), -130f),
            _ => new Vector2(-dir * 30f, 95f),
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

            //编队跟随：弹性追踪编队位 + 灵异浮动
            int dir = boss.spriteDirection == 0 ? 1 : boss.spriteDirection;
            float time = Main.GlobalTimeWrappedHourly;
            float wob = NPC.ai[1] * 1.7f;
            Vector2 bob = new((float)Math.Sin(time * 1.9f + wob * 2f) * 12f, (float)Math.Cos(time * 1.4f + wob * 3f) * 22f);
            NPC.scale = 1.0f + 0.08f * (float)Math.Sin(time * 0.9f + wob);
            Vector2 targetPos = boss.Center + FormationOffset(dir) + bob;
            NPC.Center = Vector2.Lerp(NPC.Center, targetPos, 1f / 16f);
            NPC.rotation = 0f;
            NPC.position += boss.velocity * 0.5f;
            NPC.spriteDirection = dir;

            Player player = Main.player[NPC.target];
            if (!player.Alives()) {
                NPC.TargetClosest();
                player = Main.player[NPC.target];
            }

            //===== 齐射仪式命令（Boss 置 ai[3]=1，此处自走序列后归零）=====
            if (NPC.ai[3] >= 1f) {
                RunVolleyCommand(player, boss);
                VaultUtils.ClockFrame(ref frame, 5, maxFrame - 1);
                return;
            }

            //===== 自主节奏 =====
            NPC.ai[2]++;
            switch (Role) {
                case 0: //枪兵：瞄准线预告 20f 后发射制导幽火
                    AutonomousLancer(player);
                    break;
                case 1: //爆兵：投掷缓慢的湮灭火球（弧线压制走位）
                    if (NPC.ai[2] > 150 + Slot * 9) {
                        NPC.ai[2] = 0;
                        SoundEngine.PlaySound(SoundID.Item20, NPC.Center);
                        if (!VaultUtils.isClient && player.Alives()) {
                            Vector2 ver = NPC.Center.To(player.Center).UnitVector() * 7f;
                            Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, ver
                                , ModContent.ProjectileType<OblivionFireOrb>(), NPC.damage / 2, 2);
                        }
                    }
                    break;
                default: //疫医：周期性治疗引导（40f 光束，可通过击杀打断）
                    AutonomousHealer(boss);
                    break;
            }

            VaultUtils.ClockFrame(ref frame, 5, maxFrame - 1);
        }

        private void AutonomousLancer(Player player) {
            float t = NPC.ai[2];
            float fireAt = 110 + Slot * 8;
            //发射前 20f：瞄准线粒子预告（可读性阀门）
            if (!VaultUtils.isServer && t > fireAt - 20 && t <= fireAt && (int)t % 3 == 0 && player.Alives()) {
                Vector2 aim = NPC.Center.To(player.Center).UnitVector();
                for (int i = 1; i < 5; i++) {
                    Dust d = Dust.NewDustPerfect(NPC.Center + aim * (i * 42f), DustID.GreenTorch
                        , aim * 1.5f, 160, RoleColor(), 1.1f);
                    d.noGravity = true;
                }
            }
            if (t > fireAt) {
                NPC.ai[2] = 0;
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.2f }, NPC.Center);
                if (!VaultUtils.isClient && player.Alives()) {
                    Vector2 ver = NPC.Center.To(player.Center).UnitVector() * 12f;
                    Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, ver
                        , ModContent.ProjectileType<GhostFireProj>(), NPC.damage / 2, 2);
                }
            }
        }

        private void AutonomousHealer(NPC boss) {
            float t = NPC.ai[2];
            if (t < HealCooldown) {
                return;
            }
            float channel = t - HealCooldown;
            //引导期：光束视觉 + 每 10f 服务器回血
            if (!VaultUtils.isServer && (int)channel % 2 == 0) {
                Vector2 lineDir = NPC.Center.To(boss.Center).UnitVector();
                float len = Vector2.Distance(NPC.Center, boss.Center);
                Dust d = Dust.NewDustPerfect(NPC.Center + lineDir * Main.rand.NextFloat(len), DustID.GreenTorch
                    , lineDir * 2f, 130, new Color(255, 130, 200), 1.4f);
                d.noGravity = true;
            }
            if (!VaultUtils.isClient && (int)channel % 10 == 9 && boss.life < boss.lifeMax) {
                boss.life = Math.Min(boss.life + 1200, boss.lifeMax);
                boss.netUpdate = true;
            }
            if ((int)channel == 1) {
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f }, NPC.Center);
            }
            if (channel >= HealChannel) {
                NPC.ai[2] = 0;
            }
        }

        //齐射仪式：ai[3] 自增序列，各角色在固定帧动作，序列完归零
        private void RunVolleyCommand(Player player, NPC boss) {
            float seq = NPC.ai[3];
            NPC.ai[3]++;

            if (!player.Alives()) {
                if (seq > 40) NPC.ai[3] = 0;
                return;
            }

            switch (Role) {
                case 0: //枪兵：10/20/30 帧三连射（相位按 Slot 错开 2f）
                    if ((int)seq == 10 + Slot * 2 || (int)seq == 20 + Slot * 2 || (int)seq == 30 + Slot * 2) {
                        SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.1f }, NPC.Center);
                        if (!VaultUtils.isClient) {
                            Vector2 ver = NPC.Center.To(player.Center).UnitVector() * 12.5f;
                            Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, ver
                                , ModContent.ProjectileType<GhostFireProj>(), NPC.damage / 2, 2);
                        }
                    }
                    break;
                case 1: //爆兵：14 帧抛一发火球
                    if ((int)seq == 14 + (Slot - 3) * 4) {
                        SoundEngine.PlaySound(SoundID.Item20, NPC.Center);
                        if (!VaultUtils.isClient) {
                            Vector2 ver = NPC.Center.To(player.Center).UnitVector() * 7.5f;
                            Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, ver
                                , ModContent.ProjectileType<OblivionFireOrb>(), NPC.damage / 2, 2);
                        }
                    }
                    break;
                default: //疫医：12 帧一次治疗脉冲
                    if ((int)seq == 12) {
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.5f }, NPC.Center);
                        if (!VaultUtils.isClient && boss.life < boss.lifeMax) {
                            boss.life = Math.Min(boss.life + 2500, boss.lifeMax);
                            boss.netUpdate = true;
                        }
                    }
                    break;
            }

            if (seq > 44) {
                NPC.ai[3] = 0;
            }
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
            //疫医引导期：贴身治疗光晕
            if (Role == 2 && NPC.ai[2] > HealCooldown && ACMAsset.SoftGlow != null) {
                float p = (NPC.ai[2] - HealCooldown) / HealChannel;
                Color glow = new Color(255, 130, 200, 0) * (0.5f * (float)Math.Sin(p * MathHelper.Pi));
                spriteBatch.Draw(ACMAsset.SoftGlow, NPC.Center - Main.screenPosition, null, glow
                    , 0, ACMAsset.SoftGlow.Size() / 2, 2.6f, SpriteEffects.None, 0);
            }
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
    // ai[0]=模式: 0 直线 / 1 蛇形波(ai[1]=相位, ai[2]=波幅) / 2 定点漂浮鬼火
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

        private int Mode => (int)Projectile.ai[0];

        public override void AI() {
            //蛇形波：以 timeLeft 推导相位（无本地状态, 各端一致）, 垂直于航向做正弦增量偏移
            if (Mode == 1) {
                float amp = Projectile.ai[2] > 0 ? Projectile.ai[2] : 110f;
                const float k = 0.09f;
                float t = 300 - Projectile.timeLeft;
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                float delta = MathF.Sin((t + 1) * k + Projectile.ai[1]) - MathF.Sin(t * k + Projectile.ai[1]);
                Projectile.position += perp * delta * amp;
            }
            //定点漂浮：减速悬停成为区域封锁
            else if (Mode == 2) {
                Projectile.velocity *= 0.90f;
            }

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

            //漂浮鬼火: 呼吸明暗 + 微弱 bob（纯视觉偏移）
            Vector2 bob = Vector2.Zero;
            if (Mode == 2) {
                float breath = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.whoAmI);
                baseColor *= breath;
                bob = new Vector2(0, MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f + Projectile.whoAmI) * 5f);
            }

            float alpha = 0.4f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float fade = alpha * (1f - i / (float)Projectile.oldPos.Length);
                Main.spriteBatch.Draw(tex, pos, rect, baseColor * fade, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center + bob - Main.screenPosition;
            Main.spriteBatch.Draw(tex, drawPos, rect, baseColor, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, rect, baseColor * 0.3f, Projectile.rotation, origin, scale * 1.4f, SpriteEffects.None, 0f);
            return false;
        }
    }

    //=========================================================
    // 湮灭火球（爆兵投掷 / 鬼门慢速环）
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
    // 冲撞残影（纯视觉定格残像，伤害为 0；冲刺伤害归本体速度门控）
    //=========================================================
    internal class HoqingShadow : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hoqings/Hoqing";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 100;
            Projectile.friendly = false;
            Projectile.hostile = false;
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
                //统一预警色: 尸绿呼吸圈 (TelegraphColors.GhostGreen, 持续危险)
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
    // 脓球（高空坠落 → 落点成潭；落点即预告圈锚定位置）
    // ai[0] = 目标 Y (世界坐标), ai[1] = 潭伤害
    //=========================================================
    internal class HoqingSputumGlob : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hoqings/OblivionFireOrb";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            Projectile.rotation += 0.25f;
            //坠落加速（重量感）
            if (Projectile.velocity.Y < 34f) {
                Projectile.velocity.Y += 1.6f;
            }

            if (!VaultUtils.isServer && Main.rand.NextBool()) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch
                    , -Projectile.velocity * 0.06f, 130, new Color(140, 255, 130), 1.5f);
                d.noGravity = true;
            }

            //到达锚定高度 → 成潭
            if (Projectile.Center.Y >= Projectile.ai[0]) {
                Projectile.position.Y = Projectile.ai[0] - Projectile.height / 2f;
                SoundEngine.PlaySound(SoundID.Item104 with { Pitch = -0.2f }, Projectile.Center);
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero
                        , ModContent.ProjectileType<SputumPool>(), (int)Projectile.ai[1], 0f, Main.myPlayer);
                }
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float fade = 0.4f * (1f - i / (float)Projectile.oldPos.Length);
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, new Color(120, 240, 120, 0) * fade, Projectile.rotation, origin, Projectile.scale * 0.9f, SpriteEffects.None, 0);
            }
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, new Color(160, 255, 150), Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
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
                        , ModContent.NPCType<GhostFire>(), ai0: boss, ai1: Main.rand.Next(6), target: Main.npc[boss].target);
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

    //=========================================================
    // 魂焰柱（疫风走廊）：预警 45f 鬼影 → 激活 50f 实体魂焰 → 20f 消散
    // ai[0] = 计时; SoulFlame 着色器柱体绘制
    //=========================================================
    internal class HoqingSoulPillar : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hoqings/GhostFire";

        internal const int WarnTime = 45;
        internal const int ActiveTime = 50;
        internal const int FadeTime = 20;

        private static Asset<Effect> flameRef;
        private static Effect FlameFX {
            get {
                if (Main.dedServ) {
                    return null;
                }
                flameRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/HoqingSoulFlame", AssetRequestMode.ImmediateLoad);
                return flameRef?.Value;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 84;
            Projectile.height = 860;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = WarnTime + ActiveTime + FadeTime;
        }

        private float T => Projectile.ai[0];

        public override bool CanHitPlayer(Player target) {
            return T >= WarnTime && T < WarnTime + ActiveTime;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<Buffs.HoqingDecline>(), 180);
        }

        public override void AI() {
            Projectile.ai[0]++;

            //点燃瞬间：音效 + 底部爆尘
            if ((int)T == WarnTime) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.1f }, Projectile.Center);
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 14; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-30, 30), Main.rand.NextFloat(-380, 380)),
                            DustID.GreenTorch, new Vector2(0, -Main.rand.NextFloat(2f, 6f)), 100, new Color(150, 255, 150), 2.0f);
                        d.noGravity = true;
                    }
                }
            }

            //激活期缓慢上涌的焰尘
            if (!VaultUtils.isServer && T >= WarnTime && T < WarnTime + ActiveTime && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-26, 26), Main.rand.NextFloat(-400, 420)),
                    DustID.GreenTorch, new Vector2(0, -Main.rand.NextFloat(1.5f, 4f)), 130, new Color(130, 255, 140), 1.5f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect fx = FlameFX;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            if (fx == null || pixel == null) {
                return false;
            }

            bool warn = T < WarnTime;
            float intensity;
            if (warn) {
                intensity = MathHelper.Clamp(T / 12f, 0f, 1f);
            }
            else if (T < WarnTime + ActiveTime) {
                intensity = MathHelper.Clamp((T - WarnTime) / 8f, 0f, 1f);
            }
            else {
                intensity = MathHelper.Clamp(1f - (T - WarnTime - ActiveTime) / FadeTime, 0f, 1f);
            }

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(intensity);
            fx.Parameters["uColorCore"]?.SetValue(new Color(255, 220, 150).ToVector4());
            fx.Parameters["uColorOuter"]?.SetValue(TelegraphColors.GhostGreen.ToVector4());
            fx.Parameters["uFlow"]?.SetValue(1.1f);
            fx.Parameters["uNoiseScale"]?.SetValue(2.6f);
            fx.Parameters["uPillar"]?.SetValue(1f);
            fx.Parameters["uWarn"]?.SetValue(warn ? 1f : 0f);
            fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.173f);

            //世界矩形 → 屏幕像素 (顶点契约: world - screenPosition, 批用 GameViewMatrix)
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[1] = ACMShaders.NoiseTexture;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            float drawHalfW = warn ? 46f : 62f;
            Vector2 topLeft = Projectile.Center - new Vector2(drawHalfW, Projectile.height / 2f + 30f) - Main.screenPosition;
            Rectangle dest = new((int)topLeft.X, (int)topLeft.Y, (int)(drawHalfW * 2), Projectile.height + 60);
            sb.Draw(pixel, dest, Color.White);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
            return false;
        }
    }

    //=========================================================
    // 魂灯（环阵）：现形 40f（无伤害）→ 3 波交替径向/切向弹 → 崩解
    // ai[0] 未用, ai[1]/ai[2] = 环心世界坐标; 位次由自身角度推导
    //=========================================================
    internal class HoqingSoulLantern : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hoqings/GhostFire";

        internal const int Lifetime = 176;
        internal const int RevealTime = 40;

        private static Asset<Effect> flameRef;
        private static Effect FlameFX {
            get {
                if (Main.dedServ) {
                    return null;
                }
                flameRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/HoqingSoulFlame", AssetRequestMode.ImmediateLoad);
                return flameRef?.Value;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false; //灯体不造成接触伤害（公平阀门）
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Lifetime;
        }

        private int T => Lifetime - Projectile.timeLeft;
        private Vector2 RingCenter => new(Projectile.ai[1], Projectile.ai[2]);
        private int RingIndex {
            get {
                float ang = (Projectile.Center - RingCenter).ToRotation();
                return (int)MathF.Round(((ang + MathHelper.TwoPi) % MathHelper.TwoPi) / MathHelper.TwoPi * 8f) % 8;
            }
        }

        public override void AI() {
            //灯位轻微呼吸浮动
            Projectile.velocity = Vector2.Zero;

            if (T == RevealTime) {
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.5f, Volume = 0.6f }, Projectile.Center);
            }

            //三波齐射：径向（奇数灯）与切向（偶数灯）交替
            if (!VaultUtils.isClient && (T == RevealTime || T == RevealTime + 34 || T == RevealTime + 68)) {
                int wave = (T - RevealTime) / 34;
                Vector2 toCenter = (RingCenter - Projectile.Center).SafeNormalize(Vector2.UnitY);
                bool radial = (RingIndex + wave) % 2 == 0;
                Vector2 dir = radial ? toCenter : toCenter.RotatedBy(MathHelper.PiOver2);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, dir * 8.5f
                    , ModContent.ProjectileType<GhostFireProj>(), Projectile.damage, 2f, Main.myPlayer);
            }

            if (!VaultUtils.isServer && T >= RevealTime && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14, 20),
                    DustID.GreenTorch, new Vector2(0, -Main.rand.NextFloat(0.5f, 2f)), 140, new Color(150, 255, 160), 1.3f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rect = VaultUtils.GetRectangle(tex, (int)(Main.GlobalTimeWrappedHourly * 8 + Projectile.whoAmI) % 4, 4);

            //现形期: DissolveBurn 聚形
            if (T < RevealTime) {
                float threshold = 1f - T / (float)RevealTime;
                WeaponVFX.ApplyDissolveBurn(tex, Projectile.Center, rect, new Color(170, 255, 180),
                    0f, rect.Size() / 2f, 1f,
                    threshold: threshold, intensity: 0.9f,
                    edgeColor: new Color(TelegraphColors.GhostGreen.R, TelegraphColors.GhostGreen.G, TelegraphColors.GhostGreen.B, (byte)220),
                    edgeWidth: 0.12f, noiseScale: 2.4f);
                return false;
            }

            float fade = Projectile.timeLeft < 20 ? Projectile.timeLeft / 20f : 1f;

            //灯火苗 (SoulFlame 火苗模式)
            Effect fx = FlameFX;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            if (fx != null && pixel != null) {
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uIntensity"]?.SetValue(0.9f * fade);
                fx.Parameters["uColorCore"]?.SetValue(new Color(255, 230, 170).ToVector4());
                fx.Parameters["uColorOuter"]?.SetValue(TelegraphColors.GhostGreen.ToVector4());
                fx.Parameters["uFlow"]?.SetValue(1.4f);
                fx.Parameters["uNoiseScale"]?.SetValue(2.2f);
                fx.Parameters["uPillar"]?.SetValue(0f);
                fx.Parameters["uWarn"]?.SetValue(0f);
                fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.291f);

                SpriteBatch sb = Main.spriteBatch;
                GraphicsDevice gd = Main.graphics.GraphicsDevice;
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
                gd.Textures[1] = ACMShaders.NoiseTexture;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                Vector2 topLeft = Projectile.Center - new Vector2(30f, 96f) - Main.screenPosition;
                sb.Draw(pixel, new Rectangle((int)topLeft.X, (int)topLeft.Y, 60, 116), Color.White);

                sb.End();
                ACMShaders.RestoreDefaultBatch(sb);
            }

            //灯核
            if (ACMAsset.SoftGlow != null) {
                Color glow = new Color(160, 255, 170, 0) * (0.75f * fade);
                Main.spriteBatch.Draw(ACMAsset.SoftGlow, Projectile.Center - Main.screenPosition, null, glow
                    , 0f, ACMAsset.SoftGlow.Size() / 2f, 1.5f, SpriteEffects.None, 0f);
            }
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rect, new Color(190, 255, 200) * fade
                , 0f, rect.Size() / 2f, 1f, SpriteEffects.None, 0f);
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
