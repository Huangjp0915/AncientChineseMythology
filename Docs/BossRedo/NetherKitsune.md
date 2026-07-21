# NetherKitsune（幽冥妖狐）重做设计文档 — Boss 重做工程 V3

> 单元范围：`Underworlds/Boss/NetherKitsunes/`（NetherKitsune.cs / NetherKitsuneTail.cs / NetherKitsuneProjectiles.cs / NetherKitsuneFogSystem.cs / Items/NetherKyuubiBook.cs）
> 新建着色器：`Effects/NetherKitsuneMist.fx`、`Effects/NetherKitsuneEye.fx`、`Effects/NetherKitsuneSoulflame.fx`
> 生前形态 `NPCs/Boss/KyuubiKitsunes/` 为另一代理单元，只做色彩/剧情呼应，代码零依赖。

## 1. 现状诊断

以 choreography skill 七大本能与失败模式清单为透镜：

1. **雾隐核心机制名存实亡**。FogSystem 只有氛围雾精灵/鬼火/涟漪三种粒子，"潜雾隐身 → 雾中眼睛 telegraph → 扑袭"完全不存在；`fogIntensity` 只是随阶段缓慢 lerp 的背景值，从不配合攻击节拍呼吸（违反本能 #4/#7：雾既不是因果链的一环，也不是会反应的世界）。
2. **动作语言失重且软**（本能 #1/#2/#3）。尾巴攻击全部是 EaseOutQuad/EaseInQuad 软曲线，没有 counter-motion、没有死寂帧、没有 poly(8+) 的爆发陡度；幽冥冲刺蓄力仅 25f 且蓄力表现是"变淡"（反可读），冲刺 30px/f 持续 30f（900px 长航——持续高速而非对比爆发），无硬刹车、无后坐。
3. **廉价传送**（失败模式清单点名）。P3 终极审判的传送是原地 alpha 渐隐渐显 + 位置 snap，无旧位遮掩、无 traveled 感。
4. **节奏平坦、招式复用**（本能 #5）。P1 hub 靠 `Main.rand.NextBool(120)` 每帧掷骰切招（切换时机完全不可控，可能长时间重复尾拍）；虚空九刺在 P1/P2/P3 出现三次仅改参数——没有"押后招"的未来感；攻击间无 connector 停顿。
5. **死亡无演出**。`OnKill` 只关雾；无 CheckDead 拦截，秒杀即消失——三大演出节拍缺一。
6. **多人不同步隐患**。尾巴攻击 pattern (`Main.rand.Next(0,4)`)、Phase2 出招 (`Main.rand.Next(5)`)、切招掷骰均在各端各自 roll，客户端看到的尾巴 telegraph 与服务器权威弹幕可能完全对不上。
7. **公平阀门不全**。P2 幻影追击持续压制无 breather；冲刺蓄力中 Boss 变淡（更难读）；无最小扑击距离、无伤害速度门。

判定：骨架有可取之处（FABRIK 尾巴 IK + 弹簧物理、雾系统发布通道、V2 自定义狐火弹），但核心机制缺失、编排全面平坦 → **全面重做 AI 编排与视觉**，保留并强化尾巴 IK / 发布通道两块骨架。

## 2. 设计主题与幻想感

**「雾祟」——你在她的雾里，而不是她在你的场里。**

生前的九尾狐（KyuubiKitsune）是暖金狐火、优雅流畅的舞尾；死后的冥狐是同一个灵魂被怨念泡冷的残响：狐火从暖金褪成冥蓝/鬼绿，动作语言从流畅曲线变成**僵-爆-僵**的鬼怪节奏——长时间死寂（雾中只剩发光的眼睛）→ 一瞬间的爆发扑袭 → 又归死寂。

