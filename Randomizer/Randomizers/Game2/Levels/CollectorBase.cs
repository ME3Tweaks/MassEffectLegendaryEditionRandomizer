using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.Targets;
using Microsoft.WindowsAPICodePack.PortableDevices.EventSystem;
using Randomizer.MER;
using Randomizer.Randomizers.Game2.Enemy;
using Randomizer.Randomizers.Game2.Misc;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Utility;
using Randomizer.Shared;
using Windows.UI.Popups;

namespace Randomizer.Randomizers.Game2.Levels
{
    class CollectorBase
    {
        public const string SUBOPTIONKEY_DONT_RANDOMIZE_TEAMS = "SUBOPTIONKEY_DONT_RANDOMIZE_TEAMS";
        public const string SUBOPTIONKEY_NEWFINALBOSSMUSIC = "SUBOPTIONKEY_NEWFINALBOSSMUSIC";

        public static bool PerformRandomization(GameTarget target, RandomizationOption option)
        {
            using var sequenceSupportPackage = MEPackageHandler.OpenMEPackageFromStream(
                MEREmbedded.GetEmbeddedPackage(target.Game, "SeqPrefabs.SuicideMission.pcc"), "SuicideMission.pcc");

            // Hives are the segment before The Long Walk (pipes)

            Action<GameTarget, IMEPackage>[] parallelizableEdits =
            {
                UpdateHives1, // First combat
                UpdateHives2, // First possessed enemy
                UpdateHives3, // Olympic High Jump Sprint
                UpdateHives4, // Run to the final button

                UpdateLongWalk2, // After first checkpoint
                UpdateLongWalk3, // Down the hill to the ditch
                UpdateLongWalk5, // Final run for it 

                UpdatePreFinalBattle,
                UpdateFinalBattle,
            };

            Parallel.ForEach(parallelizableEdits, action => action(target, sequenceSupportPackage));

            // The Long Walk
            if (!option.HasSubOptionSelected(SUBOPTIONKEY_DONT_RANDOMIZE_TEAMS))
            {
                RandomlyChooseTeams(target, option);
            }

            AutomateTheLongWalk(target, sequenceSupportPackage, option);

            InstallCustomFinalBattleMusic(target, sequenceSupportPackage, option);

            // Post-CollectorBase
            UpdatePostCollectorBase(target);

            UpdateLevelStreaming(target);

            SharedLE2Fixes.InstallPowerUsageFixes();

            MERFileSystem.InstallAlways("SuicideMission");
            CoalescedHandler.EnableFeatureFlag("bSuicideMissionRandomizationInstalled"); // Mark this as installed - needed for proper collector AI
            return true;
        }


        private static void UpdateLevelStreaming(GameTarget target)
        {
            // Fix streaming states for all of long walk
            var biodEndGm2F = MERFileSystem.GetPackageFile(target, "BioD_EndGm2.pcc");
            if (biodEndGm2F != null)
            {
                var package = MERFileSystem.OpenMEPackage(biodEndGm2F);

                // Fix Long Walk
                {
                    var ts = package.FindExport("TheWorld.PersistentLevel.BioTriggerStream_0");
                    var ss = ts.GetProperty<ArrayProperty<StructProperty>>("StreamingStates");

                    // Find all phase states and ensure all previous walk phases are also loaded and visible.
                    // This prevents previous enemies from despawning 
                    foreach (var state in ss)
                    {
                        var stateName = state.Properties.GetProp<NameProperty>("StateName")?.Value.Name;
                        if (stateName == null || stateName == "None")
                            continue; // Don't care

                        int phaseNum = 0;
                        if (stateName == "SS_WALKCONCLUSION")
                        {
                            // Ensure previous states are set too
                            phaseNum = 5;
                        }
                        else
                        {
                            var phasePos = stateName.IndexOf("PHASE", StringComparison.InvariantCultureIgnoreCase);
                            if (phasePos == -1)
                            {
                                continue; // We don't care about these
                            }

                            phaseNum = int.Parse(stateName.Substring(phasePos + 5));
                        }

                        // Ensure previous states are kept
                        var visibleChunks = state.GetProp<ArrayProperty<NameProperty>>("VisibleChunkNames");
                        var baseName = "BioD_EndGm2_300Walk0";
                        while (phaseNum > 1)
                        {
                            if (visibleChunks.All(x => x.Value != baseName + phaseNum))
                            {
                                // This has pawns as part of the level so we must make sure it doesn't disappear or player will just see enemies disappear
                                visibleChunks.Add(new NameProperty(baseName + phaseNum));
                            }

                            phaseNum--;
                        }
                    }

                    ts.WriteProperty(ss);
                }

                // Fix 430Combatzone being immediately streamed out when reaper dies (so fog can fade out)
                var reaperFightStates = package.FindExport("TheWorld.PersistentLevel.BioTriggerStream_5");
                BioTriggerStreamHelper.EnsureVisible(reaperFightStates, "SS_REAPER_DEFEATED", "BioD_EndGm2_430ReaperCombat");
                MERFileSystem.SavePackage(package);
            }
        }

        private static void UpdateSpawnsLongWalk(GameTarget target, IMEPackage sequenceSupportPackage)
        {
            UpdateLongWalk2(target, sequenceSupportPackage); // Ahead of the second stopping point
            UpdateLongWalk3(target, sequenceSupportPackage); // At the third stopping point
            UpdateLongWalk5(target, sequenceSupportPackage); // The final run
        }

        /// <summary>
        /// First enemy encounter in TLW
        /// </summary>
        /// <param name="target"></param>
        /// <param name="sequenceSupportPackage"></param>
        private static void UpdateLongWalk2(GameTarget target, IMEPackage sequenceSupportPackage)
        {
            string file = "BioD_EndGm2_300Walk02.pcc";
            using var package = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, file));

            // Logic here
            string[] allowedPawns = new[] { "MERChar_Enemies.ChargingHusk" }; // Swarm the player

            AddRandomSpawns(target, package,
                "TheWorld.PersistentLevel.Main_Sequence.CombatEncounter_FlyerRaid.SeqAct_Delay_1",
                allowedPawns, 6,
                "TheWorld.PersistentLevel.Main_Sequence.CombatEncounter_FlyerRaid.SeqVar_Object_36",
                1500f, sequenceSupportPackage);

            // Remove playpen manipulations

