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
    // ============================================================
    //  亡魂EX系列共享: 业障溢出系统
    //  攻击积累业障 → 满溢自动触发"业劫觉醒"8 秒 (各武器觉醒形态不同)。
    //  全部状态只在 owner 客户端演算 (命中钩子天然 owner 侧), 觉醒本体用
    //  Buff 承载 (AddBuff 自带同步), 演出用同步弹幕 KarmaAwakenFX 承载。
    // ============================================================

    /// <summary>亡魂EX系列共享状态: 业障值 / 各武器资源 (魂膛、业刃印、连段计数、引魂印)。</summary>
    public class RevenantEXKarmaPlayer : ModPlayer
    {
        public const float KarmaMax = 100f;

        /// <summary>业障 0~100, 满溢清零并触发觉醒。</summary>
        public float Karma;
        /// <summary>引魂弓·引魂印 (按 NPC whoAmI 记层, 6 层降黄泉引渡柱)。</summary>
        public byte[] SoulMarks = new byte[Main.maxNPCs];
        /// <summary>噬魂炮·魂膛 0~6 (右键聚束炮消耗)。</summary>
        public int SoulChamber;
        /// <summary>噬魂炮·吸魂命中计数 (满额转化 1 格魂膛)。</summary>
        public int SoulHitCounter;
        /// <summary>无间业刃·业刃印 0~3 (接刃获得, 3 印掷出无间劫刃)。</summary>
        public int KarmaEdge;
        /// <summary>勾魂枪·连段位 0~2 (第 3 刺为勾魂大刺)。</summary>
        public int HookCombo;
        public int HookComboTimer;
        /// <summary>屠神刀·连段位 0~2 (第 3 段为大回环)。</summary>
        public int DeicideCombo;
        public int DeicideComboTimer;
        /// <summary>觉醒时充能的"阎罗一刀"待发标记 (屠神刀下一次挥舞消耗)。</summary>
        public bool YamaNukeReady;

        public bool Awakened => Player.HasBuff<KarmaAwakenBuff>();

        /// <summary>
        /// 积累业障 (仅 owner 客户端生效; 觉醒期间不积累)。满溢 → 触发业劫觉醒。
        /// </summary>
        public void AddKarma(float amount) {
            if (Player.whoAmI != Main.myPlayer || amount <= 0f || Awakened)
                return;

            float before = Karma;
            Karma += amount;

            // 25/50/75 阈值轻提示 (音高递升的低语)
            for (int q = 1; q <= 3; q++) {
                float th = KarmaMax * q * 0.25f;
                if (before < th && Karma >= th) {
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.35f, Pitch = -0.4f + q * 0.2f }, Player.Center);
                    break;
                }
            }

            if (Karma < KarmaMax)
                return;

            // —— 业劫觉醒 ——
            Karma = 0f;
            Player.AddBuff(ModContent.BuffType<KarmaAwakenBuff>(), KarmaAwakenBuff.Duration);
            YamaNukeReady = true;
            Projectile.NewProjectile(Player.GetSource_Misc("RevenantEXAwaken"), Player.Center, Vector2.Zero,
                ModContent.ProjectileType<KarmaAwakenFX>(), 0, 0f, Player.whoAmI);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.35f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Item119 with { Volume = 0.6f, Pitch = 0.15f }, Player.Center);
            WeaponVFX.AddScreenShake(Player.Center, 7f);
        }

        public override void PostUpdate() {
            if (HookComboTimer > 0 && --HookComboTimer == 0)
                HookCombo = 0;
            if (DeicideComboTimer > 0 && --DeicideComboTimer == 0)
                DeicideCombo = 0;
            if (!Awakened)
                YamaNukeReady = false;
            // 引魂印槽位卫生: NPC 槽被释放时清印 (防 whoAmI 复用继承旧层数)
            if (Main.GameUpdateCount % 60 == 0) {
                for (int i = 0; i < Main.maxNPCs; i++)
                    if (!Main.npc[i].active && SoulMarks[i] != 0)
                        SoulMarks[i] = 0;
            }
        }
    }

    /// <summary>业劫觉醒 Buff (8 秒): 系列武器进入各自觉醒形态。图标复用原版怒火 (Wrath)。</summary>
    public class KarmaAwakenBuff : ModBuff
    {
        public const int Duration = 8 * 60;
        public override string Texture => "Terraria/Images/Buff_117";

        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Language.GetOrRegister("Mods.AncientChineseMythology.Buffs.KarmaAwakenBuff.DisplayName",
                () => "Karmic Awakening");
            Language.GetOrRegister("Mods.AncientChineseMythology.Buffs.KarmaAwakenBuff.Description",
                () => "Karma overflows - Revenant EX weapons reveal their true forms");
        }
    }

    /// <summary>
    /// 业劫觉醒演出弹幕 (纯视觉, damage=0, 随玩家): 触发瞬间短促染屏 + 三重冲击环,
    /// 觉醒期玩家身周持续旋转 RevenantEXKarmaWheel 业焰轮 (屏幕空间 decal, 不占全屏名额)。
    /// </summary>
    public class KarmaAwakenFX : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private ref float Timer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.KarmaAwakenFX.DisplayName",
                () => "Karmic Awakening");
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = KarmaAwakenBuff.Duration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Timer++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead || !owner.HasBuff<KarmaAwakenBuff>()) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = owner.MountedCenter;
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, 0.9f, 0.55f, 1.1f);

            // 身周业焰余烬 (克制: 每帧至多 2)
            if (Main.rand.NextBool(2)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = owner.MountedCenter + ang.ToRotationVector2() * Main.rand.NextFloat(60f, 95f);
                Dust ember = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.PurpleTorch : DustID.Torch,
                    (owner.MountedCenter - pos).SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * 2f,
                    120, default, Main.rand.NextFloat(1f, 1.6f));
                ember.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float total = KarmaAwakenBuff.Duration;
            float life = Timer / total;                                    // 0→1
            // 包络: 快起 (10f) → 稳定 → 尾段 40f 淡出
            float env = MathHelper.Clamp(Timer / 10f, 0f, 1f) * MathHelper.Clamp(Projectile.timeLeft / 40f, 0f, 1f);

            // —— 触发瞬间: 短促染屏定调 (≤0.15, 走名额契约, 仅前 22f) ——
            if (Timer < 22f) {
                float flash = 1f - Timer / 22f;
                WeaponVFX.ApplyPaletteTint(Main.spriteBatch,
                    new Color(70, 20, 110), new Color(255, 190, 110), flash * 0.15f, saturation: 1.1f);
                // 三重扩张冲击环
                for (int r = 0; r < 3; r++) {
                    float phase = MathHelper.Clamp((Timer - r * 4f) / 18f, 0f, 1f);
                    if (phase <= 0f)
                        continue;
                    WeaponVFX.DrawShockwaveRing(Projectile.Center, 20f + phase * 260f, 14f, (1f - phase) * 0.85f,
                        new Color(255, 200, 120), new Color(110, 40, 190));
                }
            }

            // —— 觉醒期业焰轮 (专属着色器, 屏幕空间 decal, 不占全屏名额) ——
            Effect wheel = WeaponVFX.GetEffect("RevenantEXKarmaWheel");
            if (wheel != null && env > 0.02f) {
                ACMShaders.WorldDecalParams(Projectile.Center, 112f, out Vector2 uv, out float rFrac, out float aspect);
                wheel.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                wheel.Parameters["uCenter"]?.SetValue(uv);
                wheel.Parameters["uRadius"]?.SetValue(rFrac);
                wheel.Parameters["uIntensity"]?.SetValue(env * 0.5f);
                wheel.Parameters["uAspect"]?.SetValue(aspect);
                wheel.Parameters["uColorPrimary"]?.SetValue(new Color(255, 195, 100).ToVector4());
                wheel.Parameters["uColorSecondary"]?.SetValue(new Color(120, 50, 200).ToVector4());
                wheel.Parameters["uSpin"]?.SetValue(-Timer * 0.045f);
                wheel.Parameters["uSpokes"]?.SetValue(8f);

                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                ACMShaders.DrawScreenSpaceDecalStandalone(wheel, BlendState.Additive);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            // 中心柔光呼吸
            float pulse = 0.9f + MathF.Sin(life * 40f) * 0.1f;
            WeaponVFX.DrawGlowBurst(Projectile.Center, 1.1f * pulse * env, new Color(200, 130, 255) * 0.35f);
            return false;
        }
    }

    // ============================================================
    //  孽镜无间回旋刃 (旗舰①)
    // ============================================================

    /// <summary>
    /// 孽镜无间回旋刃 - KarmasMirrorBlade的觉醒升级版
    /// 单把大业刃投掷, 命中后在敌群间"无间弹射"(至多 8 次), 每次弹射业焰增幅;
    /// 弹尽飞回, 徒手接刃获 1 层业刃印 (至多 3); 3 印时下一掷为"无间劫刃":
    /// 钉入敌群展开业焰轮驻场大招 (专属 ps_3_0 着色器 RevenantEXKarmaWheel)。
    /// 觉醒形态: 三刃齐出 (血缘回归), 每刃弹射 +2。
    /// </summary>
    public class InfinityKarmaBlade : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 2560;
            Item.crit = 28;
            Item.DamageType = DamageClass.Melee;
            Item.width = 50;
            Item.height = 50;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 10f;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<InfinityKarmaBladeProj>();
            Item.shootSpeed = 23f;
        }

        public override bool CanUseItem(Player player) {
            // 单刃纪律: 刃在外 (含劫刃驻场) 不能再掷 — "接刃"因此成为主动节奏点
            return player.ownedProjectileCounts[ModContent.ProjectileType<InfinityKarmaBladeProj>()] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<KarmaWheelBlade>()] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            var mp = player.GetModPlayer<RevenantEXKarmaPlayer>();
            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);

            // 3 业刃印 → 无间劫刃 (业焰轮驻场大招)
            if (mp.KarmaEdge >= 3) {
                mp.KarmaEdge = 0;
                Projectile.NewProjectile(source, player.Center + direction * 25f, direction * 26f,
                    ModContent.ProjectileType<KarmaWheelBlade>(), damage, knockback, player.whoAmI,
                    Main.MouseWorld.X, Main.MouseWorld.Y);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1f, Pitch = -0.4f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = 0.1f }, player.Center);
                WeaponVFX.AddScreenShake(player, 4f);
                return false;
            }

            int blades = mp.Awakened ? 3 : 1;
            for (int i = 0; i < blades; i++) {
                float rot = blades == 1 ? 0f : MathHelper.ToRadians((i - 1) * 11f);
                Vector2 dir = direction.RotatedBy(rot);
                int dmg = i == 1 || blades == 1 ? damage : (int)(damage * 0.5f);
                Projectile.NewProjectile(source, player.Center + dir * 25f, dir * Item.shootSpeed,
                    type, dmg, knockback, player.whoAmI, 0f, 0f, mp.Awakened ? 10f : 8f);
            }
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<KarmasMirrorBlade>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 无间业刃弹幕: 命中后在敌间弹射 (ai[2]=弹射上限), 每次弹射刃速/拖尾/音高递增;
    /// 弹尽或无目标 → 飞回; 接刃 (回到玩家) 获 1 层业刃印。
    /// </summary>
    public class InfinityKarmaBladeProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/InfinityKarmaBlade";

        private enum BladeState { Flying, Returning }
        private BladeState State {
            get => (BladeState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }
        private ref float Timer => ref Projectile.ai[1];
        private ref float MaxBounces => ref Projectile.ai[2];
        private ref float BounceCount => ref Projectile.localAI[0];
        private const float MaxDistance = 950f;
        private const float ReturnSpeed = 32f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) { Projectile.Kill(); return; }
            Timer++;
            // 刃速越快转越快 (速度对比: 弹射瞬间的提速肉眼可读)
            Projectile.rotation += (0.32f + Projectile.velocity.Length() * 0.012f) * Projectile.direction;

            switch (State) {
                case BladeState.Flying: HandleFlying(owner); break;
                case BladeState.Returning: HandleReturning(owner); break;
            }
            SpawnKarmaParticles();
            Lighting.AddLight(Projectile.Center, 0.9f + BounceCount * 0.06f, 0.5f, 1.2f);
        }

        private void HandleFlying(Player owner) {
            Projectile.velocity *= 0.978f;
            float distanceToPlayer = Vector2.Distance(Projectile.Center, owner.Center);
            if (distanceToPlayer > MaxDistance || Projectile.velocity.Length() < 3f || Timer > 55) {
                State = BladeState.Returning;
                Timer = 0;
                SoundEngine.PlaySound(SoundID.Item7 with { Pitch = 0.5f }, Projectile.Center);
            }
        }

        private void HandleReturning(Player owner) {
            Vector2 toPlayer = owner.Center - Projectile.Center;
            float distance = toPlayer.Length();
            Vector2 direction = toPlayer.SafeNormalize(Vector2.Zero);
            float returnSpeed = MathHelper.Lerp(ReturnSpeed, ReturnSpeed * 1.9f, 1f - MathHelper.Clamp(distance / MaxDistance, 0f, 1f));
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * returnSpeed, 0.2f);

            if (distance < 42f) {
                // —— 接刃: 获 1 层业刃印 (至多 3), 音高随层数递升 ——
                if (Projectile.owner == Main.myPlayer) {
                    var mp = owner.GetModPlayer<RevenantEXKarmaPlayer>();
                    mp.KarmaEdge = Math.Min(3, mp.KarmaEdge + 1);
                    SoundEngine.PlaySound(SoundID.Item7 with { Volume = 0.8f, Pitch = -0.2f + mp.KarmaEdge * 0.25f }, owner.Center);
                    if (mp.KarmaEdge >= 3) {
                        // 3 印就绪提示: 刃光大闪
                        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), owner.Center,
                            ACMWeaponBurst.SoulFire, scale: 1.3f, owner: Projectile.owner);
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = -0.1f }, owner.Center);
                    }
                }
                Projectile.Kill();
            }
        }

        private void SpawnKarmaParticles() {
            // 业焰随弹射数增浓
            int rate = BounceCount >= 4f ? 1 : 2;
            if (Main.rand.NextBool(rate)) {
                Dust flame = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(16, 16), 4, 4,
                    BounceCount >= 4f ? DustID.Torch : DustID.Shadowflame,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    100, default, Main.rand.NextFloat(1.2f, 2f));
                flame.noGravity = true;
            }
            if (Main.rand.NextBool(3)) {
                Dust glint = Dust.NewDustDirect(Projectile.Center, 4, 4, DustID.SilverCoin,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 40, default, 1f);
                glint.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Player owner = Main.player[Projectile.owner];
            if (Projectile.owner == Main.myPlayer)
                owner.GetModPlayer<RevenantEXKarmaPlayer>().AddKarma(2f);

            target.AddBuff(BuffID.Ichor, 300);
            target.AddBuff(BuffID.ShadowFlame, 300);

            // 命中冲击演出: 规模随弹射数递增 (业障叠层的视觉语言)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                BounceCount >= 4f ? ACMWeaponBurst.SoulFire : ACMWeaponBurst.AbyssPurple,
                scale: 1f + BounceCount * 0.08f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, MathF.Min(2f + BounceCount * 0.3f, 4f));

            if (State != BladeState.Flying)
                return;

            // —— 无间弹射: 寻找下一个目标, 刃速重置并递增 ——
            BounceCount++;
            if (BounceCount < MaxBounces) {
                NPC next = FindBounceTarget(target);
                if (next != null) {
                    float speed = 24f + BounceCount * 1.6f;
                    Projectile.velocity = (next.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * speed;
                    Timer = 0;
                    Projectile.netUpdate = true;
                    // 音高随弹射数攀升 (轮回加速的听觉语言)
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = -0.2f + BounceCount * 0.09f }, target.Center);
                    return;
                }
            }
            // 弹尽 / 无目标 → 归刃
            State = BladeState.Returning;
            Timer = 0;
        }

        private NPC FindBounceTarget(NPC exclude) {
            NPC best = null;
            float bestDist = 480f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy() || npc.whoAmI == exclude.whoAmI)
                    continue;
                // 跳过仍在本地免疫窗口内的目标, 避免弹到打不动的敌人
                if (Projectile.localNPCImmunity[i] > 0)
                    continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < bestDist) { bestDist = dist; best = npc; }
            }
            // 实在没有新目标就允许回打原目标 (免疫已过时)
            if (best == null && exclude.CanBeChasedBy() && Projectile.localNPCImmunity[exclude.whoAmI] <= 0
                && Vector2.Distance(Projectile.Center, exclude.Center) < 480f)
                best = exclude;
            return best;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            // 拖尾随弹射数增宽、向业焰红偏移
            float heat = MathHelper.Clamp(BounceCount / 8f, 0f, 1f);
            Color outer = Color.Lerp(new Color(140, 80, 220), new Color(230, 90, 60), heat);
            Color inner = Color.Lerp(new Color(240, 235, 255), new Color(255, 210, 130), heat);
            WeaponVFX.DrawProjectileTrail(Projectile, 16f + heat * 10f, outer, inner, uvScroll: Timer * 0.04f);

            Color mainColor = Color.Lerp(lightColor, new Color(240, 230, 255), 0.4f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                float pulse = 0.35f + heat * 0.25f + MathF.Sin(Timer * 0.2f) * 0.1f;
                Color starColor = Color.Lerp(new Color(220, 210, 255), new Color(255, 200, 120), heat) * 0.6f;
                starColor.A = 0;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor,
                    Timer * 0.12f, starOrigin, pulse, SpriteEffects.None, 0);
            }

            Color glowColor = Color.Lerp(new Color(200, 190, 240), new Color(255, 150, 90), heat) * 0.4f;
            glowColor.A = 0;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glowColor,
                Projectile.rotation, origin, Projectile.scale * (1.2f + heat * 0.15f), SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 12; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.SilverCoin,
                    Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f),
                    60, default, Main.rand.NextFloat(1.2f, 1.8f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 无间劫刃 (3 业刃印大招): 飞向目标点钉入自旋, 展开 RevenantEXKarmaWheel 业焰轮驻场
    /// 150f (大判定区高频 tick, 伤害为名义 0.25×/跳), 收束时镜面折射终爆 (KarmaMirrorWard 血缘呼应)。
    /// </summary>
    public class KarmaWheelBlade : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/InfinityKarmaBlade";

        private ref float TargetX => ref Projectile.ai[0];
        private ref float TargetY => ref Projectile.ai[1];
        private ref float Phase => ref Projectile.ai[2];       // 0=飞行 1=驻场
        private ref float Timer => ref Projectile.localAI[0];
        private const int WheelTime = 150;
        private const float WheelRadius = 250f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.KarmaWheelBlade.DisplayName",
                () => "Avici Karma Wheel");
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 9;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            // 驻场 tick 伤害 = 名义 0.25×/跳 (150f/9t ≈ 16 跳 ≈ 4× 名义/轮); 0 击退防搅拌。飞行段全额。
            if (Phase != 0f) {
                modifiers.FinalDamage *= 0.25f;
                modifiers.Knockback *= 0f;
            }
        }

        // 驻场期判定裁成圆形, 与业焰轮视觉严格对齐
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase == 0f)
                return null;
            Vector2 closest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(closest, Projectile.Center) <= WheelRadius * WheelRadius;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) { Projectile.Kill(); return; }
            Timer++;

            if (Phase == 0f) {
                // 飞向目标点 (业刃自旋加速)
                Projectile.rotation += 0.5f * Projectile.direction;
                Vector2 target = new(TargetX, TargetY);
                if (Vector2.Distance(Projectile.Center, target) < 34f || Timer > 45f)
                    AnchorWheel();
                if (Main.rand.NextBool(2)) {
                    Dust flame = Dust.NewDustDirect(Projectile.Center, 4, 4, DustID.Torch,
                        -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f, 80, default, 1.6f);
                    flame.noGravity = true;
                }
            }
            else {
                // 驻场: 自旋加速 → 尾段收束
                Projectile.velocity = Vector2.Zero;
                float p = Timer / WheelTime;
                Projectile.rotation += 0.25f + p * 0.55f;

                // 每 30f 一次轮压脉冲 (声画同步)
                if ((int)Timer % 30 == 0 && Timer < WheelTime - 12f) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.55f, Pitch = -0.3f + p * 0.5f }, Projectile.Center);
                    WeaponVFX.AddScreenShake(Projectile.Center, 2.5f);
                }
                // 业焰吸入粒子 (converging streaks, 尾段 25% 静默 — 爆前收声)
                if (p < 0.75f && Main.rand.NextBool(2)) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(WheelRadius * 0.7f, WheelRadius * 1.2f);
                    Dust pull = Dust.NewDustPerfect(pos, DustID.PurpleTorch,
                        (Projectile.Center - pos) * 0.055f, 100, default, Main.rand.NextFloat(1.4f, 2.2f));
                    pull.noGravity = true;
                }

                if (Timer >= WheelTime)
                    DetonateWheel(owner);
            }
            Lighting.AddLight(Projectile.Center, 1.2f, 0.7f, 1.4f);
        }

        private void AnchorWheel() {
            Phase = 1f;
            Timer = 0f;
            Projectile.velocity = Vector2.Zero;
            // 判定区扩为业焰轮直径
            Projectile.Resize((int)(WheelRadius * 2f), (int)(WheelRadius * 2f));
            Projectile.netUpdate = true;
            SoundEngine.PlaySound(SoundID.Item70 with { Volume = 0.9f, Pitch = -0.4f }, Projectile.Center);
            WeaponVFX.AddScreenShake(Projectile.Center, 5f);
            if (Projectile.owner == Main.myPlayer)
                ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center,
                    ACMWeaponBurst.FengduVoid, scale: 1.6f, owner: Projectile.owner);
        }

        private void DetonateWheel(Player owner) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.1f, Pitch = -0.5f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.8f, Pitch = 0.4f }, Projectile.Center);
            WeaponVFX.AddScreenShake(Projectile.Center, 8f);
            if (Projectile.owner == Main.myPlayer) {
                // 终爆: 镜面折射护罩 (孽镜血缘) + 虚空爆
                KarmaMirrorWard.Spawn(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.owner);
                ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center,
                    ACMWeaponBurst.FengduVoid, scale: 2f, owner: Projectile.owner);
            }
            for (int i = 0; i < 30; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(11f, 11f);
                Dust burst = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.Torch : DustID.PurpleTorch,
                    vel, 80, default, Main.rand.NextFloat(1.8f, 3f));
                burst.noGravity = true;
            }
            Projectile.Kill();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer)
                Main.player[Projectile.owner].GetModPlayer<RevenantEXKarmaPlayer>().AddKarma(1f);
            target.AddBuff(BuffID.ShadowFlame, 240);
            target.AddBuff(BuffID.OnFire3, 240);
            if (Main.rand.NextBool(3))
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.SoulFire, scale: 0.8f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            if (Phase == 0f) {
                WeaponVFX.DrawProjectileTrail(Projectile, 22f,
                    new Color(230, 90, 60), new Color(255, 220, 140), uvScroll: Timer * 0.05f);
                Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                    Color.Lerp(lightColor, new Color(255, 220, 200), 0.5f),
                    Projectile.rotation, origin, Projectile.scale * 1.15f, SpriteEffects.None, 0);
                return false;
            }

            float p = Timer / WheelTime;
            // 半径: 前 18f pow-ease 展开 → 稳定 → 尾 14f 收束 (爆前变小)
            float grow = 1f - MathF.Pow(1f - MathHelper.Clamp(Timer / 18f, 0f, 1f), 3f);
            float collapse = MathHelper.Clamp((WheelTime - Timer) / 14f, 0f, 1f);
            float radius = WheelRadius * grow * MathHelper.Lerp(0.42f, 1f, collapse);
            float intensity = 0.85f * grow * MathHelper.Lerp(0.85f, 1f, collapse);

            // —— 业焰轮 (专属着色器, decal, 不占全屏名额) ——
            Effect wheel = WeaponVFX.GetEffect("RevenantEXKarmaWheel");
            if (wheel != null) {
                ACMShaders.WorldDecalParams(Projectile.Center, radius, out Vector2 uv, out float rFrac, out float aspect);
                wheel.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                wheel.Parameters["uCenter"]?.SetValue(uv);
                wheel.Parameters["uRadius"]?.SetValue(rFrac);
                wheel.Parameters["uIntensity"]?.SetValue(intensity);
                wheel.Parameters["uAspect"]?.SetValue(aspect);
                wheel.Parameters["uColorPrimary"]?.SetValue(new Color(255, 185, 90).ToVector4());
                wheel.Parameters["uColorSecondary"]?.SetValue(new Color(130, 45, 210).ToVector4());
                wheel.Parameters["uSpin"]?.SetValue(-Timer * (0.05f + p * 0.06f));
                wheel.Parameters["uSpokes"]?.SetValue(8f);

                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                ACMShaders.DrawScreenSpaceDecalStandalone(wheel, BlendState.Additive);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            // 中心钉刃 (高速自旋)
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                Color.Lerp(lightColor, new Color(255, 230, 210), 0.6f),
                Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);
            Color coreGlow = new Color(255, 190, 110) * 0.7f;
            coreGlow.A = 0;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, coreGlow,
                Projectile.rotation, origin, Projectile.scale * 1.45f, SpriteEffects.None, 0);

            // 轮缘冲击环脉冲 (与 30f 音画同步)
            float pulsePhase = (int)Timer % 30 / 30f;
            if (Timer < WheelTime - 12f)
                WeaponVFX.DrawShockwaveRing(Projectile.Center, radius * (0.5f + pulsePhase * 0.55f), 10f,
                    (1f - pulsePhase) * 0.5f * grow, new Color(255, 200, 120), new Color(120, 40, 190));

            return false;
        }
    }

    /// <summary>
    /// 无间镜像折射演出弹幕 (纯视觉, damage=0): 劫刃终爆瞬间在爆心短暂展开 ReflectWard 折射护罩
    /// (银紫"镜面"), 配冲击环。全屏后处理走单一名额仲裁; 绘制只在 PreDraw。
    /// </summary>
    public class KarmaMirrorWard : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 26;

        public static void Spawn(IEntitySource source, Vector2 worldPos, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<KarmaMirrorWard>(), 0, 0f, owner);
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
            float fade = MathHelper.Clamp(life < 0.2f ? life / 0.2f : 1f - (life - 0.2f) / 0.8f, 0f, 1f);

            // —— ReflectWard 镜面折射罩 (银紫, 短促), 占单一全屏名额 ——
            Effect ward = ACMShaders.ReflectWard;
            if (ward != null && fade > 0.05f && ACMShaders.RequestFullscreenSlot()) {
                Vector2 uv = (Projectile.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
                float aspect = (float)Main.screenWidth / Main.screenHeight;
                Color wardCol = new Color(210, 200, 255);
                Vector4 colVec = wardCol.ToVector4();
                colVec.W = 0.55f + 0.35f * fade;

                ward.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                ward.Parameters["uCenter"]?.SetValue(uv);
                ward.Parameters["uRadius"]?.SetValue(150f / Main.screenHeight);
                ward.Parameters["uIntensity"]?.SetValue(fade * 0.7f);
                ward.Parameters["uAspect"]?.SetValue(aspect);
                ward.Parameters["uColor"]?.SetValue(colVec);
                ward.Parameters["uHexScale"]?.SetValue(9f);
                ward.Parameters["uRefract"]?.SetValue(0.6f);
                ward.Parameters["uFlash"]?.SetValue(fade);

                ACMShaders.ApplyScreenPostProcess(Main.spriteBatch, ward, bindNoise: true);
            }

            WeaponVFX.DrawShockwaveRing(Projectile.Center, 14f + life * 80f, 8f, fade * 0.8f,
                new Color(235, 230, 255), new Color(130, 80, 210));
            WeaponVFX.DrawGlowBurst(Projectile.Center, (1.2f + life) * 1.4f, new Color(210, 200, 255) * (fade * 0.7f));

            return false;
        }
    }
}
