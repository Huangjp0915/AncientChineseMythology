// ============================================================
// 亡魂EX·弑神裂隙 — 全屏后处理 (占全屏名额, 走 RequestFullscreenSlot)
// 沿给定线段把屏幕"撕开": SDF 线距 + 法向撕扯位移 + 裂隙内红黑神血 + 裂缘辉光 + 色散
// 用于: 屠神刀处决 (短促 0.4s) / 阎罗一刀 (0.7s 全屏横贯)
// 喂 Main.screenTarget(s0) + 可平铺噪声(s1)
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标
sampler uNoise  : register(s1); // 可平铺噪声

float  uTime;       // 动画时间(秒)
float2 uCenter;     // 裂隙线段 A 端 (归一化屏幕坐标)
float2 uPointB;     // 裂隙线段 B 端 (归一化屏幕坐标)
float  uIntensity;  // 整体强度 0~1 (兼作开裂进度)
float  uAspect;     // 宽高比 width/height
float  uWidth;      // 裂隙半宽 (屏幕高度比例, 建议 0.02~0.07)
float4 uTint;       // 裂隙内色 (rgb=神血红黑, a=覆盖权重)
float4 uGlow;       // 裂缘辉光色 (rgb, a=强度)

// 点到线段的距离与沿线参数
float SegDist(float2 p, float2 a, float2 b, out float t)
{
    float2 pa = p - a;
    float2 ba = b - a;
    t = saturate(dot(pa, ba) / max(dot(ba, ba), 0.00001));
    return length(pa - ba * t);
}

float4 DeicideRiftPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return tex2D(uImage0, coords);

    // 纵横比校正空间
    float2 pos = float2(coords.x * uAspect, coords.y);
    float2 a   = float2(uCenter.x * uAspect, uCenter.y);
    float2 b   = float2(uPointB.x * uAspect, uPointB.y);

    float t;
    float dist = SegDist(pos, a, b, t);

    float2 ba = b - a;
    float2 lineDir = normalize(ba + 0.00001);
    float2 normal  = float2(-lineDir.y, lineDir.x);
    float side = sign(dot(pos - a, normal)); // 在线哪一侧

    // 裂隙宽度沿线两端收尖 + 噪声锯齿边
    float endTaper = smoothstep(0.0, 0.12, t) * smoothstep(1.0, 0.88, t);
    float jag = tex2D(uNoise, float2(t * 5.0 + uTime * 0.07, 0.35)).r;
    float w = uWidth * uIntensity * endTaper * (0.65 + jag * 0.7);

    float riftMask = 1.0 - smoothstep(0.0, max(w, 0.0001), dist);          // 裂隙内部
    float nearMask = 1.0 - smoothstep(0.0, max(w * 6.0, 0.0005), dist);    // 近场 (位移/色散范围)

    // 远处早退
    if (nearMask <= 0.001)
        return tex2D(uImage0, coords);

    // —— 法向撕扯位移: 两侧场景被推离裂隙 ——
    float push = nearMask * nearMask * uIntensity * 0.028;
    float2 uvOffset = normal * side * push;
    uvOffset.x /= uAspect;
    float2 tornUV = clamp(coords + uvOffset, 0.001, 0.999);
    float4 scene = tex2D(uImage0, tornUV);

    // —— 裂缘 RGB 色散 ——
    float chroma = nearMask * uIntensity * 0.012;
    float2 chromaOff = normal * side * chroma;
    chromaOff.x /= uAspect;
    scene.r = lerp(scene.r, tex2D(uImage0, tornUV + chromaOff).r, 0.75 * uIntensity);
    scene.b = lerp(scene.b, tex2D(uImage0, tornUV - chromaOff).b, 0.75 * uIntensity);

    // —— 裂隙内部: 红黑神血涌动 ——
    float2 innerUV = float2(t * 3.0 - uTime * 0.35, dist / max(w, 0.0001) * 0.8 + uTime * 0.10);
    float blood = tex2D(uNoise, innerUV).g;
    float3 innerCol = uTint.rgb * (0.35 + blood * 0.9);
    // 裂隙最深处压黑 (神躯之外无物)
    float depth = 1.0 - smoothstep(0.0, max(w * 0.55, 0.0001), dist);
    innerCol = lerp(innerCol, innerCol * 0.12, depth * 0.8);
    scene.rgb = lerp(scene.rgb, innerCol, riftMask * uTint.a * uIntensity);

    // —— 裂缘辉光 (贴边最亮, 向外衰减) ——
    float edgeBand = smoothstep(w * 2.6, w * 0.9, dist) * (1.0 - riftMask * 0.55);
    scene.rgb += uGlow.rgb * (edgeBand * uGlow.a * uIntensity * endTaper);

    return scene;
}

technique Technique1
{
    pass DeicideRiftPass
    {
        PixelShader = compile ps_3_0 DeicideRiftPS();
    }
}
