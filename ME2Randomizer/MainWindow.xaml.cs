using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using ControlzEx.Theming;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCore.Services.Restore;
using ME3TweaksCore.Targets;
using PropertyChanged;
using Randomizer.MER;
using Randomizer.Randomizers;
using RandomizerUI.Classes;
using RandomizerUI.Classes.Controllers;
using RandomizerUI.DebugTools;
using RandomizerUI.ui;
using RandomizerUI.windows;

namespace RandomizerUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class MainWindow : MetroWindow
    {
#if __GAME1__
        private static string FaqLink = "https://me3tweaks.com/masseffectlerandomizer/faq";
        public bool ShowGenerationSelector => true;
#elif __GAME2__
        private static string FaqLink = "https://me3tweaks.com/masseffect2lerandomizer/faq";
        public bool ShowGenerationSelector => true;
#elif __GAME3__
        private static string FaqLink = "https://me3tweaks.com/masseffect3lerandomizer/faq";
        public bool ShowGenerationSelector => false;
#endif
        public bool UseMultiThreadRNG { get; set; } = true;


        /// <summary>
        /// If the UI should display the labels
        /// </summary>
        public bool ShowLabels { get; set; }

        #region Flyouts
        public bool LogUploaderFlyoutOpen { get; set; }
        public bool FirstRunFlyoutOpen { get; set; }
        #endregion

        public string GamePathString { get; set; } = "Please wait";
        public bool ShowProgressPanel { get; set; }

        public ObservableCollectionExtended<ImageCredit> ImageCredits { get; } = new();
        public ObservableCollectionExtended<string> ContributorCredits { get; } = new();
        public ObservableCollectionExtended<LibraryCredit> LibraryCredits { get; } = new();

        /// <summary>
        /// The list of options shown
        /// </summary>
        public ObservableCollectionExtended<RandomizationGroup> RandomizationGroups { get; } = new ObservableCollectionExtended<RandomizationGroup>();
        public bool AllowOptionsChanging { get; set; } = true;

        /// <summary>
        /// Reinstall the DLC mod component, vs stacking on top of it
        /// </summary>
        public bool PerformReroll { get; set; } = true;
        public long CurrentProgressValue { get; set; }
        public string CurrentOperationText { get; set; }
        public long ProgressBar_Bottom_Min { get; set; }
        public long ProgressBar_Bottom_Max { get; set; }
        public bool ProgressBarIndeterminate { get; set; }
        public bool ShowUninstallButton { get; set; }
        public bool DLCComponentInstalled { get; set; }

        public void OnDLCComponentInstalledChanged()
        {
            if (!DLCComponentInstalled)
            {
                ShowUninstallButton = false;
            }
            else
            {
                // Refresh the bindings
                CommandManager.InvalidateRequerySuggested();
            }
        }
        public LogItem SelectedLogForUpload { get; set; }
        public ObservableCollectionExtended<LogItem> LogsAvailableForUpload { get; } = new();

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                MERUtilities.OpenWebPage(((Hyperlink)sender).NavigateUri.AbsoluteUri);
            }
            catch (Exception)
            {

            }
        }

        private void UpdateOptionsForMode(OptionMode mode)
        {
            foreach (var group in RandomizationGroups)
            {
                foreach (var option in group.Options)
                {
                    SetOptionOnRecommendation(option, mode);
                }
            }

        }

        private void SetOptionOnRecommendation(RandomizationOption option, OptionMode mode)
        {
            if (mode == OptionMode.EOptionMode_Fun && option.GoodTimeRandomizer)
            {
                option.OptionIsSelected = true;
            }
            else if (mode == OptionMode.EOptionMode_Gameplay && option.GameplayRandomizer)
            {
                option.OptionIsSelected = true;
            }
            else if (mode == OptionMode.EOptionMode_Recommended && option.IsRecommended)
            {
                option.OptionIsSelected = true;
            }
            else
            {
                option.OptionIsSelected = false;
            }

            if (option.SubOptions != null)
            {
                foreach (var subOption in option.SubOptions)
                {
                    if (option.OptionIsSelected)
                    {
                        if (subOption.SelectOnPreset)
                        {
                            subOption.OptionIsSelected = true;
                        }
                        else
                        {
                            SetOptionOnRecommendation(subOption, mode);
                        }
                    }
                    else
                    {
                        subOption.OptionIsSelected = false;
                    }
                }
            }
        }


        public MainWindow()
        {
            DataContext = this;
            ProgressBar_Bottom_Max = 100;
            ProgressBar_Bottom_Min = 0;
            ShowProgressPanel = true;
            LoadCommands();
            InitializeComponent();
        }

        private void optionStateChanging(RandomizationOption obj)
        {
            if (obj.MutualExclusiveSet != null && obj.OptionIsSelected)
            {
                var allOptions = RandomizationGroups.SelectMany(x => x.Options).Where(x => x.MutualExclusiveSet == obj.MutualExclusiveSet);
                foreach (var option in allOptions)
                {
                    if (option != obj)
                    {
                        option.OptionIsSelected = false; // turn off other options
                    }
                }
            }
        }

        // Todo: move this into randomizer as it will be game specific.
        internal List<string> GetContributorCredits()
        {
            var contributors = new List<string>();
            contributors.Add("Kosh_Vader - Testing");
            contributors.Add("Vegz - Testing");
            contributors.Add("Khaar - 3D modeling");
            contributors.Add("Clericofshadows - 3D modeling");
            contributors.Add("Tajfun403 - Technical, custom weapons");
            contributors.Add("Mellin - 3D modeling");
            contributors.Add("sinsofawindmill - UwU Emoticons implementation");
            contributors.Add("benefactor - Technical");
            contributors.Add("Audemus - ME2R images & templates");
            contributors.Add("D. Senji - Music");
            contributors.Add("ZumAstra - Testing");
            contributors.Sort();
            return contributors;
        }

        #region Commands
        public GenericCommand StartRandomizationCommand { get; set; }
        public GenericCommand CloseLogUICommand { get; set; }
        public GenericCommand UploadSelectedLogCommand { get; set; }
        public RelayCommand SetupRandomizerCommand { get; set; }
        public GenericCommand UninstallDLCCommand { get; set; }

        private void LoadCommands()
        {
            StartRandomizationCommand = new GenericCommand(StartRandomization, CanStartRandomization);
            CloseLogUICommand = new GenericCommand(() => LogUploaderFlyoutOpen = false, () => LogUploaderFlyoutOpen);
            UploadSelectedLogCommand = new GenericCommand(CollectAndUploadLog, () => SelectedLogForUpload != null);
            SetupRandomizerCommand = new RelayCommand(SetupRandomizer, CanSetupRandomizer);
            UninstallDLCCommand = new GenericCommand(UninstallDLCComponent, CanUninstallDLCComponent);
            OptionTogglerCommand = new GenericCommand(ShowOptionToggler, () => DLCComponentInstalled);
        }

        private void ShowOptionToggler()
        {
            var ot = new OptionTogglerWindow();
            ot.Owner = this;
#if DEBUG
            ot.Show();
#else
            ot.ShowDialog();
#endif
        }

        private async void UninstallDLCComponent()
        {
            var dlcModPath = MERFileSystem.GetDLCModPath(TargetHandler.Target);
            if (dlcModPath != null && Directory.Exists(dlcModPath))
            {
                var pd = await this.ShowProgressAsync("Deleting DLC component", "Please wait while the DLC mod component of your current randomization is deleted.");
                pd.SetIndeterminate();
                Task.Run(() =>
                    {
                        MUtilities.DeleteFilesAndFoldersRecursively(dlcModPath);
                        DLCComponentInstalled = false;
                        Thread.Sleep(2000);
                    })
                    .ContinueWithOnUIThread(async x =>
                    {
                        await pd.CloseAsync();
                        await this.ShowMessageAsync("DLC component uninstalled", "The DLC component of the randomization has been uninstalled. A few files that cannot be placed into DLC may remain, you will need to repair your game to remove them.\n\nFor faster restores in the future, make a backup with an ME3Tweaks program. Mass Effect 2 randomization uninstallation only takes a few seconds when an ME3Tweaks backup is available.");
                        CommandManager.InvalidateRequerySuggested();
                    });
            }
        }

        private bool CanUninstallDLCComponent()
        {
            if (TargetHandler.Target == null) return false;
            var status = BackupService.GetBackupStatus(TargetHandler.Target.Game);
            var canUninstall = ShowUninstallButton = status != null && !status.BackedUp && DLCComponentInstalled;
            return canUninstall;
        }

        private bool CanSetupRandomizer(object obj)
        {
            return obj is RandomizationOption option && option.OptionIsSelected && option.SetupRandomizerDelegate != null;
        }

        private void SetupRandomizer(object obj)
        {
            if (obj is RandomizationOption option)
            {
                option.SetupRandomizerDelegate?.Invoke(option);
            }
        }

