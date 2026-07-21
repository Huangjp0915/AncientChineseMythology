// ============================================================
// 敖闰西海冰镜着色器 — 屏幕空间镜面面板 (s0=共享噪声)
// 一次 pass 绘制 ≤6 面菱形冰镜: SDF 面板 + 内部视差微光 + 边缘 glint
// 扫掠 + 蓄光升亮 + 出口白亮(charge>1 编码)
// 由镜面弹幕帧守卫合批调用 (每帧仅第一面镜触发一次绘制)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声

float  uTime;
float  uAspect;
float  uIntensity;
float4 uMirrors[6];     // xy=屏幕UV z=朝向(rad, 发射轴) w=状态: <0 无效; 0~1 生长/蓄光; >1 出口白亮(w-1)
float  uMirrorCount;
float  uSize;           // 镜面半长 (屏幕高度比例)
float4 uColorPrimary;   // 面板底色 (深海蓝)
float4 uColorSecondary; // 边缘高光 (冰白)

float4 MirrorPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0.0, 0.0, 0.0, 0.0);

    float2 pos = float2(coords.x * uAspect, coords.y);
    float3 col = float3(0.0, 0.0, 0.0);
    float alpha = 0.0;

    [unroll]
    for (int i = 0; i < 6; i++)
    {
        if (i >= (int)uMirrorCount)
            break;
        float4 m = uMirrors[i];
        if (m.w < -0.5)
            continue;

        float grow  = saturate(m.w);              // 0~1 生长兼蓄光
        float exalt = saturate(m.w - 1.0);        // >1 部分 = 出口白亮

        float2 mc = float2(m.x * uAspect, m.y);
        float2 d  = pos - mc;

        // 旋转到镜面局部空间: local.x = 发射轴, local.y = 镜面长轴
        float cs = cos(-m.z);
        float sn = sin(-m.z);
        float2 local = float2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);

        // 菱形透镜 SDF: 长轴(y)=uSize, 短轴(x)=0.42*uSize, 随生长展开
        float sz = uSize * (0.25 + 0.75 * grow);
        float2 q = float2(abs(local.x) / (sz * 0.42), abs(local.y) / sz);
        float sdf = q.x + q.y - 1.0;

        if (sdf > 0.30)
            continue;

        float fill = smoothstep(0.0, -0.30, sdf);
        float rim  = smoothstep(0.085, 0.0, abs(sdf));

        // 内部视差微光: 噪声随时间缓移, 每镜相位独立
        float n = tex2D(uImage0, local * 3.0 + m.xy * 7.0 + float2(uTime * 0.03, -uTime * 0.02)).g;
        float shimmer = 0.35 + n * 0.55;

        // glint 光带沿长轴扫掠
        float sweep = frac(uTime * 0.35 + (float)i * 0.37);
        float band = smoothstep(0.14, 0.0, abs(local.y / (sz * 2.0) + 0.5 - sweep)) * fill;

        // 蓄光瞄准线: 沿发射轴的白热窄线, 蓄光后段显现
        float sight = smoothstep(0.055, 0.0, abs(local.y) / sz) * fill * saturate((grow - 0.70) / 0.30);

        float3 c = uColorPrimary.rgb * fill * shimmer * (0.55 + grow * 0.25)
                 + uColorSecondary.rgb * (rim * (0.85 + exalt * 0.9) + band * 0.30 + sight * 0.95)
                 + uColorSecondary.rgb * fill * exalt * 0.85;

        float a = (fill * (0.34 + grow * 0.20 + exalt * 0.45) + rim * 0.70 + sight * 0.55) * uIntensity;

        col += c * a;
        alpha = max(alpha, a);
    }

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass MirrorPass
    {
        PixelShader = compile ps_3_0 MirrorPS();
    }
}