玩家体验三层：
- **被狩猎感**：浓雾=危险蓄势，雾骤清=爆发即临（雾的呼吸就是全场最大的 telegraph）；雾中亮起的狐眼是"她在看你"的原始恐惧，瞳孔缩成竖线的瞬间=扑袭倒计时归零。
- **读真博弈**：镜雾九影/虚实九影/百鬼夜行三招层层升级"从群影中找真身"——读数线索从"会眨眼"到"有眼者伤"，怨念越深假影越多。
- **剧情呼应**：死亡演出的"回光返照"一拍——雾散、九尾狐火短暂回到生前暖金，随后逐点熄灭。不依赖 KyuubiKitsunes 任何代码，仅用色彩语言完成叙事。

## 3. 阶段结构与血量断点

| 阶段 | 血量 | 名称 | 内容 |
|---|---|---|---|
| Intro | — | 雾先至 | 雾吞世界→九点鬼火→狐身凝聚→凝视静止 |
| P1 | 100%→60% | 游祟 | 尾击琶音/钳击/下砸 + 狐火吐息 + 虚空九刺（手写循环表） |
| 转场1 | 60% | 雾葬 | 清弹→雾吞全屏→Boss 溶散消失→巨眼亮起→首次扑袭出雾（教学拍） |
| P2 | 60%→30% | 雾狩 | 雾隐扑袭（核心新招）+ 鬼影三掠 + 镜雾九影 + 九刺·雾 |
| 转场2 | 30% | 怨决 | 清弹→雾转鬼绿→九尾孔雀屏逐尖点燃→白闪 |
| P3 | 30%→0 | 怨主 | 虚实九影（升级保留）+ 九刺·怨 + 百鬼夜行（≤20% 解锁 set-piece） |
| 死亡 | 0 | 九火归寂 | CheckDead 拦截 ~330f 完整死亡弧（见 §5） |

攻击选择：**手写循环表**（PACING §2），每阶段一个数组，机动压制招与 zoning 招严格相间，同类"找真身"负荷招不相邻；表索引与所有服务器掷骰量走 `SendExtraAI` 同步，尾巴视觉出招全部由 (Phase, SubState, PhaseTimer, 已同步角度) 确定性推导——顺带修复多人不同步。

## 4. 招式编排表

时长均为 60fps 帧数；「预警」列注明形状+颜色+时长（遵守 TelegraphColors 契约：红只给致命）。

### P1

**T1 尾击琶音（GhostStab 重做曲线）**
| 段 | 帧 | 内容 |
|---|---|---|
| 前摇 | 12 | 尾巴后拉 `pow(t,8)` late-snap（几乎不动→末几帧猛然吸回） |
| 死寂 | 5 | 完全静止（僵-爆-僵的"僵"） |
| 爆发 | 4 | poly(12) 刺出（一帧走完 80% 行程） |
| 收招 | 16 | 指数衰减回摆 |

9 尾自左向右每 5f 一条启动（琶音），预警=尾尖鬼火亮起+尾巴吸回动作本身。公平：单尾行程窄，横向走位即避。

**T2 狐火吐息（新）**
| 段 | 帧 | 内容 |
|---|---|---|
| 前摇 | 50 | 悬停急停（v×0.9/f），胸前聚火：converging streaks 密度∝sqrt(t) 且 72% 处截止，最后 10f 全静默（pre-silence）；雾密度 +0.25 |
| 爆发 | 18 | 3 波 ×5(专家7) 发扇形狐火弹（波间 6f），每波 Boss 后坐 4px/f |
| 收招 | 30 | 慢速漂移，雾密度回落 |

预警=聚火粒子+雾变浓+静默拍，扇形 ~70°，弹速 8.5→12 渐升（转场后减速阀）。狐火落地/超时留怨火地灾（3s 鬼绿 DoT 场，形状=圆 SDF 鬼绿——TelegraphColors.GhostGreen=DoT 契约色）。

**T3 虚空九刺（保留强化）**
前摇 42f（尾巴收拢 coil，末 8f 改为完全死寂）→ 爆发 6f（刺出瞬间粒子截止 1f + 眼闪红 1f）→ 回收 27f，×2(专家3)，轮间 baseAngle+20°。预警=ArenaRunic 收口法阵（幽紫→刺出瞬间转红致命，沿用现有实现）。

