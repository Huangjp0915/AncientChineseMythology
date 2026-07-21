// ============================================================
// AzureDragonMist.fx — 苍龙云雾涡旋 (屏幕空间贴花)
// 腾云驾雾核心视觉: FBM 双八度旋转涡 + 软圆遮罩
// 用途: 入场云中显形 / 盘旋聚雾 / 腾云吞没与破云 / 死亡雨霁
// 载体: 满屏噪声四边形 (DrawScreenSpaceDecalStandalone 同款调用),
//        s0=共享可平铺噪声; uCenter/uRadius 为屏幕 UV 契约 (WorldDecalParams)
// ============================================================

sampler uImage0 : register(s0); // 共享可平铺噪声

float  uTime;      // 动画时间(秒)
float  uIntensity; // 整体强度 0~1
float2 uCenter;    // 涡心 (屏幕UV)
float  uRadius;    // 半径 (占屏幕高度比例)
float  uAspect;    // 宽高比 width/height
float4 uColor;     // 雾色 (rgb; a=不透明度权重)
float  uSwirl;     // 涡旋强度 (弧度, 1~4)
float  uSoftEdge;  // 边缘软度 (0.2~0.8)

float2 rotate2(float2 v, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

float4 PS_Mist(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = coords;

    // 修正宽高比后的到涡心向量 (以屏幕高度为基准)
    float2 d = uv - uCenter;
    d.x *= uAspect;
    float dist = length(d) / max(uRadius, 0.001); // 0=心 1=缘

    // 软圆遮罩
    float mask = 1.0 - smoothstep(1.0 - uSoftEdge, 1.0, dist);
    if (mask <= 0.001)
        return float4(0, 0, 0, 0);

    // 涡旋: 越靠涡心旋转越多, 随时间缓转
    float angle = uSwirl * (1.0 - dist) + uTime * 0.35;
    float2 sw = rotate2(d, angle);

    // FBM 双八度: 大涡 + 细碎云絮, 反向漂移
    float2 uvA = sw * 1.6 + float2(uTime * 0.05, -uTime * 0.03);
    float2 uvB = sw * 3.7 + float2(-uTime * 0.08, uTime * 0.045);
    float nA = tex2D(uImage0, uvA).r;
    float nB = tex2D(uImage0, uvB).b;
    float cloud = nA * 0.68 + nB * 0.45;

    // 云体密度: 噪声挖出絮状边缘
    float body = saturate(cloud - dist * 0.55);
    body = body * body * (3.0 - 2.0 * body);

    // 心部微微发亮 (雷光透云)
    float glow = pow(saturate(1.0 - dist), 3.0) * (0.5 + 0.5 * sin(uTime * 2.3)) * 0.35;

    float3 col = uColor.rgb * (0.75 + 0.45 * cloud) + glow;
    float alpha = body * mask * uColor.a * uIntensity;

    return float4(saturate(col * alpha), saturate(alpha)); // 预乘输出, AlphaBlend 下柔和叠加
}

technique Technique1
{
    pass DragonMistPass
    {
        PixelShader = compile ps_3_0 PS_Mist();
    }
}
