// ============================================================
// 朱雀焰翼着色器 — 程序化火焰翅膀 (quad PS)
// 用法: Immediate batch 套用本 effect, 每侧翅膀画一个旋转 quad。
// uv 空间约定: x=0 翼根 → x=1 翼尖; y=0.5 翼骨中线, 上下近似对称
// (左翼以 rotation = PI - 右翼角 复用同一 quad, 依赖此对称性)。
// s1 绑 ACMShaders.NoiseTexture (256² 三通道 FBM, LinearWrap)。
// ============================================================

sampler uImage0 : register(s0); // 载体纹理 (仅取批次, 颜色不采样)
sampler uNoise  : register(s1); // 共享可平铺噪声

float uTime;      // 动画时间 (秒)
float uFlap;      // 振翅下压脉冲 0(滑翔)~1(刚振翅) — 驱动火舌外扩与亮度爆闪
float uIntensity; // 火焰强度 0~1.8 (0=熄灭; 涅槃形态 >1)
float uNirvana;   // 0~1 涅槃换色 (赤橙 → 金白)

// 色带常量: 缘暗红 → 焰橙 → 金芯; 涅槃时整体推向金白
static const float3 EmberDark = float3(0.42, 0.06, 0.02);
static const float3 FlameRed  = float3(1.00, 0.30, 0.06);
static const float3 FlameOrn  = float3(1.00, 0.60, 0.14);
static const float3 FlameGold = float3(1.00, 0.85, 0.42);
static const float3 NirvEdge  = float3(1.00, 0.80, 0.34);
static const float3 NirvCore  = float3(1.00, 0.97, 0.84);

float4 FireWingPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.004)
        return float4(0, 0, 0, 0);

    float x = coords.x;          // 翼根 → 翼尖
    float y = coords.y - 0.5;    // 相对翼骨中线

    // ==========================================
    //  翼形 SDF — 根厚尖细 + 肩部圆润 + 中线上拱
    // ==========================================
    float halfW = lerp(0.36, 0.055, pow(saturate(x), 0.72));
    halfW *= 0.15 + 0.85 * smoothstep(0.0, 0.10, x);
    float spine = -0.08 * sin(x * 3.1416);          // 翼骨微上拱
    float d = abs(y - spine) / max(halfW, 0.001);   // 0=翼骨 1=名义羽缘

    // ==========================================
    //  火焰对流 — 上升 UV 滚动 + 域扭曲 (两级)
    // ==========================================
    float2 fuv1 = float2(x * 1.6 + uTime * 0.05, y * 2.2 - uTime * 0.55);
    float warp  = tex2D(uNoise, fuv1).g;
    float2 fuv2 = float2(x * 3.0 - uTime * 0.10 + warp * 0.35,
                         y * 3.4 - uTime * 0.95 + warp * 0.50);
    float flame = tex2D(uNoise, fuv2).r;
    float lick  = tex2D(uNoise, float2(x * 5.0 + uTime * 0.22, y * 5.5 - uTime * 1.40)).b;
    flame = flame * 0.7 + lick * 0.3;

    // ==========================================
    //  羽缘火舌 — 噪声蚕食羽缘; 振翅时火舌外扩拉长
    // ==========================================
    float edgeReach = 0.55 + uFlap * 0.40;                    // 振翅 → 火舌伸更远
    float edge = 0.60 + (flame - 0.30) * edgeReach;           // ~0.33 .. 1.3
    float body = 1.0 - smoothstep(edge * 0.45, edge, d);

    // 分段羽枝: 沿翼展的固定频率锯齿 (羽毛感, 无动态循环)
    float feathers = 0.72 + 0.28 * sin(x * 34.0 + warp * 6.0);
    body *= lerp(1.0, feathers, smoothstep(0.35, 0.95, d));

    // ==========================================
    //  亮度合成 — 翼骨白热芯 + 对流亮斑
    // ==========================================
    float core = 1.0 - smoothstep(0.0, 0.5, d);
    float glow = body * (0.55 + flame * 0.75);
    glow += core * (0.85 + uFlap * 0.65);
    glow *= uIntensity;

    // ==========================================
    //  色带 (涅槃换金白)
    // ==========================================
    float3 cEdge = lerp(FlameRed,  NirvEdge, uNirvana);
    float3 cMid  = lerp(FlameOrn,  NirvEdge, uNirvana * 0.6);
    float3 cCore = lerp(FlameGold, NirvCore, uNirvana);
    float3 col = lerp(cEdge, cMid, saturate(glow * 0.8));
    col = lerp(col, cCore, saturate(core * (0.7 + uFlap * 0.5)));
    col = lerp(EmberDark, col, saturate(glow));

    // 根/尖淡出, 避免 quad 硬边
    float fade = smoothstep(0.0, 0.06, x) * (1.0 - smoothstep(0.90, 1.0, x));

    float a = saturate(glow) * fade;
    return float4(col * a, a) * sampleColor; // 乘 tint: 支持 CPU 侧整体透明度 (假深度/淡出)
}

technique Technique1
{
    pass FireWingPass
    {
        PixelShader = compile ps_3_0 FireWingPS();
    }
}
