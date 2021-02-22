﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MassEffectRandomizer.Classes;
using MassEffectRandomizer.Classes.Updater;
using ME2Randomizer.Classes;
using ME2Randomizer.Classes.Randomizers;
using ME2Randomizer.Classes.Randomizers.ME2.Misc;
using ME2Randomizer.DebugTools;
using ME2Randomizer.ui;
using ME3ExplorerCore;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using Microsoft.Win32;
using Octokit;
using Serilog;

namespace ME2Randomizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        private static string FaqLink = "https://me3tweaks.com/masseffectrandomizer/faq";
        public static bool DEBUG_LOGGING { get; internal set; }

        public enum RandomizationMode
        {
            ERandomizationMode_SelectAny = 0,
            ERandomizationMode_Common = 1,
            ERandomizationMode_Screed = 2
        }
        public bool UseMultiThreadRNG { get; set; } = true;

        public bool ShowProgressPanel { get; set; }
        public RandomizationMode SelectedRandomizeMode { get; set; }

        public ObservableCollectionExtended<ImageCredit> ImageCredits { get; } = new ObservableCollectionExtended<ImageCredit>();
        public ObservableCollectionExtended<string> ContributorCredits { get; } = new ObservableCollectionExtended<string>();
        public ObservableCollectionExtended<LibraryCredit> LibraryCredits { get; } = new ObservableCollectionExtended<LibraryCredit>();

        public void OnSelectedRandomizeModeChanged()
        {
            UpdateCheckboxSettings();
        }

        /// <summary>
        /// The list of options shown
        /// </summary>
        public ObservableCollectionExtended<RandomizationGroup> RandomizationGroups { get; } = new ObservableCollectionExtended<RandomizationGroup>();
        public bool AllowOptionsChanging { get; set; } = true;
        public int CurrentProgressValue { get; set; }
        public string CurrentOperationText { get; set; }
        public double ProgressBar_Bottom_Min { get; set; }
        public double ProgressBar_Bottom_Max { get; set; }
        public bool ProgressBarIndeterminate { get; set; }
        private Randomizer randomizer;
        ProgressDialogController updateprogresscontroller;

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Utilities.OpenWebPage(((Hyperlink)sender).NavigateUri.AbsoluteUri);
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
            Random random = new Random();
            var preseed = random.Next();
            ImageCredits.ReplaceAll(ImageCredit.LoadImageCredits("imagecredits.txt", false));
            ContributorCredits.ReplaceAll(GetContributorCredits());
            LibraryCredits.ReplaceAll(LibraryCredit.LoadLibraryCredits("librarycredits.txt"));
            ProgressBar_Bottom_Max = 100;
            ProgressBar_Bottom_Min = 0;
            ShowProgressPanel = true;
            LoadCommands();
            InitializeComponent();

#if DEBUG
            SeedTextBox.Text = 529572808.ToString();
#else
            SeedTextBox.Text = preseed.ToString();
