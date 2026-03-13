using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Spectres
{
    /// <summary>
    /// 小怨灵 - 怨灵Boss召唤的辅助怪物
    /// </summary>
    public class SpectreMinion : ModNPC
    {
        public override string Texture => SpectreHelper.Path + "SpectreSoul";

        private float pulsePhase = 0f;
        private float orbitAngle = 0f;
        private int attackTimer = 0;

        // 宿主Boss索引
        private int OwnerIndex => (int)NPC.ai[0];

        // 获取宿主
        private NPC Owner {
            get {
                if (OwnerIndex >= 0 && OwnerIndex < Main.maxNPCs && Main.npc[OwnerIndex].active) {
                    return Main.npc[OwnerIndex];
                }
                return null;
            }
        }

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.width = 40;
            NPC.height = 40;
            NPC.damage = 50;
            NPC.defense = 20;
            NPC.lifeMax = 8000;
            NPC.HitSound = SoundID.NPCHit54;
            NPC.DeathSound = SoundID.NPCDeath52;
            NPC.value = 0f;
            NPC.knockBackResist = 0.2f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.alpha = 80;
        }

        public override void AI() {
            // 如果宿主死亡，自己也消散
            if (Owner == null || !Owner.active) {
                NPC.life = 0;
                NPC.active = false;
                OnKill();
                return;
            }

            pulsePhase += 0.1f;
            orbitAngle += 0.03f;
            attackTimer++;

            // 目标选择
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                // 回到宿主身边
                Vector2 toOwner = (Owner.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                NPC.velocity = Vector2.Lerp(NPC.velocity, toOwner * 8f, 0.05f);
            }
            else {
                // 行为模式：环绕并攻击
                RunCombatAI(target);
            }

            // 粒子效果
            CreateAmbientParticles();

            // 发光
            Lighting.AddLight(NPC.Center, SpectreHelper.SpectreCyan.ToVector3() * 0.3f);
        }

        private void RunCombatAI(Player target) {
            // 在玩家周围游荡
            float orbitRadius = 180f + MathF.Sin(pulsePhase * 0.5f) * 30f;
            Vector2 targetPos = target.Center + new Vector2(
                MathF.Cos(orbitAngle + NPC.whoAmI * MathHelper.PiOver2) * orbitRadius,
                MathF.Sin(orbitAngle + NPC.whoAmI * MathHelper.PiOver2) * orbitRadius * 0.6f
            );

            Vector2 toTarget = targetPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 0.08f, 0.06f);

            // 攻击
            if (attackTimer % 90 == 0) {
                ShootAtTarget(target);
            }
        }

        private void ShootAtTarget(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                NPC.Center,
                toTarget * 8f,
                ModContent.ProjectileType<SpectreSoulOrb>(),
                NPC.damage / 2,
                1f,
                ai0: Main.rand.Next(2)
            );

            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.5f, Volume = 0.7f }, NPC.Center);
        }

        private void CreateAmbientParticles() {
            if (Main.rand.NextBool(4)) {
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch;
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(20, 20), dustType);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = Main.rand.NextVector2Circular(1, 1);
                d.alpha = 120;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 拖尾
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;

                Vector2 pos = NPC.oldPos[i] + NPC.Size / 2 - screenPos;
                float progress = 1f - i / (float)NPC.oldPos.Length;
                float fade = progress * 0.3f;

                Color trailColor = Color.Lerp(SpectreHelper.SpectreDeepCyan, SpectreHelper.SpectreCyan, progress);
                trailColor *= fade;
                trailColor.A = 0;

                spriteBatch.Draw(tex, pos, null, trailColor, NPC.rotation, origin,
                    NPC.scale * (0.5f + progress * 0.5f), SpriteEffects.None, 0);
            }

            // 主体
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.1f;
            Color mainColor = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, 0.3f);

            // 光晕
            Color glowColor = mainColor;
            glowColor.A = 0;
            for (int i = 2; i >= 0; i--) {
                float glowScale = NPC.scale * pulse * (1.2f + i * 0.1f);
                spriteBatch.Draw(tex, NPC.Center - screenPos, null, glowColor * (0.1f / (i + 1)),
                    NPC.rotation, origin, glowScale, SpriteEffects.None, 0);
            }

            spriteBatch.Draw(tex, NPC.Center - screenPos, null, mainColor, NPC.rotation, origin,
                NPC.scale * pulse, SpriteEffects.None, 0);

            return false;
        }

        public override void OnKill() {
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.3f, Volume = 0.8f }, NPC.Center);
            SpectreHelper.CreateSpectreBurst(NPC.Center, 50f, 2, 10);

            for (int i = 0; i < 15; i++) {
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch;
                var d = Dust.NewDustPerfect(NPC.Center, dustType);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = Main.rand.NextVector2Circular(6, 6);
            }
        }

        public override bool CheckActive() {
            // 如果宿主存在，不要自动消失
            return Owner == null || !Owner.active;
        }
    }
}
