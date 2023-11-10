using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Game2.ExportTypes;
using Randomizer.Randomizers.Game2.Misc;
using Randomizer.Randomizers.Handlers;
using Randomizer.Randomizers.Shared;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Randomizers.Game2.Levels
{
    /// <summary>
    /// Randomizer for BioP_Char.pcc
    /// </summary>
    public class CharacterCreator
    {
        private static RandomizationOption SuperRandomOption = new RandomizationOption() { SliderValue = 10 };
        public const string SUBOPTIONKEY_MALESHEP_COLORS = "SUBOPTION_MALESHEP_COLORS";
        public const string SUBOPTIONKEY_CHARCREATOR_NO_COLORS = "SUBOPTION_CHARCREATOR_COLORS";
        public const string SUBOPTIONKEY_CHARCREATOR_ICONIC_PERSISTENCE = "SUBOPTIONKEY_CHARCREATOR_ICONIC_PERSISTENCE";

        public static bool InstallIconicRandomizer(GameTarget target, RandomizationOption option)
        {
            var sfxgame = MERControl.InstallBioMorphFaceRandomizerClasses(target) ?? RSharedSFXGame.GetSFXGame(target);
            ScriptTools.InstallScriptToExport(target, sfxgame.FindExport("SFXSaveGame.LoadMorphHead"), "SFXSaveGame.LoadMorphHead.uc");
            ScriptTools.InstallScriptToExport(target, sfxgame.FindExport("BioSFHandler_NewCharacter.StartGameWithCustomCharacter"), "BioSFHandler_NewCharacter.StartGameWithCustomCharacter.uc");
            MERFileSystem.SavePackage(sfxgame);

            var biop_char = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, "BioP_Char.pcc"));
            // We must first port in the femshep fixer
            using var fsFixer = MEPackageHandler.OpenMEPackageFromStream(MEREmbedded.GetEmbeddedPackage(target.Game, "Headmorph.MERIRFemshepFixer.pcc"), "MERIRFemshepFixer.pcc");
            var mgc = biop_char.FindEntry("MERGameContent") ?? ExportCreator.CreatePackageExport(biop_char, "MERGameContent");
            foreach (var classx in fsFixer.Exports.Where(x => x.IsClass))
            {
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, classx, biop_char, classx.idxLink != 0 ? mgc : null, true, new RelinkerOptionsPackage(), out _);
            }

            // Patch BioP_Char to randomize face on load
            ScriptTools.InstallScriptToExport(target, biop_char.FindExport("SFXGameContent.BioSeqAct_ShowCharacterCreation.Activated"), "BioSeqAct_ShowCharacterCreation.Activated.uc");
            MERFileSystem.SavePackage(biop_char);

            // Set runtime feature flags
            CoalescedHandler.EnableFeatureFlag("bIconicRandomizer");
            CoalescedHandler.EnableFeatureFlag("bIconicRandomizer_Persistent", option.HasSubOptionSelected(SUBOPTIONKEY_CHARCREATOR_ICONIC_PERSISTENCE));
            MERControl.SetVariable("fIconicFaceRandomization", option.SliderValue, CoalesceParseAction.Add);
            return true;
        }

