//using Microsoft.Xna.Framework.Graphics;
//using System.Collections.Generic;
//using Terraria;
//using Terraria.ModLoader;

//namespace AncientChineseMythology.Systems
//{
//    public class BlackBearTextureSystem : ModSystem
//    {
//        public static List<Texture2D> IdleFrames = new();
//        public static List<Texture2D> RunFrames = new();
//        public static List<Texture2D> AttackFrames = new();
//        public static List<Texture2D> DieFrames = new();

//        public override void Load()
//        {
//            if (Main.dedServ) return;

//            // Idle: 4帧 => idle_01 ... idle_04
//            for (int i = 1; i <= 4; i++)
//            {
//                string path = $"AncientChineseMythology/Textures/BlackBear/idle_{i:00}";
//                IdleFrames.Add(ModContent.Request<Texture2D>(path).Value);
//            }

//            // Run: 6帧 => run_01 ... run_06
//            for (int i = 1; i <= 6; i++)
//            {
//                string path = $"AncientChineseMythology/Textures/BlackBear/run_{i:00}";
//                RunFrames.Add(ModContent.Request<Texture2D>(path).Value);
//            }

//            // Attack: 10帧 => attack_01 ... attack_10
//            for (int i = 1; i <= 10; i++)
//            {
//                string path = $"AncientChineseMythology/Textures/BlackBear/attack_{i:00}";
//                AttackFrames.Add(ModContent.Request<Texture2D>(path).Value);
//            }

//            // Die: 6帧 => die_01 ... die_06
//            for (int i = 1; i <= 6; i++)
//            {
//                string path = $"AncientChineseMythology/Textures/BlackBear/die_{i:00}";
//                DieFrames.Add(ModContent.Request<Texture2D>(path).Value);
//            }

//            Main.NewText($"[BlackBearTextureSystem] Idle={IdleFrames.Count}, Run={RunFrames.Count}, Attack={AttackFrames.Count}, Die={DieFrames.Count}", Microsoft.Xna.Framework.Color.LightGreen);
//        }

//        public override void Unload()
//        {
//            IdleFrames = null;
//            RunFrames = null;
//            AttackFrames = null;
//            DieFrames = null;
//        }
//    }
//}
