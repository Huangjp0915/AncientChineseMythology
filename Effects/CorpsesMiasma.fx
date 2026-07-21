// ============================================================
// 尸骸·枉死尸雾 — 全屏后处理 (读 screenTarget)
// 底部 FBM 尸雾上涌 + 画面去饱和压暗 + 暗角 + 上飘魂点
// + uFlash 死亡崩解白化对比帧 (一次性)
// 调用: Corpses.PostDraw 经 ACMShaders.RequestFullscreenSlot() 名额契约
// s0 = Main.screenTarget, s1 = ACMShaders.NoiseTexture (LinearWrap)
// ============================================================

sampler uScreen : register(s0);
sampler uNoise : register(s1);

float uTime;        // 秒
float uIntensity;   // 总强度 0~1 (随阶段递进)
float uFlash;       // 死亡白化对比帧 0~1
float uAspect;      // width/height

static const float3 FogViolet  = float3(0.26, 0.17, 0.40); // 幽蓝紫尸雾
static const float3 FogDeep    = float3(0.05, 0.03, 0.09);
static const float3 GhostGreen = float3(0.43, 0.90, 0.59); // 鬼绿魂点
static const float3 BoneWhite  = float3(0.88, 0.94, 0.86);

float4 MiasmaPS(float2 uv : TEXCOORD0) : COLOR0
{
    float3 col = tex2D(uScreen, uv).rgb;
    float k = saturate(uIntensity);

    // ---- 尸气蚀色: 去饱和 + 冷偏 + 压暗 ----
    float lum = dot(col, float3(0.299, 0.587, 0.114));
    float3 graded = lerp(col, lum * float3(0.80, 0.86, 0.97), 0.42 * k);
    graded *= 1.0 - 0.15 * k;

    // ---- 底部尸雾上涌 (双层滚动 FBM, 噪声向下采样 = 视觉上涌) ----
    float n1 = tex2D(uNoise, float2(uv.x * 1.6, uv.y * 2.1 + uTime * 0.045)).r;
    float n2 = tex2D(uNoise, float2(uv.x * 3.2 - uTime * 0.021, uv.y * 3.9 + uTime * 0.078)).g;
    float fogMask = smoothstep(0.42, 1.05, uv.y + (n1 - 0.5) * 0.38);
    float fog = fogMask * (0.5 + 0.5 * n2) * k;
    float3 fogCol = lerp(FogDeep, FogViolet, n1);
    // 雾顶缘一线鬼绿磷光
    float rim = smoothstep(0.02, 0.18, fogMask) * (1.0 - smoothstep(0.18, 0.60, fogMask));
    fogCol += GhostGreen * rim * n2 * 0.40;
    graded = lerp(graded, fogCol, saturate(fog * 0.85));

    // ---- 上飘魂点 (稀疏高通亮斑, 缓慢上升 + 闪烁) ----
    float s = tex2D(uNoise, float2(uv.x * 4.6 + 3.7, uv.y * 4.6 + uTime * 0.11)).b;
    float mote = smoothstep(0.955, 1.0, s) * k;
    graded += GhostGreen * mote * (0.30 + 0.24 * sin(uTime * 3.1 + uv.x * 41.0));

    // ---- 暗角 (棺内窥视感) ----
    float2 c = (uv - 0.5) * float2(uAspect, 1.0);
    float vig = smoothstep(0.55, 1.08, length(c));
    graded *= 1.0 - vig * 0.42 * k;

    // ---- 死亡崩解: 黑白高对比白化帧 ----
    float hard = smoothstep(0.28, 0.60, lum);
    float3 flashCol = lerp(float3(0.02, 0.01, 0.04), BoneWhite, hard);
    graded = lerp(graded, flashCol, saturate(uFlash));

    return float4(graded, 1.0);
}

technique Miasma
{
    pass P0
    {
        PixelShader = compile ps_3_0 MiasmaPS();
    }
}