#endregion


        public async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StartupUIController.BeginFlow(this);
        }

        public string BackupRestoreText { get; set; }
        public string BackupRestoreToolTip { get; set; }

        public bool DirectionsTextVisible { get; set; }

        public string FirstRunBackgroundImage
        {
            get
            {
#if __GAME1__
                return "/images/game1/firstrun_bg.jpg";
#elif __GAME2__
                return "/images/game2/firstrun_bg.jpg";
#elif __GAME3__
                return "/images/game3/firstrun_bg.jpg";
#endif
                throw new Exception("NOT A VALID BUILD");
            }
        }

        public string MainWindowBackgroundImage
        {
            get
            {
#if __GAME1__
                return "/images/game1/lebackground.jpg";
#elif __GAME2__
                return "/images/game2/lebackground.jpg";
#elif __GAME3__
                return "/images/game3/lebackground.jpg";
#endif
                throw new Exception("NOT A VALID BUILD");
            }
        }

        public string RandomizerName => $"{MERUtilities.GetGameUIName(true)} Legendary Edition Randomizer";
        public string IntroTitleText => $"Welcome to {RandomizerName}";
        public string IntroTitleSubText => $"Please read the following information to help ensure you have the best experience\nwith {RandomizerName} ({MERUtilities.GetRandomizerShortName()}).";
        public ICommand OptionTogglerCommand { get; set; }