**T1' 双尾钳击（新尾巴攻击）**：2 条对侧尾先飞到玩家左右 ~340px 处悬停亮尖 20f（预警）→ 同帧相向刺出（poly12, 5f）→ 回收。逃逸窗=垂直移动。
**T1'' 幻影下砸（保留）**：3 尾高举 → 落点 runic 圈预警 30f → 砸下。

P1 循环表：`[T1琶音, T2吐息, T1'钳击, T3九刺, T1''下砸, T2吐息]`，招间 connector 25f（尾巴收拢下垂 + 雾轻呼吸——段落停顿）。

### P2

**T4 雾隐扑袭（核心新招）**
| 段 | 帧 | 内容 |
|---|---|---|
| 入雾 | 25 | Boss 后拉 counter-motion + DissolveBurn 溶散，雾密度 +0.3，入雾后纯雾隐期无敌 |
| 眼现 | 18 | 玩家 380~460px 外随机方向（服务器 roll，同步），雾中一对狐眼 FadeIn + 凝视（NetherKitsuneEye，冥蓝） |
| 瞳缩 | 14 | 竖瞳收缩（固定常数=玩家可内化的倒计时），扑袭线细 beam 幽紫渐显，最后 6f 转红（致命预警） |
| 扑袭 | 10 | 眼睛处狐身一帧实体化（半透）+ set 46px/f 直线掠向玩家预测点（lead=vel×10），拖九尾雾带+残影；实体期可受击 |
| 硬刹 | 12 | ×0.6/f 制动+雾涡，原地再溶散进下一循环 |

循环 ×2（专家3），最后一次刹车后**不再溶散**：Boss 硬直喘息 40f（尾巴全部下垂、雾骤清 -0.4——呼气），大惩罚窗。
公平阀门：瞳缩 14f 固定；接触伤害仅 |v|>18px/f 时激活；眼睛距玩家 ≥380px（防 telefrag）；纯雾隐期不攻击。

**T5 鬼影三掠（幽冥冲刺重做）**
蓄力 22f（后拉 pow8 至 200px + 尾巴收拢死寂 + BeamGrad 冲刺线预告幽紫 22f，末 5f 转红）→ set 40px/f 直线 9f → 硬刹 ×0.62/f 10f → 侧向雾步（8f 溶散遮掩的短位移，替代廉价 blink：旧位置雾爆+新位置雾凝）→ 下一掠，共 ×3；第三掠终点甩尾半环 5 发狐火。与 T4 的差异=tempo：T4 长拍恐惧，T5 短拍连打。

**T6 镜雾九影（灵界召唤重做）**
玩家 360px 环上 4(专家5) 对狐眼同时亮起+雾影轮廓 40f；**真身的眼睛会眨一次**（合-开 12f，读真线索，真身索引服务器 roll 同步）→ 全体瞳缩 20f → 同帧向玩家收束冲刺 22px/f（多方向所以慢——速度补偿阀）；只有真身实体（接触+实弹），假影接触即溶散并散幽紫虚弹（无伤误导）。命中或穿过后真身硬刹实体化 25f 可受击。

**T7 虚空九刺·雾**：T3 加强（前摇 30f/回收 20f，×3），刺出瞬间九刺尖各留一朵怨火 patch（zoning 遗留，逼走位）。

P2 循环表：`[T4, T5, T7, T6, T5, T2吐息]`（T4/T6 两个"找真身"负荷招不相邻，吐息作低压 breather 保留进 P2）。

### P3

**T8 虚实九影（现有 Possession 三节拍保留+微调）**
Beat A 顺序幽刺（4f/尾加速琶音）→ Beat B 真身法阵锚+九向柔白实弹、幻影幽紫虚弹（保留现有真假博弈）→ Beat C 全尾魂魄横扫；beat 间 connector 15f 明确停顿。×2(专家3)。

