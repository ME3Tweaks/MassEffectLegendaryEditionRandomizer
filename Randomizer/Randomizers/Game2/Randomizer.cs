#if __GAME2__
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Misc.ME3Tweaks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Misc;
using Microsoft.WindowsAPICodePack.Win32Native.NamedPipe;
using Randomizer.MER;
using Randomizer.Randomizers.Game1.Misc;
using Randomizer.Randomizers.Game2.Enemy;
using Randomizer.Randomizers.Game2.ExportTypes;
using Randomizer.Randomizers.Game2.Levels;
using Randomizer.Randomizers.Game2.Misc;
using Randomizer.Randomizers.Game2.TextureAssets;
using Randomizer.Randomizers.Game2.TextureAssets.LE2;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Shared;
using Randomizer.Randomizers.Shared.Classes;
using Randomizer.Randomizers.Utility;
using Serilog;
using File = System.IO.File;

namespace Randomizer.Randomizers.Game2
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
            ResetClasses();
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

            List<string> installTimeOnlyPackages = new List<string>();
            Exception rethrowException = null;
            try
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                // Initialize FileSystem and handlers
                MERFileSystem.InitMERFS(SelectedOptions);

                // Initialize the global cache system
                MERCaches.Init(SelectedOptions.RandomizationTarget);

                // Initialize any special items here
                if (SelectedOptions.SelectedOptions.Any(x => x.RequiresGestures))
                {
                    // Prepare runtime gestures data
                    GestureManager.Init(SelectedOptions.RandomizationTarget);
                }



                // Install MERControl as many things will depend on it
                MERControl.InstallMERControl(SelectedOptions.RandomizationTarget);

                // Prepare the textures
                LE2Textures.SetupLE2Textures(SelectedOptions.RandomizationTarget);

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

                // Load MER-specific kismet objects
                foreach (var package in MEREmbedded.ExtractEmbeddedBinaryFolder("Packages.LE2.InstallSessionOnly"))
                {
                    if (!package.RepresentsPackageFilePath())
                    {
                        continue;
                    }
                    var actualPath = Path.Combine(MERFileSystem.DLCModCookedPath, MEREmbedded.GetFilenameFromAssetName(package));
                    installTimeOnlyPackages.Add(actualPath);

                    MERLog.Information($"Inventorying kismet package {actualPath}");
                    using var p = MEPackageHandler.OpenMEPackage(actualPath);
                    foreach (var ex in p.Exports.Where(x => x.IsClass && x.InheritsFrom("SequenceObject")))
                    {
                        var classInfo = GlobalUnrealObjectInfo.generateClassInfo(ex);
                        var defaults = p.GetUExport(ObjectBinary.From<UClass>(ex).Defaults);
                        MERLog.Information($@"Inventorying class {ex.InstancedFullPath}");
                        GlobalUnrealObjectInfo.GenerateSequenceObjectInfoForClassDefaults(defaults);
                        GlobalUnrealObjectInfo.InstallCustomClassInfo(ex.ObjectName, classInfo, ex.Game);
                    }
                }

                if (SelectedOptions.SelectedOptions.Any(x => x.RequiresTLK))
                {
                    // Merge TLK data - this makes lookups work
                    TLKBuilder.MergeEmbeddedTLKs();
                }

                // Save SFXGame to disk here
                SFXGame.GetSFXGame(SelectedOptions.RandomizationTarget).Save();


                MERLog.Information($"Initialization complete, took {sw.ElapsedMilliseconds}ms");
                // Pass 1: All randomizers that are file specific and are not post-run
                foreach (var sr in specificRandomizers.Where(x => !x.IsPostRun))
                {
                    sr.OnOperationUpdate += srUpdate;
                    MERLog.Information($"Running specific randomizer {sr.HumanName}");
                    SelectedOptions.SetCurrentOperationText?.Invoke($"Randomizing {sr.HumanName}");
                    sr.RandomizationInProgress = true;
                    sr.PerformSpecificRandomizationDelegate?.Invoke(SelectedOptions.RandomizationTarget, sr);
                    sr.OnOperationUpdate -= srUpdate;
                    sr.RandomizationInProgress = false;
                }

                // Pass 2: All exports
                if (perExportRandomizers.Any() || perFileRandomizers.Any())
                {
                    SelectedOptions.SetCurrentOperationText?.Invoke("Getting list of files...");
                    SelectedOptions.SetOperationProgressBarIndeterminate?.Invoke(true);

                    // we only want pcc files (me2/me3). no upks
                    var files = MELoadedFiles.GetFilesLoadedInGame(SelectedOptions.RandomizationTarget.Game, true, false, false, SelectedOptions.RandomizationTarget.TargetPath).Values.Where(x => !MERFileSystem.filesToSkip.Contains(Path.GetFileName(x))).ToList();

                    SelectedOptions.SetOperationProgressBarIndeterminate?.Invoke(false);


                    foreach (var r in perExportRandomizers.Concat(perFileRandomizers))
                    {
                        r.RandomizationInProgress = true;
                    }

                    var currentFileNumber = 0;
                    var totalFilesCount = files.Count;

#if DEBUG
                    Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = SelectedOptions.UseMultiThread ? 4 : 1 }, (file) =>
