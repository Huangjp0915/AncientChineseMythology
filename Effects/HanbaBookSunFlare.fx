// ============================================================
// 旱日·焦日日轮 — 武器 HanbaBook 专属 (ps_3_0)
// 程序化太阳盘: 白热核 + 极坐标熔面沸腾 + 噪声日冕光舌 + 预坍缩闪烁
// 用法: SpriteBatch(Immediate, Additive) 喂一张方形 quad (SoftGlow 等), uv 0~1
// s1 = 平铺噪声 (ACMShaders.NoiseTexture)
// ============================================================

sampler uImage0 : register(s0); // quad 贴图 (占位, 不采样)
sampler uNoise  : register(s1); // 平铺噪声

float  uTime;      // 秒
float  uIntensity; // 0~1 整体强度
float  uCollapse;  // 0~1 预坍缩 (收缩 + 高频闪烁 — "变小再变响")
float4 uColorHot;  // 白热核心色
float4 uColorEdge; // 熔面/日冕色

float4 SunFlarePS(float2 uv : TEXCOORD0, float4 vc : COLOR0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 p = uv * 2.0 - 1.0;
    float r = length(p);
    if (r > 1.0)
        return float4(0, 0, 0, 0);

    float ang = atan2(p.y, p.x) / 6.28318 + 0.5; // 0~1 角向

    // 预坍缩: 盘半径收缩 + 余弦高频闪烁
    float flicker = 1.0 - uCollapse * (0.35 + 0.07 * cos(uTime * 42.0));
    float disk = 0.52 * flicker; // 日面半径 (uv 半宽比例)

    // 熔面: 极坐标双层噪声 (慢自转 + 沸腾上涌)
    float molten = tex2D(uNoise, float2(ang * 3.0 + uTime * 0.05, r * 1.6 - uTime * 0.11)).r * 0.65
                 + tex2D(uNoise, float2(ang * 7.0 - uTime * 0.03, r * 3.1 + uTime * 0.07)).g * 0.35;

    // 日面主体: 芯部白热, 边缘熔纹压暗
    float inDisk = 1.0 - smoothstep(disk * 0.92, disk, r);
    float core = pow(saturate(1.0 - r / max(disk, 0.001)), 3.0);
    float3 diskCol = lerp(uColorEdge.rgb * (0.5 + 0.6 * molten), uColorHot.rgb, core);

    // 边缘增辉环 (limb brightening)
    float rim = smoothstep(disk * 0.74, disk * 0.96, r) * inDisk;
    diskCol += uColorHot.rgb * rim * (0.45 + 0.4 * molten);

    // 日冕光舌: 盘外沿角向噪声抽光焰, 径向衰减
    float tongue = tex2D(uNoise, float2(ang * 5.0 + uTime * 0.09, r * 0.9 - uTime * 0.22)).r;
    float coronaFall = pow(saturate(1.0 - (r - disk) / max(1.0 - disk, 0.001)), 2.2);
    float corona = coronaFall * step(disk, r) * saturate(tongue * 1.7 - 0.62);

    float3 col = diskCol * inDisk + uColorEdge.rgb * corona * 1.35;
    float alpha = saturate(inDisk + corona);

    col *= vc.rgb;
    alpha *= vc.a * uIntensity;
    return float4(saturate(col * uIntensity), alpha);
}

technique Technique1
{
    pass SunFlarePass
    {
        PixelShader = compile ps_3_0 SunFlarePS();
    }
}