**T9 百鬼夜行（新终结 set-piece，血量 ≤20% 解锁）**
全场浓雾 1.05，Boss 溶散 → 4 波鬼行列横掠（方向逐波交替）：三条横车道（间距 230px），**真车道入口先亮狐眼、掠影发光有伤（=Boss 本体 34px/f 横穿），暗影车道只有无害虚弹**（读真规则第三次升级）；每波前 ~28f 车道 beam 幽紫预告，真车道末段转红；波间 15f 呼吸。终拍：玩家背后方向雾中红瞳（全场唯一红瞳）20f 瞳缩 → 最终扑袭 50px/f → 落地硬直 50f 大惩罚窗。未到 20% 时该槽位由 T7 代替。

P3 循环表：`[T8, T9|T7, T8, T4']`（T4' 为扑袭快速版，循环 ×2）。

### 全局公平阀门
- 换阶段清弹 + 转场后首招弹速 20%→100% 60f 爬升。
- 距离栓绳：Boss 与目标 >1400px 时强制向内偏置（防飞屏绕圈）。
- 每状态双出口：完成或超时（所有 case 带 PhaseTimer 上限兜底）。
- 伤害窗=视觉窗：接触伤害全程由速度门+实体化状态共同控制（雾隐/半透期 CanHitPlayer=false）。

## 5. 入场 / 换阶段 / 死亡三大演出脚本

**入场《雾先至》(~240f)**：雾密度 0→0.55 爬升 60f（世界先变）→ 雾中九点鬼绿尾火自远及近渐次亮起（每 8f 一点，音阶下行）→ 狐身在九火中心反向溶解凝聚（DissolveBurn threshold 1→0, 50f）→ **凝视静止 55f**（menace is stillness：只有眼睛亮着+尾火摇曳）→ 尾巴一帧炸开成扇 + 怨啸 + shake 12 + 魂火泛光，战斗开始。

**转场1《雾葬》(~170f, 60%)**：清弹 → 惨叫 + 雾 40f 内涌到 1.0（屏幕近乎吞没）→ Boss 溶散消失 → **20f 全静默**（最浓的雾里什么都没有）→ 玩家侧前方巨眼 FadeIn 30f + 瞳缩 20f → 扑袭爆出（shake 10），雾回落 0.6，进入 P2。转场即核心机制的教学拍。

**转场2《怨决》(~110f, 30%)**：清弹 → 雾色冥蓝→鬼绿（uGhost 0→1）→ Boss 场中定身，九尾直立孔雀屏，尾尖自外向内每 6f 点燃一朵鬼绿火（音阶上行）→ 第九朵点燃瞬间白闪 1 帧 + shake 13 + 全场小眼睛氛围浮现。

**死亡《九火归寂》(~330f, CheckDead 拦截)**：
1. 0-40f 顿帧：Boss 定住、清弹、雾流动骤停（uTime 慢放）。
2. 40-100f 世界吸气：全场雾收束吸入狐身（converging streaks），雾密度 1.0→0.2。
3. 100-160f 回光返照：狐身/尾火渐变**生前暖金**（Color.Lerp 到暖橙金），仰首，一拍安静。
4. 160-250f 九火递熄：尾火自外向内逐点熄灭，间隔 18→5f 递减加速，每熄一点降调音。
5. 250f 终拍：最后心口火熄 → 1 帧白闪（bloom 满强）→ 爆散成冥蓝雾雨 + **shake 15（全场唯一最大值）** → 真死，雾 3s 内散尽。

## 6. 视觉技术方案

