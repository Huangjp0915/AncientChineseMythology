using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics;

namespace AncientChineseMythology
{
    internal static class ACMUtils
    {
        public static Vector2 SafeDirectionTo(this Entity entity, Vector2 destination, Vector2? fallback = null) {
            if (!fallback.HasValue)
                fallback = Vector2.Zero;

            return (destination - entity.Center).SafeNormalize(fallback.Value);
        }

        public static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        public static float QuadIn(float t) => t * t;
        public static float QuadOut(float t) { t = 1 - t; return 1 - t * t; }
        public static float QuadInOut(float t) => t < 0.5f ? 2 * t * t : 1 - MathF.Pow(-2 * t + 2, 2) / 2;
        public static float SineInOut(float t) => 0.5f - 0.5f * MathF.Cos(MathF.PI * Clamp01(t));
        public static float BackOut(float t) { float c1 = 1.70158f; float c3 = c1 + 1; t = Clamp01(t); return 1 + c3 * MathF.Pow(t - 1, 3) + c1 * MathF.Pow(t - 1, 2); }
        public static float ElasticOut(float t) {
            t = Clamp01(t);
            if (t == 0 || t == 1) return t;
            return MathF.Pow(2, -10 * t) * MathF.Sin((t - 0.075f) * (2 * MathF.PI) / 0.3f) + 1;
        }
        public static Vector2 BezierQuad(Vector2 a, Vector2 b, Vector2 c, float t) {
            float u = 1 - t;
            return u * u * a + 2 * u * t * b + t * t * c;
        }
        public static Vector2 BezierCubic(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t) {
            float u = 1 - t; float u2 = u * u; float u3 = u2 * u; float t2 = t * t; float t3 = t2 * t;
            return a * u3 + 3 * b * u2 * t + 3 * c * u * t2 + d * t3;
        }

        //黄金角 ≈ 137.508° = π(3-√5)
        public const float GoldenAngle = 2.39996323f;

