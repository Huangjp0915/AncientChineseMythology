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
    }
}