#else
                    Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = SelectedOptions.UseMultiThread ? 4 : 1 }, (file) =>
#endif
                    {

                        var name = Path.GetFileNameWithoutExtension(file);
                        if (SpecializedFiles.Contains(name, StringComparer.InvariantCultureIgnoreCase)) return; // Do not run randomization on this file as it's only done by specialized randomizers (e.g. char creator)

                        Interlocked.Increment(ref currentFileNumber);
                        SelectedOptions.SetOperationProgressBarProgress?.Invoke(currentFileNumber, totalFilesCount);

                        SelectedOptions.SetCurrentOperationText?.Invoke($"Randomizing game files [{currentFileNumber}/{files.Count}]");

#if DEBUG
                        //if (true
                        ////&& false //uncomment to disable filtering
                        //// && !file.Contains("BioH", StringComparison.InvariantCultureIgnoreCase)
                        //// && !file.Contains("ProFre", StringComparison.InvariantCultureIgnoreCase)
                        ////&& !file.Contains("TwrHub", StringComparison.InvariantCultureIgnoreCase)
                        //// && !file.Contains("ProCer", StringComparison.InvariantCultureIgnoreCase)
                        //)
                        //    return;
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


                foreach (var r in perExportRandomizers.Concat(perFileRandomizers))
                {
                    r.RandomizationInProgress = false;
                }


                // Pass 3: All randomizers that are file specific and are post-run
                foreach (var sr in specificRandomizers.Where(x => x.IsPostRun))
                {
                    try
                    {
                        sr.OnOperationUpdate += srUpdate;
                        sr.RandomizationInProgress = true;
                        SelectedOptions.SetOperationProgressBarIndeterminate?.Invoke(true);
                        MERLog.Information($"Running post-run specific randomizer {sr.HumanName}");
                        SelectedOptions.SetCurrentOperationText?.Invoke($"Randomizing {sr.HumanName}");
                        sr.PerformSpecificRandomizationDelegate?.Invoke(SelectedOptions.RandomizationTarget, sr);
                        sr.RandomizationInProgress = false;
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

            // Close out files and free memory
            TextureHandler.EndHandler(SelectedOptions.RandomizationTarget);
            CoalescedHandler.EndHandler();
            TLKBuilder.EndHandler();
            MERFileSystem.Finalize(SelectedOptions);
            ResetClasses();
            CleanupInstallTimeOnlyFiles(installTimeOnlyPackages);
            MemoryManager.ResetMemoryManager();
            MemoryManager.SetUsePooledMemory(false);

            // Create TOC
            TOCCreator.CreateTOCForGame(SelectedOptions.RandomizationTarget.Game, gameRootOverride: SelectedOptions.RandomizationTarget.TargetPath);

            // Test for DLC merge folder.
            // If it exists, we need to recreate it
            if (SelectedOptions.RandomizationTarget.GetInstalledDLC().Contains("DLC_MOD_M3_MERGE"))
            {
                MERModManagerIntegration.RequestM3Merge(SelectedOptions.RandomizationTarget.Game);
            }

            // Re-throw the unhandled exception after MERFS has closed
            if (rethrowException != null)
                throw rethrowException;
        }

        private void CleanupInstallTimeOnlyFiles(List<string> installTimeOnlyPackages)
        {
            foreach (var f in installTimeOnlyPackages)
            {
                File.Delete(f);
            }
        }

        /// <summary>
        /// Ensures things are set back to normal before first run
        /// </summary>
        private void ResetClasses()
        {
            RSharedMorphTarget.ResetClass();
            SquadmateHead.ResetClass();
            PawnPorting.ResetClass();
            NPCHair.ResetClass();
            MERControl.ResetClass();
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
                        Description="Changes facial animation. Higher values yield more extreme facial animation",
                        HumanName = "FaceFX animation", Ticks = "1,2,3,4", HasSliderOption = true, IsRecommended = true, SliderToTextConverter = rSetting =>
                            rSetting switch
                            {
                                1 => "Oblivion",
                                2 => "Knights of the old Republic",
                                3 => "Sonic Adventure",
                                4 => "Source filmmaker",
                                // 5 => "Total madness",
                                _ => "Error"
                            },
                        SliderTooltip = "Higher settings yield more extreme facial animation values. Default value is Sonic Adventure",
                        SliderValue = 3, // This must come after the converter
                        PerformRandomizationOnExportDelegate = RSharedFaceFXAnimSet.RandomizeExport,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        GoodTimeRandomizer = true
                    },
                    new RandomizationOption() {HumanName = "Squadmate heads",
                        Description = "Changes the heads of your squadmates",
                        PerformSpecificRandomizationDelegate = SquadmateHead.ApplyFixes,
                        PerformRandomizationOnExportDelegate = SquadmateHead.RandomizeExport2,
                        // PerformFileSpecificRandomization = SquadmateHead.FilePrerun, // Miranda/Jacob fixes for ProCer
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        RequiresTLK = true,
                        StateChangingDelegate=optionChangingDelegate,
                        MutualExclusiveSet = "SquadHead",
                        IsRecommended = true,
                        GoodTimeRandomizer = true
                    },
                    //new RandomizationOption() {HumanName = "Squadmate faces",
                    //    Description = "Only works on Wilson and Jacob, unfortunately. Other squadmates are fully modeled",
                    //    PerformSpecificRandomizationDelegate = RBioMorphFace.RandomizeSquadmateFaces,
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                    //    MutualExclusiveSet = "SquadHead",
                    //    StateChangingDelegate=optionChangingDelegate,
                    //},

                    // Non-dynamic
                    /*
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
                    },*/

                    new RandomizationOption()
                    {
                        HumanName = MERRuntimeOption.RTO_TITLE_NPCFACES,
                        Ticks = "0.5,1.0,1.5,2.0,3.0,4.0",
                        HasSliderOption = true,
                        IsRecommended = true,
                        SliderTooltip = "Higher settings yield more ridiculous faces for characters. Default value is 1.5, which provides moderate facial randomization.",
                        SliderToTextConverter = rSetting => $"Randomization window: -{rSetting} to {rSetting}",
                        SliderValue = 1.5, // This must come after the converter
                        PerformSpecificRandomizationDelegate = RBioMorphFace.RandomizeBioMorphFace2,
                        Description="Changes the face morphs on most pawns",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        IsRuntimeRandomizer = true,
                        GoodTimeRandomizer = true,
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
                    new RandomizationOption() {
                        HumanName = MERRuntimeOption.RTO_TITLE_EYES,
                        Description="Changes the types and colors of eyes",
                        IsRecommended = true,
                        //PerformSpecificRandomizationDelegate = RSharedEyes.Init,
                        //PerformRandomizationOnExportDelegate = RSharedEyes.RandomizeExport2,
                        PerformSpecificRandomizationDelegate = RSharedEyes.InstallEyeRandomizer,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        IsRuntimeRandomizer = true,
                        GoodTimeRandomizer = true
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
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal,
                        GoodTimeRandomizer = true
                    },
                    new RandomizationOption() {
                        HumanName = MERRuntimeOption.RTO_TITLE_NPCCOLORS,
                        Description="Changes NPC colors such as skin tone, hair, etc",
                        PerformSpecificRandomizationDelegate = RSharedNPC.InstallPawnColorRandomizer,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal,
                        IsRecommended = true,
                        IsRuntimeRandomizer = true,
                        GoodTimeRandomizer = true,
                    },
                    // Too much work for LE2R
                    //new RandomizationOption() {
                    //    HumanName = "NPC hair",
                    //    Description="Randomizes the hair on NPCs that have a hair mesh",
                    //    PerformRandomizationOnExportDelegate = NPCHair.RandomizeExport,
                    //    PerformSpecificRandomizationDelegate = NPCHair.Init,
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal
                    //},
                    new RandomizationOption() {
                        HumanName = MERRuntimeOption.RTO_TITLE_ROMANCE,
                        Description="Randomizes which romance you will get",
                        IsRuntimeRandomizer = true,
                        PerformSpecificRandomizationDelegate = Romance.PerformRandomization,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                        IsRecommended = true,
                        GoodTimeRandomizer = true,
                        // IsRuntimeRandomizer = true, // Technically this could be coded in but i'm lazy

                    },
                    // This is not very interesting tbh
                    //new RandomizationOption() {
                    //    HumanName = "Look At Targets",
                    //    Description="Maps have designated areas where pawns may look; this changes those po where pawns look",
                    //    PerformRandomizationOnExportDelegate = RSharedBioLookAtTarget.RandomizeExport,
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe
                    //},
                    new RandomizationOption() {
                        HumanName = MERRuntimeOption.RTO_TITLE_LOOKAT_SYSTEM,
                        Description="Changes how pawns look things, such as turning eyes, chest, neck",
                        IsConfigControlled = true,
                        IsRuntimeRandomizer = true,
                        IsRecommended = true,
                        PerformSpecificRandomizationDelegate = RSharedBioLookAtTarget.RandomizeBioPawnLookatController,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        GoodTimeRandomizer = true,
                    },
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Character Creator",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption() {
                        HumanName = "Player face morph options",
                        IsRecommended = true,
                        Description = "Extends the range of allowed values for morphing the player's face. This will cause more extreme facial features. This also makes infinite random presets.",
                        PerformSpecificRandomizationDelegate = CharacterCreator.RandomizeCharacterCreator,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        Ticks = "1.25,1.5,1.75,2.0,2.25,2.5,2.75,3.0",
                        HasSliderOption = true,
                        SliderTooltip = "Higher settings yield more ridiculous faces for characters. The original game value is 1.0, higher values are multiplicative.",
                        SliderToTextConverter = rSetting => $"Morph multiplier: {rSetting}x",
                        SliderValue = 2.0, // This must come after the converter
                        GoodTimeRandomizer = true,
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
                    new RandomizationOption()
                    {
                        HumanName = MERRuntimeOption.RTO_TITLE_ICONICFACES,
                        Description="Changes the 'iconic' player faces - Note, this only properly works if you pick 'Custom' character with the default appearance. This modification will not carry into LE3.",
                        PerformSpecificRandomizationDelegate = CharacterCreator.InstallIconicRandomizer,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        Ticks = "0.5,1.0,1.5,2.0,3.0,4.0",
                        HasSliderOption = true,
                        IsRecommended = true,
                        SliderTooltip = "Higher settings yield more ridiculous faces for characters. Default value is 1.5, which provides moderate facial randomization.",
                        SliderToTextConverter = rSetting => $"Randomization window: -{rSetting} to {rSetting}",
                        SliderValue = 1.5, // This must come after the converter
                        IsRuntimeRandomizer = true,
                        GoodTimeRandomizer = true,
                        SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                        {
                            new RandomizationOption()
                            {
                                SubOptionKey = CharacterCreator.SUBOPTIONKEY_CHARCREATOR_ICONIC_PERSISTENCE,
                                HumanName = MERRuntimeOption.RTO_TITLE_ICONICPERSISTENT,
                                Description = "Selecting this option will make your face from the character creator persist through saves. You MUST keep your randomization installed for this to work.",
                                Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                                IsOptionOnly = true
                            }
                        }
                    },
                    new RandomizationOption()
                    {
                        HumanName = "Psychological profiles",
                        Description="Completely changes the backstories of Shepard, with both new stories and continuations from ME1 Randomizer's stories",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        IsRecommended = true,
                        PerformSpecificRandomizationDelegate = CharacterCreator.RandomizePsychProfiles,
                        RequiresTLK = true,
                        GoodTimeRandomizer = true,
                    },
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Miscellaneous",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption() {
                        HumanName = "Hologram colors",
                        Description="Changes colors of holograms",
                        PerformRandomizationOnExportDelegate = RSharedHolograms.RandomizeExport,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        IsRecommended = true,
                        GoodTimeRandomizer = true,
                    },
                    // new RandomizationOption() {HumanName = "Drone colors", Description="Changes colors of drones",PerformRandomizationOnExportDelegate = CombatDrone.RandomizeExport, IsRecommended = true},
                    //new RandomizationOption() {HumanName = "Omnitool", Description="Changes colors of omnitools",PerformRandomizationOnExportDelegate = ROmniTool.RandomizeExport},
                    new RandomizationOption() {
                        HumanName = "Specific textures",
                        Description="Changes specific textures to more fun ones",
                        PerformRandomizationOnExportDelegate = TextureHandler.RandomizeExport,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        IsRecommended = true,
                        GoodTimeRandomizer = true
                    },
                    new RandomizationOption() {HumanName = "SizeSixteens mode",
                        Description = "This option installs a change specific for the streamer SizeSixteens. If you watched his ME1 Randomizer streams, you'll understand the change.",
                        PerformSpecificRandomizationDelegate = SizeSixteens.InstallSSChanges,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        RequiresTLK = true,
                        // This is a specific 'Good Time'
                    },
                    new RandomizationOption() {HumanName = "NPC names",
                        Description = "Install a list of names into the game and renames some of the generic NPCs to them. You can install your stream chat members, for example. There are 48 name slots.",
                        PerformSpecificRandomizationDelegate = CharacterNames.InstallNameSet,
                        SetupRandomizerDelegate = CharacterNames.SetupRandomizer,
                        SetupRandomizerButtonText = "Setup",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        RequiresTLK = true,
                        GoodTimeRandomizer = true,
                    }
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Movement & pawns",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption() {
                        HumanName = MERRuntimeOption.RTO_TITLE_NPCMOVEMENT,
                        Description = "Changes non-player movement stats",
                        PerformSpecificRandomizationDelegate = PawnMovementSpeed.RandomizeNonPlayerMovementSpeed,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        IsRecommended = true,
                        GameplayRandomizer = true,
                        IsRuntimeRandomizer = true,
                    },
                    new RandomizationOption() {
                        HumanName = MERRuntimeOption.RTO_TITLE_PLAYERMOVEMENT,
                        Description = "Changes player movement stats",
                        PerformSpecificRandomizationDelegate = PawnMovementSpeed.RandomizePlayerMovementSpeed,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal,
                        IsRuntimeRandomizer = true,
                        GameplayRandomizer = true
                    },
                    //new RandomizationOption() {HumanName = "NPC walking routes", PerformRandomizationOnExportDelegate = RRoute.RandomizeExport}, // Seems very specialized in ME2
                    new RandomizationOption() {HumanName = "Hammerhead",
                        IsRecommended = true,
                        Description = "Changes HammerHead stats",
                        PerformSpecificRandomizationDelegate = HammerHead.PerformRandomization,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal,
                        GameplayRandomizer = true
                    },
                    new RandomizationOption() {HumanName = "'Lite' pawn animations",
                        IsRecommended = true,
                        Description = "Changes the animations used by basic non-interactable NPCs. Some may T-pose due to the sheer complexity of this randomizer",
                        PerformRandomizationOnExportDelegate = RSFXSkeletalMeshActorMAT.RandomizeBasicGestures,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal, RequiresGestures = true,
                        GoodTimeRandomizer = true
                    },
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Weapons (general)",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption()
                    {
                        HumanName = MERRuntimeOption.RTO_TITLE_WEAPONSTATS,
                        IsRuntimeRandomizer = true,
                        Description = "Attempts to change gun stats in a way that makes game still playable",
                        PerformSpecificRandomizationDelegate = Weapons.RandomizeWeapons,
                        IsRecommended = true,
                        GameplayRandomizer = true,
                    },
                    new RandomizationOption()
                    {
                        HumanName = "Usable weapon classes",
                        Description = "Changes what gun categories the player and squad can use",
                        PerformSpecificRandomizationDelegate = Weapons.RandomizeSquadmateWeapons,
                        IsRecommended = true,
                        GameplayRandomizer = true,
                    },
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = MERRuntimeOption.RTO_TITLE_ENEMYWEAPONS,
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption()
                    {
                        HumanName = "Enemy weapons",
                        Description = "Gives enemies different guns. Enemies custom made for this randomizer will not have their weapons randomized",
                        PerformRandomizationOnExportDelegate = EnemyWeaponChanger.RandomizeExport,
                        PerformSpecificRandomizationDelegate = EnemyWeaponChanger.Init,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                        IsRecommended = true,
                        GameplayRandomizer = true,
                        IsRuntimeRandomizer = true,
                        SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                        {
                            new RandomizationOption()
                            {
                                SubOptionKey = EnemyWeaponChanger.SUBOPTIONKEY_ENEMYWEAPONS_FORCERANDOMIZER,
                                HumanName = MERRuntimeOption.RTO_TITLE_FORCERANDOMIZATION,
                                Description = "Forces randomization on custom made randomizer enemies",
                                Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                                IsOptionOnly = true,
                                IsRuntimeRandomizer = true
                            },
                            new RandomizationOption()
                            {
                                SubOptionKey = EnemyWeaponChanger.SUBOPTIONKEY_ENEMYWEAPONS_ONETIMERANDOMIZE,
                                HumanName = MERRuntimeOption.RTO_TITLE_RANDOMIZEONCEPERLOADOUT,
                                Description =
                                    "Randomizes the weapon loadout of a pawn only once; loadouts are often shared between multiple pawns of the same type. This will randomize it only once (rather than for each pawn), so all enemies near each other (typically a level) will have the same weapons until the loadout object is dropped from memory",
                                Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                                IsOptionOnly = true,
                                IsRuntimeRandomizer = true
                            },
                        }
                    }

                }
            });
            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Enemy powers",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption()
                    {
                        HumanName = MERRuntimeOption.RTO_TITLE_ENEMYPOWERS,
                        Description = "Gives enemies different powers. May make some enemies very difficult to kill. Enemies that can softlock the game and custom made enemies for this randomzier will not be randomized",
                        IsRuntimeRandomizer = true,
                        PerformRandomizationOnExportDelegate = EnemyPowerChanger.RandomizeExport2,
                        PerformSpecificRandomizationDelegate = EnemyPowerChanger.InitLE2R,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                        IsRecommended = true,
                        GameplayRandomizer = true,
                        SubOptions = new ObservableCollectionExtended<RandomizationOption>(){
                                    new RandomizationOption()
                                    {
                                        SubOptionKey = EnemyPowerChanger.SUBOPTIONKEY_ENEMYPOWERS_FORCERANDOMIZER,
                                        HumanName = MERRuntimeOption.RTO_TITLE_FORCERANDOMIZATION,
                                        Description = "Forces randomization on custom made randomizer enemies. A select few will not randomize as it will break their AI. Enemies that will softlock the game will not be randomized",
                                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                                        IsOptionOnly = true,
                                        IsRuntimeRandomizer = true
                                    },
                                    new RandomizationOption()
                                    {
                                        SubOptionKey = EnemyPowerChanger.SUBOPTIONKEY_ENEMYPOWERS_ONETIMERANDOMIZE,
                                        HumanName = MERRuntimeOption.RTO_TITLE_RANDOMIZEONCEPERLOADOUT,
                                        Description = "Randomizes the powers of a pawn type only once; loadouts are often shared between multiple pawns of the same type. This will randomize it only once (rather than for each pawn), so all same enemies near each other (typically a level) will have the same powers until the loadout object is dropped from memory",
                                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                                        IsOptionOnly = true,
                                        IsRuntimeRandomizer = true
                                    },
                                    new RandomizationOption()
                                    {
                                        SubOptionKey = EnemyPowerChanger.SUBOPTIONKEY_ENEMYPOWERS_ENFORCEMINIMUM,
                                        IsRecommended = true,
                                        HumanName = MERRuntimeOption.RTO_TITLE_GIVEPOWERSTOALLENEMIES,
                                        Description = "Ensures all enemies have at least 2 powers. Not all AI is equipped to use powers (e.g. husks). Significantly increases game difficulty",
                                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                                        IsOptionOnly = true,
                                        IsRuntimeRandomizer = true
                                    }
                                }
                    },
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Level-specific",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption() {HumanName = "Normandy",
                        Description = "Changes various things around the ship, including one sidequest",
                        PerformSpecificRandomizationDelegate = Normandy.PerformRandomization,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        IsRecommended = true,
                        GoodTimeRandomizer = true,
                    },
                    new RandomizationOption() {HumanName = "Prologue",
                        Description = "In Mass Effect Randomizer, lore is very important",
                        PerformSpecificRandomizationDelegate = LazarusStation.PerformRandomization,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        GoodTimeRandomizer = true,
                        IsRecommended = true,
                    },
                    //new RandomizationOption() {HumanName = "Tali Acquisition"}, //sfxgame tla damagetype
                    new RandomizationOption()
                    {
                        HumanName = "Citadel",
                        Description = "Changes many things across the level",
                        PerformSpecificRandomizationDelegate = Citadel.PerformRandomization,
                        RequiresTLK = true,
                        RequiresGestures = true,
                        IsRecommended = true,
                        GoodTimeRandomizer = true,
                    },
                    new RandomizationOption()
                    {
                        HumanName = "Archangel Acquisition", Description = "It's a mystery!",
                        PerformSpecificRandomizationDelegate = ArchangelAcquisition.PerformRandomization,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        IsRecommended = true,
                        RequiresTLK = true,
                        GoodTimeRandomizer = true,
                    },
                    new RandomizationOption()
                    {
                        HumanName = "Illium Hub", Description = "Changes the lounge",
                        PerformSpecificRandomizationDelegate = IlliumHub.PerformRandomization,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        IsRecommended = true,
                        GoodTimeRandomizer = true,
                    },
                    new RandomizationOption()
                    {
                        HumanName = "Omega Hub",
                        Description = "Improved dancing technique",
                        PerformSpecificRandomizationDelegate = OmegaHub.PerformRandomization,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        RequiresGestures = true,
                        IsRecommended = true,
                        GoodTimeRandomizer = true,
                    },
                    new RandomizationOption()
                    {
                        HumanName = MERRuntimeOption.RTO_TITLE_SUICIDEMISSION,
                        Description = "Sharply increases difficulty throughout the entire level, and totally overhauls the final boss fight. Try it on Insanity ;)",
                        PerformSpecificRandomizationDelegate = CollectorBase.PerformRandomization,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                        RequiresTLK = true,
                        IsRecommended = true,
                        GameplayRandomizer = true,
                        SubOptions = new()
                        {

                            new RandomizationOption()
                            {
                                HumanName = "Don't randomize team leaders",
                                Description = "Prevents autopicking who leads the various teams in the mission",
                                IsOptionOnly = true,
                                SubOptionKey = CollectorBase.SUBOPTIONKEY_DONT_RANDOMIZE_TEAMS,
                                Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe
                            },
                            new RandomizationOption()
                            {
                                HumanName = MERRuntimeOption.RTO_TITLE_USENEWFINALBOSSMUSIC,
                                Description = "Replaces the final battle music to better match the combat tempo",
                                IsRecommended = true,
                                IsOptionOnly = true,
                                IsRuntimeRandomizer = true,
                                SubOptionKey = CollectorBase.SUBOPTIONKEY_NEWFINALBOSSMUSIC,
                                Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe
                            }
                        }
                    },

                    // DLC
                    new RandomizationOption() {HumanName = "Overlord",
                        Description = "Changes a few things across the DLC's missions",
                        PerformSpecificRandomizationDelegate = OverlordDLC.PerformRandomization,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal,
                        IsRecommended = true,
                        GoodTimeRandomizer = true, // Technically also gameplay
                    },
                    new RandomizationOption() {HumanName = "Genesis Comic",
                        Description = "Completely changes the story recap segment",
                        PerformSpecificRandomizationDelegate = GenesisDLC.PerformRandomization,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        RequiresTLK = true,
                        IsRecommended = true,
                        GoodTimeRandomizer = true,
                    },
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Squad powers",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption()
                    {
                        HumanName = "Class powers",
                        Description = "Shuffles the powers of all player classes. Points spent in powers removed from your save will be refunded",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        IsRecommended = true,
                        PerformSpecificRandomizationDelegate = ClassTalents.ShuffleClassAbilitites,
                        RequiresTLK = true,
                        GameplayRandomizer = true,
                    },


                    new RandomizationOption()
                    {
                        HumanName = "Henchmen powers",
                        Description = "Shuffles the powers of squadmates. Points spent in powers removed from henchmen in your save will be refunded",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal,
                        IsRecommended = true,
                        PerformSpecificRandomizationDelegate = HenchTalents.ShuffleSquadmateAbilitiesLE2,
                        // etupRandomizerDelegate = HenchTalents.ResetTalents,
                        // SetupRandomizerButtonToolTip = "Allows you to select a save file to remove henchman records from.\nThis will wipe all henchman powers and refund the correct amount of talent points to spend.\nThis will ALSO reset the weapon they are using the default weapon they use.",
                        // SetupRandomizerButtonText = "Refund points",
                        RequiresTLK = true,
                        GameplayRandomizer = true,
                        SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                        {
                            new RandomizationOption()
                            {
                                IsOptionOnly = true,
                                SubOptionKey = HenchTalents.SUBOPTION_HENCHPOWERS_REMOVEGATING,
                                HumanName = "Remove rank-up gating",
                                Description = "Removes the unlock requirement for the second power slot. The final power slot will still be gated by loyalty.",
                            }
                        }
                    },
                }
            });


            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Gameplay",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption()
                    {
                        HumanName = "Enable basic friendly fire",
                        PerformSpecificRandomizationDelegate = SFXGame.TurnOnFriendlyFire,
                        Description = "Enables weapons to damage friendlies (enemy and player)",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal,
                        GameplayRandomizer = true,
                        IsRecommended = true,
                        SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                        {
                            new RandomizationOption()
                            {
                                IsOptionOnly = true,
                                SubOptionKey = SFXGame.SUBOPTIONKEY_CARELESSFF,
                                HumanName = "Careless mode",
                                Description = "Attack enemies, regardless of friendly casualties",
                                IsRecommended = true,
                                SelectOnPreset = true,
                            }
                        }
                    },
                    new RandomizationOption()
                    {
                        HumanName = "Shepard ragdollable",
                        Description = "Makes Shepard able to be ragdolled from various powers/attacks. Can greatly increase difficulty",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                        PerformSpecificRandomizationDelegate = SFXGame.MakeShepardRagdollable,
                        GameplayRandomizer = true,
                    }
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Level-components",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    // Doesn't seem to work
                    //                    new RandomizationOption() {HumanName = "Star colors", IsRecommended = true, PerformRandomizationOnExportDelegate = RBioSun.PerformRandomization},
                    new RandomizationOption() {
                        HumanName = MERRuntimeOption.RTO_TITLE_FOGCOLORS,
                        Description = "Changes colors of fog",
                        IsRecommended = true,
                        PerformSpecificRandomizationDelegate = RSharedHeightFogComponent.InstallDynamicHeightFogRandomizer,
                        IsRuntimeRandomizer = true,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        GoodTimeRandomizer = true
                    },
                    // new RandomizationOption() {HumanName = "Particle Systems", Description = "Randomizes data used in particles systems", IsRecommended = false, PerformRandomizationOnExportDelegate = ArrivalDLC.RandomizeParticleSystems, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning},
                    new RandomizationOption() {
                        HumanName = MERRuntimeOption.RTO_TITLE_LIGHTCOLORS,
                        Description = "Changes colors of dynamic lighting. Prebaked lighting won't be affected",
                        PerformSpecificRandomizationDelegate = RSharedLighting.InstallDynamicLightingRandomizer,
                        IsRecommended = true,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        GoodTimeRandomizer = true,
                        IsRuntimeRandomizer = true
                    },
                    //new RandomizationOption() {HumanName = "Mission rewards", Description = "Randomizes the tech and weapons given to you at the end of a mission. You can still get all the tech and weapons if you complete all the missions that award them.",
                    //    //PerformSpecificRandomizationDelegate = MissionRewards.Inventory,
                    //    PerformSpecificRandomizationDelegate = MissionRewards.PerformRandomization,
                    //    IsRecommended = true,
                    //    ProgressIndeterminate = true,
                    //    Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe},
                    new RandomizationOption() {HumanName = "Treasure pickups", Description = "Randomizes the tech and weapons you can pick up during missions. The results screen, and actions directly happening when you pick up an item won't be accurate (e.g. picking the grenade launcher will unlock a piece of research, but will have the grenade launcher for the rest of the mission)",
                        //PerformSpecificRandomizationDelegate = MissionRewards.Inventory,
                        PerformSpecificRandomizationDelegate = MissionRewards.PerformRandomization,
                        IsRecommended = true,
                        ProgressIndeterminate = true,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        GameplayRandomizer = true,
                    },
                    new RandomizationOption() {
                        HumanName = "Galaxy Map",
                        Description = "Moves things around the map, speeds up normandy",
                        PerformSpecificRandomizationDelegate = GalaxyMap.RandomizeGalaxyMap,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                        GoodTimeRandomizer = true,
                        SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                        {
                            new RandomizationOption()
                            {
                                SubOptionKey = GalaxyMap.SUBOPTIONKEY_INFINITEGAS,
                                HumanName = "Infinite fuel",
                                Description = "Prevents the Normandy from running out of fuel. Prevents possible softlock due to randomization",
                                Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                                IsOptionOnly = true,
                            }
                        }
                    },
                }
            });

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Text",
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption() {HumanName = "Game over text",
                        PerformSpecificRandomizationDelegate = SFXGame.RandomizeGameOverString,
                        RequiresTLK = true,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        IsRecommended = true,
                        GoodTimeRandomizer = true,
                    },
                    new RandomizationOption() {HumanName = "Intro Crawl",
                        PerformSpecificRandomizationDelegate = RSharedTexts.RandomizeOpeningCrawl,
                        RequiresTLK = true,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                        IsRecommended = true,
                        GoodTimeRandomizer = true,
                    },
                    new RandomizationOption()
                    {
                        HumanName = "Vowels",
                        IsPostRun = true,
                        Description="Changes vowels in text in a consistent manner, making a 'new' language",
                        PerformSpecificRandomizationDelegate = RSharedTexts.RandomizeVowels,
                        RequiresTLK = true,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal,
                        MutualExclusiveSet="AllText",
                        StateChangingDelegate=optionChangingDelegate,
                        GoodTimeRandomizer = new Random().Next(2) == 0, // Half the time this will be labeled as 'good time' so the auto picker will choose it instead
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
                    new RandomizationOption() {
                        HumanName = "UwU",
                        Description="UwUifies all text in the game, often hilarious", PerformSpecificRandomizationDelegate = RSharedTexts.UwuifyText,
                        RequiresTLK = true, Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe, MutualExclusiveSet="AllText",
                        StateChangingDelegate=optionChangingDelegate,
                        IsPostRun = true,
                        IsRecommended = true,
                        GoodTimeRandomizer = true,
                        SubOptions = new ObservableCollectionExtended<RandomizationOption>()
                        {
                            new RandomizationOption()
                            {
                                IsOptionOnly = true,
                                HumanName = "Keep casing",
                                Description = "Keeps upper and lower casing",
                                SubOptionKey = RSharedTexts.SUBOPTIONKEY_UWU_KEEPCASING,
                                Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                            },
                            new RandomizationOption()
                            {
                                IsOptionOnly = true,
                                HumanName = "Emoticons",
                                Description = "Adds emoticons ^_^\n'Keep casing' recommended. Might break email or mission summaries, sowwy UwU",
                                Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Safe,
                                SubOptionKey = RSharedTexts.SUBOPTIONKEY_REACTIONS_ENABLED,
                                SelectOnPreset = true,
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
                        PerformRandomizationOnExportDelegate = Cutscene.ShuffleCutscenePawns3,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Warning,
                        IsRecommended = true,
                        GoodTimeRandomizer = true
                        // This technically could be disabled post-install with modification of the pre-shuffler class...
                    },
                    new RandomizationOption() {
                            HumanName = "Animation data",
                            PerformRandomizationOnExportDelegate = RSharedAnimSequence.RandomizeExport,
                            SliderToTextConverter = RSharedAnimSequence.UIConverter,
                            HasSliderOption = true,
                            SliderValue = 1,
                            Ticks = "1,2",
                            Description="Fuzzes rigged bone positions and rotations",
                            SliderTooltip = "Value determines which bones are used in the remapping. Default value is basic bones only.",
                            Dangerousness = RandomizationOption.EOptionDangerousness.Danger_Normal,
                            GoodTimeRandomizer = true
                    },



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

            RandomizationGroups.Add(new RandomizationGroup()
            {
                GroupName = "Bad ideas",
                CollapseGroup = true,
                GroupDescription = "These are ideas that did not work out: you should not enable them unless you just want to experiment. They are likely to ruin the gameplay experience.",
                SortPriority = 5,
                Options = new ObservableCollectionExtended<RandomizationOption>()
                {
                    new RandomizationOption()
                    {
                        HumanName = "Pawn sizes", Description = "Changes the size of characters. Will break a lot of things", 
                        PerformRandomizationOnExportDelegate = RBioPawn.RandomizePawnSize,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_RIP,
                        Ticks = "0.1,0.2,0.3,0.4,0.5,0.75",
                        HasSliderOption = true,
                        SliderTooltip = "Values are added +/- to 1 to generate the range of allowed sizes. For example, 0.1 yields 90-110% size multiplier. Default value is 0.1.",
                        SliderToTextConverter = x=> $"Maximum size change: {Math.Round(x * 100)}%",
                        SliderValue = 0.1,
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
                    new RandomizationOption()
                    {
                        HumanName = "Lots of sounds", PerformSpecificRandomizationDelegate = SFXGame.RandomizeWwiseEvents,
                        Description = "Shuffles sound references in the main game logic file. You'll probably get stuck with the squad select theme that mutes all other audio",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_RIP
                    },
                    new RandomizationOption()
                    {
                        HumanName = "Conversation Wheel", PerformRandomizationOnExportDelegate = RBioConversation.RandomizeExportReplies,
                        Description = "Changes replies in wheel. Can make conversations very hard to exit",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_RIP
                    },
                    new RandomizationOption()
                    {
                        HumanName = "Actors in conversations",
                        PerformFileSpecificRandomization = RBioConversation.RandomizeActorsInConversation2,
                        Description = "Changes pawn roles in conversations. Somewhat buggy simply due to complexity and restrictions in engine, but can be entertaining",
                        IsRecommended = false,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_RIP
                    },
                    new RandomizationOption()
                    {
                        HumanName = "BioStage placement",
                        PerformRandomizationOnExportDelegate = RBioStage.RandomizeBioStage,
                        Description = "Swaps nodes where characters stand in a biostage. You WILL break the game if you turn this on.",
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_RIP
                    },
                    new RandomizationOption() {
                        HumanName = "Mesh rotations",
                        Description="Lightly rotates meshes on axis' - game will assuredly not work properly",
                        PerformRandomizationOnExportDelegate = ArrivalDLC.RotateMeshes,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_RIP,
                    },
                    new RandomizationOption() {
                        HumanName = "Post Processing volumes",
                        Description = "Changes postprocessing. Likely will make some areas of game unplayable",
                        PerformRandomizationOnExportDelegate = RPostProcessingVolume.RandomizeExport,
                        Dangerousness = RandomizationOption.EOptionDangerousness.Danger_RIP
                    },
                }
            });

            foreach (var g in RandomizationGroups)
            {
                g.Options.Sort(x => x.HumanName);
            }

            RandomizationGroups.ReplaceAll(RandomizationGroups.OrderBy(x => x.SortPriority).ThenBy(x => x.GroupName).ToList()); // To list is required here
        }
    }
}
#endif