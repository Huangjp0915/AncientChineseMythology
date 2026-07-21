using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.Revenants;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs
{
    /// <summary>
    /// 阎摩断业屠神刀 (旗舰②) - YamasSeverance的觉醒升级版
    /// 手持巨刃三连段 (竖劈→反手回撩→大回环), 每段掷出断业刃气;
    /// 低血非Boss敌人处决斩杀 (弑神裂隙 RevenantEXDeicideRift 撕屏);
    /// 觉醒首刀化为"阎罗一刀": 30f 举刀蓄力 → 一帧斩落 → 全屏横贯刃气 + 全屏裂隙。
    /// </summary>
    public class YamasDeicide : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 2600;
            Item.crit = 25;
            Item.DamageType = DamageClass.Melee;
            Item.width = 80;
            Item.height = 80;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 14f;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = null; // 音效由手持弹幕在爆发帧分层播放
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<YamasDeicideHeld>();
            Item.shootSpeed = 1f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<YamasDeicideHeld>()] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            var mp = player.GetModPlayer<RevenantEXKarmaPlayer>();

            int stage;
            if (mp.Awakened && mp.YamaNukeReady) {
                // 觉醒首刀: 阎罗一刀
                stage = 3;
                mp.YamaNukeReady = false;
                mp.DeicideCombo = 0;
            }
            else {
                stage = mp.DeicideCombo;
                mp.DeicideCombo = (mp.DeicideCombo + 1) % 3;
                mp.DeicideComboTimer = 80; // 80f 内接续连段, 否则回到第一段
            }

            float aim = velocity.SafeNormalize(Vector2.UnitX).ToRotation();
            Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero,
                type, damage, knockback, player.whoAmI, stage, aim);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<YamasSeverance>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 屠神刀手持巨刃弹幕: anticipation(40%, 二次入出反向蓄力) → strike(16%, poly14 急扫) → recovery 波形;
    /// stage: 0=竖劈 1=反手回撩 2=大回环(1.5×, 全周扫) 3=阎罗一刀(30f 蓄力 + 2× 一帧斩 + 全屏裂隙)。
    /// 伤害窗口严格对齐爆发帧 (CanDamage 仅 strike 段)。
    /// </summary>
    public class YamasDeicideHeld : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/YamasDeicide";

        private ref float Stage => ref Projectile.ai[0];
        // ai[1] = 挥舞基准角 (spawn 时同步, 多人下他端可正确重演挥舞)
        private ref float Timer => ref Projectile.localAI[1];
        private ref float Struck => ref Projectile.localAI[0];   // 是否已过爆发帧
        private Player Owner => Main.player[Projectile.owner];

        private const float ActAnticipation = 0.40f;
        private const float ActStrike = 0.16f;
        private const int ChargeTime = 30;    // 阎罗一刀蓄力帧

        // 刀尖轨迹环形缓冲 (仅本地绘制用)
        private readonly Vector2[] _tipTrail = new Vector2[12];
        private int _tipCount;
        private float _prevRot; // 上一帧刀角 (扫掠采样防隧穿)

        private float MeleeSpeed => MathF.Max(0.4f, Owner.GetTotalAttackSpeed(DamageClass.Melee));
        private int SwingTime => (int)MathF.Max(14f, (Stage == 2f ? 30f : Stage == 3f ? 20f : 25f) / MeleeSpeed);
        private float BladeLength => Stage >= 2f ? 185f : 165f;

        private float StartSweep => Stage switch {
            1f => 1.9f,
            2f => -2.9f,
            3f => -2.8f,
            _ => -2.1f,
        };
        private float EndSweep => Stage switch {
            1f => -1.9f,
            2f => 3.4f,
            3f => 1.4f,
            _ => 1.7f,
        };

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.YamasDeicideHeld.DisplayName",
                () => "Yama's Deicide");
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 一段挥舞每目标只结算一次
        }

        private float BaseAngle => Projectile.ai[1];
        private int Dir => MathF.Cos(BaseAngle) >= 0f ? 1 : -1;

        /// <summary>挥舞进度曲线: 反向蓄力 → poly(14) 急速爆发 → 轻微回拖收招。</summary>
        private static float SwingCurve(float p) {
            if (p < ActAnticipation) {
                float t = p / ActAnticipation;
                float e = t * t * (3f - 2f * t);
                return -0.14f * e; // 越过起点的反向蓄力
            }
            if (p < ActAnticipation + ActStrike) {
                float t = (p - ActAnticipation) / ActStrike;
                float e = 1f - MathF.Pow(1f - t, 14f);
                return -0.14f + 1.14f * e;
            }
            float r = (p - ActAnticipation - ActStrike) / (1f - ActAnticipation - ActStrike);
            return 1f - r * r * 0.06f;
        }

        private bool InCharge => Stage == 3f && Timer < ChargeTime;
        private float SwingProgress => MathHelper.Clamp((Timer - (Stage == 3f ? ChargeTime : 0f)) / SwingTime, 0f, 1f);

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) { Projectile.Kill(); return; }
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;

            float bladeRot;
            if (InCharge) {
                // —— 阎罗一刀蓄力: 举刀过头, 业焰汇聚, 尾段 22% 静默 (爆前收声) ——
                float charge = Timer / ChargeTime;
                float shiver = MathF.Sin(Timer * 2.4f) * 0.03f * charge;
                bladeRot = -MathHelper.PiOver2 + shiver * Dir;
                if (Projectile.owner == Main.myPlayer)
                    Owner.velocity.X *= 0.90f; // 蓄力缓行 (公平阀: 大招有代价)

                Vector2 tip = Owner.MountedCenter + bladeRot.ToRotationVector2() * BladeLength;
                if (charge < 0.78f && Main.rand.NextFloat() < MathF.Sqrt(charge)) {
                    // 汇聚流光: 远处业焰被吸向刀尖
                    Vector2 pos = tip + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(90f, 260f);
                    Dust pull = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.Torch : DustID.PurpleTorch,
                        (tip - pos) * 0.085f, 100, default, Main.rand.NextFloat(1.3f, 2.1f));
                    pull.noGravity = true;
                }
                // 蓄力震感 charge² 递增
                if ((int)Timer % 6 == 0)
                    WeaponVFX.AddScreenShake(Owner.Center, charge * charge * 2f);
                if ((int)Timer == ChargeTime / 2)
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.7f, Pitch = -0.5f }, Owner.Center);
            }
            else {
                float p = SwingProgress;
                bladeRot = BaseAngle + Dir * MathHelper.Lerp(StartSweep, EndSweep, SwingCurve(p));

                // —— 爆发帧: 一次性事件 (音效分层 / 刃气 / 大招裂隙) ——
                if (Struck == 0f && p >= ActAnticipation) {
                    Struck = 1f;
                    OnStrikeFrame(bladeRot);
                }
                if (p >= 1f) { Projectile.Kill(); return; }
            }

            _prevRot = Timer <= 0f ? bladeRot : Projectile.rotation;
            Projectile.rotation = bladeRot;
            Vector2 dirVec = bladeRot.ToRotationVector2();
            Projectile.Center = Owner.MountedCenter + dirVec * 24f;

            // 刀尖轨迹缓冲
            _tipTrail[_tipCount++ % _tipTrail.Length] = Owner.MountedCenter + dirVec * BladeLength;

            Owner.ChangeDir(Dir);
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, bladeRot - MathHelper.PiOver2);
            Timer++;
            Lighting.AddLight(Projectile.Center, 1f, 0.5f, 1.2f);

            // 挥舞冥火 (只在爆发/收招段, 克制)
            if (!InCharge && SwingProgress > ActAnticipation && Main.rand.NextBool(2)) {
                Vector2 tip = Owner.MountedCenter + dirVec * Main.rand.NextFloat(70f, BladeLength);
                Dust flame = Dust.NewDustPerfect(tip, DustID.Shadowflame,
                    dirVec.RotatedBy(MathHelper.PiOver2 * Dir) * 3f, 90, default, Main.rand.NextFloat(1.4f, 2.2f));
                flame.noGravity = true;
            }
        }

        /// <summary>爆发帧事件: 音效分层 + 刃气/大招投射物 + 震屏 (一帧内完成, 冲击链条 §5)。</summary>
        private void OnStrikeFrame(float bladeRot) {
            Vector2 dirVec = BaseAngle.ToRotationVector2(); // 刃气沿瞄准方向
            bool isNuke = Stage == 3f;
            bool isSpin = Stage == 2f;

            // 音效: 低频重击 + 高频破空, 音高随段位递升
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1f, Pitch = -0.25f + Stage * 0.1f + Main.rand.NextFloat(-0.05f, 0.05f) }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = 0.25f + Main.rand.NextFloat(-0.1f, 0.1f) }, Owner.Center);

            if (Projectile.owner != Main.myPlayer)
                return;

            if (isNuke) {
                // —— 阎罗一刀: 全屏横贯刃气 (3×) + 全屏弑神裂隙 + 重震 ——
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.3f }, Owner.Center);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.MountedCenter + dirVec * 60f,
                    dirVec * 24f, ModContent.ProjectileType<YamaGreatSlashWave>(),
                    (int)(Projectile.damage * 3f), Projectile.knockBack * 1.5f, Projectile.owner);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.MountedCenter + dirVec * 340f,
                    Vector2.Zero, ModContent.ProjectileType<DeicideRiftFX>(), 0, 0f, Projectile.owner,
                    dirVec.ToRotation(), 620f, 1f);
                WeaponVFX.AddScreenShake(Owner.Center, 11f);
            }
            else {
                // 每段掷 1 道断业刃气 (0.65×; 大回环 1.3 倍宽幅)
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.MountedCenter + dirVec * 50f,
                    dirVec * 19f, ModContent.ProjectileType<YamasDeicideSlash>(),
                    (int)(Projectile.damage * 0.65f), Projectile.knockBack * 0.5f, Projectile.owner,
                    isSpin ? 1.3f : 1f);
                WeaponVFX.AddScreenShake(Owner.Center, isSpin ? 4f : 2.5f);
            }
        }

        public override bool? CanDamage() {
            if (InCharge)
                return false;
            float p = SwingProgress;
            return p >= ActAnticipation && p <= ActAnticipation + ActStrike + 0.06f;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (Stage == 2f)
                modifiers.FinalDamage *= 1.5f;   // 大回环
            else if (Stage == 3f)
                modifiers.FinalDamage *= 2f;     // 阎罗一刀本体
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 高次缓动一帧掠过大弧 → 在上一帧角与当前角之间取 4 个采样线段, 防隧穿
            for (int s = 0; s <= 3; s++) {
                float ang = MathHelper.Lerp(_prevRot, Projectile.rotation, s / 3f);
                Vector2 dir = ang.ToRotationVector2();
                Vector2 start = Owner.MountedCenter + dir * 26f;
                Vector2 end = Owner.MountedCenter + dir * BladeLength;
                float collisionPoint = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 44f, ref collisionPoint))
                    return true;
            }
            return false;
        }

        public override void CutTiles() {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * BladeLength;
            Utils.PlotTileLine(start, end, 44f, DelegateMethods.CutTiles);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer)
                Owner.GetModPlayer<RevenantEXKarmaPlayer>().AddKarma(4f);

            target.AddBuff(BuffID.ShadowFlame, 600);
            target.AddBuff(BuffID.OnFire3, 600);
            target.AddBuff(BuffID.Ichor, 600);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: hit.Crit ? 1.5f : 1f + Stage * 0.12f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, hit.Crit ? 4f : 2.5f);
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.8f, Pitch = -0.3f + Main.rand.NextFloat(0.15f) }, target.Center);

            // —— 处决: 非Boss敌人血量低于15%直接斩杀 (弑神裂隙撕屏) ——
            if (!target.boss && target.life > 0 && target.life < target.lifeMax * 0.15f) {
                target.SimpleStrikeNPC(target.life + 10, hit.HitDirection, true, 0f, null, false, 0, true);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = -0.5f }, target.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.7f, Pitch = 0.2f }, target.Center);
                if (Projectile.owner == Main.myPlayer) {
                    YamaExecuteFinisher.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                        target.width + target.height, Projectile.owner);
                    ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                        ACMWeaponBurst.LethalRed, scale: 1.7f, owner: Projectile.owner);
                    // 短促弑神裂隙 (沿刀锋方向)
                    Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<DeicideRiftFX>(), 0, 0f, Projectile.owner,
                        Projectile.rotation, 200f, 0f);
                }
                WeaponVFX.AddScreenShake(target.Center, 8f);
            }

            // 神魔审判: 暴击溅射 (400px, /2)
            if (hit.Crit) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.5f }, target.Center);
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC nearby = Main.npc[i];
                    if (!nearby.CanBeChasedBy() || nearby.whoAmI == target.whoAmI)
                        continue;
                    if (Vector2.Distance(target.Center, nearby.Center) < 400f) {
                        nearby.SimpleStrikeNPC(damageDone / 2, hit.HitDirection, false, 0f, null, false, 0, true);
                        nearby.AddBuff(BuffID.ShadowFlame, 300);
                    }
                }
            }

            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(9f, 9f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, vel, 80, default, Main.rand.NextFloat(1.8f, 3f));
                burst.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 handPos = Owner.MountedCenter - Main.screenPosition;

            // 刀柄原点 + 对角贴图旋转补正 (贴图刃口朝右上)
            SpriteEffects effects = Dir == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float rotOffset = Dir == 1 ? MathHelper.PiOver4 : MathHelper.Pi * 0.75f;
            Vector2 origin = Dir == 1 ? new Vector2(8f, texture.Height - 8f) : new Vector2(texture.Width - 8f, texture.Height - 8f);
            float scale = BladeLength / (texture.Width * 1.30f) * 1.6f;

            float p = SwingProgress;
            bool striking = !InCharge && p >= ActAnticipation && p < ActAnticipation + ActStrike + 0.10f;

            // —— 爆发段残影 (仅 strike 帧开启, 速度门控的装饰) ——
            if (striking) {
                for (int g = 1; g <= 3; g++) {
                    float ghostRot = Projectile.rotation - Dir * g * 0.16f;
                    Color ghost = new Color(160, 70, 220) * (0.32f - g * 0.09f);
                    ghost.A = 0;
                    Main.EntitySpriteDraw(texture, handPos, null, ghost, ghostRot + rotOffset, origin, scale, effects, 0);
                }
                // 刀锋光刃 (BeamGrad 沿刃)
                Vector2 dirVec = Projectile.rotation.ToRotationVector2();
                ACMShaders.DrawBeam(Owner.MountedCenter + dirVec * 30f, Owner.MountedCenter + dirVec * BladeLength,
                    12f, new Color(240, 190, 255), new Color(120, 40, 200), 0.9f,
                    flowSpeed: 2.4f, flowScale: 2f, coreSharp: 2.6f);
            }

            // —— 刀尖轨迹 ribbon (strike + 收招前半段) ——
            if (!InCharge && p > ActAnticipation && _tipCount >= 3) {
                int n = Math.Min(_tipCount, _tipTrail.Length);
                var pts = new Vector2[n];
                for (int i = 0; i < n; i++)
                    pts[i] = _tipTrail[(_tipCount - 1 - i + _tipTrail.Length * 2) % _tipTrail.Length];
                float trailFade = MathHelper.Clamp(1f - (p - ActAnticipation) / 0.5f, 0f, 1f);
                if (trailFade > 0.05f)
                    WeaponVFX.DrawRibbonTrail(pts, 20f,
                        new Color(120, 40, 200) * trailFade, new Color(240, 200, 255) * trailFade,
                        uvScroll: Timer * 0.05f);
            }

            // —— 蓄力期演出: 刀尖聚焰 + 预坍缩 (爆前变小) ——
            if (InCharge) {
                float charge = Timer / (float)ChargeTime;
                Vector2 tip = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * BladeLength - Main.screenPosition;
                float preCollapse = charge > 0.8f ? MathHelper.Lerp(1f, 0.45f, (charge - 0.8f) / 0.2f) : 1f;
                float glowScale = MathHelper.Lerp(0.3f, 2.2f, charge * charge * charge) * preCollapse;
                WeaponVFX.DrawGlowBurst(tip + Main.screenPosition, glowScale, new Color(255, 180, 90) * (0.5f + charge * 0.4f));
                Texture2D lightShot = ACMAsset.LightShot;
                if (lightShot != null) {
                    Color tipCol = new Color(255, 200, 120) * charge;
                    tipCol.A = 0;
                    Main.EntitySpriteDraw(lightShot, tip, null, tipCol, Projectile.rotation, lightShot.Size() / 2f,
                        0.9f * preCollapse, SpriteEffects.None, 0);
                }
            }

            Color mainColor = Color.Lerp(lightColor, new Color(235, 215, 255), striking ? 0.55f : 0.25f);
            Main.EntitySpriteDraw(texture, handPos, null, mainColor, Projectile.rotation + rotOffset, origin, scale, effects, 0);

            // 觉醒期刀身业焰描边
            if (Owner.HasBuff<KarmaAwakenBuff>()) {
                Color awaken = new Color(255, 160, 80) * 0.45f;
                awaken.A = 0;
                Main.EntitySpriteDraw(texture, handPos, null, awaken, Projectile.rotation + rotOffset, origin, scale * 1.06f, effects, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 断业刃气弹幕 (ai[0]=宽幅倍率): 挥舞爆发帧掷出的弧形斩波, BeamGrad 主刃 + 双层斩迹。
    /// </summary>
    public class YamasDeicideSlash : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/YamasDeicide";

        private float WidthMul => Projectile.ai[0] <= 0f ? 1f : Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 16;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 8;
            Projectile.timeLeft = 60;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.alpha = 40;
        }

        public override void AI() {
            Projectile.alpha += 4;
            if (Projectile.alpha > 255) {
                Projectile.Kill();
                return;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            float brightness = (255 - Projectile.alpha) / 255f;
            Lighting.AddLight(Projectile.Center, 1f * brightness, 0.4f * brightness, 1.2f * brightness);

            for (int i = 0; i < 2; i++) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(15, 15),
                    4, 4, DustID.Shadowflame,
                    -Projectile.velocity.X * 0.3f, -Projectile.velocity.Y * 0.3f,
                    100, default, Main.rand.NextFloat(1.5f, 2.5f));
                trail.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer)
                Main.player[Projectile.owner].GetModPlayer<RevenantEXKarmaPlayer>().AddKarma(1.5f);
            target.AddBuff(BuffID.ShadowFlame, 300);
            target.AddBuff(BuffID.OnFire3, 300);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, vel, 80, default, Main.rand.NextFloat(1.8f, 2.8f));
                burst.noGravity = true;
            }
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: 1f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            float opacity = (255 - Projectile.alpha) / 255f;

            // 屠神斩 BeamGrad 主刃 (垂直于飞行方向的弧刃, 宽幅随 ai[0])
            Vector2 perp = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();
            float half = 78f * WidthMul;
            ACMShaders.DrawBeam(Projectile.Center - perp * half, Projectile.Center + perp * half, 22f * opacity * WidthMul,
                new Color(235, 180, 255), new Color(120, 40, 200), opacity,
                flowSpeed: 2.0f, flowScale: 2.2f, coreSharp: 2.4f);

            WeaponVFX.DrawProjectileTrail(Projectile, 22f * WidthMul,
                new Color(120, 40, 200, 150), new Color(235, 190, 255, 200),
                uvScroll: -(float)Main.timeForVisualEffects * 0.03f);

            Texture2D glaciate = ACMAsset.GlaciateWave;
            if (glaciate != null) {
                Vector2 origin = glaciate.Size() / 2f;
                Color mainColor = new Color(255, 150, 255) * opacity * 0.9f;
                mainColor.A = 0;
                Main.EntitySpriteDraw(glaciate, Projectile.Center - Main.screenPosition, null, mainColor,
                    Projectile.rotation, origin, new Vector2(0.7f, 0.4f * WidthMul), SpriteEffects.None, 0);
                Color glowColor = new Color(200, 60, 255) * opacity * 0.5f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(glaciate, Projectile.Center - Main.screenPosition, null, glowColor,
                    Projectile.rotation, origin, new Vector2(0.85f, 0.55f * WidthMul), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 14; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame,
                    Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f),
                    80, default, Main.rand.NextFloat(1.5f, 2.5f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 阎罗一刀·全屏横贯刃气 (3×): 巨型弧刃直线推进, 命中致命红爆。
    /// </summary>
    public class YamaGreatSlashWave : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private ref float Timer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 18;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.YamaGreatSlashWave.DisplayName",
                () => "Yama's Single Stroke");
        }

        public override void SetDefaults() {
            Projectile.width = 150;
            Projectile.height = 380; // 与视觉刃高对齐 (公平: 判定≈视觉)
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 46;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 每目标一次
        }

        public override void AI() {
            Timer++;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 1.6f, 0.5f, 0.6f);
            // 尾段淡出
            if (Projectile.timeLeft < 10)
                Projectile.alpha += 26;

            if (Main.rand.NextBool(2)) {
                Vector2 perp = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();
                Vector2 pos = Projectile.Center + perp * Main.rand.NextFloat(-120f, 120f);
                Dust flame = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.Torch : DustID.Shadowflame,
                    -Projectile.velocity * 0.15f, 90, default, Main.rand.NextFloat(1.6f, 2.6f));
                flame.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 阎罗一刀本身是觉醒产物, 不再回充业障
            target.AddBuff(BuffID.ShadowFlame, 600);
            target.AddBuff(BuffID.OnFire3, 600);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.LethalRed, scale: 1.4f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 4f);
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.9f, Pitch = -0.4f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            float opacity = (255 - Projectile.alpha) / 255f;
            Vector2 perp = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();

            // 巨型竖贯刃 (双层 BeamGrad: 白炽芯 + 致命红边)
            float half = 200f * MathHelper.Clamp(Timer / 6f, 0.3f, 1f);
            ACMShaders.DrawBeam(Projectile.Center - perp * half, Projectile.Center + perp * half, 30f * opacity,
                new Color(255, 240, 230), new Color(250, 60, 70), opacity,
                flowSpeed: 3f, flowScale: 2.4f, coreSharp: 3f);
            ACMShaders.DrawBeam(Projectile.Center - perp * half * 0.8f, Projectile.Center + perp * half * 0.8f, 52f * opacity,
                new Color(250, 90, 90), new Color(90, 10, 30), opacity * 0.55f,
                flowSpeed: 2f, flowScale: 2f, coreSharp: 1.8f);

            WeaponVFX.DrawProjectileTrail(Projectile, 40f,
                new Color(150, 12, 18), new Color(255, 190, 170), uvScroll: Timer * 0.06f);

            Texture2D glaciate = ACMAsset.GlaciateWave;
            if (glaciate != null) {
                Vector2 origin = glaciate.Size() / 2f;
                Color c = new Color(255, 120, 120) * opacity * 0.85f;
                c.A = 0;
                Main.EntitySpriteDraw(glaciate, Projectile.Center - Main.screenPosition, null, c,
                    Projectile.rotation, origin, new Vector2(1f, 1.5f), SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 弑神裂隙演出弹幕 (纯视觉, damage=0): 用专属全屏着色器 RevenantEXDeicideRift 沿斩击线撕开屏幕。
    /// ai[0]=裂隙方向(rad), ai[1]=半长(px), ai[2]=0 短促处决 24f / 1 全屏大招 42f。
    /// 全屏后处理走单一名额契约; 名额被占时退化为 BeamGrad 血线, 保证总有反馈。
    /// </summary>
    public class DeicideRiftFX : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private float Angle => Projectile.ai[0];
        private float HalfLen => Projectile.ai[1] <= 0f ? 200f : Projectile.ai[1];
        private bool Long => Projectile.ai[2] >= 1f;
        private int Life => Long ? 42 : 24;
        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.DeicideRiftFX.DisplayName",
                () => "Deicide Rift");
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Timer++;
            if (Timer >= Life)
                Projectile.Kill();
        }

        private static Vector2 ScreenUV(Vector2 world) {
            Vector2 offset = world - Main.screenPosition;
            Vector2 half = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
            float zoom = Main.GameViewMatrix.Zoom.X;
            Vector2 screenPos = (offset - half) * zoom + half;
            return screenPos / new Vector2(Main.screenWidth, Main.screenHeight);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float progress = MathHelper.Clamp(Timer / Life, 0f, 1f);
            // 开裂包络: 快开 → 保持 → 合拢
            float open = MathF.Pow(MathF.Sin(progress * MathHelper.Pi), 0.75f);
            if (open <= 0.02f)
                return false;

            Vector2 dir = Angle.ToRotationVector2();
            Vector2 a = Projectile.Center - dir * HalfLen;
            Vector2 b = Projectile.Center + dir * HalfLen;

            // —— 专属全屏裂隙 (占单一全屏名额) ——
            Effect rift = WeaponVFX.GetEffect("RevenantEXDeicideRift");
            bool applied = false;
            if (rift != null && ACMShaders.RequestFullscreenSlot()) {
                rift.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                rift.Parameters["uCenter"]?.SetValue(ScreenUV(a));
                rift.Parameters["uPointB"]?.SetValue(ScreenUV(b));
                rift.Parameters["uIntensity"]?.SetValue(open);
                rift.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
                rift.Parameters["uWidth"]?.SetValue((Long ? 0.052f : 0.030f) * open);
                rift.Parameters["uTint"]?.SetValue(new Vector4(0.62f, 0.06f, 0.10f, 0.9f));
                rift.Parameters["uGlow"]?.SetValue(new Vector4(1f, 0.35f, 0.30f, 0.85f));
                ACMShaders.ApplyScreenPostProcess(Main.spriteBatch, rift, bindNoise: true);
                applied = true;
            }

            // 退化路径: 名额被占也保证有一条血线反馈
            if (!applied)
                ACMShaders.DrawBeam(a, b, 10f * open, new Color(255, 230, 220), new Color(200, 20, 40), open,
                    flowSpeed: 3f, flowScale: 2.6f, coreSharp: 3f);

            // 裂缘辉光线 + 沿线火星 (前 40%)
            if (progress < 0.4f && Main.rand.NextBool(2)) {
                Vector2 pos = Vector2.Lerp(a, b, Main.rand.NextFloat());
                Dust spark = Dust.NewDustPerfect(pos, DustID.Torch,
                    dir.RotatedBy(MathHelper.PiOver2 * (Main.rand.NextBool() ? 1 : -1)) * Main.rand.NextFloat(2f, 6f),
                    60, default, Main.rand.NextFloat(1.4f, 2.2f));
                spark.noGravity = true;
            }
            return false;
        }
    }

    /// <summary>
    /// 屠神处决演出弹幕 (纯视觉, damage=0): 斩杀低血敌人瞬间在其位置展开 DissolveBurn 溶解崩解斩痕
    /// (致命纯红灼边) + 冲击环 + 径向辉光。绘制只在 PreDraw, 命中阶段仅 <see cref="Spawn"/> 触发。
    /// </summary>
    public class YamaExecuteFinisher : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 30;
        private float Size => Projectile.ai[0] <= 0f ? 80f : Projectile.ai[0];

        public static void Spawn(IEntitySource source, Vector2 worldPos, float size, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<YamaExecuteFinisher>(), 0, 0f, owner, size);
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;
        public override void AI() => Projectile.velocity = Vector2.Zero;

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float life = 1f - Projectile.timeLeft / (float)Life;
            float fade = MathHelper.Clamp(1f - life, 0f, 1f);

            Texture2D slash = ACMAsset.SlashBurst;
            if (slash != null && slash.Width > 0) {
                Vector2 origin = slash.Size() * 0.5f;
                float scale = (Size / slash.Width) * MathHelper.Lerp(1.6f, 2.8f, life);
                WeaponVFX.ApplyDissolveBurn(slash, Projectile.Center, null,
                    new Color(250, 40, 56), 0f, origin, scale,
                    threshold: life, intensity: fade,
                    edgeColor: new Color(255, 140, 90, 255), edgeWidth: 0.11f, noiseScale: 2.6f);
            }

            WeaponVFX.DrawShockwaveRing(Projectile.Center, 16f + life * 120f, 12f, fade * 0.85f,
                new Color(255, 110, 110), new Color(150, 12, 18));
            if (fade > 0.4f)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.08f, fade * 0.6f, new Color(250, 60, 70), 8f);

            return false;
        }
    }
}
