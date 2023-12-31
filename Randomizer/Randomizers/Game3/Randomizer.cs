﻿#if __GAME3__
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Misc;
using Randomizer.MER;
using Randomizer.Randomizers.Game3.Enemies;
using Randomizer.Randomizers.Game3.ExportTypes;
using Randomizer.Randomizers.Game3.ExportTypes.Enemy;
using Randomizer.Randomizers.Game3.Framework;
using Randomizer.Randomizers.Game3.Levels;
using Randomizer.Randomizers.Game3.Misc;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Shared;
using Randomizer.Randomizers.Shared.Classes;
using Randomizer.Randomizers.Utility;
using Serilog;

namespace Randomizer.Randomizers.Game3
{
    public class Randomizer
    {
        private BackgroundWorker randomizationWorker;

        // Files that should not be generally passed over
        private static List<string> SpecializedFiles { get; } = new List<string>()
        {
            "BioP_Char",
            "BioD_Nor_103aGalaxyMap",
            "BioG_UIWorld" // Char creator lighting
        };

        public Randomizer()
        {

        }

        /// <summary>
        /// Are we busy randomizing?
        /// </summary>
        public bool Busy => randomizationWorker != null && randomizationWorker.IsBusy;

        /// <summary>
        /// The options selected by the user that will be used to determine what the randomizer does
        /// </summary>
        public OptionsPackage SelectedOptions { get; set; }

        public void Randomize(OptionsPackage op)
        {
            SelectedOptions = op;
            ThreadSafeRandom.Reset();
            if (!SelectedOptions.UseMultiThread)
            {
                ThreadSafeRandom.SetSingleThread(SelectedOptions.Seed);
            }

            randomizationWorker = new BackgroundWorker();
            randomizationWorker.DoWork += PerformRandomization;
            randomizationWorker.RunWorkerCompleted += Randomization_Completed;

            if (SelectedOptions.UseMultiThread)
            {
                MERLog.Information("-------------------------STARTING RANDOMIZER (MULTI THREAD)--------------------------");
            }
            else
            {
                MERLog.Information($"------------------------STARTING RANDOMIZER WITH SEED {op.Seed} (SINGLE THREAD)--------------------------");
            }
            randomizationWorker.RunWorkerAsync();
            op.SetTaskbarState?.Invoke(MTaskbarState.Indeterminate);
        }



        private void Randomization_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            SelectedOptions.SetRandomizationInProgress?.Invoke(false);

            if (e.Error != null)
            {
                MERLog.Exception(e.Error, @"Randomizer thread exited with exception!");
                SelectedOptions.SetCurrentOperationText?.Invoke($"Randomizer failed with error: {e.Error.Message}, please report to Mgamerz");
                TelemetryInterposer.TrackError(new Exception("Randomizer thread exited with exception", e.Error));
            }
            else
            {
                TelemetryInterposer.TrackEvent("Randomization completed");
                SelectedOptions.SetCurrentOperationText?.Invoke("Randomization complete");
            }
            CommandManager.InvalidateRequerySuggested();
            SelectedOptions.SetTaskbarState?.Invoke(MTaskbarState.None);
            SelectedOptions.SetRandomizationInProgress?.Invoke(false);
        }

