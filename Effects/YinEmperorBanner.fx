// ============================================================
// 阴天子·冥幡着色器 — 酆都法庭仪仗幡旗
// 程序化布幔: 风场摆动(自由端加剧) + 竖排符文流动 + 金边镶饰
// uBurn 0->1: 自下而上灼烧熄灭(死亡演出逐杆熄灭)
// 喂可平铺噪声(s0, RGB三通道独立); quad UV: x=横向 y=0顶部挂点 y=1底部垂端
// ============================================================

sampler uNoise : register(s0); // 可平铺噪声

float  uTime;            // 动画时间(秒)
float  uIntensity;       // 整体可见度 0~1
float  uWave;            // 风力 0~2 (布幔摆动幅度)
float  uBurn;            // 熄灭进度 0~1 (0=完好, 1=烧尽)
float4 uColorPrimary;    // 布面主色(深冥)
float4 uColorSecondary;  // 符文/镶边色(帝金)
float  uSeed;            // 每杆幡旗的相位种子

float4 BannerPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = coords;

    // —— 布料风场: 自由端(y=1)摆幅大, 挂点(y=0)固定 ——
    float sway = sin(uv.y * 3.4 - uTime * 1.6 + uSeed) * 0.5
               + sin(uv.y * 7.1 - uTime * 2.7 + uSeed * 1.7) * 0.28;
    float swayAmp = uv.y * uv.y * 0.085 * uWave;
    float cx = uv.x - 0.5 + sway * swayAmp;

    // —— 幡形遮罩: 竖直窄条 + 底部燕尾开衩 ——
    float halfW = 0.335 - uv.y * 0.03;
    float body = smoothstep(halfW, halfW - 0.045, abs(cx));
    // 燕尾: 底部中央向上剪开
    float notchDepth = smoothstep(0.10, 0.0, abs(cx)) * 0.16;
    float bottomEdge = 1.0 - notchDepth;
    body *= smoothstep(bottomEdge, bottomEdge - 0.03, uv.y);
    // 顶部横杆阴影线
    body *= smoothstep(0.0, 0.025, uv.y);

    if (body < 0.01)
        return float4(0, 0, 0, 0);

    // —— 灼烧熄灭: 自下而上噪声蚕食 ——
    float nBurn = tex2D(uNoise, float2(uv.x * 2.3 + uSeed, uv.y * 1.7)).g;
    float burnField = (1.0 - uv.y) + (nBurn - 0.5) * 0.24;
    clip(burnField - uBurn * 1.15 + 0.001);
    // 灼烧前沿亮边(余烬)
    float ember = 1.0 - smoothstep(0.0, 0.10, burnField - uBurn * 1.15);
    ember = saturate(ember) * step(0.01, uBurn);

    // —— 布面明暗: 皱褶(风场导数近似) + 纵向渐暗 ——
    float fold = 0.5 + 0.5 * sin(uv.y * 9.0 - uTime * 2.2 + sway * 2.0 + uSeed);
    float shade = 0.72 + fold * 0.28;
    float3 cloth = uColorPrimary.rgb * shade * (1.0 - uv.y * 0.18);

    // —— 中央竖排符文: 窄带内块状字符向下流动 ——
    float bandMask = smoothstep(0.115, 0.075, abs(cx));
    float glyphN = tex2D(uNoise, float2(0.31 + uSeed * 0.13, uv.y * 3.2 - uTime * 0.055)).r;
    float glyphCell = step(0.56, glyphN);
    // 字符内部横笔画感
    float stroke = step(0.35, frac(uv.y * 26.0 + glyphN * 7.0));
    float rune = bandMask * glyphCell * stroke;
    float runePulse = 0.75 + 0.25 * sin(uTime * 2.4 + uv.y * 6.0 + uSeed);

    // —— 金边镶饰: 幡缘窄金线 ——
    float edgeLine = smoothstep(halfW - 0.045, halfW - 0.02, abs(cx))
                   * smoothstep(halfW, halfW - 0.02, abs(cx));

    float3 col = cloth;
    col = lerp(col, uColorSecondary.rgb, rune * runePulse * 0.9);
    col += uColorSecondary.rgb * edgeLine * 0.55;
    // 余烬前沿: 亮金转赤
    col = lerp(col, float3(1.0, 0.55, 0.18), ember * 0.85);
    col += float3(1.0, 0.62, 0.2) * ember * ember * 1.6;

    float alpha = body * uIntensity * (0.82 + rune * 0.18);
    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass BannerPass
    {
        PixelShader = compile ps_3_0 BannerPS();
    }
}
