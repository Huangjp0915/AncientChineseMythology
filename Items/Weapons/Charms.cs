using AncientChineseMythology.Buffs;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Waapons
{
    public class ChickenCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Charms/ChickenCharm";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.accessory = true;
            Item.value = Item.buyPrice(gold: 100);
            Item.rare = ItemRarityID.Red;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // 赋予无限飞行时间
            player.wingTime = int.MaxValue;
            // 防止坠落伤害
            player.noFallDmg = true;
            
            // 控制垂直运动
            if (player.controlUp || player.controlJump)
            {
                // 向上飞行：提升速度到 -12f
                player.velocity.Y = -12f;
            }
            else if (player.controlDown)
            {
                // 向下飞行：提升速度到 12f
                player.velocity.Y = 12f;
                // 模拟平台下穿：尝试将玩家位置向下移动 4 像素，但仅在不会碰撞到实心方块的情况下
                Vector2 newPos = player.position + new Vector2(0, 4f);
                if (!Collision.SolidCollision(newPos, player.width, player.height))
                {
                    player.position = newPos;
                }
            }
            else
            {
                // 悬浮时保持垂直速度 0
                player.velocity.Y = 0f;
            }

            // 持续添加鸡符咒专属 Buff（持续2 tick，每帧刷新）
            player.AddBuff(ModContent.BuffType<ChickenCharmBuff>(), 2);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacChicken>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        // 重写该方法以调整物品在世界中的绘制大小
        public override bool PreDrawInWorld(
            SpriteBatch spriteBatch,
            Color lightColor,
            Color alphaColor,
            ref float rotation,
            ref float scale,
            int whoAmI)
        {
            // 让物品在地上时，绘制更小
            float customScale = 0.5f;

            // 如果你想手动绘制，可以这样做：
            Texture2D texture = TextureAssets.Item[Item.type].Value;

            // 以物品中心为基准进行绘制
            Vector2 drawPosition = Item.Center - Main.screenPosition;

            // 如果需要让它贴得更紧一点，可以手动往下移动
            // 例如：drawPosition.Y += 2f;

            Vector2 origin = texture.Size() * 0.5f;

            // 手动绘制
            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                lightColor,
                rotation,
                origin,
                customScale,
                SpriteEffects.None,
                0f
            );

            // 返回 false 表示“我已经手动完成了绘制，不再用默认逻辑绘制”
            return false;
        }
    }

    public class CowCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Charms/CowCharm";

        public override void SetStaticDefaults()
        {
        }
        
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.accessory = true;
            Item.value = Item.buyPrice(gold: 100);
            Item.rare = ItemRarityID.Red;
        }
        
        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // 增加防御力
            player.statDefense += 40;
            // 增加所有伤害80%
            player.GetDamage(DamageClass.Generic) += 0.8f;

            player.AddBuff(ModContent.BuffType<CowCharmBuff>(), 2);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacCow>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        // 重写该方法以调整物品在世界中的绘制大小
        public override bool PreDrawInWorld(
            SpriteBatch spriteBatch,
            Color lightColor,
            Color alphaColor,
            ref float rotation,
            ref float scale,
            int whoAmI)
        {
            // 让物品在地上时，绘制更小
            float customScale = 0.5f;

            // 如果你想手动绘制，可以这样做：
            Texture2D texture = TextureAssets.Item[Item.type].Value;

            // 以物品中心为基准进行绘制
            Vector2 drawPosition = Item.Center - Main.screenPosition;

            // 如果需要让它贴得更紧一点，可以手动往下移动
            // 例如：drawPosition.Y += 2f;

            Vector2 origin = texture.Size() * 0.5f;

            // 手动绘制
            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                lightColor,
                rotation,
                origin,
                customScale,
                SpriteEffects.None,
                0f
            );

            // 返回 false 表示“我已经手动完成了绘制，不再用默认逻辑绘制”
            return false;
        }
    }

    public class DogCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Charms/DogCharm";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.accessory = true;
            Item.value = Item.buyPrice(gold: 100);
            // 设置为最高稀有度（红色）
            Item.rare = ItemRarityID.Red;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // Terraria 的生命回复计算方式：lifeRegen 每秒回复的生命为 lifeRegen/2
            // 因此这里增加 100，即每秒回复 50 点生命
            player.lifeRegen += 100;
            
            // 增加魔力回复效果（具体数值可根据测试进行调整）
            player.manaRegenBonus += 50;

            // 刷新 DogCharmBuff（持续2帧刷新，使其不会消失）
            player.AddBuff(ModContent.BuffType<DogCharmBuff>(), 2);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacDog>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        // 重写该方法以调整物品在世界中的绘制大小
        public override bool PreDrawInWorld(
            SpriteBatch spriteBatch,
            Color lightColor,
            Color alphaColor,
            ref float rotation,
            ref float scale,
            int whoAmI)
        {
            // 让物品在地上时，绘制更小
            float customScale = 0.5f;

            // 如果你想手动绘制，可以这样做：
            Texture2D texture = TextureAssets.Item[Item.type].Value;

            // 以物品中心为基准进行绘制
            Vector2 drawPosition = Item.Center - Main.screenPosition;

            // 如果需要让它贴得更紧一点，可以手动往下移动
            // 例如：drawPosition.Y += 2f;

            Vector2 origin = texture.Size() * 0.5f;

            // 手动绘制
            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                lightColor,
                rotation,
                origin,
                customScale,
                SpriteEffects.None,
                0f
            );

            // 返回 false 表示“我已经手动完成了绘制，不再用默认逻辑绘制”
            return false;
        }
    }

    public class DragonCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Charms/DragonCharm";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 30;
            Item.scale = 0.4f;
            // 此武器采用举起使用的方式，可根据需要更换 UseStyle
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.damage = 300; // 可根据需要调整伤害
            Item.knockBack = 6f;
            Item.value = Item.buyPrice(gold: 100);
            Item.rare = ItemRarityID.Red;
            // 发射激光弹
            Item.shoot = ModContent.ProjectileType<DragonCharmLaser>();
            Item.shootSpeed = 16f;
            Item.noMelee = true;
            Item.DamageType = DamageClass.Ranged;
        }

        // 每次使用武器时扣除玩家30点生命值
       public override bool? UseItem(Player player)
        {
            int damage = 30; // 固定扣除的生命值
            player.statLife -= damage;
            // 显示红色的伤害文字
            CombatText.NewText(player.Hitbox, Microsoft.Xna.Framework.Color.Red, damage, true);
            // 如果血量扣除后小于等于0，则触发死亡
            if (player.statLife <= 0)
            {
                player.KillMe(PlayerDeathReason.ByCustomReason($"{player.name} 被龙符咒榨干了..."), damage, 0);
            }
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacDragon>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        // 重写该方法以调整物品在世界中的绘制大小
        public override bool PreDrawInWorld(
            SpriteBatch spriteBatch,
            Color lightColor,
            Color alphaColor,
            ref float rotation,
            ref float scale,
            int whoAmI)
        {
            // 让物品在地上时，绘制更小
            float customScale = 0.5f;

            // 如果你想手动绘制，可以这样做：
            Texture2D texture = TextureAssets.Item[Item.type].Value;

            // 以物品中心为基准进行绘制
            Vector2 drawPosition = Item.Center - Main.screenPosition;

            // 如果需要让它贴得更紧一点，可以手动往下移动
            // 例如：drawPosition.Y += 2f;

            Vector2 origin = texture.Size() * 0.5f;

            // 手动绘制
            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                lightColor,
                rotation,
                origin,
                customScale,
                SpriteEffects.None,
                0f
            );

            // 返回 false 表示“我已经手动完成了绘制，不再用默认逻辑绘制”
            return false;
        }
    }

    public class HorseCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Charms/HorseCharm";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.accessory = true;
            Item.value = Item.buyPrice(gold: 100);
            Item.rare = ItemRarityID.Red;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // 移除玩家当前所有的 debuff
            for (int i = player.buffType.Length - 1; i >= 0; i--)
            {
                int buffID = player.buffType[i];
                if (buffID > 0 && Main.debuff[buffID])
                {
                    player.DelBuff(i);
                }
            }
            
            // 设置所有 debuff 类型的免疫标记为 true
            // buffImmune 数组的长度通常覆盖了所有可能的 buff
            for (int i = 0; i < player.buffImmune.Length; i++)
            {
                if (Main.debuff[i])
                {
                    player.buffImmune[i] = true;
                }
            }

            player.AddBuff(ModContent.BuffType<HorseCharmBuff>(), 2);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacHorse>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        // 重写该方法以调整物品在世界中的绘制大小
        public override bool PreDrawInWorld(
            SpriteBatch spriteBatch,
            Color lightColor,
            Color alphaColor,
            ref float rotation,
            ref float scale,
            int whoAmI)
        {
            // 让物品在地上时，绘制更小
            float customScale = 0.5f;

            // 如果你想手动绘制，可以这样做：
            Texture2D texture = TextureAssets.Item[Item.type].Value;

            // 以物品中心为基准进行绘制
            Vector2 drawPosition = Item.Center - Main.screenPosition;

            // 如果需要让它贴得更紧一点，可以手动往下移动
            // 例如：drawPosition.Y += 2f;

            Vector2 origin = texture.Size() * 0.5f;

            // 手动绘制
            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                lightColor,
                rotation,
                origin,
                customScale,
                SpriteEffects.None,
                0f
            );

            // 返回 false 表示“我已经手动完成了绘制，不再用默认逻辑绘制”
            return false;
        }
    }

    public class PigCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Charms/PigCharm";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 30;
            // 使用举起的使用方式
            Item.useStyle = ItemUseStyleID.HoldUp;
            // 这里的 useTime 和 useAnimation 设置为 1，实际效果由 channel 控制
            Item.useTime = 1;
            Item.useAnimation = 1;
            Item.channel = true; // 支持持续使用（长按）
            Item.noMelee = true;
            Item.value = Item.buyPrice(gold: 100);
            Item.rare = ItemRarityID.Red;
            // 不直接设 shoot，采用 HoldItem 来判断是否已生成激光
            Item.DamageType = DamageClass.Magic;
            Item.damage = 168;    // 根据需要调整伤害
            Item.knockBack = 2f;
            // 本物品本身不消耗魔力，魔力消耗在激光内控制
        }

        public override void HoldItem(Player player)
        {
            // 如果玩家在按住左键，并且还没有生成该激光，则生成
            if (player.channel)
            {
                if (player.ownedProjectileCounts[ModContent.ProjectileType<Projectiles.PigCharmLaser>()] <= 0)
                {
                    Projectile.NewProjectile(
                        player.GetSource_ItemUse(Item),
                        player.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<Projectiles.PigCharmLaser>(),
                        Item.damage,
                        Item.knockBack,
                        player.whoAmI
                    );
                }
            }
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacPig>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        // 重写该方法以调整物品在世界中的绘制大小
        public override bool PreDrawInWorld(
            SpriteBatch spriteBatch,
            Color lightColor,
            Color alphaColor,
            ref float rotation,
            ref float scale,
            int whoAmI)
        {
            // 让物品在地上时，绘制更小
            float customScale = 0.5f;

            // 如果你想手动绘制，可以这样做：
            Texture2D texture = TextureAssets.Item[Item.type].Value;

            // 以物品中心为基准进行绘制
            Vector2 drawPosition = Item.Center - Main.screenPosition;

            // 如果需要让它贴得更紧一点，可以手动往下移动
            // 例如：drawPosition.Y += 2f;

            Vector2 origin = texture.Size() * 0.5f;

            // 手动绘制
            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                lightColor,
                rotation,
                origin,
                customScale,
                SpriteEffects.None,
                0f
            );

            // 返回 false 表示“我已经手动完成了绘制，不再用默认逻辑绘制”
            return false;
        }
    }

    public class RabbitCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Charms/RabbitCharm";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.accessory = true;
            Item.value = Item.buyPrice(gold: 100);
            Item.rare = ItemRarityID.Red;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // 将最高奔跑速度设为 15
            player.maxRunSpeed = 15f;
            // 提高加速速度，让玩家能更快达到最高速度
            player.runAcceleration += 10f;
            // 增加移动速度倍率（此处增加30%的额外移动速度）
            player.moveSpeed += 0.3f;

            // 当玩家没有按左右方向键时，立即将水平速度归零
            if (!player.controlLeft && !player.controlRight)
            {
                player.velocity.X = 0f;
            }

            player.AddBuff(ModContent.BuffType<RabbitCharmBuff>(), 2);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacRabbit>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        // 重写该方法以调整物品在世界中的绘制大小
        public override bool PreDrawInWorld(
            SpriteBatch spriteBatch,
            Color lightColor,
            Color alphaColor,
            ref float rotation,
            ref float scale,
            int whoAmI)
        {
            // 让物品在地上时，绘制更小
            float customScale = 0.5f;

            // 如果你想手动绘制，可以这样做：
            Texture2D texture = TextureAssets.Item[Item.type].Value;

            // 以物品中心为基准进行绘制
            Vector2 drawPosition = Item.Center - Main.screenPosition;

            // 如果需要让它贴得更紧一点，可以手动往下移动
            // 例如：drawPosition.Y += 2f;

            Vector2 origin = texture.Size() * 0.5f;

            // 手动绘制
            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                lightColor,
                rotation,
                origin,
                customScale,
                SpriteEffects.None,
                0f
            );

            // 返回 false 表示“我已经手动完成了绘制，不再用默认逻辑绘制”
            return false;
        }
    }

    public class SnakeCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Charms/SnakeCharm";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.scale = 0.4f;
            // 此物品作为使用类物品（类似药剂或武器）
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.consumable = false;
            Item.rare = ItemRarityID.Red;
            // 允许右键使用
            Item.autoReuse = false;
            Item.value = Item.buyPrice(gold: 100);
        }

        // 允许 alt 功能（右键使用）
        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        // 根据按键使用效果不同
        public override bool? UseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                // 右键使用：解除隐身（清除对应 Buff）
                player.ClearBuff(ModContent.BuffType<SnakeInvisibilityBuff>());
            }
            else
            {
                // 左键使用：赋予无限隐身，使用 int.MaxValue 作为时长
                player.AddBuff(ModContent.BuffType<SnakeInvisibilityBuff>(), int.MaxValue);
            }
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacSnake>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        // 重写该方法以调整物品在世界中的绘制大小
        public override bool PreDrawInWorld(
            SpriteBatch spriteBatch,
            Color lightColor,
            Color alphaColor,
            ref float rotation,
            ref float scale,
            int whoAmI)
        {
            // 让物品在地上时，绘制更小
            float customScale = 0.5f;

            // 如果你想手动绘制，可以这样做：
            Texture2D texture = TextureAssets.Item[Item.type].Value;

            // 以物品中心为基准进行绘制
            Vector2 drawPosition = Item.Center - Main.screenPosition;

            // 如果需要让它贴得更紧一点，可以手动往下移动
            // 例如：drawPosition.Y += 2f;

            Vector2 origin = texture.Size() * 0.5f;

            // 手动绘制
            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                lightColor,
                rotation,
                origin,
                customScale,
                SpriteEffects.None,
                0f
            );

            // 返回 false 表示“我已经手动完成了绘制，不再用默认逻辑绘制”
            return false;
        }
    }

    public class RatCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Charms/RatCharm";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(gold: 100);
        }

        public override bool AltFunctionUse(Player player)
            => player.inventory[player.selectedItem].type == Item.type;

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                if (player.inventory[player.selectedItem].type != Item.type)
                    return false;
            }
            return base.CanUseItem(player);
        }

        public override bool ConsumeItem(Player player)
            => player.inventory[player.selectedItem].type == Item.type;

        public override bool? UseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                // 这里写你的召唤逻辑，比如在 UseItem 中调用系统接口
                // 示例：ModContent.GetInstance<你的系统>().DoSomething();
                return true;
            }
            return base.UseItem(player);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 1)
                .AddIngredient(ModContent.ItemType<ZodiacRat>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        // 保留你原来的世界绘制代码
        public override bool PreDrawInWorld(
            SpriteBatch spriteBatch,
            Color lightColor,
            Color alphaColor,
            ref float rotation,
            ref float scale,
            int whoAmI)
        {
            float customScale = 0.5f;
            var texture = TextureAssets.Item[Item.type].Value;
            Vector2 drawPosition = Item.Center - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;
            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                lightColor,
                rotation,
                origin,
                customScale,
                SpriteEffects.None,
                0f
            );
            return false;
        }
	}

}