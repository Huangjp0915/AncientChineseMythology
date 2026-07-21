using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Swords
{
    /// <summary>
    /// 玄铁剑 — "重剑无锋, 大巧不工"。
    /// 重做: 原版匀速 Swing 换皮 → 手持弹幕三段重挥 (横斩→回斩→过顶崩地斩),
    /// 每段前摇(后摆蓄势)→爆发(poly(12) 一瞬到位)→收招(缓沉回正) 的重量波形;
    /// 第三段斩底触发"崩地"地裂 (范围 ×0.8 补击 + 冲击环 + 碎石 + 屏震)。流血机制保留。
    /// </summary>
    public class XuanTieSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/XuanTieSword";

        private int comboStep;      // 0 横斩 / 1 回斩 / 2 崩地斩
        private int comboIdleTimer; // 超时回首式

        public override void SetDefaults() {
            Item.damage = 16;                     // 原 13; 段循环变慢, DPS 论证见 Docs/WeaponRedo/Swords.md §6
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6.5f;
            Item.value = Terraria.Item.buyPrice(silver: 75);
            Item.rare = ItemRarityID.White;
            Item.UseSound = null;                 // 音效由手持弹幕在爆发帧分层播放
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<XuanTieHeldSlash>();
            Item.shootSpeed = 1f;
        }

        public override bool CanUseItem(Player player) {
            // 上一挥未收招完不可再挥 (重剑节奏)
            return player.ownedProjectileCounts[ModContent.ProjectileType<XuanTieHeldSlash>()] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int step = comboStep;
            float mult = step == 2 ? 1.6f : 1f; // 崩地斩单段更重
            Projectile.NewProjectile(source, player.MountedCenter, velocity, type,
                (int)(damage * mult), knockback, player.whoAmI, step);
            comboStep = (comboStep + 1) % 3;
            comboIdleTimer = 0;
            return false;
        }

        public override void UpdateInventory(Player player) {
            comboIdleTimer = Math.Min(comboIdleTimer + 1, 90);
            if (comboIdleTimer >= 90)
                comboStep = 0;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<XuanTie.XuanTieBar>(), 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    /// <summary>
    /// 玄铁重斩 (手持弹幕) — 三段重挥波形载体。
    /// ai[0]=段序 (0 横斩 / 1 回斩 / 2 崩地斩); ai[1]=瞄准初始角; ai[2]=段内计时。
    /// 波形: 前摇 quadratic 后摆 (末段带剑身微颤) → 爆发 poly(12) ease-out → 收招 quintic 缓沉。
    /// </summary>
    public class XuanTieHeldSlash : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/XuanTieSword";
        public override LocalizedText DisplayName => Language.GetOrRegister(
            "Mods.AncientChineseMythology.Projectiles.XuanTieHeldSlash.DisplayName", () => "玄铁重斩");

        private const float SwingRange = 2.5f;   // 横斩覆角
        private const float SlamRange = 3.3f;    // 崩地斩覆角
        private const float Backswing = 0.4f;    // 前摇后摆角
        private const int TrailLen = 10;

        private Player Owner => Main.player[Projectile.owner];
        private int Step => (int)Projectile.ai[0];
        private ref float InitialAngle => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];

        private enum Stage { Prepare, Execute, Recover }
        private Stage CurrentStage {
            get => (Stage)Projectile.localAI[0];
            set { Projectile.localAI[0] = (float)value; Timer = 0; }
        }
        private ref float Progress => ref Projectile.localAI[1]; // 相对初始角的已扫角度

        private bool quakeSpawned;

        private float AtkSpeed => Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float PrepTime => (Step == 2 ? 15f : 10f) / AtkSpeed;
        private float ExecTime => 5f / AtkSpeed;
        private float RecoverTime => (Step == 2 ? 14f : 11f) / AtkSpeed;
        private float Range => Step == 2 ? SlamRange : SwingRange;
        /// <summary>回斩反向扫; 崩地斩正向。</summary>
        private float SwingSign => Step == 1 ? -1f : 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailCacheLength[Type] = TrailLen; // 只取 oldRot 容量, 记录自管 (不设 TrailingMode)
        }

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.timeLeft = 600;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
            float dir = Projectile.spriteDirection;
            if (Step == 2) {
                // 过顶崩地: 起于头顶偏后, 扫向前下方
                InitialAngle = -MathHelper.PiOver2 - 0.6f * dir;
            }
            else {
                float targetAngle = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
                // 目标角限制在面向侧 (同向量夹角保护, 参考干将实现)
                if (dir > 0)
                    targetAngle = MathHelper.Clamp(targetAngle, -MathHelper.Pi / 3f, MathHelper.Pi / 3.5f);
                else {
                    if (targetAngle < 0)
                        targetAngle += MathHelper.TwoPi;
                    targetAngle = MathHelper.Clamp(targetAngle, MathHelper.Pi * 0.71f, MathHelper.Pi * 4f / 3f);
                }
                // 横斩从后上起手, 回斩从前上反抡
                InitialAngle = targetAngle - 0.55f * Range * dir * SwingSign;
            }
        }

        public override void SendExtraAI(BinaryWriter writer) => writer.Write((sbyte)Projectile.spriteDirection);
        public override void ReceiveExtraAI(BinaryReader reader) => Projectile.spriteDirection = reader.ReadSByte();

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                Projectile.Kill();
                return;
            }

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.ChangeDir(Projectile.spriteDirection);

            switch (CurrentStage) {
                case Stage.Prepare: DoPrepare(); break;
                case Stage.Execute: DoExecute(); break;
                default: DoRecover(); break;
            }

            SetSwordPosition();

            // 尖端历史 (拖尾) — 记录旋转即可, 绘制时以当前人物中心重建
            for (int i = TrailLen - 1; i > 0; i--)
                Projectile.oldRot[i] = Projectile.oldRot[i - 1];
            Projectile.oldRot[0] = Projectile.rotation;

            Timer++;
        }

        private void DoPrepare() {
            float t = MathHelper.Clamp(Timer / PrepTime, 0f, 1f);
            // quadratic in-out 后摆
            float ease = t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;
            Progress = -Backswing * ease;
            // 末段剑身微颤 (蓄劲)
            if (t > 0.6f)
                Progress += MathF.Sin(Timer * 2.4f) * 0.02f;

            // 崩地斩蓄势: 铁屑向剑身汇聚
            if (Step == 2 && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 tip = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * 70f;
                Dust d = Dust.NewDustPerfect(tip + Main.rand.NextVector2Circular(30f, 30f), DustID.Iron);
                d.noGravity = true;
                d.velocity = (tip - d.position) * 0.12f;
                d.scale = Main.rand.NextFloat(0.7f, 1.1f);
            }

            if (Timer >= PrepTime) {
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = Step == 2 ? -0.5f : -0.3f, Volume = 1.1f }, Owner.Center);
                CurrentStage = Stage.Execute;
            }
        }

        private void DoExecute() {
            float t = MathHelper.Clamp(Timer / ExecTime, 0f, 1f);
            // poly(12) ease-out: 几乎全部角位移压进最初几帧 — 重剑的"一瞬"
            float ease = 1f - MathF.Pow(1f - t, 12f);
            Progress = MathHelper.Lerp(-Backswing, Range, ease);

            if (Step == 2 && !quakeSpawned && t >= 0.85f) {
                quakeSpawned = true;
                SpawnQuake();
            }

            if (Timer >= ExecTime)
                CurrentStage = Stage.Recover;
        }

        private void DoRecover() {
            float t = MathHelper.Clamp(Timer / RecoverTime, 0f, 1f);
            // quintic 缓沉: 轻微回弹后定住
            float ease = 1f - MathF.Pow(1f - t, 5f);
            Progress = MathHelper.Lerp(Range, Range * 0.94f, ease);
            Projectile.Opacity = 1f - t * t;

            if (Timer >= RecoverTime)
                Projectile.Kill();
        }

        /// <summary>崩地: 斩底在地面生成地裂补击 + 冲击反馈 (仅 owner 端生成, 视觉各端自绘)。</summary>
        private void SpawnQuake() {
            Vector2 strike = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * 76f * Projectile.scale;
            if (Projectile.owner == Main.myPlayer) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), strike, Vector2.Zero,
                    ModContent.ProjectileType<XuanTieQuake>(), (int)(Projectile.damage * 0.8f),
                    Projectile.knockBack, Projectile.owner);
            }
        }

        public void SetSwordPosition() {
            Projectile.rotation = InitialAngle + Projectile.spriteDirection * SwingSign * Progress;

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            Vector2 armPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            armPosition.Y += Owner.gfxOffY;
            Projectile.Center = armPosition;
            Projectile.scale = 1.5f * Owner.GetAdjustedItemScale(Owner.HeldItem);
            Owner.heldProj = Projectile.whoAmI;
        }

        public override bool? CanDamage() => CurrentStage == Stage.Execute ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 1.06f);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 16f * Projectile.scale, ref collisionPoint);
        }

        public override void CutTiles() {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 1.06f);
            Utils.PlotTileLine(start, end, 16f * Projectile.scale, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!target.friendly && !target.dontTakeDamage)
                target.AddBuff(ModContent.BuffType<Buffs.XuanTieBleed>(), 60 * 3);

            float scale = Step == 2 ? 1.3f : 1f;
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.XuanTieBleed, scale, Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, Step == 2 ? 2f : 1.5f);
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.3f + Step * 0.08f, Volume = 0.8f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 origin;
            float rotationOffset;
            SpriteEffects effects;
            if (Projectile.spriteDirection > 0) {
                origin = new Vector2(0, Projectile.height);
                rotationOffset = MathHelper.ToRadians(45f);
                effects = SpriteEffects.None;
            }
            else {
                origin = new Vector2(Projectile.width, Projectile.height);
                rotationOffset = MathHelper.ToRadians(135f);
                effects = SpriteEffects.FlipHorizontally;
            }

            // 挥砍拖尾 (仅爆发/收招显示): 玄铁暗红双层 ribbon, 沿刃尖历史
            if (CurrentStage != Stage.Prepare && Projectile.Opacity > 0.15f) {
                var pts = new List<Vector2>(TrailLen);
                float reach = 66f * Projectile.scale;
                for (int i = 0; i < TrailLen; i++) {
                    if (i > 0 && Projectile.oldRot[i] == 0f)
                        break;
                    pts.Add(Owner.MountedCenter + Projectile.oldRot[i].ToRotationVector2() * reach);
                }
                if (pts.Count >= 2) {
                    WeaponVFX.DrawRibbonTrail(pts.ToArray(), 20f,
                        new Color(90, 10, 10, (int)(150 * Projectile.Opacity)),
                        new Color(220, 70, 70, (int)(200 * Projectile.Opacity)),
                        uvScroll: -Main.GlobalTimeWrappedHourly * 1.2f);
                }
            }

            // 前摇蓄劲: 刃身暗红辉光渐强
            if (CurrentStage == Stage.Prepare) {
                float t = MathHelper.Clamp(Timer / PrepTime, 0f, 1f);
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.25f + 0.3f * t * t, new Color(190, 40, 40) * (0.35f + 0.4f * t));
            }

            Main.spriteBatch.Draw(TextureAssets.Projectile[Type].Value,
                Projectile.Center - Main.screenPosition, null,
                lightColor * Projectile.Opacity,
                Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0);
            return false;
        }
    }

    /// <summary>
    /// 玄铁·崩地 — 崩地斩落点地裂 (短命范围补击 + 冲击环/碎石/屏震表现)。
    /// 生成时向下吸附至地表; 命中同样上流血。
    /// </summary>
    public class XuanTieQuake : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override LocalizedText DisplayName => Language.GetOrRegister(
            "Mods.AncientChineseMythology.Projectiles.XuanTieQuake.DisplayName", () => "玄铁崩地");

        private const int Life = 14;

        public override void SetDefaults() {
            Projectile.width = 150;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void OnSpawn(IEntitySource source) {
            // 向下吸附地表 (最多 10 格)
            Point tile = Projectile.Center.ToTileCoordinates();
            for (int dy = 0; dy < 10; dy++) {
                int y = tile.Y + dy;
                if (y >= Main.maxTilesY - 10)
                    break;
                if (WorldGen.SolidTile(tile.X, y)) {
                    Projectile.Bottom = new Vector2(Projectile.Center.X, y * 16f);
                    break;
                }
            }

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.2f, Volume = 1.1f }, Projectile.Center);
            WeaponVFX.AddScreenShake(Projectile.Center, 3f);

            if (!Main.dedServ) {
                for (int i = 0; i < 22; i++) {
                    Vector2 vel = new(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6.5f, -1.5f));
                    Dust d = Dust.NewDustPerfect(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-70f, 70f), 0f),
                        Main.rand.NextBool(3) ? DustID.RedTorch : DustID.Stone, vel, 0, default, Main.rand.NextFloat(1f, 1.7f));
                    d.noGravity = Main.rand.NextBool(4);
                }
            }
        }

        public override void AI() {
            if (!Main.dedServ && Projectile.timeLeft > Life - 4 && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Iron);
                d.noGravity = true;
                d.velocity *= 0.5f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!target.friendly && !target.dontTakeDamage)
                target.AddBuff(ModContent.BuffType<Buffs.XuanTieBleed>(), 60 * 3);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.XuanTieBleed, 1f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 扩张地面冲击环 (双层, 玄铁暗红)
            float life = 1f - Projectile.timeLeft / (float)Life;
            float radius = 20f + life * 85f;
            float alpha = (1f - life) * 0.85f;
            WeaponVFX.DrawShockwaveRing(Projectile.Bottom - new Vector2(0f, 8f), radius, 9f, alpha,
                new Color(220, 70, 70), new Color(90, 10, 10));
            return false;
        }
    }
}
