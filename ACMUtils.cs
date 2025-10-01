using System;
using Terraria;

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
    }
}