#endif
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            TextBlock_AssemblyVersion.Text = "Version " + version;
            Title += " " + version;
            SelectedRandomizeMode = RandomizationMode.ERandomizationMode_SelectAny;
            PerformUpdateCheck();
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

        private List<string> GetContributorCredits()
        {
            var contributors = new List<string>();
            contributors.Add("Mellin - 3D modeling");
            contributors.Add("Jenya - 3D modeling");
            contributors.Add("Audemus - Textures");
            contributors.Add("JadeBarker - Technical assistance");

            return contributors;
        }

        #region Commands
        public GenericCommand StartRandomizationCommand { get; set; }

        private void LoadCommands()
        {
            StartRandomizationCommand = new GenericCommand(StartRandomization, CanStartRandomization);
        }

        #endregion







        public async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //var files = File.ReadAllLines(@"C:\users\mgame\desktop\fileslist.txt");
            //var dest = @"E:\Documents\BioWare\Mass Effect 2\BIOGame\Movies";
            //var src = @"E:\Documents\BioWare\Mass Effect 2\BIOGame\Movies\load_f01.bik";
            //foreach (var file in files)
            //{
            //    var rdest = Path.Combine(dest, file);
            //    if (File.Exists(rdest)) continue;
            //    File.Copy(src, rdest);
            //}

            //List<(string, string)> strs = new List<(string, string)>();
            //var fils = Directory.GetFiles(@"D:\Origin Games\Mass Effect 2\BioGame\CookedPC\BIOGame_INT");
            //foreach (var fil in fils)
            //{
            //    var x = XDocument.Load(fil);
            //    foreach (var str in x.Descendants("String"))
            //    {
            //        if (str.Value.Contains("plague", StringComparison.InvariantCultureIgnoreCase))
            //        {
            //            strs.Add((str.Attribute("id").ToString(), str.Value));
            //        }
            //    }
            //}

            //foreach (var str in strs)
            //{
            //    Debug.WriteLine($"<String id=\"{str.Item1}\">{str.Item2}</String>");
            //}


            string me2Path = Utilities.GetGamePath(allowMissing: true);

            //int installedGames = 5;
            bool me2installed = (me2Path != null);

            if (!me2installed)
            {
                Log.Error("Mass Effect 2 couldn't be found. Application will now exit.");
                await this.ShowMessageAsync("Mass Effect 2 is not installed", "Mass Effect 2 couldn't be found on this system. Mass Effect 2 Randomizer only works with legitimate, official copies of Mass Effect 2. Ensure you have run the game at least once. If you need assistance, please come to the ME3Tweaks Discord.");
                Log.Error("Exiting due to game not being found");
                Environment.Exit(1);
            }

            GameLocationTextbox.Text = "Game Path: " + me2Path;
            Log.Information("Game is installed at " + me2Path);

            Log.Information("Detecting locale...");
            if (!Utilities.IsSupportedLocale())
            {
                Log.Error("Unable to detect INT locale.");
                await this.ShowMessageAsync("Mass Effect 2 unsupported locale", "Mass Effect 2 Randomizer only works with INT(english) locales of the game. Your current installation locale is unsupported or could not determined (could not detect loc_int files). Mass Effect 2 Randomizer is written against the INT locale and will not work with other localizations of the game. The application will now exit. If you need assistance, please come to the ME3Tweaks Discord.");
                Log.Error("Exiting due to unsupported locale");
                Environment.Exit(1);
            }

            string path = Utilities.GetGameBackupPath();
            if (path != null)
            {
                BackupRestoreText = "Restore";
                BackupRestore_Button.ToolTip = "Click to restore game from " + Environment.NewLine + path;

                string testME2Installed = Utilities.GetGamePath();
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
            }
        }

        public string BackupRestoreText { get; set; }
        private bool CanStartRandomization() => SeedTextBox != null && int.TryParse(SeedTextBox.Text, out var value) && value != 0;
        private async void StartRandomization()
        {
            if (!Utilities.isGameRunning())
            {
                ShowProgressPanel = true;
                randomizer = new Randomizer(this);

                AllowOptionsChanging = false;

                var op = new OptionsPackage()
                {
                    Seed = int.Parse(SeedTextBox.Text),
                    SelectedOptions = RandomizationGroups.SelectMany(x => x.Options.Where(x => x.OptionIsSelected)).ToList(),
                    UseMultiThread = UseMultiThreadRNG
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

        private async void PerformUpdateCheck()
        {
            Log.Information("Checking for application updates from github");
            ProgressBarIndeterminate = true;
            CurrentOperationText = "Checking for application updates";
            var versInfo = Assembly.GetEntryAssembly().GetName().Version;
            var client = new GitHubClient(new ProductHeaderValue("MassEffect2Randomizer"));
            try
            {
                int myReleaseAge = 0;
                var releases = await client.Repository.Release.GetAll("Mgamerz", "MassEffect2Randomizer");
                if (releases.Count > 0)
                {
                    Log.Information("Fetched application releases from github");

                    //The release we want to check is always the latest, so [0]
                    Release latest = null;
                    Version latestVer = new Version("0.0.0.0");
                    foreach (Release r in releases)
                    {
                        if (r.Assets.Count > 0)
                        {
                            Version releaseVersion = new Version(r.TagName);
                            if (versInfo < releaseVersion)
                            {
                                myReleaseAge++;
                            }

                            if (releaseVersion > latestVer)
                            {
                                latest = r;
                                latestVer = releaseVersion;
                            }
                        }
                    }

                    if (latest != null)
                    {
                        Log.Information("Latest available: " + latest.TagName);
                        Version releaseName = new Version(latest.TagName);
                        if (versInfo < releaseName && latest.Assets.Count > 0)
                        {
                            bool upgrade = false;
                            bool canCancel = true;
                            Log.Information("Latest release is applicable to us.");

                            string versionInfo = "";
                            int daysAgo = (DateTime.Now - latest.PublishedAt.Value).Days;
                            string ageStr = "";
                            if (daysAgo == 1)
                            {
                                ageStr = "1 day ago";
                            }
                            else if (daysAgo == 0)
                            {
                                ageStr = "today";
                            }
                            else
                            {
                                ageStr = daysAgo + " days ago";
                            }

                            versionInfo += "\nReleased " + ageStr;
                            MetroDialogSettings mds = new MetroDialogSettings();
                            mds.AffirmativeButtonText = "Update";
                            mds.NegativeButtonText = "Later";
                            mds.DefaultButtonFocus = MessageDialogResult.Affirmative;

                            string message = "Mass Effect Randomizer " + releaseName + " is available. You are currently using version " + versInfo + "." + versionInfo;
                            UpdateAvailableDialog uad = new UpdateAvailableDialog(message, latest.Body, this);
                            await this.ShowMetroDialogAsync(uad, mds);
                            await uad.WaitUntilUnloadedAsync();
                            upgrade = uad.wasUpdateAccepted();

                            if (upgrade)
                            {
                                Log.Information("Downloading update for application");

                                //there's an update
                                message = "Downloading update...";

                                updateprogresscontroller = await this.ShowProgressAsync("Downloading update", message, canCancel);
                                updateprogresscontroller.SetIndeterminate();
                                WebClient downloadClient = new WebClient();

                                downloadClient.Headers["Accept"] = "application/vnd.github.v3+json";
                                downloadClient.Headers["user-agent"] = "MassEffectRandomizer";
                                string temppath = Path.GetTempPath();
                                int downloadProgress = 0;
                                downloadClient.DownloadProgressChanged += (s, e) =>
                                {
                                    if (downloadProgress != e.ProgressPercentage)
                                    {
                                        Log.Information("Program update download percent: " + e.ProgressPercentage);
                                    }

                                    string downloadedStr = FileSize.FormatSize(e.BytesReceived) + " of " + FileSize.FormatSize(e.TotalBytesToReceive);
                                    updateprogresscontroller.SetMessage(message + "\n\n" + downloadedStr);

                                    downloadProgress = e.ProgressPercentage;
                                    updateprogresscontroller.SetProgress((double)e.ProgressPercentage / 100);
                                };
                                updateprogresscontroller.Canceled += async (s, e) =>
                                {
                                    if (downloadClient != null)
                                    {
                                        Log.Information("Application update was in progress but was canceled.");
                                        downloadClient.CancelAsync();
                                        await updateprogresscontroller.CloseAsync();
                                    }
                                };
                                downloadClient.DownloadFileCompleted += UpdateDownloadCompleted;
                                string downloadPath = Path.Combine(temppath, "MassEffectRandomizer-Update.exe");
                                //DEBUG ONLY
                                Uri downloadUri = new Uri(latest.Assets[0].BrowserDownloadUrl);
                                downloadClient.DownloadFileAsync(downloadUri, downloadPath, new KeyValuePair<ProgressDialogController, string>(updateprogresscontroller, downloadPath));
                            }
                            else
                            {
                                Log.Warning("Application update was declined");
                            }
                        }
                        else
                        {
                            //up to date
                            CurrentOperationText = "Application up to date";
                        }
                    }
                }
                else
                {
                    Log.Information("No releases found on Github");
                }
            }
            catch (Exception e)
            {
                Log.Error("Error checking for update: " + e);
            }

            // Load ME3ExplorerCore
            ME3ExplorerCoreLib.InitLib(TaskScheduler.FromCurrentSynchronizationContext());
            MEPackageHandler.GlobalSharedCacheEnabled = false;
            Randomizer.SetupOptions(RandomizationGroups, optionStateChanging);
            ShowProgressPanel = false;
        }


        protected override void OnClosing(CancelEventArgs e)
        {
            // close all active threads
            //if (randomizer != null && randomizer.Busy){
            //    Environment.Exit(0); //force close threads
            //}
            // Let app close itself
        }

        private void UpdateDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            Log.Information("Update downloaded - rebooting to new downloaded file, in update mode");
            string temppath = Path.GetTempPath();
            string exe = Path.Combine(temppath, "MassEffectRandomizer-Update.exe");

            // FIX FOR .NET CORE SINGLE FILE
            Assembly assembly = Assembly.GetExecutingAssembly();
            string exePath = assembly.Location;

            string args = "--update-dest-path \"" + exePath + "\"";
            Utilities.runProcess(exe, args, true);
            while (true)
            {
                try
                {
                    Environment.Exit(0);
                }
                catch (TaskCanceledException)
                {
                    //something to do with how shutting down works.
                }
            }
        }

        private void Logs_Click(object sender, RoutedEventArgs e)
        {
            //LogUploaderFlyoutOpen = true;
        }

        private void Diagnostics_Click(object sender, RoutedEventArgs e)
        {
            //DiagnosticsFlyoutOpen = true;
        }

        private async void BackupRestore_Click(object sender, RoutedEventArgs e)
        {
            string path = Utilities.GetGameBackupPath();
            if (path != null)
            {
                MetroDialogSettings settings = new MetroDialogSettings();
                settings.NegativeButtonText = "Cancel";
                settings.AffirmativeButtonText = "Restore";
                MessageDialogResult result = await this.ShowMessageAsync("Restoring Mass Effect 2 from backup", "Restoring Mass Effect 2 will wipe out the current installation and put your game back to the state when you backed it up. Are you sure you want to do this?", MessageDialogStyle.AffirmativeAndNegative, settings);
                if (result == MessageDialogResult.Affirmative)
                {
                    //RestoreGame();
                }
            }
        }

        private void DebugCloseDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            //DiagnosticsFlyoutOpen = false;
        }

        private void Button_FirstTimeRunDismiss_Click(object sender, RoutedEventArgs e)
        {
            //FirstRunFlyoutOpen = false;
            bool? hasShownFirstRun = Utilities.GetRegistrySettingBool("HasRunFirstRun");
            Utilities.WriteRegistryKey(Registry.CurrentUser, App.REGISTRY_KEY, "HasRunFirstRun", true);
            //PerformPostStartup();
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
            Utilities.OpenWebPage(FaqLink);
        }

        private void DiscordButton_Click(object sender, RoutedEventArgs e)
        {
            //ME3Tweaks Discord
            Utilities.OpenWebPage(App.DISCORD_INVITE_LINK);
        }

        private void DataFinder_Click(object sender, RoutedEventArgs e)
        {
            DataFinder df = new DataFinder(this);
        }

        private void Combobox_LogSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void LogUploaderFlyout_IsOpenChanged(object sender, RoutedEventArgs e)
        {

        }

        private void Button_CancelLog_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_SelectLog_Click(object sender, RoutedEventArgs e)
        {

        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void MemAnalyzer_Click(object sender, RoutedEventArgs e)
        {
            new MemoryAnalyzerUI().Show();
        }
    }
}