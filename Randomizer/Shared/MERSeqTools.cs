using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Media;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using ME3TweaksCore.Targets;
using Randomizer.MER;
using Randomizer.Randomizers.Game2.Enemy;
using Randomizer.Randomizers.Utility;

namespace Randomizer.Shared
{
    class MERSeqTools
    {
        /// <summary>
        /// Installs a random switch into the sequence with the specified number of outlinks.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="sequence"></param>
        /// <param name="numLinks"></param>
        /// <returns></returns>
        public static ExportEntry InstallRandomSwitchIntoSequence(GameTarget target, ExportEntry sequence, int numLinks)
        {
            var nSwitch = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqAct_RandomSwitch", new MERPackageCache(target, MERCaches.GlobalCommonLookupCache, true));
            KismetHelper.AddObjectToSequence(nSwitch, sequence);
            var properties = nSwitch.GetProperties();
            //    var packageBin = MEREmbedded.GetEmbeddedPackage(target.Game, "PremadeSeqObjs.pcc");
            //    var premadeObjsP = MEPackageHandler.OpenMEPackageFromStream(packageBin);

            //    // 1. Add the switch object and link it to the sequence
            //    var nSwitch = PackageTools.PortExportIntoPackage(target, sequence.FileRef, premadeObjsP.FindExport("SeqAct_RandomSwitch_0"), sequence.UIndex, false, true);
            //    KismetHelper.AddObjectToSequence(nSwitch, sequence);

            // 2. Generate the output links array. We will refresh the properties
            // with new structs so we don't have to make a copy constructor
            var olinks = nSwitch.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            while (olinks.Count < numLinks)
            {
                olinks.Add(olinks[0]); // Just add a bunch of the first link
            }

            nSwitch.WriteProperty(olinks);

            // Reload the olinks with unique structs now
            olinks = nSwitch.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            for (int i = 0; i < numLinks; i++)
            {
                olinks[i].GetProp<StrProperty>("LinkDesc").Value = $"Link {i + 1}";
            }

            nSwitch.WriteProperty(olinks);
            nSwitch.WriteProperty(new IntProperty(numLinks, "LinkCount"));

            return nSwitch;
        }

        /// <summary>
        /// Adds a new switch output link. Returns the link number (NOT THE INDEX!!) that you can use 'Link X' as the description for
        /// </summary>
        /// <param name="existingSwitch"></param>
        /// <returns></returns>
        public static int AddRandomSwitchOutput(ExportEntry existingSwitch)
        {
            var outputLinks = existingSwitch.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            int numExistingOutputLinks = 0;

            if (outputLinks == null)
            {
                outputLinks = new ArrayProperty<StructProperty>(new List<StructProperty>(), "OutputLinks");
                existingSwitch.WriteProperty(outputLinks); // Add outputlinks
            }
            else
            {
                numExistingOutputLinks = outputLinks.Count;
            }

            KismetHelper.CreateNewOutputLink(existingSwitch, $"Link {numExistingOutputLinks + 1}", null);
            existingSwitch.WriteProperty(new IntProperty(numExistingOutputLinks + 1, "LinkCount")); // Update the link count
            return numExistingOutputLinks + 1;
        }

        /// <summary>
        /// Changes a single output link to a new target and commits the properties.
        /// </summary>
        /// <param name="export">Export to operate on</param>
        /// <param name="outputLinkIndex">The index of the item in 'OutputLinks'</param>
        /// <param name="linksIndex">The index of the item in the Links array</param>
        /// <param name="newTarget">The UIndex of the new target</param>
        public static void ChangeOutlink(ExportEntry export, int outputLinkIndex, int linksIndex, int newTarget)
        {
            var props = export.GetProperties();
            ChangeOutlink(props, outputLinkIndex, linksIndex, newTarget);
            export.WriteProperties(props);
        }

        /// <summary>
        /// Changes a single output link to a new target.
        /// </summary>
        /// <param name="export">The export properties list to operate on</param>
        /// <param name="outputLinkIndex">The index of the item in 'OutputLinks'</param>
        /// <param name="linksIndex">The index of the item in the Links array</param>
        /// <param name="newTarget">The UIndex of the new target</param>
        public static void ChangeOutlink(PropertyCollection props, int outputLinkIndex, int linksIndex, int newTarget)
        {
            props.GetProp<ArrayProperty<StructProperty>>("OutputLinks")[outputLinkIndex].GetProp<ArrayProperty<StructProperty>>("Links")[linksIndex].GetProp<ObjectProperty>("LinkedOp").Value = newTarget;
        }

