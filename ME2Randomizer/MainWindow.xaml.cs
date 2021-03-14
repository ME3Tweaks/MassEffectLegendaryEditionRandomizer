﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.PlatformSpecific.Windows;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MassEffectRandomizer.Classes;
using ME2Randomizer.Classes;
using ME2Randomizer.Classes.Controllers;
using ME2Randomizer.Classes.Randomizers;
using ME2Randomizer.DebugTools;
//using ME2Randomizer.DebugTools;
using ME2Randomizer.ui;
using ME3ExplorerCore.Gammtek.Extensions;
using ME3ExplorerCore.Helpers;
using Serilog;

namespace ME2Randomizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        private static string FaqLink = "https://me3tweaks.com/masseffect2randomizer/faq";

        public enum RandomizationMode
        {
            ERandomizationMode_SelectAny = 0,
            ERandomizationMode_Common = 1,
            ERandomizationMode_Screed = 2
        }
        public bool UseMultiThreadRNG { get; set; } = true;

        #region Flyouts
        public bool LogUploaderFlyoutOpen { get; set; }
        public bool FirstRunFlyoutOpen { get; set; }
        #endregion

        public string GamePathString { get; set; } = "Please wait";
        public bool ShowProgressPanel { get; set; }
        public RandomizationMode SelectedRandomizeMode { get; set; }

        public ME3ExplorerCore.Misc.ObservableCollectionExtended<ImageCredit> ImageCredits { get; } = new ME3ExplorerCore.Misc.ObservableCollectionExtended<ImageCredit>();
        public ME3ExplorerCore.Misc.ObservableCollectionExtended<string> ContributorCredits { get; } = new ME3ExplorerCore.Misc.ObservableCollectionExtended<string>();
        public ME3ExplorerCore.Misc.ObservableCollectionExtended<LibraryCredit> LibraryCredits { get; } = new ME3ExplorerCore.Misc.ObservableCollectionExtended<LibraryCredit>();

        public void OnSelectedRandomizeModeChanged()
        {
            UpdateCheckboxSettings();
        }

        /// <summary>
        /// The list of options shown
        /// </summary>
        public ME3ExplorerCore.Misc.ObservableCollectionExtended<RandomizationGroup> RandomizationGroups { get; } = new ME3ExplorerCore.Misc.ObservableCollectionExtended<RandomizationGroup>();
        public bool AllowOptionsChanging { get; set; } = true;
        public bool PerformReroll { get; set; } = true;
        public int CurrentProgressValue { get; set; }
        public string CurrentOperationText { get; set; }
        public double ProgressBar_Bottom_Min { get; set; }
        public double ProgressBar_Bottom_Max { get; set; }
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
        public LogCollector.LogItem SelectedLogForUpload { get; set; }
        public ME3ExplorerCore.Misc.ObservableCollectionExtended<LogCollector.LogItem> LogsAvailableForUpload { get; } = new ME3ExplorerCore.Misc.ObservableCollectionExtended<LogCollector.LogItem>();

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
        private void UpdateCheckboxSettings()
        {
            foreach (var group in RandomizationGroups)
            {
                foreach (var option in group.Options)
                {
                    SetOptionOnRecommendation(option);
                }
            }
        }

        private void SetOptionOnRecommendation(RandomizationOption option)
        {
            if (SelectedRandomizeMode == RandomizationMode.ERandomizationMode_Screed) option.OptionIsSelected = option.Dangerousness < RandomizationOption.EOptionDangerousness.Danger_RIP;
            if (SelectedRandomizeMode == RandomizationMode.ERandomizationMode_SelectAny) option.OptionIsSelected = false;
            if (SelectedRandomizeMode == RandomizationMode.ERandomizationMode_Common) option.OptionIsSelected = option.IsRecommended;
            if (option.SubOptions != null)
            {
                foreach (var subOption in option.SubOptions)
                {
                    SetOptionOnRecommendation(subOption);
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

        internal List<string> GetContributorCredits()
        {
            var contributors = new List<string>();
            contributors.Add("Mellin - 3D modeling");
            contributors.Add("Jenya - 3D modeling, testing");
            contributors.Add("Audemus - Textures");
            contributors.Add("JadeBarker - Technical assistance");
            contributors.Add("StrifeTheHistorian - Psychological profiles");
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
        }

        private async void UninstallDLCComponent()
        {
            var dlcModPath = MERFileSystem.GetDLCModPath();
            if (Directory.Exists(dlcModPath))
            {
                var pd = await this.ShowProgressAsync("Deleting DLC component", "Please wait while the DLC mod component of your current randomization is deleted.");
                pd.SetIndeterminate();
                Task.Run(() =>
                    {
                        Utilities.DeleteFilesAndFoldersRecursively(dlcModPath);
                        DLCComponentInstalled = false;
                        Thread.Sleep(2000);
                    })
                    .ContinueWithOnUIThread(async x =>
                    {
                        await pd.CloseAsync();
                        await this.ShowMessageAsync("DLC component uninstalled", "The DLC component of the randomization has been uninstalled. A few files that cannot be placed into DLC may remain, you will need to repair your game to remove them.\n\nFor faster restores in the future, make a backup with an ME3Tweaks program. Mass Effect 2 Randomization uninstallation only takes a few seconds when an ME3Tweaks backup is available.");
                        CommandManager.InvalidateRequerySuggested();
                    });
            }
        }

        private bool CanUninstallDLCComponent()
        {
            var status = BackupService.GetBackupStatus(MERFileSystem.Game);
            var canUninstall = ShowUninstallButton = status != null && !status.BackedUp && DLCComponentInstalled;
            return canUninstall;
        }

        private bool CanSetupRandomizer(object obj)
        {
            return obj is RandomizationOption option && option.SetupRandomizerDelegate != null;
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

        private async void Startup()
        {
            /*string me2Path = MERUtilities.GetGamePath(allowMissing: true);

            //int installedGames = 5;
            bool me2installed = (me2Path != null);

            if (!me2installed)
            {
                Log.Error("Mass Effect 2 couldn't be found. Application will now exit.");
                await this.ShowMessageAsync("Mass Effect 2 is not installed", "Mass Effect 2 couldn't be found on this system. Mass Effect 2 Randomizer only works with legitimate, official copies of Mass Effect 2. Ensure you have run the game at least once. If you need assistance, please come to the ME3Tweaks Discord.");
                Log.Error("Exiting due to game not being found");
                Environment.Exit(1);
            }

            GameLocationTextbox.Text = $"Game Path: {me2Path}";
            Log.Information("Game is installed at " + me2Path);

            Log.Information("Detecting locale...");
            if (!MERUtilities.IsSupportedLocale())
            {
                Log.Error("Unable to detect INT locale.");
                await this.ShowMessageAsync("Mass Effect 2 unsupported locale", "Mass Effect 2 Randomizer only works with INT(english) locales of the game. Your current installation locale is unsupported or could not determined (could not detect loc_int files). Mass Effect 2 Randomizer is written against the INT locale and will not work with other localizations of the game. The application will now exit. If you need assistance, please come to the ME3Tweaks Discord.");
                Log.Error("Exiting due to unsupported locale");
                Environment.Exit(1);
            }

            string path = MERUtilities.GetGameBackupPath();
            if (path != null)
            {
                BackupRestoreText = "Restore";
                BackupRestore_Button.ToolTip = "Click to restore game from " + Environment.NewLine + path;

                string testME2Installed = MERUtilities.GetGamePath();
                if (testME2Installed == null)
                {
                    Log.Error("Mass Effect detected as installed, but files are missing");
                    MetroDialogSettings settings = new MetroDialogSettings();
                    settings.NegativeButtonText = "Cancel";
                    settings.AffirmativeButtonText = "Restore";
                    MessageDialogResult result = await this.ShowMessageAsync("Mass Effect detected, but files are missing", "Mass Effect's location was successfully detected, but the game files were not found. This may be due to a failed restore. Would you like to restore your game to the original location?", MessageDialogStyle.AffirmativeAndNegative, settings);
                    if (result == MessageDialogResult.Affirmative)
                    {
                        Log.Error("Mass Effect being restored by user");
                        //RestoreGame();
                    }
                    else
                    {
                        Log.Error("Exiting due to game not being found");
                        Environment.Exit(1);
                    }
                }
            }
            else
            {
                if (me2installed)
                {
                    BackupRestoreText = "Backup";
                    BackupRestore_Button.ToolTip = "Click to backup game";
                }
            }*/
        }

        public string BackupRestoreText { get; set; }

        private bool CanStartRandomization() => SeedTextBox != null && int.TryParse(SeedTextBox.Text, out var value) && value != 0 && Locations.GetTarget(MERFileSystem.Game) != null;
        private async void StartRandomization()
        {
            if (!MERUtilities.IsGameRunning(MERFileSystem.Game))
            {
                ShowProgressPanel = true;
                var randomizer = new Randomizer(this);

                AllowOptionsChanging = false;

                var op = new OptionsPackage()
                {
                    Seed = int.Parse(SeedTextBox.Text),
                    SelectedOptions = RandomizationGroups.SelectMany(x => x.Options.Where(x => x.OptionIsSelected)).ToList(),
                    UseMultiThread = UseMultiThreadRNG,
                    Reroll = PerformReroll
                };
                randomizer.Randomize(op);
            }
            else
            {
                await this.ShowMessageAsync("Mass Effect 2 is running", "Cannot randomize the game while Mass Effect 2 is running. Please close the game and try again.");
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
            Randomizer.SetupOptions(RandomizationGroups, optionStateChanging);
            ShowProgressPanel = false;
        }

        private void Logs_Click(object sender, RoutedEventArgs e)
        {
            LogUploaderFlyoutOpen = true;
        }

        private async void BackupRestore_Click(object sender, RoutedEventArgs e)
        {
            string path = BackupService.GetGameBackupPath(MERFileSystem.Game, out var isVanilla, false);
            var gameTarget = Locations.GetTarget(MERFileSystem.Game);
            if (path != null && gameTarget != null)
            {
                MetroDialogSettings settings = new MetroDialogSettings();
                settings.NegativeButtonText = "Full";
                settings.FirstAuxiliaryButtonText = "Cancel";
                settings.AffirmativeButtonText = "Quick";
                settings.DefaultButtonFocus = MessageDialogResult.Affirmative;
                MessageDialogResult result = await this.ShowMessageAsync("Select restore mode", "Select which restore mode you would like to perform:\n\nQuick: Restores basegame files modifiable by Mass Effect 2 Randomizer, deletes the DLC mod component\n\nFull: Deletes entire game installation and restores the backup in its place. Fully resets the game to the backup state", MessageDialogStyle.AffirmativeAndNegativeAndSingleAuxiliary, settings);
                if (result == MessageDialogResult.FirstAuxiliary)
                {
                    // Do nothing. User canceled
                }
                else
                {
                    RestoreController.StartRestore(this, result == MessageDialogResult.Affirmative);
                }
            }
            else if (gameTarget == null)
            {
                await this.ShowMessageAsync($"{MERFileSystem.Game.ToGameName()} not found", $"{MERFileSystem.Game.ToGameName()} was not found, and as such, cannot be restored by {MERFileSystem.Game.ToGameName()} Randomizer. Repair your game using Steam, Origin, or your DVD, or restore your backup using ME3Tweaks Mod Manager.");
            }
        }

        private void DebugCloseDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            //DiagnosticsFlyoutOpen = false;
        }

        private void Button_FirstTimeRunDismiss_Click(object sender, RoutedEventArgs e)
        {
            RegistryHandler.WriteRegistrySettingBool(SETTING_FIRSTRUN, true);
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

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnLogUploaderFlyoutOpenChanged()
        {
            if (LogUploaderFlyoutOpen)
            {
                LogsAvailableForUpload.ReplaceAll(LogCollector.GetLogsList());
                SelectedLogForUpload = LogsAvailableForUpload.FirstOrDefault();
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
                //ProgressIndeterminate = true;
                //GameTarget target = GameChosen != null ? Locations.GetTarget(GameChosen.Value) : null;
                StringBuilder logUploadText = new StringBuilder();

                string logText = "";
                //if (target != null)
                //{
                //    logUploadText.Append("[MODE]diagnostics\n"); //do not localize
                //    logUploadText.Append(LogCollector.PerformDiagnostic(target, FullDiagChosen,
                //            x => DiagnosticStatusText = x,
                //            x =>
                //            {
                //                ProgressIndeterminate = false;
                //                ProgressValue = x;
                //            },
                //            () => ProgressIndeterminate = true));
                //    logUploadText.Append("\n"); //do not localize
                //}

                if (SelectedLogForUpload != null)
                {
                    logUploadText.Append("[MODE]logs\n"); //do not localize
                    logUploadText.AppendLine(LogCollector.CollectLogs(SelectedLogForUpload.filepath));
                    logUploadText.Append("\n"); //do not localize
                }

                //DiagnosticStatusText = "Uploading to log viewing service";
                //ProgressIndeterminate = true;
                var response = LogUploader.UploadLog(logUploadText.ToString(), "https://me3tweaks.com/masseffect2randomizer/logservice/logupload");
                if (response.uploaded)
                {
                    var DiagnosticResultText = response.result;
                    if (response.result.StartsWith("http"))
                    {
                        Utilities.OpenWebPage(response.result);
                    }
                }


                if (!response.uploaded || QuickFixHelper.IsQuickFixEnabled(QuickFixHelper.QuickFixName.ForceSavingLogLocally))
                {
                    // Upload failed.
                    var GeneratedLogPath = Path.Combine(LogCollector.LogDir, $"FailedLogUpload_{DateTime.Now.ToString("s").Replace(":", ".")}.txt");
                    File.WriteAllText(GeneratedLogPath, logUploadText.ToString());
                }

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
    }
}