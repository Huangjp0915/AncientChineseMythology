// ============================================================
// 林地系列专属 — "年轮脉冲" (万木生 / 荣枯页 大招时刻)
// 世界空间 quad 单 pass (非全屏后处理, 不占全屏名额):
//   同心年轮环随 uProgress 外推 + 根须放射线 (噪声扰动) + 中心柔光
// s0 = SoftGlow (中心柔光遮罩), s1 = 可平铺噪声 (ACMShaders.NoiseTexture)
// 建议 BlendState.Additive; 颜色经 uColorInner/uColorOuter 传主题
// (翠绿=巨剑 / 嫩绿金=秘典 / 橙焰=赤铜升级)
// ============================================================

sampler uImage0 : register(s0); // 柔光遮罩 (中心辉光用)
sampler uNoise  : register(s1); // 可平铺三通道噪声

float  uTime;       // 秒
float  uProgress;   // 0→1 演出进度
float  uIntensity;  // 整体强度 0~1
float  uRayCount;   // 根须放射线条数 (建议 10~14)
float4 uColorInner; // 内芯色 (亮)
float4 uColorOuter; // 外沿色 (暗)

float4 VerdantPulsePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 p = (coords - 0.5) * 2.0;      // -1..1
    float dist = length(p);
    float ang = atan2(p.y, p.x);          // -pi..pi

    // 叶脉噪声 (极坐标采样, 让环与射线都带木质纹理)
    float2 veinUV = float2(ang * 0.477 + uTime * 0.02, dist * 0.8 - uTime * 0.03);
    float vein = tex2D(uNoise, veinUV).r;

    // ---- 三道年轮环: 错峰外推, 越外越暗越宽 ----
    float front = saturate(uProgress) * 1.12; // 波前半径
    float rings = 0.0;

    float r0 = saturate(uProgress * 1.15) * 1.05;
    rings += (1.0 - smoothstep(0.0, 0.05, abs(dist - r0))) * (1.0 - r0 * 0.45);

    float r1 = saturate(uProgress * 1.15 - 0.18) * 1.05;
    rings += (1.0 - smoothstep(0.0, 0.075, abs(dist - r1))) * (1.0 - r1 * 0.5) * 0.8;

    float r2 = saturate(uProgress * 1.15 - 0.36) * 1.05;
    rings += (1.0 - smoothstep(0.0, 0.1, abs(dist - r2))) * (1.0 - r2 * 0.55) * 0.6;

    rings *= 0.7 + 0.6 * vein; // 年轮带木纹起伏

    // ---- 根须放射线: 角向锯齿 + 噪声扰动, 只在波前内部生长 ----
    float rayJitter = tex2D(uNoise, float2(ang * 0.955, 0.37)).g; // 每条射线固定扰动
    float rays = pow(abs(sin(ang * uRayCount * 0.5 + rayJitter * 2.6)), 8.0);
    rays *= smoothstep(0.06, 0.22, dist);              // 中心留孔
    rays *= 1.0 - smoothstep(front * 0.85, front, dist); // 不越过波前
    rays *= 0.55 + 0.45 * vein;

    // ---- 中心柔光 (借 s0 遮罩, 起爆时最亮随后衰减) ----
    float glow = tex2D(uImage0, coords).r * (1.0 - smoothstep(0.0, 0.65, uProgress)) * 1.4;

    // ---- 合成: 内外双色 + 边缘淡出 + 进度包络 ----
    float edgeFade = 1.0 - smoothstep(0.72, 1.0, dist);
    float envelope = saturate(uProgress * 6.0) * (1.0 - smoothstep(0.62, 1.0, uProgress));

    float3 ringCol = lerp(uColorInner.rgb, uColorOuter.rgb, saturate(dist * 1.1));
    float3 col = ringCol * rings
               + uColorInner.rgb * rays * 0.75
               + uColorInner.rgb * glow;

    col *= edgeFade * envelope * uIntensity;
    col *= sampleColor.rgb;

    return float4(col, 0); // 加法混合, alpha 置 0
}

technique Technique1
{
    pass VerdantPulsePass
    {
        PixelShader = compile ps_3_0 VerdantPulsePS();
    }
}
