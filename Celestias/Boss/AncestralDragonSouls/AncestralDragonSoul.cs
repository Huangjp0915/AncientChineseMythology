using AncientChineseMythology.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls
{
    /// <summary>
    /// 祖龙残魂基类 - 迷幻仙气风格的大后期超级Boss
    /// 颜色风格偏向白色和雾气，具有空灵飘渺的视觉效果
    /// </summary>
    public abstract class AncestralDragonSoul : BasicWorm
    {
        public override bool IsUseSpriteDirection => true;

        /// <summary>获取当前目标玩家</summary>
        public Player Target {
            get {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers ||
                    Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }

        /// <summary>全局时间计数器</summary>
        protected float globalTime;

        /// <summary>雾气透明度</summary>
        protected float mistAlpha = 0.6f;

        /// <summary>灵魂脉动相位</summary>
        protected float soulPulsePhase;

        /// <summary>体节索引,用于蛇形波计算。头部为0,尾部为SummonMax</summary>
        public int segmentIndex = 0;

        /// <summary>是否为分裂出的副本龙</summary>
        public bool IsTwin;

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 80;
            NPC.height = 80;
            NPC.lifeMax = 8000000; // 超级Boss血量
            NPC.damage = 320;
            NPC.defense = 120;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.knockBackResist = 0f;
            NPC.HitSound = SoundID.NPCHit54;
            NPC.DeathSound = SoundID.NPCDeath52;
            SummonMax = 80; // 超长身体

            // 难度调整
            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.4f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }
        }

        public override void BossHeadRotation(ref float rotation) {
            rotation = NPC.rotation + MathHelper.PiOver2 * NPC.spriteDirection;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            if (NPCWormType != WormType.Head) {
                return false;
            }
            return null;
        }

        public override void AI() {
            base.AI();

            globalTime += 1f / 60f;
            soulPulsePhase += 0.08f;

            // 如果跟随父级，更新连接
            if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active) {
                NPC.dontTakeDamage = Main.npc[NPC.realLife].dontTakeDamage;
            }

            // 身体段连接粒子效果
            if (FatherNPC.Alives()) {
                SpawnConnectionParticles();
            }

            // 龙魂发光效果
            float pulseIntensity = 0.6f + MathF.Sin(soulPulsePhase) * 0.2f;
            Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.95f, 1f) * pulseIntensity);
        }

        /// <summary>
        /// 蛇形鞭梢运动:父节点锚点+垂直正弦摆动+速度传递+拖尾延迟
        /// 相比原版的硬Lerp,这种算法让身体拥有更强的惯性感和鞭打张力,体现超级Boss的压迫感
        /// </summary>
        public override void ChangePos() {
            if (FatherNPC == null || !FatherNPC.active) {
                return;
            }

            Vector2 toParent = FatherNPC.Center - NPC.Center;
            float targetDist = (FatherNPC.width + NPC.width) / 2f;
            Vector2 dirToParent = toParent.SafeNormalize(Vector2.UnitY);

            // 锚点:父节点后方固定距离
            Vector2 anchor = FatherNPC.Center - dirToParent * targetDist;

            // 蛇形波:沿身体传导,越靠近尾部幅度越大
            float segPhase = globalTime * 5.2f - segmentIndex * 0.42f;
            float parentSpeed = FatherNPC.velocity.Length();
            float speedFactor = MathHelper.Clamp(parentSpeed / 18f, 0.35f, 1.5f);
            float segFactor = MathHelper.Clamp(segmentIndex / 30f, 0.4f, 1.3f);
            float waveAmp = 15f * speedFactor * segFactor;
            Vector2 perp = dirToParent.RotatedBy(MathHelper.PiOver2);
            anchor += perp * MathF.Sin(segPhase) * waveAmp;

            // 拖尾式插值,保留惯性
            Vector2 newCenter = Vector2.Lerp(NPC.Center, anchor, 0.4f);
            Vector2 delta = anchor - newCenter;

            // 继承父节点速度让整条龙体甩动更富张力
            NPC.velocity = delta + FatherNPC.velocity * 0.18f;

            // 限速保护
            const float maxSpeed = 55f;
            if (NPC.velocity.LengthSquared() > maxSpeed * maxSpeed) {
                NPC.velocity = NPC.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }

            NPC.Center = new Vector2((int)newCenter.X, (int)newCenter.Y);
        }

        /// <summary>生成身体段之间的连接粒子</summary>
        protected virtual void SpawnConnectionParticles() {
            if (Main.netMode == NetmodeID.Server) return;

            Vector2 midPoint = NPC.Center + NPC.Center.To(FatherNPC.Center) / 2;

            // 白色仙气粒子
            for (int i = 0; i < (int)(NPC.velocity.Length() / 3); i++) {
                if (Main.rand.NextBool(3)) {
                    int dustType = Main.rand.NextBool(3) ? DustID.Cloud : DustID.WhiteTorch;
                    int dust = Dust.NewDust(midPoint + Main.rand.NextVector2Circular(15, 15), 1, 1, dustType, 0, 0, 200, new Color(240, 245, 255), 1.2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = NPC.velocity.RotatedByRandom(0.4f) * 0.3f;
                    Main.dust[dust].fadeIn = 1.2f;
                }
            }

            // 龙魂残影粒子
            if (Main.rand.NextBool(8)) {
                Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(40, 40);
                int dust = Dust.NewDust(dustPos, 1, 1, DustID.Clentaminator_Cyan, 0, 0, 150, Color.White, 0.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(1f, 1f);
                Main.dust[dust].alpha = 180;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 计算灵魂脉动效果
            float soulPulse = 1f + MathF.Sin(soulPulsePhase + NPC.whoAmI * 0.3f) * 0.08f;

            // 迷幻仙气色调 - 白色偏青的空灵效果
            Color mistColor = Color.Lerp(drawColor, new Color(230, 240, 255), 0.5f);
            mistColor = Color.Lerp(mistColor, Color.White, 0.3f);

            // 外层光晕（迷幻效果）
            DrawMysticalGlow(spriteBatch, screenPos, tex, origin, soulPulse);

            // 绘制拖尾（仙气效果）
            if (NPCWormType == WormType.Head) {
                DrawEtherealTrail(spriteBatch, screenPos, tex, origin);
            }

            // 主体绘制
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, mistColor * NPC.Opacity,
                NPC.rotation, origin, NPC.scale * soulPulse, effects, 0f);

            // 内层发光
            Color innerGlow = new Color(255, 255, 255) * 0.3f * soulPulse;
            innerGlow.A = 0;
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, innerGlow,
                NPC.rotation, origin, NPC.scale * 0.9f, effects, 0f);

            return false;
        }

        /// <summary>绘制迷幻光晕效果</summary>
        protected virtual void DrawMysticalGlow(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D tex, Vector2 origin, float pulse) {
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // 多层光晕营造迷幻效果
            for (int i = 0; i < 3; i++) {
                float layerOffset = i * 0.15f;
                float layerScale = 1.1f + i * 0.1f + MathF.Sin(soulPulsePhase * 2f + i) * 0.05f;
                float layerAlpha = (0.25f - i * 0.06f) * mistAlpha;

                // 颜色渐变：白色 -> 淡青 -> 淡紫
                Color layerColor = i switch {
                    0 => new Color(255, 255, 255),
                    1 => new Color(220, 240, 255),
                    _ => new Color(230, 220, 255)
                };
                layerColor *= layerAlpha;
                layerColor.A = 0;

                // 轻微的位置偏移创造飘渺感
                Vector2 offset = new Vector2(MathF.Sin(globalTime * 2f + i), MathF.Cos(globalTime * 1.5f + i)) * 3f;

                spriteBatch.Draw(tex, NPC.Center + offset - screenPos, null, layerColor,
                    NPC.rotation, origin, NPC.scale * layerScale * pulse, effects, 0f);
            }
        }

        /// <summary>绘制空灵拖尾效果</summary>
        protected virtual void DrawEtherealTrail(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D tex, Vector2 origin) {
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / NPC.oldPos.Length;
                float trailAlpha = progress * 0.2f * mistAlpha;

                // 白色到淡青的渐变拖尾
                Color trailColor = Color.Lerp(new Color(255, 255, 255), new Color(200, 230, 255), 1f - progress);
                trailColor *= trailAlpha;
                trailColor.A = 0;

                Vector2 pos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float scale = NPC.scale * progress * 0.95f;

                spriteBatch.Draw(tex, pos, null, trailColor,
                    NPC.oldRot[i], origin, scale, effects, 0f);
            }
        }
    }
}
