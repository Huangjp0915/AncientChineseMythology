using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.NiuMa
{
    /// <summary>
    /// 勾魂索 — 牛头马面掉落鞭
    /// 命中时将敌人勾向玩家，并施加灵魂侵蚀持续伤害
    /// </summary>
    public class SoulHookWhip : ModItem
    {
        public override void SetDefaults() {
            Item.DefaultToWhip(ModContent.ProjectileType<SoulHookWhipProjectile>(), 28, 2f, 2f, 20);
            Item.damage = 52;
            Item.knockBack = 4f;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.Pink;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.ThornWhip;
    }

    public class SoulHookWhipDebuff : ModBuff
    {
        private const float PullRadius = 640f;
        private const float MinPullDistance = 40f;

        public override string Texture => "Terraria/Images/Buff_" + BuffID.ShadowFlame;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.IsATagBuff[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            npc.lifeRegen -= 8;

            if (Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Shadowflame,
                    0f, 0f, 120, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
                d.velocity *= 0.4f;
            }

            Player owner = FindHookOwner(npc);
            if (owner == null)
                return;

            float dist = Vector2.Distance(owner.Center, npc.Center);
            if (dist < MinPullDistance || dist > PullRadius)
                return;

            Vector2 pullDir = (owner.Center - npc.Center).SafeNormalize(Vector2.Zero);
            float pullStrength = MathHelper.Lerp(0.45f, 0.1f, dist / PullRadius);
            pullStrength *= 1f - npc.knockBackResist * 0.85f;
            if (pullStrength <= 0f)
                return;

            npc.velocity += pullDir * pullStrength;
        }

        private static Player FindHookOwner(NPC npc) {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player.active && !player.dead && player.MinionAttackTargetNPC == npc.whoAmI)
                    return player;
            }

            return null;
        }
    }

    public class SoulHookWhipProjectile : ModProjectile
    {
        private const float HookImpulseMin = 3.5f;
        private const float HookImpulseMax = 9f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.DefaultToWhip();
            Projectile.WhipSettings.Segments = 20;
            Projectile.WhipSettings.RangeMultiplier = 1f;
        }

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ThornWhip;

        public override bool PreAI() {
            List<Vector2> points = Projectile.WhipPointsForCollision;
            if (points.Count <= 10 || !Main.rand.NextBool(3))
                return true;

            points.Clear();
            Projectile.FillWhipControlPoints(Projectile, points);
            int pointIndex = Main.rand.Next(10, points.Count);
            Rectangle spawnArea = Utils.CenteredRectangle(points[pointIndex], new Vector2(24f, 24f));

            Dust dust = Dust.NewDustDirect(spawnArea.TopLeft(), spawnArea.Width, spawnArea.Height,
                Main.rand.NextBool() ? DustID.Shadowflame : DustID.Wraith, 0f, 0f, 100, default,
                Main.rand.NextFloat(0.8f, 1.2f));
            dust.position = points[pointIndex];
            dust.noGravity = true;
            dust.velocity *= 0.35f;

            Vector2 segmentDir = points[pointIndex] - points[pointIndex - 1];
            dust.velocity += segmentDir.SafeNormalize(Vector2.Zero).RotatedBy(Main.player[Projectile.owner].direction * MathHelper.PiOver2) * 0.35f;

            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Player owner = Main.player[Projectile.owner];

            target.AddBuff(ModContent.BuffType<SoulHookWhipDebuff>(), 240);
            owner.MinionAttackTargetNPC = target.whoAmI;
            Projectile.damage = (int)(Projectile.damage * 0.75f);

            if (Projectile.owner != Main.myPlayer)
                return;

            Vector2 pullDir = (owner.Center - target.Center).SafeNormalize(Vector2.Zero);
            float dist = Vector2.Distance(owner.Center, target.Center);
            float impulse = MathHelper.Clamp(HookImpulseMax - dist / 90f, HookImpulseMin, HookImpulseMax);
            impulse *= 1f - target.knockBackResist * 0.85f;
            if (impulse > 0f)
                target.velocity += pullDir * impulse;

            for (int i = 0; i < 8; i++) {
                Vector2 velocity = pullDir.RotatedByRandom(0.8) * Main.rand.NextFloat(2f, 5f);
                Dust soul = Dust.NewDustPerfect(target.Center, DustID.Wraith, velocity, 80, default, Main.rand.NextFloat(1f, 1.5f));
                soul.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.35f, Pitch = 0.35f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            List<Vector2> points = new();
            Projectile.FillWhipControlPoints(Projectile, points);
            DrawSoulChain(points);
            Main.DrawWhip_WhipBland(Projectile, points);
            return false;
        }

        private static void DrawSoulChain(List<Vector2> points) {
            if (points.Count < 2)
                return;

            Texture2D chainTex = TextureAssets.Chains[0].Value;
            Vector2 start = points[0];
            Vector2 end = points[points.Count - 1];
            float dist = Vector2.Distance(start, end);
            if (dist < 12f)
                return;

            Rectangle frame = new(0, 0, chainTex.Width, (int)(dist * 0.92f));
            float rotation = (start - end).ToRotation() - MathHelper.PiOver2;
            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);
            Vector2 drawPos = start - Main.screenPosition;
            Color chainColor = new Color(110, 70, 160, 140);

            Main.EntitySpriteDraw(chainTex, drawPos, frame, chainColor, rotation, origin, 0.75f, SpriteEffects.None, 0);
        }
    }
}
