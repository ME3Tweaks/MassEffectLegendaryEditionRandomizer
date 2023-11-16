#if DEBUG
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.TLK.ME2ME3;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using ME3TweaksModManager.modmanager.starterkit;
using Randomizer.MER;
using Randomizer.Randomizers.Utility;
using WinCopies.Util;

namespace Randomizer.Randomizers.Game1.GalaxyMap
{
    public class GalaxyMapRandomizerDebug
    {
        public static void BuildSWFPackage(object sender, DoWorkEventArgs doWorkEventArgs)
        {
#if DEBUG
            var option = doWorkEventArgs.Argument as RandomizationOption;
            option.ProgressIndeterminate = false;
            var blankSwfFile = @"G:\My Drive\Mass Effect Legendary Modding\LERandomizer\LE1\GalaxyMapImages\BlankGalaxyMapImage.gfx";
            var blankStream = new MemoryStream(File.ReadAllBytes(blankSwfFile));
            var destPackageF = @"B:\UserProfile\source\repos\ME2Randomizer\Randomizer\Randomizers\Game1\Assets\Binary\Packages\LE1\Always_GalaxyMap\GUI_SF_GalaxyMapImages_LE1R.pcc";
            var destPackageP = MEPackageHandler.CreateAndOpenPackage(destPackageF, MEGame.LE1);

            var packageRoot = ExportCreator.CreatePackageExport(destPackageP, "GUI_SF_GalaxyMapImages_LE1R");

            var sourceFilesFolder = @"G:\My Drive\Mass Effect Legendary Modding\LERandomizer\LE1\GalaxyMapImages\processed";
            var sourceFiles = Directory.GetFiles(sourceFilesFolder, "*.jpg", SearchOption.AllDirectories);

            Dictionary<ExportEntry, string> swfTextureMapping = new Dictionary<ExportEntry, string>();

            // Install SWFs first
            option.CurrentOperation = "Installing SWFs";
            option.ProgressValue = 0;
            option.ProgressMax = sourceFiles.Length;
            foreach (var sourceFile in sourceFiles)
            {
                var group = Directory.GetParent(sourceFile).Name;
                var swfName = $"{group}_{Path.GetFileNameWithoutExtension(sourceFile)}";
                var textureName = $"{swfName}_I1";
                var swfExport = ExportCreator.CreateExport(destPackageP, swfName, "GFxMovieInfo", indexed: false);

                // Set up the SWF
                blankStream.Seek(0, SeekOrigin.Begin);
                MemoryStream dataStream = new MemoryStream();
                blankStream.CopyToEx(dataStream, 0x20); // SWFName Offset
                blankStream.ReadByte(); // Skip length 0 in the original file

                dataStream.WriteByte((byte)swfName.Length); // Write SWF Name Len (1 byte)
                dataStream.WriteStringASCII(swfName); // Write SWF Name

                blankStream.CopyToEx(dataStream, 0x18); // Copy bytes 0x21 - 0x39
                blankStream.ReadByte();

                dataStream.WriteByte((byte)(textureName.Length + 4)); // Write SWF Texture Name Len (1 byte, +4 for .tga)
                dataStream.WriteStringASCII(textureName + ".tga"); // Write SWF Texture Name

                blankStream.CopyTo(dataStream); // Copy the rest of the stream.

                // Update data lengths.
                var offset = (short)(swfName.Length - 9); // This is how much the length changed

                // SWF Size
                dataStream.Seek(4, SeekOrigin.Begin);
                dataStream.WriteInt32((int)dataStream.Length);

                // Exporter Info Tag - this is a fixed value
                dataStream.Seek(0x15, SeekOrigin.Begin);
                var len = dataStream.ReadInt16();
                len += offset;
                dataStream.Seek(-2, SeekOrigin.Current);
                dataStream.WriteInt16(len);

                // DefineExternalImage2 Tag
                dataStream.Seek(0x35 + offset, SeekOrigin.Begin);
                len = dataStream.ReadInt16();
                len += offset; // galMap001 is 9 chars long, so adjust it
                dataStream.Seek(-2, SeekOrigin.Current);
                dataStream.WriteInt16(len);

                swfExport.WriteProperty(new ImmutableByteArrayProperty(dataStream.ToArray(), "RawData"));

                swfTextureMapping[swfExport] = sourceFile;
                option.ProgressValue++;
            }

            // Install SWF textures
            option.CurrentOperation = "Generating SWF texture data";
            option.ProgressValue = 0;
            option.ProgressMax = sourceFiles.Length;
            foreach (var mapping in swfTextureMapping)
            {
                var textureName = $"{mapping.Key.ObjectName.Name}_I1";
                var textureExport = ExportCreator.CreateExport(destPackageP, textureName, "Texture2D", mapping.Key.Parent, indexed: false);
                System.Drawing.Image img = System.Drawing.Image.FromFile(mapping.Value);

                PropertyCollection properties = new PropertyCollection()
                {
                    new IntProperty(img.Width, "SizeX"),
                    new IntProperty(img.Height, "SizeY"),
                    new IntProperty(1024, "OriginalSizeX"),
                    new IntProperty(512, "OriginalSizeY"),
                    new IntProperty(9, "MipTailBaseIdx"),
                    new EnumProperty("PF_DXT1","EPixelFormat", MERFileSystem.Game,"Format"),
                    new BoolProperty(false, "SRGB"),
                    new BoolProperty(true, "CompressionNoAlpha"),
                    new BoolProperty(true, "CompressionNoMipmaps"),
                    new BoolProperty(true, "NeverStream"),
                    new EnumProperty("TEXTUREGROUP_UI","TextureGroup", MERFileSystem.Game,"LODGroup"),
                };
                textureExport.WriteProperties(properties);

                var data = File.ReadAllBytes(mapping.Value);
                var textureData = Texture2D.CreateSingleMip(data, Image.ImageFormat.JPEG, PixelFormat.DXT1);

                var tex2d = new UTexture2D();
                tex2d.Mips = new List<UTexture2D.Texture2DMipMap>(new[]
                {
                    new UTexture2D.Texture2DMipMap()
                    {
                        CompressedSize = textureData.Length,
                        DataOffset = 0,
                        Mip = textureData,
                        SizeX = img.Width,
                        SizeY = img.Height,
                        StorageType = StorageTypes.pccUnc,
                        UncompressedSize = textureData.Length
                    }
                });
                tex2d.TextureGuid = Guid.NewGuid(); // Not sure this matters.
                textureExport.WriteBinary(tex2d);

                // Add references to SWF
                var refs = new ArrayProperty<ObjectProperty>("References");
                refs.Add(new ObjectProperty(textureExport));
                mapping.Key.WriteProperty(refs);

                option.ProgressValue++;
            }

            // Add object referencer
            // Dunno why it's in StarterKitAddins...
            var objRef = StarterKitAddins.CreateObjectReferencer(destPackageP, false);
            var objRefs = new ArrayProperty<ObjectProperty>("ReferencedObjects");
            objRefs.AddRange(destPackageP.Exports.Where(x => x.ClassName == "GFxMovieInfo").Select(x => new ObjectProperty(x)));
            objRef.WriteProperty(objRefs);

            destPackageP.Save();
#endif
        }

