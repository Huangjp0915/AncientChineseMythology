// ============================================================
// 敖闰寒潮冻土 / 冻结陷阱着色器 — 屏幕空间地纹 decal (s0=共享噪声)
//   uMode 0 = 寒潮冻土: 扁椭圆枝晶生长场 + 生长前沿亮环 + 内部闪晶
//   uMode 1 = 冻结陷阱: ≤4 个圆区合批, 外环标界 + 内缩倒计时环 + 中心晶花
// 由 ACMShaders.DrawScreenSpaceDecal / Standalone 全屏绘制噪声载体驱动
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;
float2 uCenter;         // mode0: 场中心屏幕 UV
float  uRadius;         // mode0: 最大半径 (屏幕高度比例)
float  uProgress;       // mode0: 蔓延进度 0~1
float  uIntensity;      // 整体强度 0~1
float  uAspect;         // 宽高比
float  uMode;           // 0=冻土场 1=陷阱
float4 uColorPrimary;   // 主色 (Frost 冰蓝)
float4 uColorSecondary; // 亮色 (IceWhite)
float4 uTraps[4];       // mode1: xy=UV z=半径(屏高比例) w=倒计时进度 0~1
float  uTrapCount;      // mode1: 有效陷阱数

float4 FieldColor(float2 pos, float2 center)
{
    // 压扁椭圆距离: 地表霜面 (宽、低)
    float2 diff = pos - center;
    diff.y *= 3.4;
    float dist = length(diff);
    float normDist = dist / max(uRadius, 0.001);
    float grow = saturate(uProgress);

    if (normDist > grow + 0.10)
        return float4(0.0, 0.0, 0.0, 0.0);

    float angle   = atan2(diff.y, diff.x);
    float angNorm = angle / 6.28318 + 0.5;

    // 枝晶脉络: 极坐标噪声锐化为放射状晶脉
    float vein1 = tex2D(uImage0, float2(angNorm * 9.0, normDist * 2.2 - uTime * 0.015)).r;
    float vein2 = tex2D(uImage0, float2(angNorm * 17.0 + 3.1, normDist * 4.0 + uTime * 0.010)).g;
    float veins = smoothstep(0.44, 0.50, vein1) * smoothstep(0.56, 0.50, vein1)
                + smoothstep(0.46, 0.51, vein2) * smoothstep(0.56, 0.51, vein2) * 0.6;

    // 生长前沿亮环
    float front = smoothstep(0.075, 0.0, abs(normDist - grow)) * smoothstep(1.02, 0.85, grow * 0.9 + 0.1);

    // 内部闪晶: 高频噪声阈值 + 时间闪烁
    float sp = tex2D(uImage0, pos * 9.0 + float2(0.0, uTime * 0.02)).b;
    float sparkle = smoothstep(0.78, 0.88, sp) * (0.5 + 0.5 * sin(uTime * 5.0 + sp * 40.0));

    float inner = smoothstep(grow, grow * 0.35, normDist);

    float3 col = uColorPrimary.rgb * (0.55 + veins * 0.5) + uColorSecondary.rgb * (front * 1.3 + sparkle * 0.8);
    float alpha = (inner * (0.26 + veins * 0.22 + sparkle * 0.25) + front * 0.85) * uIntensity;

    return float4(saturate(col * alpha), saturate(alpha));
}

float4 TrapColor(float2 pos)
{
    float3 col = float3(0.0, 0.0, 0.0);
    float alpha = 0.0;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        if (i >= (int)uTrapCount)
            break;
        float4 trap = uTraps[i];
        if (trap.w < -0.5)
            continue;

        float2 tc = float2(trap.x * uAspect, trap.y);
        float dist = length(pos - tc);
        float norm = dist / max(trap.z, 0.001);
        if (norm > 1.12)
            continue;

        float cnt = saturate(trap.w); // 倒计时进度 0=刚放置 1=引爆

        // 外环标界 (恒定, 告知范围)
        float boundary = smoothstep(0.035, 0.0, abs(norm - 1.0));

        // 内缩倒计时环: 半径 = 1-cnt, 越接近引爆越亮
        float ringR = 1.0 - cnt;
        float ring = smoothstep(0.055, 0.0, abs(norm - ringR)) * (0.4 + cnt * 0.8);

        // 中心晶花: 六瓣, 随倒计时绽放
        float angle = atan2(pos.y - tc.y, pos.x - tc.x);
        float petal = pow(abs(cos(angle * 3.0 + uTime * 0.4)), 8.0);
        float flower = petal * smoothstep(0.55, 0.05, norm) * cnt;

        // 末段迫近: 整盘渐白
        float fill = smoothstep(1.0, 0.2, norm) * saturate((cnt - 0.78) / 0.22) * 0.40;

        float n = tex2D(uImage0, (pos - tc) * 5.0 + trap.xy * 11.0).r;

        float3 c = uColorPrimary.rgb * (boundary * 0.9 + ring * 0.5)
                 + uColorSecondary.rgb * (ring * 0.6 * cnt + flower * 0.9 + fill * (0.7 + n * 0.3));
        float a = (boundary * 0.65 + ring * 0.75 + flower * 0.55 + fill) * uIntensity;

        col += c * a;
        alpha = max(alpha, a);
    }

    return float4(saturate(col), saturate(alpha));
}

float4 FrostGroundPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0.0, 0.0, 0.0, 0.0);

    float2 pos = float2(coords.x * uAspect, coords.y);

    if (uMode < 0.5)
    {
        float2 center = float2(uCenter.x * uAspect, uCenter.y);
        return FieldColor(pos, center);
    }
    return TrapColor(pos);
}

technique Technique1
{
    pass FrostGroundPass
    {
        PixelShader = compile ps_3_0 FrostGroundPS();
    }
}
