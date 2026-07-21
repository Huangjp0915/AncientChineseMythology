// ============================================================
// 朱雀热浪扭曲着色器 — 全屏后处理
// 本体周围径向热浪 UV 扭曲 + 上升对流条纹 + 轻微热色散。
// 必须走 ACMShaders.RequestFullscreenSlot() 名额契约;
// 涅槃 PaletteLUT grade 生效时由调用方让位 (同帧只申请一个名额)。
// s0 = Main.screenTarget, s1 = ACMShaders.NoiseTexture。
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标
sampler uNoise  : register(s1); // 可平铺噪声 (RGB 三通道)

float uTime;      // 动画时间 (秒)
float2 uCenter;   // 本体归一化屏幕坐标 (0~1)
float uIntensity; // 整体强度 (0~1)
float uRadius;    // 热浪有效半径 (屏幕高度比例)
float uAspect;    // 屏幕宽高比 (width / height)

float4 HeatHazePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.002)
        return tex2D(uImage0, coords);

    // 宽高比校正距离
    float2 pos = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff = pos - center;
    float dist = length(diff);
    float normDist = dist / max(uRadius, 0.001);

    float falloff = smoothstep(2.3, 0.15, normDist);
    if (falloff < 0.003)
        return tex2D(uImage0, coords);

    // ==========================================
    //  上升对流 — 两层差频噪声向上滚动
    // ==========================================
    float2 cuv1 = float2(coords.x * 3.0, coords.y * 4.0 - uTime * 0.90);
    float2 cuv2 = float2(coords.x * 6.0 + 0.31, coords.y * 7.0 - uTime * 1.60);
    float n1 = tex2D(uNoise, cuv1).r;
    float n2 = tex2D(uNoise, cuv2).g;
    float conv = n1 * 0.65 + n2 * 0.35;

    // ==========================================
    //  UV 扭曲 — 径向热浪 + 垂直对流抖动
    // ==========================================
    float2 radialDir = diff / max(dist, 0.0001);
    float wobble = (conv - 0.5) * 2.0;
    float2 off = radialDir * wobble * 0.011
               + float2((n2 - 0.5) * 0.009, -(n1 - 0.5) * 0.014);
    off *= uIntensity * falloff;
    off.x /= uAspect;

    float2 uv = clamp(coords + off, 0.001, 0.999);
    float4 scene = tex2D(uImage0, uv);

    // ==========================================
    //  热折射色散 — RGB 轻微错位
    // ==========================================
    float ca = uIntensity * falloff * 0.004;
    float2 caOff = float2(ca / uAspect, 0.0);
    scene.r = lerp(scene.r, tex2D(uImage0, clamp(uv + caOff, 0.001, 0.999)).r, 0.6);
    scene.b = lerp(scene.b, tex2D(uImage0, clamp(uv - caOff, 0.001, 0.999)).b, 0.6);

    // ==========================================
    //  对流条纹微亮 + 近核暖化
    // ==========================================
    float stripe = smoothstep(0.58, 0.92, conv) * falloff * uIntensity;
    scene.rgb += float3(1.0, 0.55, 0.20) * stripe * 0.05;
    scene.rgb = lerp(scene.rgb, scene.rgb * float3(1.05, 0.99, 0.93), falloff * uIntensity * 0.5);

    return scene;
}

technique Technique1
{
    pass HeatHazePass
    {
        PixelShader = compile ps_3_0 HeatHazePS();
    }
}
