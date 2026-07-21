using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons
{
    /// <summary>
    /// 镇尸钉 (棺材钉) — 赢勾残片合成的旗舰投掷件 (民俗"七星钉封棺"幻想)。
    /// 快直投, 命中把钉留在敌人身上持续镇魂 (12%/45帧); 同一敌人钉满 7 根触发
    /// 【七星封棺】: 棺形封印合盖 → 北斗七星逐颗点亮 → 静默 → 起爆 (350%, 160px)。
    /// </summary>
    internal class CoffinNail : ModItem
    {
        public override string Texture => "AncientChineseMythology/Items/Weapons/CoffinNail";

        public override void SetDefaults() {
            Item.damage = 300;
            Item.DamageType = DamageClass.Melee;
            Item.width = 34;
            Item.height = 34;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 8f;
            Item.value = Item.buyPrice(0, 50, 0, 0);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1; // 基础掷声广播; 金属层在 Shoot 叠加
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;

            Item.shoot = ModContent.ProjectileType<CoffinNailProjectile>();
            Item.shootSpeed = 16f;
            Item.consumable = false;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);

            Projectile.NewProjectile(source, player.Center + dir * 30f, velocity, type, damage, knockback, player.whoAmI);

            // 高频金属出手层 (低频掷击由 UseSound 广播)
            SoundEngine.PlaySound(SoundID.Item39 with { Volume = 0.3f, Pitch = 0.5f + Main.rand.NextFloat(-0.1f, 0.1f) }, player.Center);

            // 出手微后坐 (钉的份量)
            player.velocity -= dir * 0.8f;
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<CoffinNailFragment>(1)
                .AddIngredient(ItemID.SoulofNight, 8)
                .AddIngredient(ItemID.Ectoplasm, 6)
                .AddIngredient(ItemID.HallowedBar, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "CoffinNailDesc1", "[c/8B0000:镇尸钉 — 僵尸始祖所惧]"));
            tooltips.Add(new TooltipLine(Mod, "CoffinNailDesc2", "[c/DC143C:钉入敌身持续镇魂]"));
            tooltips.Add(new TooltipLine(Mod, "CoffinNailDesc3", "[c/B22222:七星钉满，封棺起爆]"));
        }

        public override Color? GetAlpha(Color lightColor) {
            return Color.Lerp(lightColor, Color.DarkRed, 0.3f);
        }
    }

    /// <summary>
    /// 封棺余波 (每 NPC 实例): 七星封棺起爆后 2s 内不可再被钉 (防循环锁死)。
    /// owner 端设置与消费 (钉入决策本就在 owner 端)。
    /// </summary>
    public class CoffinSealWardNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public int WardTimer;

        public override void PostAI(NPC npc) {
            if (WardTimer > 0)
                WardTimer--;
        }
    }

    /// <summary>
    /// 镇尸钉弹幕: 快直投 (extraUpdates 2, 无制导无弹跳) → 命中钉入敌身 (stuck, 12%/45帧镇魂 tick,
    /// 钉入音高随根数上行) → 第 7 根触发七星封棺仪式。
    /// ai[0]=状态 (0 飞行 / 1 钉入 / 2 弹飞), ai[1]=宿主, ai[2]=原始伤害备份; 钉入后 velocity 存宿主相对偏移。
    /// </summary>
    public class CoffinNailProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Items/Weapons/CoffinNail";

        private const int NailsToSeal = 7;
        private const int StuckUpdates = 1800;      // 钉驻留 (extraUpdates 2 → 10s)
        private const int DotCooldown = 135;        // 镇魂 tick 间隔 (45 真实帧)

        private static readonly Color LethalRed = new(250, 40, 56);
        private static readonly Color DeepRed = new(90, 8, 12);

        private ref float State => ref Projectile.ai[0];
        private ref float HostIndex => ref Projectile.ai[1];
        private ref float StoredDamage => ref Projectile.ai[2];
        private ref float TickFlash => ref Projectile.localAI[0]; // 镇魂 tick 震颤 (视觉)

        private bool Stuck => State == 1f;

        private NPC Host {
            get {
                int idx = (int)HostIndex;
                if (idx < 0 || idx >= Main.maxNPCs)
                    return null;
                NPC npc = Main.npc[idx];
                return npc.active && npc.life > 0 ? npc : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee; // 与物品职业一致 (旧版 Ranged 定位分裂修复)
            Projectile.penetrate = -1;                 // 命中转钉入而非消亡
            Projectile.timeLeft = 270;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = DotCooldown;
        }

        public override bool ShouldUpdatePosition() => !Stuck;

        public override void AI() {
            if (TickFlash > 0f)
                TickFlash -= 0.06f;

            switch (State) {
                case 0f: // 飞行: 死直线 (钉的果断), 尾焰节流
                    Projectile.rotation = Projectile.velocity.ToRotation();
                    if (Main.rand.NextBool(4)) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                            -Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.5f, 0.5f), 110, Color.Crimson, 1.0f);
                        d.noGravity = true;
                    }
                    Lighting.AddLight(Projectile.Center, 0.55f, 0.1f, 0.1f);
                    break;

                case 1f: // 钉入: 跟随宿主, velocity=相对偏移
                    NPC host = Host;
                    if (host == null) {
                        Eject();
                        return;
                    }
                    Projectile.Center = host.Center + Projectile.velocity;
                    Lighting.AddLight(Projectile.Center, 0.4f, 0.07f, 0.07f);
                    if (Main.rand.NextBool(30)) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                            new Vector2(0, Main.rand.NextFloat(0.5f, 1.2f)), 120, default, 0.9f);
                    }
                    break;

                default: // 弹飞: 重力翻滚淡出 (纯视觉)
                    Projectile.velocity.Y += 0.35f;
                    Projectile.velocity.X *= 0.99f;
                    Projectile.rotation += 0.4f * Math.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X);
                    Projectile.alpha += 6;
                    if (Projectile.alpha >= 255)
                        Projectile.Kill();
                    break;
            }
        }

        public override bool? CanHitNPC(NPC target) {
            if (State == 2f)
                return false;
            if (Stuck)
                return target.whoAmI == (int)HostIndex ? null : false; // 钉入后只 tick 宿主
            return null;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (Stuck) {
                // 镇魂 tick: 不暴击不击退 (仪式感的稳定读数; 数值本体已在钉入时降为 12%)
                modifiers.DisableCrit();
                modifiers.Knockback *= 0f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Stuck) {
                // 镇魂 tick 反馈: 钉身震颤 + 微血尘
                TickFlash = 1f;
                if (Main.rand.NextBool(2))
                    SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.2f, Pitch = 0.3f }, target.Center);
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                        Main.rand.NextVector2Circular(1.5f, 1.5f), 80, default, 1.0f);
                    d.noGravity = true;
                }
                return;
            }
            if (State != 0f)
                return;

            // ===== 飞行钉命中: 钉入 (owner 端决策, 状态经 ai/velocity 同步) =====
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.6f, Pitch = -0.1f }, target.Center);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Fatal, 1f, Projectile.owner);
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    Main.rand.NextVector2Circular(4f, 4f), 60, Color.DarkRed, Main.rand.NextFloat(1.2f, 1.8f));
                d.noGravity = true;
            }

            if (Projectile.owner != Main.myPlayer)
                return;

            var ward = target.GetGlobalNPC<CoffinSealWardNPC>();
            if (ward.WardTimer > 0 || NPCID.Sets.ProjectileNPC[target.type]) {
                // 封棺余波中 (或弹幕型 NPC): 不钉入, 直接弹飞
                Eject();
                return;
            }

            // 钉入: 偏移收进宿主身体内
            State = 1f;
            HostIndex = target.whoAmI;
            StoredDamage = Projectile.damage;
            Vector2 offset = Projectile.Center - target.Center;
            offset.X = MathHelper.Clamp(offset.X, -target.width * 0.38f, target.width * 0.38f);
            offset.Y = MathHelper.Clamp(offset.Y, -target.height * 0.38f, target.height * 0.38f);
            Projectile.velocity = offset;
            Projectile.tileCollide = false;
            Projectile.timeLeft = StuckUpdates;
            Projectile.damage = Math.Max(1, (int)(StoredDamage * 0.12f));
            Projectile.netUpdate = true;

            // 数钉: 音高上行的"钉、钉、钉"听觉读条
            int count = CountNailsOn(target.whoAmI);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = -0.3f + count * 0.08f }, target.Center);

            if (count >= NailsToSeal)
                TriggerSeal(target);
        }

        private int CountNailsOn(int hostWho) {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == Type && p.owner == Projectile.owner
                    && p.ai[0] == 1f && (int)p.ai[1] == hostWho)
                    count++;
            }
            return count;
        }

        /// <summary>七星封棺 (第 7 钉触发, owner 端): 生成封棺仪式弹幕, 全部钉弹飞。</summary>
        private void TriggerSeal(NPC target) {
            target.GetGlobalNPC<CoffinSealWardNPC>().WardTimer = 120;

            int sealDamage = Math.Max(1, (int)(StoredDamage * 3.5f));
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<CoffinSealRite>(), sealDamage, 9f, Projectile.owner, target.whoAmI);

            // 所有钉弹飞 (含自己)
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == Type && p.owner == Projectile.owner
                    && p.ai[0] == 1f && (int)p.ai[1] == target.whoAmI
                    && p.ModProjectile is CoffinNailProjectile nail) {
                    nail.Eject();
                }
            }
        }

        /// <summary>弹飞 (宿主死亡/封棺/余波拒钉): 转纯视觉翻滚坠落。</summary>
        public void Eject() {
            State = 2f;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, 150);
            Projectile.velocity = Main.rand.NextVector2CircularEdge(4f, 4f) - new Vector2(0f, 3f);
            Projectile.netUpdate = true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            // 撞墙即钉在墙上消散 (不再弹跳)
            Collision.HitTiles(Projectile.position, oldVelocity, Projectile.width, Projectile.height);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.55f, Pitch = 0.2f }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    -oldVelocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.7) * Main.rand.NextFloat(1f, 3f),
                    100, Color.Crimson, 1.1f);
                d.noGravity = true;
            }
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            // 血色双层拖尾 (仅飞行)
            if (State == 0f)
                WeaponVFX.DrawProjectileTrail(Projectile, 9f,
                    DeepRed with { A = 160 }, LethalRed with { A = 200 },
                    uvScroll: -(float)Main.GlobalTimeWrappedHourly * 1.4f);

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            float alpha = 1f - Projectile.alpha / 255f;
            Color drawColor = Color.Lerp(lightColor, Color.Red, 0.5f) * alpha;
            // 贴图本身斜置 → 旋转补偿; 钉入后 tick 震颤抖动
            float rotation = Projectile.rotation + MathHelper.ToRadians(-50);
            if (Stuck && TickFlash > 0f)
                drawPosition += Main.rand.NextVector2Circular(2.5f, 2.5f) * TickFlash;

            Main.EntitySpriteDraw(texture, drawPosition, null, drawColor, rotation,
                texture.Size() / 2f, Projectile.scale, SpriteEffects.None, 0);

            // 红晕 (钉入后随 tick 呼吸)
            float glowStrength = Stuck ? 0.25f + TickFlash * 0.45f : 0.3f;
            Color glow = LethalRed * (glowStrength * alpha);
            glow.A = 0;
            Main.EntitySpriteDraw(texture, drawPosition, null, glow, rotation,
                texture.Size() / 2f, Projectile.scale * 1.2f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (State == 0f) {
                for (int i = 0; i < 8; i++) {
                    Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                        DustID.Blood, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, Color.DarkRed,
                        Main.rand.NextFloat(1f, 1.8f));
                    d.noGravity = true;
                }
            }
        }
    }

    /// <summary>
    /// 七星封棺仪式 (旗舰峰值弹幕): 跟随宿主 — 合盖扫线 (0~30f) → 北斗点星 (随合盖逐颗) →
    /// 静默 (42~48f) → 起爆 (48f, 350% / 160px) → 余辉。绘制走 CoffinNailSeal.fx 屏幕空间 decal。
    /// ai[0]=宿主。演出由 timeLeft 驱动, 各端确定性一致。
    /// </summary>
    public class CoffinSealRite : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int LifeTime = 78;
        private const int LidCloseEnd = 30;   // 合盖完成帧
        private const int StarsEnd = 42;      // 七星点亮完成帧
        private const int BoomFrame = 48;     // 起爆帧 (前有 6f 静默)
        private const float BlastRadius = 160f;

        private static readonly Color LethalRed = new(250, 40, 56);
        private static readonly Color BoneWhite = new(235, 230, 210);

        private int Frame => LifeTime - Projectile.timeLeft;
        private bool _boomed;

        private NPC Host {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs)
                    return null;
                NPC npc = Main.npc[idx];
                return npc.active && npc.life > 0 ? npc : null;
            }
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.CoffinSealRite.DisplayName",
                () => "Seven-Star Coffin Seal");
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false; // 仅起爆窗口开启
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 起爆单次判定
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            NPC host = Host;
            if (host != null && Frame < BoomFrame)
                Projectile.Center = host.Center;

            int frame = Frame;

            if (frame == 1)
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.8f, Pitch = -0.4f }, Projectile.Center);

            // 合盖期: 宿主被封印减速 + 合盖尘
            if (frame <= StarsEnd && host != null) {
                if (Projectile.owner == Main.myPlayer && frame % 12 == 0)
                    host.AddBuff(BuffID.Slow, 90);

                if (frame <= LidCloseEnd && Main.rand.NextBool(2)) {
                    // 棺沿落尘: 沿扫线高度两侧洒落
                    float lidY = MathHelper.Lerp(-1f, 1f, frame / (float)LidCloseEnd);
                    Vector2 pos = host.Center + new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), lidY) * (host.height * 0.65f + 24f);
                    Dust d = Dust.NewDustPerfect(pos, DustID.Torch, new Vector2(0, 0.8f), 110, Color.Crimson, 1.1f);
                    d.noGravity = true;
                }
            }

            // 点星帧: 每颗星一记上行音阶 ping (7 颗分布在 8~StarsEnd)
            if (frame >= 8 && frame <= StarsEnd && (frame - 8) % 5 == 0) {
                int starIdx = (frame - 8) / 5;
                if (starIdx < 7)
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = -0.2f + starIdx * 0.12f }, Projectile.Center);
            }

            // 静默节拍 (42~48f): 无声无尘 — 爆发前的吸气
            if (frame == BoomFrame && !_boomed) {
                _boomed = true;
                Detonate();
            }
        }

        private void Detonate() {
            // 起爆帧冲击链: 音 → 震 → Burst → 尘 (伤害经 friendly 窗口由命中管线结算)
            Projectile.friendly = true;

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.25f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.7f, Pitch = -0.5f }, Projectile.Center);
            WeaponVFX.AddScreenShake(Projectile.Center, 5f);

            ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center,
                ACMWeaponBurst.Fatal, 2.2f, Projectile.owner);

            for (int i = 0; i < 26; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(9f, 9f) * Main.rand.NextFloat(0.35f, 1f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool(3) ? DustID.Bone : DustID.Blood,
                    vel, 60, default, Main.rand.NextFloat(1.4f, 2.4f));
                d.noGravity = i < 18;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 起爆帧: 160px 圆域判定
            if (Frame < BoomFrame || Frame > BoomFrame + 4)
                return false;
            return Vector2.Distance(targetHitbox.Center.ToVector2(), Projectile.Center) <= BlastRadius;
        }

        public override bool PreDraw(ref Color lightColor) {
            int frame = Frame;
            float lidProgress = MathHelper.Clamp(frame / (float)LidCloseEnd, 0f, 1f);
            // 缓入缓出的合盖 (棺盖的份量)
            lidProgress = lidProgress * lidProgress * (3f - 2f * lidProgress);
            float stars = MathHelper.Clamp((frame - 8) / 5f, 0f, 7f);
            // 静默期微收缩 (爆前吸气), 起爆闪, 余辉衰减
            float silence = frame >= StarsEnd && frame < BoomFrame ? 1f - (frame - StarsEnd) / (float)(BoomFrame - StarsEnd) * 0.25f : 1f;
            float flash = frame >= BoomFrame ? MathHelper.Clamp(1f - (frame - BoomFrame) / 14f, 0f, 1f) : 0f;
            float fadeOut = frame > BoomFrame ? MathHelper.Clamp(1f - (frame - BoomFrame) / (float)(LifeTime - BoomFrame), 0f, 1f) : 1f;

            NPC host = Host;
            float coffinHalfHeight = MathF.Max(host?.height ?? 80f, 80f) * 0.95f + 26f;

            Effect fx = WeaponVFX.GetEffect("CoffinNailSeal");
            if (fx != null) {
                ACMShaders.WorldDecalParams(Projectile.Center, coffinHalfHeight * silence,
                    out Vector2 uvCenter, out float radiusFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uIntensity"]?.SetValue(0.95f * fadeOut);
                fx.Parameters["uCenter"]?.SetValue(uvCenter);
                fx.Parameters["uRadius"]?.SetValue(radiusFrac);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uProgress"]?.SetValue(lidProgress);
                fx.Parameters["uStars"]?.SetValue(stars);
                fx.Parameters["uFlash"]?.SetValue(flash);
                fx.Parameters["uColorMain"]?.SetValue(LethalRed.ToVector4());
                fx.Parameters["uColorRim"]?.SetValue(BoneWhite.ToVector4());
                ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
            }
            else {
                // 退化: 柔光 + 收拢环
                WeaponVFX.DrawGlowBurst(Projectile.Center, 1.5f * fadeOut, LethalRed * 0.7f);
                WeaponVFX.DrawShockwaveRing(Projectile.Center, coffinHalfHeight * (1.2f - lidProgress * 0.4f), 14f,
                    0.6f * fadeOut, BoneWhite, LethalRed);
            }

            // 起爆帧的冲击环 + 径向辉光 (走名额契约, 满则自动退化)
            if (flash > 0f) {
                float ringT = (frame - BoomFrame) / 14f;
                WeaponVFX.DrawShockwaveRing(Projectile.Center, BlastRadius * (0.3f + ringT * 1.1f), 18f,
                    flash * 0.9f, BoneWhite, LethalRed);
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.12f, flash * 0.8f, LethalRed, 7f);
            }

            return false;
        }
    }
}