        private void PerformRandomization(object sender, DoWorkEventArgs e)
        {
            MemoryManager.SetUsePooledMemory(true, false, false, (int)FileSize.KibiByte * 8, 4, 2048, false);
            ResetClasses(); // Cleanup anything that wasn't cleaned up previously for some reason.
            SelectedOptions.SetRandomizationInProgress?.Invoke(true);
            SelectedOptions.SetCurrentOperationText?.Invoke("Initializing randomizer");
            SelectedOptions.SetOperationProgressBarIndeterminate?.Invoke(true);
            var specificRandomizers = SelectedOptions.SelectedOptions.Where(x => x.PerformSpecificRandomizationDelegate != null).ToList();
            var perFileRandomizers = SelectedOptions.SelectedOptions.Where(x => x.PerformFileSpecificRandomization != null).ToList();
            var perExportRandomizers = SelectedOptions.SelectedOptions.Where(x => x.IsExportRandomizer).ToList();

            // Log randomizers
            MERLog.Information("Randomizers used in this pass:");
            foreach (var sr in specificRandomizers.Concat(perFileRandomizers).Concat(perExportRandomizers).Distinct())
            {
                MERLog.Information($" - {sr.HumanName}");
                if (sr.SubOptions != null)
                {
                    foreach (var subR in sr.SubOptions)
                    {
                        MERLog.Information($"   - {subR.HumanName}");
                    }
                }
            }

            MERCaches.Init(SelectedOptions.RandomizationTarget);
            Exception rethrowException = null;
            try
            {
                // Initialize FileSystem and handlers
                MERFileSystem.InitMERFS(SelectedOptions);

                // Initialize any special items here
                if (SelectedOptions.SelectedOptions.Any(x => x.RequiresGestures))
                {
                    // Prepare runtime gestures data
                    GestureManager.Init(SelectedOptions.RandomizationTarget);
                }

                Stopwatch sw = new Stopwatch();
                sw.Start();

                void srUpdate(object? o, EventArgs eventArgs)
                {
                    if (o is RandomizationOption option)
                    {
                        SelectedOptions.SetOperationProgressBarIndeterminate?.Invoke(option.ProgressIndeterminate);
                        SelectedOptions.SetOperationProgressBarProgress?.Invoke(option.ProgressValue, option.ProgressMax);
                        if (option.CurrentOperation != null)
                        {
                            SelectedOptions.SetCurrentOperationText?.Invoke(option.CurrentOperation);
                        }
                    }
                }

                // Pass 1: All randomizers that are file specific and are not post-run
                foreach (var sr in specificRandomizers.Where(x => !x.IsPostRun))
                {
                    sr.OnOperationUpdate += srUpdate;
                    MERLog.Information($"Running specific randomizer {sr.HumanName}");
                    SelectedOptions.SetCurrentOperationText?.Invoke($"Randomizing {sr.HumanName}");
                    sr.PerformSpecificRandomizationDelegate?.Invoke(SelectedOptions.RandomizationTarget, sr);
                    sr.OnOperationUpdate -= srUpdate;
                }

                // Pass 2: All exports
                if (perExportRandomizers.Any() || perFileRandomizers.Any())
                {
                    SelectedOptions.SetCurrentOperationText?.Invoke("Getting list of files...");
                    SelectedOptions.SetOperationProgressBarIndeterminate?.Invoke(true);

                    // we only want pcc files (me2/me3). no upks
                    var files = MELoadedFiles.GetFilesLoadedInGame(SelectedOptions.RandomizationTarget.Game, true, false, false, SelectedOptions.RandomizationTarget.TargetPath).Values.Where(x => !MERFileSystem.filesToSkip.Contains(Path.GetFileName(x))).ToList();

                    SelectedOptions.SetOperationProgressBarIndeterminate?.Invoke(false);

                    var currentFileNumber = 0;
                    var totalFilesCount = files.Count;

#if DEBUG
                    Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = SelectedOptions.UseMultiThread ? 1 : 1 }, (file) =>
#else
                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = SelectedOptions.UseMultiThread ? 1 : 1 }, (file) =>
#endif
                    {

                        var name = Path.GetFileNameWithoutExtension(file);
                        if (SpecializedFiles.Contains(name, StringComparer.InvariantCultureIgnoreCase)) return; // Do not run randomization on this file as it's only done by specialized randomizers (e.g. char creator)

                        Interlocked.Increment(ref currentFileNumber);
                        SelectedOptions.SetOperationProgressBarProgress?.Invoke(currentFileNumber, totalFilesCount);

                        SelectedOptions.SetCurrentOperationText?.Invoke($"Randomizing game files [{currentFileNumber}/{files.Count}]");

#if DEBUG
                        if (true
                        //&& false //uncomment to disable filtering
                        //&& !file.Contains("Cat002", StringComparison.InvariantCultureIgnoreCase)
                        //&& !file.Contains("Cat003", StringComparison.InvariantCultureIgnoreCase)
                         && !file.Contains("CerMir", StringComparison.InvariantCultureIgnoreCase)
                        && !file.Contains("CitSam", StringComparison.InvariantCultureIgnoreCase)
                         && !file.Contains("Omg", StringComparison.InvariantCultureIgnoreCase)
                        )
                            return;
#endif
                        try
                        {
                            Debug.WriteLine($"Opening package {file}");
                            //Log.Information($@"Opening package {file}");
                            var package = MERFileSystem.OpenMEPackage(file);
                            //Debug.WriteLine(file);
                            foreach (var rp in perFileRandomizers)
                            {
                                // Specific randomization pass before the exports are processed
                                rp.PerformFileSpecificRandomization(SelectedOptions.RandomizationTarget, package, rp);
                            }

                            if (perExportRandomizers.Any())
                            {
                                for (int i = 0; i < package.ExportCount; i++)
                                //                    foreach (var exp in package.Exports.ToList()) //Tolist cause if we add export it will cause modification
                                {
                                    var exp = package.Exports[i];
                                    foreach (var r in perExportRandomizers)
                                    {
                                        r.PerformRandomizationOnExportDelegate(SelectedOptions.RandomizationTarget, exp, r);
                                    }
                                }
                            }

                            MERFileSystem.SavePackage(package);
                        }
                        catch (Exception e)
                        {
                            Log.Error($@"Exception occurred in per-file/export randomization: {e.Message}");
                            TelemetryInterposer.TrackError(new Exception("Exception occurred in per-file/export randomizer", e));
                            Debugger.Break();
                        }
                    });
                }


                // Pass 3: All randomizers that are file specific and are not post-run
                foreach (var sr in specificRandomizers.Where(x => x.IsPostRun))
                {
                    try
                    {
                        sr.OnOperationUpdate += srUpdate;
                        SelectedOptions.SetOperationProgressBarIndeterminate?.Invoke(true);
                        MERLog.Information($"Running post-run specific randomizer {sr.HumanName}");
                        SelectedOptions.SetCurrentOperationText?.Invoke($"Randomizing {sr.HumanName}");
                        sr.PerformSpecificRandomizationDelegate?.Invoke(SelectedOptions.RandomizationTarget, sr);
                        sr.OnOperationUpdate -= srUpdate;
                    }
                    catch (Exception ex)
                    {
                        TelemetryInterposer.TrackError(new Exception($"Exception occurred in post-run specific randomizer {sr.HumanName}", ex));
                    }
                }

                sw.Stop();
                MERLog.Information($"Randomization time: {sw.Elapsed.ToString()}");

                SelectedOptions.SetOperationProgressBarIndeterminate?.Invoke(true);
                SelectedOptions.SetCurrentOperationText?.Invoke("Finishing up");
                SelectedOptions.NotifyDLCComponentInstalled?.Invoke(true);
            }
            catch (Exception exception)
            {
                MERLog.Exception(exception, "Unhandled Exception in randomization thread:");
                rethrowException = exception;
            }

            // DEBUG SCRIPTS
            MERDebug.InstallDebugScript(SelectedOptions.RandomizationTarget, "SFXGame.pcc", "BioCheatManager.ProfilePower");


            // TOC game
            var dlcFolder = MERFileSystem.GetDLCModPath(SelectedOptions.RandomizationTarget);
            var toc = TOCCreator.CreateDLCTOCForDirectory(dlcFolder, SelectedOptions.RandomizationTarget.Game);
            toc.WriteToFile(Path.Combine(dlcFolder, "PCConsoleTOC.bin"));

            // Close out files and free memory
            TFCBuilder.EndTFCs(SelectedOptions.RandomizationTarget);
            CoalescedHandler.EndHandler();
            TLKBuilder.EndHandler();
            MERFileSystem.Finalize(SelectedOptions);
            ResetClasses();
            MemoryManager.ResetMemoryManager();
            MemoryManager.SetUsePooledMemory(false);

            // Re-throw the unhandled exception after MERFS has closed
            if (rethrowException != null)
                throw rethrowException;
        }

        /// <summary>
        /// Ensures things are set back to normal before first run
        /// </summary>
        private void ResetClasses()
        {
            //RMorphTarget.ResetClass();
            //SquadmateHead.ResetClass();
            //PawnPorting.ResetClass();
            //NPCHair.ResetClass();
            RPawnStats.ResetClass();
            MERCaches.Cleanup();
        }


        /// <summary>
        /// Sets the options up that can be selected and their methods they call
        /// </summary>
        /// <param name="RandomizationGroups"></param>
        public static void SetupOptions(ObservableCollectionExtended<RandomizationGroup> RandomizationGroups, Action<RandomizationOption> optionChangingDelegate)
        {
#if DEBUG
            //EnemyPowerChanger.Init(null); // Load the initial list
            //EnemyWeaponChanger.Preboot(); // Load the initial list
#endif
            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Faces",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
#if DEBUG
                    //new RandomizationOption()
                    //{
                    //    Description="Runs debug code randomization",
                    //    HumanName = "Debug randomizer",
                    //    PerformRandomizationOnExportDelegate = DebugTools.DebugRandomizer.RandomizeExport,
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe
                    //},
#endif
                    new RandomizationOption()
                    {
                        Description="Changes facial animation. The best feature of MER",
                        HumanName = "FaceFX animation", Ticks = "1,2,3,4,5", HasSliderOption = true, IsRecommended = true, SliderToTextConverter = rSetting =>
                            rSetting switch
                            {
                                1 => "Oblivion",
                                2 => "Knights of the old Republic",
                                3 => "Sonic Adventure",
                                4 => "Source filmmaker",
                                5 => "Total madness",
                                _ => "Error"
                            },
                        SliderTooltip = "Higher settings yield more extreme facial animation values. Default value is Sonic Adventure",
                        SliderValue = 3, // This must come after the converter
                        PerformRandomizationOnExportDelegate = RSharedFaceFXAnimSet.RandomizeExport,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe
                    },
                    //new RandomizationOption() {HumanName = "Squadmate heads",
                    //    Description = "Changes the heads of your squadmates",
                    //    PerformRandomizationOnExportDelegate = SquadmateHead.RandomizeExport2,
                    //    PerformFileSpecificRandomization = SquadmateHead.FilePrerun,
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                    //    RequiresTLK = true,
                    //    StateChangingDelegate=optionChangingDelegate,
                    //    MutualExclusiveSet = "SquadHead",
                    //    IsRecommended = true
                    //},
                    //new RandomizationOption() {HumanName = "Squadmate faces",
                    //    Description = "Only works on Wilson and Jacob, unfortunately. Other squadmates are fully modeled",
                    //    PerformSpecificRandomizationDelegate = RBioMorphFace.RandomizeSquadmateFaces,
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                    //    MutualExclusiveSet = "SquadHead",
                    //    StateChangingDelegate=optionChangingDelegate,
                    //},

                    new RandomizationOption()
                    {
                        HumanName = "NPC faces",
                        Ticks = "0.1,0.2,0.3,0.4,0.5,0.6,0.7",
                        HasSliderOption = true,
                        IsRecommended = true,
                        SliderTooltip = "Higher settings yield more ridiculous faces for characters that use the BioFaceMorph system. Default value is 0.3.",
                        SliderToTextConverter = rSetting => $"Randomization amount: {rSetting}",
                        SliderValue = .3, // This must come after the converter
                        PerformRandomizationOnExportDelegate = RBioMorphFace.RandomizeExportNonHench,
                        Description="Changes the BioFaceMorph used by some pawns",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                    },
                    // Sadly not used by anything but shepard
                    // For some reason data is embedded into files even though it's never used there
                    //new RandomizationOption()
                    //{
                    //    HumanName = "NPC Faces - Extra jacked up",
                    //    Description = "Changes the MorphTargets that map bones to the face morph system",
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                    //    PerformRandomizationOnExportDelegate = RMorphTarget.RandomizeGlobalExport
                    //},
                    new RandomizationOption() {HumanName = "Eyes (excluding Illusive Man)",
                        Description="Changes the colors of eyes",
                        IsRecommended = true,
                        PerformRandomizationOnExportDelegate = RSharedEyes.RandomizeExport,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe
                    },
                    //new RandomizationOption() {HumanName = "Illusive Man eyes",
                    //    Description="Changes the Illusive Man's eye color",
                    //    IsRecommended = true, PerformRandomizationOnExportDelegate = RIllusiveEyes.RandomizeExport,
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe
                    //},
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Characters",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption()
                    {
                        HumanName = "Animation Set Bones",
                        PerformRandomizationOnExportDelegate = RSharedBioAnimSetData.RandomizeExport,
                        SliderToTextConverter = RSharedBioAnimSetData.UIConverter,
                        HasSliderOption = true,
                        SliderValue = 1,
                        Ticks = "1,2,3,4,5",
                        SliderTooltip = "Higher settings yield more bone randomization. Default value basic bones only.",
                        Description = "Changes the order of animations mapped to bones. E.g. arm rotation will be swapped with eyes",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal
                    },
                    new RandomizationOption() {HumanName = "NPC colors", Description="Changes NPC colors such as skin tone, hair, etc",
                        PerformRandomizationOnExportDelegate = RMaterialInstance.RandomizeNPCExport2,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal, IsRecommended = true},
                    //new RandomizationOption() {HumanName = "NPC hair", Description="Randomizes the hair on NPCs that have a hair mesh",
                    //    PerformRandomizationOnExportDelegate = NPCHair.RandomizeExport,
                    //    PerformSpecificRandomizationDelegate = NPCHair.Init,
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal},
                    new RandomizationOption() {
                        HumanName = "Romance",
                        Description="Randomizes which romance you will get. Randomization is done at runtime so every romance is different",
                        PerformSpecificRandomizationDelegate = Romance.PerformRandomization,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning, IsRecommended = true

                    },
                    new RandomizationOption()
                    {
                        HumanName = "NPCs (requires LE3 Framework)",
                        PerformSpecificRandomizationDelegate = RNPC.RandomizeNPCs,
                        Description = "Shuffles the NPCs that the LE3 Framework provides.",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning
                    },
                    new RandomizationOption()
                    {
                        HumanName = "Henchmen",
                        PerformSpecificRandomizationDelegate = RBioH.RandomizeBioH,
                        Description = "Shuffles the pawn files around so different squadmates load in place of others. This will automatically re-apply powers to the pawn due to save/load issues.",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning
                    },
                    new RandomizationOption() {
                        HumanName = "Look At Definitions",
                        Description="Changes how pawns look at things",
                        PerformRandomizationOnExportDelegate = RSharedBioLookAtDefinition.RandomizeExport,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, IsRecommended = true},
                    new RandomizationOption() {
                        HumanName = "Look At Targets",
                        Description="Changes where pawns look",
                        PerformRandomizationOnExportDelegate = RSharedBioLookAtTarget.RandomizeExport,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe
                    },
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Character Creator",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption() {
                        HumanName = "Premade faces",
                        IsRecommended = true,
                        Description = "Completely randomizes settings including skin tones and slider values. Adds extra premade faces",
                        PerformSpecificRandomizationDelegate = CharacterCreator.RandomizeCharacterCreator,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                        {
                            new RandomizationOption()
                            {
                                SubOptionKey = CharacterCreator.SUBOPTIONKEY_CHARCREATOR_NO_COLORS,
                                HumanName = "Don't randomize colors",
                                Description = "Prevents changing colors such as skin tone, teeth, eyes, etc",
                                Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                                IsOptionOnly = true
                            }
                        }
                    },
                    //new RandomizationOption()
                    //{
                    //    HumanName = "Iconic FemShep face",
                    //    Description="Changes the default FemShep face",
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                    //    Ticks = "0.1,0.2,0.3,0.4,0.5,0.6,0.7",
                    //    HasSliderOption = true,
                    //    IsRecommended = true,
                    //    SliderTooltip = "Higher settings yield more extreme facial changes. Default value is 0.3.",
                    //    SliderToTextConverter = rSetting => $"Randomization amount: {rSetting}",
                    //    SliderValue = .3, // This must come after the converter
                    //    PerformSpecificRandomizationDelegate = CharacterCreator.RandomizeIconicFemShep
                    //},
                    //new RandomizationOption()
                    //{
                    //    HumanName = "Iconic MaleShep face",
                    //    Description="Changes the bones in default MaleShep face. Due to it being modeled, the changes only occur when the face moves",
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                    //    Ticks = "0.25,0.5,1.0,1.25,1.5,2.0",
                    //    HasSliderOption = true,
                    //    IsRecommended = true,
                    //    SliderTooltip = "Higher settings yields further bone position shifting, which can sometimes be undesirable. Default value is 1.0.",
                    //    SliderToTextConverter = rSetting => $"Randomization amount: {rSetting}",
                    //    SliderValue = 1.0, // This must come after the converter
                    //    PerformSpecificRandomizationDelegate = CharacterCreator.RandomizeIconicMaleShep,
                    //    SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                    //    {
                    //        new RandomizationOption()
                    //        {
                    //            SubOptionKey = CharacterCreator.SUBOPTIONKEY_MALESHEP_COLORS,
                    //            HumanName = "Include colors",
                    //            Description = "Also changes colors like skintone, eyes, scars",
                    //            Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                    //            IsOptionOnly = true
                    //        }
                    //    }
                    //},
                    //new RandomizationOption()
                    //{
                    //    HumanName = "Psychological profiles",
                    //    Description="Completely changes the backstories of Shepard, with both new stories and continuations from ME1 Randomizer's stories",
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                    //    IsRecommended = true,
                    //    PerformSpecificRandomizationDelegate = CharacterCreator.RandomizePsychProfiles,
                    //    RequiresTLK = true
                    //},
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Miscellaneous",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption() {HumanName = "Hologram colors", Description="Changes colors of holograms",PerformRandomizationOnExportDelegate = RSharedHolograms.RandomizeExport, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, IsRecommended = true},
                    // new RandomizationOption() {HumanName = "Photo mode", Description="Adds additional photo mode filters", PerformSpecificRandomizationDelegate = RPhotoMode.InstallAdditionalFilters, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, IsRecommended = true},
                    //new RandomizationOption() {HumanName = "Drone colors", Description="Changes colors of drones",PerformRandomizationOnExportDelegate = CombatDrone.RandomizeExport, IsRecommended = true},
                    //new RandomizationOption() {HumanName = "Omnitool", Description="Changes colors of omnitools",PerformRandomizationOnExportDelegate = ROmniTool.RandomizeExport},
                    //new RandomizationOption() {HumanName = "Specific textures",Description="Changes specific textures to more fun ones", PerformRandomizationOnExportDelegate = TFCBuilder.RandomizeExport, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, IsRecommended = true},
                    /*new RandomizationOption() {HumanName = "SizeSixteens mode",
                        Description = "This option installs a change specific for the streamer SizeSixteens. If you watched his ME1 Randomizer streams, you'll understand the change.",
                        PerformSpecificRandomizationDelegate = SizeSixteens.InstallSSChanges,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        RequiresTLK = true
                    },*/
                    new RandomizationOption() {HumanName = "NPC names",
                        Description = "Install a list of names into the game and renames generic NPCs to them. You can install your stream chat members, for example. There are 525 name slots.",
                        PerformSpecificRandomizationDelegate = RCharacterNames.InstallNameSet,
                        SetupRandomizerDelegate = RCharacterNames.SetupRandomizer,
                        SetupRandomizerButtonText = "Setup",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        RequiresTLK = true,
                        HasSliderOption = true,
                        Ticks = "1,2,3,4,5,6,7,8,9,10",
                        SliderToTextConverter = x=> $"Duplicate namelist {x} time(s)",
                        SliderTooltip = "Duplicates the namelist this many times. For example, if you load 5 names, if you duplicate it 3 times, 15 NPC names will be replaced - your list will be installed 3 times.",
                        SliderValue = 1,
                    },
