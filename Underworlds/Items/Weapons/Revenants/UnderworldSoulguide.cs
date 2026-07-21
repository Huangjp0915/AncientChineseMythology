using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Revenants
{
    /// <summary>
    /// 黄泉引魂弓 - 记名引渡弓，远程弓类武器
    /// 箭矢优先强追踪已记名 (业力 ≥1) 的目标; 每第 4 箭必出双箭;
    /// 击杀已记名者引渡出渡魂魄 (<see cref="SoulFerryWisp"/>) 续攻。业满宣判见 <see cref="RevenantKarma"/>。
    /// </summary>
    public class UnderworldSoulguide : ModItem
    {
        /// <summary>射箭计数 (owner 侧, 每第 4 箭必出双箭)。</summary>
        private int arrowCounter;

        public override void SetDefaults() {
            Item.damage = 56;
            Item.crit = 8;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 24;
            Item.height = 58;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 14f;
            Item.useAmmo = AmmoID.Arrow;
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-2, 0);
        }

        public override void HoldItem(Player player) {
            //下一箭将出双箭: 弓臂鬼绿渐亮预告 (决策点可读, ≤1/3 帧)
            if (arrowCounter == 3 && !Main.dedServ && Main.rand.NextBool(3)) {
                Vector2 pos = player.MountedCenter + new Vector2(
                    player.direction * Main.rand.NextFloat(8f, 22f), Main.rand.NextFloat(-16f, 16f));
                Dust d = Dust.NewDustPerfect(pos, DustID.RainbowMk2, new Vector2(0f, -0.9f), 130,
                    new Color(120, 230, 140), 0.95f);
                d.noGravity = true;
                d.fadeIn = 0.5f;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //将普通箭转换为引魂箭弹幕
            int soulArrow = ModContent.ProjectileType<SoulguideArrow>();
            Projectile.NewProjectile(source, position, velocity, soulArrow, damage, knockback, player.whoAmI);

            //每第 4 箭必出双箭 (副箭偏 ±6°, 伤害 0.6×) —— 固定节奏替代旧 1/3 随机, 可读可预期
            arrowCounter++;
            if (arrowCounter >= 4) {
                arrowCounter = 0;
                Vector2 sideSpeed = velocity.RotatedBy(MathHelper.ToRadians(Main.rand.NextBool() ? 6f : -6f));
                Projectile.NewProjectile(source, position, sideSpeed, soulArrow, (int)(damage * 0.6f), knockback, player.whoAmI);
            }
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<NetherBar>(), 8)
                .AddIngredient<SoulFragment>(8)
                .AddIngredient<UmbralStoneItem>(28)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    /// <summary>
    /// 引魂箭弹幕 - 记名追踪箭矢: 优先强追踪业力最高的已记名目标 (锁定时箭体转金),
    /// 无业目标仅弱引; 命中 +1 业, 击杀已记名者引渡出 <see cref="SoulFerryWisp"/>。
    /// 使用ACMAsset.SoftGlow和ACMAsset.BlankStar叠加绘制
    /// </summary>
    public class SoulguideArrow : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/UnderworldSoulguide";

        private ref float HomingTimer => ref Projectile.ai[0];

        /// <summary>本帧是否锁定已记名 (业≥1) 目标 (纯视觉标记, 各端 AI 自行计算)。</summary>
        private bool markedLock;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.arrow = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            HomingTimer++;

            //飞行0.3秒后开始记名追踪: 已记名 (业≥1) 目标强追, 无业目标仅弱引
            if (HomingTimer > 18f) {
                NPC target = FindKarmaTarget(out bool marked);
                markedLock = marked;
                if (target != null) {
                    float lerpStrength = marked ? 0.12f : 0.03f;
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), lerpStrength);
                }
            }

            //幽蓝色光照
            Lighting.AddLight(Projectile.Center, 0.3f, 0.5f, 0.7f);

            //幽魂拖尾粒子
            if (Main.rand.NextBool(2)) {
                Dust soul = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f,
                    4, 4, DustID.Wraith,
                    -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f,
                    120, default, Main.rand.NextFloat(1.0f, 1.4f)
                );
                soul.noGravity = true;
            }

            //淡蓝光点
            if (Main.rand.NextBool(3)) {
                Dust glow = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(6, 6),
                    2, 2, DustID.BlueTorch,
                    0f, -0.3f, 100, default, 0.7f
                );
                glow.noGravity = true;
            }
        }

        /// <summary>
        /// 记名索敌: 500px 内优先选业力最高者 (强追踪, marked=true; 同层取更近者);
        /// 无已记名目标时退化为 400px 内最近者 (弱追踪)。
        /// </summary>
        private NPC FindKarmaTarget(out bool marked) {
            NPC bestMarked = null;
            int bestKarma = 0;
            float bestMarkedDist = 500f;
            NPC closest = null;
            float closestDist = 400f;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);

                if (dist < 500f) {
                    int karma = npc.GetGlobalNPC<RevenantKarmaGlobalNPC>().Karma;
                    if (karma >= 1 && (karma > bestKarma || (karma == bestKarma && dist < bestMarkedDist))) {
                        bestKarma = karma;
                        bestMarkedDist = dist;
                        bestMarked = npc;
                    }
                }
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }

            marked = bestMarked != null;
            return bestMarked ?? closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //冥火灼烧 (系列语言统一)
            target.AddBuff(BuffID.ShadowFlame, 120);

            //记名: +1 业
            RevenantKarma.AddKarma(Projectile, target, 1);

            //渡魂魄: 击杀已记名 (业≥1) 者, 引渡出 2 枚追魂魄 (仅 owner 侧生成, 同屏 ≤6)
            if (Main.myPlayer == Projectile.owner && target.life <= 0
                && target.GetGlobalNPC<RevenantKarmaGlobalNPC>().Karma >= 1) {
                SpawnFerryWisps(target);
            }

            //引魂效果：灵魂升腾粒子
            for (int i = 0; i < 12; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-5f, -2f));
                Dust soul = Dust.NewDustPerfect(
                    target.Center, DustID.Wraith, vel,
                    100, default, Main.rand.NextFloat(1.2f, 1.8f)
                );
                soul.noGravity = true;
            }

            //蓝色星光爆发
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust star = Dust.NewDustPerfect(
                    target.Center, DustID.BlueTorch, vel,
                    80, default, Main.rand.NextFloat(1.0f, 1.5f)
                );
                star.noGravity = true;
            }

            //引魂"标记"演出: 命中点青黄魂火径向辉光 + 冲击环 (走 ACMWeaponBurst, 更新阶段安全)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.SoulFire, scale: 0.8f, owner: Projectile.owner);
        }

        private void SpawnFerryWisps(NPC target) {
            int wispType = ModContent.ProjectileType<SoulFerryWisp>();

            //同 owner 渡魂魄同屏 ≤6 (手动遍历: 同帧新生成的也计入)
            int owned = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.type == wispType)
                    owned++;
            }

            int wispDamage = Math.Max(1, (int)(Projectile.damage * 0.3f));
            for (int i = 0; i < 2 && owned < 6; i++, owned++) {
                //初速向上偏的随机方向 (魂魄自尸身升腾)
                Vector2 vel = (-Vector2.UnitY).RotatedByRandom(1.1f) * 6f;
                Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, vel,
                    wispType, wispDamage, 1f, Projectile.owner);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float brightness = MathHelper.Clamp(Projectile.timeLeft / 30f, 0.25f, 1f);

            //BeamGrad 箭体光束 (锁定已记名目标时内芯转金 —— "记名"状态可读)
            Vector2 head = Projectile.Center;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            float beamLen = MathHelper.Clamp(Projectile.velocity.Length() * 3.2f, 36f, 130f);
            Color coreColor = markedLock ? new Color(255, 230, 150) : new Color(170, 230, 255);
            ACMShaders.DrawBeam(head - dir * beamLen, head + dir * 6f, halfWidth: 7f,
                core: coreColor, edge: new Color(40, 90, 190),
                intensity: brightness, flowSpeed: 2.2f, flowScale: 2.4f, coreSharp: 2.6f);

            //箭头呼吸柔光核 (廉价 SoftGlow, 不占全屏名额)
            float corePulse = (0.95f + MathF.Sin(HomingTimer * 0.22f) * 0.15f) * brightness;
            Color glowColor = (markedLock ? new Color(255, 230, 150) : new Color(140, 210, 255)) * 0.4f;
            WeaponVFX.DrawGlowBurst(head, corePulse, glowColor);

            //箭尖星光闪烁
            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                float pulse = (0.28f + MathF.Sin(HomingTimer * 0.2f) * 0.08f) * brightness;
                Color starColor = new Color(160, 225, 255) * 0.5f;
                starColor.A = 0;
                Main.EntitySpriteDraw(blankStar, head - Main.screenPosition, null, starColor, HomingTimer * 0.1f, starOrigin, pulse, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            //消散时灵魂升腾
            for (int i = 0; i < 8; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.Wraith,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, -1f),
                    100, default, Main.rand.NextFloat(1.0f, 1.5f)
                );
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 渡魂魄 - 击杀已记名 (业≥1) 敌人时自尸身引渡出的追魂小弹 (30% 箭伤, 同 owner 同屏 ≤6)。
    /// 升腾片刻后追踪敌人 (400px / lerp 0.08), 命中 +1 业 —— 让"记名→引渡"形成资源循环。
    /// 视觉: GhostGreen 短 ribbon 拖尾 + SoftGlow 呼吸核。
    /// </summary>
    public class SoulFerryWisp : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private ref float Timer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;   //灵体穿墙, 靠 timeLeft 自然消散
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, 0.15f, 0.4f, 0.2f);

            //升腾 12 帧后开始引渡追踪
            if (Timer > 12f) {
                NPC target = FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 9f, 0.08f);
                }
            }

            //幽魂微粒 (≤1/3 帧)
            if (Main.rand.NextBool(3)) {
                Dust soul = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    DustID.GreenTorch, -Projectile.velocity * 0.1f, 130, default, 0.8f);
                soul.noGravity = true;
            }
        }

        private NPC FindClosestNPC(float maxRange) {
            NPC closest = null;
            float closestDist = maxRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //渡魂续业: +1 业
            RevenantKarma.AddKarma(Projectile, target, 1);

            //鬼绿引渡演出 (走 ACMWeaponBurst, 更新阶段安全)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.GhostGreen, scale: 0.6f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            //GhostGreen 短 ribbon 拖尾 (外宽暗绿 + 内窄亮魂绿)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 5f,
                outerColor: new Color(30, 90, 60, 140), innerColor: new Color(170, 255, 190, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.2f);

            //SoftGlow 呼吸核
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                float pulse = 0.3f + MathF.Sin(Timer * 0.25f) * 0.07f;
                Color c = new Color(170, 255, 190) * 0.7f;
                c.A = 0;
                Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, c, 0f,
                    glow.Size() / 2f, pulse, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            //魂魄散逸
            for (int i = 0; i < 4; i++) {
                Dust death = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    DustID.GreenTorch,
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2.5f, -0.5f)),
                    120, default, Main.rand.NextFloat(0.8f, 1.2f));
                death.noGravity = true;
            }
        }
    }
}