#if __GAME1__ || __GAME3__
        public bool ShowImageCredits => true;
#else
        // LE2R does not use third party images
        public bool ShowImageCredits => false;
#endif

        /// <summary>
        /// The displayed copyright string
        /// </summary>
        public string CopyrightString { get; set; }

        public void SetupCopyrightString()
        {
            CopyrightString = $"Copyright (C) 2019-{(BuildHelper.BuildDate.Year > 2018 ? BuildHelper.BuildDate.Year : 2023)} ME3Tweaks\n\nThis program is free software: you can redistribute it and/or modify it under the terms of the\nGNU General Public License as published by the Free Software Foundation, either version 3 of the\nLicense, or (at your option) any later version.\nThis program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without\neven the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the\nGNU General Public License for more details.\n\n{MERUtilities.GetGameUIName(true)} is a registered trademark of EA INTERNATIONAL (STUDIO AND PUBLISHING) LTD.\nThis program has no affiliation with BioWare or Electronic Arts.";
        }

    private bool CanStartRandomization()
        {
            if (SeedTextBox == null || !int.TryParse(SeedTextBox.Text, out var value) || value == 0)
                return false;

            // Target not found
            if (TargetHandler.Target == null)
                return false;
            return true;
        }

        private async void StartRandomization()
        {
            if (TargetHandler.Target == null)
                return;

            if (MERUtilities.IsGameRunning(MERFileSystem.Game))
            {
                await this.ShowMessageAsync($"{MERFileSystem.Game.ToGameName()} is running", $"Cannot randomize the game while {MERFileSystem.Game.ToGameName()} is running. Please close the game and try again.");
                return;
            }

            var modPath = MERFileSystem.GetDLCModPath(TargetHandler.Target);
            var backupStatus = BackupService.GetBackupStatus(TargetHandler.Target.Game);
            if (!backupStatus.BackedUp && !Directory.Exists(modPath))
            {
                var settings = new MetroDialogSettings()
                {
                    AffirmativeButtonText = "Continue anyways",
                    NegativeButtonText = "Cancel"
                };
                var result = await this.ShowMessageAsync("No ME3Tweaks-based backup available", "It is recommended that you create an ME3Tweaks-based backup before randomization, as this allows much faster re-rolls. You can take a backup using the button on the bottom left of the interface.", MessageDialogStyle.AffirmativeAndNegative, settings);
                if (result == MessageDialogResult.Negative)
                {
                    // Do nothing. User canceled
                    return;
                }
            }

            if (Directory.Exists(modPath))
            {
                if (PerformReroll && backupStatus.BackedUp)
                {
                    var settings = new MetroDialogSettings()
                    {
                        AffirmativeButtonText = "Quick restore",
                        NegativeButtonText = "No restore",
                        FirstAuxiliaryButtonText = "Cancel",
                        DefaultButtonFocus = MessageDialogResult.Affirmative,
                    };
                    var result = await this.ShowMessageAsync("Existing randomization already installed",
                        "An existing randomization is already installed. It is highly recommended that you perform a quick restore before re-rolling so that basegame changes do not stack or are left installed if your new options do not include these changes.\n\nQuick restore will restore basegame only files, such as SFXGame.pcc - changes to these files (including by other mods) will be reverted.\n\nPerform a quick restore before randomization?",
                        MessageDialogStyle.AffirmativeAndNegativeAndSingleAuxiliary, settings);
                    if (result == MessageDialogResult.FirstAuxiliary)
                    {
                        // Do nothing. User canceled
                        return;
                    }

                    if (result == MessageDialogResult.Affirmative)
                    {
                        // Perform quick restore first
                        RestoreController.StartRestore(this, TargetHandler.Target, true, InternalStartRandomization);
                        return; // Return, we will run randomization after this
                    }

                    // User did not want to restore, just run 
                }
                else if (PerformReroll) // Backup doesn't exist but user specified they want fresh install
                {
                    // no backup, can't quick restore
                    var settings = new MetroDialogSettings()
                    {
                        AffirmativeButtonText = "Continue anyways",
                        NegativeButtonText = "Cancel",
                    };
                    var result = await this.ShowMessageAsync("Existing randomization already installed",
                        "An existing randomization is already installed. Some basegame only randomized files may remain after the DLC component is removed, and if options that modify these files are selected, the effects will stack. It is recommended you 'Remove Randomization' in the bottom left window, then repair your game to ensure you have a fresh installation for a re-roll.\n\nAn ME3Tweaks-based backup is recommended to avoid this procedure, which can be created in the bottom left of the application. It enables the quick restore feature, which only takes a few seconds.",
                        MessageDialogStyle.AffirmativeAndNegative, settings);
                    if (result == MessageDialogResult.Negative)
                    {
                        // Do nothing. User canceled
                        return;
                    }
                }
            }

            InternalStartRandomization();
        }

        private async void InternalStartRandomization()
        {
            if (!MERUtilities.IsGameRunning(TargetHandler.Target.Game))
            {
#if __GAME1__
                var randomizer = new Randomizer.Randomizers.Game1.Randomizer();
#elif __GAME2__
                var randomizer = new Randomizer.Randomizers.Game2.Randomizer();
#elif __GAME3__
                var randomizer = new Randomizer.Randomizers.Game3.Randomizer();
#endif

                var op = new OptionsPackage()
                {
                    Seed = int.Parse(SeedTextBox.Text),
                    SelectedOptions = RandomizationGroups.SelectMany(x => x.Options.Where(x => x.OptionIsSelected)).ToList(),
                    UseMultiThread = UseMultiThreadRNG,
                    Reroll = PerformReroll,
                    RandomizationTarget = TargetHandler.Target,
                    SetCurrentOperationText = x => CurrentOperationText = x,
                    SetOperationProgressBarIndeterminate = x => ProgressBarIndeterminate = x,
                    NotifyDLCComponentInstalled = x => DLCComponentInstalled = x,
                    SetOperationProgressBarProgress = (x, y) =>
                    {
                        CurrentProgressValue = x;
                        ProgressBar_Bottom_Max = y;
                    },
                    SetRandomizationInProgress = x =>
                    {
                        ShowProgressPanel = x;
                        AllowOptionsChanging = !x;
                    }
                };
                randomizer.Randomize(op);
            }
            else
            {
                await this.ShowMessageAsync($"{TargetHandler.Target.Game.ToGameName()} is running", $"Cannot randomize the game while {TargetHandler.Target.Game.ToGameName()} is running. Please close the game and try again.");
            }
        }

        private void Image_ME3Tweaks_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start("https://me3tweaks.com");
            }
            catch (Exception)
            {

            }
        }

        public void FinalizeInterfaceLoad()
        {
#if __GAME1__
            Randomizer.Randomizers.Game1.Randomizer.SetupOptions(RandomizationGroups, optionStateChanging);
#elif __GAME2__
            Randomizer.Randomizers.Game2.Randomizer.SetupOptions(RandomizationGroups, optionStateChanging);
#elif __GAME3__
            Randomizer.Randomizers.Game3.Randomizer.SetupOptions(RandomizationGroups, optionStateChanging);
#endif
            ShowProgressPanel = false;
        }

        private void Logs_Click(object sender, RoutedEventArgs e)
        {
            LogUploaderFlyoutOpen = true;
        }

        private async void BackupRestore_Click(object sender, RoutedEventArgs e)
        {
            if (TargetHandler.Target == null)
                return; // Do not allow!

            string path = BackupService.GetGameBackupPath(TargetHandler.Target.Game);
            if (path != null)
            {

                if (TargetHandler.Target.TextureModded)
                {
                    RestoreController.StartRestore(this, TargetHandler.Target, false);
                }
                else
                {
                    MetroDialogSettings settings = new MetroDialogSettings();
                    settings.NegativeButtonText = "Full";
                    settings.FirstAuxiliaryButtonText = "Cancel";
                    settings.AffirmativeButtonText = "Quick";
                    settings.DefaultButtonFocus = MessageDialogResult.Affirmative;
                    var result = await this.ShowMessageAsync("Select restore mode", $"Select which restore mode you would like to perform:\n\nQuick: Restores basegame files modifiable by {MERUI.GetRandomizerName()}, deletes the DLC mod component\n\nFull: Deletes entire game installation and restores the backup in its place. Fully resets the game to the backup state", MessageDialogStyle.AffirmativeAndNegativeAndSingleAuxiliary, settings);
                    if (result == MessageDialogResult.FirstAuxiliary)
                    {
                        // Do nothing. User canceled
                    }
                    else
                    {
                        RestoreController.StartRestore(this, TargetHandler.Target, result == MessageDialogResult.Affirmative);
                    }
                }
            }
            //else if (gameTarget == null)
            //{
            //    await this.ShowMessageAsync($"{MERFileSystem.Game.ToGameName()} not found", $"{MERFileSystem.Game.ToGameName()} was not found, and as such, cannot be restored by {MERFileSystem.Game.ToGameName()} Randomizer. Repair your game using Steam, Origin, or your DVD, or restore your backup using ME3Tweaks Mod Manager.");
            //}
        }

        private void DebugCloseDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            //DiagnosticsFlyoutOpen = false;
        }

        private void Button_FirstTimeRunDismiss_Click(object sender, RoutedEventArgs e)
        {
            MERSettings.WriteSettingBool(ESetting.SETTING_FIRSTRUN, true);
            FirstRunFlyoutOpen = false;
        }

        private void Flyout_Mousedown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Flyout_Doubleclick(object sender, MouseButtonEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowState = WindowState.Normal;
            }
        }

        private void FAQ_Click(object sender, RoutedEventArgs e)
        {
            MERUtilities.OpenWebPage(FaqLink);
        }

        private void DiscordButton_Click(object sender, RoutedEventArgs e)
        {
            //ME3Tweaks Discord
            MERUtilities.OpenWebPage(App.DISCORD_INVITE_LINK);
        }

        public void OnLogUploaderFlyoutOpenChanged()
        {
            if (LogUploaderFlyoutOpen)
            {
                LogsAvailableForUpload.ReplaceAll(LogCollector.GetLogsList(MERLog.CurrentLogFilePath));
                SelectedLogForUpload = LogsAvailableForUpload.FirstOrDefault(x => x.IsActiveLog);
            }
            else
            {
                LogsAvailableForUpload.ClearEx();
                SelectedLogForUpload = null;
            }
        }

        private void DebugWindow_Click(object sender, RoutedEventArgs e)
        {
            new DebugWindow(this).Show();
        }

        private async void CollectAndUploadLog()
        {
            var pd = await this.ShowProgressAsync("Uploading log", $"Please wait while the application log is uploaded to the ME3Tweaks Log Viewing Service.");
            pd.SetIndeterminate();

            NamedBackgroundWorker nbw = new NamedBackgroundWorker("DiagnosticsWorker");
            nbw.DoWork += (a, b) =>
            {
                LogUploadPackage lup = new LogUploadPackage()
                {
                    DiagnosticTarget = TargetHandler.Target,
                    SelectedLog = SelectedLogForUpload,
                    UpdateStatusCallback = s =>
                    {
                        pd.SetMessage(s);
                    }
                };

                var response = LogCollector.SubmitDiagnosticLog(lup);

                //DiagnosticStatusText = "Uploading to log viewing service";
                //ProgressIndeterminate = true;
                if (response != null)
                {
                    if (response.StartsWith("http"))
                    {
                        MERUtilities.OpenWebPage(response);
                    }
                }


                //if (!response.uploaded || QuickFixHelper.IsQuickFixEnabled(QuickFixHelper.QuickFixName.ForceSavingLogLocally))
                //{
                //    // Upload failed.
                //    var GeneratedLogPath = Path.Combine(MCoreFilesystem.GetLogDir(), $"FailedLogUpload_{DateTime.Now.ToString("s").Replace(":", ".")}.txt");
                //    File.WriteAllText(GeneratedLogPath, logUploadText.ToString());
                //}

                //DiagnosticComplete = true;
                //DiagnosticInProgress = false;
            };
            nbw.RunWorkerCompleted += async (sender, args) =>
            {
                CommandManager.InvalidateRequerySuggested();
                LogUploaderFlyoutOpen = false;
                await pd.CloseAsync();
            };
            //DiagnosticInProgress = true;
            nbw.RunWorkerAsync();
        }

        #region Settings

        public const string SETTING_FIRSTRUN = "FirstRunCompleted";
        private void FirstRunShowButton_Click(object sender, RoutedEventArgs e)
        {
            FirstRunFlyoutOpen = true;
        }

        #endregion

        public void SetupTargetDescriptionText()
        {
            if (TargetHandler.Target == null)
            {
                var gameName = Randomizer.MER.MERUtilities.GetGameUIName(false);
                GamePathString = $"{gameName} not detected. Repair and run your game to fix detection.";
            }
            //else if (TargetHandler.Target.TextureModded)
            //{
            //    // How true is this for LE?
            //    GamePathString = "Cannot randomize, game is texture modded";
            //}
            else
            {
                DirectionsTextVisible = true;
                GamePathString = $"Randomization target: {TargetHandler.Target.TargetPath}";
            }
        }

        private void ROClickHACK_Click(object sender, MouseButtonEventArgs e)
        {
            if (AllowOptionsChanging && sender is FrameworkElement fe && fe.DataContext is RandomizationOption option)
            {
                // Toggle
                option.OptionIsSelected = !option.OptionIsSelected;
            }
        }

        internal void MERPeriodicRefresh(object sender, EventArgs eventArgs)
        {
            if (TargetHandler.Target != null)
            {
                // Update DLC component status (if its modified outside of MER)
                // Is DLC component installed?
                var dlcModPath = MERFileSystem.GetDLCModPath(TargetHandler.Target);
                DLCComponentInstalled = dlcModPath != null && Directory.Exists(dlcModPath);
                
                // Update backup status
                var backupStatus = BackupService.GetBackupStatus(TargetHandler.Target.Game);
                BackupRestoreText = backupStatus?.BackupActionText;
                BackupRestoreToolTip = backupStatus != null && backupStatus.BackedUp ? "Click to restore game/uninstall randomizer mod" : "Click to backup game"; 
            }
        }

        private void FunMode_Click(object sender, RoutedEventArgs e)
        {
            UpdateOptionsForMode(OptionMode.EOptionMode_Fun);
        }

        private void GameplayMode_Click(object sender, RoutedEventArgs e)
        {
            UpdateOptionsForMode(OptionMode.EOptionMode_Gameplay);
        }

        private void Recommended_Click(object sender, RoutedEventArgs e)
        {
            UpdateOptionsForMode(OptionMode.EOptionMode_Recommended);
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            UpdateOptionsForMode(OptionMode.EOptionMode_Clear);
        }
    }
}