#if DEBUG && __ME2__
                    new RandomizationOption() {HumanName = "Skip splash",
                        Description = "Skips the splash screen",
                        PerformSpecificRandomizationDelegate = EntryMenu.SetupFastStartup,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        OptionIsSelected = true},
#endif

                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Movement & pawns",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    //new RandomizationOption() {HumanName = "NPC movement speeds", Description = "Changes non-player movement stats", PerformRandomizationOnExportDelegate = PawnMovementSpeed.RandomizeMovementSpeed, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, IsRecommended = true},
                    //new RandomizationOption() {HumanName = "Player movement speeds", Description = "Changes player movement stats", PerformSpecificRandomizationDelegate = PawnMovementSpeed.RandomizePlayerMovementSpeed, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal},
                    //new RandomizationOption() {HumanName = "NPC walking routes", PerformRandomizationOnExportDelegate = RRoute.RandomizeExport}, // Seems very specialized in ME2
                    new RandomizationOption() {HumanName = "Pawns stats",
                        IsRecommended = true,
                        Description = "Runtime randomization of various non-player pawn stats. Pick options below to customize",
                        PerformSpecificRandomizationDelegate = RPawnStats.PerformRandomization,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal,
                        SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                        {
                            new RandomizationOption()
                            {
                                HumanName = "Health",
                                Description = "Randomizes pawn health health",
                                SubOptionKey = RPawnStats.HEALTH_OPTION,
                                IsOptionOnly = true,
                            },
                            new RandomizationOption()
                            {
                                HumanName = "Shields",
                                Description = "Randomizes pawn shields and shield regen",
                                SubOptionKey = RPawnStats.SHIELD_OPTION,
                                IsOptionOnly = true,
                            },
                            new RandomizationOption()
                            {
                                HumanName = "Movement speeds",
                                Description = "Randomizes pawn movement speeds and accelerations",
                                SubOptionKey = RPawnStats.MOVEMENTSPEED_OPTION,
                                IsOptionOnly = true,
                            },
                            new RandomizationOption() {HumanName = "Evasion abilities",
                            Description = "Randomizes what abilities can be used for evading damage",
                            SubOptionKey = RPawnStats.EVASION_OPTION,
                            IsOptionOnly = true,
                            },new RandomizationOption() {HumanName = "Melee abilities",
                                Description = "Randomizes what ability can be used for the close range melee attack",
                                SubOptionKey = RPawnStats.MELEE_OPTION,
                                IsOptionOnly = true,
                            },
                        }
                    },
                    new RandomizationOption() {HumanName = "'Lite' pawn animations", IsRecommended = true, Description = "Changes the animations used by most basic non-interactable NPCs.", PerformRandomizationOnExportDelegate = RSFXSkeletalMeshActor.RandomizeBasicGestures, RequiresGestures = true, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning},
                    new RandomizationOption() {HumanName = "Pawn materials", IsRecommended = true, Description = "Runtime randomzier that randomizes the inputs to materials on every level file load. An input may be a glow, color, etc.",PerformFileSpecificRandomization = RSharedSkeletalMesh.InstallRandomMICKismetObject, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal},

                    //new RandomizationOption()
                    //{
                    //    HumanName = "Pawn sizes", Description = "Changes the size of characters. Will break a lot of things", PerformRandomizationOnExportDelegate = RBioPawn.RandomizePawnSize,
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_RIP,
                    //    Ticks = "0.1,0.2,0.3,0.4,0.5,0.75",
                    //    HasSliderOption = true,
                    //    SliderTooltip = "Values are added +/- to 1 to generate the range of allowed sizes. For example, 0.1 yields 90-110% size multiplier. Default value is 0.1.",
                    //    SliderToTextConverter = x=> $"Maximum size change: {Math.Round(x * 100)}%",
                    //    SliderValue = 0.1,
                    //},
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Enemy Specific",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption()
                    {
                        HumanName = "Banshee",
                        Description = "Changes how the Banshee enemy behaves",
                        PerformFileSpecificRandomization = RBanshee.RandomizeBanshee,
                        SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                        {
                            new RandomizationOption()
                            {
                                IsOptionOnly = true,
                                HumanName = "Reverse-side warping",
                                Description = "Banshee teleportion at close range to the target will change between jumping beyond the target (reverse-side) and the default same-side. This can make the Banshee very difficult to predict at close range",
                                SubOptionKey = RBanshee.OPTION_REVERSE_SIDE_MIXIN
                            },
                            new RandomizationOption()
                            {
                                IsOptionOnly = true,
                                HumanName = "Variable teleportation distance",
                                Description = "Banshee teleportion distances will randomly vary from the default range (1x) to up to 20x. This will make the banshee close distance extremely quickly and will significantly increase game difficulty",
                                SubOptionKey = RBanshee.OPTION_JUMPDIST_MIXIN
                            },
                            new RandomizationOption()
                            {
                                IsOptionOnly = true,
                                HumanName = "Ignore invalid pathing",
                                Description = "Banshee teleportion will ignore pathing that it normally cannot use. This will let the Banshee get into areas it normally shouldn't be able to, such as on top of cover. This will allow the banshee to close distance faster",
                                SubOptionKey = RBanshee.OPTION_IGNOREINVALIDPATHING_MIXIN
                            }
                        }
                    },
                    //new RandomizationOption()
                    //{
                    //    HumanName = "Engineer",
                    //    Description = "Changes how the Engineer enemy behaves",
                    //    SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                    //    {
                    //        new RandomizationOption()
                    //        {
                    //            IsOptionOnly = true,
                    //            HumanName = "Place turret anywhere",
                    //            Description = "The Engineer can place its turret anywhere, instead of specified locations",
                    //            SubOptionKey = RBanshee.OPTION_REVERSE_SIDE_MIXIN
                    //        },
                    //        new RandomizationOption()
                    //        {
                    //            IsOptionOnly = true,
                    //            HumanName = "No turret suicide",
                    //            Description = "Disables the suicide timer on the Engineer turret",
                    //            SubOptionKey = RBanshee.OPTION_JUMPDIST_MIXIN
                    //        },
                    //        new RandomizationOption()
                    //        {
                    //            IsOptionOnly = true,
                    //            HumanName = "Repair anybody",
                    //            Description = "Allows the Engineer to heal any teammate, not just the Atlas and Turret",
                    //            SubOptionKey = RBanshee.OPTION_IGNOREINVALIDPATHING_MIXIN
                    //        }
                    //    }
                    //}
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Weapons & Enemies",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    //new RandomizationOption() {HumanName = "Weapon stats", Description = "Attempts to change gun stats in a way that makes game still playable", PerformSpecificRandomizationDelegate = Weapons.RandomizeWeapons, IsRecommended = true},
                    //new RandomizationOption() {HumanName = "Usable weapon classes", Description = "Changes what guns the player and squad can use", PerformSpecificRandomizationDelegate = Weapons.RandomizeSquadmateWeapons, IsRecommended = true},
                    new RandomizationOption() {
                        HumanName = "Enemies always berserk",
                        Description = "Changes enemy AI to go 'berserk'. Enemies in berserk mode will close distance as fast as possible. This setting will significantly increase game difficulty.",
                        PerformSpecificRandomizationDelegate = RSFXAI_Core.SetupBerserkerAI,
                        IsRecommended = false,
                        SliderTooltip = "The random chance that an enemy will spawn with berserk AI",
                        Ticks = "5,10,15,20,25,30,35,40,45,50,55,60,65,70,75,80,85,90,95,100",
                        SliderToTextConverter = val => $"{val}% chance to spawn berserk",
                        SliderValue = 25,
                        HasSliderOption = true,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning
                    },
                                        new RandomizationOption() {HumanName = "Pawn weapons",
                                            Description = "Every spawned combat pawn (except squadmates) will have a random weapon. May introduce a small amount of game stutter on enemy spawn",
                                            PerformSpecificRandomizationDelegate = REnemyWeapons.RandomizeEnemyWeapons,
                                            Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                                            IsRecommended = true,
                                            // Debug stuff.
                    #if DEBUG
                                            //HasSliderOption = true,
                                            //Ticks = string.Join(",",Enumerable.Range(-1,EnemyWeaponChanger.AllAvailableWeapons.Count + 1)),
                                            //SliderToTextConverter = x =>
                                            //{
                                            //    if (x < 0)
                                            //        return "All weapons";
                                            //    var idx = (int) x;
                                            //    return EnemyWeaponChanger.AllAvailableWeapons[idx].GunName;
                                            //},
                                            //SliderValue = -1, // End debug stuff
                    #endif
                                        },
                                        new RandomizationOption() {
                                            HumanName = "Enemy weapons can penetrate",
                                            Description = "Allows enemy weapons to penetrate cover and walls. This only has an effect if the enemy weapon has innate penetration ability, which most don't by default",
                                            PerformSpecificRandomizationDelegate = RSFXGameGeneric.AllowEnemyWeaponPenetration,
                                            IsRecommended = true,
                                        },
                                        new RandomizationOption()
                                        {
                                            HumanName = "Enemy powers", Description = "Gives non-player characters different powers - at least one option must be chosen. Can introduce some slight stutter on enemy load. Will significantly increase game difficulty.",
                                            PerformFileSpecificRandomization = REnemyPowers.PerFileAIChanger,
                                            PerformSpecificRandomizationDelegate = REnemyPowers.Init,
                                            Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                                            IsRecommended = true,
                                            SubOptions =new ObservableCollectionExtended<RandomizationOption>()
                                            {
                                                new RandomizationOption()
                                                {
                                                    HumanName = "Vanilla powers",
                                                    Description = "Allows vanilla powers to the power pool",
                                                    SubOptionKey = REnemyPowers.OPTION_VANILLA,
                                                    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal,
                                                    IsOptionOnly = true,
                                                },
                                                new RandomizationOption()
                                                {
                                                    HumanName = "New powers",
                                                    Description = "Allows powers designed for this randomizer to be added to the power pool",
                                                    SubOptionKey = REnemyPowers.OPTION_NEW,
                                                    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                                                    IsOptionOnly = true,
                                                },new RandomizationOption()
                                                {
                                                    HumanName = "Placeholder",
                                                    Description = "Dummy option",
                                                    SubOptionKey = REnemyPowers.OPTION_PORTED,
                                                    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal,
                                                    IsOptionOnly = true,
                                                }
                                            }
                                            // Debug stuff.
                    #if DEBUG
                                            //HasSliderOption = true,
                                            //Ticks = string.Join(",",Enumerable.Range(-1,EnemyPowerChanger.Powers.Count + 1)),
                                            //SliderToTextConverter = x =>
                                            //{
                                            //    if (x < 0)
                                            //        return "All powers";
                                            //    var idx = (int) x;
                                            //    return EnemyPowerChanger.Powers[idx].PowerName;
                                            //},
                                            //SliderValue = -1, // End debug stuff
                    #endif
                                        },

                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Level-specific",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    //    new RandomizationOption() {HumanName = "Normandy", Description = "Changes various things around the ship, including one sidequest", PerformSpecificRandomizationDelegate = Normandy.PerformRandomization, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, IsRecommended = true},
                    //    //new RandomizationOption() {HumanName = "Prologue"},
                    //    //new RandomizationOption() {HumanName = "Tali Acquisition"}, //sfxgame tla damagetype
                        new RandomizationOption() {HumanName = "Citadel (Not DLC)", Description = "Changes many things across the Citadel", PerformSpecificRandomizationDelegate = CitHub.RandomizeLevel,
                            RequiresTLK = true, RequiresAudio = true, IsRecommended = true},
                    //    new RandomizationOption() {HumanName = "Archangel Acquisition", Description = "It's a mystery!", PerformSpecificRandomizationDelegate = ArchangelAcquisition.PerformRandomization, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, IsRecommended = true, RequiresTLK = true},
                    //    new RandomizationOption() {HumanName = "Illium Hub", Description = "Changes the lounge", PerformSpecificRandomizationDelegate = IlliumHub.PerformRandomization, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, IsRecommended = true},
                    //    new RandomizationOption() {HumanName = "Omega Hub", Description = "Improved dancing technique", PerformSpecificRandomizationDelegate = OmegaHub.PerformRandomization, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, IsRecommended = true},
                    //    new RandomizationOption() {HumanName = "Suicide Mission", Description = "Significantly changes level. Greatly increases difficulty", PerformSpecificRandomizationDelegate = CollectorBase.PerformRandomization, RequiresTLK = true, IsRecommended = true},
                    //
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Squad powers",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    //new RandomizationOption()
                    //{
                    //    HumanName = "Class powers",
                    //    Description = "Shuffles the powers of all player classes. Loading an existing save after running this will cause you to lose talent points. Use the refund points button below to adjust your latest save file and reset your powers.",
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                    //    IsRecommended = true,
                    //    PerformSpecificRandomizationDelegate = ClassTalents.ShuffleClassAbilitites,
                    //    RequiresTLK = true,
                    //    SetupRandomizerDelegate = HenchTalents.ResetTalents,
                    //    SetupRandomizerButtonToolTip = "Allows you to select a save file to remove player power records from.\nThis will wipe all assigned power points and refund the correct amount of talent points to spend.",
                    //    SetupRandomizerButtonText = "Refund points",
                    //    /* Will have to implement later as removing gating code is actually complicated
                    //    SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                    //    {
                    //        new RandomizationOption()
                    //        {
                    //            IsOptionOnly = true,
                    //            SubOptionKey = HenchTalents.SUBOPTION_HENCHPOWERS_REMOVEGATING,
                    //            HumanName = "Remove rank-up gating",
                    //            Description = "Removes the unlock requirement for the second power slot. The final power slot will still be gated by loyalty."
                    //        }
                    //    }*/
                    //},

