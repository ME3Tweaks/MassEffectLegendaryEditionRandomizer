﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
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

        public static bool RandomizeCharacterCreator(GameTarget target, RandomizationOption option)
        {
            var biop_charF = MERFileSystem.GetPackageFile(target, @"BioP_Char.pcc");
            var biop_char = MEPackageHandler.OpenMEPackage(biop_charF);
            var maleFrontEndData = biop_char.GetUExport(18753);
            var femaleFrontEndData = biop_char.GetUExport(18754);

            var codeMapMale = CalculateCodeMap(maleFrontEndData);
            var codeMapFemale = CalculateCodeMap(femaleFrontEndData);

            var bioUI = CoalescedHandler.GetIniFile("BIOUI.ini");
            var bgr = CoalescedHandler.GetIniFile("BIOGuiResources.ini");
            var charCreatorPCS = bgr.GetOrAddSection("SFXGame.BioSFHandler_PCNewCharacter");
            var charCreatorControllerS = bioUI.GetOrAddSection("SFXGame.BioSFHandler_NewCharacter");

            charCreatorPCS.AddEntry(new CoalesceProperty("MalePregeneratedHeadCodes", new CoalesceValue("CLEAR", CoalesceParseAction.RemoveProperty)));
            charCreatorControllerS.AddEntry(new CoalesceProperty("MalePregeneratedHeadCodes", new CoalesceValue("CLEAR", CoalesceParseAction.RemoveProperty)));

            int numToMake = 20;
            int i = 0;

            // Male: 34 chars
            while (i < numToMake)
            {
                i++;
                charCreatorPCS.AddEntry(GenerateHeadCode(codeMapMale, false));
                charCreatorControllerS.AddEntry(GenerateHeadCode(codeMapMale, false));
            }



            // Female: 36 chars
            //charCreatorPCS.Entries.Add(new DuplicatingIni.IniEntry("")); //blank line
            //charCreatorControllerS.Entries.Add(new DuplicatingIni.IniEntry("")); //blank line

            charCreatorPCS.AddEntry(new CoalesceProperty("FemalePregeneratedHeadCodes", new CoalesceValue("CLEAR", CoalesceParseAction.RemoveProperty)));
            charCreatorControllerS.AddEntry(new CoalesceProperty("FemalePregeneratedHeadCodes", new CoalesceValue("CLEAR", CoalesceParseAction.RemoveProperty)));

            i = 0;
            while (i < numToMake)
            {
                i++;
                charCreatorPCS.AddEntry(GenerateHeadCode(codeMapFemale, true));
                charCreatorControllerS.AddEntry(GenerateHeadCode(codeMapFemale, true));
            }


            randomizeFrontEnd(maleFrontEndData);
            randomizeFrontEnd(femaleFrontEndData);


            //Copy the final skeleton from female into male.
            var femBase = biop_char.GetUExport(3480);
            var maleBase = biop_char.GetUExport(3481);
            maleBase.WriteProperty(femBase.GetProperty<ArrayProperty<StructProperty>>("m_aFinalSkeleton"));

            var randomizeColors = !option.HasSubOptionSelected(CharacterCreator.SUBOPTIONKEY_CHARCREATOR_NO_COLORS);

            foreach (var export in biop_char.Exports)
            {
                if (export.ClassName == "BioMorphFace" && !export.ObjectName.Name.Contains("Iconic"))
                {
                    RBioMorphFace.RandomizeExportNonHench(target, export, SuperRandomOption); //.3 default
                }
                else if (export.ClassName == "MorphTarget")
                {
                    if (
                         export.ObjectName.Name.StartsWith("jaw") || export.ObjectName.Name.StartsWith("mouth")
                                                                  || export.ObjectName.Name.StartsWith("eye")
                                                                  || export.ObjectName.Name.StartsWith("cheek")
                                                                  || export.ObjectName.Name.StartsWith("nose")
                                                                  || export.ObjectName.Name.StartsWith("teeth")
                        )
                    {
                        RSharedMorphTarget.RandomizeExport(export, option);
                    }
                }
                else if (export.ClassName == "BioMorphFaceFESliderColour" && randomizeColors)
                {
                    var colors = export.GetProperty<ArrayProperty<StructProperty>>("m_acColours");
                    foreach (var color in colors)
                    {
                        StructTools.RandomizeColor(color, true, .5, 1.5);
                    }
                    export.WriteProperty(colors);
                }
                else if (export.ClassName == "BioMorphFaceFESliderMorph")
                {
                    // These don't work becuase of the limits in the morph system
                    // So all this does is change how much the values change, not the max/min
                }
                else if (export.ClassName == "BioMorphFaceFESliderScalar" || export.ClassName == "BioMorphFaceFESliderSetMorph")
                {
                    //no idea how to randomize this lol
                    var floats = export.GetProperty<ArrayProperty<FloatProperty>>("m_afValues");
                    var minfloat = floats.Min();
                    var maxfloat = floats.Max();
                    if (minfloat == maxfloat)
                    {
                        if (minfloat == 0)
                        {
                            maxfloat = 1;
                        }
                        else
                        {
                            var vari = minfloat / 2;
                            maxfloat = ThreadSafeRandom.NextFloat(-vari, vari) + minfloat; //+/- 50%
                        }

                    }
                    foreach (var floatval in floats)
                    {
                        floatval.Value = ThreadSafeRandom.NextFloat(minfloat, maxfloat);
                    }
                    export.WriteProperty(floats);
                }
                else if (export.ClassName == "BioMorphFaceFESliderTexture")
                {

                }
            }
            MERFileSystem.SavePackage(biop_char);
            return true;
        }


        private static string unnotchedSliderCodeChars = "123456789ABCDEFGHIJKLMNOPQRSTUVW";

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
                        val.Value = ThreadSafeRandom.NextFloat(0, range * 100);
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
