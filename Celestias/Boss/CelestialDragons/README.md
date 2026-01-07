# 天庭巡卫金龙 Boss 实现

## 概述
天庭巡卫金龙是一个月球领主后的蠕虫类Boss，整体散发金色光芒，拥有多种攻击模式和华丽的特效。
继承自`BasicWorm`类以使用正确的蠕虫跟随系统。

## 贴图规范

### 贴图朝向
- **所有贴图向右为正方向**（龙头朝右）
- 当速度向右时不需要翻转
- 当速度向左时使用垂直翻转（FlipVertically）

### 贴图尺寸
| 部位 | 宽度 | 高度 |
|------|------|------|
| 头部 | 382px | 256px |
| 体节 | 152px | 92px |
| 尾部 | 412px | 124px |

### 体节对齐
- **第一个体节从龙头贴图的一半开始**
- **体节之间相互覆盖约40%宽度**
- **每个体节被前一节牵引**（不是插值跟随）
- **共50个体节**

## Boss特性

### 基础属性
- **生命值**: 500,000
- **防御力**: 80
- **伤害**: 180
- **体节数量**: 50节
- **击退抗性**: 100%

### 运动特性
- **头部始终朝向速度方向**
- **体节被头部牵引**，不会抽搐
- **大范围巡航**：从屏幕一侧穿越到另一侧
- **大起大落的运动轨迹**

### 攻击模式

#### 1. 大范围巡空（ai[0] == 0）
- 水平范围：玩家左右各1200-1600像素
- 垂直范围：大幅度正弦波动（600-800像素）
- 高速巡航：22-34像素/帧
- 发射辐射弹幕（环形散射）
- 路径预警：显示龙即将穿越的路径

#### 2. 俯冲穿越（ai[0] == 1）
- 飞到玩家一侧高处（1000像素外，700像素高）
- 发出路径预警
- 高速俯冲穿过玩家位置（35-50像素/帧）
- 俯冲时两侧喷射剑气

#### 3. 剑气喷吐（ai[0] == 2）
- 大范围水平8字形移动（800像素范围）
- 边移动边向玩家喷吐扇形剑气
- 3-6道剑气（根据阶段）

#### 4. 大圆环绕（ai[0] == 3）
- 围绕玩家画大圆（半径550-700像素）
- 椭圆轨迹
- 持续发射辐射能量弹

#### 5. 全屏攻击（ai[0] == 4，生命值<50%）
- 继续大范围移动（不停下）
- 环形闪电向心收缩（12-18道，带路径预警）
- 持续天降金剑

### 阶段机制

| 生命值范围 | 阶段 | 特性 |
|-----------|------|------|
| 100%-75%  | 阶段1 | 基础攻击，攻击间隔600帧 |
| 75%-50%   | 阶段2 | 速度+，弹幕+，间隔500帧 |
| 50%-25%   | 阶段3 | 解锁全屏攻击，间隔400帧 |
| 25%-0%    | 阶段4 | 最强攻击，间隔300帧 |

## 弹幕类型

| 弹幕 | 功能 |
|------|------|
| CelestialPathWarning | 路径预警，显示弹幕将要划过的范围 |
| CelestialLightningWarning | 竖直闪电预警 |
| CelestialLightning | 金色闪电 |
| GoldenSwordAura | 金色剑气 |
| GoldenEnergy | 辐射能量弹 |
| FallingSword | 下落金剑 |

## 技术实现

### 体节跟随算法
```csharp
// 直接定位到父节点后方，不使用插值（避免抽搐）
Vector2 directionFromParent = (NPC.Center - FatherNPC.Center).SafeNormalize(Vector2.UnitX);
NPC.Center = FatherNPC.Center + directionFromParent * targetDistance;
NPC.rotation = (FatherNPC.Center - NPC.Center).ToRotation();
```

### 贴图绘制
```csharp
// 贴图向右为正方向，原点在右边中心（头部前端）
Vector2 origin = new Vector2(texture.Width, texture.Height / 2f);
// 向左时垂直翻转
SpriteEffects effects = NPC.velocity.X < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
```

### 路径预警
```csharp
// 预警显示从当前位置到目标位置的路径
Projectile.NewProjectile(..., ModContent.ProjectileType<CelestialPathWarning>(), 
    0, 0f, Main.myPlayer, targetX, targetY);
```
