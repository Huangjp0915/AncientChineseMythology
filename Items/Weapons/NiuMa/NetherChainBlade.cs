using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.NiuMa
{
    /// <summary>
    /// 冥链刃 — 牛头马面掉落链刃
    /// 掷出后沿锁链飞出并回收；命中时可勾连两名敌人，链间持续冥火伤害
    /// </summary>
    public class NetherChainBlade : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 58;
            Item.DamageType = DamageClass.Melee;
            Item.width = 52;
            Item.height = 52;
            Item.useTime = Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<NetherChainBladeProjectile>();
            Item.shootSpeed = 14f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<NetherChainBladeProjectile>()] < 1;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.ChainKnife;
    }

    public class NetherChainBladeProjectile : ModProjectile
    {
        private const float MaxRange = 360f;
        private const float OutboundTime = 28f;
        private const float ChainPulseInterval = 42f;
        private const float HookSearchRange = 420f;

        private Player Owner => Main.player[Projectile.owner];

        private ref float FlightTimer => ref Projectile.ai[0];
        private ref float HookA => ref Projectile.ai[1];
        private ref float HookB => ref Projectile.ai[2];
        private ref float ChainPulseTimer => ref Projectile.localAI[0];
        private ref float PulsePhase => ref Projectile.localAI[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void OnSpawn(IEntitySource source) {
            HookA = -1f;
            HookB = -1f;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems) {
                Projectile.Kill();
                return;
            }

            Owner.itemAnimation = Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;
            Owner.ChangeDir(Projectile.Center.X > Owner.Center.X ? 1 : -1);

            PulsePhase += 0.12f;
            Projectile.rotation += 0.42f * Math.Sign(Projectile.velocity.X == 0f ? Owner.direction : Projectile.velocity.X);

            FlightTimer++;
            bool returning = FlightTimer >= OutboundTime
                || Vector2.Distance(Projectile.Center, Owner.Center) > MaxRange;

            if (returning) {
                Projectile.tileCollide = false;
                Vector2 toOwner = Owner.Center - Projectile.Center;
                float dist = toOwner.Length();
                if (dist < 24f) {
                    Projectile.Kill();
                    return;
                }

                Vector2 desired = toOwner.SafeNormalize(Vector2.Zero) * MathHelper.Lerp(12f, 20f, 1f - dist / MaxRange);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);
            }
            else {
                Projectile.velocity *= 0.985f;
            }

            UpdateDualHook();
            SpawnTrailDust();
            Lighting.AddLight(Projectile.Center, 0.35f, 0.15f, 0.55f);
        }

        private void UpdateDualHook() {
            NPC first = GetHookedNpc(HookA);
            NPC second = GetHookedNpc(HookB);

            if (first == null && second == null)
                return;

            if (first != null && second == null && Projectile.owner == Main.myPlayer) {
                NPC nearest = FindHookPartner(first);
                if (nearest != null)
                    RegisterSecondHook(nearest);
            }

            if (first == null || second == null)
                return;

            first.AddBuff(BuffID.ShadowFlame, 30);
            second.AddBuff(BuffID.ShadowFlame, 30);

            ChainPulseTimer++;
            if (ChainPulseTimer < ChainPulseInterval)
                return;

            ChainPulseTimer = 0f;
            if (Projectile.owner != Main.myPlayer)
                return;

            int chainDamage = Math.Max(1, (int)(Projectile.damage * 0.35f));
            int hitDir = Math.Sign(second.Center.X - first.Center.X);
            first.SimpleStrikeNPC(chainDamage, hitDir, false, 0f, null, false, 0, true);
            second.SimpleStrikeNPC(chainDamage, -hitDir, false, 0f, null, false, 0, true);
            SpawnHookPulseDust(first.Center, second.Center);
            SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.35f, Pitch = 0.15f }, Vector2.Lerp(first.Center, second.Center, 0.5f));
        }

        private void RegisterSecondHook(NPC target) {
            HookB = target.whoAmI;
            target.AddBuff(BuffID.Slow, 240);
            NPC first = GetHookedNpc(HookA);
            if (first == null)
                return;

            first.AddBuff(BuffID.Slow, 240);

            SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.55f, Pitch = -0.1f }, target.Center);
            for (int i = 0; i < 12; i++) {
                float t = i / 12f;
                Vector2 pos = Vector2.Lerp(first.Center, target.Center, t);
                Dust d = Dust.NewDustPerfect(pos, DustID.Shadowflame, Main.rand.NextVector2Circular(2f, 2f), 60, default, 1.4f);
                d.noGravity = true;
            }
        }

        private void TryRegisterFirstHook(NPC target) {
            if (HookA >= 0f)
                return;

            HookA = target.whoAmI;
            target.AddBuff(BuffID.Slow, 180);
            SoundEngine.PlaySound(SoundID.NPCHit7 with { Volume = 0.45f, Pitch = 0.2f }, target.Center);
        }

        private NPC GetHookedNpc(float index) {
            int whoAmI = (int)index;
            if (whoAmI < 0 || whoAmI >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[whoAmI];
            return npc.active && npc.CanBeChasedBy(Projectile) ? npc : null;
        }

        private NPC FindHookPartner(NPC primary) {
            float closest = HookSearchRange;
            NPC best = null;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile) || npc.whoAmI == primary.whoAmI)
                    continue;

                float dist = Vector2.Distance(primary.Center, npc.Center);
                if (dist >= closest)
                    continue;

                closest = dist;
                best = npc;
            }

            return best;
        }

        private void SpawnTrailDust() {
            if (!Main.rand.NextBool(3))
                return;

            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame,
                -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(1f, 1f), 70, default, 1.1f);
            d.noGravity = true;
        }

        private static void SpawnHookPulseDust(Vector2 start, Vector2 end) {
            for (int i = 0; i < 10; i++) {
                float t = i / 10f;
                Vector2 pos = Vector2.Lerp(start, end, t) + Main.rand.NextVector2Circular(6f, 6f);
                Dust d = Dust.NewDustPerfect(pos, DustID.Shadowflame, Main.rand.NextVector2Circular(3f, 3f), 50, default, 1.5f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            TryRegisterFirstHook(target);

            if (HookA >= 0f && HookB < 0f && target.whoAmI != (int)HookA && Projectile.owner == Main.myPlayer) {
                RegisterSecondHook(target);
            }

            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, Main.rand.NextVector2Circular(4f, 4f), 60, default, 1.2f);
                d.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity * -0.35f;
            FlightTimer = OutboundTime;
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.45f, Pitch = -0.15f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame,
                    (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero).RotatedByRandom(0.6) * Main.rand.NextFloat(2f, 5f),
                    50, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawPlayerChain();
            DrawHookChain();
            DrawBlade(lightColor);
            return false;
        }

        private void DrawPlayerChain() {
            Texture2D chainTex = TextureAssets.Chains[0].Value;
            Vector2 start = Owner.Center;
            Vector2 end = Projectile.Center;
            DrawChainSegment(chainTex, start, end, new Color(90, 70, 110), 0.85f);
        }

        private void DrawHookChain() {
            NPC first = GetHookedNpc(HookA);
            NPC second = GetHookedNpc(HookB);
            if (first == null || second == null)
                return;

            Texture2D chainTex = TextureAssets.Chains[0].Value;
            Color hookColor = Color.Lerp(new Color(120, 80, 170), new Color(180, 120, 220), 0.5f + MathF.Sin(PulsePhase) * 0.5f);
            DrawChainSegment(chainTex, first.Center, second.Center, hookColor, 1.05f);
        }

        private static void DrawChainSegment(Texture2D chainTex, Vector2 start, Vector2 end, Color color, float scale) {
            float dist = Vector2.Distance(start, end);
            if (dist < 8f)
                return;

            SpriteBatch sb = Main.spriteBatch;
            Rectangle frame = new(0, 0, chainTex.Width, Math.Max(4, (int)(dist * 0.92f)));
            float rotation = (start - end).ToRotation() - MathHelper.PiOver2;
            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);
            Vector2 drawPos = start - Main.screenPosition;

            Color glow = color;
            glow.A = 0;
            sb.Draw(chainTex, drawPos, frame, glow * 0.35f, rotation, origin, scale * 1.15f, SpriteEffects.None, 0f);
            sb.Draw(chainTex, drawPos, frame, color, rotation, origin, scale, SpriteEffects.None, 0f);
        }

        private void DrawBlade(Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[ProjectileID.ChainKnife].Value;
            Vector2 origin = texture.Size() * 0.5f;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            Color glow = new Color(170, 120, 220) * (0.35f + MathF.Sin(PulsePhase * 2f) * 0.1f);
            glow.A = 0;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glow,
                Projectile.rotation, origin, Projectile.scale * 1.12f, SpriteEffects.None, 0);
        }

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ChainKnife;
    }
}