#if DEBUG
                    // Needs fixes in porting code...
                    //new RandomizationOption()
                    //{
                    //    HumanName = "Henchmen powers",
                    //    Description = "Shuffles the powers of squadmates. Loading an existing save after running this will cause them to lose talent points. Use the refund points button below to adjust your latest save file and reset their powers.",
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                    //    IsRecommended = true,
                    //    PerformSpecificRandomizationDelegate = HenchTalents.ShuffleSquadmateAbilities,
                    //    SetupRandomizerDelegate = HenchTalents.ResetTalents,
                    //    SetupRandomizerButtonToolTip = "Allows you to select a save file to remove henchman records from.\nThis will wipe all henchman powers and refund the correct amount of talent points to spend.\nThis will ALSO reset the weapon they are using the default weapon they use.",
                    //    SetupRandomizerButtonText = "Refund points",
                    //    RequiresTLK = true,
                    //    SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                    //    {
                    //        new RandomizationOption()
                    //        {
                    //            IsOptionOnly = true,
                    //            SubOptionKey = HenchTalents.SUBOPTION_HENCHPOWERS_REMOVEGATING,
                    //            HumanName = "Remove rank-up gating",
                    //            Description = "Removes the unlock requirement for the second power slot. The final power slot will still be gated by loyalty."
                    //        }
                    //    }
                    //},