        //Catmull-Rom样条插值，在p1-p2间以t∈[0,1]插值，p0/p3为邻控制点
        public static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t) {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        //Hermite样条插值，给定端点位置和切线
        public static Vector2 Hermite(Vector2 pos0, Vector2 tan0, Vector2 pos1, Vector2 tan1, float t) {
            float t2 = t * t, t3 = t2 * t;
            float h00 = 2 * t3 - 3 * t2 + 1;
            float h10 = t3 - 2 * t2 + t;
            float h01 = -2 * t3 + 3 * t2;
            float h11 = t3 - t2;
            return h00 * pos0 + h10 * tan0 + h01 * pos1 + h11 * tan1;
        }

        //临界阻尼弹簧(单轴)，模拟有质量感的追踪运动
        //stiffness=刚度(越大越快), damping=阻尼(2*sqrt(stiffness)为临界阻尼)
        public static float SpringDamp(float current, float target, ref float velocity, float stiffness, float damping, float dt) {
            float delta = current - target;
            float accel = -stiffness * delta - damping * velocity;
            velocity += accel * dt;
            return current + velocity * dt;
        }

        //二维弹簧 — 返回新位置，velocity通过ref修改
        public static Vector2 SpringDamp2D(Vector2 current, Vector2 target, ref Vector2 velocity, float stiffness, float damping, float dt) {
            Vector2 delta = current - target;
            Vector2 accel = -stiffness * delta - damping * velocity;
            velocity += accel * dt;
            return current + velocity * dt;
        }

        //Verlet积分一步: 更新位置链并施加距离约束
        //positions[0]为锚点(固定), gravity仅影响Y
        public static void VerletStep(Vector2[] positions, float gravity, float constraintDist, int iterations) {
            int n = positions.Length;
            //记录旧位置用于隐式速度
            Vector2[] old = new Vector2[n];
            for (int i = 0; i < n; i++) old[i] = positions[i];

            //积分(跳过锚点0)
            for (int i = 1; i < n; i++) {
                Vector2 vel = positions[i] - old[i];
                positions[i] += vel;
                positions[i].Y += gravity;
            }

            //距离约束松弛
            for (int iter = 0; iter < iterations; iter++) {
                for (int i = 0; i < n - 1; i++) {
                    Vector2 diff = positions[i + 1] - positions[i];
                    float dist = diff.Length();
                    if (dist < 0.001f) continue;
                    float error = (dist - constraintDist) / dist;
                    Vector2 correction = diff * error * 0.5f;
                    if (i == 0)
                        positions[i + 1] -= correction * 2f; //锚点不动
                    else {
                        positions[i] += correction;
                        positions[i + 1] -= correction;
                    }
                }
            }
        }

        //提前量瞄准: 计算射弹应瞄准的位置以拦截移动目标
        //返回瞄准方向(已归一化)，如目标不可拦截则瞄准当前位置
        public static Vector2 LeadTarget(Vector2 shooterPos, Vector2 targetPos, Vector2 targetVel, float projSpeed) {
            Vector2 toTarget = targetPos - shooterPos;
            float a = targetVel.LengthSquared() - projSpeed * projSpeed;
            float b = 2f * Vector2.Dot(targetVel, toTarget);
            float c = toTarget.LengthSquared();
            float disc = b * b - 4f * a * c;
            if (MathF.Abs(a) < 0.001f || disc < 0) {
                return toTarget.SafeNormalize(Vector2.UnitX);
            }
            float sqrtDisc = MathF.Sqrt(disc);
            float t1 = (-b - sqrtDisc) / (2f * a);
            float t2 = (-b + sqrtDisc) / (2f * a);
            float t = (t1 > 0 && t2 > 0) ? MathF.Min(t1, t2) : MathF.Max(t1, t2);
            if (t <= 0) return toTarget.SafeNormalize(Vector2.UnitX);
            Vector2 aimPos = targetPos + targetVel * t;
            return (aimPos - shooterPos).SafeNormalize(Vector2.UnitX);
        }

        //从位置数组构建平滑TriangleStrip顶点ribbon
        //positions: 中心线点(至少2个), widthFunc: 根据归一化进度[0,1]返回半宽
        //colorFunc: 根据进度返回颜色, uvScroll: UV纵向滚动偏移
        //subdivisions: 每段间CatmullRom细分数(1=不细分，4=高质量)
        public static ColoredVertex[] BuildRibbonStrip(
            Vector2[] positions, Func<float, float> widthFunc, Func<float, Color> colorFunc,
            float uvScroll = 0f, int subdivisions = 3) {

            int n = positions.Length;
            if (n < 2) return Array.Empty<ColoredVertex>();

            //先生成细分后的中心线
            var spine = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < n - 1; i++) {
                Vector2 p0 = positions[Math.Max(i - 1, 0)];
                Vector2 p1 = positions[i];
                Vector2 p2 = positions[Math.Min(i + 1, n - 1)];
                Vector2 p3 = positions[Math.Min(i + 2, n - 1)];
                for (int s = 0; s < subdivisions; s++) {
                    float t = (float)s / subdivisions;
                    spine.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }
            spine.Add(positions[n - 1]);

            int count = spine.Count;
            var verts = new ColoredVertex[count * 2];
            for (int i = 0; i < count; i++) {
                float progress = (float)i / (count - 1);
                Vector2 pos = spine[i];

                //计算法线(垂直于切线)
                Vector2 tangent;
                if (i == 0) tangent = spine[1] - spine[0];
                else if (i == count - 1) tangent = spine[count - 1] - spine[count - 2];
                else tangent = spine[i + 1] - spine[i - 1];
                Vector2 normal = new Vector2(-tangent.Y, tangent.X);
                if (normal.LengthSquared() > 0.001f) normal.Normalize();

                float halfW = widthFunc(progress);
                Color col = colorFunc(progress);
                float u = progress + uvScroll;

                verts[i * 2] = new ColoredVertex(pos + normal * halfW, new Vector3(u, 0, 1), col);
                verts[i * 2 + 1] = new ColoredVertex(pos - normal * halfW, new Vector3(u, 1, 1), col);
            }
            return verts;
        }
    }
}