#if ME2R
        public static bool RandomizeIconicFemShep(GameTarget target, RandomizationOption option)
        {
            // LE version
            var files = new[] { @"BIOG_HMF_HED_PROMorph_R.pcc", @"BioP_Char.pcc" };
            foreach (var f in files)
            {
                var headPackage = MERFileSystem.OpenMEPackage(MERFileSystem.GetPackageFile(target, f));
                var shepMDL = headPackage.FindExport("PROSheppard.HMF_HED_PROSheppard_MDL");
                if (shepMDL == null)
                {
                    shepMDL = headPackage.FindExport("BIOG_HMF_HED_PROMorph_R.PROSheppard.HMF_HED_PROSheppard_MDL");
                }

                var objBin = RSharedSkeletalMesh.FuzzSkeleton(shepMDL, option);

                //if (option.HasSubOptionSelected(CharacterCreator.SUBOPTIONKEY_MALESHEP_COLORS))
                {
                    Dictionary<string, CFVector4> vectors = new();
                    Dictionary<string, float> scalars = new();
                    var materials = objBin.Materials;
                    foreach (var mat in materials.Select(x => headPackage.GetUExport(x)))
                    {
                        RMaterialInstance.RandomizeSubMatInst(mat, vectors, scalars);
                    }
                }

                MERFileSystem.SavePackage(headPackage);
            }

            return true;
        }





        public static bool RandomizeIconicMaleShep(GameTarget target, RandomizationOption option)
        {
            var sfxgameP = SFXGame.GetSFXGame(target);
            var shepMDL = sfxgameP.FindExport("BIOG_HMM_HED_PROMorph.Sheppard.HMM_HED_PROSheppard_MDL");
            var objBin = RSharedSkeletalMesh.FuzzSkeleton(shepMDL, option);

            if (option.HasSubOptionSelected(CharacterCreator.SUBOPTIONKEY_MALESHEP_COLORS))
            {
                Dictionary<string, CFVector4> vectors = new();
                Dictionary<string, float> scalars = new();
                var materials = objBin.Materials;
                foreach (var mat in materials.Select(x => sfxgameP.GetUExport(x)))
                {
                    RMaterialInstance.RandomizeSubMatInst(mat, vectors, scalars);
                }
            }

            MERFileSystem.SavePackage(sfxgameP);
            return true;
        }