        /// <summary>
        /// Finds variable connections that come to this node.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="sequenceElements"></param>
        /// <returns></returns>
        public static List<ExportEntry> FindVariableConnectionsToNode(ExportEntry node, List<ExportEntry> sequenceElements)
        {
            List<ExportEntry> referencingNodes = new List<ExportEntry>();

            foreach (var seqObj in sequenceElements)
            {
                if (seqObj == node) continue; // Skip node pointing to itself
                var linkSet = SeqTools.GetVariableLinksOfNode(seqObj);
                if (linkSet.Any(x => x.LinkedNodes.Any(y => y == node)))
                {
                    referencingNodes.Add(seqObj);
                }
            }

            return referencingNodes.Distinct().ToList();
        }

        /// <summary>
        /// Clones basic data type objects. Do not use with complicated types.
        /// </summary>
        /// <param name="itemToClone"></param>
        /// <returns></returns>
        public static ExportEntry CloneBasicSequenceObject(ExportEntry itemToClone)
        {
            ExportEntry exp = EntryCloner.CloneEntry(itemToClone);
            //needs to have the same index to work properly
            if (exp.ClassName == "SeqVar_External")
            {
                // Probably shouldn't clone these...
                exp.indexValue = itemToClone.indexValue;
            }

            itemToClone.FileRef.AddExport(exp);

            var sequence = itemToClone.GetProperty<ObjectProperty>("ParentSequence").ResolveToEntry(itemToClone.FileRef) as ExportEntry;
            KismetHelper.AddObjectToSequence(exp, sequence);
            return exp;
        }


        public static void WriteOriginator(ExportEntry export, IEntry originator)
        {
            export.WriteProperty(new ObjectProperty(originator.UIndex, "Originator"));
        }

        public static void WriteObjValue(ExportEntry export, IEntry objValue)
        {
            export.WriteProperty(new ObjectProperty(objValue.UIndex, "ObjValue"));
        }

        public static void PrintVarLinkInfo(List<SeqTools.VarLinkInfo> seqLinks)
        {
            foreach (var link in seqLinks)
            {
                Debug.WriteLine($"VarLink {link.LinkDesc}, expected type: {link.ExpectedTypeName}");
                foreach (var linkedNode in link.LinkedNodes.OfType<ExportEntry>())
                {
                    var findTag = linkedNode.GetProperty<StrProperty>("m_sObjectTagToFind");
                    var objValue = linkedNode.GetProperty<ObjectProperty>("ObjValue");
                    Debug.WriteLine($"   {linkedNode.UIndex} {linkedNode.ObjectName.Instanced} {findTag?.Value} {objValue?.ResolveToEntry(linkedNode.FileRef).ObjectName.Instanced}");
                }
            }
        }