#endif
                }
            });


            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Gameplay",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption() {HumanName = "Enemy spawns", Description = "Runtime randomization of what enemies will spawn",
                        PerformSpecificRandomizationDelegate = RSFXSeqAct_AIFactory2.Init,
                        PerformRandomizationOnExportDelegate = RSFXSeqAct_AIFactory2.RandomizeSpawnSets, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Unsafe},

                    //new RandomizationOption() {HumanName = "Skip minigames", Description = "Skip all minigames. Doesn't even load the UI, just skips them entirely", PerformRandomizationOnExportDelegate = SkipMiniGames.DetectAndSkipMiniGameSeqRefs, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal},
                    //new RandomizationOption()
                    //{
                    //    HumanName = "Enable basic friendly fire",
                    //    PerformSpecificRandomizationDelegate = SFXGame.TurnOnFriendlyFire,
                    //    Description = "Enables weapons to damage friendlies (enemy and player)",
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal,
                    //    SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                    //    {
                    //        new RandomizationOption()
                    //        {
                    //            IsOptionOnly = true,
                    //            SubOptionKey = SFXGame.SUBOPTIONKEY_CARELESSFF,
                    //            HumanName = "Careless mode",
                    //            Description = "Attack enemies, regardless of friendly casualties"
                    //        }
                    //    }
                    //},
                    //new RandomizationOption()
                    //{
                    //    HumanName = "Shepard ragdollable",
                    //    Description = "Makes Shepard able to be ragdolled from various powers/attacks. Can greatly increase difficulty",
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                    //    PerformSpecificRandomizationDelegate = SFXGame.MakeShepardRagdollable,
                    //},
                    //new RandomizationOption()
                    //{
                    //    HumanName = "Remove running camera shake",
                    //    Description = "Removes the camera shake when running",
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                    //    PerformSpecificRandomizationDelegate = SFXGame.RemoveStormCameraShake,
                    //},
                    //new RandomizationOption()
                    //{
                    //    HumanName = "One hit kill",
                    //    Description = "Makes Shepard die upon taking any damage. Removes bonuses that grant additional health. Extremely difficult, do not mix with other randomizers",
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Unsafe,
                    //    PerformSpecificRandomizationDelegate = OneHitKO.InstallOHKO,
                    //},
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "DLC",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    //    //new RandomizationOption() {HumanName = "Overlord DLC", Description = "Changes many things across the DLC", PerformSpecificRandomizationDelegate = OverlordDLC.PerformRandomization, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal, IsRecommended = true},
                    //    //new RandomizationOption() {HumanName = "Arrival DLC", Description = "Changes the relay colors", PerformSpecificRandomizationDelegate = ArrivalDLC.PerformRandomization, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, IsRecommended = true},
                    //    //new RandomizationOption() {HumanName = "Genesis DLC", Description = "Completely changes the backstory", PerformSpecificRandomizationDelegate = GenesisDLC.PerformRandomization, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, RequiresTLK = true, IsRecommended = true},
                    //    //new RandomizationOption() {HumanName = "Kasumi DLC", Description = "Changes the art gallery", PerformSpecificRandomizationDelegate = KasumiDLC.PerformRandomization, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, IsRecommended = true},
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Level-components",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    // Doesn't seem to work
                    //                    new RandomizationOption() {HumanName = "Star colors", IsRecommended = true, PerformRandomizationOnExportDelegate = RBioSun.PerformRandomization},
                    new RandomizationOption() {HumanName = "Fog colors", Description = "Changes colors of fog", IsRecommended = true, PerformRandomizationOnExportDelegate = RSharedHeightFogComponent.RandomizeExport, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe},
                    new RandomizationOption() {
                        HumanName = "Post Processing volumes",
                        Description = "Changes postprocessing. Likely will make some areas of game unplayable",
                        PerformRandomizationOnExportDelegate = RPostProcessingVolume.RandomizeExport,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_RIP
                    },
                    new RandomizationOption() {HumanName = "Light colors", Description = "Changes colors of dynamic lighting",
                        PerformRandomizationOnExportDelegate = RSharedLighting.RandomizeExport,
                        IsRecommended = true,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe},
                    //new RandomizationOption() {HumanName = "Mission rewards", Description = "Randomizes the tech and weapons given to you at the end of a mission. You can still get all the tech and weapons if you complete all the missions that award them.",
                    //    //PerformSpecificRandomizationDelegate = MissionRewards.Inventory,
                    //    PerformSpecificRandomizationDelegate = MissionRewards.PerformRandomization,
                    //    IsRecommended = true,
                    //    ProgressIndeterminate = true,
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe},
                    new RandomizationOption() {
                        HumanName = "Galaxy Map",
                        Description = "Rewrites the galaxy map",
                        RequiresTLK = true,
                        PerformSpecificRandomizationDelegate = GalaxyMap.InstallGalaxyMapRewrite,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                        //SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                        //{
                        //    new RandomizationOption()
                        //    {
                        //        SubOptionKey = GalaxyMap.SUBOPTIONKEY_INFINITEGAS,
                        //        HumanName = "Infinite fuel",
                        //        Description = "Prevents the Normandy from running out of fuel. Prevents possible softlock due to randomization",
                        //        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        //        IsOptionOnly = true
                        //    }
                        //}
                    },
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Text",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption() {HumanName = "Game over text", PerformSpecificRandomizationDelegate = RSharedTexts.RandomizeGameOverText, RequiresTLK = true, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, IsRecommended = true},
                    new RandomizationOption() {HumanName = "Intro Crawl", PerformSpecificRandomizationDelegate = RSharedTexts.RandomizeOpeningCrawl, RequiresTLK = true, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, IsRecommended = true},
                    new RandomizationOption()
                    {
                        HumanName = "Vowels",
                        IsPostRun = true,
                        Description="Changes vowels in text in a consistent manner, making a 'new' language",
                        PerformSpecificRandomizationDelegate = RSharedTexts.RandomizeVowels,
                        RequiresTLK = true,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                        MutualExclusiveSet="AllText",
                        StateChangingDelegate=optionChangingDelegate,
                        SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                        {
                            new RandomizationOption()
                            {
                                SubOptionKey = RSharedTexts.SUBOPTIONKEY_VOWELS_HARDMODE,
                                HumanName = "Hurd Medi",
                                Description = "Adds an additional 2 consonants to swap (for a total of 4 letter changes). Can make text extremely challenging to read",
                                Dangerousness = RandomizationOption.EOptionDangerousness.Danger_RIP,
                                IsOptionOnly = true
                            }
                        }
                    },
                    new RandomizationOption() {HumanName = "UwU",
                        Description="UwUifies all text in the game, often hilarious. Based on Jade's OwO mod", PerformSpecificRandomizationDelegate = RSharedTexts.UwuifyText,
                        RequiresTLK = true, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, MutualExclusiveSet="AllText",
                        StateChangingDelegate=optionChangingDelegate,
                        IsPostRun = true,
                        SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                        {
                            new RandomizationOption()
                            {
                                IsOptionOnly = true,
                                HumanName = "Keep casing",
                                Description = "Keeps upper and lower casing.",
                                SubOptionKey = RSharedTexts.SUBOPTIONKEY_UWU_KEEPCASING,
                            },
                            new RandomizationOption()
                            {
                                IsOptionOnly = true,
                                HumanName = "Emoticons",
                                Description = "Adds emoticons ^_^\n'Keep casing' recommended. Might break email or mission summaries, sowwy UwU",
                                Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                                SubOptionKey = RSharedTexts.SUBOPTIONKEY_REACTIONS_ENABLED
                            }
                        }
                    },
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Wackadoodle",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption() {
                        HumanName = "Actors in cutscenes",
                        Description="Swaps pawns around in animated cutscenes. May break some due to complexity, but often hilarious",
                        PerformRandomizationOnExportDelegate = Cutscene.ShuffleCutscenePawns2,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                        IsRecommended = true
                    },
                    new RandomizationOption() {
                        HumanName = "Gestures in conversations",
                        Description="Changes animations used in conversations",
                        PerformRandomizationOnExportDelegate = ConversationGestures.RandomizeConversationGestures,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal,
                        RequiresGestures = true,
                        IsRecommended = false
                    },
                    new RandomizationOption() {
                            HumanName = "Animation data",
                            PerformRandomizationOnExportDelegate = RSharedAnimSequence.RandomizeExport,
                            SliderToTextConverter = RSharedAnimSequence.UIConverter,
                            HasSliderOption = true,
                            SliderValue = 1,
                            Ticks = "1,2",
                            Description="Fuzzes rigged bone positions and rotations",
                            IsRecommended = true,
                            SliderTooltip = "Value determines which bones are used in the remapping. Default value is basic bones only.",
                            Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal
                    },
                    new RandomizationOption()
                    {
                        HumanName = "Random interpolations",
                        Description = "Randomly fuzzes interpolation data. Can make game very dizzying on higher values!",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_RIP,
                        PerformRandomizationOnExportDelegate = RSharedInterpTrackMove.RandomizeExport,
                        Ticks = "0.025,0.05,0.075,0.1,0.15,0.2,0.3,0.4,0.5",
                        HasSliderOption = true,
                        SliderTooltip = "Higher settings yield more extreme position and rotational changes to interpolations. Values above 0.05 are very likely to make the game unplayable. Default value is 0.05.",
                        SliderToTextConverter = x=> $"Maximum interp change: {Math.Round(x * 100)}%",
                        SliderValue = 0.05,
                    },
                    //new RandomizationOption()
                    //{
                    //    HumanName = "Conversation Wheel", PerformRandomizationOnExportDelegate = RBioConversation.RandomizeExportReplies,
                    //    Description = "Changes replies in wheel. Can make conversations hard to exit",
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Unsafe
                    //},
                    //new RandomizationOption()
                    //{
                    //    HumanName = "Actors in conversations",
                    //    PerformFileSpecificRandomization = RBioConversation.RandomizePackageActorsInConversation,
                    //    Description = "Changes pawn roles in conversations. Somewhat buggy simply due to complexity and restrictions in engine, but can be entertaining",
                    //    IsRecommended = true,
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning
                    //},
                    // Crashes game too often :/
                    //new RandomizationOption()
                    //{
                    //    HumanName = "Music",
                    //    PerformSpecificRandomizationDelegate = RMusic.Init,
                    //    PerformRandomizationOnExportDelegate = RMusic.RandomizeMusic,
                    //    Description = "Changes what audio is played. Due to how audio is layered in ME2 this may be annoying",
                    //    IsRecommended = false,
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning
                    //}
                }
            });

            foreach (var g in RandomizationGroups)
            {
                if (g.Options != null)
                {
                    g.Options.Sort(x => x.HumanName);
                }
            }
            RandomizationGroups.Sort(x => x.GroupName);
        }
    }
}
#endif