            MERFileSystem.SavePackage(package);
        }

        /// <summary>
        /// First enemy encounter in TLW
        /// </summary>
        /// <param name="target"></param>
        /// <param name="sequenceSupportPackage"></param>
        private static void UpdateLongWalk3(GameTarget target, IMEPackage sequenceSupportPackage)
        {
            string file = "BioD_EndGm2_300Walk03.pcc";
            using var package = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, file));

            // Logic here
            string[] allowedPawns = new[] { "MERChar_EndGm2.SuicideBomination" }; // Special sequence for spawning will kill these as they fly over and these pawns have timed detonators

            AddRandomSpawns(target, package,
                "TheWorld.PersistentLevel.Main_Sequence.Henchmen_Patrol_Track_03.BioSeqAct_PMExecuteTransition_4",
                allowedPawns, 3,
                "TheWorld.PersistentLevel.Main_Sequence.Henchmen_Patrol_Track_03.SeqVar_Object_4",
                400f, sequenceSupportPackage, spawnSeqName: "EndGm2RandomEnemySpawnSuicide");

            // Remove playpen manipulations

            MERFileSystem.SavePackage(package);
        }

        private static void UpdateLongWalk5(GameTarget target, IMEPackage sequenceSupportPackage)
        {
            // Run for your lives
            string file = "BioD_EndGm2_300Walk05.pcc";
            using var package = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, file));

            // Logic here
            string[] allowedPawns = new[] { "MERChar_Enemies.ChargingHusk", "MERChar_EndGm2.Bomination" }; // Swarm the player

            AddRandomSpawns(target, package,
                "TheWorld.PersistentLevel.Main_Sequence.Henchmen_Patrol_Track_05.BioSeqAct_PMExecuteTransition_2",
                allowedPawns, 12,
                MERSeqTools.CreateNewSquadObject(SeqTools.GetParentSequence(package.FindExport("TheWorld.PersistentLevel.Main_Sequence.Henchmen_Patrol_Track_05.BioSeqAct_PMExecuteTransition_2"))).InstancedFullPath,
                900f, sequenceSupportPackage);

            // Remove playpen manipulations

            MERFileSystem.SavePackage(package);
        }

        #region Hives

        private static void UpdateHives1(GameTarget target, IMEPackage sequenceSupportPackage)
        {
            string file = "BioD_EndGm2_120TheHives.pcc";
            using var package = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, file));

            // Logic here
            string[] allowedPawns = new[] { "MERChar_Enemies.ChargingHusk" }; // We only use husks in the early area
            //string[] allowedPawns = new[] { "MERChar_EndGm2.Bomination" }; // We only use husks in the early area

            AddRandomSpawns(target, package,
                "TheWorld.PersistentLevel.Main_Sequence.Hives120_Combat_Respawn.Seq_FlyIn_LowerSection.SeqEvent_SequenceActivated_0",
                allowedPawns, 10,
                "TheWorld.PersistentLevel.Main_Sequence.Hives120_Combat_Respawn.Seq_FlyIn_LowerSection.SeqVar_Object_40",
                1500f, sequenceSupportPackage);

            // Remove playpen manipulations
            KismetHelper.RemoveAllLinks(
                package.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.Hives120_Playpen_Manipulators.SeqEvent_Touch_0"));
            KismetHelper.RemoveAllLinks(
                package.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.Hives120_Playpen_Manipulators.SeqEvent_Touch_1"));
            KismetHelper.RemoveAllLinks(
                package.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.Hives120_Playpen_Manipulators.SeqEvent_Touch_3"));

            MERFileSystem.SavePackage(package);
        }

        private static void UpdateHives2(GameTarget target, IMEPackage sequenceSupportPackage)
        {
            string file = "BioD_EndGm2_140CoverCorridor.pcc";
            using var package = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, file));

            // Logic here
            // Originally was Varren
            // Varren turn into land sharks for some reason
            string[] allowedPawns = new[] { "MERChar_Enemies.CollectorFlamerSpawnable" };

            AddRandomSpawns(target, package,
                "TheWorld.PersistentLevel.Main_Sequence.Corridor150_Combat_Respawn.SeqAct_Gate_0", allowedPawns, 2,
                "TheWorld.PersistentLevel.Main_Sequence.Corridor150_Combat_Respawn.SeqVar_Object_36", 1300f,
                sequenceSupportPackage);

            // Remove playpen manipulations
            KismetHelper.RemoveAllLinks(package.FindExport(
                "TheWorld.PersistentLevel.Main_Sequence.Corridor150_Playpen_Manipulators.SeqEvent_Touch_0"));
            KismetHelper.RemoveAllLinks(package.FindExport(
                "TheWorld.PersistentLevel.Main_Sequence.Corridor150_Playpen_Manipulators.SeqEvent_Touch_1"));
            KismetHelper.RemoveAllLinks(package.FindExport(
                "TheWorld.PersistentLevel.Main_Sequence.Corridor150_Playpen_Manipulators.SeqEvent_Touch_4"));
            KismetHelper.RemoveAllLinks(package.FindExport(
                "TheWorld.PersistentLevel.Main_Sequence.Corridor150_Playpen_Manipulators.SeqEvent_Touch_9"));
            KismetHelper.RemoveAllLinks(package.FindExport(
                "TheWorld.PersistentLevel.Main_Sequence.Corridor150_Playpen_Manipulators.SeqEvent_Touch_3"));

            MERFileSystem.SavePackage(package);
        }

        private static void UpdateHives3(GameTarget target, IMEPackage sequenceSupportPackage)
        {
            string file = "BioD_EndGm2_160HoneyCombs.pcc";
            using var package = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, file));

            // Logic here
            string[]
                allowedPawns = new[]
                {
                    "BioChar_Animals.Combat.ELT_Spider"
                }; // Guys that blow up are spawned close to the player to make them panic

            AddRandomSpawns(target, package,
                "TheWorld.PersistentLevel.Main_Sequence.HoneyCombs160_Combat_Respawn_B.SeqAct_Gate_1", allowedPawns, 4,
                "TheWorld.PersistentLevel.Main_Sequence.HoneyCombs160_Combat_Respawn_B.SeqVar_Object_4", 700f,
                sequenceSupportPackage);

            // Remove playpen manipulations
            KismetHelper.RemoveAllLinks(package.FindExport(
                "TheWorld.PersistentLevel.Main_Sequence.HoneyCombs160_Playpen_Manipulators.SeqEvent_Touch_0"));
            KismetHelper.RemoveAllLinks(package.FindExport(
                "TheWorld.PersistentLevel.Main_Sequence.HoneyCombs160_Playpen_Manipulators.SeqEvent_Touch_1"));

            MERFileSystem.SavePackage(package);
        }

        private static void UpdateHives4(GameTarget target, IMEPackage sequenceSupportPackage)
        {
            string file = "BioD_EndGm2_180FactoryEntryB.pcc";
            using var package = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, file));

            // Logic here
            string[]
                allowedPawns = new[]
                {
                    "BioChar_Collectors.ELT_Scion"
                }; // A few scions for the final area as it will force player to run for it

            AddRandomSpawns(target, package,
                "TheWorld.PersistentLevel.Main_Sequence.FactoryEntry180_Combat_RespawnB.SeqAct_Gate_0", allowedPawns, 2,
                "TheWorld.PersistentLevel.Main_Sequence.FactoryEntry180_Combat_RespawnB.SeqVar_Object_36", 3000f,
                sequenceSupportPackage, minSpawnDistance: 500f);

            // Remove playpen manipulations
            KismetHelper.RemoveAllLinks(package.FindExport(
                "TheWorld.PersistentLevel.Main_Sequence.FactoryEntry180_Playpen_Manipulators.SeqEvent_Touch_6"));
            KismetHelper.RemoveAllLinks(package.FindExport(
                "TheWorld.PersistentLevel.Main_Sequence.FactoryEntry180_Playpen_Manipulators.SeqEvent_Touch_7"));
            KismetHelper.RemoveAllLinks(package.FindExport(
                "TheWorld.PersistentLevel.Main_Sequence.FactoryEntry180_Playpen_Manipulators.SeqEvent_Touch_8"));

            MERFileSystem.SavePackage(package);
        }

        #endregion

        #region The Long Walk

        private static void RandomlyChooseTeams(GameTarget target, RandomizationOption option)
        {
            var files = new string[] { "BioD_EndGm1_310Huddle.pcc", "BioD_EndGm2_200Factory.pcc" };

            foreach (var file in files)
            {
                var teamSelectFile = MERFileSystem.GetPackageFile(target, file);
                var package = MERFileSystem.OpenMEPackage(teamSelectFile);

                var teams = package.Exports.Where(x => x.ClassName == "Sequence" && x.ObjectName.Instanced.StartsWith("Select_Team_")).ToList();

                foreach (var teamSeq in teams)
                {
                    var seqObjects = SeqTools.GetAllSequenceElements(teamSeq).OfType<ExportEntry>().ToList();
                    var chosenIndex = package.FindExport(teamSeq.InstancedFullPath + ".SeqVar_Int_0");

                    // Count the number of options added

                    // 1. Create the counter
                    var inputInt = MERSeqTools.CreateInt(teamSeq, 0);

                    // Output to count


                    // Create our rand list container and replace ShowGUI logic with it
                    var hackDelay =
                        MERSeqTools.CreateDelay(teamSeq,
                            0.01f); // This is a hack so kismet logic runs in order. Otherwise this would require 
                    // significantly rearchitecting the sequence

                    // Add rand selector
                    var randIndexContainer = SequenceObjectCreator.CreateSequenceObject(package,
                        "MERSeqAct_RandIntList", MERCaches.GlobalCommonLookupCache);
                    KismetHelper.AddObjectToSequence(randIndexContainer, teamSeq);
                    KismetHelper.CreateVariableLink(randIndexContainer, "Input", inputInt);
                    KismetHelper.CreateVariableLink(randIndexContainer, "Result", chosenIndex);

                    // Add logic to add each valid choice index to our random int list
                    foreach (var seqObj in seqObjects.Where(x => x.ClassName == "BioSeqAct_AddChoiceGUIElement"))
                    {
                        var choiceIdLink = SeqTools.GetVariableLinksOfNode(seqObj)[8];
                        ExportEntry choiceIdInt;
                        if (choiceIdLink.LinkedNodes.Count == 0)
                        {
                            // This is choice 0 which is default which is why this is null
                            choiceIdInt = MERSeqTools.CreateInt(teamSeq, 0);
                        }
                        else
                        {
                            choiceIdInt = SeqTools.GetVariableLinksOfNode(seqObj)[8].LinkedNodes[0] as ExportEntry;
                            if (choiceIdInt == null)
                                Debugger.Break();
                        }

                        var setInt = MERSeqTools.CreateSetInt(teamSeq, inputInt, choiceIdInt);
                        KismetHelper.CreateOutputLink(seqObj, "Success", setInt);
                        KismetHelper.CreateOutputLink(setInt, "Out", randIndexContainer);
                    }

                    // Get the node after showchoicegui and closegui - we will output to that to skip the UI
                    var showChoiceGui = seqObjects.FirstOrDefault(x => x.ClassName == "BioSeqAct_ShowChoiceGUI");
                    var nextNode = MERSeqTools.GetNextNode(showChoiceGui, 0);
                    nextNode = MERSeqTools.GetNextNode(nextNode, 0);
                    KismetHelper.CreateOutputLink(randIndexContainer, "SetValue",
                        nextNode); // Point to after CloseChoiceGUI

                    // Repoint from ShowGUI to rand selector
                    var outboundNodes = SeqTools.FindOutboundConnectionsToNode(showChoiceGui, seqObjects);
                    foreach (var outboundNode in outboundNodes)
                    {
                        var outboundLinks = SeqTools.GetOutboundLinksOfNode(outboundNode);
                        foreach (var outLink in outboundLinks)
                        {
                            foreach (var linkedOp in outLink)
                            {
                                if (linkedOp.LinkedOp == showChoiceGui)
                                {
                                    linkedOp.LinkedOp = hackDelay;
                                    linkedOp.InputLinkIdx = 0; // SetValue
                                }
                            }
                        }

                        SeqTools.WriteOutboundLinksToNode(outboundNode, outboundLinks);
                    }

                    KismetHelper.CreateOutputLink(hackDelay, "Finished", randIndexContainer, 2); // SetOutput
                }


                MERFileSystem.SavePackage(package);
            }
        }

        private static void AutomateTheLongWalk(GameTarget target, IMEPackage sequenceSupportPackage, RandomizationOption option)
        {
            var longwalkfile = MERFileSystem.GetPackageFile(target, "BioD_EndGm2_300LongWalk.pcc");
            if (longwalkfile != null)
            {
                // automate TLW
                var package = MEPackageHandler.OpenMEPackage(longwalkfile);
                var seq = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State");

                MERSeqTools.InstallSequenceStandalone(sequenceSupportPackage.FindExport("LongWalkHelper"), package, seq);

                // Signal LongWalkHelper
                var stopWalking = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.SeqEvent_SequenceActivated_2");
                var stoppedWalkingARE = MERSeqTools.CreateActivateRemoteEvent(seq, "WalkingDone");
                KismetHelper.CreateOutputLink(stopWalking, "Out", stoppedWalkingARE);

                // Add LongWalkHelper listener
                var startWalkingRE = MERSeqTools.CreateSeqEventRemoteActivated(seq, "StartWalking");
                KismetHelper.CreateOutputLink(startWalkingRE, "Out", package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.SeqAct_Toggle_1"), 1);

                // Skip the saving/resurrecting code. This is now handled by LongWalkHelper
                SeqTools.SkipSequenceElement(package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.SeqCond_CompareBool_0"), "True");


                // The auto walk delay on Stop Walking
                /*
                var delayNormal = MERSeqTools.CreateRandomDelay(seq, 2, 7); // How long to hold position
                var delayHardcore = MERSeqTools.CreateRandomDelay(seq, 3, 9); // How long to hold position
                var delayInsanity = MERSeqTools.CreateRandomDelay(seq, 6, 13); // How long to hold position
                var diffSwitch = MERSeqTools.CreateCondGetDifficulty(seq);

                KismetHelper.CreateOutputLink(diffSwitch, "Casual", delayNormal);
                KismetHelper.CreateOutputLink(diffSwitch, "Normal", delayNormal);
                KismetHelper.CreateOutputLink(diffSwitch, "Veteran", delayNormal);
                KismetHelper.CreateOutputLink(diffSwitch, "Hardcore", delayHardcore);
                KismetHelper.CreateOutputLink(diffSwitch, "Insanity", delayInsanity);
                KismetHelper.CreateOutputLink(diffSwitch, "Failed", delayNormal);


                KismetHelper.CreateOutputLink(delayNormal, "Finished", package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.BioSeqAct_ResurrectHenchman_0"));
                KismetHelper.CreateOutputLink(delayHardcore, "Finished", package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.BioSeqAct_ResurrectHenchman_0"));
                KismetHelper.CreateOutputLink(delayInsanity, "Finished", package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.BioSeqAct_ResurrectHenchman_0"));
                KismetHelper.CreateOutputLink(stopWalking, "Out", diffSwitch); */

                // Do not allow targeting the escort
                package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.SeqVar_Bool_8").WriteProperty(new IntProperty(0, "bValue")); // stopped walking
                package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.SeqVar_Bool_14").WriteProperty(new IntProperty(0, "bValue")); // loading from save - we will auto start
                KismetHelper.CreateOutputLink(package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.SeqAct_Toggle_2"), "Out", package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.BioSeqAct_ResurrectHenchman_0")); // Auto start walking


                /*
                // Only sometimes allow autosaves if combat was not completed
                var itemAfterSave = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.SeqAct_Gate_2"); // If skipping saving, it goes to this

                var linkCountForSave = 4; // 25% chance
                var rSaveSwitch = MERSeqTools.CreateRandSwitch(seq, linkCountForSave);

                var saveGame = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.SFXSeqAct_SaveGame_0");
                var inCombat = MERSeqTools.CreateAndAddToSequence(seq, "BioSeqCond_InCombat");

                KismetHelper.CreateOutputLink(inCombat, "False", saveGame); // Player killed everything - save the game
                KismetHelper.CreateOutputLink(inCombat, "True", rSaveSwitch); // Only a chance for save
                


                // Do not save after the third part
                var saveChanceStart = MERSeqTools.CreateCompareInt(seq, MERSeqTools.CreatePlotInt(seq, 226), MERSeqTools.CreateInt(seq, 4));
                KismetHelper.CreateOutputLink(saveChanceStart, "A >= B", itemAfterSave); // Can't ave after the third segment
                KismetHelper.CreateOutputLink(saveChanceStart, "A < B", inCombat); // Go to save code

                // Move ReviveHench
                var loadedFromSave = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.SeqCond_CompareBool_0");
                var reviveHench = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.BioSeqAct_ResurrectHenchman_0");
                SeqTools.ChangeOutlink(loadedFromSave, 1, 0, reviveHench.UIndex); // Change to revive hench
                SeqTools.ChangeOutlink(reviveHench, 0, 0, saveChanceStart.UIndex);

                // Rand chance to save if player did not complete combat. This prevents it from being impossible
                for (int i = 0; i < linkCountForSave; i++)
                {
                    if (i == 0)
                    {
                        // Save
                        KismetHelper.CreateOutputLink(rSaveSwitch, $"Link {i + 1}", saveGame);
                    }
                    else
                    {
                        // Don't save
                        KismetHelper.CreateOutputLink(rSaveSwitch, $"Link {i + 1}", package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.SeqAct_Gate_2"));
                    }
                } */

                // Damage henchmen outside of the bubble
                var hench2Vfx = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.Damage_if_too_Far.HenchB_VFX_OnRange");
                var hench1Vfx = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.Damage_if_too_Far.HenchA_VFX_OnRange");
                seq = SeqTools.GetParentSequence(hench2Vfx);
                var damage1 = SequenceObjectCreator.CreateSequenceObject(package, "MERSeqAct_CauseDamageVocal", MERCaches.GlobalCommonLookupCache);
                var damage2 = SequenceObjectCreator.CreateSequenceObject(package, "MERSeqAct_CauseDamageVocal", MERCaches.GlobalCommonLookupCache);
                KismetHelper.AddObjectsToSequence(SeqTools.GetParentSequence(hench1Vfx), true, damage1, damage2);

                KismetHelper.CreateVariableLink(damage1, "Target", package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.Damage_if_too_Far.SeqVar_Object_6"));
                KismetHelper.CreateVariableLink(damage2, "Target", package.FindExport("TheWorld.PersistentLevel.Main_Sequence.LongWalk_Controller.Control_Walking_State.Damage_if_too_Far.SeqVar_Object_4"));

                var henchDamageAmount = 4;
                KismetHelper.CreateVariableLink(damage1, "Amount", MERSeqTools.CreateFloat(seq, henchDamageAmount));
                KismetHelper.CreateVariableLink(damage2, "Amount", MERSeqTools.CreateFloat(seq, henchDamageAmount));

                damage1.WriteProperty(new ObjectProperty(package.FindEntry("SFXGame.SFXDamageType_Environmental"), "DamageType"));
                damage2.WriteProperty(new ObjectProperty(package.FindEntry("SFXGame.SFXDamageType_Environmental"), "DamageType"));

                KismetHelper.CreateOutputLink(hench1Vfx, "Took Damage", damage1);
                KismetHelper.CreateOutputLink(hench2Vfx, "Took Damage", damage2);

                MERFileSystem.SavePackage(package);
            }

            //randomize long walk lengths.
            // IFP map
            var endwalkexportmap = new Dictionary<string, string>()
            {
                {
                    "BioD_EndGm2_300Walk01",
                    "TheWorld.PersistentLevel.Main_Sequence.Henchmen_Patrol_Track_01.SeqAct_Interp_1"
                },
                {
                    "BioD_EndGm2_300Walk02",
                    "TheWorld.PersistentLevel.Main_Sequence.Henchmen_Patrol_Track_02.SeqAct_Interp_1"
                },
                {
                    "BioD_EndGm2_300Walk03",
                    "TheWorld.PersistentLevel.Main_Sequence.Henchmen_Patrol_Track_03.SeqAct_Interp_2"
                },
                {
                    "BioD_EndGm2_300Walk04",
                    "TheWorld.PersistentLevel.Main_Sequence.Henchmen_Patrol_Track_04.SeqAct_Interp_0"
                },
                {
                    "BioD_EndGm2_300Walk05",
                    "TheWorld.PersistentLevel.Main_Sequence.Henchmen_Patrol_Track_05.SeqAct_Interp_4"
                }
            };

            foreach (var map in endwalkexportmap)
            {
                var file = MERFileSystem.GetPackageFile(target, map.Key + ".pcc");
                if (file != null)
                {
                    var package = MEPackageHandler.OpenMEPackage(file);
                    var export = package.FindExport(map.Value);
                    var seq = SeqTools.GetParentSequence(export);
                    var seqElements = SeqTools.GetAllSequenceElements(seq).OfType<ExportEntry>().ToList();
                    var randPR = MERSeqTools.CreateAndAddToSequence(seq, "MERSeqAct_RandomizePlayRateInNextInterp");
                    var randFloat = MERSeqTools.CreateRandFloat(seq, 0.5f, 2.5f);
                    KismetHelper.CreateVariableLink(randPR, "PlayRate", randFloat);
                    MERSeqTools.RedirectInboundLinks(export, randPR);
                    KismetHelper.CreateOutputLink(randPR, "Out", export);

                    MERFileSystem.SavePackage(package);
                }
            }
        }

        #endregion

        #region Platforming and Final Fights


        private static float DESTROYER_PLAYRATE = 2;

        private static void InstallCustomFinalBattleMusic(GameTarget target, IMEPackage sequenceSupportPackage, RandomizationOption option)
        {
            string file = "BioS_EndGm2.pcc";
            using var package = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, file));

            // Logic here
            var musSeq = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.IMPORT_Music_Events");


            // Set logic branch based on if music feature is enabled
            var plotMusicInt = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.IMPORT_Music_Events.BioSeqVar_StoryManagerInt_3");

            var originalSetMusic = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.IMPORT_Music_Events.SeqAct_SetInt_3");
            var newSetMusic = MERSeqTools.CreateSetInt(musSeq, plotMusicInt, MERSeqTools.CreateInt(musSeq, 0));
            var checkFeature = MERSeqTools.CreateMERIsFeatureEnabled(musSeq, "bUseNewFinalBossMusic");

            // Port in events
            var playEvent = PackageTools.PortExportIntoPackage(target, package, sequenceSupportPackage.FindExport("Wwise_Music_Streaming_SM_MER.Play_fallingdown"));
            var stopEvent = PackageTools.PortExportIntoPackage(target, package, sequenceSupportPackage.FindExport("Wwise_Music_Streaming_SM_MER.Stop_fallingdown"));

            // Branch code - boss mus
            KismetHelper.CreateOutputLink(checkFeature, "Enabled", newSetMusic);
            KismetHelper.CreateOutputLink(checkFeature, "Enabled", MERSeqTools.CreateWwisePostEvent(musSeq, playEvent));
            KismetHelper.CreateOutputLink(checkFeature, "Not Enabled", originalSetMusic);


            // Hookup music events
            var trigFinalBossStarted = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.IMPORT_Music_Events.SeqEvent_RemoteEvent_2");
            KismetHelper.RemoveOutputLinks(trigFinalBossStarted);
            KismetHelper.CreateOutputLink(trigFinalBossStarted, "Out", checkFeature);

            // Always post the stop
            var trigFinalBossKilled = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.IMPORT_Music_Events.SeqEvent_RemoteEvent_3");
            KismetHelper.CreateOutputLink(trigFinalBossKilled, "Out", MERSeqTools.CreateWwisePostEvent(musSeq, stopEvent));

            MERFileSystem.SavePackage(package);

            CoalescedHandler.EnableFeatureFlag("bUseNewFinalBossMusic", option.HasSubOptionSelected(SUBOPTIONKEY_NEWFINALBOSSMUSIC));
        }


        /// <summary>
        /// Adds a fog that gradually fades in as you damage the reaper, making it harder to see
        /// </summary>
        /// <param name="finalFightPackage"></param>
        /// <param name="sequenceSupportPackage"></param>
        private static void InstallAtmosphereHandler(IMEPackage finalFightPackage, IMEPackage sequenceSupportPackage)
        {
            var scalar =
                finalFightPackage.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.BioSeqAct_ScalarMathUnit_0");
            var healthPercent =
                finalFightPackage.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqVar_Int_1");
            var atmoHandler = MERSeqTools.InstallSequenceStandalone(
                sequenceSupportPackage.FindExport("AtmosphereHandler"), finalFightPackage,
                SeqTools.GetParentSequence(scalar));

            KismetHelper.CreateOutputLink(scalar, "Out", atmoHandler);
            KismetHelper.CreateVariableLink(atmoHandler, "HealthPercent", healthPercent);

            // Add the fog, ash, and lightning
            foreach (var actor in sequenceSupportPackage.Exports.Where(x => x.idxLink == 0 && x.ClassName is "Emitter" or "RollingHeightFog"))
            {
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, actor,
                    finalFightPackage, finalFightPackage.GetLevel(), true, new RelinkerOptionsPackage(),
                    out var newEntry2);

                finalFightPackage.AddToLevelActorsIfNotThere(newEntry2 as ExportEntry);
            }

            // Add ash at 50%
            finalFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqVar_Float_3").WriteProperty(new FloatProperty(0.5f, "FloatValue"));
            var logTriggerAsh = finalFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqAct_Log_12");
            var seq = SeqTools.GetParentSequence(logTriggerAsh);
            KismetHelper.RemoveOutputLinks(logTriggerAsh);
            var ashRemoteEvent = MERSeqTools.CreateActivateRemoteEvent(seq, "ATMO_ASH");
            KismetHelper.CreateOutputLink(logTriggerAsh, "Out", ashRemoteEvent);

            // Add lightning at 30%
            finalFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqVar_Float_4").WriteProperty(new FloatProperty(0.3f, "FloatValue"));
            var logTriggerLightning = finalFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqAct_Log_8");
            KismetHelper.RemoveOutputLinks(logTriggerLightning);
            var lightningRemoteEvent = MERSeqTools.CreateActivateRemoteEvent(seq, "ATMO_LIGHTNING");
            KismetHelper.CreateOutputLink(logTriggerLightning, "Out", lightningRemoteEvent);

        }


        private static void AddRandomSpawnsToFinalFight(GameTarget target, IMEPackage reaperFightPackage, IMEPackage sequenceSupportPackage)
        {
            // Logic here
            string[] allowedPawns = new[] { "MERChar_Enemies.ChargingHusk", "MERChar_EndGm2.Bomination" }; // We only use husks in the early area

            // Spawn additional enemies at 80% HP
            KismetHelper.RemoveOutputLinks(reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqAct_Log_9")); // Repurpose this (break panel) to our usage
            reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqVar_Float_1").WriteProperty(new FloatProperty(0.8f, "FloatValue")); // Trigger at 80% HP
            var squad1 = MERSeqTools.CreateNewSquadObject(reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler"), "LoopSquad");
            AddRandomSpawns(target, reaperFightPackage,
                "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqAct_Log_9",
                allowedPawns, 8,
                squad1.InstancedFullPath,
                2000f, sequenceSupportPackage);

            //var squad2 = MERSeqTools.CreateNewSquadObject(reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler"), "LoopSquad");
            //AddRandomSpawns(target, reaperFightPackage,
            //    "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqAct_Log_9",
            //    allowedPawns, 2,
            //    squad2.InstancedFullPath,
            //    1500f, sequenceSupportPackage);
        }

        private static void ChangeLowHealthEnemies(GameTarget target, IMEPackage reaperFightPackage, IMEPackage sequenceSupportPackage)
        {
            // Create sequence for changing the spawnable types

            var seq = reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence");
            var kSpawnable = MERSeqTools.CreateObject(seq, reaperFightPackage.FindExport("MERChar_Enemies.KaidanSpawnable"));
            var aSpawnable = MERSeqTools.CreateObject(seq, reaperFightPackage.FindExport("MERChar_Enemies.AshleySpawnable"));

            // The default enemies
            // These two must be ported in as they may not reliably be in memory, even though sniper is technically in 420CombatZone
            PawnPorting.PortPawnIntoPackage(target, PawnPorting.GetPortablePawn("BioChar_Collectors.Soldiers.ELT_COL_Needler"), reaperFightPackage);
            PawnPorting.PortPawnIntoPackage(target, PawnPorting.GetPortablePawn("MERChar_Enemies.CollectorFlamerSpawnable"), reaperFightPackage);

            var defCollector = reaperFightPackage.FindExport("BioChar_Collectors.Soldiers.MIN_COL_AssaultRifle");
            var sniperCollector = reaperFightPackage.FindExport("BioChar_Collectors.Soldiers.ELT_COL_Needler");
            var flamerCollector = reaperFightPackage.FindExport("MERChar_Enemies.CollectorFlamerSpawnable");

            // The default list uses 4:1:1 distribution of enemy types
            var list = MERSeqTools.CreateRandSeqVarList(seq, defCollector, defCollector, defCollector, defCollector, sniperCollector, flamerCollector);
            list.WriteProperty(new NameProperty("CrawlingCombatPawnTypes", "VarName")); // Set as a variable

            var lowHPTrigger = MERSeqTools.CreateSeqEventRemoteActivated(seq, "ReaperLowHP");

            var clearList = MERSeqTools.CreateModifyObjectList(seq);
            var addKList = MERSeqTools.CreateModifyObjectList(seq);
            var addAList = MERSeqTools.CreateModifyObjectList(seq);
            var pmCheckState = MERSeqTools.CreatePMCheckState(seq, 1541); // Kaidan died?

            // Link up the sequence

            KismetHelper.CreateOutputLink(lowHPTrigger, "Out", clearList, 2); // Empty list
            KismetHelper.CreateOutputLink(clearList, "Out", pmCheckState);
            KismetHelper.CreateOutputLink(pmCheckState, "True", addKList); // Ashley lived (use Kaidan instead)
            KismetHelper.CreateOutputLink(pmCheckState, "False", addAList); // Kaidan lived (use Ashley instead)

            KismetHelper.CreateVariableLink(clearList, "ObjectListVar", list);
            KismetHelper.CreateVariableLink(addKList, "ObjectListVar", list);
            KismetHelper.CreateVariableLink(addAList, "ObjectListVar", list);
            KismetHelper.CreateVariableLink(addKList, "ObjectRef", kSpawnable);
            KismetHelper.CreateVariableLink(addAList, "ObjectRef", aSpawnable);


            // Now insert MERSeqAct_AssignAIFactoryData
            var aiFactories = reaperFightPackage.Exports.Where(x => x.ClassName == "SFXSeqAct_AIFactory").ToList();

            foreach (var aiFactory in aiFactories)
            {
                seq = SeqTools.GetParentSequence(aiFactory);
                // SeqRef parent
                if (!seq.HasParent || !seq.Parent.HasParent || seq.Parent.Parent.ObjectName.Name != "CrawlingCombat")
                {
                    continue; // Only modify the crawling combats
                }

                // Create the assignment object
                var aiFactoryAssignment = SequenceObjectCreator.CreateSequenceObject(reaperFightPackage, "MERSeqAct_AssignAIFactoryData", MERCaches.GlobalCommonLookupCache);
                KismetHelper.AddObjectToSequence(aiFactoryAssignment, seq);
                aiFactoryAssignment.WriteProperty(new ObjectProperty(aiFactory.GetProperty<ObjectProperty>("Factory").ResolveToEntry(reaperFightPackage), "Factory"));
                var pawnTypeList = MERSeqTools.CreateSeqVarNamed(seq, "CrawlingCombatPawnTypes", "SeqVar_ObjectList");
                KismetHelper.CreateVariableLink(aiFactoryAssignment, "PawnType(s)", pawnTypeList);

                // Repoint incoming to spawn to this node instead
                MERSeqTools.RedirectInboundLinks(aiFactory, aiFactoryAssignment);

                // Create outlink to continue spawn
                KismetHelper.CreateOutputLink(aiFactoryAssignment, "Out", aiFactory);
            }

            // Add the low HP trigger
            // Same as atmo
            lowHPTrigger = reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqAct_Log_8");
            seq = SeqTools.GetParentSequence(lowHPTrigger);
            var lowHPRemoteEvent = MERSeqTools.CreateActivateRemoteEvent(seq, "ReaperLowHP");
            KismetHelper.CreateOutputLink(lowHPTrigger, "Out", lowHPRemoteEvent);
        }

        private static void PlatformDestroyer(GameTarget target, IMEPackage reaperFightPackage, IMEPackage sequenceSupportPackage)
        {
            // Logic for platform destruction
            var sequences = reaperFightPackage.Exports.Where(x => x.ClassName == "Sequence" && x.ObjectName.Instanced.StartsWith("Destroy_Platform_")).ToList();
            foreach (var sequence in sequences)
            {
                var seqObjs = SeqTools.GetAllSequenceElements(sequence).OfType<ExportEntry>().ToList();
                // Update interp speeds
                foreach (var seqObj in seqObjs.Where(x => x.ClassName == "SeqAct_Interp"))
                {
                    bool setGesture = false;
                    // 1. Set gesture speed
                    var interpData = new InterpTools.InterpData(MERSeqTools.GetInterpData(seqObj));
                    foreach (var group in interpData.InterpGroups.Where(x => x.GroupName == "ProtoReaper"))
                    {
                        foreach (var track in group.Tracks.Where(x =>
                                     x.TrackTitle != null && x.TrackTitle.StartsWith("Gesture")))
                        {
                            setGesture = true;
                            var gestures = track.Export.GetProperty<ArrayProperty<StructProperty>>(@"m_aGestures");
                            foreach (var g in gestures)
                            {
                                g.Properties.AddOrReplaceProp(new FloatProperty(DESTROYER_PLAYRATE, "fPlayRate"));
                            }

                            track.Export.WriteProperty(gestures);
                        }
                    }


                    // 2. If there is gesture speed, also set playrate of whole track (this skips platform destroy animation)
                    //if (setGesture)
                    {
                        seqObj.WriteProperty(new FloatProperty(DESTROYER_PLAYRATE, "PlayRate"));
                    }
                }

                // Change player check to all pawns check
                var checkIfInVolume = seqObjs.FirstOrDefault(x => x.ClassName == "BioSeqAct_CheckIfInVolume");
                var doActionInVolume = MERSeqTools.CreateAndAddToSequence(sequence, "BioSeqAct_DoActionInVolume");
                MERSeqTools.RedirectInboundLinks(checkIfInVolume, doActionInVolume);

                // Hookup vars to new check
                var volume = SeqTools.GetVariableLinksOfNode(checkIfInVolume)[1].LinkedNodes[0] as ExportEntry; // ooo spicy (Volume)
                var currentObj = MERSeqTools.CreateObject(sequence, null);

                KismetHelper.CreateVariableLink(doActionInVolume, "Volume", volume);
                KismetHelper.CreateVariableLink(doActionInVolume, "CurrentObject", currentObj);

                var attachEffect =
                    seqObjs.FirstOrDefault(x =>
                        x.ObjectName.Instanced ==
                        "BioSeqAct_AttachVisualEffect_0"); // This is very specific and depends on compile order of file!
                var ragdollIntoAir = MERSeqTools.InstallSequenceStandalone(sequenceSupportPackage.FindExport("RagdollIntoAir"), reaperFightPackage, sequence);
                KismetHelper.CreateVariableLink(ragdollIntoAir, "Pawn", currentObj);
                KismetHelper.CreateOutputLink(doActionInVolume, "Next", ragdollIntoAir); // Connect input
                KismetHelper.CreateOutputLink(ragdollIntoAir, "Out", doActionInVolume, 1); // Continue
                KismetHelper.CreateVariableLink(doActionInVolume, "Finished", attachEffect);
            }

            // Install sequence that handles choosing the next platform to destroy
            var attackLoopSeq = reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop");
            MERSeqTools.InstallSequenceStandalone(sequenceSupportPackage.FindExport("ReaperPlatformDestroyer"), reaperFightPackage, attackLoopSeq);

            // Port in Wwise Event that will be used to trigger sounds of platforms blowing up
            var audioPackage = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, "BioD_EndGm1_220Cutscenes.pcc"));
            var sourceEvent = audioPackage.FindExport("Wwise_VFX_Grenades.Play_vfx_explosion_grenade_generic");
            var wwiseBangEvent = PackageTools.PortExportIntoPackage(target, reaperFightPackage, sourceEvent);

            // Hook up audio events
            var interpsToHook = new[]
            {
                "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_A.SeqAct_Interp_2", // A
                "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_B.SeqAct_Interp_2", // B
                "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_C.SeqAct_Interp_2", // C
            };

            for (int i = 0; i < interpsToHook.Length; i++)
            {
                var ifp = interpsToHook[i];
                var interpExp = reaperFightPackage.FindExport(ifp);
                var seq = SeqTools.GetParentSequence(interpExp);

                string letter = "A";
                if (i == 1)
                    letter = "B";
                if (i == 2)
                    letter = "C";

                var postEvent = MERSeqTools.CreateWwisePostEvent(seq, wwiseBangEvent);
                KismetHelper.CreateVariableLink(postEvent, "Target", MERSeqTools.CreateFindObject(seq, $"DestPlat_{letter}"));
                KismetHelper.CreateOutputLink(interpExp, "Bang", postEvent);
            }

            // Make reaper blow up platforms much earlier
            var destroyPlatformAseq = reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_A");
            var destroyPlatformBseq = reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_B");
            var destroyPlatformCseq = reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_C");

            // Create the remote event signalers
            var chooseInitialAttackSignalEvent = MERSeqTools.CreateActivateRemoteEvent(destroyPlatformAseq, "ChoosePlatformToDestroy");
            var chooseNextAttackASignalEvent = MERSeqTools.CreateActivateRemoteEvent(destroyPlatformAseq, "ChoosePlatformToDestroy");
            var chooseNextAttackBSignalEvent = MERSeqTools.CreateActivateRemoteEvent(destroyPlatformBseq, "ChoosePlatformToDestroy");
            var chooseNextAttackCSignalEvent = MERSeqTools.CreateActivateRemoteEvent(destroyPlatformCseq, "ChoosePlatformToDestroy");

            // Do not pick B or C Platform destroy sequences, only choose A
            SeqTools.SkipSequenceElement(reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.SeqAct_Switch_0"), "Link 1");

            // Finishing A -> Choose Next Attack
            {
                // Initial climb up
                var climbInterp = reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_A.SeqAct_Interp_0");
                var links = SeqTools.GetOutboundLinksOfNode(climbInterp);
                links[0][0].LinkedOp = chooseInitialAttackSignalEvent;
                SeqTools.WriteOutboundLinksToNode(climbInterp, links);

                // Finish
                var retractLog = reaperFightPackage.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_A.SeqAct_Log_3");
                KismetHelper.RemoveOutputLinks(retractLog); // Interp and voc
                KismetHelper.CreateOutputLink(retractLog, "Out", chooseNextAttackASignalEvent);
                KismetHelper.CreateOutputLink(retractLog,
                    "Out", // This is the gate that needs opened so that this logic can pass through once completed. Links to 'Open'
                    reaperFightPackage.FindExport(
                        "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_A.SeqAct_Gate_2"),
                    1);
            }
            // Finishing B -> Choose Next Attack

            // Wipes output of 'Completed' and points to our logic
            {
                var interp = reaperFightPackage.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_B.SeqAct_Interp_3");
                var links = SeqTools.GetOutboundLinksOfNode(interp);
                links[0].Clear();
                links[0].Add(new SeqTools.OutboundLink() { InputLinkIdx = 0, LinkedOp = chooseNextAttackBSignalEvent });
                // Opens the passthrough gate
                links[0].Add(new SeqTools.OutboundLink()
                {
                    InputLinkIdx = 1,
                    LinkedOp = reaperFightPackage.FindExport(
                        "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_B.SeqAct_Gate_2")
                });
                SeqTools.WriteOutboundLinksToNode(interp, links);
            }
            // Finishing C -> Choose Next Attack
            {
                // Fix tag name for platform C for support seq
                reaperFightPackage.FindExport("TheWorld.PersistentLevel.BioTriggerVolume_1").WriteProperty(new NameProperty("trig_PlatC_Outter", "Tag"));

                // Repoint retract interp to signal instead
                var interp = reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_C.SeqAct_Interp_3");
                var links = SeqTools.GetOutboundLinksOfNode(interp);
                links[0][0].LinkedOp = chooseNextAttackCSignalEvent;
                SeqTools.WriteOutboundLinksToNode(interp, links);

                // Also install exit event to retract
                var exit = MERSeqTools.CreateSeqEventRemoteActivated(SeqTools.GetParentSequence(interp), "ExitPlatformDestroyer");
                KismetHelper.CreateOutputLink(exit, "Out", reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_C.SeqAct_Interp_10"));
            }
            // These are receivers from the previous platform destroy to trigger the next animation
            var destroyAEvent = MERSeqTools.CreateSeqEventRemoteActivated(destroyPlatformAseq, "DestroyPlatformA");
            var destroyBEvent = MERSeqTools.CreateSeqEventRemoteActivated(destroyPlatformBseq, "DestroyPlatformB");
            var destroyCEvent = MERSeqTools.CreateSeqEventRemoteActivated(destroyPlatformCseq, "DestroyPlatformC");

            // Events close the input gates but don't use them
            KismetHelper.CreateOutputLink(destroyBEvent, "Out", reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_B.SeqAct_Gate_4"), 2);
            KismetHelper.CreateOutputLink(destroyCEvent, "Out", reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_C.SeqAct_Gate_4"), 2);

            // A Platform and reaper plays
            KismetHelper.CreateOutputLink(destroyAEvent, "Out", reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_A.SeqAct_Interp_3"));
            KismetHelper.CreateOutputLink(destroyAEvent, "Out", reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_A.SeqAct_Interp_2"));
            KismetHelper.CreateOutputLink(destroyAEvent, "Out", reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_A.SeqAct_WwisePostEvent_8"));

            // B Platform and reaper plays
            KismetHelper.CreateOutputLink(destroyBEvent, "Out", reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_B.SeqAct_Interp_3"));
            KismetHelper.CreateOutputLink(destroyBEvent, "Out", reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_B.SeqAct_Interp_2"));
            KismetHelper.CreateOutputLink(destroyCEvent, "Out", reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_B.SeqAct_WwisePostEvent_8"));

            // C Platform and reaper plays
            KismetHelper.CreateOutputLink(destroyCEvent, "Out", reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_C.SeqAct_Interp_3"));
            KismetHelper.CreateOutputLink(destroyCEvent, "Out", reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_C.SeqAct_Interp_2"));
            KismetHelper.CreateOutputLink(destroyCEvent, "Out", reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_C.SeqAct_WwisePostEvent_8"));

            // Make the time after he retracts not a million years
            reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.SeqVar_Float_6").WriteProperty(new FloatProperty(1, "FloatValue"));

            // Suicide bombing during this sequence
            string[] allowedPawns = new[] { "MERChar_EndGm2.SuicideBomination" }; // Special sequence for spawning will kill these as they fly over and these pawns have timed detonators

            // Coming up
            AddRandomSpawns(target, reaperFightPackage, "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.Destroy_Platform_A.SeqAct_Gate_4",
                allowedPawns, 2, MERSeqTools.CreateNewSquadObject(destroyPlatformAseq).InstancedFullPath, 500f, sequenceSupportPackage, spawnSeqName: "EndGm2RandomEnemySpawnSuicide");

            // One for each platform destroyed
            AddRandomSpawns(target, reaperFightPackage, destroyAEvent.InstancedFullPath,
                allowedPawns, 1, MERSeqTools.CreateNewSquadObject(destroyPlatformBseq).InstancedFullPath, 500f, sequenceSupportPackage, spawnSeqName: "EndGm2RandomEnemySpawnSuicide");
            AddRandomSpawns(target, reaperFightPackage, destroyBEvent.InstancedFullPath,
                allowedPawns, 1, MERSeqTools.CreateNewSquadObject(destroyPlatformBseq).InstancedFullPath, 500f, sequenceSupportPackage, spawnSeqName: "EndGm2RandomEnemySpawnSuicide");
            AddRandomSpawns(target, reaperFightPackage, destroyCEvent.InstancedFullPath,
                allowedPawns, 1, MERSeqTools.CreateNewSquadObject(destroyPlatformBseq).InstancedFullPath, 500f, sequenceSupportPackage, spawnSeqName: "EndGm2RandomEnemySpawnSuicide");


            // Add shepard ragdoll recovery to work around... fun issues with ragdoll
            MERSeqTools.InstallSequenceStandalone(sequenceSupportPackage.FindExport("ShepardRagdollRecovery"),
                reaperFightPackage, null);


            // Do not save, calling method will handle it
        }

        /// <summary>
        /// Makes Reaper destroy platforms and use different weapons, more HP, randomly pick popup
        /// </summary>
        /// <param name="reaperFightP"></param>
        private static void MichaelBayifyFinalFight(GameTarget target, IMEPackage reaperFightP, IMEPackage sequenceSupportPackage)
        {
            // Give more HP since squadmates do a lot more now
            var loadout = reaperFightP.FindExport("BioChar_Loadouts.Collector.BOS_Reaper");
            var shields = loadout.GetProperty<ArrayProperty<StructProperty>>("ShieldLoadouts");
            // 12000 -> 17000
            shields[0].GetProp<StructProperty>("MaxShields").GetProp<FloatProperty>("X").Value = 17000;
            shields[0].GetProp<StructProperty>("MaxShields").GetProp<FloatProperty>("Y").Value = 17000;
            loadout.WriteProperty(shields);

            // Add reaper weapon handler
            var fireWeaponAts = reaperFightP.Exports.Where(x => x.ClassName == @"SFXSeqAct_FireWeaponAt").ToList();
            var matchingInputIdxs = new List<int>(new[] { 0 });

            foreach (var fwa in fireWeaponAts)
            {
                var seqObjs = SeqTools.GetAllSequenceElements(fwa).OfType<ExportEntry>();
                var inboundLinks = SeqTools.FindOutboundConnectionsToNode(fwa, seqObjs, matchingInputIdxs);
                if (inboundLinks.Count != 1)
                {
                    Debugger.Break();
                    continue; // Something is wrong!!
                }


                var newSeq = MERSeqTools.InstallSequenceChained(
                    sequenceSupportPackage.FindExport("ReaperWeaponHandler"), reaperFightP,
                    SeqTools.GetParentSequence(fwa), fwa, 0);

                var source = inboundLinks[0];
                var outlinks = SeqTools.GetOutboundLinksOfNode(source);
                foreach (var v in outlinks)
                {
                    foreach (var o in v)
                    {
                        if (o.LinkedOp == fwa && o.InputLinkIdx == 0)
                        {
                            o.LinkedOp = newSeq; // Repoint output link from FWA to our new sequnce instead
                        }
                    }
                }

                SeqTools.WriteOutboundLinksToNode(source, outlinks);

                // Add attack during idle step
                var log = seqObjs.FirstOrDefault(x => x.ClassName == "SeqAct_Log" && x.GetProperty<ArrayProperty<StrProperty>>("m_aObjComment") is ArrayProperty<StrProperty> comments && comments.Count == 1 && comments[0].Value.Contains("Starting Idle"));
                if (log == null)
                {
                    continue;
                }

                // Link to extra firing handler
                var firingHandler = MERSeqTools.InstallSequenceChained(sequenceSupportPackage.FindExport("ReaperExtraWeaponFiringController"), reaperFightP, SeqTools.GetParentSequence(fwa), newSeq, 0);
                KismetHelper.CreateVariableLink(firingHandler, "Reaper", MERSeqTools.CreateFindObject(SeqTools.GetParentSequence(firingHandler), "BOSS_ProtoReaper"));
                KismetHelper.CreateOutputLink(log, "Out", firingHandler);
            }

            // Award the player 2 medigel on battle start - since it's gonna be nearly impossible with none
            var initCombatSeq = reaperFightP.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Initialize_Reaper_Combat");
            var medigelStartHook = reaperFightP.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Initialize_Reaper_Combat.BioSeqAct_StopLoadingMovie_2");
            KismetHelper.CreateOutputLink(medigelStartHook, "Done", MERSeqTools.CreateActivateRemoteEvent(initCombatSeq, "FinalFightStart"));
            MERSeqTools.InstallSequenceStandalone(sequenceSupportPackage.FindExport("FinalFightStart"), reaperFightP, initCombatSeq);


            // Random popup spot
            var rSwitch = reaperFightP.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.SeqAct_Switch_1");
            rSwitch.ObjectName = new NameReference("SeqAct_RandomSwitch", rSwitch.ObjectName.Number);
            rSwitch.Class = EntryImporter.EnsureClassIsInFile(reaperFightP, "SeqAct_RandomSwitch", new RelinkerOptionsPackage(MERCaches.GlobalCommonLookupCache), gamePathOverride: target.TargetPath);

            // Add Ashley/Kaidan to introduce the fight

            // Add gated special spawns based on who died on Virmire
            var logHook = reaperFightP.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Reaper_Attack_Loop.SeqAct_Log_2");
            var seq = SeqTools.GetParentSequence(logHook);
            var squad = MERSeqTools.CreateNewSquadObject(seq, squadActor: reaperFightP.FindExport("TheWorld.PersistentLevel.BioSquadCombat_4")); // This squad, on death, will drop heavy weapon ammo/medigel
            var pmCheckState = MERSeqTools.CreatePMCheckState(seq, 1541); // Kaidan Died
            var kaSpawnDelay = MERSeqTools.CreateDelay(seq, 5);

            // Add delay to start of spawn
            KismetHelper.CreateOutputLink(logHook, "Out", kaSpawnDelay);
            KismetHelper.CreateOutputLink(kaSpawnDelay, "Finished", pmCheckState);

            AddRandomSpawns(target, reaperFightP, pmCheckState.InstancedFullPath, new[] { "MERChar_Enemies.KaidanSpawnable" }, 2, squad.InstancedFullPath, 1000, sequenceSupportPackage, hookupOutputIdx: 0, delayMultiplier: 4); // 0 = true, Kaidan died (use him for the fight)
            AddRandomSpawns(target, reaperFightP, pmCheckState.InstancedFullPath, new[] { "MERChar_Enemies.AshleySpawnable" }, 2, squad.InstancedFullPath, 1000, sequenceSupportPackage, hookupOutputIdx: 1, delayMultiplier: 4); // 1 = false, Kaidan lived (use Ashley instead)


            // Fix collector possession code
            var sixtyLog = reaperFightP.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqAct_Log_13");
            var startPossessionLoop = reaperFightP.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Add_CollectorPossessed_To_Combat");
            KismetHelper.CreateOutputLink(sixtyLog, "Out", startPossessionLoop);
            AddRandomSpawnsToFinalFight(target, reaperFightP, sequenceSupportPackage);
            PlatformDestroyer(target, reaperFightP, sequenceSupportPackage);
        }

        private static void Update425Tubes(GameTarget target, IMEPackage sequenceSupportPackage)
        {
            var tubesF = MERFileSystem.GetPackageFile(target, "BioD_EndGm2_425ReaperTubes.pcc");
            if (tubesF != null && File.Exists(tubesF))
            {
                var reaperTubesP = MEPackageHandler.OpenMEPackage(tubesF);

                // Change open all to single tube
                var baseName = "SEQ_HANDLE_REAPERTUBE0";
                for (int i = 1; i <= 4; i++)
                {
                    var tubeSeqIFP = $"TheWorld.PersistentLevel.Main_Sequence.REAPER_DESTRUCTABLE_CONNECTION_HANDLER.{baseName}{i}";
                    var tubeCloseTriggerIFP = $"TheWorld.PersistentLevel.Main_Sequence.REAPER_DESTRUCTABLE_CONNECTION_HANDLER.SEQ_HANDLE_REAPERTUBE0{i}.SeqAct_Toggle_4";
                    var tubeSeq = reaperTubesP.FindExport(tubeSeqIFP);
                    var tubeCloseTrigger = reaperTubesP.FindExport(tubeCloseTriggerIFP);

                    // Update events
                    var objects = SeqTools.GetAllSequenceElements(tubeSeq).OfType<ExportEntry>().ToList();

                    foreach (var obj in objects.Where(x => x.ClassName == "SeqEvent_RemoteEvent"))
                    {
                        var eventName = obj.GetProperty<NameProperty>("EventName");
                        bool isOpen = false;
                        if (eventName.Value.Name.Contains("OPEN", StringComparison.InvariantCultureIgnoreCase))
                        {
                            eventName.Value = $"RE_OPEN_TUBE{i}";
                            isOpen = true;
                        }
                        else if (eventName.Value.Name.Contains("CLOSE", StringComparison.InvariantCultureIgnoreCase))
                        {
                            eventName.Value = $"RE_CLOSE_TUBE{i}";
                        }

                        obj.WriteProperty(eventName);

                        if (isOpen)
                        {
                            // Open a different tube if this one is closed
                            var pmCheckState = MERSeqTools.GetNextNode(obj, 0);
                            var openAll = MERSeqTools.CreateActivateRemoteEvent(tubeSeq, "RE_OPEN_ALL_TUBES");
                            var calloutEvent = MERSeqTools.CreateActivateRemoteEvent(tubeSeq, "CALLOUT_SHOOT_TUBES");
                            KismetHelper.CreateOutputLink(pmCheckState, "True", openAll);
                            KismetHelper.CreateOutputLink(pmCheckState, "False", calloutEvent);
                        }
                    }

                    // Add close timer
                    var randDelay = MERSeqTools.CreateRandomDelay(tubeSeq, 4, 12);
                    var closeTube = MERSeqTools.CreateActivateRemoteEvent(tubeSeq, "RE_CLOSE_ALL_TUBES");
                    KismetHelper.CreateOutputLink(tubeCloseTrigger, "Out", randDelay);
                    KismetHelper.CreateOutputLink(randDelay, "Finished", closeTube);
                }

                // Install new handler for the vanilla remote events
                MERSeqTools.InstallSequenceStandalone(sequenceSupportPackage.FindExport("MER_REAPER_TUBE_HANDLER"), reaperTubesP);

                // Install tube counter helper
                // Call 'GetDestroyedTubeCount' and listen for 'TubeCount1/2/3/4' for how many tubes have been cut
                MERSeqTools.InstallSequenceStandalone(sequenceSupportPackage.FindExport("MER_REAPER_TUBE_COUNTER"), reaperTubesP);


                // Gate attack properly so we can fire it independently of tubes opening
                var windDownInterp = reaperTubesP.FindExport("TheWorld.PersistentLevel.Main_Sequence.REAPER_DESTRUCTABLE_CONNECTION_HANDLER.SEQ_HANDLE_REAPER_ATTACK.SeqAct_Interp_1");
                var attackSeq = SeqTools.GetParentSequence(windDownInterp);
                var gate = MERSeqTools.CreateGate(attackSeq);

                var attackDelay = reaperTubesP.FindExport("TheWorld.PersistentLevel.Main_Sequence.REAPER_DESTRUCTABLE_CONNECTION_HANDLER.SEQ_HANDLE_REAPER_ATTACK.SeqAct_Delay_4");
                MERSeqTools.RedirectInboundLinks(attackDelay, gate);
                KismetHelper.CreateOutputLink(gate, "Out", reaperTubesP.FindExport("TheWorld.PersistentLevel.Main_Sequence.REAPER_DESTRUCTABLE_CONNECTION_HANDLER.SEQ_HANDLE_REAPER_ATTACK.SeqAct_Delay_4"));
                KismetHelper.CreateOutputLink(gate, "Out", gate, 2); // Close gate

                KismetHelper.CreateOutputLink(windDownInterp, "Completed", gate, 1); // Open
                KismetHelper.CreateOutputLink(windDownInterp, "Reversed", gate, 1); // Open

                // Reaper attack hits squadmates
                var causeDamageObj = reaperTubesP.FindExport("TheWorld.PersistentLevel.Main_Sequence.REAPER_DESTRUCTABLE_CONNECTION_HANDLER.SEQ_HANDLE_REAPER_ATTACK.BioSeqAct_CauseDamage_0");
                causeDamageObj.WriteProperty(new FloatProperty(300, "DamageAmount")); // Nerf 1000 -> 300
                var sqm1 = reaperTubesP.FindExport("TheWorld.PersistentLevel.Main_Sequence.REAPER_DESTRUCTABLE_CONNECTION_HANDLER.SEQ_HANDLE_REAPER_ATTACK.SeqVar_Object_2");
                var sqm2 = reaperTubesP.FindExport("TheWorld.PersistentLevel.Main_Sequence.REAPER_DESTRUCTABLE_CONNECTION_HANDLER.SEQ_HANDLE_REAPER_ATTACK.SeqVar_Object_3");
                KismetHelper.CreateVariableLink(causeDamageObj, "Target", sqm1);
                KismetHelper.CreateVariableLink(causeDamageObj, "Target", sqm2);

                // Add vocalization
                var causeDamageVocal = MERSeqTools.CreateAndAddToSequence(attackSeq, "MERSeqAct_CauseDamageVocal");
                KismetHelper.CreateOutputLink(causeDamageObj, "Out", causeDamageVocal);

                // Port over the variable links
                causeDamageVocal.WriteProperty(causeDamageObj.GetProperty<ArrayProperty<StructProperty>>("VariableLinks"));

                // Remove tube handling from reaper attack
                // Open all tubes
                MERSeqTools.RemoveLinksTo(reaperTubesP.FindExport("TheWorld.PersistentLevel.Main_Sequence.REAPER_DESTRUCTABLE_CONNECTION_HANDLER.SEQ_HANDLE_REAPER_ATTACK.SeqAct_ActivateRemoteEvent_8"));
                // Close all tubes
                MERSeqTools.RemoveLinksTo(reaperTubesP.FindExport("TheWorld.PersistentLevel.Main_Sequence.REAPER_DESTRUCTABLE_CONNECTION_HANDLER.SEQ_HANDLE_REAPER_ATTACK.SeqAct_ActivateRemoteEvent_7"));

                // Update tubes dialogue to only play when we are actually doing tube stuff
                var tubeLineSeq = reaperTubesP.FindExport("TheWorld.PersistentLevel.Main_Sequence.REAPER_DESTRUCTABLE_CONNECTION_HANDLER.SEQ_HANDLE_REAPER_ATTACK.Tube_Warning");
                MERSeqTools.RemoveLinksTo(tubeLineSeq);
                var shootTubes = MERSeqTools.CreateSeqEventRemoteActivated(tubeLineSeq, "CALLOUT_SHOOT_TUBES");
                var delay = MERSeqTools.CreateDelay(tubeLineSeq, 1.5f);
                KismetHelper.CreateOutputLink(shootTubes, "Out", delay);
                KismetHelper.CreateOutputLink(delay, "Finished", reaperTubesP.FindExport("TheWorld.PersistentLevel.Main_Sequence.REAPER_DESTRUCTABLE_CONNECTION_HANDLER.SEQ_HANDLE_REAPER_ATTACK.Tube_Warning.SeqAct_Switch_0"));

                MERFileSystem.SavePackage(reaperTubesP);
            }
        }

        private static void UpdatePreFinalBattle(GameTarget target, IMEPackage sequenceSupportPackage)
        {
            string file = "BioD_EndGm2_420CombatZone.pcc";
            using var package = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, file));

            // Logic here
            AutomatePlatforming400(package); // This is act 1 of final battle (before the human reaper)
            AutomatePlatforming420(package); // This is act 2 of final battle
            UnconstipatePathing(package);
            RandomizePreReaperSpawns(target, package, sequenceSupportPackage); // Act 2 of final battle (before active human reaper)
            Update425Tubes(target, sequenceSupportPackage); // This saves package for 425ReaperTubes which is used in Act 2 battle
            UpdatePreReaperAttacks(target, package, sequenceSupportPackage);
            UpdateTubeOpenings(target, package);
            MERFileSystem.SavePackage(package);

        }

        private static void UpdateTubeOpenings(GameTarget target, IMEPackage package)
        {
            // Controls when tubes open
            var trigger = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.PostPlatform_CombatFiller.BioSeqAct_PMCheckState_0");
            var seq = SeqTools.GetParentSequence(trigger);
            var delay = MERSeqTools.CreateRandomDelay(seq, 2, 15);
            KismetHelper.CreateOutputLink(trigger, "False", delay);

            var open = MERSeqTools.CreateActivateRemoteEvent(seq, "RE_OPEN_ALL_TUBES");
            KismetHelper.CreateOutputLink(delay, "Finished", open);
        }

        private static void UpdatePreReaperAttacks(GameTarget target, IMEPackage package, IMEPackage sequenceSupportPackage)
        {
            // Controls when the reaper attacks

            // Reaper attacks when all platforms have docked
            var plat3Interp = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.CombatB_IncomingPlatform03.SeqAct_Interp_1");
            var seq = SeqTools.GetParentSequence(plat3Interp);
            var remote = MERSeqTools.CreateActivateRemoteEvent(seq, "RE_START_REAPER_ATTACK");
            var remote2 = MERSeqTools.CreateActivateRemoteEvent(seq, "RE_OPEN_ALL_TUBES");
            KismetHelper.CreateOutputLink(plat3Interp, "Completed", remote); // Start attack when final platform has docked
            KismetHelper.CreateOutputLink(plat3Interp, "Completed", remote2); // Start first tube when final platform has docked

            // Control other times when he attacks shortly after post combat filler starts
            var trigger = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.PostPlatform_CombatFiller.BioSeqAct_PMCheckState_0");
            var delay = MERSeqTools.CreateRandomDelay(seq, 0, 8);
            KismetHelper.CreateOutputLink(trigger, "False", delay);

            var open = MERSeqTools.CreateActivateRemoteEvent(seq, "RE_START_REAPER_ATTACK");
            KismetHelper.CreateOutputLink(delay, "Finished", open);

            // Reaper should not attack just cause squad died
            var squadDiedTrig = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.PostPlatform_CombatFiller.SeqEvent_RemoteEvent_6");
            KismetHelper.RemoveOutputLinks(squadDiedTrig);

            var gateTrigger = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.PostPlatform_CombatFiller.BioSeqAct_PMCheckState_1");
            seq = SeqTools.GetParentSequence(gateTrigger);
            var gate = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.PostPlatform_CombatFiller.SeqAct_Gate_3");
            KismetHelper.RemoveOutputLinks(gateTrigger);

            delay = MERSeqTools.CreateRandomDelay(seq, 2, 6);
            KismetHelper.CreateOutputLink(gateTrigger, "False", delay); // Give player a second
            KismetHelper.CreateOutputLink(gateTrigger, "True", gate, 2); // Close
            KismetHelper.CreateOutputLink(delay, "Finished", gate); // In

            // Fix post-combat filler to start when the three squads are killed
            var squadA = package.FindExport("TheWorld.PersistentLevel.BioSquadCombat_10");
            var squadB = package.FindExport("TheWorld.PersistentLevel.BioSquadCombat_2");

            var deathA = MERSeqTools.CreateSeqEventDeath(seq, squadA);
            var deathB = MERSeqTools.CreateSeqEventDeath(seq, squadB);
            var deathC = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.PostPlatform_CombatFiller.SeqEvent_Death_2");
            KismetHelper.RemoveOutputLinks(deathC); // Remove outlinks from this

            // Setup all deaths to our new helpers
            var getSquadStatus = sequenceSupportPackage.FindExport("GetSquadStatus");
            var checkSquadA = MERSeqTools.InstallSequenceStandalone(getSquadStatus, package, seq);
            var checkSquadB = MERSeqTools.InstallSequenceStandalone(getSquadStatus, package, seq);
            var checkSquadC = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.PostPlatform_CombatFiller.SequenceReference_3");

            KismetHelper.CreateOutputLink(checkSquadA, "SquadEmpty", checkSquadB);
            KismetHelper.CreateVariableLink(checkSquadA, "SquadToCheck", MERSeqTools.CreateNewSquadObject(seq, squadActor: squadA));
            KismetHelper.CreateOutputLink(checkSquadB, "SquadEmpty", checkSquadC);
            KismetHelper.CreateVariableLink(checkSquadB, "SquadToCheck", MERSeqTools.CreateNewSquadObject(seq, squadActor: squadB));
            // C already goes to next for us, already has squad setup

            // Hook up deaths - all go to same obj
            KismetHelper.CreateOutputLink(deathA, "Out", checkSquadA);
            KismetHelper.CreateOutputLink(deathB, "Out", checkSquadA);
            KismetHelper.CreateOutputLink(deathC, "Out", checkSquadA);

            // Make gate open for squad all dead
            package.FindExport("TheWorld.PersistentLevel.Main_Sequence.PostPlatform_CombatFiller.SeqAct_Gate_3").RemoveProperty("bOpen");


        }

        private static void AutomatePlatforming420(IMEPackage package)
        {
            var combatBPlat1 = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.CombatB_IncomingPlatform01");
            var seq = SeqTools.GetParentSequence(combatBPlat1);

            var hackLog = MERSeqTools.CreateLog(seq, "Starting auto combat platforming");

            MERSeqTools.RedirectInboundLinks(combatBPlat1, hackLog);
            var delay2 = MERSeqTools.CreateDelay(seq, 12); // Delay for Platform 2 to arrive
            var delay3 = MERSeqTools.CreateDelay(seq, 12); // Delay for Platform 3 to arrive - they both arrive at the same time anyways due to how the flow-through works on the objects

            var combatBPlat2 = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.CombatB_IncomingPlatform02");
            var combatBPlat3 = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.CombatB_IncomingPlatform03");

            KismetHelper.CreateOutputLink(hackLog, "Out", combatBPlat1);
            KismetHelper.CreateOutputLink(hackLog, "Out", delay2);
            KismetHelper.CreateOutputLink(hackLog, "Out", delay3);
            KismetHelper.CreateOutputLink(delay2, "Finished", combatBPlat2);
            KismetHelper.CreateOutputLink(delay3, "Finished", combatBPlat3);

            // Fix the gates in each so they can trigger logic without waiting for reaper attack
            var plat2BActivate = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.CombatB_IncomingPlatform02.SeqEvent_SequenceActivated_0");
            var plat3BActivate = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.CombatB_IncomingPlatform03.SeqEvent_SequenceActivated_2");

            // These are gates
            var plat2BNext = MERSeqTools.GetNextNode(plat2BActivate, 0);
            var plat3BNext = MERSeqTools.GetNextNode(plat3BActivate, 0);

            KismetHelper.RemoveOutputLinks(plat2BActivate);
            KismetHelper.RemoveOutputLinks(plat3BActivate);

            KismetHelper.CreateOutputLink(plat2BActivate, "Out", plat2BNext); // In
            KismetHelper.CreateOutputLink(plat3BActivate, "Out", plat3BNext); // In

            plat2BNext.RemoveProperty("bOpen"); // Open initially
            plat3BNext.RemoveProperty("bOpen");
        }

        private static void UpdateFinalBattle(GameTarget target, IMEPackage sequenceSupportPackage)
        {
            string file = "BioD_EndGm2_430ReaperCombat.pcc";
            using var package = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, file));

            // Logic here
            PatchReaper(target, package, sequenceSupportPackage); // This must be done first as it modifies classes
            MichaelBayifyFinalFight(target, package, sequenceSupportPackage);
            InstallAtmosphereHandler(package, sequenceSupportPackage); // <--- This must come before ChangeLowHealthEnemies!
            ChangeLowHealthEnemies(target, package, sequenceSupportPackage);
            RandomizePickups(target, package, sequenceSupportPackage);
            MarkDownedSquadmatesDead(package, sequenceSupportPackage);
            ImproveSquadmateAI(package, sequenceSupportPackage);
            AddNewHarbyVoiceLines(target, package, sequenceSupportPackage);
            FixSquadmatesNeverRespawning(target, package, sequenceSupportPackage);
            InstallDifficultyHelper(target, package, sequenceSupportPackage);
            MERFileSystem.SavePackage(package);
        }

        private static void FixSquadmatesNeverRespawning(GameTarget target, IMEPackage package, IMEPackage sequenceSupportPackage)
        {
            // Henchmen can fly off and never respawn due to bad logic in their states
            // This is the lazy way of fixing it
            MERSeqTools.InstallSequenceStandalone(sequenceSupportPackage.FindExport("HenchRespawner"), package,
                package.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler"));
        }

        private static void InstallDifficultyHelper(GameTarget target, IMEPackage package, IMEPackage sequenceSupportPackage)
        {
            // Engineer's kit sucks real bad in this game
            // Make it slightly easier on higher difficult for them
            MERSeqTools.InstallSequenceStandalone(sequenceSupportPackage.FindExport("EngineerHelper"), package,
                package.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler"));
        }

        private static void AddNewHarbyVoiceLines(GameTarget target, IMEPackage reaperFightPackage, IMEPackage sequenceSupportPackage)
        {
            // Todo
            // Port Wwise stuff from 425 to 430 LOC files
            // ADd imports
            // Reference wwise events 

            var langs = new[] { "INT", "FRA", "DEU", "ITA", "POL" };

            // We only need some events, not all of them
            var pathsToCopy = new[]
            {
                // 80%
                ("endgm2_boss_fight_a_S.VO_314678_f_Play", "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqAct_Log_9"), // extinction is inevitable

                // 60% 
                ("endgm2_boss_fight_a_S.VO_314681_f_Play","TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqAct_Log_13"), // This vision is your future
                
                // 50%
                ("endgm2_boss_fight_a_S.VO_314676_m_Play", "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqAct_Log_12"), // There is no escape human
                
                // 30%
                ("endgm2_boss_fight_a_S.VO_314690_f_Play", "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqAct_Log_8"), // Your disrespect grows tiresome
            };

            foreach (var lang in langs)
            {
                var filePath425 = MERFileSystem.GetPackageFile(target, $"BioD_EndGm2_425ReaperTubes_LOC_{lang}.pcc");
                if (filePath425 == null)
                    continue; // User removed localization


                var reaperFight430 = MERFileSystem.GetPackageFile(target, $"BioD_EndGm2_430ReaperCombat_LOC_{lang}.pcc");
                if (reaperFight430 == null)
                    continue; // User removed localization

                var package425 = MERFileSystem.OpenMEPackage(filePath425);
                var package430 = MERFileSystem.OpenMEPackage(reaperFight430);

                foreach (var path in pathsToCopy)
                {
                    var sourceExp = package425.FindExport(path.Item1);
                    EntryExporter.ExportExportToPackage(sourceExp, package430, out var entry, MERCaches.GlobalCommonLookupCache);
                    PackageTools.AddToObjectReferencer(entry);
                }

                MERFileSystem.SavePackage(package430);
            }

            var seq = reaperFightPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler");
            var protoReaper = MERSeqTools.CreateFindObject(seq, "BOSS_ProtoReaper");
            var link = PackageTools.CreateImport(reaperFightPackage, "endgm2_boss_fight_a_S", "Package", "Core");
            foreach (var path in pathsToCopy)
            {
                var hookup = reaperFightPackage.FindExport(path.Item2);
                var wwiseEvent = PackageTools.CreateImport(reaperFightPackage, path.Item1.Substring(path.Item1.IndexOf(".") + 1), "WwiseEvent", "WwiseAudio", link);
                var wwiseObj = MERSeqTools.CreateWwisePostEvent(seq, wwiseEvent);
                KismetHelper.CreateVariableLink(wwiseObj, "Target", protoReaper);
                KismetHelper.CreateOutputLink(hookup, "Out", wwiseObj);

            }

            // Add post events
        }

        private static void PatchReaper(GameTarget target, IMEPackage package, IMEPackage sequenceSupportPackage)
        {
            // BioWer Pls
            // Why is the reaper in here
            var stasisNew = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, "SFXPower_StasisNew.pcc"));

            var packages = new[] { package, stasisNew };
            foreach (var p in packages)
            {
                // This must be ported in because it will not be in non suicide mission packages
                // var ported = PackageTools.PortExportIntoPackage(target, p, sequenceSupportPackage.FindExport("MERGameContent.MERReaperWeaponInfo"));

                ScriptTools.InstallClassToPackageFromEmbedded(target, p, "SFXAI_Reaper", "SFXGamePawns");

                ScriptTools.AddToClassInPackageFromEmbedded(target, p, "SFXPawn_Reaper.TakeDamage",
                    "SFXGamePawns.SFXPawn_Reaper"); // Prevents damage to self or from team
                /* ScriptTools.AddToClassInPackageFromEmbedded(target, p, "SFXAI_Reaper.SelectTarget",
                     "SFXGamePawns.SFXAI_Reaper"); // Ignore stealthed and non-player party targets
                 ScriptTools.AddToClassInPackageFromEmbedded(target, p, "SFXAI_Reaper.OnTargetChanged",
                     "SFXGamePawns.SFXAI_Reaper"); // Do not stop firing on target change */
#if DEBUG
                p.FindExport("SFXGamePawns.SFXAI_Reaper").GetDefaults().WriteProperty(new BoolProperty(true, "bAILogging"));
                p.FindExport("SFXGamePawns.SFXAI_Reaper").GetDefaults().WriteProperty(new BoolProperty(true, "bAILogToWindow"));
#endif
            }

            MERFileSystem.SavePackage(stasisNew);

#if DEBUG
            MERDebug.InstallDebugScript(target, "SFXGame.pcc", "BioAiController.AILog_Internal");
#endif
            // Incoming package is not saved as its passed through
        }


        private static void RandomizePickups(GameTarget target, IMEPackage package, IMEPackage sequenceSupportPackage)
        {
            // Items to branch from
            var gates = new[]
            {
                "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Add_CollectorPossessed_To_Combat.SequenceReference_0.Sequence_2443.SeqAct_Gate_0",
                "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Add_CollectorPossessed_To_Combat.SequenceReference_4.Sequence_2445.SeqAct_Gate_0",
                "TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Add_CollectorPossessed_To_Combat.SequenceReference_1.Sequence_2444.SeqAct_Gate_0"
            };

            foreach (var gateIFP in gates)
            {
                var gate = package.FindExport(gateIFP);
                var sequence = SeqTools.GetParentSequence(gate);

                // The original option
                var awardAmmo = MERSeqTools.FindSequenceObjectByClassAndPosition(sequence, "SFXSeqAct_AwardResource");

                // The end result
                var toggleHidden = MERSeqTools.GetNextNode(awardAmmo, 0);

                KismetHelper.RemoveOutputLinks(gate);
                KismetHelper.CreateOutputLink(gate, "Out", gate, 2); // Close self

                // New option
                var awardMedigel = SequenceObjectCreator.CreateSequenceObject(package, "SFXSeqAct_AwardResource",
                    MERCaches.GlobalCommonLookupCache);
                awardMedigel.WriteProperty(new EnumProperty("MEDIGEL_TREASURE", "ETreasureType", package.Game,
                    "TreasureType"));
                KismetHelper.AddObjectToSequence(awardMedigel, sequence);
                KismetHelper.CreateVariableLink(awardMedigel, "Amount", MERSeqTools.CreateInt(sequence, 2));
                KismetHelper.CreateOutputLink(awardMedigel, "Out", toggleHidden);

                // Install branching
                var randSwitch = MERSeqTools.CreateRandSwitch(sequence, 2);
                KismetHelper.CreateOutputLink(randSwitch, "Link 1", awardAmmo);
                KismetHelper.CreateOutputLink(randSwitch, "Link 2", awardMedigel);

                KismetHelper.CreateOutputLink(gate, "Out", randSwitch);
            }
        }

        private static void MarkDownedSquadmatesDead(IMEPackage package, IMEPackage sequenceSupportPackage)
        {
            var trigger = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SeqAct_Log_7");
            var end = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SequenceReference_20");
            KismetHelper.RemoveOutputLinks(trigger);

            var mark = SequenceObjectCreator.CreateSequenceObject(package, "MERSeqAct_MarkDownedSquadmatesUnloyal", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(mark, SeqTools.GetParentSequence(trigger));
            KismetHelper.CreateOutputLink(trigger, "Out", mark);
            KismetHelper.CreateOutputLink(mark, "Out", end);
        }

        private static void ImproveSquadmateAI(IMEPackage package, IMEPackage sequenceSupportPackage)
        {
            // Installs HenchTargeting sequence to the Main_Sequence
            MERSeqTools.InstallSequenceStandalone(sequenceSupportPackage.FindExport("HenchTargetting"), package);
            var trigger = package.FindExport("TheWorld.PersistentLevel.Main_Sequence.Reaper_Combat_Handler.SEQ_Initialize_Reaper_Combat.BioSeqAct_TurnTowards_1");
            var remoteEvent = MERSeqTools.CreateActivateRemoteEvent(SeqTools.GetParentSequence(trigger), "SetupFinalBattleHench"); // Trigger HenchTargetting
            KismetHelper.CreateOutputLink(trigger, "Out", remoteEvent);

            // Change the tag of a gunship so HenchTargetting can find it
            var eyeLeft = package.FindExport("TheWorld.PersistentLevel.SFXPawn_Targetable_Gunship_6");
            eyeLeft.WriteProperty(new NameProperty("HenchTargetAid", "Tag"));
        }


        private static void RandomizePreReaperSpawns(GameTarget target, IMEPackage preReaperFightP, IMEPackage sequenceSupportPackage)
        {
            // This changes the collectors that fly in 2 at a time
            //GenericRandomizeAIFactorySpawns(target, preReaperFightP, new[] { "MERChar_Enemies.AshleySpawnable" });

            var crawlingCombatIntroTrigger = preReaperFightP.FindExport("TheWorld.PersistentLevel.Main_Sequence.PostPlatform_CombatFiller.BioSeqAct_PMCheckState_0");
            var seq = SeqTools.GetParentSequence(crawlingCombatIntroTrigger);
            var getCount = MERSeqTools.CreateActivateRemoteEvent(seq, "GetDestroyedTubesCount");
            KismetHelper.CreateOutputLink(crawlingCombatIntroTrigger, "False", getCount); // This will get count

            var d1Tube = MERSeqTools.CreateSeqEventRemoteActivated(seq, "Destroyed1Tube");
            var d2Tube = MERSeqTools.CreateSeqEventRemoteActivated(seq, "Destroyed2Tubes");
            var d3Tube = MERSeqTools.CreateSeqEventRemoteActivated(seq, "Destroyed3Tubes");

            var squad = MERSeqTools.CreateNewSquadObject(seq);
            AddRandomSpawns(target, preReaperFightP, d1Tube.InstancedFullPath, new[] { "BioChar_Collectors.ELT_Scion" }, 1, squad.InstancedFullPath, 2000, sequenceSupportPackage);
            AddRandomSpawns(target, preReaperFightP, d2Tube.InstancedFullPath, new[] { "MERChar_Enemies.ChargingHusk", "MERChar_EndGm2.Bomination" }, 3, squad.InstancedFullPath, 1500, sequenceSupportPackage);
            AddRandomSpawns(target, preReaperFightP, d3Tube.InstancedFullPath, new[] { "MERChar_EndGm2.Bomination" }, 8, squad.InstancedFullPath, 1000, sequenceSupportPackage);
        }

        /// <summary>
        /// Removes bBlocked from all MantleMarkers in the file
        /// </summary>
        /// <param name="reaper420Pathing"></param>
        private static void UnconstipatePathing(IMEPackage reaper420Pathing)
        {
            // Pathing in final battle is total ass
            // It's no wonder your squadmates do nothing

            // For some reason all the MantleMarkers have bBlocked = true which is preventing 
            // AI from being able to move around, and IDK why
            // This removes them from the final boss area as well as the platforming before it (400 Platforming)

            foreach (var mm in reaper420Pathing.Exports.Where(x => x.ClassName == "MantleMarker"))
            {
                mm.RemoveProperty("bBlocked"); // Get rid of it
            }

            // Fix bad map design where platform C can be mantled onto even if it's gone
            var nonFlyingSeq = reaper420Pathing.FindExport("TheWorld.PersistentLevel.Main_Sequence.NON_FLYING_PLATFORMS");
            var platDestroyedEvent = reaper420Pathing.FindExport("TheWorld.PersistentLevel.Main_Sequence.NON_FLYING_PLATFORMS.SeqEvent_RemoteEvent_2");
            var modifyCover = MERSeqTools.CreateAndAddToSequence(nonFlyingSeq, "SeqAct_ModifyCover");
            modifyCover.WriteProperty(new ArrayProperty<IntProperty>(new[] { 0, 1, 2, 3 }.Select(x => new IntProperty(x)), "Slots"));
            KismetHelper.CreateVariableLink(modifyCover, "Target", MERSeqTools.CreateObject(nonFlyingSeq, reaper420Pathing.FindExport("TheWorld.PersistentLevel.CoverLink_50")));
            KismetHelper.CreateOutputLink(platDestroyedEvent, "Out", modifyCover, 1);
        }

        /// <summary>
        /// Changes all AIFactory spawns in the given file
        /// </summary>
        /// <param name="target"></param>
        /// <param name="package"></param>
        /// <param name="maxNumNewEnemies"></param>
        /// <param name="minClassification"></param>
        /// <param name="maxClassification"></param>
        private static void GenericRandomizeAIFactorySpawns(GameTarget target, IMEPackage package, string[] allowedPawns)
        {
            var aiFactories = package.Exports.Where(x => x.ClassName == "SFXSeqAct_AIFactory").ToList();

            foreach (var aiFactory in aiFactories)
            {
                var seq = SeqTools.GetParentSequence(aiFactory);
                var sequenceObjects = KismetHelper.GetSequenceObjects(seq).OfType<ExportEntry>().ToList();

                // Create the assignment object
                var aiFactoryAssignment = SequenceObjectCreator.CreateSequenceObject(package, "MERSeqAct_AssignAIFactoryData", MERCaches.GlobalCommonLookupCache);
                KismetHelper.AddObjectToSequence(aiFactoryAssignment, seq);
                aiFactoryAssignment.WriteProperty(new ObjectProperty(aiFactory.GetProperty<ObjectProperty>("Factory").ResolveToEntry(package), "Factory"));
                var pawnTypeList = MERSeqTools.CreatePawnList(target, seq, allowedPawns);
                KismetHelper.CreateVariableLink(aiFactoryAssignment, "PawnType(s)", pawnTypeList);

                // Repoint incoming to spawn to this node instead
                MERSeqTools.RedirectInboundLinks(aiFactory, aiFactoryAssignment);

                // Create outlink to continue spawn
                KismetHelper.CreateOutputLink(aiFactoryAssignment, "Out", aiFactory);
            }
        }

        private static void AutomatePlatforming400(IMEPackage platformController)
        {
            // Remove completion state from squad kills as we won't be using that mechanism
            KismetHelper.RemoveOutputLinks(
                platformController.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.CombatA_IncomingPlatform01.SeqAct_Log_2")); //A Platform 01
            KismetHelper.RemoveOutputLinks(
                platformController.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.CombatA_IncomingPlatform02.SeqAct_Log_2")); //A Platform 02
            KismetHelper.RemoveOutputLinks(
                platformController.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.CombatA_IncomingPlatform03.SeqAct_Log_2")); //A Platform 03
            KismetHelper.RemoveOutputLinks(
                platformController.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.CombatA_IncomingPlatform0405.SeqAct_Log_2")); //A Platform 0405 (together)
                                                                                                          // there's final platform with the controls on it. we don't touch it

            // Install delays and hook them up to the complection states
            InstallPlatformAutomation(
                platformController.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.CombatA_IncomingPlatform01.SeqEvent_SequenceActivated_0"),
                platformController.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.CombatA_IncomingPlatform01.SeqAct_FinishSequence_1"),
                1); //01 to 02
            InstallPlatformAutomation(
                platformController.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.CombatA_IncomingPlatform02.SeqEvent_SequenceActivated_0"),
                platformController.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.CombatA_IncomingPlatform02.SeqAct_FinishSequence_1"),
                2); //02 to 03
            InstallPlatformAutomation(
                platformController.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.CombatA_IncomingPlatform03.SeqEvent_SequenceActivated_0"),
                platformController.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.CombatA_IncomingPlatform03.SeqAct_FinishSequence_1"),
                3); //03 to 0405
            InstallPlatformAutomation(
                platformController.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.CombatA_IncomingPlatform0405.SeqEvent_SequenceActivated_0"),
                platformController.FindExport(
                    "TheWorld.PersistentLevel.Main_Sequence.CombatA_IncomingPlatform0405.SeqAct_FinishSequence_0"),
                4); //0405 to 06
        }

        private static void InstallPlatformAutomation(ExportEntry seqActivated, ExportEntry finishSeq, int platIdx)
        {
            var seq =
                seqActivated.GetProperty<ObjectProperty>("ParentSequence")
                    .ResolveToEntry(seqActivated.FileRef) as ExportEntry;

            // Clone a delay object, set timer on it
            var delay = MERSeqTools.CreateRandomDelay(seq, 6, 15 - platIdx);

            // Point start to delay
            KismetHelper.CreateOutputLink(seqActivated, "Out", delay);

            // Point delay to finish
            KismetHelper.CreateOutputLink(delay, "Finished", finishSeq);
        }
        #endregion

        #region EndGm3 Post-Final Fight

        private static void UpdatePostCollectorBase(GameTarget target)
        {
            InstallBorger(target);
        }

        private static void InstallBorger(GameTarget target)
        {
            var packageBin = MEREmbedded.GetEmbeddedPackage(target.Game, "Burger.Delux2go_Setup.pcc");
            var burgerPackage = MEPackageHandler.OpenMEPackageFromStream(packageBin);

            // Update cig to borgar
            var langs = new[] { "INT", "FRA", "DEU", "ITA", "POL" };
            foreach (var lang in langs)
            {
                var endGm3Loc = MERFileSystem.GetPackageFile(target, $"BioP_EndGm3_LOC_{lang}.pcc");
                if (endGm3Loc == null)
                    continue; // User removed localization

                var endGm3PLoc = MERFileSystem.OpenMEPackage(endGm3Loc);

                // Replace cig with borgar
                var cineBurger = endGm3PLoc.FindExport("BioApl_Dec_Cigarette01.Cine_Cig_World");
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, burgerPackage.FindExport(cineBurger.InstancedFullPath), endGm3PLoc, cineBurger, true, new RelinkerOptionsPackage(), out _);

                MERFileSystem.SavePackage(endGm3PLoc);
            }

            // Update collector base in renegade mode, shep dies
            var endGame3 = MERFileSystem.GetPackageFile(target, "BioP_EndGm3.pcc");
            var endGame3P = MERFileSystem.OpenMEPackage(endGame3);

            // 1. Add the burger package
            var burgerMDL = PackageTools.PortExportIntoPackage(target, endGame3P, burgerPackage.FindExport("Edmonton_Burger_Delux2go.Burger_MDL"));

            // 3. Convert the collector base into lunch or possibly early dinner
            // It's early dinner cause that thing will keep you full all night long
            endGame3P.FindExport("TheWorld.PersistentLevel.SkeletalMeshActor_2.SkeletalMeshComponent_18").WriteProperty(new ObjectProperty(burgerMDL.UIndex, "SkeletalMesh"));
            endGame3P.FindExport("TheWorld.PersistentLevel.SkeletalMeshActor_25.SkeletalMeshComponent_18").WriteProperty(new ObjectProperty(burgerMDL.UIndex, "SkeletalMesh"));
            MERFileSystem.SavePackage(endGame3P);
        }


        #endregion

        #region Utility

        private static void AddRandomSpawns(GameTarget target, IMEPackage package, string hookupIFP,
            string[] allowedPawns, int numToSpawn, string squadObjectIFP, float radius,
            IMEPackage sequenceSupportPackage,
            float minSpawnDistance = 0.0f, string spawnSeqName = "EndGm2RandomEnemySpawn", int hookupOutputIdx = 0, float delayMultiplier = 1)
        {

            if (minSpawnDistance > radius)
            {
                Debugger.Break(); // Cannot have swapped radiuses
            }

            var trigger = package.FindExport(hookupIFP);
            if (trigger == null)
            {
                MERLog.Error($"Cannot find random spawn trigger IFP: {hookupIFP} in {package.FilePath}");
                Debugger.Break();
                return;
            }
            var outputName = SeqTools.GetOutlinkNames(trigger)[hookupOutputIdx];
            var sequence = SeqTools.GetParentSequence(trigger);

            List<ExportEntry> bioPawnTypes = new List<ExportEntry>();
            var pawnTypeList = MERSeqTools.CreatePawnList(target, sequence, allowedPawns);
            var endGm2RandomEnemySpawn = sequenceSupportPackage.FindExport(spawnSeqName);
            //var squad = squadObjectIFP != null ? package.FindExport(squadObjectIFP) : MERSeqTools.CreateSquadObject(sequence);
            var squad = package.FindExport(squadObjectIFP);
            var squadActor = squad.GetProperty<ObjectProperty>("ObjValue").ResolveToExport(package);
            squadActor.RemoveProperty("PlayPenVolumes"); // Allow free roam

            for (int i = 0; i < numToSpawn; i++)
            {
                var delay = MERSeqTools.CreateRandomDelay(sequence, 0, numToSpawn * 1.25f * delayMultiplier);
                KismetHelper.CreateOutputLink(trigger, outputName, delay);
                var spawnSeq = MERSeqTools.InstallSequenceStandalone(endGm2RandomEnemySpawn, package, sequence);
                KismetHelper.CreateOutputLink(delay, "Finished", spawnSeq);
                KismetHelper.CreateVariableLink(spawnSeq, "PawnTypes", pawnTypeList);
                KismetHelper.CreateVariableLink(spawnSeq, "Squad", squad);
                KismetHelper.CreateVariableLink(spawnSeq, "Radius", MERSeqTools.CreateFloat(sequence, radius));
                KismetHelper.CreateVariableLink(spawnSeq, "Spawned Flyer", MERSeqTools.CreateObject(sequence, null));

                if (minSpawnDistance > 0f)
                {
                    KismetHelper.CreateVariableLink(spawnSeq, "MinRadius",
                        MERSeqTools.CreateFloat(sequence, minSpawnDistance));
                }
            }
        }

        #endregion

        #region Old or unused
        // Code here was either from ME2R and was changed/cut or was developed and was cut due to bugs/time
        // These are not used but kept here for reference for someone, someday, maybe
        private static void RandomizeStuntHench(GameTarget target)
        {
            var shFile = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, "BioP_EndGm_StuntHench.pcc"));

            var originalTriggerStreams = shFile.Exports.Where(x => x.ClassName == @"BioTriggerStream").ToList();
            var triggerStreamProps = originalTriggerStreams
                .Select(x => x.GetProperty<ArrayProperty<StructProperty>>("StreamingStates"))
                .ToList(); // These are the original streaming states in same order as original
            triggerStreamProps.Shuffle();

            for (int i = 0; i < originalTriggerStreams.Count; i++)
            {
                var oTrigStream = originalTriggerStreams[i];
                var newPropSet = triggerStreamProps.PullFirstItem();
                var trigStreams = oTrigStream.GetProperty<ArrayProperty<StructProperty>>("StreamingStates");
                trigStreams[1].Properties
                    .AddOrReplaceProp(newPropSet[1].Properties.GetProp<ArrayProperty<NameProperty>>("LoadChunkNames"));
                trigStreams[2].Properties.AddOrReplaceProp(newPropSet[2].Properties
                    .GetProp<ArrayProperty<NameProperty>>("VisibleChunkNames"));
                oTrigStream.WriteProperty(trigStreams);
            }

            MERFileSystem.SavePackage(shFile);
        }

        #endregion
    }
}