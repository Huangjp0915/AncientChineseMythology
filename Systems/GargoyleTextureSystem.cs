//using Microsoft.Xna.Framework;
//using Microsoft.Xna.Framework.Graphics;
//using Terraria.ModLoader;
//using Terraria;
//using System.Collections.Generic;

//namespace AncientChineseMythology.Systems
//{
//    public class GargoyleTextureSystem : ModSystem
//    {
//        public static List<Texture2D> RunFrames = new List<Texture2D>();
//        public static List<Texture2D> AttackFrames = new List<Texture2D>();
//        public static List<Texture2D> DieFrames = new List<Texture2D>();

//        public override void Load()
//        {
//            int runFrameCount = 4;
//            int attackFrameCount = 4;
//            int dieFrameCount = 6;

//            for (int i = 1; i <= runFrameCount; i++)
//            {
//                string path = $"AncientChineseMythology/Textures/angry_gargoyle/run_{i:D2}";
//                RunFrames.Add(ModContent.Request<Texture2D>(path).Value);
//            }
//            for (int i = 1; i <= attackFrameCount; i++)
//            {
//                string path = $"AncientChineseMythology/Textures/angry_gargoyle/attack_{i:D2}";
//                AttackFrames.Add(ModContent.Request<Texture2D>(path).Value);
//            }
//            for (int i = 1; i <= dieFrameCount; i++)
//            {
//                string path = $"AncientChineseMythology/Textures/angry_gargoyle/die_{i:D2}";
//                DieFrames.Add(ModContent.Request<Texture2D>(path).Value);
//            }
//            Main.NewText($"Loaded textures: run={RunFrames.Count}, attack={AttackFrames.Count}, die={DieFrames.Count}", Microsoft.Xna.Framework.Color.Green);
//        }

//        public override void Unload()
//        {
//            RunFrames.Clear();
//            AttackFrames.Clear();
//            DieFrames.Clear();
//        }
//    }
//}
