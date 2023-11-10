using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using ME3TweaksCore.Targets;
using Newtonsoft.Json;
using Randomizer.MER;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Shared.Classes;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game2.TextureAssets.LE2
{
    public static class LE2Textures
    {
        private static string[] PictureFrameIFPs = new[]
        {
            "BioS_Exp1Apt.NRM_SR1", // Liara's Normandy picture
            "BioVFX_Env_Hologram.Textures.Ashley_01", // Died on virmire? Romanced in ME1?
            "BioVFX_Env_Hologram.Textures.Kaiden_01",
            "BioVFX_Env_Hologram.Textures.Liara_1",
            "biovfx_env_unc_pack01.Textures.archer_photograph", // UNC1 Archer
        };

        private static string[] HTextIFPs = new[]
        {
            "BioVFX_Env_Hologram.Textures.H_texts",
            "BioVFX_Env_Hologram.Textures.H_texts_2",
            "BioVFX_Env_Hologram.Textures.H_texts_3",
            "BioVFX_Env_Hologram.Textures.HoloTextScroll"
        };

        private static string[] CitadelHolomodIFPs = new[]
        {
            "BioVFX_Env_Hologram.Textures.Holomod_11_Tex",
        };

        /// <summary>
        /// Dual half height images
        /// </summary>
        private static string[] HGraphIFPs = new[]
        {
            "BioVFX_Env_Hologram.Textures.H_Graphs",
            "BioVFX_Env_Hologram.Textures.H_Graphs3_5",
        };

        private static string[] DatapadIFPs = new[]
        {
            "BioApl_Dec_DataPad01.Materials.Datapad01_Screen",
        };

        private static string[] EndGm3DatapadIFPs = new[]
        {
            "BioVFX_Env_End.Textures.Reaper_Display"
        };

        public static void BuildPremadeTFC()
        {
#if DEBUG

            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(MEGame.LE2, includeTFCs: true, forceReload: true);
            if (loadedFiles.TryGetValue($"{PremadeTFCName}.tfc", out var premadeExisting))
            {
                File.Delete(premadeExisting);
            }

            MELoadedFiles.GetFilesLoadedInGame(MEGame.LE2, forceReload: true); // Force reload to remove file
            List<SourceTexture> builderInfo = getShippedTextures();

            var premadePackageF = @"B:\UserProfile\source\repos\ME2Randomizer\Randomizer\Randomizers\Game2\Assets\Binary\Packages\LE2\Textures\PremadeImages.pcc";
            using var premadePackage = MEPackageHandler.CreateAndOpenPackage(premadePackageF, MEGame.LE2);

            var premadeTfcPath = $@"B:\UserProfile\source\repos\ME2Randomizer\Randomizer\Randomizers\Game2\Assets\Binary\Textures\{PremadeTFCName}.tfc";
            foreach (var bfi in builderInfo)
            {
                bfi.StoreTexture(premadePackage);
            }

            loadedFiles = MELoadedFiles.GetFilesLoadedInGame(MEGame.LE2, includeTFCs: true, forceReload: true);
            File.Copy(loadedFiles[PremadeTFCName + ".tfc"], premadeTfcPath, true);

            premadePackage.Save();
#endif
        }

        private static List<SourceTexture> getShippedTextures()
        {
            return new List<SourceTexture>()
            {
                // Feature: Citadel
                new SourceTexture()
                {
                    Filename = "cursed.png",
                    ContainingPackageName = "Startup_INT.pcc",
                    IFPsToBuildOff = CitadelHolomodIFPs,
                    Id = "CursedBC7",
                    SpecialUseOnly = true,
                },
                new SourceTexture()
                {
                    Filename = "borgar.png",
                    ContainingPackageName = "Startup_INT.pcc",
                    IFPsToBuildOff = CitadelHolomodIFPs,
                    Id = "BorgarBC7",
                    SpecialUseOnly = true,
                },


                // Hologram screens
                new SourceTexture()
                {
                    Filename = "H_Graphs_mybrandamazon.png",
                    ContainingPackageName = "BioA_N7Mmnt1.pcc",
                    IFPsToBuildOff = HGraphIFPs,
                    Id = "HoloScreensAmazon"
                },
                new SourceTexture()
                {
                    Filename = "H_Graphs_pizzaonion.png",
                    ContainingPackageName = "BioA_N7Mmnt1.pcc",
                    IFPsToBuildOff = HGraphIFPs,
                    Id = "HoloScreensPizza"
                },
                new SourceTexture()
                {
                    Filename = "bsod_toasters.png",
                    ContainingPackageName = "BioA_N7Mmnt1.pcc",
                    IFPsToBuildOff = HGraphIFPs,
                    Id = "HologramBSODToaster"
                },
                new SourceTexture()
                {
                    Filename = "sweat_burgerrecipe.png",
                    ContainingPackageName = "BioA_N7Mmnt1.pcc",
                    IFPsToBuildOff = HGraphIFPs,
                    Id = "HoloscreenSweatBurger"
                },
                new SourceTexture()
                {
                    // This had to be recovered from ME2R as the source image was lost
                    Filename = "sonicburger_me2r.png",
                    ContainingPackageName = "BioA_N7Mmnt1.pcc",
                    IFPsToBuildOff = HGraphIFPs,
                    Id = "HoloscreenSonicBurgerME2R"
                },

                // Datapads
                new SourceTexture()
                {
                    Filename = "map.png",
                    ContainingPackageName = "BioD_CitAsL.pcc",
                    IFPsToBuildOff = DatapadIFPs,
                    Id = "DatapadMap"
                },
                new SourceTexture()
                {
                    Filename = "monsterplan.png",
                    ContainingPackageName = "BioD_CitAsL.pcc",
                    IFPsToBuildOff = DatapadIFPs,
                    Id = "DatapadMonsterPlan"
                },
                new SourceTexture()
                {
                    Filename = "sizebounty.png",
                    ContainingPackageName = "BioD_CitAsL.pcc",
                    IFPsToBuildOff = DatapadIFPs,
                    Id = "DatapadSizeBounty"
                },
                new SourceTexture()
                {
                    Filename = "thisisfine.png",
                    ContainingPackageName = "BioD_CitAsL.pcc",
                    IFPsToBuildOff = DatapadIFPs,
                    Id = "DatapadThisIsFine"
                },


                // EndGm3 Datapad
                new SourceTexture()
                {
                    Filename = "audemus_fishdog_food_shack.png",
                    ContainingPackageName = "BioP_EndGm3.pcc",
                    IFPsToBuildOff = EndGm3DatapadIFPs,
                    Id = "FishdogFoodShack"
                },
                new SourceTexture()
                {
                    Filename = "mgamerz_me3_mp.png",
                    ContainingPackageName = "BioP_EndGm3.pcc",
                    IFPsToBuildOff = EndGm3DatapadIFPs,
                    Id = "LE3MPTeaser"
                },


                // H_Texts
                new SourceTexture()
                {
                    Filename = "H_Text_3_vim.png",
                    ContainingPackageName = "BioA_ProCer_350.pcc",
                    IFPsToBuildOff = HTextIFPs,
                    Id = "H_Text_Vim"
                },
                new SourceTexture()
                {
                    Filename = "newspaper.png",
                    ContainingPackageName = "BioA_ProCer_350.pcc",
                    IFPsToBuildOff = HTextIFPs,
                    Id = "H_Text_Newspaper"
                },
                new SourceTexture()
                { // This reference doesn't make as much sense outside the pandemic that ME2R was launched in
                    Filename = "sourdough.png",
                    ContainingPackageName = "BioA_ProCer_350.pcc",
                    IFPsToBuildOff = HTextIFPs,
                    Id = "H_Text_Sourdough"
                },
                new SourceTexture()
                { 
                    Filename = "khaarsgame.png",
                    ContainingPackageName = "BioA_ProCer_350.pcc",
                    IFPsToBuildOff = HTextIFPs,
                    Id = "H_Text_KhaarsGame"
                },
                new SourceTexture()
                {
                    Filename = "screenpresso.png",
                    ContainingPackageName = "BioA_ProCer_350.pcc",
                    IFPsToBuildOff = HTextIFPs,
                    Id = "H_Text_Screenpresso"
                },

                // Picture frames
                new SourceTexture()
                {
                    Filename = "garage.png",
                    ContainingPackageName = "BioD_Exp1Lvl1_100Apartment.pcc",
                    IFPsToBuildOff = PictureFrameIFPs,
                    Id = "HereInMyGarage"
                },
                new SourceTexture()
                {
                    Filename = "digsite.png",
                    ContainingPackageName = "BioD_Exp1Lvl1_100Apartment.pcc",
                    IFPsToBuildOff = PictureFrameIFPs,
                    Id = "UnderControl"
                },
                new SourceTexture()
                {
                    Filename = "ohno.png",
                    ContainingPackageName = "BioD_Exp1Lvl1_100Apartment.pcc",
                    IFPsToBuildOff = PictureFrameIFPs,
                    Id = "BigMistake"
                },
                new SourceTexture()
                {
                    Filename = "salarian.png",
                    ContainingPackageName = "BioD_Exp1Lvl1_100Apartment.pcc",
                    IFPsToBuildOff = PictureFrameIFPs,
                    Id = "SalarianEyes"
                },
                new SourceTexture()
                {
                    Filename = "aria.png",
                    ContainingPackageName = "BioD_Exp1Lvl1_100Apartment.pcc",
                    IFPsToBuildOff = PictureFrameIFPs,
                    Id = "BigEyesAria"
                },
                new SourceTexture()
                {
                    Filename = "bighead.png",
                    ContainingPackageName = "BioD_Exp1Lvl1_100Apartment.pcc",
                    IFPsToBuildOff = PictureFrameIFPs,
                    Id = "BigHeadModeEngaged"
                },
                new SourceTexture()
                {
                    Filename = "monkaAnderson.png",
                    ContainingPackageName = "BioD_Exp1Lvl1_100Apartment.pcc",
                    IFPsToBuildOff = PictureFrameIFPs,
                    Id = "SideEyeAnderson"
                },
            };
        }

        /// <summary>
        /// DO NOT CHANGE
        /// </summary>
        public const string PremadeTFCName = @"Textures_DLC_MOD_LE2Randomizer_PM";

        public static void SetupLE2Textures(GameTarget target)
        {
            TextureHandler.StartHandler(target, getShippedTextures());
        }
    }
}