        static string FormatXml(string xml)
        {
            try
            {
                XDocument doc = XDocument.Parse(xml);
                return doc.ToString();
            }
            catch (Exception)
            {
                // Handle and throw if fatal exception here; don't just ignore them
                return xml;
            }
        }

        [Conditional("DEBUG")]
        public static void DumpPlanetTexts(ExportEntry export, ITalkFile tf)
        {
            Bio2DA planets = new Bio2DA(export);
            var planetInfos = new List<RandomizedPlanetInfo>();

            int nameRefcolumn = planets.GetColumnIndexByName("Name");
            int descColumn = planets.GetColumnIndexByName("Description");

            for (int i = 0; i < planets.RowNames.Count; i++)
            {
                RandomizedPlanetInfo rpi = new RandomizedPlanetInfo();
                rpi.PlanetName = tf.FindDataById(planets[i, nameRefcolumn].IntValue);

                var descCell = planets[i, descColumn];
                if (descCell != null)
                {
                    rpi.PlanetDescription = tf.FindDataById(planets[i, 7].IntValue);
                }

                rpi.RowID = i;
                planetInfos.Add(rpi);
            }

            using (StringWriter writer = new StringWriter())
            {
                XmlSerializer xs = new XmlSerializer(typeof(List<RandomizedPlanetInfo>));
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = true;

                XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
                namespaces.Add(string.Empty, string.Empty);

                XmlWriter xmlWriter = XmlWriter.Create(writer, settings);
                xs.Serialize(xmlWriter, planetInfos, namespaces);

                File.WriteAllText(@"C:\users\mgame\desktop\planetinfo.xml", FormatXml(writer.ToString()));
            }
        }
    }
}
#endif