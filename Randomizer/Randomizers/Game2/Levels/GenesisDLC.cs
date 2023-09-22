using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game2.Levels
{
    class GenesisDLC
    {
        public static bool PerformRandomization(GameTarget target, RandomizationOption option)
        {
            return CompletelyRandomizeAudio(target);
        }

        private static bool CompletelyRandomizeAudio(GameTarget target)
        {
            var g2LocIntF = MERFileSystem.GetPackageFile(target, @"BioD_ProNor_LOC_int.pcc");
            var g2LocIntP = MEPackageHandler.OpenMEPackage(g2LocIntF);

            // Male
            RandomizeAudio2(target, g2LocIntP.FindExport("dlc_dhme1proto_D.dlc_dhme1proto_dlg"));

            // Female
            RandomizeAudio2(target, g2LocIntP.FindExport("dlc_dhme1proto_f_D.dlc_dhme1proto_f_dlg"));

            // Conversation options
            RandomizeStrRefs();

            MERFileSystem.SavePackage(g2LocIntP);
            return true;
        }

        private static void RandomizeStrRefs()
        {
            var strRefs = new[]
            {
                // Male
                386655,
                386656,
                386657,
                386658,
                386662,
                386663,
                386668,
                386669,
                386674,
                386675,
                386687,
                386688,
                386692,
                386693,
                
                // Female
                388082,
                388083,
                388084,
                388088,
                388094,
                388095,
                388102,
                388103,
                388113,
                388114,
                388140,
                388141,
                388149,
                388150,
            };

            var tlks = TLKBuilder.GetOfficialTLKs().ToList();

            foreach (var strRef in strRefs)
            {
                bool installed = false;
                while (!installed)
                {
                    var tlkToUse = tlks.RandomElement();
                    if (tlkToUse.Localization != MELocalization.INT)
                        continue;
                    var nStrRef = tlkToUse.StringRefs.RandomElement();
                    if (string.IsNullOrWhiteSpace(nStrRef.Data))
                        continue;
                    if (nStrRef.Data.Length > 30)
                        continue;
                    if (nStrRef.Data.StartsWith("DLC"))
                        continue;
                    if (nStrRef.Data.Contains("\n"))
                        continue;

                    TLKBuilder.ReplaceString(strRef, nStrRef.Data.TrimEnd('.'), MELocalization.INT);
                    installed = true;
                }
            }
        }

        private static void RandomizeAudio(GameTarget target, IMEPackage package, int topLevelUIndex)
        {
            var audioToChange = package.Exports.Where(x => x.idxLink == topLevelUIndex && x.ClassName == "WwiseStream").ToList();
            var audioSources = MERFileSystem.LoadedFiles.Keys.Where(x => x.Contains("_LOC_INT", StringComparison.InvariantCultureIgnoreCase) && x.Contains("Bio")).ToList();
            foreach (var aExp in audioToChange)
            {
                bool installed = false;
                while (!installed)
                {
                    var rAudioSourceF = audioSources.RandomElement();
                    var rAudioSourceP = MEPackageHandler.OpenMEPackage(MERFileSystem.GetPackageFile(target, rAudioSourceF));
                    // var rAudioSourceP = MEPackageHandler.UnsafePartialLoad(MERFileSystem.GetPackageFile(target, rAudioSourceF), x => x.ClassName == "WwiseStream");
                    var audioOptions = rAudioSourceP.Exports.Where(x => x.ClassName == "WwiseStream").ToList();
                    if (!audioOptions.Any())
                        continue;

                    var audioChoice = audioOptions.RandomElement();
                    WwiseStream ws = ObjectBinary.From<WwiseStream>(aExp);
                    if (ws.DataSize == 0)
                    {
                        // Don't allow unused audio
                        continue;
                    }

                    // Repoint the TLK to match what's going to be said
                    var nTlk = WwiseTools.ExtractTLKIdFromExportName(audioChoice);
                    var oTlk = WwiseTools.ExtractTLKIdFromExportName(aExp);
                    if (nTlk != -1 && oTlk != -1 && !string.IsNullOrWhiteSpace(TLKBuilder.TLKLookupByLang(nTlk, MELocalization.INT)))
                    {
                        TLKBuilder.ReplaceString(oTlk, TLKBuilder.TLKLookupByLang(nTlk, MELocalization.INT));

                        WwiseTools.RepointWwiseStream(audioChoice, aExp);
                        installed = true;
                    }
                }
            }
        }

        private static void RandomizeAudio2(GameTarget target, ExportEntry bioConv)
        {
            var isFemale = bioConv.ObjectName.Instanced.Contains("_f_");
            var conversationSeq = bioConv.GetProperty<ObjectProperty>("MatineeSequence").ResolveToExport(bioConv.FileRef);

            var audioSources = MERFileSystem.LoadedFiles.Keys.Where(x => x.Contains("_LOC_INT", StringComparison.InvariantCultureIgnoreCase) && x.Contains("Bio")).ToList();


            var audioInterps = SeqTools.GetAllSequenceElements(conversationSeq).Where(x => x.ClassName == "InterpData")
                    .OfType<ExportEntry>().ToList();
            foreach (var audioInterp in audioInterps)
            {
                var data = new InterpTools.InterpData(audioInterp);
                var vo = data.InterpGroups[0].Tracks[0];
                var tlkId = vo.Export.GetProperty<IntProperty>("m_nStrRefID").Value; // Data for this needs replaced, if we change it, it will break convo
                var wwiseStream = conversationSeq.FileRef.Exports.FirstOrDefault(x => x.ClassName == "WwiseStream" && x.ObjectName.Instanced.Contains(tlkId.ToString()));
                if (wwiseStream == null)
                {
                    MERLog.Information($@"[GENESIS]: VO WwiseStream for TLK id {tlkId} does not exist, skipping");
                    continue;
                }

                bool installed = false;
                float len = 0;
                while (!installed)
                {
                    var rAudioSourceF = audioSources.RandomElement();
                    // var rAudioSourceP = MEPackageHandler.OpenMEPackage(MERFileSystem.GetPackageFile(target, rAudioSourceF));
                    var rAudioSourceP = MEPackageHandler.UnsafePartialLoad(MERFileSystem.GetPackageFile(target, rAudioSourceF), x => x.ClassName == "WwiseStream");
                    var audioOptions = rAudioSourceP.Exports.Where(x => x.ClassName == "WwiseStream").ToList();
                    if (!audioOptions.Any())
                        continue;

                    var audioChoice = audioOptions.RandomElement();
                    WwiseStream ws = ObjectBinary.From<WwiseStream>(audioChoice);
                    if (ws.DataSize == 0)
                    {
                        // Don't allow unused audio
                        continue;
                    }

                    // Repoint the TLK to match what's going to be said
                    var nTlk = WwiseTools.ExtractTLKIdFromExportName(audioChoice);
                    if (nTlk != -1 && !string.IsNullOrWhiteSpace(TLKBuilder.TLKLookupByLang(nTlk, MELocalization.INT)))
                    {
                        TLKBuilder.ReplaceString(tlkId, TLKBuilder.TLKLookupByLang(nTlk, MELocalization.INT));
                        WwiseTools.RepointWwiseStream(audioChoice, wwiseStream);
                        len = (float)ws.GetAudioInfo().GetLength().TotalSeconds + ThreadSafeRandom.NextFloat(1.7) + 0.2f;
                        installed = true;
                    }
                }
                data.Export.WriteProperty(new FloatProperty(len, "InterpLength"));
            }
        }
    }
}