#endif

        public static bool RandomizePsychProfiles(GameTarget target, RandomizationOption option)
        {
            //Psych Profiles
            string fileContents = MEREmbedded.GetEmbeddedTextAsset("psychprofiles.xml");

            XElement rootElement = XElement.Parse(fileContents);
            var childhoods = rootElement.Descendants("childhood").Where(x => x.Value != "").Select(x => (x.Attribute("name").Value, string.Join("\n", x.Value.Split('\n').Select(s => s.Trim())))).ToList();
            var reputations = rootElement.Descendants("reputation").Where(x => x.Value != "").Select(x => (x.Attribute("name").Value, string.Join("\n", x.Value.Split('\n').Select(s => s.Trim())))).ToList();

            childhoods.Shuffle();
            reputations.Shuffle();

            var backgroundTlkPairs = new List<(int nameId, int descriptionId)>();
            backgroundTlkPairs.Add((45477, 34931)); //Spacer
            backgroundTlkPairs.Add((45508, 34940)); //Earthborn
            backgroundTlkPairs.Add((45478, 34971)); //Colonist
            foreach (var pair in backgroundTlkPairs)
            {
                var childHood = childhoods.PullFirstItem();
                TLKBuilder.ReplaceString(pair.nameId, childHood.Value);
                TLKBuilder.ReplaceString(pair.descriptionId, childHood.Item2.Trim());
            }

            backgroundTlkPairs.Clear();
            backgroundTlkPairs.Add((45482, 34934)); //Sole Survivor
            backgroundTlkPairs.Add((45483, 34936)); //War Hero
            backgroundTlkPairs.Add((45484, 34938)); //Ruthless
            foreach (var pair in backgroundTlkPairs)
            {
                var reputation = reputations.PullFirstItem();
                TLKBuilder.ReplaceString(pair.nameId, reputation.Value);
                TLKBuilder.ReplaceString(pair.descriptionId, reputation.Item2.Trim());
            }
            return true;
        }

        public static bool RandomizeCharacterCreator(GameTarget target, RandomizationOption option)
        {
            var sfxgame = RSharedSFXGame.GetSFXGame(target);
            ScriptTools.InstallScriptToExport(target, sfxgame.FindExport("BioSFHandler_NewCharacter.SelectNextPregeneratedHead"), "BioSFHandler_NewCharacter.SelectNextPregeneratedHead.uc");
            //ScriptTools.InstallScriptToExport(sfxgame.FindExport("BioSFHandler_NewCharacter.ApplyNewCode"), "BioSFHandler_NewCharacter.ApplyNewCode.uc");
            MERFileSystem.SavePackage(sfxgame);

            // MERControl.SetVariable("fCCBioMorphFaceRandomization", 2);


            var biop_charF = MERFileSystem.GetPackageFile(target, @"BioP_Char.pcc");
            var biop_char = MEPackageHandler.OpenMEPackage(biop_charF);
            var maleFrontEndData = biop_char.FindExport("BioChar_Player.FrontEnd.SFXMorphFaceFrontEnd_Male");
            var femaleFrontEndData = biop_char.FindExport("BioChar_Player.FrontEnd.SFXMorphFaceFrontEnd_Female");

            var maleHeadSet = maleFrontEndData.GetProperty<ObjectProperty>("MorphTargetSet").ResolveToExport(biop_char);
            var femaleHeadSet = femaleFrontEndData.GetProperty<ObjectProperty>("MorphTargetSet").ResolveToExport(biop_char);

            var list = new[] { maleHeadSet, femaleHeadSet };
            foreach (var hs in list)
            {
                hs.ObjectName = new NameReference(hs.ObjectName.Name + "_MER", hs.ObjectName.Number);

                var targets = hs.GetProperty<ArrayProperty<ObjectProperty>>("Targets");
                foreach (var t in targets.Select(x => x.ResolveToExport(biop_char)))
                {
                    var bin = ObjectBinary.From<MorphTarget>(t);
                    for (int i = 0; i < bin.MorphLODModels.Length; i++)
                    {
                        for (int j = 0; j < bin.MorphLODModels[i].Vertices.Length; j++)
                        {
                            bin.MorphLODModels[i].Vertices[j].PositionDelta.X *= (float)option.SliderValue;
                            bin.MorphLODModels[i].Vertices[j].PositionDelta.Y *= (float)option.SliderValue;
                            bin.MorphLODModels[i].Vertices[j].PositionDelta.Z *= (float)option.SliderValue;
                        }
                    }

                    for (int i = 0; i < bin.BoneOffsets.Length; i++)
                    {
                        bin.BoneOffsets[i].Offset.X *= (float)option.SliderValue;
                        bin.BoneOffsets[i].Offset.Y *= (float)option.SliderValue;
                        bin.BoneOffsets[i].Offset.Z *= (float)option.SliderValue;
                    }
                    t.WriteBinary(bin);
                }
            }

            var randomizeColors = !option.HasSubOptionSelected(CharacterCreator.SUBOPTIONKEY_CHARCREATOR_NO_COLORS);
            foreach (var export in biop_char.Exports)
            {
                if (export.ClassName == "BioMorphFaceFESliderColour" && randomizeColors)
                {
                    var colors = export.GetProperty<ArrayProperty<StructProperty>>("m_acColours");
                    foreach (var color in colors)
                    {
                        StructTools.RandomizeColor(color, true, .5, 1.5);
                    }
                    export.WriteProperty(colors);
                }
                //else if (export.ClassName == "BioMorphFaceFESliderMorph")
                //{
                // These don't work becuase of the limits in the morph system
                // So all this does is change how much the values change, not the max/min
                //}
                //else if (export.ClassName == "BioMorphFaceFESliderScalar" || export.ClassName == "BioMorphFaceFESliderSetMorph")
                //{
                //    //no idea how to randomize this lol
                //    var floats = export.GetProperty<ArrayProperty<FloatProperty>>("m_afValues");
                //    var minfloat = floats.Min();
                //    var maxfloat = floats.Max();
                //    if (minfloat == maxfloat)
                //    {
                //        if (minfloat == 0)
                //        {
                //            maxfloat = 1;
                //        }
                //        else
                //        {
                //            var vari = minfloat / 2;
                //            maxfloat = ThreadSafeRandom.NextFloat(-vari, vari) + minfloat; //+/- 50%
                //        }

                //    }
                //    foreach (var floatval in floats)
                //    {
                //        floatval.Value = ThreadSafeRandom.NextFloat(minfloat, maxfloat);
                //    }
                //    export.WriteProperty(floats);
                //}
                //else if (export.ClassName == "BioMorphFaceFESliderTexture")
                //{

                //}
            }
            MERFileSystem.SavePackage(biop_char);
            return true;
        }


        private static string unnotchedSliderCodeChars = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <summary>
        /// Builds a map of position => allowable values (as a string)
        /// </summary>
        /// <param name="frontEndData"></param>
        /// <returns></returns>
        private static Dictionary<int, char[]> CalculateCodeMap(ExportEntry frontEndData)
        {
            Dictionary<int, char[]> map = new();
            var props = frontEndData.GetProperties();
            var categories = props.GetProp<ArrayProperty<StructProperty>>("MorphCategories");
            int position = 0;
            foreach (var category in categories)
            {
                foreach (var slider in category.GetProp<ArrayProperty<StructProperty>>("m_aoSliders"))
                {
                    if (!slider.GetProp<BoolProperty>("m_bNotched"))
                    {
                        map[position] = unnotchedSliderCodeChars.ToCharArray();
                    }
                    else
                    {
                        // It's notched
                        map[position] = unnotchedSliderCodeChars.Substring(0, slider.GetProp<IntProperty>("m_iSteps")).ToCharArray();
                    }

                    position++;
                }

            }

            return map;
        }

        private static CoalesceProperty GenerateHeadCode(Dictionary<int, char[]> codeMap, bool female)
        {
            // Doubt this will actually work but whatevers.
            int numChars = female ? 36 : 34;
            var headCode = new char[numChars];
            int i = 0;
            while (i < numChars)
            {
                headCode[i] = codeMap[i].RandomElement();
                i++;
            }

            return new CoalesceProperty(female ? "FemalePregeneratedHeadCodes" : "MalePregeneratedHeadCodes", new CoalesceValue(new string(headCode), CoalesceParseAction.AddUnique));
        }

        private static void randomizeFrontEnd(ExportEntry frontEnd)
        {
            var props = frontEnd.GetProperties();

            //read categories
            var morphCategories = props.GetProp<ArrayProperty<StructProperty>>("MorphCategories");
            var sliders = new Dictionary<string, StructProperty>();
            foreach (var cat in morphCategories)
            {
                var catSliders = cat.GetProp<ArrayProperty<StructProperty>>("m_aoSliders");
                foreach (var cSlider in catSliders)
                {
                    var name = cSlider.GetProp<StrProperty>("m_sName");
                    sliders[name.Value] = cSlider;
                }
            }

            //Default Settings
            var defaultSettings = props.GetProp<ArrayProperty<StructProperty>>("m_aDefaultSettings");
            foreach (var basehead in defaultSettings)
            {
                randomizeBaseHead(basehead, frontEnd, sliders);
            }

            //randomize base heads ?
            var baseHeads = props.GetProp<ArrayProperty<StructProperty>>("m_aBaseHeads");
            foreach (var basehead in baseHeads)
            {
                randomizeBaseHead(basehead, frontEnd, sliders);
            }


            frontEnd.WriteProperties(props);

        }


        private static void randomizeBaseHead(StructProperty basehead, ExportEntry frontEnd, Dictionary<string, StructProperty> sliders)
        {
            var bhSettings = basehead.GetProp<ArrayProperty<StructProperty>>("m_fBaseHeadSettings");
            foreach (var baseSlider in bhSettings)
            {
                var sliderName = baseSlider.GetProp<StrProperty>("m_sSliderName");
                //is slider stepped?
                if (sliderName.Value == "Scar")
                {
                    baseSlider.GetProp<FloatProperty>("m_fValue").Value = 1;
                    continue;
                }
                var slider = sliders[sliderName.Value];
                var notched = slider.GetProp<BoolProperty>("m_bNotched");
                var val = baseSlider.GetProp<FloatProperty>("m_fValue");

                if (notched)
                {
                    //it's indexed
                    var maxIndex = slider.GetProp<IntProperty>("m_iSteps");
                    val.Value = ThreadSafeRandom.Next(maxIndex);
                }
                else
                {
                    //it's variable, we have to look up the m_fRange in the SliderMorph.
                    var sliderDatas = slider.GetProp<ArrayProperty<ObjectProperty>>("m_aoSliderData");
                    if (sliderDatas.Count == 1)
                    {
                        var slDataExport = frontEnd.FileRef.GetUExport(sliderDatas[0].Value);
                        var range = slDataExport.GetProperty<FloatProperty>("m_fRange");
                    }
                    else
                    {
                        // This is just a guess
                        val.Value = ThreadSafeRandom.NextFloat(0, 1f);
                    }

                }
            }
        }
    }
}
