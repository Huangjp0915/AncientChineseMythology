// ============================================================
// 亡魂系列·业镜刃迹条带 — TriangleStrip 挥砍刃迹 / 居合刀痕 / 镜面刃晕
// 镜面拉丝高光 + 折影重像(uv 双像偏移) + uHeat 热度(青黄→朱红白热) + 业火斑驳
// 顶点由 ACMUtils.BuildRibbonStrip 提供 (uv.x=沿长 0头1尾, uv.y=横宽 0~1)
// 仅 PS, 顶点变换沿用 SpriteBatch 外部矩阵 (同 BeamGrad/YingouBladeRibbon 约定)
// s0 = 刃光纹理(SlashBurst/噪声均可), s1 = 可平铺流动噪声
// ============================================================

sampler uImage0 : register(s0); // 刃光纹理
sampler uNoise  : register(s1); // 可平铺噪声 (RGB 三通道独立)

float  uTime;        // 动画时间(秒)
float  uIntensity;   // 整体强度 0~1 (兼作淡入淡出)
float4 uColorCore;   // 芯色 (rgb, a=芯部不透明度权重)
float4 uColorEdge;   // 缘色 (rgb, a=缘部不透明度权重)
float  uHeat;        // 0~1 业热: 芯部向判决朱红/白热偏移 (蓄势→处决用)
float  uGhost;       // 0~1 折影强度: 镜中重像的可见度 (孽镜蓄影/断业残痕用)
float  uFlowSpeed;   // 流动速度
float  uFlowScale;   // 流动纹理尺度
float  uCoreSharp;   // 芯部收窄锐度 (1~5)
float  uTaper;       // 尾端衰减幂 (1=线性, 2~3=快收)

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

// 单条刃形轮廓: 给定横向坐标 y(0~1) 返回 (芯轮廓, 体轮廓)
float2 BladeProfile(float y)
{
    float edgeDist = abs(y - 0.5) * 2.0;
    float core = pow(saturate(1.0 - edgeDist), max(uCoreSharp, 0.001));
    float body = saturate(1.0 - edgeDist * edgeDist);
    return float2(core, body);
}

float4 PS_KarmaRibbon(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;

    // 沿长流动: 双八度噪声 "镜纹拉丝"
    float2 f1 = float2(uv.x * max(uFlowScale, 0.001) - uTime * uFlowSpeed, uv.y * 0.30);
    float2 f2 = float2(uv.x * max(uFlowScale, 0.001) * 2.1 - uTime * uFlowSpeed * 1.7, uv.y * 0.22 + 0.41);
    float flow = tex2D(uNoise, f1).r * 0.6 + tex2D(uNoise, f2).g * 0.4;

    // 主刃轮廓
    float2 pMain = BladeProfile(uv.y);

    // 折影重像: 镜中两道错位残影 (轻微沿长错相, 模拟镜面折射的重影)
    float ghostShift = 0.16 + 0.03 * sin(uTime * 3.0);
    float2 pG1 = BladeProfile(uv.y + ghostShift);
    float2 pG2 = BladeProfile(uv.y - ghostShift);
    float ghost = (pG1.x + pG2.x) * 0.5 * saturate(uGhost);

    // 镜面拉丝高光: 沿长的锐利闪条 (镜光扫过)
    float glint = pow(abs(sin(uv.x * 22.0 - uTime * 7.0 + flow * 4.0)), 10.0);
    glint *= pMain.y * 0.8;

    // 刃光纹理叠底
    float slash = tex2D(uImage0, float2(uv.x * 0.9 - uTime * uFlowSpeed * 0.5, uv.y)).r;

    // 尾端衰减 (uv.x=0 刃头): 头部略收口
    float fade = pow(saturate(1.0 - uv.x), max(uTaper, 0.2));
    fade *= smoothstep(0.0, 0.03, uv.x) * 0.35 + 0.65;

    // 芯↔缘渐变; 业热把芯部推向判决朱红→白热
    float3 col = lerp(uColorEdge.rgb, uColorCore.rgb, pMain.x);
    float3 verdictHot = lerp(float3(0.98, 0.16, 0.22), float3(1.0, 0.93, 0.86), saturate(uHeat) * 0.6);
    col = lerp(col, verdictHot, saturate(uHeat) * pMain.x);
    col *= (0.55 + 0.75 * flow);
    col += uColorCore.rgb * pMain.x * pMain.x * (0.45 + uHeat * 1.0); // 芯部加法过曝
    col += uColorCore.rgb * glint * (0.6 + uHeat * 0.5);              // 镜光闪条
    // 折影: 冷紫幽影 (与主刃色错开, 读成"镜中之像")
    col += float3(0.55, 0.38, 0.95) * ghost * 0.65;
    col *= 0.78 + 0.44 * slash;

    float alpha = pMain.y * fade;
    alpha = saturate(alpha + ghost * 0.55 * fade);
    alpha *= lerp(uColorEdge.a, uColorCore.a, pMain.x);
    alpha *= uIntensity;

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass KarmaRibbonPass
    {
        PixelShader = compile ps_3_0 PS_KarmaRibbon();
    }
}