**新建专属着色器（ps_3_0，NetherKitsune 前缀，Boss 代码内静态缓存，不注册 ACMShaders）**：
1. `NetherKitsuneMist.fx` — 全屏冥雾后处理（s0=screenTarget, s1=共享噪声）：三层视差 FBM 域扭曲体积雾；uDensity 驱动呼吸；uGhost 冥蓝↔鬼绿换色（P3）；浓雾时场景去饱和+轻微 UV 扭曲+底部沉降+顶部暗角；玩家周围 uClearRadius 挖清晰洞（可读性保护）；uFreeze 死亡演出雾冻结。**替代现有 GenericWarp fog 调用，占同一全屏名额**（RequestFullscreenSlot 契约不变）。
2. `NetherKitsuneEye.fx` — 狐眼 SDF telegraph：杏仁眼形（双圆弧交）+ 竖瞳（uPupil 圆→竖线收缩）+ uOpen 眨眼 + 内辉光；局部世界空间 quad 绘制，FogSystem 统一管理眼睛实例池（FadeIn→Stare→Squint→Strike→FadeOut 状态机）。
3. `NetherKitsuneSoulflame.fx` — 程序化撕裂鬼火 sprite（FBM 上卷撕边+白青芯+冥蓝缘, uGhost 切鬼绿, uFlicker）：狐火弹主体、尾尖火、怨火地灾、死亡演出九火通用。

**复用共享件**：ArenaRunic（九刺收口/真身锚/下砸落点）、BeamGrad（冲刺线/车道预告/尾梢光带）、RadialBloom 加性 overlay（魂火泛光，走 FogSystem 现有发布通道）、DissolveBurn（Boss/幻影溶散实体化）、ACMScreenShakeSystem、ACMAsset.SoftGlow、共享 NoiseTexture。
**保留**：FogSystem 雾精灵 sprite 层调低强度做近景视差；鬼火 wisp、涟漪保留。

## 7. 性能与多人预算

- 全屏后处理仅 NetherKitsuneMist 一个，走名额契约 + `MythologyConfig.FullscreenShadersEnabled`，强度<0.01 直接 return；RadialBloom 走加性 overlay 不读 screenTarget。
- 眼睛实例池上限 12 对；每对 2 个局部 quad；怨火 patch 同屏上限 10（服务器端生成时计数）；雾精灵沿用 Max 常数。
- 所有 Effect/噪声静态缓存一次；Update/Draw 无每帧 new 纹理/Effect；无热路径 LINQ。
- 多人：随机决策（扑袭方向、真身索引、九刺基角、车道）仅服务器 roll → 存字段 → `SendExtraAI` 同步 + netUpdate；尾巴/幻影视觉由同步状态确定性推导；弹幕仅 `Main.netMode != MultiplayerClient` 生成；`Main.LocalPlayer`/雾绘制只在客户端路径。
- 掉落/进度不回退：NetherKyuubiBook 掉落保留；SoulBanner 层级引用的类名不动。

## 8. 实施清单

1. `Effects/NetherKitsuneMist.fx` / `NetherKitsuneEye.fx` / `NetherKitsuneSoulflame.fx` 新建 + 按名编译到退出码 0。
2. `NetherKitsuneFogSystem.cs`：雾密度呼吸通道（AI 发布目标密度+脉冲）、Mist 全屏绘制接管、狐眼实例池与状态机、雾冻结/鬼绿通道、死亡收束模式；保留 Bloom/Runic 通道。
3. `NetherKitsuneTail.cs`：新增僵-爆-僵刺击曲线（pow8 后拉+死寂+poly12）、双尾钳击攻击、孔雀屏姿态、尾尖鬼火发布。
4. `NetherKitsune.cs`：全新阶段机（Intro/P1 hub+循环表/转场1/P2 hub/转场2/P3 hub/Death）、雾隐扑袭/鬼影三掠/镜雾九影/狐火吐息/百鬼夜行、CheckDead 死亡演出、距离栓绳、同步字段扩展、PostDraw 换 Mist。
5. `NetherKitsuneProjectiles.cs`：NetherFoxfireSoul 升级 Soulflame 绘制 + 吐息变体；新增 NetherGhostflamePatch 怨火地灾（鬼绿 DoT 场，AddSoulErosion 联动）。
6. `Items/NetherKyuubiBook.cs`：不动核心（掉落/数值/公开类名不变）。
7. ReadLints 全部改动文件清零；最后小步 StrReplace 补两个 hjson 的新弹幕 DisplayName 并回读验证。
