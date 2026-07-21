// ============================================================
// 神威·断罪刃 — 处决裁决闪 (impact frame)
// 全屏后处理: 黑白高对比 + 中心斩线过曝 + 暗角
// s0 = Main.screenTarget (经 ACMShaders.ApplyScreenPostProcess 喂入)
// 全屏名额契约: 仅换阶段3首秀(低强度)与死亡定格(满强度)两处使用
// ============================================================

sampler uImage0 : register(s0); // screenTarget

float  uTime;      // 动画时间(秒)
float  uIntensity; // 0~1: 0=原图, 1=完全裁决闪
float  uAspect;    // 宽高比 width/height
float2 uCenter;    // 斩线中心 (归一化屏幕坐标)
float  uSlashAng;  // 斩线角度(弧度, 0=竖直)

float4 ExecutionFlashPS(float4 color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 src = tex2D(uImage0, coords);
    if (uIntensity < 0.01)
        return src;

    // —— 黑白高对比: 亮度阈值二分, 中段陡峭 S 曲线 ——
    float luma = dot(src.rgb, float3(0.299, 0.587, 0.114));
    float bw = smoothstep(0.32, 0.62, luma);          // 阈值二分
    bw = saturate((bw - 0.5) * 2.6 + 0.5);            // 对比再拉陡
    // 白底带一点金, 黑底带一点铁蓝 — 不是纯灰度, 保留"断罪金"身份
    float3 mono = lerp(float3(0.03, 0.03, 0.06), float3(1.0, 0.97, 0.88), bw);

    // —— 中心斩线: 过曝亮刃 + 窄晕 ——
    float2 pos = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 d = pos - center;
    // 旋转到斩线坐标系 (斩线沿局部 y 轴)
    float ca = cos(uSlashAng);
    float sa = sin(uSlashAng);
    float across = abs(d.x * ca - d.y * sa);           // 垂直斩线的距离
    float along = d.x * sa + d.y * ca;                 // 沿斩线的位置
    float lenMask = 1.0 - smoothstep(0.55, 0.95, abs(along));
    float blade = pow(saturate(1.0 - across / 0.012), 3.0) * lenMask;      // 刃芯
    float halo = pow(saturate(1.0 - across / 0.10), 2.0) * lenMask * 0.35; // 刃晕
    // 刃芯轻微闪烁 (定格期间的"能量嘶鸣")
    blade *= 0.85 + 0.15 * sin(uTime * 60.0);

    float3 flashCol = mono + (blade * 1.6 + halo) * float3(1.0, 0.98, 0.9);

    // —— 暗角: 四周压黑, 视线聚拢到斩线 ——
    float2 vd = coords - 0.5;
    float vig = saturate(1.0 - dot(vd, vd) * 1.7);
    flashCol *= lerp(1.0, vig, 0.75);

    float3 outCol = lerp(src.rgb, flashCol, saturate(uIntensity));
    return float4(outCol, src.a);
}

technique Technique1
{
    pass ExecutionFlashPass
    {
        PixelShader = compile ps_3_0 ExecutionFlashPS();
    }
}
