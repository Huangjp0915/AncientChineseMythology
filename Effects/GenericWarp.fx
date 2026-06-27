// ============================================================
// 泛化主题扭曲着色器 — 全屏后处理
// 噪声驱动 UV 偏移 + RGB 色散 + 主题染色, 从中心径向衰减
// 主题由参数切换(不重写): 热浪/霜冻/雾/裂隙/虚空/折射
//   uMode: 0=heat 1=frost 2=fog 3=rift 4=void 5=refraction (仅微调形态)
//   连续主题靠 uTint / uChroma / uWarpScale / uRadialPull 驱动
// 喂 Main.screenTarget(s0) + 可平铺噪声(s1)
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标
sampler uNoise  : register(s1); // 可平铺噪声

float  uTime;        // 动画时间 (秒)
float2 uCenter;      // 效果中心归一化屏幕坐标 0~1
float  uIntensity;   // 整体强度 0~1
float  uRadius;      // 有效半径 (屏幕高度比例)
float  uAspect;      // 宽高比 width/height
float  uWarpScale;   // 扭曲幅度 (建议 0.5~2)
float  uChroma;      // 色散强度 (建议 0~1)
float  uRadialPull;  // 径向吸入(虚空/裂隙) (-1~1, 负=向外推)
float  uMode;        // 主题形态档位
float4 uTint;        // 主题染色 (rgb=色, a=覆盖强度)

float4 GenericWarpPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return tex2D(uImage0, coords);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  dist   = length(diff);
    float  normDist = dist / max(uRadius, 0.001);

    // 远处早退
    if (normDist > 2.5)
        return tex2D(uImage0, coords);

    float2 radialDir  = normalize(diff + 0.0001);
    float2 tangentDir = float2(-radialDir.y, radialDir.x);

    // 多层噪声
    float angle   = atan2(diff.y, diff.x);
    float angNorm = angle / 6.28318 + 0.5;
    float scale   = max(uWarpScale, 0.001);

    float2 n1UV = float2(angNorm * 4.0 * scale + uTime * 0.04, normDist * 2.0 - uTime * 0.03);
    float2 n2UV = float2(angNorm * 6.0 * scale - uTime * 0.05, normDist * 3.0 + uTime * 0.025);
    float2 n3UV = coords * 2.0 * scale + float2(uTime * 0.02, -uTime * 0.02);

    float n1 = tex2D(uNoise, n1UV).r;
    float n2 = tex2D(uNoise, n2UV).g;
    float n3 = tex2D(uNoise, n3UV).b;
    float fbm = n1 * 0.5 + n2 * 0.3 + n3 * 0.2;

    // 扭曲衰减(近强远弱)
    float falloff = smoothstep(2.0, 0.0, normDist);
    float strength = uIntensity * 0.045 * scale * falloff;

    float radialOffset  = (fbm - 0.5) * 2.0;
    float tangentOffset = (n2 - 0.5) * 2.0;

    // 径向吸入/外推(虚空 void / 裂隙 rift)
    float pull = uRadialPull * smoothstep(2.2, 0.1, normDist) * uIntensity * 0.08;

    float2 uvOffset = (radialDir * radialOffset + tangentDir * tangentOffset * 0.6) * strength
                    - radialDir * pull;
    uvOffset.x /= uAspect; // 反校正回UV空间

    float2 distortedUV = clamp(coords + uvOffset, 0.001, 0.999);
    float4 sceneColor  = tex2D(uImage0, distortedUV);

    // RGB 色散
    float chromaStr = uIntensity * 0.012 * uChroma * falloff;
    float2 chromaOffset = radialDir * chromaStr;
    chromaOffset.x /= uAspect;
    float rCh = tex2D(uImage0, distortedUV + chromaOffset).r;
    float bCh = tex2D(uImage0, distortedUV - chromaOffset).b;
    sceneColor.r = lerp(sceneColor.r, rCh, uChroma * uIntensity * 0.7);
    sceneColor.b = lerp(sceneColor.b, bCh, uChroma * uIntensity * 0.7);

    // 主题染色 — 近处更浓
    float tintCover = smoothstep(1.6, 0.1, normDist) * uTint.a * uIntensity;
    sceneColor.rgb = lerp(sceneColor.rgb, uTint.rgb, tintCover * 0.35);

    // 虚空档(uMode~4): 中心压暗成黑洞
    float voidMask = saturate(1.0 - abs(uMode - 4.0));
    float darken = voidMask * smoothstep(0.9, 0.0, normDist) * uIntensity;
    sceneColor.rgb *= lerp(1.0, 0.12, darken);

    // 雾档(uMode~2): 抬整体覆盖, 降对比
    float fogMask = saturate(1.0 - abs(uMode - 2.0));
    float fogCover = fogMask * smoothstep(2.2, 0.2, normDist) * uIntensity * 0.25 * uTint.a;
    sceneColor.rgb = lerp(sceneColor.rgb, uTint.rgb, fogCover);

    return sceneColor;
}

technique Technique1
{
    pass GenericWarpPass
    {
        PixelShader = compile ps_3_0 GenericWarpPS();
    }
}
