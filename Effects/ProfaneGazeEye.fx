// ============================================================
// 亵渎系列 · 凝视之眼着色器 (武器线专属)
// 程序化血肉之眼: 噪声撕裂眼睑 + 血丝巩膜 + 金瞳虹膜纤维
// + uLock 竖瞳收缩/致命红环/微震颤 + uGazeDir 瞳孔追踪
// 用途: 凝视肉典巨眼 / 畸变眼球弹 / 搐筋弓眼箭 / 摘取处决眼闪
// 喂共享可平铺噪声 (s0), Additive 混合 (输出预乘 rgb)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声

float  uTime;       // 动画秒
float  uOpen;       // 眼睑开合 0~1 (0=闭合不绘制)
float  uLock;       // 锁定态 0~1 (竖瞳收缩 + 红环 + 震颤)
float2 uGazeDir;    // 视线偏移 (-1~1, 已含幅度)
float  uIntensity;  // 整体强度 0~1
float  uSeed;       // 每眼随机相位
float4 uIrisColor;  // 虹膜金瞳 (250,208,130)
float4 uScleraColor;// 巩膜底色 (苍白肉 235,190,170)
float4 uVeinColor;  // 血丝色 (248,64,96)

static const float3 LethalRed = float3(0.98, 0.16, 0.22); // 处决红 (对齐 LethalRed 主题)

float4 GazeEyePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01 || uOpen < 0.01)
        return float4(0, 0, 0, 0);

    float2 p = coords - 0.5;

    // 锁定震颤 — 瞳孔"咬死"目标的紧绷感
    float2 jitter = float2(sin(uTime * 43.0 + uSeed * 19.0), cos(uTime * 37.0 + uSeed * 7.0)) * 0.005 * uLock;
    p += jitter;

    // 杏仁眼形 + 噪声撕裂眼睑边 (血肉之眼的睑缘不是光滑弧线)
    float xn = saturate((p.x + 0.46) / 0.92);
    float ragged = (tex2D(uImage0, float2(xn * 2.0 + uSeed, uSeed * 3.7)).r - 0.5) * 0.05;
    float lidH = uOpen * (0.30 * pow(abs(sin(3.14159 * xn)), 0.7) + ragged);
    float lid = smoothstep(lidH, lidH - 0.06, abs(p.y));
    if (lid <= 0.001)
        return float4(0, 0, 0, 0);

    // 巩膜: 苍白肉底光, 眼心向外衰减
    float rEye = length(p * float2(1.0, 1.55));
    float sclera = pow(saturate(1.0 - rEye / 0.52), 1.5);

    // 巩膜血丝: 从眼周向瞳孔爬行的 ridged 枝状红纹, 越靠边缘越密
    float angS = atan2(p.y, p.x);
    float veinN = tex2D(uImage0, float2(angS * 0.954930 + uSeed * 4.0, rEye * 1.6 - uTime * 0.02)).b;
    float vein = smoothstep(0.72, 0.95, 1.0 - abs(veinN * 2.0 - 1.0));
    float veinW = vein * smoothstep(0.10, 0.45, rEye) * (0.6 + 0.6 * uLock);

    // 虹膜 — 中心随视线偏移
    float2 pupilC = uGazeDir * 0.13;
    float2 q = p - pupilC;
    float rIris = length(q);
    float irisR = 0.175;
    float iris = smoothstep(irisR, irisR - 0.035, rIris);

    // 虹膜径向纤维 (极坐标噪声, u 跨度整数保证接缝连续)
    float2 qa = rIris < 0.0001 ? float2(1.0, 0.0) : q;
    float ang = atan2(qa.y, qa.x);
    float fiber = tex2D(uImage0, float2(ang * 1.27324 + uSeed, rIris * 2.2 - uTime * 0.06)).r;
    fiber = 0.5 + 0.95 * fiber;

    // 瞳孔: 锁定时圆瞳 → 竖裂瞳 (捕食者收缩)
    float slit = lerp(1.0, 3.4, uLock);                    // 横向压扁比
    float rPupil = length(q * float2(slit, 1.0));
    float pupilR = lerp(0.085, 0.055, uLock);
    float pupil = smoothstep(pupilR - 0.022, pupilR + 0.018, rPupil);

    // 锁定致命红环 (虹膜外缘)
    float ring = 1.0 - smoothstep(0.0, 0.05, abs(rIris - irisR * 1.16));

    // 组色: 巩膜肉光 → 血丝 → 虹膜金瞳 → 竖瞳挖黑 → 红环
    float3 col = uScleraColor.rgb * sclera * 0.42;
    col += uVeinColor.rgb * veinW * sclera;
    float3 irisCol = lerp(uIrisColor.rgb, uVeinColor.rgb, saturate(rIris / irisR) * 0.55) * fiber;
    irisCol = lerp(irisCol, LethalRed, uLock * 0.55);
    col = lerp(col, irisCol, iris);
    col *= pupil;
    col += LethalRed * ring * uLock * 0.95;

    // 湿润高光 (瞳孔左上一点反光, 让眼"活")
    float glint = pow(saturate(1.0 - length(q - float2(-0.045, -0.05)) / 0.05), 2.0);
    col += float3(1.0, 0.95, 0.9) * glint * 0.6 * iris;

    float alpha = lid * uIntensity;
    return float4(col * alpha * sampleColor.a, 0);
}

technique Technique1
{
    pass GazeEyePass
    {
        PixelShader = compile ps_3_0 GazeEyePS();
    }
}
