using AncientChineseMythology.Celestias.Boss.Aokins;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins.Items
{
    /// <summary>
    /// 焚天龙枪 — 敖钦掉落的长枪近战
    /// 焰纹突刺、焚风 trail、点燃叠层（最高 8 层）
    /// </summary>
    public class InfernoDragonSpear : ModItem
    {
        private int thrustCount;

        public override void SetDefaults() {
            Item.damage = 350;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 70;
            Item.useTime = Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6.5f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Yellow;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<InfernoDragonSpearThrust>();
            Item.shootSpeed = 16f;
            Item.crit = 12;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.Gungnir;

        public override void HoldItem(Player player) {
            if (Main.rand.NextBool(5)) {
                Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(50, 50);
                AokinHelper.CreateFireTrail(dustPos, Vector2.Zero, 0.7f);
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            thrustCount++;
            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);

            Projectile.NewProjectile(source, player.Center, direction * 18f, type, damage, knockback, player.whoAmI);

            if (thrustCount >= 4) {
                thrustCount = 0;
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.1f, Volume = 0.9f }, player.Center);

                Projectile.NewProjectile(source, player.Center, direction * 15f,
                    ModContent.ProjectileType<InfernoScorchGust>(),
                    (int)(damage * 1.45f), knockback * 1.4f, player.whoAmI);

                if (player.whoAmI == Main.myPlayer) {
                    player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(5, 12);
                }
            }

            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "DragonLore", "焰纹铸成的南海龙枪，枪尖缠绕焚风"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect", "突刺留下焚风 trail，每四次释放焚风冲击"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect2", "命中叠加点燃层数，层数越高灼烧越久、伤害越高"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect3", "点燃叠满 8 层时再次命中，引爆「焚天涅槃」赤焰新星"));
        }
    }

    /// <summary>焚天龙枪 — 焰纹突刺弹幕</summary>
    public class InfernoDragonSpearThrust : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float thrustProgress;
        private const float MaxExtend = 125f;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 16;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;

            thrustProgress += 0.13f;
            float extend;
            if (thrustProgress < 0.5f) {
                extend = ACMUtils.QuadOut(thrustProgress * 2f) * MaxExtend;
            }
            else {
                extend = (1f - ACMUtils.QuadIn((thrustProgress - 0.5f) * 2f)) * MaxExtend;
            }

            if (thrustProgress >= 1f) {
                Projectile.Kill();
                return;
            }

            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.rotation = direction.ToRotation();
            Projectile.Center = Owner.MountedCenter + direction * (32f + extend);
            Owner.direction = direction.X >= 0 ? 1 : -1;

            float armRotation = Projectile.rotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);

            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = Projectile.Center + direction * Main.rand.NextFloat(10f, 35f);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                var d = Dust.NewDustPerfect(dustPos, dustType);
                d.noGravity = true;
                d.scale = 1.4f + Main.rand.NextFloat(0.5f);
                d.velocity = -direction * Main.rand.NextFloat(2f, 5f) + direction.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-1.5f, 1.5f);
                d.alpha = 90;
            }

            AokinHelper.CreateFireTrail(Projectile.Center, direction * 6f, 0.5f);
            Lighting.AddLight(Projectile.Center, AokinHelper.DragonFlameRed.ToVector3() * 0.55f);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            var brand = target.GetGlobalNPC<InfernoDragonSpearGlobalNPC>();
            if (brand.infernoStacks > 0) {
                modifiers.FlatBonusDamage += brand.infernoStacks * 10;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            InfernoDragonSpearGlobalNPC.ApplyBrand(target, 1);
            InfernoDragonSpearGlobalNPC.TryDetonateNova(target, Projectile.owner, Projectile.damage, Projectile.knockBack, Projectile.GetSource_FromThis());

            for (int i = 0; i < 10; i++) {
                Vector2 vel = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.6f) * Main.rand.NextFloat(4f, 9f);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                var d = Dust.NewDustPerfect(target.Center, dustType);
                d.noGravity = true;
                d.scale = 1.8f;
                d.velocity = vel;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Item[ModContent.ItemType<InfernoDragonSpear>()].Value;

            // 焚风刃影 — 沿突刺轴向叠加 SlashBurst 喷发，呈现炽焰拉伸感
            if (ACMAsset.SlashBurst != null && thrustProgress > 0.05f) {
                float burstAlpha = MathF.Sin(thrustProgress * MathF.PI) * 0.85f;
                Vector2 burstPos = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * 70f;
                Color burstColor = Color.Lerp(AokinHelper.MoltenOrange, AokinHelper.BlazingGold, 0.4f) * burstAlpha;
                burstColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.SlashBurst, burstPos - Main.screenPosition, null, burstColor,
                    Projectile.rotation + MathHelper.PiOver2, new Vector2(ACMAsset.SlashBurst.Width / 2f, ACMAsset.SlashBurst.Height),
                    new Vector2(0.2f, 0.32f), SpriteEffects.None, 0f);
            }

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + MathHelper.ToRadians(68), tex.Size() / 2f, Projectile.scale, SpriteEffects.None, 0f);

            if (ACMAsset.LightShot != null) {
                Vector2 tipPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 38f;
                Color tipColor = AokinHelper.BlazingGold * 0.75f;
                tipColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, tipPos - Main.screenPosition, null, tipColor,
                    Projectile.rotation, ACMAsset.LightShot.Size() / 2f, 0.45f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = Projectile.Center + Projectile.rotation.ToRotationVector2() * 42f;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 26f, ref collisionPoint);
        }
    }

    /// <summary>焚风冲击 — 每四次突刺释放的火焰波</summary>
    public class InfernoScorchGust : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float gustScale = 0.55f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 150;
            Projectile.height = 130;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 45;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            gustScale = MathHelper.Lerp(gustScale, 1.25f, 0.09f);
            Projectile.scale = gustScale;

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 3; i++) {
                    Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                    float offset = MathF.Sin(Projectile.ai[0] * 0.45f + i) * 18f * gustScale;
                    Vector2 dustPos = Projectile.Center + perp * offset;
                    int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                    var d = Dust.NewDustPerfect(dustPos, dustType);
                    d.noGravity = true;
                    d.scale = 2f * gustScale;
                    d.velocity = -Projectile.velocity * 0.12f;
                    d.alpha = 80;
                }
            }

            Projectile.ai[0]++;
            Lighting.AddLight(Projectile.Center, AokinHelper.MoltenOrange.ToVector3() * gustScale * 0.75f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            InfernoDragonSpearGlobalNPC.ApplyBrand(target, 2);
            InfernoDragonSpearGlobalNPC.TryDetonateNova(target, Projectile.owner, (int)(Projectile.damage * 0.8f), Projectile.knockBack, Projectile.GetSource_FromThis());
            target.AddBuff(BuffID.OnFire3, 150);

            Vector2 knockDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            target.velocity += knockDir * 5f;

            AokinHelper.CreateDragonFireBurst(target.Center, 80f, 2, 10);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, tex.Height / 2f);

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                float bodyScale = progress * gustScale * (0.9f + MathF.Sin(Projectile.ai[0] * 0.35f + i * 0.4f) * 0.12f);

                Color trailColor = Color.Lerp(AokinHelper.MoltenOrange, AokinHelper.DragonFlameRed, 1f - progress);
                trailColor *= progress * 0.65f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin,
                    new Vector2(1f * bodyScale, 0.32f * bodyScale), SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            AokinHelper.CreateDragonFireBurst(Projectile.Center, 100f, 2, 12);
        }
    }

    /// <summary>焚天龙枪点燃叠层 — 按 NPC 实例追踪</summary>
    public class InfernoDragonSpearGlobalNPC : GlobalNPC
    {
        public const int MaxStacks = 8;
        public const int StackDecayDelay = 300;

        public override bool InstancePerEntity => true;

        public int infernoStacks;
        public int stackTimer;

        public static void ApplyBrand(NPC npc, int stacksToAdd) {
            if (!npc.active || npc.friendly || npc.dontTakeDamage) return;

            var brand = npc.GetGlobalNPC<InfernoDragonSpearGlobalNPC>();
            brand.infernoStacks = Math.Min(MaxStacks, brand.infernoStacks + stacksToAdd);
            brand.stackTimer = StackDecayDelay;

            int duration = 90 + brand.infernoStacks * 45;
            if (brand.infernoStacks >= 5) {
                npc.AddBuff(BuffID.OnFire3, duration);
            }
            else {
                npc.AddBuff(BuffID.OnFire, duration);
            }
        }

        /// <summary>点燃叠满（8 层）时引爆「焚天涅槃」赤焰新星，消耗层数造成范围灼烧。</summary>
        public static void TryDetonateNova(NPC npc, int owner, int baseDamage, float knockback, IEntitySource source) {
            if (!npc.active || npc.friendly || npc.dontTakeDamage) return;

            var brand = npc.GetGlobalNPC<InfernoDragonSpearGlobalNPC>();
            if (brand.infernoStacks < MaxStacks) return;

            brand.infernoStacks = 0;
            brand.stackTimer = 0;

            Vector2 center = npc.Center;
            int novaDamage = (int)(baseDamage * 2.4f);
            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.35f, Volume = 1f }, center);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.1f, Volume = 0.7f }, center);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                foreach (NPC other in Main.ActiveNPCs) {
                    if (!other.CanBeChasedBy()) continue;
                    if (Vector2.Distance(other.Center, center) > 200f) continue;

                    other.SimpleStrikeNPC(novaDamage, other.Center.X > center.X ? 1 : -1, false, knockback * 0.5f);
                    other.AddBuff(BuffID.OnFire3, 300);
                    var ob = other.GetGlobalNPC<InfernoDragonSpearGlobalNPC>();
                    ob.infernoStacks = Math.Min(MaxStacks, ob.infernoStacks + 2);
                    ob.stackTimer = StackDecayDelay;
                }
            }

            if (Main.dedServ) return;

            AokinHelper.CreateDragonFireBurst(center, 200f, 4, 20);
            AokinHelper.CreateFlameVortex(center, 160f, 1.6f, 36);
            for (int i = 0; i < 28; i++) {
                float angle = MathHelper.TwoPi * i / 28f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 13f);
                int dustType = Main.rand.Next(3) switch { 0 => DustID.Torch, 1 => DustID.SolarFlare, _ => DustID.RedTorch };
                var d = Dust.NewDustPerfect(center, dustType, vel, 60, default, Main.rand.NextFloat(2f, 3.2f));
                d.noGravity = true;
            }
            if (owner == Main.myPlayer) {
                Main.player[owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 14);
            }
        }

        public override void AI(NPC npc) {
            if (infernoStacks <= 0) return;

            if (--stackTimer <= 0) {
                infernoStacks = Math.Max(0, infernoStacks - 1);
                stackTimer = 60;
            }
        }

        public override void OnKill(NPC npc) {
            infernoStacks = 0;
            stackTimer = 0;
        }

        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (infernoStacks <= 0) return;
            if (!npc.HasBuff(BuffID.OnFire) && !npc.HasBuff(BuffID.OnFire3)) return;

            if (npc.lifeRegen > 0) npc.lifeRegen = 0;
            npc.lifeRegen -= infernoStacks * 3;
            int minDamage = infernoStacks * 2;
            if (damage < minDamage) damage = minDamage;
        }
    }

    /// <summary>
    /// 焰缠双环 - 敖钦掉落的火系回旋刃
    /// 掷出两枚交缠火环，去程与回程各造成一次有效打击
    /// </summary>
    public class FlamecoilChakram : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 355;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 40;
            Item.useTime = Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 4.5f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<FlamecoilChakramProjectile>();
            Item.shootSpeed = 17f;
            Item.crit = 8;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.LightDisc;

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<FlamecoilChakramProjectile>()] < 2;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            for (int ring = 0; ring < 2; ring++) {
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, ring);
            }

            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "DragonLore", "南海龙鳞淬铸的双生火环，交缠飞旋"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect", "掷出两枚火环，去程与回程各造成一次打击，回程暴击强化"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect2", "两环同场时之间架起焚焰锁链，灼烧穿过锁链的敌人"));
        }
    }

    /// <summary>
    /// 焰缠双环弹幕 - 交缠火 coil 回旋
    /// </summary>
    public class FlamecoilChakramProjectile : ModProjectile
    {
        private ref float RingIndex => ref Projectile.ai[0];
        private ref float FlightTimer => ref Projectile.ai[1];
        private ref float IsReturning => ref Projectile.ai[2];
        private ref float CoilPhase => ref Projectile.localAI[0];
        private ref float CoreX => ref Projectile.localAI[1];
        private ref float CoreY => ref Projectile.localAI[2];

        private int tetherCooldown;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 38;
            Projectile.height = 38;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            FlightTimer++;
            CoilPhase += 0.22f;

            bool returning = IsReturning >= 1f;
            Vector2 coreVelocity = Projectile.velocity;

            if (!returning) {
                coreVelocity *= 0.975f;

                if (coreVelocity.Length() < 4f || Projectile.timeLeft < 210) {
                    IsReturning = 1f;
                    returning = true;
                }
            }

            if (returning) {
                Vector2 toOwner = Owner.Center - Projectile.Center;
                float distance = toOwner.Length();

                if (distance < 28f) {
                    Projectile.Kill();
                    return;
                }

                float returnSpeed = 18f + FlightTimer * 0.08f;
                coreVelocity = Vector2.Lerp(coreVelocity, toOwner.SafeNormalize(Vector2.Zero) * returnSpeed, 0.11f);
            }

            ApplyCoilMotion(ref coreVelocity, returning);
            SpawnFlameCoilParticles(returning);

            // 双环交缠：低序号环负责维护两环之间的焚焰锁链，灼烧穿过锁链的敌人
            if (RingIndex < 0.5f) {
                HandleFlameTether();
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.MoltenOrange.ToVector3() * 0.65f);
        }

        private Projectile FindPartnerRing() {
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.whoAmI == Projectile.whoAmI) continue;
                if (p.type == Projectile.type && p.owner == Projectile.owner) return p;
            }
            return null;
        }

        private void HandleFlameTether() {
            Projectile partner = FindPartnerRing();
            if (partner == null) return;

            if (tetherCooldown > 0) {
                tetherCooldown--;
                return;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 a = Projectile.Center;
                Vector2 b = partner.Center;
                bool hitAny = false;
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!npc.CanBeChasedBy()) continue;
                    float pt = 0f;
                    if (Collision.CheckAABBvLineCollision(npc.Hitbox.TopLeft(), npc.Hitbox.Size(), a, b, 22f, ref pt)) {
                        npc.SimpleStrikeNPC((int)(Projectile.damage * 0.5f), 0, false, 0f);
                        npc.AddBuff(BuffID.OnFire, 180);
                        hitAny = true;
                    }
                }
                if (hitAny && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item73 with { Volume = 0.35f, Pitch = 0.3f }, Projectile.Center);
                }
            }
            tetherCooldown = 12;
        }

        private void DrawFlameTether() {
            Projectile partner = FindPartnerRing();
            if (partner == null || ACMAsset.LightShot == null) return;

            Vector2 a = Projectile.Center - Main.screenPosition;
            Vector2 b = partner.Center - Main.screenPosition;
            Vector2 mid = (a + b) / 2f;
            float rot = (b - a).ToRotation();
            float len = Vector2.Distance(a, b);
            float pulse = 0.8f + MathF.Sin(CoilPhase * 2f) * 0.2f;

            Color beam = AokinHelper.MoltenOrange * 0.55f * pulse;
            beam.A = 0;
            Main.spriteBatch.Draw(ACMAsset.LightShot, mid, null, beam, rot, ACMAsset.LightShot.Size() / 2f,
                new Vector2(len / ACMAsset.LightShot.Width, 0.24f * pulse), SpriteEffects.None, 0f);

            Color core = AokinHelper.BlazingGold * 0.7f * pulse;
            core.A = 0;
            Main.spriteBatch.Draw(ACMAsset.LightShot, mid, null, core, rot, ACMAsset.LightShot.Size() / 2f,
                new Vector2(len / ACMAsset.LightShot.Width, 0.1f * pulse), SpriteEffects.None, 0f);
        }

        private void ApplyCoilMotion(ref Vector2 coreVelocity, bool returning) {
            if (FlightTimer <= 1f) {
                CoreX = Projectile.Center.X;
                CoreY = Projectile.Center.Y;
            }

            Vector2 corePosition = new Vector2(CoreX, CoreY) + coreVelocity;
            CoreX = corePosition.X;
            CoreY = corePosition.Y;

            Vector2 direction = coreVelocity.SafeNormalize(Vector2.UnitX);
            Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);

            float coilRadius = returning ? 16f : 24f;
            float phase = CoilPhase + RingIndex * MathHelper.Pi;
            Vector2 coilOffset = perpendicular * MathF.Sin(phase) * coilRadius;
            coilOffset += direction * MathF.Cos(phase) * coilRadius * 0.35f;

            Projectile.Center = corePosition + coilOffset;
            Projectile.velocity = coreVelocity;
            Projectile.rotation += 0.32f * (RingIndex < 0.5f ? 1f : -1f);
        }

        private void SpawnFlameCoilParticles(bool returning) {
            if (Main.netMode == NetmodeID.Server)
                return;

            AokinHelper.CreateFireTrail(Projectile.Center, Projectile.velocity, returning ? 0.85f : 1.1f);

            int ringCount = returning ? 2 : 3;
            for (int ring = 0; ring < ringCount; ring++) {
                float ringRadius = 18f + ring * 10f;
                float ringRot = CoilPhase * (1f - ring * 0.18f) * (ring % 2 == 0 ? 1f : -1f);
                float angle = ringRot + RingIndex * MathHelper.PiOver2;

                if (!Main.rand.NextBool(returning ? 3 : 2))
                    continue;

                Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * ringRadius;
                int dustType = Main.rand.NextBool(3) ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 120, default, Main.rand.NextFloat(1.2f, 2f));
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(2f, 5f);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (IsReturning >= 1f)
                modifiers.SourceDamage *= 1.45f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 360);

            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 90, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            if (Main.rand.NextBool(3))
                SoundEngine.PlaySound(SoundID.Item73 with { Volume = 0.45f, Pitch = 0.2f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Item[ModContent.ItemType<FlamecoilChakram>()].Value;
            Vector2 origin = tex.Size() / 2f;
            float pulse = 1f + MathF.Sin(CoilPhase * 2f) * 0.08f;

            if (RingIndex < 0.5f) {
                DrawFlameTether();
            }

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                float spiralAngle = CoilPhase - i * 0.28f + RingIndex * MathHelper.PiOver2;
                Vector2 spiralOffset = spiralAngle.ToRotationVector2() * (12f * progress);

                Color trailColor = Color.Lerp(AokinHelper.BlazingGold, AokinHelper.DragonFlameRed, 1f - progress);
                trailColor *= progress * 0.55f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f + spiralOffset - Main.screenPosition;
                Main.spriteBatch.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, 0.65f * progress * pulse, SpriteEffects.None, 0f);
            }

            AokinHelper.DrawFlameAura(Main.spriteBatch, Projectile.Center, 34f, CoilPhase + RingIndex, 0.55f);

            Color outerColor = AokinHelper.DragonFlameRed * 0.35f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, outerColor,
                Projectile.rotation * 0.5f, origin, 1.15f * pulse, SpriteEffects.None, 0f);

            Color mainColor = AokinHelper.MoltenOrange * 0.75f;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor,
                Projectile.rotation, origin, 0.85f * pulse, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                -Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.6f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 赤潮旋涡弓 - 敖钦掉落的火系弓
    /// 将箭矢转化为赤旋风暴箭，命中生成小型火龙卷
    /// </summary>
    public class CrimsonMaelstromBow : ModItem
    {
        public const int MaelstromThreshold = 5;

        public override void SetDefaults() {
            Item.damage = 360;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 18;
            Item.height = 40;
            Item.useTime = Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 16f;
            Item.useAmmo = AmmoID.Arrow;
            Item.crit = 12;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            type = ModContent.ProjectileType<CrimsonStormArrow>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            var maelstromPlayer = player.GetModPlayer<CrimsonMaelstromPlayer>();
            bool surge = maelstromPlayer.ConsumeMaelstromSurge();

            for (int i = -1; i <= 1; i++) {
                Vector2 spreadVel = velocity.RotatedBy(MathHelper.ToRadians(4f * i));
                float ai0 = (surge && i == 0) ? 1f : 0f;
                int arrowDamage = surge ? (int)(damage * 1.2f) : damage;
                Projectile.NewProjectile(source, position, spreadVel, type, arrowDamage, knockback, player.whoAmI, ai0);
            }

            if (surge) {
                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.1f, Volume = 0.9f }, player.Center);
                if (Main.netMode != NetmodeID.Server) {
                    AokinHelper.CreateFlameVortex(position, 52f, 1.2f, 24);
                }
            }

            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "DragonLore", "以龙筋为弦的南海火龙神弓"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect", "将箭矢转化为赤色旋风暴箭，命中生成小型火龙卷"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect2", $"每命中 {MaelstromThreshold} 次，下一箭凝成赤潮巨旋风，卷吸并灼烧成群敌人"));
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.Marrow;
    }

    /// <summary>赤潮旋涡弓 — 追踪命中计数，满计数后下一射凝成赤潮巨旋风。</summary>
    public class CrimsonMaelstromPlayer : ModPlayer
    {
        private int hitCount;
        private bool pendingSurge;

        public void RegisterHit() {
            if (pendingSurge) {
                return;
            }

            hitCount++;
            if (hitCount >= CrimsonMaelstromBow.MaelstromThreshold) {
                hitCount = 0;
                pendingSurge = true;
            }
        }

        public bool ConsumeMaelstromSurge() {
            if (!pendingSurge) {
                return false;
            }

            pendingSurge = false;
            return true;
        }
    }

    /// <summary>
    /// 赤旋风暴箭 - 赤潮旋涡弓发射的火焰箭矢
    /// </summary>
    public class CrimsonStormArrow : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float stormPhase;
        private float baseSpeed;

        private ref float IsMaelstrom => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
        }

        public override void AI() {
            if (baseSpeed <= 0f) {
                baseSpeed = Projectile.velocity.Length();
            }

            stormPhase += 0.18f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Projectile.velocity = (Projectile.velocity + perp * MathF.Sin(stormPhase) * 0.35f).SafeNormalize(Vector2.UnitX) * baseSpeed;

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Torch,
                    1 => DustID.SolarFlare,
                    _ => DustID.RedTorch
                };
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 120, default, 1.3f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.12f + perp * Main.rand.NextFloat(-1.5f, 1.5f);
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.DragonFlameRed.ToVector3() * 0.45f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 180);

            Player owner = Main.player[Projectile.owner];
            if (owner.active && !owner.dead) {
                owner.GetModPlayer<CrimsonMaelstromPlayer>().RegisterHit();
            }

            bool maelstrom = IsMaelstrom >= 1f;

            for (int i = 0; i < (maelstrom ? 14 : 8); i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.6f);
                Main.dust[dust].noGravity = true;
            }

            if (Main.myPlayer == Projectile.owner) {
                float tornadoScale = maelstrom ? 1.85f : 1f;
                float damageMult = maelstrom ? 0.85f : 0.55f;
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    target.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<MiniCrimsonFireTornado>(),
                    (int)(Projectile.damage * damageMult),
                    Projectile.knockBack * 0.4f,
                    Projectile.owner,
                    tornadoScale
                );
            }

            SoundEngine.PlaySound(SoundID.Item34 with { Pitch = maelstrom ? -0.15f : 0.1f, Volume = maelstrom ? 0.85f : 0.6f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, 0);

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AokinHelper.DeepFlamePurple, AokinHelper.DragonFlameRed, 1f - progress);
                trailColor *= progress * 0.55f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i] - MathHelper.PiOver2, origin,
                    new Vector2(0.3f * progress, 0.5f * progress), SpriteEffects.None, 0f);
            }

            Color mainColor = Color.Lerp(AokinHelper.MoltenOrange, AokinHelper.BlazingGold, 0.35f) * 0.9f;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor,
                Projectile.rotation - MathHelper.PiOver2, origin, new Vector2(0.42f, 0.62f), SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 120, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 小型火龙卷 - 赤旋箭命中时生成
    /// </summary>
    public class MiniCrimsonFireTornado : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float tornadoRotation;
        private float tornadoAlpha;
        private float tornadoHeight;
        private float sizeScale = 1f;
        private float MaxHeight => 220f * sizeScale;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void OnSpawn(IEntitySource source) {
            sizeScale = Projectile.ai[0] >= 1f ? Projectile.ai[0] : 1f;
            if (sizeScale > 1.2f) {
                Projectile.timeLeft = 140;
                Projectile.width = 60;
                Projectile.height = 60;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            tornadoRotation += 0.22f;
            tornadoAlpha = MathHelper.Lerp(tornadoAlpha, 1f, 0.05f);
            tornadoHeight = MathHelper.Lerp(tornadoHeight, MaxHeight, 0.06f);
            Projectile.velocity *= 0.92f;

            float pullRange = 110f * sizeScale;
            foreach (NPC npc in Main.npc) {
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                float distance = Vector2.Distance(npc.Center, Projectile.Center);
                if (distance < pullRange && distance > 20f) {
                    Vector2 pullDir = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    npc.velocity += pullDir * 0.35f * sizeScale;
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 4; i++) {
                    float heightOffset = Main.rand.NextFloat(-tornadoHeight / 2, tornadoHeight / 2);
                    float angle = tornadoRotation + Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = 18f + MathF.Abs(heightOffset / tornadoHeight) * 28f;

                    Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(angle) * radius, heightOffset);
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Torch,
                        1 => DustID.SolarFlare,
                        _ => DustID.Smoke
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 140, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = new Vector2(MathF.Cos(angle + MathHelper.PiOver2) * 4f, Main.rand.NextFloat(-1.5f, 1.5f));
                }
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.DragonFlameRed.ToVector3() * tornadoAlpha * 0.7f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float targetX = targetHitbox.Center.X;
            float distance = MathF.Abs(targetX - Projectile.Center.X);
            float targetY = targetHitbox.Center.Y;
            float heightDiff = MathF.Abs(targetY - Projectile.Center.Y);
            return distance < 34f * sizeScale && heightDiff < tornadoHeight / 2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 120);

            for (int i = 0; i < 6; i++) {
                float angle = tornadoRotation + MathHelper.TwoPi * i / 6;
                Vector2 vel = angle.ToRotationVector2() * 4f;
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.Torch, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;

            Main.instance.LoadProjectile(ProjectileID.SandnadoHostile);
            Texture2D tornadoTex = TextureAssets.Projectile[ProjectileID.SandnadoHostile].Value;
            Vector2 origin = tornadoTex.Size() / 2f;

            int segments = 8;
            for (int seg = 0; seg < segments; seg++) {
                float heightPercent = (float)seg / segments;
                float yOffset = (heightPercent - 0.5f) * tornadoHeight;
                float segRadius = 0.35f + MathF.Abs(heightPercent - 0.5f) * 0.45f;
                float segRot = tornadoRotation + seg * 0.35f;

                Vector2 segPos = screenPos + new Vector2(0, yOffset);

                Color outerColor = AokinHelper.DragonFlameRed * tornadoAlpha * 0.45f;
                outerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, outerColor, segRot, origin, segRadius * 1.15f, SpriteEffects.None, 0f);

                Color midColor = AokinHelper.MoltenOrange * tornadoAlpha * 0.65f;
                midColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, midColor, segRot * 1.25f, origin, segRadius, SpriteEffects.None, 0f);

                Color innerColor = AokinHelper.BlazingGold * tornadoAlpha * 0.35f;
                innerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, innerColor, segRot * 1.5f, origin, segRadius * 0.6f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);

            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4, 8);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 龙魂余烬杖 — 敖钦掉落的火系召唤法杖
    /// 召唤余烬幼龙跟随作战，周期吐出熔岩蛋造成范围灼烧
    /// </summary>
    public class DraconicEmber : ModItem
    {
        public override void SetStaticDefaults() {
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
            ItemID.Sets.StaffMinionSlotsRequired[Type] = 1f;
        }

        public override void SetDefaults() {
            Item.damage = 340;
            Item.DamageType = DamageClass.Summon;
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.mana = 10;
            Item.shoot = ModContent.ProjectileType<DraconicEmberProj>();
            Item.shootSpeed = 0f;
            Item.buffType = ModContent.BuffType<DraconicEmberBuff>();
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.PygmyStaff;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);

            var projectile = Projectile.NewProjectileDirect(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI);
            projectile.originalDamage = Item.damage;

            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.2f, Volume = 0.7f }, player.Center);
            AokinHelper.CreateDragonFireBurst(player.Center, 60f, 2, 10);

            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "DragonLore", "南海龙魂凝成的余烬法杖，唤出炽焰幼龙"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect", "余烬幼龙环绕助战，啃咬敌人并周期吐出熔岩蛋"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect2", "熔岩蛋爆炸造成范围灼烧，叠加点燃效果"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect3", "近身时喷吐扇形龙焰，灼烧并叠加点燃层数"));
        }
    }

    public class DraconicEmberBuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<DraconicEmberProj>()] > 0) {
                player.buffTime[buffIndex] = 18000;
            }
            else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }

    /// <summary>余烬幼龙 — 龙魂余烬杖召唤物</summary>
    public class DraconicEmberProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float EggCooldown => ref Projectile.localAI[0];
        private ref float WingPhase => ref Projectile.localAI[1];
        private ref float BreathCooldown => ref Projectile.ai[0];

        private const float EggInterval = 100f;
        private const float BreathInterval = 190f;
        private const float OrbitRadius = 95f;
        private const float TargetRange = 680f;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            if (!player.active || player.dead) {
                player.ClearBuff(ModContent.BuffType<DraconicEmberBuff>());
                return;
            }

            if (player.HasBuff(ModContent.BuffType<DraconicEmberBuff>())) {
                Projectile.timeLeft = 2;
            }

            WingPhase += 0.18f;
            if (EggCooldown > 0f) {
                EggCooldown--;
            }
            if (BreathCooldown > 0f) {
                BreathCooldown--;
            }

            NPC target = FindTarget(player, TargetRange);
            if (target != null) {
                ChaseAndAttack(player, target);
            }
            else {
                OrbitPlayer(player);
            }

            SpawnEmberTrail();
            Lighting.AddLight(Projectile.Center, AokinHelper.MoltenOrange.ToVector3() * 0.65f);
        }

        private void OrbitPlayer(Player player) {
            float orbitAngle = Main.GlobalTimeWrappedHourly * 2.4f + Projectile.whoAmI * 0.7f;
            float bob = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + Projectile.whoAmI) * 24f;
            Vector2 targetPos = player.Center + orbitAngle.ToRotationVector2() * OrbitRadius;
            targetPos.Y -= 40f + bob;

            Vector2 toTarget = targetPos - Projectile.Center;
            float speed = MathHelper.Clamp(toTarget.Length() * 0.14f, 4f, 16f);
            Projectile.velocity = toTarget.Length() > 12f
                ? toTarget.SafeNormalize(Vector2.Zero) * speed
                : Projectile.velocity * 0.92f;

            FaceVelocity();
        }

        private void ChaseAndAttack(Player player, NPC target) {
            Vector2 toEnemy = target.Center - Projectile.Center;
            float distance = toEnemy.Length();
            float desiredDistance = 70f;

            if (distance > desiredDistance + 20f) {
                Projectile.velocity = Vector2.Lerp(
                    Projectile.velocity,
                    toEnemy.SafeNormalize(Vector2.Zero) * 14f,
                    0.12f);
            }
            else if (distance < desiredDistance - 10f) {
                Projectile.velocity = Vector2.Lerp(
                    Projectile.velocity,
                    -toEnemy.SafeNormalize(Vector2.Zero) * 8f,
                    0.08f);
            }
            else {
                Projectile.velocity *= 0.94f;
            }

            FaceVelocity();

            if (EggCooldown <= 0f && Main.netMode != NetmodeID.MultiplayerClient) {
                SpitLavaEgg(target);
                EggCooldown = EggInterval;
            }

            if (BreathCooldown <= 0f && distance < 380f && Main.netMode != NetmodeID.MultiplayerClient) {
                BreatheFire(target);
                BreathCooldown = BreathInterval;
            }
        }

        private void BreatheFire(NPC target) {
            Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.2f, Volume = 0.6f }, Projectile.Center);

            for (int i = -2; i <= 2; i++) {
                Vector2 vel = direction.RotatedBy(MathHelper.ToRadians(11f * i)) * 9.5f;
                int flame = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center + direction * 18f,
                    vel,
                    ModContent.ProjectileType<DraconicEmberFlame>(),
                    (int)(Projectile.damage * 0.7f),
                    Projectile.knockBack * 0.5f,
                    Projectile.owner);

                if (flame >= 0 && flame < Main.maxProjectiles) {
                    Main.projectile[flame].originalDamage = Projectile.originalDamage;
                }
            }

            if (!Main.dedServ) {
                for (int i = 0; i < 10; i++) {
                    Vector2 vel = direction.RotatedByRandom(0.4f) * Main.rand.NextFloat(3f, 7f);
                    var d = Dust.NewDustPerfect(Projectile.Center + direction * 14f, Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare);
                    d.noGravity = true;
                    d.scale = 1.6f;
                    d.velocity = vel;
                }
            }
        }

        private void SpitLavaEgg(NPC target) {
            Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
            direction = direction.RotatedByRandom(0.08f);
            float speed = 11f + MathHelper.Clamp(Vector2.Distance(target.Center, Projectile.Center) * 0.015f, 0f, 6f);

            int egg = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center + direction * 16f,
                direction * speed,
                ModContent.ProjectileType<DraconicEmberEggProj>(),
                (int)(Projectile.damage * 1.15f),
                Projectile.knockBack,
                Projectile.owner,
                target.whoAmI);

            if (egg >= 0) {
                Main.projectile[egg].originalDamage = Projectile.originalDamage;
            }

            SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.15f, Volume = 0.55f }, Projectile.Center);

            for (int i = 0; i < 8; i++) {
                Vector2 vel = direction.RotatedByRandom(0.35f) * Main.rand.NextFloat(2f, 5f);
                var d = Dust.NewDustPerfect(Projectile.Center + direction * 10f, DustID.Torch);
                d.noGravity = true;
                d.scale = 1.6f;
                d.velocity = vel;
            }
        }

        private NPC FindTarget(Player player, float maxDistance) {
            if (player.HasMinionAttackTargetNPC) {
                NPC targeted = Main.npc[player.MinionAttackTargetNPC];
                if (targeted.active && targeted.CanBeChasedBy() && !targeted.friendly) {
                    float dist = Vector2.Distance(targeted.Center, Projectile.Center);
                    if (dist < maxDistance * 1.35f) {
                        return targeted;
                    }
                }
            }

            NPC closest = null;
            float closestDist = maxDistance;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.friendly) {
                    continue;
                }

                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }

            return closest;
        }

        private void FaceVelocity() {
            if (Projectile.velocity.Length() > 0.5f) {
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation, Projectile.velocity.ToRotation(), 0.18f);
            }

            Projectile.spriteDirection = Projectile.velocity.X >= 0f ? 1 : -1;
        }

        private void SpawnEmberTrail() {
            if (Main.netMode == NetmodeID.Server || !Main.rand.NextBool(3)) {
                return;
            }

            float wingOffset = MathF.Sin(WingPhase) * 14f;
            Vector2 perpendicular = Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
            Vector2 dustPos = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * 12f + perpendicular * wingOffset;

            int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
            var d = Dust.NewDustPerfect(dustPos, dustType);
            d.noGravity = true;
            d.scale = 1.4f;
            d.velocity = -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(1f, 1f);
        }

        public override bool? CanDamage() {
            return Projectile.velocity.Length() > 2f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 120);
            InfernoDragonSpearGlobalNPC.ApplyBrand(target, 1);
            AokinHelper.CreateFireTrail(target.Center, Projectile.velocity, 1.2f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            SpriteEffects effects = Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float pulse = 1f + MathF.Sin(WingPhase * 2f) * 0.12f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AokinHelper.MoltenOrange, AokinHelper.DragonFlameRed, 1f - progress);
                trailColor *= progress * 0.45f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i], origin,
                    0.55f * progress, effects, 0f);
            }

            Color glowColor = AokinHelper.BlazingGold * 0.35f * pulse;
            glowColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, glowColor, Projectile.rotation, origin,
                1.15f * pulse, effects, 0f);

            Color bodyColor = Color.Lerp(AokinHelper.MoltenOrange, AokinHelper.DragonFlameRed, 0.35f);
            bodyColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, bodyColor, Projectile.rotation, origin,
                0.85f * pulse, effects, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            AokinHelper.CreateDragonFireBurst(Projectile.Center, 70f, 2, 10);
        }
    }

    /// <summary>熔岩蛋 — 余烬幼龙吐出的范围爆炸弹幕</summary>
    public class DraconicEmberEggProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float TargetIndex => ref Projectile.ai[0];
        private ref float HasExploded => ref Projectile.localAI[0];

        private const float ExplosionRadius = 96f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.MinionShot[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 75;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            if (HasExploded > 0f) {
                return;
            }

            Projectile.rotation += 0.25f;
            Projectile.velocity.Y += 0.1f;

            if (TargetIndex >= 0f && TargetIndex < Main.maxNPCs) {
                NPC target = Main.npc[(int)TargetIndex];
                if (target.active && target.CanBeChasedBy()) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    if (toTarget.Length() > 24f && Projectile.timeLeft > 12) {
                        Vector2 desired = toTarget.SafeNormalize(Vector2.Zero) * (Projectile.velocity.Length() + 0.15f);
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.06f);
                    }
                    else if (toTarget.Length() <= 36f) {
                        Explode();
                        return;
                    }
                }
            }

            if (Projectile.timeLeft <= 8) {
                Explode();
                return;
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = -Projectile.velocity * 0.15f;
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.DragonFlameRed.ToVector3() * 0.55f);
        }

        public override bool? CanDamage() => false;

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Explode();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Explode();
        }

        private void Explode() {
            if (HasExploded > 0f || Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }

            HasExploded = 1f;
            Vector2 center = Projectile.Center;
            int blastDamage = (int)(Projectile.damage * 1.35f);

            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }

                if (Vector2.Distance(npc.Center, center) > ExplosionRadius) {
                    continue;
                }

                npc.SimpleStrikeNPC(blastDamage, Projectile.direction, false, Projectile.knockBack * 0.6f);
                npc.AddBuff(BuffID.OnFire3, 180);
                InfernoDragonSpearGlobalNPC.ApplyBrand(npc, 1);
            }

            AokinHelper.CreateDragonFireBurst(center, ExplosionRadius, 3, 14);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.15f, Volume = 0.75f }, center);
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(Projectile.rotation * 3f) * 0.1f;

            Color outer = AokinHelper.DragonFlameRed * 0.45f * pulse;
            outer.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, outer, Projectile.rotation, origin, 1.1f * pulse, SpriteEffects.None, 0f);

            Color core = Color.Lerp(AokinHelper.BlazingGold, AokinHelper.MoltenOrange, 0.4f);
            core.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, core, Projectile.rotation, origin, 0.65f * pulse, SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>余烬龙焰 — 余烬幼龙喷吐的扇形龙焰短弹。</summary>
    public class DraconicEmberFlame : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.MinionShot[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 48;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.985f;

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool()) {
                var d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = Projectile.velocity * 0.2f;
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.MoltenOrange.ToVector3() * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 120);
            InfernoDragonSpearGlobalNPC.ApplyBrand(target, 1);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color c = Color.Lerp(AokinHelper.BlazingGold, AokinHelper.DragonFlameRed, 1f - progress) * progress * 0.5f;
                c.A = 0;
                Main.spriteBatch.Draw(tex, Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition, null, c, 0f, origin, 0.5f * progress, SpriteEffects.None, 0f);
            }

            Color core = AokinHelper.MoltenOrange * 0.8f;
            core.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, core, 0f, origin, 0.45f, SpriteEffects.None, 0f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) return;
            for (int i = 0; i < 6; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, Main.rand.NextVector2Circular(3f, 3f));
                d.noGravity = true;
                d.scale = 1.3f;
            }
        }
    }

    /// <summary>
    /// 唤流星杖 - 敖钦掉落的火系法师武器
    /// 蓄力在目标处刻印龙纹法阵，松手后降下多枚焰陨流星
    /// </summary>
    public class MeteorCallerStaff : ModItem
    {
        private int chargeTime;
        private Vector2 channelTarget;
        private const int MinCharge = 10;
        private const int MaxCharge = 45;

        public override void SetDefaults() {
            Item.damage = 365;
            Item.DamageType = DamageClass.Magic;
            Item.width = 32;
            Item.height = 32;
            Item.useTime = Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item20;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.mana = 12;
            Item.shoot = ModContent.ProjectileType<MeteorCallerDragonSigil>();
            Item.shootSpeed = 0f;
            Item.staff[Item.type] = true;
            Item.channel = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.AmberStaff;

        public override void HoldItem(Player player) {
            if (player.channel && player.CheckMana(Item, -1, false, false)) {
                channelTarget = Main.MouseWorld;
                chargeTime++;

                float progress = MathHelper.Clamp((chargeTime - MinCharge) / (float)(MaxCharge - MinCharge), 0f, 1f);

                if (chargeTime > 6 && Main.rand.NextBool(2)) {
                    float radius = MathHelper.Lerp(24f, 90f, progress);
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 dustPos = channelTarget + angle.ToRotationVector2() * radius * Main.rand.NextFloat(0.75f, 1.1f);
                    Vector2 toCenter = (channelTarget - dustPos).SafeNormalize(Vector2.Zero);

                    int dustType = Main.rand.NextBool(3) ? DustID.Torch : DustID.SolarFlare;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 1.4f + progress);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = toCenter * (4f + progress * 4f);
                }

                if (chargeTime > 12) {
                    Lighting.AddLight(channelTarget, AokinHelper.DragonFlameRed.ToVector3() * (0.25f + progress * 0.55f));
                }

                if (chargeTime == MaxCharge) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.4f, Volume = 0.8f }, channelTarget);

                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi * i / 12f;
                        Vector2 vel = angle.ToRotationVector2() * 5f;
                        int dust = Dust.NewDust(channelTarget, 0, 0, DustID.SolarFlare, vel.X, vel.Y, 100, default, 2.2f);
                        Main.dust[dust].noGravity = true;
                    }
                }

                if (chargeTime > MaxCharge)
                    chargeTime = MaxCharge;
            }
            else if (chargeTime > 0) {
                if (chargeTime >= MinCharge && player.CheckMana(Item, -1, true, false)) {
                    CastMeteorShower(player);
                }

                chargeTime = 0;
            }
        }

        private void CastMeteorShower(Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            float chargeProgress = MathHelper.Clamp((chargeTime - MinCharge) / (float)(MaxCharge - MinCharge), 0f, 1f);
            int meteorCount = 4 + (int)(chargeProgress * 8f);

            Projectile.NewProjectile(
                player.GetSource_ItemUse(Item),
                channelTarget,
                Vector2.Zero,
                ModContent.ProjectileType<MeteorCallerDragonSigil>(),
                Item.damage,
                Item.knockBack,
                player.whoAmI,
                meteorCount,
                chargeProgress
            );

            SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.1f + chargeProgress * 0.2f, Volume = 0.9f }, channelTarget);

            if (player.whoAmI == Main.myPlayer && chargeProgress > 0.65f) {
                player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(5 + (int)(chargeProgress * 4f), 12);
            }

            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dust = Dust.NewDust(player.Center, 0, 0, DustID.Torch, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "MeteorLore", "「龙纹既成，焰陨随行」"));
            tooltips.Add(new TooltipLine(Mod, "MeteorEffect", "长按在光标处刻印龙纹法阵"));
            tooltips.Add(new TooltipLine(Mod, "MeteorEffect2", "松手后降下多枚焰陨流星，蓄力越久数量越多"));
            tooltips.Add(new TooltipLine(Mod, "MeteorEffect3", "流星陨落处残留灼热熔池，持续灼烧驻足的敌人"));
        }
    }

    /// <summary>
    /// 龙纹唤星法阵 - 蓄力完成后在目标处展开并召唤流星雨
    /// </summary>
    public class MeteorCallerDragonSigil : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float circleScale;
        private float runeRotation;
        private float pulsePhase;
        private int meteorTimer;
        private int meteorsSpawned;
        private int meteorCount;
        private float chargeProgress;
        private int lifetime;
        private int maxLifetime;

        public override void SetDefaults() {
            Projectile.width = 180;
            Projectile.height = 180;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
            Projectile.penetrate = -1;
        }

        public override void OnSpawn(IEntitySource source) {
            meteorCount = Math.Max(4, (int)Projectile.ai[0]);
            chargeProgress = Projectile.ai[1];
            maxLifetime = 28 + meteorCount * 4;
            Projectile.timeLeft = maxLifetime;
            lifetime = 0;
        }

        public override void AI() {
            lifetime++;
            float lifeProgress = lifetime / (float)maxLifetime;

            pulsePhase += 0.1f;
            runeRotation += 0.035f;

            if (lifeProgress < 0.22f) {
                circleScale = MathHelper.Lerp(circleScale, 1f + chargeProgress * 0.35f, 0.12f);
            }
            else if (lifeProgress > 0.82f) {
                circleScale = MathHelper.Lerp(circleScale, 0f, 0.08f);
            }

            int size = (int)(160 * circleScale);
            Projectile.width = Projectile.height = Math.Max(32, size);

            meteorTimer++;
            int spawnInterval = Math.Max(3, 7 - (int)(chargeProgress * 3f));
            if (circleScale > 0.55f && meteorTimer >= spawnInterval && meteorsSpawned < meteorCount) {
                meteorTimer = 0;
                SpawnMeteor();
            }

            CreateSigilParticles();

            Lighting.AddLight(Projectile.Center, AokinHelper.DragonFlameRed.ToVector3() * circleScale * 0.7f);
        }

        private void SpawnMeteor() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            float radius = 70f * circleScale;
            Vector2 impactPos = Projectile.Center + Main.rand.NextVector2Circular(radius, radius);
            Vector2 spawnPos = impactPos + new Vector2(Main.rand.NextFloat(-20f, 20f), Main.rand.NextFloat(-520f, -360f));
            Vector2 velocity = (impactPos - spawnPos).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(14f, 20f);

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                spawnPos,
                velocity,
                ModContent.ProjectileType<MeteorCallerMeteor>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner
            );

            meteorsSpawned++;

            for (int i = 0; i < 4; i++) {
                int dust = Dust.NewDust(spawnPos, 0, 0, DustID.SolarFlare, 0, 0, 120, default, 1.6f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(2, 2);
            }
        }

        private void CreateSigilParticles() {
            if (Main.netMode == NetmodeID.Server)
                return;

            float effectiveRadius = 70f * circleScale;

            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * effectiveRadius;
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(pos, 0, 0, dustType, 0, 0, 100, default, 1.5f * circleScale);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 3.5f;
            }

            if (Main.rand.NextBool(5)) {
                float runeAngle = runeRotation + MathHelper.TwoPi * Main.rand.Next(6) / 6f;
                Vector2 runePos = Projectile.Center + runeAngle.ToRotationVector2() * effectiveRadius * 0.65f;
                int dust = Dust.NewDust(runePos, 0, 0, DustID.SolarFlare, 0, 0, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Vector2.Zero;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (circleScale <= 0.05f)
                return false;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D starTex = ACMAsset.BlankStar ?? TextureAssets.Projectile[Type].Value;
            Texture2D waveTex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 starOrigin = starTex.Size() / 2f;
            Vector2 waveOrigin = new Vector2(0, waveTex.Height / 2f);
            float effectiveRadius = 70f * circleScale;

            for (int ring = 0; ring < 2; ring++) {
                float ringRadius = effectiveRadius * (0.55f + ring * 0.35f);
                float ringRotation = runeRotation * (ring == 0 ? 1f : -1.4f);
                int segments = 8 - ring * 2;
                float ringAlpha = (0.75f - ring * 0.2f) * circleScale;

                for (int i = 0; i < segments; i++) {
                    float angle = ringRotation + MathHelper.TwoPi * i / segments;
                    float pulse = MathF.Sin(pulsePhase + angle * 2f) * 0.25f + 0.75f;
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * ringRadius;

                    Color runeColor = Color.Lerp(AokinHelper.BlazingGold, AokinHelper.DragonFlameRed, pulse) * ringAlpha;
                    runeColor.A = 0;

                    sb.Draw(starTex, pos - Main.screenPosition, null, runeColor, angle + MathHelper.PiOver4, starOrigin,
                        (0.28f + pulse * 0.12f) * circleScale, SpriteEffects.None, 0f);
                }
            }

            for (int i = 0; i < 4; i++) {
                float angle = runeRotation * 1.6f + MathHelper.PiOver2 * i;
                Vector2 dragonPos = Projectile.Center + angle.ToRotationVector2() * effectiveRadius * 0.35f;
                Color dragonColor = Color.Lerp(AokinHelper.MoltenOrange, AokinHelper.DeepFlamePurple, MathF.Sin(pulsePhase + i) * 0.5f + 0.5f);
                dragonColor *= 0.55f * circleScale;
                dragonColor.A = 0;

                sb.Draw(waveTex, dragonPos - Main.screenPosition, null, dragonColor, angle, waveOrigin,
                    new Vector2(0.45f * circleScale, 0.12f * circleScale), SpriteEffects.None, 0f);
            }

            Texture2D lightTex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 lightOrigin = lightTex.Size() / 2f;
            float coreScale = 0.55f + MathF.Sin(pulsePhase * 1.4f) * 0.12f;

            Color coreColor = AokinHelper.BlazingGold * circleScale;
            coreColor.A = 0;
            sb.Draw(lightTex, Projectile.Center - Main.screenPosition, null, coreColor, 0f, lightOrigin, coreScale * circleScale, SpriteEffects.None, 0f);

            Color haloColor = AokinHelper.DragonFlameRed * (0.35f * circleScale);
            haloColor.A = 0;
            sb.Draw(lightTex, Projectile.Center - Main.screenPosition, null, haloColor, 0f, lightOrigin, 1.4f * coreScale * circleScale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server)
                return;

            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 焰陨流星 - 龙纹法阵唤下的友好陨石
    /// </summary>
    public class MeteorCallerMeteor : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float firePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            firePhase += 0.12f;
            Projectile.rotation += 0.12f;

            if (Projectile.velocity.Y < 22f)
                Projectile.velocity.Y += 0.35f;

            if (Main.netMode != NetmodeID.Server) {
                AokinHelper.CreateFireTrail(Projectile.Center, Projectile.velocity, 1.1f);

                if (Main.rand.NextBool(3)) {
                    int dust = Dust.NewDust(Projectile.Center + Main.rand.NextVector2Circular(8, 8), 0, 0, DustID.SolarFlare, 0, -2, 100, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -Projectile.velocity * 0.15f;
                }
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.DragonFlameRed.ToVector3() * 0.75f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 240);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(firePhase * 3f) * 0.15f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AokinHelper.BlazingGold, AokinHelper.DragonFlameRed, 1f - progress);
                trailColor *= progress * 0.55f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.rotation, origin, (0.55f + progress * 0.35f) * pulse, SpriteEffects.None, 0f);
            }

            Color outerColor = AokinHelper.DragonFlameRed * (0.45f * pulse);
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, Projectile.rotation, origin, 1.2f * pulse, SpriteEffects.None, 0f);

            Color midColor = AokinHelper.MoltenOrange * (0.65f * pulse);
            midColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, midColor, Projectile.rotation, origin, 0.75f * pulse, SpriteEffects.None, 0f);

            Color coreColor = AokinHelper.BlazingGold * 0.95f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, Projectile.rotation, origin, 0.42f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.myPlayer == Projectile.owner) {
                Projectile.NewProjectile(
                    Projectile.GetSource_Death(),
                    Projectile.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<MeteorCallerFlamePool>(),
                    Math.Max(1, (int)(Projectile.damage * 0.3f)),
                    0f,
                    Projectile.owner);
            }

            if (Main.netMode == NetmodeID.Server)
                return;

            AokinHelper.CreateDragonFireBurst(Projectile.Center, 48f, 2, 10);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.1f, Volume = 0.55f }, Projectile.Center);

            if (ACMAsset.SlashBurst != null) {
                for (int i = 0; i < 3; i++) {
                    var d = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare, Main.rand.NextVector2Circular(2f, 2f));
                    d.noGravity = true;
                    d.scale = 2.4f;
                }
            }

            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>焰陨余烬池 — 流星陨落后残留的灼热熔池，持续灼烧驻足的敌人。</summary>
    public class MeteorCallerFlamePool : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float scaleUp;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            scaleUp = MathHelper.Lerp(scaleUp, 1f, 0.1f);
            Projectile.velocity = Vector2.Zero;

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(45f, 18f);
                    var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare);
                    d.noGravity = true;
                    d.scale = 1.6f * scaleUp;
                    d.velocity = new Vector2(0f, -Main.rand.NextFloat(1f, 3f));
                }
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.DragonFlameRed.ToVector3() * 0.7f * scaleUp);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 120);
            InfernoDragonSpearGlobalNPC.ApplyBrand(target, 1);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dx = MathF.Abs(targetHitbox.Center.X - Projectile.Center.X);
            float dy = MathF.Abs(targetHitbox.Center.Y - Projectile.Center.Y);
            return dx < 55f * scaleUp && dy < 32f * scaleUp;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            float pulse = 0.85f + MathF.Sin((float)Main.timeForVisualEffects * 0.2f) * 0.15f;
            float fade = Math.Min(Projectile.timeLeft / 40f, 1f);

            Color outer = AokinHelper.DragonFlameRed * 0.5f * fade * pulse;
            outer.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, outer, 0f, origin,
                new Vector2(1.4f, 0.7f) * scaleUp * pulse, SpriteEffects.None, 0f);

            Color core = AokinHelper.MoltenOrange * 0.45f * fade;
            core.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, core, 0f, origin,
                new Vector2(0.9f, 0.45f) * scaleUp, SpriteEffects.None, 0f);
            return false;
        }
    }
}