        /// <summary>
        /// Is the specified node assigned to a subclass of the specified type?
        /// </summary>
        /// <param name="sequenceObj">The object that we are checking that should be ignored for references (like conversation start)</param>
        /// <param name="vlNode">The node to check</param>
        /// <param name="sequenceElements">List of nodes in the sequence</param>
        /// <returns></returns>
        public static bool IsAssignedClassType(ExportEntry sequenceObj, ExportEntry vlNode, List<ExportEntry> sequenceElements, string rootClass)
        {
            var inboundConnections = SeqTools.FindVariableConnectionsToNode(vlNode, sequenceElements);
            foreach (var sequenceObject in inboundConnections)
            {
                if (sequenceObject == sequenceObj)
                    continue; // Obviously we reference this node

                // Is this a 'SetObject' that is assigning the value?
                if (sequenceObject.InheritsFrom("SequenceAction") && sequenceObject.ClassName == "SeqAct_SetObject" && sequenceObject != sequenceObj)
                {
                    //check if target is my node
                    var referencingVarLinks = SeqTools.GetVariableLinksOfNode(sequenceObject);
                    var targetLink = referencingVarLinks.FirstOrDefault(x => x.LinkDesc == "Target"); // What is the target node?
                    if (targetLink != null)
                    {
                        //see if target is node we are investigating for setting.
                        foreach (var potentialTarget in targetLink.LinkedNodes.OfType<ExportEntry>())
                        {
                            if (potentialTarget == vlNode)
                            {
                                // There's a 'SetObject' with Target of the attached variable to our interp cutscene
                                // That means something is 'setting' the value of this
                                // We need to inspect what it is to see if we can shuffle it

                                //Debug.WriteLine("Found a setobject to variable linked item on a sequence");

                                //See what value this is set to. If it inherits from BioPawn we can use it in the shuffling.
                                var pointedAtValueLink = referencingVarLinks.FirstOrDefault(x => x.LinkDesc == "Value");
                                if (pointedAtValueLink != null && pointedAtValueLink.LinkedNodes.Count == 1) // Only 1 item being set. More is too complicated
                                {
                                    var linkedNode = pointedAtValueLink.LinkedNodes[0] as ExportEntry;
                                    var linkedNodeType = linkedNode.GetProperty<ObjectProperty>("ObjValue");
                                    if (linkedNodeType != null)
                                    {
                                        var linkedNodeData = sequenceObj.FileRef.GetUExport(linkedNodeType.Value);
                                        if (linkedNodeData.IsA(rootClass))
                                        {
                                            //We can shuffle this item.

                                            // We write the property to the node so if it's not assigned at runtime (like on gender check) it still can show something.
                                            // Cutscene will still be goofed up but will instead show something instead of nothing

                                            linkedNode.WriteProperty(linkedNodeType);

                                            //Debug.WriteLine("Adding shuffle item: " + objRef.Value);
                                            // Original value below was 'linkedNode' which i'm pretty sure is the value that would be assigned, not the actual object that holds that value oncea assigned
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the next connected node from the specified kismet node, as the first outlink Does not do any error handling!
        /// </summary>
        /// <param name="node"></param>
        /// <param name="outputLinkIdx"></param>
        /// <returns></returns>
        public static ExportEntry GetNextNode(ExportEntry node, int outputLinkIdx)
        {
            return SeqTools.GetOutboundLinksOfNode(node)[outputLinkIdx][0].LinkedOp as ExportEntry;
        }

        public static ExportEntry FindSequenceObjectByClassAndPosition(ExportEntry sequence, string className, int posX = int.MinValue, int posY = int.MinValue)
        {
            var seqObjs = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects")
                .Select(x => x.ResolveToEntry(sequence.FileRef)).OfType<ExportEntry>().ToList();
            return FindSequenceObjectByClassAndPosition(seqObjs, className, posX, posY);
        }

        /// <summary>
        /// Removes all links to other sequence objects from the given object.
        /// Use the optional parameters to specify which types of links can be removed.
        /// </summary>
        /// <param name="export">Sequence object to remove all links from</param>
        /// <param name="outlinks">If true, output links will be removed. Default: True</param>
        /// <param name="variablelinks">If true, variable links will be removed. Default: True</param>
        /// <param name="eventlinks">If true, event links will be removed. Default: True</param>
        public static void RemoveAllNamedOutputLinks(ExportEntry export, string linkName)
        {
            var props = export.GetProperties();
            var outLinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
            if (outLinksProp != null)
            {
                foreach (var prop in outLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc")?.Value == linkName)
                    {
                        prop.GetProp<ArrayProperty<StructProperty>>("Links").Clear();
                    }
                }
            }
            export.WriteProperties(props);
        }

        public static ExportEntry FindSequenceObjectByClassAndPosition(List<ExportEntry> seqObjs, string className, int posX = int.MinValue, int posY = int.MinValue)
        {
            seqObjs = seqObjs.Where(x => x.ClassName == className).ToList();
            foreach (var obj in seqObjs)
            {
                if (posX != int.MinValue && posY != int.MinValue)
                {
                    var props = obj.GetProperties();
                    var foundPosX = props.GetProp<IntProperty>("ObjPosX")?.Value;
                    var foundPosY = props.GetProp<IntProperty>("ObjPosY")?.Value;
                    if (foundPosX != null && foundPosY != null &&
                        foundPosX == posX && foundPosY == posY)
                    {
                        return obj;
                    }
                }
                else if (seqObjs.Count == 1)
                {
                    return obj; // First object
                }
                else
                {
                    throw new Exception($"COULD NOT FIND OBJECT OF TYPE {className}");
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a new sequence object in the given sequence
        /// </summary>
        /// <param name="SelectedSequence"></param>
        /// <param name="info"></param>
        public static void CreateNewObject(ExportEntry SelectedSequence, ClassInfo info)
        {
            if (SelectedSequence == null)
            {
                return;
            }

            IEntry classEntry;
            if (SelectedSequence.FileRef.Exports.Any(exp => exp.ObjectName == info.ClassName) || SelectedSequence.FileRef.Imports.Any(imp => imp.ObjectName == info.ClassName) ||
                GlobalUnrealObjectInfo.GetClassOrStructInfo(SelectedSequence.FileRef.Game, info.ClassName) is { } classInfo && EntryImporter.IsSafeToImportFrom(classInfo.pccPath, SelectedSequence.FileRef.Game, SelectedSequence.FileRef.FilePath))
            {
                var rop = new RelinkerOptionsPackage();
                classEntry = EntryImporter.EnsureClassIsInFile(SelectedSequence.FileRef, info.ClassName, rop);
            }
            else
            {
                classEntry = EntryImporter.EnsureClassIsInFile(SelectedSequence.FileRef, info.ClassName, new RelinkerOptionsPackage());
            }
            if (classEntry is null)
            {
                return;
            }
            var packageCache = new PackageCache { AlwaysOpenFromDisk = false };
            packageCache.InsertIntoCache(SelectedSequence.FileRef);
            var newSeqObj = new ExportEntry(SelectedSequence.FileRef, SelectedSequence, SelectedSequence.FileRef.GetNextIndexedName(info.ClassName), properties: SequenceObjectCreator.GetSequenceObjectDefaults(SelectedSequence.FileRef, info, packageCache))
            {
                Class = classEntry,
            };
            newSeqObj.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;
            SelectedSequence.FileRef.AddExport(newSeqObj);
            KismetHelper.AddObjectToSequence(newSeqObj, SelectedSequence, true);
        }

        /// <summary>
        /// Installs a sequence with no inputs. The sequence should have its own events to trigger itself
        /// </summary>
        public static ExportEntry InstallSequenceStandalone(ExportEntry sourceSequence, IMEPackage targetPackage, ExportEntry parentSequence = null)
        {
            parentSequence ??= targetPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence");
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceSequence, targetPackage, parentSequence, true, new RelinkerOptionsPackage(), out var newUiSeq);
            var expCount = targetPackage.Exports.Count(x => x.InstancedFullPath == newUiSeq.InstancedFullPath);
            if (expCount > 1)
            {
                // update the index
                newUiSeq.ObjectName = targetPackage.GetNextIndexedName(sourceSequence.ObjectName.Name);
            }

            KismetHelper.AddObjectToSequence(newUiSeq as ExportEntry, parentSequence);
            return newUiSeq as ExportEntry;
        }

        /// <summary>
        /// Adds a new delay object to a sequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        public static ExportEntry CreateDelay(ExportEntry sequence, float delay)
        {
            var newDelay = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqAct_Delay", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(newDelay, sequence);
            newDelay.WriteProperty(new FloatProperty(delay, "Duration"));
            return newDelay;
        }

#if __GAME2__
        public static ExportEntry InstallMakeItCreepySingle(ExportEntry sourcePawnObj, ExportEntry destPawnObj)
        {
            var sequence = SeqTools.GetParentSequence(sourcePawnObj);
            var creepyPrefab = MEPackageHandler.OpenMEPackageFromStream(MEREmbedded.GetEmbeddedPackage(MEGame.LE2, "SeqPrefabs.MakeItCreepy.pcc"), @"MakeItCreepy.pcc");
            var creepySeq = creepyPrefab.FindExport("MakeItCreepyInput");

            var newSeq = MERSeqTools.InstallSequenceStandalone(creepySeq, sequence.FileRef, sequence);
            KismetHelper.CreateVariableLink(newSeq, "SourceActor", sourcePawnObj);
            KismetHelper.CreateVariableLink(newSeq, "TargetActor", destPawnObj);

            return newSeq;
        }
#endif
        /// <summary>
        /// Creates a player object in the given sequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="returnsPawns"></param>
        /// <returns></returns>
        public static ExportEntry CreatePlayerObject(ExportEntry sequence, bool returnsPawns)
        {
            var player = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_Player", MERCaches.GlobalCommonLookupCache);
            if (returnsPawns)
            {
                player.WriteProperty(new BoolProperty(true, "bReturnPawns"));
            }
            KismetHelper.AddObjectToSequence(player, sequence);
            return player;
        }

        /// <summary>
        /// Creates a SeqAct_ConsoleCommand that executes the command on a player object
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="consoleCommand"></param>
        /// <returns></returns>
        public static ExportEntry CreateConsoleCommandObject(ExportEntry sequence, string consoleCommand)
        {
            var player = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_Player");
            var consoleCommandObj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqAct_ConsoleCommand");
            var ap = new ArrayProperty<StrProperty>("Commands");
            ap.Add(consoleCommand);
            consoleCommandObj.WriteProperty(ap);
            KismetHelper.CreateVariableLink(consoleCommandObj, "Target", player);
            KismetHelper.AddObjectsToSequence(sequence, false, player, consoleCommandObj);
            return consoleCommandObj;
        }

        /// <summary>
        /// Creates a new SeqAct_ActivateRemoteEvent with the specified event name
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="eventName"></param>
        /// <returns></returns>
        public static ExportEntry CreateActivateRemoteEvent(ExportEntry sequence, string eventName)
        {
            var rmEvt = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqAct_ActivateRemoteEvent", MERCaches.GlobalCommonLookupCache);
            rmEvt.WriteProperty(new NameProperty(eventName, "EventName"));
            KismetHelper.AddObjectsToSequence(sequence, false, rmEvt);
            return rmEvt;
        }

        /// <summary>
        /// Installs a sequence with an output. The input must be linked separately.
        /// </summary>
        /// <param name="sourceSequence"></param>
        /// <param name="targetPackage"></param>
        /// <param name="parentSequence"></param>
        /// <param name="linkedOp"></param>
        /// <param name="linkedInputIdx"></param>
        /// <returns></returns>
        public static ExportEntry InstallSequenceChained(ExportEntry sourceSequence, IMEPackage targetPackage, ExportEntry parentSequence, ExportEntry linkedOp, int linkedInputIdx)
        {
            parentSequence ??= targetPackage.FindExport("TheWorld.PersistentLevel.Main_Sequence");
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceSequence, targetPackage, parentSequence, true, new RelinkerOptionsPackage(), out var newUiSeq);
            var expCount = targetPackage.Exports.Count(x => x.InstancedFullPath == newUiSeq.InstancedFullPath);
            if (expCount > 1)
            {
                // update the index
                newUiSeq.ObjectName = targetPackage.GetNextIndexedName(sourceSequence.ObjectName.Name);
            }

            KismetHelper.AddObjectToSequence(newUiSeq as ExportEntry, parentSequence);
            KismetHelper.CreateOutputLink(newUiSeq as ExportEntry, "Out", linkedOp, linkedInputIdx);
            return newUiSeq as ExportEntry;
        }

        /// <summary>
        /// Returns the first linked node - on a proper seqact_interp this will be InterpData. This code is real bad
        /// so hope I don't regret this later
        /// </summary>
        /// <param name="seqActInterp"></param>
        /// <returns></returns>
        public static ExportEntry GetInterpData(ExportEntry seqActInterp)
        {
            var links = SeqTools.GetVariableLinksOfNode(seqActInterp);
            return links[0].LinkedNodes[0] as ExportEntry;
        }

        /// <summary>
        /// Makes a new SeqVar_ObjectList in the given sequence with the given values in ObjList.
        /// </summary>
        /// <param name="objects"></param>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public static ExportEntry CreateSeqVarList(ExportEntry sequence, params ExportEntry[] objects)
        {
            var list = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_ObjectList", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(list, sequence);

            ArrayProperty<ObjectProperty> objList = new ArrayProperty<ObjectProperty>("ObjList");
            objList.ReplaceAll(objects.Select(x => new ObjectProperty(x)));
            list.WriteProperty(objList);

            return list;
        }

        /// <summary>
        /// Creates a new hostile squad and returns its object. Optionally you can pass in a squad actor and it will use that instead
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public static ExportEntry CreateNewSquadObject(ExportEntry sequence, string tag = null, ExportEntry squadActor = null)
        {
            var squadObj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_Object", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(squadObj, sequence);

            if (squadActor == null)
            {
                squadActor = ExportCreator.CreateExport(sequence.FileRef, "BioSquadCombat", "BioSquadCombat",
                    sequence.FileRef.GetLevel());
                squadActor.WriteProperty(new IntProperty(2, "ObjVersion")); // Not sure this is required - LE2
                squadActor.WriteProperty(new NameProperty(tag ?? "BioSquadCombat", "Tag"));
                squadActor.FileRef.AddToLevelActorsIfNotThere(squadActor);
            }

            squadObj.WriteProperty(new ObjectProperty(squadActor, "ObjValue"));


            return squadObj;
        }

        /// <summary>
        /// Creates a new SeqVar_Float with the given value in the given sequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ExportEntry CreateFloat(ExportEntry sequence, float value)
        {
            var fObj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_Float", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(fObj, sequence);

            fObj.WriteProperty(new FloatProperty(value, "FloatValue"));

            return fObj;
        }

        /// <summary>
        /// Creates a new SeqVar_RandomFloat with the given value range in the given sequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public static ExportEntry CreateRandFloat(ExportEntry sequence, float minValue, float maxValue)
        {
            var fFloat = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_RandomFloat", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(fFloat, sequence);

            fFloat.WriteProperty(new FloatProperty(minValue, "Min"));
            fFloat.WriteProperty(new FloatProperty(maxValue, "Max"));

            return fFloat;
        }

        /// <summary>
        /// Creates a new SeqVar_Object with the given value in the given sequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ExportEntry CreateObject(ExportEntry sequence, ExportEntry value)
        {
            var fObj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_Object", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(fObj, sequence);

            fObj.WriteProperty(new ObjectProperty(value?.UIndex ?? 0, "ObjValue"));

            return fObj;
        }

        /// <summary>
        /// Creates a new SeqVar_Int with the given value in the given sequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="i"></param>
        /// <returns></returns>
        public static ExportEntry CreateInt(ExportEntry sequence, int value)
        {
            var iObj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqVar_Int", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(iObj, sequence);

            iObj.WriteProperty(new IntProperty(value, "IntValue"));

            return iObj;
        }

        /// <summary>
        /// Creates a new SeqEvent_RemoteEvent with the given EventName
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="eventName"></param>
        /// <returns></returns>
        public static ExportEntry CreateSeqEventRemoteActivated(ExportEntry sequence, string eventName)
        {
            var fObj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqEvent_RemoteEvent", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(fObj, sequence);

            fObj.WriteProperty(new NameProperty(eventName, "EventName"));

            return fObj;
        }

        /// <summary>
        /// Creates a new SeqEvent_Death with the given Originator
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="eventName"></param>
        /// <returns></returns>
        public static ExportEntry CreateSeqEventDeath(ExportEntry sequence, ExportEntry originator)
        {
            var fObj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqEvent_Death", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(fObj, sequence);

            fObj.WriteProperty(new ObjectProperty(originator, "Originator"));

            return fObj;
        }


        /// <summary>
        /// Creates a new BioSeqVar_ObjectFindByTag with the given tag name and optionally searching unique tags
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="tagToFind"></param>
        /// <param name="searchUniqueTags"></param>
        /// <returns></returns>
        public static ExportEntry CreateFindObject(ExportEntry sequence, string tagToFind, bool searchUniqueTags = false)
        {
            var fObj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "BioSeqVar_ObjectFindByTag", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(fObj, sequence);

            fObj.WriteProperty(new StrProperty(tagToFind, "m_sObjectTagToFind"));
            if (searchUniqueTags)
            {
                fObj.WriteProperty(new BoolProperty(true, "m_bSearchUniqueTag"));
            }
            return fObj;
        }

        /// <summary>
        /// Creates a new SeqVar_AddInt with the specified parameters (if any)
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <param name="IntResult"></param>
        /// <returns></returns>
        public static ExportEntry CreateAddInt(ExportEntry sequence, ExportEntry A = null, ExportEntry B = null, ExportEntry IntResult = null, ExportEntry FloatResult = null)
        {
            var addInt = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqAct_AddInt", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(addInt, sequence);

            if (A != null)
            {
                KismetHelper.CreateVariableLink(addInt, "A", A);
            }
            if (B != null)
            {
                KismetHelper.CreateVariableLink(addInt, "B", B);
            }
            if (IntResult != null)
            {
                KismetHelper.CreateVariableLink(addInt, "IntResult", IntResult);
            }
            if (FloatResult != null)
            {
                KismetHelper.CreateVariableLink(addInt, "FloatResult", FloatResult);
            }

            return addInt;
        }

        /// <summary>
        /// Creates a SeqAct_SetInt with the specified parameters
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="target"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static ExportEntry CreateSetInt(ExportEntry sequence, ExportEntry target = null, ExportEntry value = null)
        {
            var setInt = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqAct_SetInt", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(setInt, sequence);

            if (value != null)
            {
                KismetHelper.CreateVariableLink(setInt, "Value", value);
            }

            if (target != null)
            {
                KismetHelper.CreateVariableLink(setInt, "Target", target);
            }

            return setInt;

        }

        /// <summary>
        /// Creates a SeqVar_ObjectList with BioPawnTypes specified by the allowedPawns list that MER can port around
        /// </summary>
        /// <param name="target"></param>
        /// <param name="sequence"></param>
        /// <param name="allowedPawns"></param>
        /// <returns></returns>
        public static ExportEntry CreatePawnList(GameTarget target, ExportEntry sequence, string[] allowedPawns)
        {
            var bioPawnTypes = new List<ExportEntry>();
            foreach (var v in PawnPorting.PortablePawns)
            {
                if (allowedPawns.Contains(v.BioPawnTypeIFP))
                {
                    PawnPorting.PortPawnIntoPackage(target, v, sequence.FileRef);
                    var bpt = sequence.FileRef.FindExport(v.BioPawnTypeIFP);
                    if (bpt == null)
                        Debugger.Break();
                    bioPawnTypes.Add(bpt);
                }
            }

            if (bioPawnTypes.Count == 0)
            {
                Debugger.Break(); // The list should not be empty
            }

            return MERSeqTools.CreateSeqVarList(sequence, bioPawnTypes.ToArray());
        }

        /// <summary>
        /// Redirects all inputs to a given sequence object to another
        /// </summary>
        /// <param name="original"></param>
        /// <param name="newDest"></param>
        /// <param name="inputIdxOriginal"></param>
        /// <param name="newDestIdx"></param>
        public static void RedirectInboundLinks(ExportEntry original, ExportEntry newDest, int inputIdxOriginal = 0, int newDestIdx = 0)
        {
            var parentSeq = SeqTools.GetParentSequence(original); // Use parent sequence. This ensures we can redirect on Sequence and SequenceReference
            var seqObjects = SeqTools.GetAllSequenceElements(parentSeq).OfType<ExportEntry>();
            var outboundNodes = SeqTools.FindOutboundConnectionsToNode(original, seqObjects);
            foreach (var outboundNode in outboundNodes)
            {
                var outboundLinks = SeqTools.GetOutboundLinksOfNode(outboundNode);
                foreach (var outLink in outboundLinks)
                {
                    foreach (var linkedOp in outLink)
                    {
                        if (linkedOp.LinkedOp == original && linkedOp.InputLinkIdx == inputIdxOriginal)
                        {
                            linkedOp.LinkedOp = newDest;
                            linkedOp.InputLinkIdx = newDestIdx;
                        }
                    }
                }

                SeqTools.WriteOutboundLinksToNode(outboundNode, outboundLinks);
            }
        }

        /// <summary>
        /// Creates a basic Gate object in the given sequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public static ExportEntry CreateGate(ExportEntry sequence)
        {
            var gate = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqAct_Gate", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(gate, sequence);
            return gate;
        }

        /// <summary>
        /// Creates an object of the specified class and adds it to the listed sequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="className"></param>
        /// <returns></returns>
        public static ExportEntry CreateAndAddToSequence(ExportEntry sequence, string className)
        {
            var obj = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, className, MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(obj, sequence);
            return obj;
        }

        /// <summary>
        /// Creates a SeqAct_Log object
        /// </summary>
        /// <param name="seq"></param>
        /// <param name="comment"></param>
        /// <returns></returns>
        public static ExportEntry CreateLog(ExportEntry seq, string comment)
        {
            // This is often used for hackjobbing things
            var obj = CreateAndAddToSequence(seq, "SeqAct_Log");
            KismetHelper.SetComment(obj, comment);
            return obj;
        }

        /// <summary>
        /// Removes incoming links to a sequence action object
        /// </summary>
        public static void RemoveLinksTo(ExportEntry seqNode)
        {
            var sequence = SeqTools.GetParentSequence(seqNode);
            var nodes = SeqTools.GetAllSequenceElements(sequence).OfType<ExportEntry>();
            var inboundNodes = SeqTools.FindOutboundConnectionsToNode(seqNode, nodes);
            foreach (var inbound in inboundNodes)
            {
                var outbound = SeqTools.GetOutboundLinksOfNode(inbound);
                foreach (var ob in outbound)
                {
                    // Remove all items that link to our node
                    ob.RemoveAll(x => x.LinkedOp == seqNode);
                }

                SeqTools.WriteOutboundLinksToNode(inbound, outbound);
            }

        }

        /// <summary>
        /// Creates a new delay with a SeqVar_RandomFloat in the specified range
        /// </summary>
        /// <param name="seq"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static ExportEntry CreateRandomDelay(ExportEntry seq, float min, float max)
        {
            var newDelay = SequenceObjectCreator.CreateSequenceObject(seq.FileRef, "SeqAct_Delay", MERCaches.GlobalCommonLookupCache);
            var newRandFloat = CreateRandFloat(seq, min, max);
            KismetHelper.AddObjectsToSequence(seq, false, newDelay, newRandFloat);
            KismetHelper.CreateVariableLink(newDelay, "Duration", newRandFloat);
            return newDelay;
        }

        /// <summary>
        /// Creates a PMCheckState with the given index to check for
        /// </summary>
        /// <param name="seq"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static ExportEntry CreatePMCheckState(ExportEntry seq, int index)
        {
            var checkState = SequenceObjectCreator.CreateSequenceObject(seq.FileRef, "BioSeqAct_PMCheckState", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(checkState, seq);

            checkState.WriteProperty(new IntProperty(index, "m_nIndex"));

            return checkState;
        }

        /// <summary>
        /// Creates a ModifyObjectList in the given sequence
        /// </summary>
        /// <param name="seq"></param>
        /// <returns></returns>
        public static ExportEntry CreateModifyObjectList(ExportEntry seq)
        {
            var objListModifier = SequenceObjectCreator.CreateSequenceObject(seq.FileRef, "SeqAct_ModifyObjectList", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(objListModifier, seq);
            return objListModifier;
        }

        /// <summary>
        /// Creates a new SeqVar_Named to find the name/class type combo in, in the given sequence
        /// </summary>
        /// <param name="seq"></param>
        /// <param name="varName"></param>
        /// <param name="expectedType"></param>
        /// <returns></returns>
        public static ExportEntry CreateSeqVarNamed(ExportEntry seq, string varName, string expectedType)
        {
            var varNamed = SequenceObjectCreator.CreateSequenceObject(seq.FileRef, "SeqVar_Named", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(varNamed, seq);
            var expectedTypeClass = EntryImporter.EnsureClassIsInFile(seq.FileRef, expectedType, new RelinkerOptionsPackage(MERCaches.GlobalCommonLookupCache));
            varNamed.WriteProperty(new NameProperty(varName, "FindVarName"));
            varNamed.WriteProperty(new ObjectProperty(expectedTypeClass, "ExpectedType"));
            return varNamed;
        }

        /// <summary>
        /// Creates a WwisePostEvent action in the given sequence with the given event
        /// </summary>
        /// <param name="seq"></param>
        /// <returns></returns>
        public static ExportEntry CreateWwisePostEvent(ExportEntry seq, IEntry wwiseEvent)
        {
            var postEvent = SequenceObjectCreator.CreateSequenceObject(seq.FileRef, "SeqAct_WwisePostEvent", MERCaches.GlobalCommonLookupCache);
            KismetHelper.AddObjectToSequence(postEvent, seq);

            postEvent.WriteProperty(new ObjectProperty(wwiseEvent, "WwiseObject"));

            return postEvent;
        }

        /// <summary>
        /// Inserts an object between another in a kismet graph. The object being inserted should not have any outlinks on the outlink name specified
        /// </summary>
        /// <param name="originalNode"></param>
        /// <param name="outlinkName"></param>
        /// <param name="mitmNode"></param>
        /// <param name="mitmInputIdx"></param>
        /// <param name="mitmOutlinkName"></param>
        public static void InsertActionAfter(ExportEntry originalNode, string outlinkName, ExportEntry mitmNode, int mitmInputIdx, string mitmOutlinkName)
        {
            var outLinkIdxToRedirect = SeqTools.GetOutlinkNames(originalNode).IndexOf(outlinkName);
            if (outLinkIdxToRedirect == -1)
            {
                // Outlink needs made
                KismetHelper.CreateNewOutputLink(originalNode, outlinkName, null);
                outLinkIdxToRedirect = SeqTools.GetOutlinkNames(originalNode).IndexOf(outlinkName);
            }


            var originalOutLinks = SeqTools.GetOutboundLinksOfNode(originalNode);
            var newOutLinks = SeqTools.GetOutboundLinksOfNode(originalNode);

            newOutLinks[outLinkIdxToRedirect].Clear();
            newOutLinks[outLinkIdxToRedirect].Add(new SeqTools.OutboundLink() { InputLinkIdx = mitmInputIdx, LinkedOp = mitmNode }); // Point only to our new node
            SeqTools.WriteOutboundLinksToNode(originalNode, newOutLinks);

            var mitmOutLinks = SeqTools.GetOutboundLinksOfNode(mitmNode);
            var mitmOutlinkIdxToUse = SeqTools.GetOutlinkNames(mitmNode).IndexOf(mitmOutlinkName);

            mitmOutLinks[mitmOutlinkIdxToUse] = originalOutLinks[outLinkIdxToRedirect]; // Use the original outlinks as the output from this outlink

            SeqTools.WriteOutboundLinksToNode(mitmNode, mitmOutLinks);
        }
    }
}
