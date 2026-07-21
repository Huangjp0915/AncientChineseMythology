// ============================================================
// 干将莫邪·合鸣阴阳盘 — 屏幕空间世界 decal (刀剑系列旗舰大招专属)
// 满鸣合璧时在交点展开: 赤金/青蓝双鱼回旋 + 双鱼眼 + 白热界线 + 金环收口
// 经 ACMShaders.DrawScreenSpaceDecalStandalone 绘制 (s0 = 共享噪声, 满屏 uv)
// uCenter/uRadius/uAspect 用 ACMShaders.WorldDecalParams 换算
// ============================================================

sampler uNoise : register(s0);

float  uTime;      // 动画时间(秒)
float  uIntensity; // 整体强度 0~1 (开合)
float2 uCenter;    // 屏幕 UV 中心
float  uRadius;    // 半径 (屏幕高度比例)
float  uAspect;    // 宽高比
float4 uColorA;    // 干将赤金
float4 uColorB;    // 莫邪青蓝
float  uSpin;      // 自旋速度 (rad/s)

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0;
};

float4 PS_Unity(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;

    // 屏幕 UV → 以盘半径为 1 的局部坐标 (纵向为基准, 横向乘宽高比)
    float2 p = uv - uCenter;
    p.x *= uAspect;
    float r = length(p) / max(uRadius, 0.0001);
    if (r > 1.25)
        return float4(0, 0, 0, 0);

    // 自旋
    float ang = atan2(p.y, p.x) + uTime * uSpin;
    float2 q = float2(cos(ang), sin(ang)) * r; // 归一化盘面坐标 (r=1 为盘缘)

    // 阴阳双鱼几何: 上下两枚半径 0.5 的半盘圆
    float dTop = length(q - float2(0.0, 0.5));
    float dBot = length(q + float2(0.0, 0.5));
    float soft = 0.045;

    // A 侧 (赤金) = 右半 ∪ 下小圆 − 上小圆
    float half_ = smoothstep(-soft, soft, q.x);       // 右半权重
    float inTop = smoothstep(soft, -soft, dTop - 0.5); // 上小圆内
    float inBot = smoothstep(soft, -soft, dBot - 0.5); // 下小圆内
    float sideA = saturate(half_ * (1.0 - inTop) + inBot);

    // 鱼眼 (反色)
    float eyeTop = smoothstep(soft, -soft, dTop - 0.16); // 上眼 → A 色
    float eyeBot = smoothstep(soft, -soft, dBot - 0.16); // 下眼 → B 色
    sideA = saturate(sideA + eyeTop - eyeBot);

    float3 col = lerp(uColorB.rgb, uColorA.rgb, sideA);

    // 噪声呼吸 (盘面能量流动)
    float n = tex2D(uNoise, q * 0.4 + float2(uTime * 0.05, -uTime * 0.04)).r;
    col *= 0.8 + 0.45 * n;

    // 白热界线: S 形界 (两小圆边) + 眼缘
    float boundary = 0.0;
    boundary += smoothstep(0.05, 0.0, abs(dTop - 0.5)) * (1.0 - inBot);
    boundary += smoothstep(0.05, 0.0, abs(dBot - 0.5)) * (1.0 - inTop);
    boundary += smoothstep(0.04, 0.0, abs(dTop - 0.16));
    boundary += smoothstep(0.04, 0.0, abs(dBot - 0.16));
    boundary = saturate(boundary) * smoothstep(1.02, 0.9, r); // 只在盘内
    col += float3(1.0, 0.98, 0.9) * boundary * 0.9;

    // 盘缘金环 (双层)
    float rim = smoothstep(0.07, 0.0, abs(r - 1.0));
    float rim2 = smoothstep(0.05, 0.0, abs(r - 1.12)) * 0.5;
    col += lerp(uColorA.rgb, float3(1.0, 0.95, 0.8), 0.5) * (rim + rim2) * 1.2;

    // 盘内软填充 + 缘外淡出
    float inside = smoothstep(1.18, 1.0, r);
    float fill = lerp(0.35, 1.0, pow(1.0 - saturate(r), 0.5)); // 心部略亮

    float alpha = inside * fill * uIntensity;
    alpha += (boundary + rim) * uIntensity * 0.5;

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass UnityPass
    {
        PixelShader = compile ps_3_0 PS_Unity();
    }
}
