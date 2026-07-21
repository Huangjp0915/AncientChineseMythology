using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Revenants
{
    /// <summary>
    /// 亡魂系列·业镜刃迹条带绘制助手 (RevenantKarmaRibbon.fx 的唯一调用封装)。
    /// 顶点契约与 <see cref="ACMShaders.DrawBeam"/> 相同 (世界坐标 - screenPosition + GameViewMatrix)。
    /// 断业刀挥砍刃迹/居合刀痕与孽镜刃迹共用; 只能在有活动批的绘制阶段调用。
    /// </summary>
    public static class RevenantRibbonVFX
    {
        /// <param name="worldPoints">刃迹中心线世界坐标 (头→尾)。</param>
        /// <param name="baseWidth">根部半宽 (像素)。</param>
        /// <param name="core">芯色 (a=芯部权重)。</param>
        /// <param name="edge">缘色 (a=缘部权重)。</param>
        /// <param name="intensity">整体强度 0~1。</param>
        /// <param name="heat">业热 0~1 (芯部向判决朱红/白热偏移)。</param>
        /// <param name="ghost">折影强度 0~1 (镜中重像)。</param>
        public static void DrawKarmaRibbon(Vector2[] worldPoints, float baseWidth, Color core, Color edge,
            float intensity, float heat, float ghost, float uvScroll = 0f, int subdivisions = 3) {
            if (Main.dedServ || worldPoints == null || worldPoints.Length < 2 || intensity <= 0.01f)
                return;
            if (MythologyConfig.Trail == TrailQualityLevel.Off)
                return;
            if (MythologyConfig.Trail == TrailQualityLevel.Med)
                subdivisions = Math.Max(1, subdivisions / 2);

            Effect fx = WeaponVFX.GetEffect("RevenantKarmaRibbon");
            if (fx == null)
                return;

            Vector2[] pts = new Vector2[worldPoints.Length];
            for (int i = 0; i < worldPoints.Length; i++)
                pts[i] = worldPoints[i] - Main.screenPosition;

            var verts = ACMUtils.BuildRibbonStrip(pts,
                p => MathHelper.Lerp(baseWidth, baseWidth * 0.22f, p),
                _ => Color.White, uvScroll, subdivisions);
            if (verts.Length < 4)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uColorCore"]?.SetValue(core.ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(edge.ToVector4());
            fx.Parameters["uHeat"]?.SetValue(MathHelper.Clamp(heat, 0f, 1f));
            fx.Parameters["uGhost"]?.SetValue(MathHelper.Clamp(ghost, 0f, 1f));
            fx.Parameters["uFlowSpeed"]?.SetValue(2.6f);
            fx.Parameters["uFlowScale"]?.SetValue(2.2f);
            fx.Parameters["uCoreSharp"]?.SetValue(2.6f);
            fx.Parameters["uTaper"]?.SetValue(1.6f);

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D noise = ACMShaders.NoiseTexture;
            gd.Textures[0] = ACMAsset.SlashBurst ?? noise;
            gd.Textures[1] = noise;
            gd.SamplerStates[0] = SamplerState.LinearWrap;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }
    }

    /// <summary>
    /// 阎摩断业刀 - 阎王裁断众生业报的巨刀 (系列主旗舰, 手持弹幕近战)。
    /// 左键三连段: 横斩 → 回斩 → 过顶大劈 (释放断业剑气, +2 业);
    /// 右键勾决居合: 蓄势拔刀 → 瞬身位移斩 — 业 ≥4 者受断业判决 (消费业力放大伤害),
    /// 记名 (业≥1) 且残血 (<15%) 的非 Boss 直接处决。
    /// </summary>
    public class YamasSeverance : ModItem
    {
        /// <summary>连段计数 (0/1/2, owner 侧)。</summary>
        private int comboStep;
        /// <summary>上次挥砍的帧号 (超时回落到第一段)。</summary>
        private ulong lastSwingFrame;

        public override void SetDefaults() {
            Item.damage = 86;
            Item.crit = 8;
            Item.DamageType = DamageClass.Melee;
            Item.width = 64;
            Item.height = 64;
            Item.useTime = 26;
            Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 7f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = null; // 音效由手持弹幕分层播放
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<YamasSeveranceSwing>();
            Item.shootSpeed = 12f;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            // 场上有本刀的任何手持弹幕时不可再用 (连段/居合由弹幕自身控时长)
            return player.ownedProjectileCounts[ModContent.ProjectileType<YamasSeveranceSwing>()] < 1
                && player.ownedProjectileCounts[ModContent.ProjectileType<YamasVerdictIai>()] < 1;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);

            if (player.altFunctionUse == 2) {
                // 右键·勾决居合
                Projectile.NewProjectile(source, player.MountedCenter, dir,
                    ModContent.ProjectileType<YamasVerdictIai>(), damage, knockback, player.whoAmI);
                comboStep = 0;
                return false;
            }

            // 连段超时回落 (45 帧无后续挥砍则从第一段重来)
            if (Main.GameUpdateCount - lastSwingFrame > 45ul + 26ul)
                comboStep = 0;
            lastSwingFrame = Main.GameUpdateCount;

            Projectile.NewProjectile(source, player.MountedCenter, dir,
                ModContent.ProjectileType<YamasSeveranceSwing>(), damage, knockback, player.whoAmI,
                comboStep);
            comboStep = (comboStep + 1) % 3;
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
    /// 断业刀·三连段挥砍 (手持弹幕)。ai[0]=连段号 (0 横斩 / 1 回斩 / 2 过顶大劈), ai[1]=瞄准角。
    /// 波形: 前摇 (quad 回拉) → 爆发 (poly(9) ease-out, 数帧扫完全部弧程) → 收招 (smoothstep 回位淡出)。
    /// 大劈在爆发末帧释放 <see cref="YamasSeveranceSlash"/> 并震屏; 刃迹走 RevenantKarmaRibbon。
    /// </summary>
    public class YamasSeveranceSwing : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/YamasSeverance";

        private const float BladeLength = 108f;

        private int Combo => (int)Projectile.ai[0];
        private ref float AimAngle => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];

        private Player Owner => Main.player[Projectile.owner];

        // 连段参数: [前摇帧, 爆发帧, 收招帧, 起始角偏, 结束角偏, 伤害倍率]
        private int PrepTime => Combo == 2 ? 14 : (Combo == 1 ? 9 : 10);
        private int StrikeTime => Combo == 2 ? 5 : 4;
        private int RecoverTime => Combo == 2 ? 15 : 12;
        private float ArcBack => Combo == 2 ? -2.35f : -2.05f;
        private float ArcEnd => Combo == 2 ? 1.75f : 1.55f;
        /// <summary>挥向: 一段顺挥, 二段反挥, 大劈顺挥 (角度偏移的符号)。</summary>
        private float SwingSign => Combo == 1 ? -1f : 1f;

        // 刃尖轨迹环形缓存 (纯视觉)
        private readonly Vector2[] tipTrail = new Vector2[14];
        private int tipCount;

        private bool strikeSoundPlayed;
        private bool slashReleased;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 每次挥砍每目标至多一击
        }

        public override void OnSpawn(IEntitySource source) {
            AimAngle = Projectile.velocity.SafeNormalize(Vector2.UnitX).ToRotation();
            Projectile.velocity = Vector2.Zero;
            Projectile.spriteDirection = MathF.Cos(AimAngle) >= 0f ? 1 : -1;

            // 起手风声: 音高随连段上行 (听觉连段计数)
            SoundEngine.PlaySound(SoundID.Item7 with {
                Volume = 0.5f, Pitch = -0.3f + Combo * 0.18f + Main.rand.NextFloat(-0.05f, 0.05f)
            }, Owner.Center);
        }

        /// <summary>当前刃角 (前摇→爆发→收招合成波形)。</summary>
        private float CurrentOffset(out float phase01, out int phaseId) {
            float t = Timer;
            if (t < PrepTime) {
                // 前摇: 自然位 → 深回拉 (quad in-out); 大劈末 2 帧带蓄势抖动
                phaseId = 0;
                phase01 = t / PrepTime;
                float eased = MathHelper.SmoothStep(0f, 1f, phase01);
                float offset = MathHelper.Lerp(-0.45f, ArcBack, eased);
                if (Combo == 2 && phase01 > 0.6f)
                    offset += MathF.Sin(t * 2.3f) * 0.035f * ((phase01 - 0.6f) / 0.4f);
                return offset * SwingSign;
            }
            t -= PrepTime;
            if (t < StrikeTime) {
                // 爆发: poly(9) ease-out — 前 2 帧扫完 ~85% 弧程
                phaseId = 1;
                phase01 = t / StrikeTime;
                float eased = 1f - MathF.Pow(1f - phase01, 9f);
                return MathHelper.Lerp(ArcBack, ArcEnd, eased) * SwingSign;
            }
            // 收招: 少量顺势过冲后定格淡出
            t -= StrikeTime;
            phaseId = 2;
            phase01 = MathHelper.Clamp(t / RecoverTime, 0f, 1f);
            float drift = MathHelper.SmoothStep(ArcEnd, ArcEnd + 0.28f, phase01);
            return drift * SwingSign;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                Projectile.Kill();
                return;
            }

            Owner.heldProj = Projectile.whoAmI;
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.direction = Projectile.spriteDirection;

            float offset = CurrentOffset(out float phase01, out int phaseId);
            Projectile.rotation = AimAngle + offset;
            Projectile.Center = Owner.MountedCenter;

            float armRot = Projectile.rotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRot);

            Vector2 tip = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * BladeLength;

            // 爆发帧: 斩击音爆 + 刃风粒子沿弧喷出
            if (phaseId == 1) {
                if (!strikeSoundPlayed) {
                    strikeSoundPlayed = true;
                    SoundEngine.PlaySound(SoundID.Item71 with {
                        Volume = 0.85f, Pitch = (Combo == 2 ? -0.25f : 0.05f + Combo * 0.12f) + Main.rand.NextFloat(-0.06f, 0.06f)
                    }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = 0.1f }, Owner.Center);
                }
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustPerfect(
                        Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(50f, BladeLength),
                        DustID.Shadowflame,
                        (Projectile.rotation + MathHelper.PiOver2 * SwingSign).ToRotationVector2() * Main.rand.NextFloat(3f, 7f),
                        100, default, Main.rand.NextFloat(1.2f, 1.8f));
                    d.noGravity = true;
                }
            }

            // 大劈: 爆发末释放断业剑气 + 落点冲击 (owner 侧生成)
            if (Combo == 2 && phaseId == 2 && !slashReleased) {
                slashReleased = true;
                if (Projectile.owner == Main.myPlayer) {
                    Vector2 dir = AimAngle.ToRotationVector2();
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.MountedCenter + dir * 40f,
                        dir * 12f, ModContent.ProjectileType<YamasSeveranceSlash>(),
                        (int)(Projectile.damage * 0.7f), Projectile.knockBack * 0.5f, Projectile.owner);
                    ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), tip,
                        ACMWeaponBurst.AbyssPurple, scale: 1.15f, owner: Projectile.owner);
                }
                WeaponVFX.AddScreenShake(tip, 3f);
            }

            // 大劈前摇: 刀身聚拢业火 (收束粒子 = 蓄势可读)
            if (Combo == 2 && phaseId == 0 && phase01 > 0.3f && Main.rand.NextBool(2)) {
                Vector2 from = tip + Main.rand.NextVector2CircularEdge(46f, 46f);
                Dust d = Dust.NewDustPerfect(from, DustID.PurpleTorch, (tip - from) * 0.09f,
                    120, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
            }

            // 刃尖轨迹缓存 (仅爆发与收招前半段记录)
            if (phaseId == 1 || (phaseId == 2 && phase01 < 0.5f)) {
                for (int i = tipTrail.Length - 1; i > 0; i--)
                    tipTrail[i] = tipTrail[i - 1];
                tipTrail[0] = tip;
                tipCount = Math.Min(tipCount + 1, tipTrail.Length);
            }

            Lighting.AddLight(tip, 0.45f, 0.2f, 0.55f);

            Timer++;
            if (Timer >= PrepTime + StrikeTime + RecoverTime)
                Projectile.Kill();
        }

        public override bool? CanDamage() {
            CurrentOffset(out float phase01, out int phaseId);
            return phaseId == 1 || (phaseId == 2 && phase01 < 0.25f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * BladeLength;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 30f, ref _);
        }

        public override void CutTiles() {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * BladeLength;
            Utils.PlotTileLine(start, end, 30f, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (Combo == 2)
                modifiers.FinalDamage *= 1.15f;
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 180);

            // 积业: 一二段 +1, 大劈 +2
            RevenantKarma.AddKarma(Projectile, target, Combo == 2 ? 2 : 1);

            for (int i = 0; i < 8; i++) {
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.Shadowflame,
                    Main.rand.NextVector2Circular(6f, 6f), 100, default, Main.rand.NextFloat(1.4f, 2.0f));
                burst.noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: Combo == 2 ? 1.3f : 0.95f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, Combo == 2 ? 3f : 1.8f);

            SoundEngine.PlaySound(SoundID.NPCHit18 with {
                Volume = 0.5f, Pitch = -0.1f + Main.rand.NextFloat(-0.1f, 0.1f)
            }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            CurrentOffset(out float phase01, out int phaseId);

            // 刃迹条带 (爆发起亮, 收招淡出); 大劈高热
            if (tipCount >= 2) {
                float trailIntensity = phaseId == 1 ? 1f : (phaseId == 2 ? 1f - phase01 : 0f);
                if (trailIntensity > 0.02f) {
                    var pts = new Vector2[tipCount];
                    Array.Copy(tipTrail, pts, tipCount);
                    RevenantRibbonVFX.DrawKarmaRibbon(pts, 30f,
                        core: new Color(235, 170, 255, 230), edge: new Color(90, 30, 150, 160),
                        intensity: trailIntensity * 0.95f,
                        heat: Combo == 2 ? 0.75f : 0.3f, ghost: 0.35f,
                        uvScroll: Main.GlobalTimeWrappedHourly * 1.5f);
                }
            }

            // 刀身绘制 (柄在手, 刃指 rotation 方向; 贴图对角线朝右上)
            Vector2 handPos = Owner.MountedCenter - Main.screenPosition;
            Vector2 origin;
            float rotOffset;
            SpriteEffects fxFlip;
            if (Projectile.spriteDirection > 0) {
                origin = new Vector2(6f, texture.Height - 6f);
                rotOffset = MathHelper.PiOver4;
                fxFlip = SpriteEffects.None;
            }
            else {
                origin = new Vector2(texture.Width - 6f, texture.Height - 6f);
                rotOffset = MathHelper.Pi * 0.75f;
                fxFlip = SpriteEffects.FlipHorizontally;
            }

            Main.EntitySpriteDraw(texture, handPos, null, lightColor,
                Projectile.rotation + rotOffset, origin, Projectile.scale * 1.25f, fxFlip, 0);

            // 大劈前摇蓄势: 刀身沿刃线叠热浪光带 (heat 随前摇进度)
            if (Combo == 2 && phaseId == 0 && phase01 > 0.25f) {
                float heat = (phase01 - 0.25f) / 0.75f;
                Vector2 hilt = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * 14f;
                Vector2 tip = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * (BladeLength - 6f);
                RevenantRibbonVFX.DrawKarmaRibbon([hilt, tip], 12f,
                    core: new Color(255, 200, 160, 220), edge: new Color(160, 50, 90, 140),
                    intensity: 0.35f + heat * 0.5f, heat: heat, ghost: 0f);
            }

            // 爆发帧: 刀身加色重影 (速度感)
            if (phaseId == 1) {
                Color ghostCol = new Color(200, 120, 255) * 0.45f;
                ghostCol.A = 0;
                Main.EntitySpriteDraw(texture, handPos, null, ghostCol,
                    Projectile.rotation + rotOffset - 0.22f * SwingSign, origin, Projectile.scale * 1.25f, fxFlip, 0);
            }

            return false;
        }
    }

    /// <summary>
    /// 断业刀·勾决居合 (右键, 手持弹幕)。蓄势拔刀 16f (末 4 帧静默) → 瞬身 11f (24px/f 位移斩,
    /// 路径全判定) → 收刀 18f。业 ≥4 者受断业判决 (×(1.8+0.12×业), 业力清零 = 消费型宣判);
    /// 记名且残血 (<15%) 非 Boss 直接处决。处决/判决命中触发大招演出 (判决印 + 染屏 + 震屏 8)。
    /// </summary>
    public class YamasVerdictIai : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/YamasSeverance";

        private const int WindupTime = 16;
        private const int DashTime = 11;
        private const int SheathTime = 18;
        private const float DashSpeed = 24f;

        private ref float DirX => ref Projectile.ai[0];
        private ref float DirY => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];

        private Player Owner => Main.player[Projectile.owner];
        private Vector2 DashDir => new(DirX, DirY);

        /// <summary>判决/处决大招节拍剩余帧 (驱动染屏与刀痕余韵, 纯视觉)。</summary>
        private int beatTimer;
        private bool dashSoundPlayed;

        // 居合刀痕路径 (纯视觉)
        private readonly Vector2[] pathTrail = new Vector2[14];
        private int pathCount;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = WindupTime + DashTime + SheathTime + 4;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = false; // 位移斩允许穿越薄墙判定 (路径由位移本身限制)
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void OnSpawn(IEntitySource source) {
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            DirX = dir.X;
            DirY = dir.Y;
            Projectile.velocity = Vector2.Zero;
            Projectile.spriteDirection = dir.X >= 0f ? 1 : -1;

            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = -0.4f }, Owner.Center);
        }

        private int PhaseId => Timer < WindupTime ? 0 : (Timer < WindupTime + DashTime ? 1 : 2);

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                Projectile.Kill();
                return;
            }

            Owner.heldProj = Projectile.whoAmI;
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.direction = Projectile.spriteDirection;
            Projectile.Center = Owner.MountedCenter;

            int phase = PhaseId;
            float dashAngle = DashDir.ToRotation();

            if (phase == 0) {
                // —— 蓄势拔刀: 身形微沉, 刀收于身后; 收束粒子在末 4 帧静默 (爆发前的吸气) ——
                float t = Timer / WindupTime;
                Owner.velocity.X *= 0.85f;

                Projectile.rotation = dashAngle + MathHelper.Pi * 0.88f * Projectile.spriteDirection
                    + MathF.Sin(Timer * 1.9f) * 0.04f * t;

                bool silence = Timer >= WindupTime - 4;
                if (!silence && Main.rand.NextBool(2)) {
                    Vector2 from = Owner.MountedCenter + Main.rand.NextVector2CircularEdge(70f, 70f);
                    Dust d = Dust.NewDustPerfect(from, Main.rand.NextBool() ? DustID.RedTorch : DustID.Shadowflame,
                        (Owner.MountedCenter - from) * 0.11f, 110, default, Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = true;
                }

                float armRotW = Projectile.rotation - MathHelper.PiOver2;
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotW);
            }
            else if (phase == 1) {
                // —— 瞬身: 一帧起速 (set, 不是 ramp), 直线位移斩 ——
                if (!dashSoundPlayed) {
                    dashSoundPlayed = true;
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1f, Pitch = -0.35f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.6f, Pitch = 0.3f }, Owner.Center);
                    WeaponVFX.AddScreenShake(Owner.Center, 3.5f);
                }

                Owner.velocity = DashDir * DashSpeed;
                Owner.fallStart = (int)(Owner.position.Y / 16f); // 防落伤结算
                Projectile.rotation = dashAngle;

                // 位移拉丝粒子 (速度门控的速度感修饰)
                for (int i = 0; i < 2; i++) {
                    Dust d = Dust.NewDustPerfect(
                        Owner.MountedCenter + Main.rand.NextVector2Circular(20f, 20f) - DashDir * Main.rand.NextFloat(10f, 60f),
                        DustID.RedTorch, -DashDir * Main.rand.NextFloat(2f, 5f), 130, default, Main.rand.NextFloat(1.0f, 1.6f));
                    d.noGravity = true;
                }

                // 刀痕路径记录
                for (int i = pathTrail.Length - 1; i > 0; i--)
                    pathTrail[i] = pathTrail[i - 1];
                pathTrail[0] = Owner.MountedCenter + DashDir * 40f;
                pathCount = Math.Min(pathCount + 1, pathTrail.Length);

                float armRotD = Projectile.rotation - MathHelper.PiOver2;
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotD);
            }
            else {
                // —— 收刀: 硬制动 + 顺势收势 ——
                if (Timer < WindupTime + DashTime + 3)
                    Owner.velocity *= 0.55f;

                float t = MathHelper.Clamp((Timer - WindupTime - DashTime) / (float)SheathTime, 0f, 1f);
                Projectile.rotation = MathHelper.Lerp(dashAngle, dashAngle + 1.15f * Projectile.spriteDirection,
                    MathHelper.SmoothStep(0f, 1f, t));

                float armRotS = Projectile.rotation - MathHelper.PiOver2;
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter, armRotS);
            }

            if (beatTimer > 0)
                beatTimer--;

            Lighting.AddLight(Owner.MountedCenter, 0.7f, 0.25f, 0.25f);

            Timer++;
            if (Timer >= WindupTime + DashTime + SheathTime)
                Projectile.Kill();
        }

        public override bool? CanDamage() => PhaseId == 1;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 位移斩判定线: 身前 90px / 身后 30px (覆盖当帧扫过路径)
            Vector2 start = Owner.MountedCenter - DashDir * 30f;
            Vector2 end = Owner.MountedCenter + DashDir * 90f;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 34f, ref _);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            var g = target.GetGlobalNPC<RevenantKarmaGlobalNPC>();
            if (g.Karma >= 4)
                modifiers.FinalDamage *= 1.8f + 0.12f * g.Karma; // 断业判决 (随后在 OnHit 清账)
            else if (g.Karma <= 0)
                modifiers.FinalDamage *= 1.1f;
            modifiers.HitDirectionOverride = DashDir.X >= 0f ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            var g = target.GetGlobalNPC<RevenantKarmaGlobalNPC>();
            int karmaBefore = g.Karma;
            bool consumed = karmaBefore >= 4;
            bool executed = false;

            if (consumed) {
                // 消费型宣判: 清账 + 既决锁 + 判决印 (damage=0 纯视觉盖印)
                g.Karma = 0;
                g.SettleCooldown = RevenantKarma.SettleLockout;
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<KarmicVerdict>(), 0, 0f, Projectile.owner, 0f, 1.25f);
                }
            }

            // 处决: 记名 + 残血 + 非 Boss (公平化的"一击必杀")
            if (!target.boss && target.life > 0 && karmaBefore >= 1
                && target.life < target.lifeMax * 0.15f && Projectile.owner == Main.myPlayer) {
                executed = true;
                target.SimpleStrikeNPC(target.life + (int)target.defense + 60, hit.HitDirection, true, 0f, null, false, 0, true);
            }

            target.AddBuff(BuffID.ShadowFlame, 240);

            if (consumed || executed) {
                beatTimer = 26; // 驱动染屏/印记余韵
                WeaponVFX.AddScreenShake(target.Center, 8f);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = -0.2f }, target.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.45f, Pitch = 0.25f }, target.Center);
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.LethalRed, scale: executed ? 1.7f : 1.4f, owner: Projectile.owner);
            }
            else {
                WeaponVFX.AddScreenShake(target.Center, 3f);
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.AbyssPurple, scale: 1f, owner: Projectile.owner);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            int phase = PhaseId;
            float dashAngle = DashDir.ToRotation();

            // —— 居合刀痕 (位移路径, 朱红高热 + 折影; 收刀期渐隐) ——
            if (pathCount >= 2) {
                float trailFade = phase == 1 ? 1f
                    : MathHelper.Clamp(1f - (Timer - WindupTime - DashTime) / (float)SheathTime, 0f, 1f);
                if (trailFade > 0.02f) {
                    var pts = new Vector2[pathCount];
                    Array.Copy(pathTrail, pts, pathCount);
                    RevenantRibbonVFX.DrawKarmaRibbon(pts, 36f,
                        core: new Color(255, 190, 170, 235), edge: new Color(150, 25, 45, 170),
                        intensity: trailFade, heat: 0.9f, ghost: 0.45f,
                        uvScroll: Main.GlobalTimeWrappedHourly * 2f);
                }
            }

            // —— 蓄势期: 刀身后收 + 沿刃热浪 (uHeat 随蓄势) ——
            float windupHeat = phase == 0 ? Timer / WindupTime : 1f;
            Vector2 handPos = Owner.MountedCenter - Main.screenPosition;
            Vector2 origin;
            float rotOffset;
            SpriteEffects fxFlip;
            if (Projectile.spriteDirection > 0) {
                origin = new Vector2(6f, texture.Height - 6f);
                rotOffset = MathHelper.PiOver4;
                fxFlip = SpriteEffects.None;
            }
            else {
                origin = new Vector2(texture.Width - 6f, texture.Height - 6f);
                rotOffset = MathHelper.Pi * 0.75f;
                fxFlip = SpriteEffects.FlipHorizontally;
            }

            Main.EntitySpriteDraw(texture, handPos, null, lightColor,
                Projectile.rotation + rotOffset, origin, Projectile.scale * 1.25f, fxFlip, 0);

            if (phase == 0 && windupHeat > 0.2f) {
                Vector2 hilt = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * 12f;
                Vector2 tip = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * 96f;
                RevenantRibbonVFX.DrawKarmaRibbon([hilt, tip], 11f,
                    core: new Color(255, 180, 150, 220), edge: new Color(140, 30, 60, 140),
                    intensity: 0.3f + windupHeat * 0.55f, heat: windupHeat, ghost: 0f);

                // 末 4 帧静默前的最后一闪 (collapse-before-release)
                if (Timer >= WindupTime - 5 && Timer < WindupTime - 4)
                    WeaponVFX.DrawGlowBurst(Owner.MountedCenter, 2.2f, new Color(255, 120, 100) * 0.8f);
            }

            // —— 判决/处决大招节拍: 朱红染屏 (占全屏名额, 短暂定调 ≤26f) ——
            if (beatTimer > 0) {
                float env = beatTimer / 26f;
                WeaponVFX.ApplyPaletteTint(Main.spriteBatch,
                    shadowTint: new Color(120, 10, 25, 255), highlightTint: new Color(255, 130, 95, 255),
                    intensity: 0.12f * env, saturation: 1.05f, hueShift: 0f);
            }

            return false;
        }
    }

    /// <summary>
    /// 断业剑气弹幕 - 大劈释放的紫色剑气波, 向前飞行并穿透敌人 (命中 +1 业)。
    /// GlaciateWave 底 + DissolveBurn 噪声消融 + BeamGrad 弧光刃边。
    /// </summary>
    public class YamasSeveranceSlash : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/YamasSeverance";

        private int hitCount;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 45;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.alpha = 80;
        }

        public override void AI() {
            // 逐渐消退
            Projectile.alpha += 4;
            if (Projectile.alpha > 255) {
                Projectile.Kill();
                return;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            float brightness = (255 - Projectile.alpha) / 255f;
            Lighting.AddLight(Projectile.Center, 0.5f * brightness, 0.2f * brightness, 0.6f * brightness);

            if (Main.rand.NextBool(2)) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(10, 10),
                    4, 4, DustID.Shadowflame,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    120, default, Main.rand.NextFloat(1.0f, 1.5f));
                trail.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 120);

            // 剑气也记业
            RevenantKarma.AddKarma(Projectile, target, 1);

            for (int i = 0; i < 6; i++) {
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.Shadowflame,
                    Main.rand.NextVector2Circular(5f, 5f), 100, default, Main.rand.NextFloat(1.2f, 1.8f));
                burst.noGravity = true;
            }

            // 第三次贯穿命中触发"宽幅断业"演出 (穿透 4)
            hitCount++;
            if (hitCount == 3) {
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.AbyssPurple, scale: 1.6f, owner: Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 4f);
                SoundEngine.PlaySound(SoundID.Item70 with { Volume = 0.6f, Pitch = -0.2f }, target.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glaciate = ACMAsset.GlaciateWave;
            if (glaciate == null)
                return false;

            Vector2 origin = glaciate.Size() / 2f;
            float opacity = (255 - Projectile.alpha) / 255f;
            Vector2 screenCenter = Projectile.Center - Main.screenPosition;

            // 外层光晕底
            Color glowColor = new Color(140, 60, 200) * opacity * 0.4f;
            glowColor.A = 0;
            Main.EntitySpriteDraw(glaciate, screenCenter, null, glowColor, Projectile.rotation, origin, new Vector2(0.5f, 0.35f), SpriteEffects.None, 0);

            // 主体剑气: DissolveBurn 噪声消融 (随 alpha 上升而灼烧崩解)
            float threshold = MathHelper.Clamp(1f - opacity, 0f, 1f);
            WeaponVFX.ApplyDissolveBurn(glaciate, Projectile.Center, null,
                new Color(200, 120, 255) * 0.9f, Projectile.rotation, origin, 0.42f,
                threshold: threshold, intensity: opacity,
                edgeColor: new Color(235, 130, 255, 200), edgeWidth: 0.1f, noiseScale: 2.2f,
                direction: -Projectile.velocity.SafeNormalize(Vector2.UnitX), sweepStrength: 0.6f);

            // BeamGrad 扇形断业弧光边 (横切剑气, 体现刀锋利刃)
            Vector2 perp = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();
            float arcHalf = 64f;
            ACMShaders.DrawBeam(Projectile.Center - perp * arcHalf, Projectile.Center + perp * arcHalf,
                halfWidth: 10f, core: new Color(225, 160, 255), edge: new Color(120, 40, 190),
                intensity: opacity, flowSpeed: 1.8f, flowScale: 1.6f, coreSharp: 2.8f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f),
                    100, default, Main.rand.NextFloat(1.0f, 1.5f));
                death.noGravity = true;
            }
        }
    }
}
