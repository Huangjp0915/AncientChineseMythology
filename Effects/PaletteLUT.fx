// ============================================================
// 屏幕调色着色器 — 全屏后处理 (重映射)
// 阴影/高光分区染色 + 饱和度 + 色相位移 + 阴阳分屏(yin-yang-split)
// 神威罪名色 / 大椿四季 / 朱雀涅槃灰<->赤 / 阴天子·无常阴阳分屏 共用
// 与 ElementalScreenTint 区别: tint=加色叠加, LUT=对原画面重映射
// 喂 Main.screenTarget(s0)
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标

float  uTime;          // 动画时间(秒)
float  uIntensity;     // 整体强度 0~1 (与原画面混合权重)
float  uAspect;        // 宽高比 width/height
float  uSaturation;    // 饱和度缩放 (1=不变, 0=灰, >1=增艳)
float  uHueShift;      // 色相位移(弧度)
float4 uShadowTint;    // 阴影染色 (rgb, a=权重)
float4 uHighlightTint; // 高光染色 (rgb, a=权重)
float  uSplit;         // 阴阳分屏 0=关 1=开
float2 uSplitDir;      // 分屏法线方向(归一化)
float  uSplitPos;      // 分屏中线位置(沿法线投影 0~1)

static const float3 LUM = float3(0.299, 0.587, 0.114);

// 色相旋转(绕亮度轴近似)
float3 HueRotate(float3 col, float a)
{
    float c = cos(a);
    float s = sin(a);
    // YIQ 色相旋转近似矩阵
    float3x3 m = float3x3(
        0.299 + 0.701 * c + 0.168 * s, 0.587 - 0.587 * c + 0.330 * s, 0.114 - 0.114 * c - 0.497 * s,
        0.299 - 0.299 * c - 0.328 * s, 0.587 + 0.413 * c + 0.035 * s, 0.114 - 0.114 * c + 0.292 * s,
        0.299 - 0.300 * c + 1.250 * s, 0.587 - 0.588 * c - 1.050 * s, 0.114 + 0.886 * c - 0.203 * s);
    return mul(m, col);
}

float4 PaletteLUTPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 scene = tex2D(uImage0, coords);
    if (uIntensity < 0.01)
        return scene;

    float3 col = scene.rgb;
    float lum = dot(col, LUM);

    // 色相位移
    if (abs(uHueShift) > 0.0001)
        col = HueRotate(col, uHueShift);

    // 饱和度
    float3 grey = dot(col, LUM).xxx;
    col = lerp(grey, col, uSaturation);

    // 阴影/高光分区染色
    float shadowW = (1.0 - smoothstep(0.0, 0.5, lum)) * uShadowTint.a;
    float highW   = smoothstep(0.5, 1.0, lum) * uHighlightTint.a;
    col = lerp(col, col * uShadowTint.rgb * 2.0, shadowW * 0.5);
    col = lerp(col, col * uHighlightTint.rgb * 2.0, highW * 0.5);

    // 阴阳分屏: 沿 uSplitDir 一侧走"阴"(去饱和压暗+阴影色), 另一侧走"阳"(高光色提亮)
    if (uSplit > 0.5)
    {
        float2 p = float2(coords.x * uAspect, coords.y);
        float proj = dot(p, normalize(uSplitDir + 0.0001));
        float mid = uSplitPos * (1.0 + uAspect) * 0.5;
        float seam = proj - mid;
        float side = smoothstep(-0.02, 0.02, seam); // 0=阴 1=阳

        float3 yin = lerp(dot(col, LUM).xxx, col, 0.35) * uShadowTint.rgb * 1.6;
        float3 yang = col * uHighlightTint.rgb * 1.4 + uHighlightTint.rgb * 0.1;
        float3 splitCol = lerp(yin, yang, side);

        // 缝隙高光
        float seamGlow = exp(-seam * seam * 1200.0);
        splitCol += (uShadowTint.rgb + uHighlightTint.rgb) * 0.5 * seamGlow;

        col = lerp(col, splitCol, uSplit);
    }

    col = lerp(scene.rgb, col, uIntensity);
    return float4(saturate(col), scene.a);
}

technique Technique1
{
    pass PaletteLUTPass
    {
        PixelShader = compile ps_3_0 PaletteLUTPS();
    }
}
