// ============================================================
// 将臣专属 — 电弧条带着色器 (TriangleStrip 直带, 与 BeamGrad 同顶点契约)
// 与 BeamGrad 的区别: 中心线被"分段折线 + 平滑噪声"位移, 读作真电弧而非光束
//   ● 分段随机折点 (hash 折线, 按 uRerollHz 重掷 → 电弧形状不断跳变)
//   ● 高次幂白热芯 + 雷青辉光 halo + 次级幻影弧 (分叉感)
//   ● uSeed 每条弧独立形状; 全局高频频闪
// 仅 PS, 顶点变换走 SpriteBatch 外部矩阵 (uv.x=沿长 0~1, uv.y=横宽 0~1)
// s0/s1 均绑共享噪声 (JiangcenVFX.DrawArc 负责)
// ============================================================

sampler uImage0 : register(s0); // 共享噪声 (占位, 与 s1 同源)
sampler uNoise  : register(s1); // 可平铺噪声 (平滑摆动用)

float  uTime;       // 动画时间(秒)
float  uIntensity;  // 整体强度 0~1
float  uSeed;       // 每条弧独立随机种子
float4 uColorCore;  // 核心色 (a=芯部不透明度权重)
float4 uColorEdge;  // 辉光色 (a=边缘不透明度权重)
float  uCoreGlow;   // 芯部加法过曝辉度
float  uJagAmp;     // 折线位移幅度 (0~0.42, uv.y 半宽单位)
float  uJagScale;   // 折点段数 (建议 6~16)
float  uFlickerHz;  // 全局频闪频率 (0=不闪)
float  uRerollHz;   // 折线形状重掷频率 (建议 8~18)

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

float Hash11(float x)
{
    return frac(sin(x * 127.1 + 311.7) * 43758.5453);
}

// 分段折线位移: 量化 uv.x 成段, 段端点 hash 随机高度, 段内线性插值 → 经典闪电折线
float JagOffset(float x, float seed)
{
    float segPos = x * max(uJagScale, 1.0);
    float segId  = floor(segPos);
    float segF   = frac(segPos);
    float a = Hash11(segId + seed) - 0.5;
    float b = Hash11(segId + 1.0 + seed) - 0.5;
    float jag = lerp(a, b, segF);
    // 端点钉死 (首尾折回中心线, 电弧两端锚在端点上)
    float anchor = smoothstep(0.0, 0.08, x) * smoothstep(1.0, 0.92, x);
    return jag * anchor;
}

float4 PS_LightningArc(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;

    // 折线形状按 uRerollHz 重掷 → 电弧不断改变路径
    float reroll = floor(uTime * max(uRerollHz, 0.01)) * 51.7 + uSeed * 13.37;

    // 主折线 + 平滑摆动 (噪声给折线之间的细颤)
    float jag = JagOffset(uv.x, reroll) * uJagAmp;
    float2 nUV = float2(uv.x * 2.3 + uSeed * 0.71, uTime * 0.9 + uSeed);
    float smoothSway = (tex2D(uNoise, nUV).r - 0.5) * uJagAmp * 0.5;
    float center = 0.5 + jag + smoothSway;

    // 次级幻影弧: 另一套折点, 幅度稍大更飘, 亮度减半 → 分叉/残影感
    float jag2 = JagOffset(uv.x, reroll + 77.7) * uJagAmp * 1.35;
    float center2 = 0.5 + jag2 + smoothSway * 0.6;

    // 到两条弧线的横向距离 (0=线上)
    float d1 = abs(uv.y - center) * 2.0;
    float d2 = abs(uv.y - center2) * 2.0;

    // 主弧: 白热窄芯 + 宽辉光
    float core1 = pow(saturate(1.0 - d1 * 3.2), 6.0);
    float halo1 = pow(saturate(1.0 - d1), 2.0);
    // 次弧: 只有暗芯
    float core2 = pow(saturate(1.0 - d2 * 3.6), 6.0) * 0.45;

    // 沿长亮斑 (能量结点): 阈值化 hash → 弧上零星过曝点
    float bead = step(0.82, Hash11(floor(uv.x * uJagScale * 2.0) + reroll * 0.37));
    core1 += bead * core1 * 1.5;

    // 端点收口
    float ends = smoothstep(0.0, 0.05, uv.x) * smoothstep(1.0, 0.95, uv.x);

    // 全局高频频闪 (电感的主来源之一)
    float flick = 1.0;
    if (uFlickerHz > 0.01)
        flick = 0.68 + 0.32 * Hash11(floor(uTime * uFlickerHz) + uSeed * 7.0);

    float3 col = uColorEdge.rgb * halo1
               + uColorCore.rgb * (core1 + core2)
               + uColorCore.rgb * core1 * uCoreGlow;
    col *= flick;

    float alpha = saturate(halo1 * uColorEdge.a + (core1 + core2) * uColorCore.a);
    alpha *= ends * uIntensity * flick;

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass LightningArcPass
    {
        PixelShader = compile ps_3_0 PS_LightningArc();
    }
}
