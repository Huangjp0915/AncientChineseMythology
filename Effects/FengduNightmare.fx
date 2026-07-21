// ============================================================
// 酆都罗生门噩梦 — 全屏后处理 (酆都武器系列专属, 大招短暂定调 ≤30 帧)
// 屏幕边缘噩梦暗纹蠕动涌入 + 轻向心收缩采样 + 径向色差
// 中心 ~40% 保持清晰 (可读性阀门: 特效不遮蔽战场)
// 喂 Main.screenTarget(s0) + 可平铺噪声(s1)
// 必走 ACMShaders.RequestFullscreenSlot 名额契约
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标
sampler uNoise  : register(s1); // 可平铺噪声

float  uTime;        // 动画时间(秒)
float2 uCenter;      // 效果中心归一化屏幕坐标 0~1
float  uIntensity;   // 整体强度 0~1
float  uAspect;      // 宽高比 width/height
float  uPull;        // 向心收缩采样强度 (建议 0~1)
float4 uTint;        // 噩梦染色 (rgb=黑紫, a=覆盖权重)

float4 FengduNightmarePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return tex2D(uImage0, coords);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  dist   = length(diff);

    // 半屏高为单位的归一距离 (0=中心, ~1=屏缘)
    float normDist = dist / 0.62;
    float2 radialDir = normalize(diff + 0.0001);

    // 中心保护: 40% 内几乎不动 (可读性)
    float edgeZone = smoothstep(0.40, 1.15, normDist);

    // 轻向心收缩采样 (整屏被门"吸"了一口)
    float pull = uPull * uIntensity * 0.045 * edgeZone;
    float2 warpUV = coords - radialDir * pull * float2(1.0 / uAspect, 1.0);
    warpUV = clamp(warpUV, 0.001, 0.999);

    float4 sceneColor = tex2D(uImage0, warpUV);

    // 径向色差 (仅边缘区)
    float chroma = uIntensity * 0.010 * edgeZone;
    float2 chromaOff = radialDir * chroma * float2(1.0 / uAspect, 1.0);
    sceneColor.r = lerp(sceneColor.r, tex2D(uImage0, clamp(warpUV + chromaOff, 0.001, 0.999)).r, 0.8);
    sceneColor.b = lerp(sceneColor.b, tex2D(uImage0, clamp(warpUV - chromaOff, 0.001, 0.999)).b, 0.8);

    // 边缘噩梦暗纹: 双八度 fbm 蠕动, 由屏缘向内涌
    float angle   = atan2(diff.y, diff.x);
    float angNorm = angle / 6.28318 + 0.5;
    float n1 = tex2D(uNoise, float2(angNorm * 3.0 + uTime * 0.05, normDist * 1.4 - uTime * 0.09)).r;
    float n2 = tex2D(uNoise, float2(angNorm * 5.0 - uTime * 0.04, normDist * 2.6 + uTime * 0.06)).g;
    float fbm = n1 * 0.62 + n2 * 0.38;

    // 涌入锋面: 噪声扰动的暗纹边界, 强度越高涌入越深
    float veilEdge = 1.05 - uIntensity * 0.38 + (fbm - 0.5) * 0.30;
    float veil = smoothstep(veilEdge, veilEdge + 0.45, normDist);

    // 噩梦暗纹压暗 + 染紫
    float cover = veil * uTint.a * uIntensity;
    sceneColor.rgb = lerp(sceneColor.rgb, uTint.rgb, cover * 0.55);
    sceneColor.rgb *= 1.0 - cover * 0.42;

    // 暗纹内的蠕动亮丝 (紫色噩梦脉络, 很淡)
    float veinN = tex2D(uNoise, float2(angNorm * 8.0 + uTime * 0.07, normDist * 3.2 - uTime * 0.12)).b;
    float veins = smoothstep(0.80, 0.96, veinN) * veil * uIntensity;
    sceneColor.rgb += uTint.rgb * veins * 0.65;

    return sceneColor;
}

technique Technique1
{
    pass FengduNightmarePass
    {
        PixelShader = compile ps_3_0 FengduNightmarePS();
    }
}
