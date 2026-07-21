using AncientChineseMythology.Buffs;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons
{
    // ============================================================
    //  生肖符箓系列 — 道教黄纸朱砂符 × 十二生肖灵兽
    //  统一语言: 掷符(纸片翻飞+朱砂走线) → 朱印起爆 → 灵兽以书法笔画化形。
    //  设计文档: Docs/WeaponRedo/Charms.md
    // ============================================================

    /// <summary>系列共享调色板与着色器绘制助手 (CharmTalisman / CharmDragonBody, 经 WeaponVFX.GetEffect 缓存)。</summary>
    public static class CharmVFX
    {
        // charmId: 0鼠 1牛 2兔 3龙 4蛇 5马 6鸡 7狗 8猪
        public const int Rat = 0, Ox = 1, Rabbit = 2, Dragon = 3, Snake = 4, Horse = 5, Rooster = 6, Dog = 7, Pig = 8;

        /// <summary>符纸 ai[0] 旗标位: 低 4 位 = charmId。</summary>
        public const int FlagUlt = 16;     // 龙符第 4 掷 = 雷龙劫
        public const int FlagAmbush = 32;  // 蛇蜕伏击

        public static readonly Color PaperColor = new(232, 205, 148);
        public static readonly Color InkVermilion = new(198, 44, 34);
        public static readonly Color InkGold = new(255, 200, 90);

        public readonly struct CharmPalette
        {
            public readonly Color Glow;   // 灵兽亮芯
            public readonly Color Dark;   // 笔画外层暗色
            public readonly int Burst;    // ACMWeaponBurst 主题
            public CharmPalette(Color glow, Color dark, int burst) { Glow = glow; Dark = dark; Burst = burst; }
        }

        private static readonly CharmPalette[] Palettes = {
            new(new Color(255, 236, 170), new Color(120, 110, 90),  ACMWeaponBurst.Gold),    // 鼠·窃金
            new(new Color(255, 196, 110), new Color(150, 82, 38),   ACMWeaponBurst.Bronze),  // 牛·赭土
            new(new Color(228, 240, 255), new Color(110, 140, 215), ACMWeaponBurst.Gem),     // 兔·月白
            new(new Color(255, 235, 150), new Color(118, 88, 215),  ACMWeaponBurst.Gold),    // 龙·金雷紫
            new(new Color(178, 255, 190), new Color(28, 135, 80),   ACMWeaponBurst.Nature),  // 蛇·青绿
            new(new Color(255, 200, 120), new Color(196, 62, 30),   ACMWeaponBurst.Crimson), // 马·赤驹
            new(new Color(255, 240, 185), new Color(226, 92, 42),   ACMWeaponBurst.Gold),    // 鸡·破晓
            new(new Color(200, 220, 255), new Color(72, 90, 150),   ACMWeaponBurst.Shadow),  // 狗·玄青
            new(new Color(255, 226, 140), new Color(200, 140, 45),  ACMWeaponBurst.Gold),    // 猪·金黄
        };

        public static CharmPalette GetPalette(int charmId)
            => Palettes[Math.Clamp(charmId, 0, Palettes.Length - 1)];

        /// <summary>
        /// 绘制一张符纸/朱印四边形 (CharmTalisman.fx)。须在有活动批阶段调用 (PreDraw)。
        /// mode 0=符纸 (AlphaBlend), mode 1=朱印 (Additive)。载体为共享噪声整图 (uv 0~1)。
        /// </summary>
        public static void DrawTalisman(Vector2 worldCenter, float rotation, float sizePx, float intensity,
            float stroke, float burn, float spread, float flash, int charmId, Color ink, int mode) {
            if (Main.dedServ || intensity <= 0.01f)
                return;
            Effect fx = WeaponVFX.GetEffect("CharmTalisman");
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uMode"]?.SetValue((float)mode);
            fx.Parameters["uStroke"]?.SetValue(MathHelper.Clamp(stroke, 0f, 1f));
            fx.Parameters["uBurn"]?.SetValue(MathHelper.Clamp(burn, 0f, 1f));
            fx.Parameters["uSpread"]?.SetValue(MathHelper.Clamp(spread, 0f, 1f));
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1f));
            fx.Parameters["uCharmId"]?.SetValue((float)charmId);
            fx.Parameters["uPaperColor"]?.SetValue(PaperColor.ToVector4());
            fx.Parameters["uInkColor"]?.SetValue(ink.ToVector4());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, mode == 1 ? BlendState.Additive : BlendState.AlphaBlend,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, fx,
                Main.GameViewMatrix.TransformationMatrix);
            float scale = sizePx / noise.Height;
            sb.Draw(noise, worldCenter - Main.screenPosition, null, Color.White, rotation,
                noise.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 雷龙真身条带 (CharmDragonBody.fx, BuildRibbonStrip 顶点契约: 首点=龙头)。
        /// 须在有活动批阶段调用; 受 Trail 配置降细分。
        /// </summary>
        public static void DrawDragonRibbon(Vector2[] worldPoints, float headWidth, Color core, Color edge,
            float energy, float pulse, float intensity = 1f) {
            if (Main.dedServ || worldPoints == null || worldPoints.Length < 2 || intensity <= 0.01f)
                return;
            Effect fx = WeaponVFX.GetEffect("CharmDragonBody");
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            int subdiv = MythologyConfig.Trail == TrailQualityLevel.High ? 3 : 2;
            Vector2[] pts = new Vector2[worldPoints.Length];
            for (int i = 0; i < worldPoints.Length; i++)
                pts[i] = worldPoints[i] - Main.screenPosition;

            var verts = ACMUtils.BuildRibbonStrip(pts,
                p => MathHelper.Lerp(headWidth, headWidth * 0.22f, p * p),
                _ => Color.White, 0f, subdiv);
            if (verts.Length < 4)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uColorCore"]?.SetValue(core.ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(edge.ToVector4());
            fx.Parameters["uEnergy"]?.SetValue(MathHelper.Clamp(energy, 0f, 1f));
            fx.Parameters["uPulse"]?.SetValue(MathHelper.Clamp(pulse, 0f, 1f));

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[0] = noise;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>单条书法笔画 (双层 ribbon, 外暗内亮), 灵兽化形的基本元素。</summary>
        public static void DrawStroke(Vector2[] worldPoints, float width, int charmId, float alpha = 1f, float uvScroll = 0f) {
            if (worldPoints == null || worldPoints.Length < 2 || alpha <= 0.02f)
                return;
            CharmPalette pal = GetPalette(charmId);
            Color outer = pal.Dark; outer.A = (byte)(170 * MathHelper.Clamp(alpha, 0f, 1f));
            Color inner = pal.Glow; inner.A = (byte)(210 * MathHelper.Clamp(alpha, 0f, 1f));
            WeaponVFX.DrawRibbonTrail(worldPoints, width, outer, inner, tex: ACMAsset.SoftGlow,
                uvScroll: uvScroll, subdivisions: 2);
        }

        /// <summary>掷符音效双层 (低频挥掷 + 高频符咒)。</summary>
        public static void PlayThrowSound(Vector2 pos) {
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = Main.rand.NextFloat(-0.15f, 0.15f) }, pos);
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.45f, Pitch = 0.3f }, pos);
        }

        /// <summary>朱印落章音效 (低频砸章 + 高频清音)。</summary>
        public static void PlayStampSound(Vector2 pos, float pitchShift = 0f) {
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = -0.4f + pitchShift }, pos);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = 0.1f + pitchShift }, pos);
        }
    }

    // ============================================================
    //  基类: 掷符武器 (Swing 掷出 CharmPaperProj)
    // ============================================================

    public abstract class BaseZodiacCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Charms/" + Name;

        /// <summary>生肖编号 (CharmVFX.Rat 等)。</summary>
        public abstract int CharmId { get; }

        protected abstract int BaseDamage { get; }
        protected abstract int UseTime { get; }
        protected abstract int ManaCost { get; }
        protected abstract float KnockBack { get; }

        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = UseTime;
            Item.useAnimation = UseTime;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.DamageType = DamageClass.Magic;
            Item.damage = BaseDamage;
            Item.mana = ManaCost;
            Item.knockBack = KnockBack;
            Item.shoot = ModContent.ProjectileType<CharmPaperProj>();
            Item.shootSpeed = 14f;
            Item.UseSound = null;
            Item.value = Item.buyPrice(gold: 100);
            Item.rare = ItemRarityID.Red;
        }

        /// <summary>子类附加旗标 (蛇蜕/雷龙劫等), 编码进符纸 ai[0] 高位。</summary>
        protected virtual int GetThrowFlags(Player player) => 0;

        /// <summary>掷出瞬间的子类钩子 (血祭/计数等), 仅 owner 端。</summary>
        protected virtual void OnCharmThrow(Player player) { }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback) {
            OnCharmThrow(player);
            CharmVFX.PlayThrowSound(player.Center);

            Vector2 target = Main.MouseWorld;
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI,
                CharmId | GetThrowFlags(player), target.X, target.Y);
            return false;
        }

        //物品在地面上的缩小绘制 (系列统一)
        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            ref float rotation, ref float scale, int whoAmI) {
            Texture2D texture = TextureAssets.Item[Item.type].Value;
            spriteBatch.Draw(texture, Item.Center - Main.screenPosition, null, lightColor, rotation,
                texture.Size() * 0.5f, 0.5f, SpriteEffects.None, 0f);
            return false;
        }
    }

    // ============================================================
    //  九张符
    // ============================================================

    /// <summary>鸡符咒 — 金鸡破晓: 落点立鸡灵, 三声啼鸣扩散破晓冲击环。</summary>
    public class ChickenCharm : BaseZodiacCharm
    {
        public override int CharmId => CharmVFX.Rooster;
        protected override int BaseDamage => 160;
        protected override int UseTime => 42;
        protected override int ManaCost => 12;
        protected override float KnockBack => 4f;

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacChicken>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    /// <summary>牛符咒 — 蛮牛攻城: 刨地蓄势后横贯冲撞, 重击退贯穿一路。</summary>
    public class CowCharm : BaseZodiacCharm
    {
        public override int CharmId => CharmVFX.Ox;
        protected override int BaseDamage => 380;
        protected override int UseTime => 48;
        protected override int ManaCost => 12;
        protected override float KnockBack => 9f;

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacCow>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    /// <summary>狗符咒 — 灵犬咬定: 犬灵咬住敌人持续撕咬, 全场至多三犬。</summary>
    public class DogCharm : BaseZodiacCharm
    {
        public override int CharmId => CharmVFX.Dog;
        protected override int BaseDamage => 95;
        protected override int UseTime => 40;
        protected override int ManaCost => 10;
        protected override float KnockBack => 2f;

        public override bool CanUseItem(Player player) {
            //全场 ≤3 犬 (锚型持续伤害的上限)
            if (player.ownedProjectileCounts[ModContent.ProjectileType<DogSpiritProj>()] >= 3)
                return false;
            return base.CanUseItem(player);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacDog>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    /// <summary>龙符咒 — 血绘雷龙 (系列旗舰): 以血为引召雷龙; 每第四掷降下雷龙劫。</summary>
    public class DragonCharm : BaseZodiacCharm
    {
        public override int CharmId => CharmVFX.Dragon;
        protected override int BaseDamage => 300;
        protected override int UseTime => 30;
        protected override int ManaCost => 0;
        protected override float KnockBack => 5f;

        private const int LifeCost = 12;
        private int _throwCount;
        private bool _ultThisThrow;

        public override void SetStaticDefaults() {
            Language.GetOrRegister("Mods.AncientChineseMythology.Items.DragonCharm.JieReady",
                () => "The next talisman calls down the Dragon Tribulation");
        }

        public override void SetDefaults() {
            base.SetDefaults();
            Item.width = 28;
            Item.height = 30;
            Item.DamageType = DamageClass.Ranged; // 职业定位不回退
            Item.mana = 0;
        }

        public override bool CanUseItem(Player player) {
            //血量门槛: 血祭不可能致死
            if (player.statLife <= 60)
                return false;
            return base.CanUseItem(player);
        }

        protected override int GetThrowFlags(Player player)
            => _ultThisThrow ? CharmVFX.FlagUlt : 0;

        protected override void OnCharmThrow(Player player) {
            //第 4 掷 = 雷龙劫 (owner 端计数)
            _throwCount++;
            _ultThisThrow = _throwCount >= 4;
            if (_ultThisThrow)
                _throwCount = 0;

            //血祭: 朱砂调血 (owner 端, 与原实现同承载; CanUseItem 已保证不致死)
            player.statLife -= LifeCost;
            CombatText.NewText(player.Hitbox, CombatText.LifeRegenNegative, LifeCost, true);
            SoundEngine.PlaySound(SoundID.Item44 with { Volume = 0.4f, Pitch = -0.2f }, player.Center);
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            if (_throwCount >= 3)
                tooltips.Add(new TooltipLine(Mod, "DragonJieReady", Language.GetTextValue(
                    "Mods.AncientChineseMythology.Items.DragonCharm.JieReady")));
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacDragon>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    /// <summary>马符咒 — 万马奔腾: 落点三波九骑奔雷马灵横贯扫场。</summary>
    public class HorseCharm : BaseZodiacCharm
    {
        public override int CharmId => CharmVFX.Horse;
        protected override int BaseDamage => 95;
        protected override int UseTime => 45;
        protected override int ManaCost => 11;
        protected override float KnockBack => 6f;

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacHorse>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    /// <summary>猪符咒 — 贪食洪流: 引导金色洪流, 越贪越壮, 松手打个饱嗝。</summary>
    public class PigCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Charms/PigCharm";

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.channel = true;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.DamageType = DamageClass.Magic;
            Item.damage = 168;
            Item.mana = 10;
            Item.knockBack = 2f;
            Item.UseSound = SoundID.Item8;
            Item.shoot = ModContent.ProjectileType<PigCharmLaser>();
            Item.shootSpeed = 1f;
            Item.value = Item.buyPrice(gold: 100);
            Item.rare = ItemRarityID.Red;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback) {
            //同 owner 同屏仅一根洪流
            if (player.ownedProjectileCounts[type] > 0)
                return false;
            Projectile.NewProjectile(source, player.Center, velocity.SafeNormalize(Vector2.UnitX),
                type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacPig>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            ref float rotation, ref float scale, int whoAmI) {
            Texture2D texture = TextureAssets.Item[Item.type].Value;
            spriteBatch.Draw(texture, Item.Center - Main.screenPosition, null, lightColor, rotation,
                texture.Size() * 0.5f, 0.5f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>兔符咒 — 玉兔三踏: 灵兔瞬步三连击, 第三击捣月下砸。</summary>
    public class RabbitCharm : BaseZodiacCharm
    {
        public override int CharmId => CharmVFX.Rabbit;
        protected override int BaseDamage => 130;
        protected override int UseTime => 40;
        protected override int ManaCost => 10;
        protected override float KnockBack => 3f;

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacRabbit>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    /// <summary>蛇符咒 — 灵蛇缠身: 蜿蜒追踪, 三口缠身; 右键蛇蜕短隐伏击。</summary>
    public class SnakeCharm : BaseZodiacCharm
    {
        public override int CharmId => CharmVFX.Snake;
        protected override int BaseDamage => 150;
        protected override int UseTime => 36;
        protected override int ManaCost => 9;
        protected override float KnockBack => 2f;

        public override bool AltFunctionUse(Player player) => true;

        protected override int GetThrowFlags(Player player)
            => player.HasBuff<SnakeAmbushBuff>() ? CharmVFX.FlagAmbush : 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback) {
            //存档迁移礼貌: 清掉旧版遗留的永久隐身
            if (player.HasBuff<SnakeInvisibilityBuff>())
                player.ClearBuff(ModContent.BuffType<SnakeInvisibilityBuff>());

            if (player.altFunctionUse == 2) {
                //右键: 蛇蜕 (2.5s 低仇恨, 期间掷蛇 = 伏击); 耗蓝走 Item.mana, 不再额外扣
                player.AddBuff(ModContent.BuffType<SnakeAmbushBuff>(), 150);
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.35f, Volume = 0.7f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f, Volume = 0.45f }, player.Center);
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacSnake>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    /// <summary>鼠符咒 — 群鼠窃金: 五只鼠灵乱窜啃咬; 亦是唤醒圣主雕像的钥匙 (手持右键雕像)。</summary>
    public class RatCharm : BaseZodiacCharm
    {
        public override int CharmId => CharmVFX.Rat;
        protected override int BaseDamage => 70;
        protected override int UseTime => 32;
        protected override int ManaCost => 7;
        protected override float KnockBack => 1f;

        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue; //钥匙职能的原始稀有度不动
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacRat>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    // ============================================================
    //  蛇蜕 Buff (系列内新 Buff, 不动共享 Buffs/)
    // ============================================================

    /// <summary>蛇蜕: 低仇恨潜行, 期间掷出的蛇灵首口 ×1.6。</summary>
    public class SnakeAmbushBuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/SnakeInvisibilityBuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = false;
            Language.GetOrRegister("Mods.AncientChineseMythology.Buffs.SnakeAmbushBuff.DisplayName", () => "Serpent Molt");
            Language.GetOrRegister("Mods.AncientChineseMythology.Buffs.SnakeAmbushBuff.Description",
                () => "Enemies barely notice you; your next snake spirits strike from ambush");
        }

        public override void Update(Player player, ref int buffIndex) {
            player.aggro -= 400;
        }
    }

    // ============================================================
    //  符纸弹幕 — 系列统一投掷物
    // ============================================================

    /// <summary>
    /// 掷出的黄纸符: 翻飞着奔向目标点, 飞行中朱砂笔画逐笔写就, 到点/命中盖印起爆并化形灵兽。
    /// ai[0]=charmId|旗标, ai[1]/ai[2]=目标点。龙符变体在半途燃尽化龙。
    /// </summary>
    public class CharmPaperProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private int CharmId => (int)Projectile.ai[0] & 15;
        private bool IsUlt => ((int)Projectile.ai[0] & CharmVFX.FlagUlt) != 0;
        private bool IsAmbush => ((int)Projectile.ai[0] & CharmVFX.FlagAmbush) != 0;
        private Vector2 TargetPos => new(Projectile.ai[1], Projectile.ai[2]);
        private ref float Age => ref Projectile.localAI[0];

        private float _flightEstimate = 40f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.CharmPaperProj.DisplayName", () => "Zodiac Talisman");
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            if (Age == 0f) {
                //龙符走远程管线 (职业定位), 其余魔法
                if (CharmId == CharmVFX.Dragon)
                    Projectile.DamageType = DamageClass.Ranged;
                _flightEstimate = MathHelper.Clamp(Vector2.Distance(Projectile.Center, TargetPos) / 13f, 18f, 55f);
            }
            Age++;

            //朝目标点微弧修正 + 纸片翻飞
            Vector2 toTarget = TargetPos - Projectile.Center;
            float dist = toTarget.Length();
            if (dist > 4f) {
                Vector2 desired = toTarget.SafeNormalize(Vector2.UnitX) * 14f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.09f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2
                + MathF.Sin(Age * 0.4f) * 0.28f;

            //朱砂微尘走线
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    DustID.RedTorch, -Projectile.velocity * 0.05f, 120, default, 0.9f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, CharmVFX.GetPalette(CharmId).Glow.ToVector3() * 0.25f);

            //龙符: 半途燃尽化龙 (纸不用抵达)
            if (CharmId == CharmVFX.Dragon && Age >= 14f) {
                Detonate();
                return;
            }
            //抵达目标点
            if (dist < 26f)
                Detonate();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Detonate();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                CharmVFX.GetPalette(CharmId).Burst, 0.8f, Projectile.owner);
            Detonate();
        }

        public override void OnKill(int timeLeft) {
            //寿命耗尽 (没到点没命中) 也要起爆, 保证每张符都有收尾
            if (timeLeft <= 0)
                DetonateEffectsOnly();
        }

        private bool _detonated;

        /// <summary>盖印起爆: 生成朱印演出 + 对应灵兽 (owner 端), 然后销毁。</summary>
        private void Detonate() {
            if (_detonated)
                return;
            _detonated = true;
            SpawnPayload(spawnSpirit: true);
            Projectile.Kill();
        }

        private void DetonateEffectsOnly() {
            if (_detonated)
                return;
            _detonated = true;
            SpawnPayload(spawnSpirit: false);
        }

        private void SpawnPayload(bool spawnSpirit) {
            if (Projectile.owner != Main.myPlayer)
                return;

            Vector2 pos = Projectile.Center;
            IEntitySource src = Projectile.GetSource_FromThis();
            int dmg = Projectile.damage;
            float kb = Projectile.knockBack;
            int owner = Projectile.owner;

            //朱印演出 (所有符共有; 雷龙劫用金印大号)
            Projectile.NewProjectile(src, pos, Vector2.Zero, ModContent.ProjectileType<CharmSealFX>(),
                0, 0f, owner, CharmId, IsUlt ? 1.6f : 1f);

            if (!spawnSpirit)
                return;

            Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            switch (CharmId) {
                case CharmVFX.Rat:
                    //五鼠散开
                    for (int i = 0; i < 5; i++) {
                        Vector2 vel = aim.RotatedBy(Main.rand.NextFloat(-1.2f, 1.2f)) * Main.rand.NextFloat(5f, 9f);
                        Projectile.NewProjectile(src, pos, vel, ModContent.ProjectileType<RatSpiritProj>(),
                            dmg, kb, owner, i);
                    }
                    break;
                case CharmVFX.Ox:
                    Projectile.NewProjectile(src, pos, Vector2.Zero, ModContent.ProjectileType<OxSpiritProj>(),
                        dmg, kb, owner, aim.X >= 0f ? 1f : -1f);
                    break;
                case CharmVFX.Rabbit:
                    Projectile.NewProjectile(src, pos, Vector2.Zero, ModContent.ProjectileType<RabbitSpiritProj>(),
                        dmg, kb, owner);
                    break;
                case CharmVFX.Dragon:
                    if (IsUlt) {
                        //雷龙劫: 大招控制器立在目标点
                        Projectile.NewProjectile(src, TargetPos, Vector2.Zero,
                            ModContent.ProjectileType<DragonCharmExplosion>(), dmg, kb, owner);
                    }
                    else {
                        //普通雷龙从燃尽点扑向目标
                        Vector2 vel = (TargetPos - pos).SafeNormalize(aim) * 10f;
                        Projectile.NewProjectile(src, pos, vel, ModContent.ProjectileType<DragonCharmLaser>(),
                            dmg, kb, owner, TargetPos.X, TargetPos.Y);
                    }
                    break;
                case CharmVFX.Snake:
                    Projectile.NewProjectile(src, pos, aim * 9f, ModContent.ProjectileType<SnakeSpiritProj>(),
                        dmg, kb, owner, IsAmbush ? 1f : 0f);
                    break;
                case CharmVFX.Horse:
                    //三波 × 三骑: 从目标侧后方 260px 起跑, 横贯目标点
                    Vector2 dir = MathF.Abs(aim.X) > 0.05f ? new Vector2(MathF.Sign(aim.X), 0f) : new Vector2(1f, 0f);
                    for (int wave = 0; wave < 3; wave++) {
                        for (int lane = -1; lane <= 1; lane++) {
                            Vector2 start = TargetPos - dir * (260f + wave * 40f) + new Vector2(0f, lane * 70f);
                            Projectile.NewProjectile(src, start, dir * 0.01f,
                                ModContent.ProjectileType<HorseSpiritProj>(), dmg, kb, owner,
                                wave * 18f + Main.rand.Next(4), lane);
                        }
                    }
                    break;
                case CharmVFX.Rooster:
                    Projectile.NewProjectile(src, pos, Vector2.Zero, ModContent.ProjectileType<RoosterSpiritProj>(),
                        dmg, kb, owner);
                    break;
                case CharmVFX.Dog:
                    Projectile.NewProjectile(src, pos, Vector2.Zero, ModContent.ProjectileType<DogSpiritProj>(),
                        dmg, kb, owner, 0f, -1f);
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            //朱砂走线细拖尾
            WeaponVFX.DrawProjectileTrail(Projectile, 5f,
                new Color(150, 30, 25, 130), new Color(255, 120, 80, 170), uvScroll: Age * 0.05f);

            //符纸本体 (笔画随飞行进度写就; 雷龙劫符转金)
            float stroke = MathHelper.Clamp(Age / _flightEstimate, 0f, 1f);
            Color ink = IsUlt ? CharmVFX.InkGold : CharmVFX.InkVermilion;
            float burn = CharmId == CharmVFX.Dragon ? MathHelper.Clamp((Age - 8f) / 7f, 0f, 0.65f) : 0f;
            CharmVFX.DrawTalisman(Projectile.Center, Projectile.rotation, 46f, 1f,
                stroke, burn, 1f, 0f, CharmId, ink, mode: 0);
            return false;
        }
    }

    // ============================================================
    //  朱印起爆演出 (纯视觉) + 通用起爆 AoE
    // ============================================================

    /// <summary>朱印落章 (纯视觉): 方印白闪 + 冲击环 + 柔光。ai[0]=charmId, ai[1]=规模。</summary>
    public class CharmSealFX : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int LifeTime = 24;
        private int CharmId => (int)Projectile.ai[0];
        private float Scale => Projectile.ai[1] <= 0f ? 1f : Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                CharmVFX.PlayStampSound(Projectile.Center, CharmId * 0.02f);
                WeaponVFX.AddScreenShake(Projectile.Center, 1.5f * Scale);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float t = 1f - Projectile.timeLeft / (float)LifeTime; // 0→1
            var pal = CharmVFX.GetPalette(CharmId);

            //方印: 出章急、余韵缓
            float grow = ACMUtils.BackOut(MathHelper.Clamp(t * 2.2f, 0f, 1f));
            float fade = 1f - ACMUtils.QuadIn(t);
            float flash = MathHelper.Clamp(1f - t * 3.5f, 0f, 1f);
            Color ink = CharmId == CharmVFX.Dragon ? CharmVFX.InkGold : CharmVFX.InkVermilion;
            CharmVFX.DrawTalisman(Projectile.Center, t * 0.15f, (58f + 30f * t) * Scale, fade,
                1f, 0f, 1f, flash, CharmId, ink, mode: 1);

            //冲击环 + 柔光
            WeaponVFX.DrawShockwaveRing(Projectile.Center, (10f + t * 66f) * Scale, 9f * Scale,
                (1f - t) * 0.8f, pal.Glow, pal.Dark);
            WeaponVFX.DrawGlowBurst(Projectile.Center, (0.9f + t * 0.8f) * Scale, pal.Glow * ((1f - t) * 0.7f));
            return false;
        }
    }

    /// <summary>
    /// 通用起爆 AoE (龙印二段 / 雷龙劫 / 嗝爆共用): 短命圆形范围判定, 视觉=扩张环。
    /// ai[0]=最大半径, ai[1]=旗标(1=首次命中回20血, 2=远程管线) + charmId*16。
    /// </summary>
    public class CharmNovaProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int LifeTime = 12;
        private float MaxRadius => MathF.Max(Projectile.ai[0], 40f);
        private int Flags => (int)Projectile.ai[1] & 15;
        private int CharmId => (int)Projectile.ai[1] / 16;

        public override void SetStaticDefaults() {
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.CharmNovaProj.DisplayName", () => "Seal Nova");
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        private float Progress => 1f - Projectile.timeLeft / (float)LifeTime;
        private float CurrentRadius => MaxRadius * ACMUtils.QuadOut(MathHelper.Clamp(Progress * 1.6f, 0f, 1f));

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if ((Flags & 2) != 0)
                    Projectile.DamageType = DamageClass.Ranged;
            }
        }

        public override bool? CanDamage() => Projectile.timeLeft > 4;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float reach = CurrentRadius + MathF.Max(targetHitbox.Width, targetHitbox.Height) * 0.5f;
            return Vector2.DistanceSquared(Projectile.Center, targetHitbox.Center.ToVector2()) <= reach * reach;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //雷龙劫·偿: 首次命中回 20 血 (血债血偿)
            if ((Flags & 1) != 0 && Projectile.localAI[1] == 0f && Projectile.owner == Main.myPlayer) {
                Projectile.localAI[1] = 1f;
                Main.player[Projectile.owner].Heal(20);
            }
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                CharmVFX.GetPalette(CharmId).Burst, 1.1f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            var pal = CharmVFX.GetPalette(CharmId);
            float alpha = 1f - ACMUtils.QuadIn(Progress);
            WeaponVFX.DrawShockwaveRing(Projectile.Center, CurrentRadius, 16f, alpha * 0.9f, pal.Glow, pal.Dark);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 1.4f * (1f - Progress) + 0.4f, pal.Glow * (alpha * 0.8f));
            return false;
        }
    }

    // ============================================================
    //  灵兽弹幕 — 朱砂笔画化形
    // ============================================================

    /// <summary>鼠灵 ×5: 乱窜弱追踪, 窃金啃咬 (单穿透)。ai[0]=个体相位。</summary>
    public class RatSpiritProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.RatSpiritProj.DisplayName", () => "Rat Spirit");
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            float phase = Projectile.ai[0];
            Projectile.localAI[0]++;

            //乱窜: 周期性抖动转向 + 对最近敌人的弱追踪
            if ((int)(Projectile.localAI[0] + phase * 3f) % 9 == 0)
                Projectile.velocity = Projectile.velocity.RotatedBy(Main.rand.NextFloat(-0.55f, 0.55f));

            NPC target = FindNearest(420f);
            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 10f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.07f);
            }
            if (Projectile.velocity.Length() < 5f)
                Projectile.velocity *= 1.05f;

            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ && Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldCoin,
                    Main.rand.NextVector2Circular(1f, 1f), 100, default, 0.7f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.25f, 0.22f, 0.1f);
        }

        private NPC FindNearest(float range) {
            NPC best = null;
            float bestDist = range;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy())
                    continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < bestDist) { bestDist = d; best = npc; }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.35f, Pitch = 0.5f }, target.Center);
            //窃金: 金屑飞溅
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.GoldCoin,
                    Main.rand.NextVector2Circular(3f, 3f), 80, default, 1f);
                d.noGravity = true;
            }
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, 0.6f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            //鼠身 = 短笔画拖尾 + 头点 + 细尾
            var pts = new System.Collections.Generic.List<Vector2>(8);
            Vector2 half = Projectile.Size * 0.5f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                pts.Add(Projectile.oldPos[i] + half);
            }
            if (pts.Count >= 2)
                CharmVFX.DrawStroke(pts.ToArray(), 7f, CharmVFX.Rat, 0.9f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.32f, CharmVFX.GetPalette(CharmVFX.Rat).Glow * 0.8f);
            return false;
        }
    }

    /// <summary>牛灵: 显形→刨地蓄势→贯穿冲撞→急刹消散。ai[0]=冲撞方向 (±1)。</summary>
    public class OxSpiritProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int MaterializeEnd = 14;
        private const int PawBackEnd = 36;
        private const int DashEnd = 60;
        private const int BrakeEnd = 68;

        private ref float Age => ref Projectile.localAI[0];
        private float Dir => Projectile.ai[0] >= 0f ? 1f : -1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.OxSpiritProj.DisplayName", () => "Ox Spirit");
        }

        public override void SetDefaults() {
            Projectile.width = 96;
            Projectile.height = 64;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = BrakeEnd + 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool? CanDamage() => Age >= PawBackEnd;

        public override void AI() {
            Age++;

            if (Age <= MaterializeEnd) {
                //显形: 静止, 尘土自地面聚起
                Projectile.velocity = Vector2.Zero;
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), 26f),
                        DustID.Dirt, new Vector2(0f, -Main.rand.NextFloat(1f, 2.5f)), 60, default, 1.2f);
                    d.noGravity = true;
                }
                if (Age == MaterializeEnd)
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.35f, Pitch = 0.55f }, Projectile.Center);
            }
            else if (Age <= PawBackEnd) {
                //刨地蓄势: 后撤 pow(t,3) — 沉默的反向吸气
                float t = (Age - MaterializeEnd) / (float)(PawBackEnd - MaterializeEnd);
                Projectile.velocity = new Vector2(-Dir * ACMUtils.QuadIn(t) * t * 4.2f, 0f);
                if (!Main.dedServ && (int)Age % 5 == 0) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(-Dir * 30f, 28f),
                        DustID.Dirt, new Vector2(-Dir * 3f, -1.6f), 40, default, 1.3f);
                    d.noGravity = false;
                }
            }
            else if (Age <= DashEnd) {
                //冲撞: 1 帧 set + 每帧递增 (爆发是 set 不是 ramp)
                if (Age == PawBackEnd + 1) {
                    Projectile.velocity = new Vector2(Dir * 34f, 0f);
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
                    WeaponVFX.AddScreenShake(Projectile.Center, 2f);
                }
                Projectile.velocity *= 1.02f;
                if (!Main.dedServ) {
                    for (int i = 0; i < 2; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(-Dir * 40f, Main.rand.NextFloat(10f, 30f)),
                            DustID.Dirt, new Vector2(-Dir * 4f, -1f), 60, default, 1.4f);
                        d.noGravity = true;
                    }
                }
            }
            else {
                //急刹: 撞进位置的顿挫
                Projectile.velocity *= 0.72f;
            }
            Lighting.AddLight(Projectile.Center, CharmVFX.GetPalette(CharmVFX.Ox).Glow.ToVector3() * 0.4f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.7f, Pitch = -0.15f }, target.Center);
            WeaponVFX.AddScreenShake(target.Center, 2.5f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Bronze, 1.4f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float fadeIn = MathHelper.Clamp(Age / MaterializeEnd, 0f, 1f);
            float fadeOut = Age > DashEnd ? 1f - (Age - DashEnd) / (float)(BrakeEnd - DashEnd) : 1f;
            float alpha = fadeIn * MathHelper.Clamp(fadeOut, 0f, 1f);
            Vector2 c = Projectile.Center;
            float dir = Dir;

            //蓄势期躯干微微下压 (重量)
            float crouch = Age > MaterializeEnd && Age <= PawBackEnd
                ? MathF.Sin((Age - MaterializeEnd) / (float)(PawBackEnd - MaterializeEnd) * MathHelper.Pi) * 7f : 0f;

            //躯干主笔 (一笔弓背): 尾→背拱→头
            Span<Vector2> body = stackalloc Vector2[4];
            body[0] = c + new Vector2(-dir * 46f, 12f + crouch);
            body[1] = c + new Vector2(-dir * 18f, -16f + crouch);
            body[2] = c + new Vector2(dir * 16f, -14f + crouch);
            body[3] = c + new Vector2(dir * 44f, 4f + crouch);
            CharmVFX.DrawStroke(body.ToArray(), 20f, CharmVFX.Ox, alpha);

            //双角笔 (头端两short arc)
            Span<Vector2> horn1 = stackalloc Vector2[3];
            horn1[0] = c + new Vector2(dir * 36f, -2f + crouch);
            horn1[1] = c + new Vector2(dir * 52f, -16f + crouch);
            horn1[2] = c + new Vector2(dir * 66f, -12f + crouch);
            CharmVFX.DrawStroke(horn1.ToArray(), 6f, CharmVFX.Ox, alpha);
            Span<Vector2> horn2 = stackalloc Vector2[3];
            horn2[0] = c + new Vector2(dir * 34f, 6f + crouch);
            horn2[1] = c + new Vector2(dir * 54f, 0f + crouch);
            horn2[2] = c + new Vector2(dir * 64f, 8f + crouch);
            CharmVFX.DrawStroke(horn2.ToArray(), 5f, CharmVFX.Ox, alpha * 0.8f);

            //冲刺期的速度拖影
            if (Age > PawBackEnd && Age <= DashEnd + 4) {
                var pts = new System.Collections.Generic.List<Vector2>(10);
                Vector2 half = Projectile.Size * 0.5f;
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero)
                        continue;
                    pts.Add(Projectile.oldPos[i] + half);
                }
                if (pts.Count >= 2)
                    CharmVFX.DrawStroke(pts.ToArray(), 26f, CharmVFX.Ox, alpha * 0.55f, uvScroll: Age * 0.03f);
            }

            WeaponVFX.DrawGlowBurst(c + new Vector2(dir * 44f, crouch), 0.55f * alpha,
                CharmVFX.GetPalette(CharmVFX.Ox).Glow * alpha);
            return false;
        }
    }

    /// <summary>兔灵: 瞬步三连击, 第三击捣月下砸 ×1.5。</summary>
    public class RabbitSpiritProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private enum State { Seek, Crouch, Strike, Rest, Fade }

        private State _state = State.Seek;
        private int _timer;
        private int _strikeIndex;             // 0/1/2
        private int _targetId = -1;
        private Vector2 _blinkFrom, _blinkTo;
        private int _blinkFlash;

        public override void SetStaticDefaults() {
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.RabbitSpiritProj.DisplayName", () => "Moon Rabbit Spirit");
        }

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool? CanDamage() => _state == State.Strike;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //第三击·捣月
            if (_strikeIndex >= 2)
                modifiers.FinalDamage *= 1.5f;
        }

        public override void AI() {
            _timer++;
            Projectile.velocity = Vector2.Zero;

            switch (_state) {
                case State.Seek: {
                    NPC target = FindTarget(500f);
                    if (target == null) {
                        //无人可击: 化月尘散去
                        _state = State.Fade;
                        _timer = 0;
                        break;
                    }
                    _targetId = target.whoAmI;
                    _state = State.Crouch;
                    _timer = 0;
                    break;
                }
                case State.Crouch: {
                    //蹲缩 6f (前摇)
                    if (_timer >= 6) {
                        NPC target = ValidTarget() ?? FindTarget(500f);
                        if (target == null) { _state = State.Fade; _timer = 0; break; }
                        _targetId = target.whoAmI;
                        //瞬步: 三击分别从侧/侧/上方切入
                        _blinkFrom = Projectile.Center;
                        Vector2 offset = _strikeIndex == 2
                            ? new Vector2(0f, -64f)
                            : new Vector2(Main.rand.NextBool() ? -44f : 44f, Main.rand.NextFloat(-18f, 6f));
                        _blinkTo = target.Center + offset;
                        Projectile.Center = _blinkTo;
                        Projectile.netUpdate = true; //瞬步位置广播, 各端不漂移
                        _blinkFlash = 8;
                        _state = State.Strike;
                        _timer = 0;
                        SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = 0.5f + _strikeIndex * 0.1f }, _blinkTo);
                    }
                    break;
                }
                case State.Strike: {
                    NPC target = ValidTarget();
                    //贴身打击窗口 4f: 吸附在目标上保证接触
                    if (target != null)
                        Projectile.Center = _strikeIndex == 2
                            ? Vector2.Lerp(Projectile.Center, target.Center, 0.5f)  // 捣月下砸
                            : target.Center + (Projectile.Center - target.Center).SafeNormalize(Vector2.UnitX) * 22f;
                    if (_timer >= 4) {
                        if (_strikeIndex >= 2) {
                            //三踏完成
                            if (target != null) {
                                WeaponVFX.AddScreenShake(Projectile.Center, 2.5f);
                                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.5f, Pitch = 0.35f }, Projectile.Center);
                                if (Projectile.owner == Main.myPlayer)
                                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                                        Vector2.Zero, ModContent.ProjectileType<CharmSealFX>(), 0, 0f,
                                        Projectile.owner, CharmVFX.Rabbit, 0.8f);
                            }
                            _state = State.Fade;
                            _timer = 0;
                        }
                        else {
                            _strikeIndex++;
                            _state = State.Rest;
                            _timer = 0;
                        }
                    }
                    break;
                }
                case State.Rest: {
                    //定格 5f (让眼睛跟上)
                    if (_timer >= 5) { _state = State.Crouch; _timer = 0; }
                    break;
                }
                case State.Fade: {
                    if (_timer == 1 && !Main.dedServ) {
                        for (int i = 0; i < 8; i++) {
                            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueTorch,
                                Main.rand.NextVector2Circular(2.5f, 2.5f), 120, default, 1.1f);
                            d.noGravity = true;
                        }
                    }
                    if (_timer >= 10)
                        Projectile.Kill();
                    break;
                }
            }

            if (_blinkFlash > 0)
                _blinkFlash--;
            Lighting.AddLight(Projectile.Center, CharmVFX.GetPalette(CharmVFX.Rabbit).Glow.ToVector3() * 0.35f);
        }

        private NPC ValidTarget() {
            if (_targetId < 0 || _targetId >= Main.maxNPCs)
                return null;
            NPC npc = Main.npc[_targetId];
            return npc.active && npc.CanBeChasedBy() ? npc : null;
        }

        private NPC FindTarget(float range) {
            NPC best = null;
            float bestDist = range;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy())
                    continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < bestDist) { bestDist = d; best = npc; }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gem, _strikeIndex >= 2 ? 1.3f : 0.7f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float alpha = _state == State.Fade ? 1f - _timer / 10f : 1f;
            Vector2 c = Projectile.Center;

            //瞬步残影线 (拉伸的月白光带)
            if (_blinkFlash > 0 && _blinkFrom != Vector2.Zero) {
                float streak = _blinkFlash / 8f;
                Span<Vector2> line = stackalloc Vector2[2];
                line[0] = _blinkFrom;
                line[1] = _blinkTo;
                CharmVFX.DrawStroke(line.ToArray(), 10f * streak, CharmVFX.Rabbit, streak * 0.9f);
            }

            //兔身: 蹲缩时压扁 (蓄), 打击时拉长 (发)
            float squash = _state == State.Crouch ? 1f - _timer / 6f * 0.35f : 1f;
            float stretch = _state == State.Strike ? 1.35f : 1f;
            Span<Vector2> body = stackalloc Vector2[3];
            body[0] = c + new Vector2(-16f * stretch, 10f * squash);
            body[1] = c + new Vector2(0f, -12f * squash);
            body[2] = c + new Vector2(16f * stretch, 8f * squash);
            CharmVFX.DrawStroke(body.ToArray(), 13f, CharmVFX.Rabbit, alpha);

            //长耳双笔
            Span<Vector2> ear = stackalloc Vector2[2];
            ear[0] = c + new Vector2(-4f, -10f * squash);
            ear[1] = c + new Vector2(-9f, -30f * squash);
            CharmVFX.DrawStroke(ear.ToArray(), 4.5f, CharmVFX.Rabbit, alpha * 0.9f);
            ear[0] = c + new Vector2(4f, -10f * squash);
            ear[1] = c + new Vector2(10f, -28f * squash);
            CharmVFX.DrawStroke(ear.ToArray(), 4.5f, CharmVFX.Rabbit, alpha * 0.9f);

            WeaponVFX.DrawGlowBurst(c, 0.4f * alpha, CharmVFX.GetPalette(CharmVFX.Rabbit).Glow * (0.7f * alpha));
            return false;
        }
    }

    /// <summary>蛇灵: 蜿蜒追踪 5 穿透; 对同一目标第三口 ×1.8 + 缠身剧毒减速。ai[0]=1 伏击 (首口×1.6)。</summary>
    public class SnakeSpiritProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float Age => ref Projectile.localAI[0];
        //localAI[1] = 上个目标 whoAmI+1, localAI[2] = 连续命中数
        private bool AmbushArmed => Projectile.ai[0] >= 1f && Projectile.ai[2] == 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.SnakeSpiritProj.DisplayName", () => "Serpent Spirit");
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            Age++;

            //蜿蜒: 追踪方向 + 正弦游动 (转向率上限保证可读)
            NPC target = FindNearest(560f);
            Vector2 desiredDir = target != null
                ? (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
                : Projectile.velocity.SafeNormalize(Vector2.UnitX);

            float currentRot = Projectile.velocity.ToRotation();
            float desiredRot = desiredDir.ToRotation() + MathF.Sin(Age * 0.28f) * 0.55f;
            float turn = MathHelper.WrapAngle(desiredRot - currentRot);
            turn = MathHelper.Clamp(turn, -0.06f, 0.06f);
            float speed = MathF.Min(13f, 9f + Age * 0.06f);
            Projectile.velocity = (currentRot + turn).ToRotationVector2() * speed;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ && Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                    -Projectile.velocity * 0.08f, 120, default, 0.9f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, CharmVFX.GetPalette(CharmVFX.Snake).Glow.ToVector3() * 0.3f);
        }

        private NPC FindNearest(float range) {
            NPC best = null;
            float bestDist = range;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy())
                    continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < bestDist) { bestDist = d; best = npc; }
            }
            return best;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (AmbushArmed)
                modifiers.FinalDamage *= 1.6f;
            //本口是对同一目标的第三口 → 缠身
            if (Projectile.localAI[1] == target.whoAmI + 1 && Projectile.localAI[2] == 2f)
                modifiers.FinalDamage *= 1.8f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (AmbushArmed) {
                Projectile.ai[2] = 1f; //伏击只吃一口
                Projectile.netUpdate = true;
            }

            if (Projectile.localAI[1] == target.whoAmI + 1)
                Projectile.localAI[2]++;
            else {
                Projectile.localAI[1] = target.whoAmI + 1;
                Projectile.localAI[2] = 1f;
            }

            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.1f }, target.Center);

            if (Projectile.localAI[2] >= 3f) {
                //缠身: 剧毒 + 减速 + 演出
                Projectile.localAI[2] = 0f;
                target.AddBuff(BuffID.Poisoned, 300);
                target.AddBuff(BuffID.Slow, 120);
                WeaponVFX.AddScreenShake(target.Center, 2f);
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Nature, 1.3f, Projectile.owner);
            }
            else {
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Nature, 0.6f, Projectile.owner);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            //蛇身 = 拖尾本体 (加正弦摆幅让身体真的在游)
            var pts = new System.Collections.Generic.List<Vector2>(16);
            Vector2 half = Projectile.Size * 0.5f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                float rot = Projectile.oldRot.Length > i ? Projectile.oldRot[i] : Projectile.rotation;
                Vector2 perp = rot.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                pts.Add(Projectile.oldPos[i] + half + perp * MathF.Sin(Age * 0.28f - i * 0.5f) * 5f);
            }
            if (pts.Count >= 2)
                CharmVFX.DrawStroke(pts.ToArray(), 9f, CharmVFX.Snake, 1f, uvScroll: -Age * 0.04f);
            //蛇首楔形亮点 (伏击时更亮)
            WeaponVFX.DrawGlowBurst(Projectile.Center, AmbushArmed ? 0.55f : 0.4f,
                CharmVFX.GetPalette(CharmVFX.Snake).Glow * 0.9f);
            return false;
        }
    }

    /// <summary>奔雷马灵: 延迟列队后直线贯穿 (直=快)。ai[0]=延迟帧, ai[1]=车道 (仅中道出声)。</summary>
    public class HorseSpiritProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float RunTime => ref Projectile.localAI[0];
        private bool Launched => Projectile.localAI[1] > 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.HorseSpiritProj.DisplayName", () => "Stampede Spirit");
        }

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesIDStaticNPCImmunity = true; //九骑共享冷却: 同波三骑不叠爆, 三波各命中一次
            Projectile.idStaticNPCHitCooldown = 16;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool? CanDamage() => Launched;

        public override void AI() {
            if (!Launched) {
                //列队待发 (隐形; 保留微速度承载方向, 位移可忽略)
                if (Projectile.ai[0] > 0f) {
                    Projectile.ai[0]--;
                    return;
                }
                Projectile.localAI[1] = 1f;
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 30f;
                if (Projectile.ai[1] == 0f) {
                    //每波仅中道出声, 免得九骑齐鸣
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.22f, Pitch = 0.6f }, Projectile.Center);
                }
            }

            RunTime++;
            if (RunTime > 50f) {
                Projectile.Kill();
                return;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(0f, 18f)
                    + Main.rand.NextVector2Circular(14f, 6f), DustID.Torch,
                    -Projectile.velocity * 0.06f, 80, default, 1.2f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, CharmVFX.GetPalette(CharmVFX.Horse).Glow.ToVector3() * 0.35f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Crimson, 0.8f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ || !Launched)
                return false;

            float alpha = MathHelper.Clamp(RunTime / 5f, 0f, 1f) * MathHelper.Clamp((50f - RunTime) / 8f, 0f, 1f);
            Vector2 c = Projectile.Center;
            float dir = MathF.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X);

            //奔驰拖影 (速度即形体)
            var pts = new System.Collections.Generic.List<Vector2>(12);
            Vector2 half = Projectile.Size * 0.5f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                pts.Add(Projectile.oldPos[i] + half);
            }
            if (pts.Count >= 2)
                CharmVFX.DrawStroke(pts.ToArray(), 16f, CharmVFX.Horse, alpha * 0.8f, uvScroll: RunTime * 0.05f);

            //马首扬颈笔 + 鬃焰笔
            Span<Vector2> neck = stackalloc Vector2[3];
            neck[0] = c + new Vector2(dir * 8f, -4f);
            neck[1] = c + new Vector2(dir * 26f, -16f);
            neck[2] = c + new Vector2(dir * 40f, -10f);
            CharmVFX.DrawStroke(neck.ToArray(), 9f, CharmVFX.Horse, alpha);
            Span<Vector2> mane = stackalloc Vector2[3];
            mane[0] = c + new Vector2(-dir * 4f, -12f);
            mane[1] = c + new Vector2(dir * 10f, -24f + MathF.Sin(RunTime * 0.5f) * 3f);
            mane[2] = c + new Vector2(dir * 24f, -30f + MathF.Sin(RunTime * 0.5f + 1f) * 3f);
            CharmVFX.DrawStroke(mane.ToArray(), 6f, CharmVFX.Horse, alpha * 0.85f, uvScroll: RunTime * 0.08f);

            WeaponVFX.DrawGlowBurst(c + new Vector2(dir * 36f, -8f), 0.4f * alpha,
                CharmVFX.GetPalette(CharmVFX.Horse).Glow * alpha);
            return false;
        }
    }

    /// <summary>金鸡灵: 昂首蓄势后三声啼鸣, 每声扩散一圈破晓冲击环 (环带判定)。</summary>
    public class RoosterSpiritProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int WindupEnd = 14;
        private const int CrowInterval = 16;
        private const int RingLife = 15;
        private const float RingBand = 28f;

        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.RoosterSpiritProj.DisplayName", () => "Dawn Rooster Spirit");
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = WindupEnd + CrowInterval * 3 + 12;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = CrowInterval; //每圈可对同一敌人各结算一次
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        private static float MaxRingRadius(int i) => 90f + 60f * i;

        /// <summary>第 i 圈当前半径 (未启动返回 -1)。</summary>
        private float RingRadius(int i) {
            float start = WindupEnd + CrowInterval * i;
            float t = Age - start;
            if (t < 0f || t > RingLife)
                return -1f;
            return MaxRingRadius(i) * ACMUtils.QuadOut(t / RingLife);
        }

        public override void AI() {
            Age++;

            //三声啼鸣, 音高逐声上行 (听觉读条)
            for (int i = 0; i < 3; i++) {
                if ((int)Age == WindupEnd + CrowInterval * i) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.75f, Pitch = -0.05f + i * 0.18f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.3f + i * 0.15f }, Projectile.Center);
                    WeaponVFX.AddScreenShake(Projectile.Center, 1.5f + i * 0.5f);
                }
            }

            if (!Main.dedServ && Age < WindupEnd && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.GoldFlame, new Vector2(0f, -1.2f), 100, default, 1f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, CharmVFX.GetPalette(CharmVFX.Rooster).Glow.ToVector3()
                * (0.3f + 0.4f * MathHelper.Clamp(Age / WindupEnd, 0f, 1f)));
        }

        public override bool? CanDamage() => Age > WindupEnd;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float targetReach = MathF.Max(targetHitbox.Width, targetHitbox.Height) * 0.5f;
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            for (int i = 0; i < 3; i++) {
                float r = RingRadius(i);
                if (r > 0f && MathF.Abs(dist - r) <= RingBand + targetReach)
                    return true;
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, 0.9f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            var pal = CharmVFX.GetPalette(CharmVFX.Rooster);
            float windup = MathHelper.Clamp(Age / WindupEnd, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
            float alpha = windup * fadeOut;
            Vector2 c = Projectile.Center;

            //破晓冲击环 (伤害与视觉严格对齐)
            for (int i = 0; i < 3; i++) {
                float r = RingRadius(i);
                if (r > 0f) {
                    float ringT = r / MaxRingRadius(i);
                    WeaponVFX.DrawShockwaveRing(c, r, RingBand * 0.7f, (1f - ringT * 0.7f) * 0.9f,
                        pal.Glow, new Color(226, 92, 42));
                }
            }

            //鸡灵: 昂首弧线随蓄势抬升
            float lift = windup * 10f + (Age > WindupEnd ? MathF.Sin((Age - WindupEnd) * 0.5f) * 2f : 0f);
            Span<Vector2> body = stackalloc Vector2[4];
            body[0] = c + new Vector2(-22f, 12f);
            body[1] = c + new Vector2(-6f, 2f - lift * 0.3f);
            body[2] = c + new Vector2(8f, -8f - lift * 0.7f);
            body[3] = c + new Vector2(16f, -22f - lift);
            CharmVFX.DrawStroke(body.ToArray(), 12f, CharmVFX.Rooster, alpha);

            //尾羽三笔 (stackalloc 提出循环, 避免 CA2014 栈累积)
            Span<Vector2> plume = stackalloc Vector2[3];
            for (int i = 0; i < 3; i++) {
                float spread = (i - 1) * 0.4f;
                plume[0] = c + new Vector2(-20f, 10f);
                plume[1] = c + new Vector2(-34f, -2f + spread * 14f);
                plume[2] = c + new Vector2(-46f, -8f + spread * 22f);
                CharmVFX.DrawStroke(plume.ToArray(), 5f, CharmVFX.Rooster, alpha * 0.8f);
            }

            //鸡冠朱笔 (用系列朱砂色的小段)
            Span<Vector2> comb = stackalloc Vector2[3];
            comb[0] = c + new Vector2(12f, -24f - lift);
            comb[1] = c + new Vector2(16f, -32f - lift);
            comb[2] = c + new Vector2(21f, -26f - lift);
            Color combOuter = CharmVFX.InkVermilion; combOuter.A = 170;
            Color combInner = new Color(255, 120, 90); combInner.A = 210;
            WeaponVFX.DrawRibbonTrail(comb.ToArray(), 4f, combOuter, combInner);

            WeaponVFX.DrawGlowBurst(c + new Vector2(16f, -22f - lift), 0.45f * alpha, pal.Glow * alpha);
            return false;
        }
    }

    /// <summary>犬灵: 扑咬最近敌人并咬住不放, 每 24f 一口, 末口撕咬 ×1.4。ai[1]=目标 whoAmI。</summary>
    public class DogSpiritProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int LatchTime = 168;
        private const int PounceLimit = 45;

        private ref float StateTimer => ref Projectile.localAI[0];
        //localAI[1] = 已咬口数
        private bool Latched => Projectile.ai[0] >= 1f;
        private int TargetId => (int)Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.DogSpiritProj.DisplayName", () => "Hound Spirit");
        }

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = PounceLimit + LatchTime + 30;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24; //咬口节拍
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void OnSpawn(IEntitySource source) {
            if (Projectile.owner != Main.myPlayer)
                return;
            //选targets: 最近的、未被我方其他犬占用的敌人
            int picked = -1;
            float bestDist = 620f;
            int dogType = Type;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy())
                    continue;
                bool taken = false;
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.owner == Projectile.owner && p.type == dogType && p.whoAmI != Projectile.whoAmI
                        && (int)p.ai[1] == npc.whoAmI) {
                        taken = true;
                        break;
                    }
                }
                if (taken)
                    continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < bestDist) { bestDist = d; picked = npc.whoAmI; }
            }
            Projectile.ai[1] = picked;
            Projectile.netUpdate = true;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //末口撕咬
            if (Projectile.localAI[1] >= 6f)
                modifiers.FinalDamage *= 1.4f;
        }

        public override void AI() {
            NPC target = TargetId >= 0 && TargetId < Main.maxNPCs ? Main.npc[TargetId] : null;
            bool valid = target != null && target.active && target.CanBeChasedBy();

            if (!valid) {
                //目标失效: 就近重锁或散形
                if (Projectile.owner == Main.myPlayer) {
                    int next = -1;
                    float bestDist = 400f;
                    foreach (NPC npc in Main.ActiveNPCs) {
                        if (!npc.CanBeChasedBy())
                            continue;
                        float d = Vector2.Distance(Projectile.Center, npc.Center);
                        if (d < bestDist) { bestDist = d; next = npc.whoAmI; }
                    }
                    if (next >= 0) {
                        Projectile.ai[1] = next;
                        Projectile.ai[0] = 0f; //重新扑击
                        StateTimer = 0f;
                        Projectile.netUpdate = true;
                    }
                    else {
                        FadeAway();
                    }
                }
                return;
            }

            StateTimer++;

            if (!Latched) {
                //扑击: 强追踪加速
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
                    * MathF.Min(24f, 12f + StateTimer * 0.5f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.18f);
                Projectile.rotation = Projectile.velocity.ToRotation();

                if (Vector2.Distance(Projectile.Center, target.Center) < 42f) {
                    Projectile.ai[0] = 1f; //咬住
                    StateTimer = 0f;
                    Projectile.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = -0.3f }, target.Center);
                }
                else if (StateTimer > PounceLimit) {
                    FadeAway();
                }
            }
            else {
                //咬定: 吸附目标 + 撕咬抖动 (接触伤害按 localNPCHitCooldown 自动成拍)
                float shakeAmp = 4f + MathF.Sin(StateTimer * 0.9f) * 3f;
                Vector2 offset = (StateTimer * 0.9f).ToRotationVector2() * shakeAmp;
                Projectile.Center = target.Center + offset;
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation = offset.ToRotation() + MathHelper.Pi;

                if (StateTimer >= LatchTime)
                    FadeAway();
            }
            Lighting.AddLight(Projectile.Center, CharmVFX.GetPalette(CharmVFX.Dog).Glow.ToVector3() * 0.3f);
        }

        private void FadeAway() {
            if (!Main.dedServ) {
                for (int i = 0; i < 6; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueTorch,
                        Main.rand.NextVector2Circular(2f, 2f), 130, default, 1f);
                    d.noGravity = true;
                }
            }
            Projectile.Kill();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.localAI[1]++;
            bool finisher = Projectile.localAI[1] >= 7f;
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = finisher ? 0.7f : 0.45f, Pitch = -0.3f + Projectile.localAI[1] * 0.04f }, target.Center);
            if (finisher)
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.3f, Pitch = 0.5f }, target.Center);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Shadow, finisher ? 1.4f : 0.6f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Vector2 c = Projectile.Center;
            float dir = MathF.Sign(MathF.Cos(Projectile.rotation)) >= 0 ? 1f : -1f;
            float alpha = 1f;

            //扑击拖影
            if (!Latched) {
                var pts = new System.Collections.Generic.List<Vector2>(8);
                Vector2 half = Projectile.Size * 0.5f;
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero)
                        continue;
                    pts.Add(Projectile.oldPos[i] + half);
                }
                if (pts.Count >= 2)
                    CharmVFX.DrawStroke(pts.ToArray(), 12f, CharmVFX.Dog, 0.7f);
            }

            //犬身弓背笔
            float lunge = Latched ? MathF.Sin(StateTimer * 0.9f) * 4f : 0f;
            Span<Vector2> body = stackalloc Vector2[4];
            body[0] = c + new Vector2(-dir * 26f, 8f);
            body[1] = c + new Vector2(-dir * 8f, -8f + lunge * 0.5f);
            body[2] = c + new Vector2(dir * 8f, -6f + lunge * 0.5f);
            body[3] = c + new Vector2(dir * 24f, 2f + lunge);
            CharmVFX.DrawStroke(body.ToArray(), 11f, CharmVFX.Dog, alpha);

            //上下颚双笔 (咬合随节拍开合)
            float jaw = Latched ? (MathF.Sin(StateTimer * 0.26f) * 0.5f + 0.5f) * 8f : 5f;
            Span<Vector2> jawU = stackalloc Vector2[2];
            jawU[0] = c + new Vector2(dir * 22f, 0f + lunge);
            jawU[1] = c + new Vector2(dir * 38f, -jaw + lunge);
            CharmVFX.DrawStroke(jawU.ToArray(), 5f, CharmVFX.Dog, alpha * 0.9f);
            Span<Vector2> jawL = stackalloc Vector2[2];
            jawL[0] = c + new Vector2(dir * 22f, 4f + lunge);
            jawL[1] = c + new Vector2(dir * 36f, jaw + lunge);
            CharmVFX.DrawStroke(jawL.ToArray(), 5f, CharmVFX.Dog, alpha * 0.9f);

            WeaponVFX.DrawGlowBurst(c + new Vector2(dir * 30f, lunge), 0.4f,
                CharmVFX.GetPalette(CharmVFX.Dog).Glow * 0.85f);
            return false;
        }
    }
}
