// ============================================================
// 祖龙残魂·星尘龙体着色器 — 体节单 pass (虚实对比核心件)
// 溶解显形/消散(clip+星辉边) + 幽魂虚化(噪声透明波动+星点剥离)
// + 体内流光(CPU 按节传入亮度) + 合体太初金染
// s0=体节贴图, s1=共享可平铺FBM噪声(ACMShaders.NoiseTexture)
// 由龙头统一合批绘制, Immediate 模式逐节 SetValue+Apply
// ============================================================

sampler uImage0 : register(s0); // 体节贴图
sampler uNoise  : register(s1); // 三通道FBM噪声

float  uTime;        // 动画时间(秒)
float  uNoiseScale;  // 噪声密度 (建议 1.2~2.5)
float  uDissolve;    // 溶解进度 0=完整 1=完全消散 (入场显形/死亡星散共用)
float  uGhost;       // 虚化程度 0=凝实 1=完全幽魂化 (相位穿行/喘息拍)
float  uFlowGlow;    // 体内流光经过该节的加性亮度 0~1 (CPU 按节波形计算)
float  uSeed;        // 每节相位种子 (错开噪声, 避免整龙同步闪烁)
float  uMergeGold;   // 合体「太初真身」金染 0~1
float4 uEdgeColor;   // 星辉边缘色 (rgb=色, a=强度)
float4 uFlowColor;   // 流光色 (rgb=色, a=强度)

static const float EdgeWidth = 0.13; // 溶解星辉边宽度

float4 SoulBodyPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 base = tex2D(uImage0, coords) * sampleColor;

    // ---- 溶解场: 噪声 + 节种子漂移 ----
    float2 nUV = coords * max(uNoiseScale, 0.001)
               + float2(uTime * 0.03 + uSeed, uSeed * 1.73 - uTime * 0.021);
    float field = tex2D(uNoise, nUV).r;

    // 完全消散像素直接丢弃 (dissolve=0 时不裁剪)
    clip(field - uDissolve);

    // 星辉灼烧边: 紧贴溶解边缘的窄带
    float edge = 1.0 - smoothstep(uDissolve, uDissolve + EdgeWidth, field);
    edge = saturate(edge) * step(0.0001, uDissolve);

    // ---- 幽魂虚化: 低频噪声流动的体透明波动 ----
    float ghostNoise = tex2D(uNoise, coords * 1.7 + float2(uSeed * 2.1, uTime * 0.055)).g;
    float ghostMask = lerp(1.0, 0.28 + 0.42 * ghostNoise, saturate(uGhost));

    // 星点剥离: 虚化越深, 高频噪声点越亮 (身体化作星屑的读法)
    float sparkle = pow(tex2D(uNoise, coords * 4.2 + float2(uTime * 0.09 + uSeed, uSeed)).b, 9.0)
                  * saturate(uGhost) * 2.4;

    // ---- 合成 ----
    float3 col = base.rgb;

    // 合体太初金: 色相向暖金偏移 (保持预乘规模)
    col = lerp(col, col * float3(1.12, 1.00, 0.78) + float3(0.10, 0.07, 0.01) * base.a, saturate(uMergeGold) * 0.7);

    // 虚化: 主体按 ghostMask 整体衰减 (预乘 alpha 同步)
    col *= ghostMask;
    float alpha = base.a * ghostMask;

    // 星辉边 (溶解) — 不随 ghost 衰减, 虚化时轮廓仍可读
    col = lerp(col, uEdgeColor.rgb * alpha, edge * uEdgeColor.a * 0.65);
    col += uEdgeColor.rgb * edge * edge * uEdgeColor.a * 1.6 * base.a;

    // 体内流光 + 星点剥离 (加性, 以贴图 alpha 为遮罩)
    col += uFlowColor.rgb * uFlowGlow * uFlowColor.a * base.a;
    col += uEdgeColor.rgb * sparkle * base.a;

    return float4(col, alpha);
}

technique AncestralSoulBody
{
    pass P0
    {
        PixelShader = compile ps_3_0 SoulBodyPS();
    }
}
