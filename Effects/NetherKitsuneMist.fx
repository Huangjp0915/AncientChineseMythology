// ============================================================
// 幽冥妖狐 · 冥雾体积后处理 (NetherKitsuneMist) — 全屏后处理
// 三层视差 FBM + 域扭曲的体积雾; 密度即攻击节拍 (浓=蓄势, 骤清=爆发);
// uGhost 冥蓝<->鬼绿换色 (P3 怨决); uFreeze 雾冻结 (死亡演出顿帧);
// 玩家周围挖清晰洞保证可读性; 覆盖率设上限, 场景永不完全淹没。
// s0 = Main.screenTarget, s1 = 共享 FBM 噪声 (ACMShaders.NoiseTexture)
// ============================================================

sampler uImage0 : register(s0);
sampler uNoise  : register(s1);

float uTime;         // 动画时间 (秒)
float uDensity;      // 雾密度 0~1.15 (呼吸主参数)
float uGhost;        // 0=冥蓝 1=鬼绿 (P3 怨念显形)
float uFreeze;       // 0~1 雾流动冻结 (死亡演出)
float2 uClearCenter; // 玩家屏幕 UV (可读性挖洞中心)
float uClearRadius;  // 挖洞半径 (屏幕高度比例)
float uAspect;       // 屏幕宽高比
float2 uWind;        // 雾整体流向 (随 Boss 移动微偏)

static const float3 MistDeep  = float3(0.055, 0.085, 0.16);  // 雾影深处
static const float3 MistBlue  = float3(0.30, 0.46, 0.72);    // 冥蓝主体
static const float3 MistPale  = float3(0.60, 0.78, 0.95);    // 雾亮部
static const float3 GhostDeep = float3(0.04, 0.12, 0.085);   // 鬼绿深处
static const float3 GhostLit  = float3(0.42, 0.86, 0.56);    // 鬼绿亮部

float4 MistPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float density = saturate(uDensity);
    if (density < 0.005)
        return tex2D(uImage0, coords);

    // 冻结: 时间慢放到近停 (雾滞住的死寂感)
    float t = uTime * (1.0 - uFreeze * 0.96);
    float2 p = float2(coords.x * uAspect, coords.y);

    // ---- 域扭曲 + 三层视差 FBM ----
    float2 warp = tex2D(uNoise, p * 0.9 + float2(t * 0.021, -t * 0.013)).rg - 0.5;
    float n1 = tex2D(uNoise, p * 1.15 + warp * 0.30 + uWind * t * 0.030).r;
    float n2 = tex2D(uNoise, p * 2.10 - warp * 0.22 + uWind * t * 0.052 + 0.37).g;
    float n3 = tex2D(uNoise, p * 3.60 + warp * 0.15 - uWind * t * 0.074 + 0.71).b;
    float mist = n1 * 0.50 + n2 * 0.32 + n3 * 0.18;

    // 底部沉降 + 慢呼吸
    mist += (coords.y - 0.35) * 0.22;
    mist += sin(t * 0.9) * 0.03;

    // 密度重映射: 密度越高雾越连片
    float cover = smoothstep(0.62 - density * 0.42, 0.95 - density * 0.25, mist);

    // ---- 玩家周围挖洞 (可读性保护) ----
    float2 cd = float2((coords.x - uClearCenter.x) * uAspect, coords.y - uClearCenter.y);
    float clearHole = smoothstep(uClearRadius * 0.55, uClearRadius * 1.55, length(cd));
    cover *= lerp(0.30, 1.0, clearHole);

    // ---- 场景采样 (雾梯度轻微折射) ----
    float2 uvOff = warp * 0.010 * density * cover;
    uvOff.x /= uAspect;
    float4 scene = tex2D(uImage0, clamp(coords + uvOff, 0.001, 0.999));

    // ---- 雾色 (冥蓝 <-> 鬼绿) ----
    float3 deep = lerp(MistDeep, GhostDeep, uGhost);
    float3 lit  = lerp(lerp(MistBlue, MistPale, n2), lerp(GhostDeep * 2.0, GhostLit, n2), uGhost);
    float3 mistCol = lerp(deep, lit, saturate(mist * 0.9 + 0.15));

    // ---- 浓雾下场景去饱和 + 冷染 ----
    float grey = dot(scene.rgb, float3(0.30, 0.55, 0.15));
    float3 coldCast = lerp(float3(0.75, 0.85, 1.05), float3(0.78, 1.02, 0.85), uGhost);
    scene.rgb = lerp(scene.rgb, grey * coldCast, density * 0.45);

    // ---- 雾覆盖 (上限 0.62, 场景永远保底可读) ----
    float alpha = cover * density * 0.62;
    scene.rgb = lerp(scene.rgb, mistCol, alpha);

    // ---- 顶部暗角 (压迫感) ----
    float vign = smoothstep(0.25, 0.0, coords.y) * density * 0.28;
    scene.rgb = lerp(scene.rgb, deep * 0.6, vign);

    return scene;
}

technique Technique1
{
    pass MistPass
    {
        PixelShader = compile ps_3_0 MistPS();
    }
}
