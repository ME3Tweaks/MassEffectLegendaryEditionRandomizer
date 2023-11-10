using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using MahApps.Metro.Controls;
using ME3TweaksCore.Config;
using ME3TweaksCoreWPF.UI;
using PropertyChanged;
using Randomizer.MER;
using RandomizerUI.Classes;
using Windows.Foundation.Collections;
using Windows.System;
using LegendaryExplorerCore.Helpers;
using Microsoft.WindowsAPICodePack.PortableDevices.CommandSystem.Object;
using Microsoft.WindowsAPICodePack.PortableDevices.EventSystem;
using System.Collections;
using WinCopies.Util;

namespace RandomizerUI.windows
{
    /// <summary>
    /// Interaction logic for OptionTogglerWindow.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class OptionTogglerWindow : MetroWindow
    {
        public ObservableCollection<MERRuntimeOption> InstalledOptions { get; } = new();

        /// <summary>
        /// Options that the user cannot modify
        /// </summary>
        private List<MERRuntimeOption> HiddenOptions { get; } = new();

        public OptionTogglerWindow()
        {
            MERUILog.Information($"OptionToggler: Opening window");

            LoadCommands();
            InitializeComponent();
            GetInstalledOptions();
        }

        public bool IsEmptyList { get; set; }
        public ICommand CancelCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        private void LoadCommands()
        {
            CancelCommand = new GenericCommand(Close);
            SaveCommand = new GenericCommand(SaveChanges, CanSaveChanges);
        }

        private bool CanSaveChanges()
        {
            return InstalledOptions != null && InstalledOptions.Any(); // UI options must exist for this to save
        }

        private List<MERRuntimeOption> GetInstalledOptions()
        {
            MERUILog.Information($"OptionToggler: Getting current configuration");

            var options = new List<MERRuntimeOption>();
            ConfigAssetBundle bundle = ConfigAssetBundle.FromDLCFolder(MERFileSystem.Game, MERFileSystem.GetDLCModCookedPath(TargetHandler.Target), MERFileSystem.DLCModName);
            var engine = bundle.GetAsset("BioEngine.ini", false);
            var section = engine.GetOrAddSection("Engine.MERControlEngine");
            foreach (var v in section)
            {
                // Only type 2 is supported - lists cannot be edited by this UI.
                if (v.Value[0].ParseAction == CoalesceParseAction.Add)
                {
                    if (!IsHiddenOption(v.Key))
                    {
                        options.Add(new MERRuntimeOption(v.Key, v.Value));
                    }
                    else
                    {
                        HiddenOptions.Add(new MERRuntimeOption(v.Key, v.Value));
                    }
                }
            }

            InstalledOptions.ReplaceAll(options.OrderBy(x=>x.DisplayString));
            IsEmptyList = InstalledOptions.Count == 0;
            return options;
        }

        private bool IsHiddenOption(string propertyName)
        {
            switch (propertyName)
            {
                case "bSuicideMissionRandomizationInstalled":
                    return true;
                default:
                    return false;
            }
        }

        private void SaveChanges()
        {
            ConfigAssetBundle bundle = ConfigAssetBundle.FromDLCFolder(MERFileSystem.Game, MERFileSystem.GetDLCModCookedPath(TargetHandler.Target), MERFileSystem.DLCModName);
            var engine = bundle.GetAsset("BioEngine.ini", false);
            var section = engine.GetOrAddSection("Engine.MERControlEngine");

            section.RemoveAll(x => x.Value.Any(x => x.ParseAction == CoalesceParseAction.Add)); // 'Add' properties are removed. Add Uniques are lists which are not cleared or supported by this UI
            foreach (var v in InstalledOptions.Concat(HiddenOptions))
            {
                string valueStr = "";
                if (v.IsBoolProperty)
                    valueStr = v.IsSelected ? "TRUE" : "FALSE";
                if (v.IsIntProperty)
                    valueStr = v.IntValue.ToString();
                if (v.IsFloatProperty)
                    valueStr = v.FloatValue.ToString(CultureInfo.InvariantCulture);

                MERUILog.Information($"OptionToggler: Setting runtime variable: {v.PropertyName} = {valueStr}");
                section.AddEntry(new CoalesceProperty(v.PropertyName, new CoalesceValue(valueStr, CoalesceParseAction.Add)));
            }

            bundle.CommitDLCAssets();

            Close();
        }
    }
}
