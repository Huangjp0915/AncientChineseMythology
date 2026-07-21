using AncientChineseMythology.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙基础类 (V3)。
    ///
    /// 相对 V2 的身体层升级:
    ///   ● 鞭波弹簧链 — 每节持有横向 <see cref="SpringOffset"/>, 与父节软耦合传播;
    ///     头部一次冲刺/受击脉冲沿 34 节身体行进一秒 (MOTION §4 whip chain), 纯视觉不影响判定。
    ///   ● 冥焰披风 — 身体的体积感交给头部铺设的 NetherDragonRibbon 条带, 删除每节每帧的
    ///     dust 风暴 (V2 峰值 500+/帧)。
    ///   ● 演出协议 — 读头部 <see cref="NetherDragonHead.BodyHidden"/> (门内隐匿) 与
    ///     <see cref="NetherDragonHead.ContactDamageOn"/> (伤害窗口), 以及死亡逐节爆燃波
    ///     (<see cref="NetherDragonHead.DeathWaveFront"/> 过境即爆)。
    ///   ● P1《巡墓》幽火留痕机制保留 (每 4 节取 1, 计时错相)。
    /// </summary>
    public abstract class NetherDragon : BasicWorm
    {
        public override bool IsUseSpriteDirection => true;

        // —— 鞭波弹簧链 (纯视觉) ——
        public float SpringOffset;
        public float SpringVel;

        // —— 演出状态 (纯本地) ——
        protected float hitFlash;          // 受击白闪 0~1
        protected bool deathExploded;      // 死亡波前已过境 (爆燃后隐灭)

        public Player Target {
            get {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers ||
                    Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }

        /// <summary>头部 ModNPC (头返回自身; 体节经 realLife 取头; 头不在返回 null)。</summary>
        public NetherDragonHead HeadBoss {
            get {
                if (this is NetherDragonHead self)
                    return self;
                if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active &&
                    Main.npc[NPC.realLife].ModNPC is NetherDragonHead head)
                    return head;
                return null;
            }
        }

        /// <summary>读取头部当前阶段; 头不在则默认 1。</summary>
        protected int HeadPhase => HeadBoss?.Phase ?? 1;

        /// <summary>绘制/条带用中心 = 实位置 + 鞭波横向偏移。</summary>
        public Vector2 VisualCenter => NPC.Center + SpringPerp * SpringOffset;

        /// <summary>身体横向单位向量 (垂直于行进方向)。</summary>
        protected Vector2 SpringPerp {
            get {
                Vector2 dir = NPC.velocity;
                if (dir.LengthSquared() < 0.01f)
                    dir = NPC.rotation.ToRotationVector2();
                dir = dir.SafeNormalize(Vector2.UnitX);
                return new Vector2(-dir.Y, dir.X);
            }
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 60;
            NPC.height = 60;
            NPC.lifeMax = 80000;
            NPC.damage = 80;
            NPC.defense = 35;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.knockBackResist = 0f;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Underworld");
            // V2 的 60 节拖成两屏长龙; 34 节整条 1.5 屏内可读, 速度感与性能双赢
            SummonMax = 34;
        }

        public override void BossHeadRotation(ref float rotation) {
            rotation = NPC.rotation + MathHelper.PiOver2 * NPC.spriteDirection;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            if (NPCWormType != WormType.Head) {
                return false;
            }
            return null;
        }

        private int TrailDamage => Main.masterMode ? 45 : (Main.expertMode ? 35 : 24);

        public override void AI() {
            base.AI();
            NetherDragonHead head = HeadBoss;

            // 无敌与伤害窗口全部由头部裁决并传播 (演出期免伤 / 门内隐匿零伤害)
            if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active)
                NPC.dontTakeDamage = Main.npc[NPC.realLife].dontTakeDamage;

            bool hidden = head != null && head.BodyHidden;
            bool damageOn = head == null || (head.ContactDamageOn && !hidden);
            NPC.damage = damageOn ? NPC.defDamage : 0;

            // —— 鞭波弹簧链: 与父节软耦合, 行进波沿身传播后自然衰减 ——
            if (NPCWormType == WormType.Head) {
                SpringOffset *= 0.92f;   // 头部脉冲源自然回落
            }
            else if (FatherNPC.Alives() && FatherNPC.ModNPC is NetherDragon father) {
                SpringVel += (father.SpringOffset - SpringOffset) * 0.30f;
                SpringVel *= 0.80f;
                SpringOffset += SpringVel;
                SpringOffset *= 0.94f;
            }

            hitFlash = MathHelper.Lerp(hitFlash, 0f, 0.10f);

            // —— 死亡逐节爆燃波: 波前 (尾→头) 过境本节即一次性爆燃并隐灭 ——
            if (!deathExploded && NPCWormType != WormType.Head && head != null &&
                head.DeathWaveActive && head.DeathWaveFront <= SummonCount) {
                deathExploded = true;
                SpringOffset += Main.rand.NextFloat(-14f, 14f);
                if (!Main.dedServ) {
                    float progress = 1f - SummonCount / (float)Math.Max(1, SummonMax + 1);
                    SoundEngine.PlaySound(SoundID.NPCDeath39 with {
                        Volume = 0.55f, Pitch = -0.4f + progress * 0.8f
                    }, NPC.Center);
                    ACMUtils.AddScreenShake(2.5f);
                    for (int i = 0; i < 16; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(16f, 16f),
                            Main.rand.NextBool() ? DustID.GreenTorch : DustID.PurpleTorch, Vector2.Zero, 90,
                            new Color(110, 230, 150), Main.rand.NextFloat(1.4f, 2.4f));
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(5f, 5f);
                    }
                }
            }

            // —— P1《巡墓》: 身段沿途留驻留幽火 DoT 残痕 (可读空隙, 保留 V2 机制) ——
            // 仅 body/tail 留痕; 每 4 节取 1 → 空间空隙; 计时器错相 → 时间空隙。
            if (!hidden && !deathExploded && NPCWormType != WormType.Head &&
                HeadPhase == 1 && SummonCount % 4 == 0) {
                NPC.localAI[1] += 1f;
                if (NPC.localAI[1] >= 55f) {
                    NPC.localAI[1] = Main.rand.Next(8); // 错相, 避免整排同时落痕
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<NetherFlameTrail>(), TrailDamage, 0f);
                }
            }

            if (!hidden && !deathExploded)
                Lighting.AddLight(NPC.Center, 0.12f, 0.32f, 0.2f);
        }

        public override void HitEffect(NPC.HitInfo hit) {
            hitFlash = 1f;
            // 受击的横向反冲脉冲 → 沿身传播一小段鞭波 (mass is reaction)
            SpringOffset = MathHelper.Clamp(SpringOffset + hit.HitDirection * -5f, -26f, 26f);
            if (Main.dedServ || deathExploded)
                return;
            for (int i = 0; i < 3; i++) {
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(14f, 14f),
                    DustID.GreenTorch, Vector2.Zero, 110, new Color(110, 230, 150), 1.3f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(2.5f, 2.5f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            NetherDragonHead head = HeadBoss;
            if (deathExploded || (head != null && head.BodyHidden))
                return false;

            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 drawPos = VisualCenter - screenPos;
            SpriteEffects fx = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // 幽蓝紫基调; 暴怒时体节泛红
            Color netherColor = Color.Lerp(drawColor, new Color(120, 90, 200), 0.4f);
            if (head != null && head.EnrageVis > 0.01f)
                netherColor = Color.Lerp(netherColor, new Color(235, 90, 80), head.EnrageVis * 0.45f);

            spriteBatch.Draw(tex, drawPos, null, netherColor, NPC.rotation + MathHelper.PiOver2,
                origin, NPC.scale, fx, 0);

            // 受击白闪 (加性覆层)
            if (hitFlash > 0.05f) {
                Color flash = Color.White with { A = 0 };
                spriteBatch.Draw(tex, drawPos, null, flash * (hitFlash * 0.55f), NPC.rotation + MathHelper.PiOver2,
                    origin, NPC.scale, fx, 0);
            }

            return false;
        }
    }
}
