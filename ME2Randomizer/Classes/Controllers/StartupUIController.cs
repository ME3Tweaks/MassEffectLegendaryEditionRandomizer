

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using MahApps.Metro.Controls.Dialogs;
using ME3TweaksCore;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Misc;
using ME3TweaksCore.Targets;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Randomizer.MER;
using Randomizer.Randomizers;
using RandomizerUI.Classes.Telemetry;
using Serilog;

namespace RandomizerUI.Classes.Controllers
{
    public class StartupUIController
    {
        private static bool telemetryStarted = false;

        private static void startTelemetry()
        {
            initAppCenter();
            AppCenter.SetEnabledAsync(true);
        }

        private static void stopTelemetry()
        {
            AppCenter.SetEnabledAsync(false);
        }

        private static void initAppCenter()
        {
#if !DEBUG
            if (APIKeys.HasAppCenterKey && !telemetryStarted)
            {
                Crashes.GetErrorAttachments = (ErrorReport report) =>
                {
                    var attachments = new List<ErrorAttachmentLog>();
                    // Attach some text.
                    string errorMessage = "Randomizer has crashed! This is the exception that caused the crash:\n" + report.StackTrace;
                    MERUILog.Fatal(errorMessage);
                    MERUILog.Error("Note that this exception may appear to occur in a follow up boot due to how appcenter works");
                    string log = LogCollector.CollectLatestLog(MCoreFilesystem.GetLogDir(), false);
                    if (log.Length < 1024 * 1024 * 7)
                    {
                        attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, "crashlog.txt"));
                    }
                    else
                    {
                        //Compress log
                        var compressedLog = LZMA.CompressToLZMAFile(Encoding.UTF8.GetBytes(log));
                        attachments.Add(ErrorAttachmentLog.AttachmentWithBinary(compressedLog, "crashlog.txt.lzma", "application/x-lzma"));
                    }

                    return attachments;
                };
                AppCenter.Start(APIKeys.AppCenterKey, typeof(Analytics), typeof(Crashes));
            }
#else
            if (!APIKeys.HasAppCenterKey)
            {
                Debug.WriteLine(" >>> This build is missing an API key for AppCenter!");
            }
            else
            {
                Debug.WriteLine("This build has an API key for AppCenter");
            }
#endif
            telemetryStarted = true;
        }

        public static async void BeginFlow(MainWindow window)
        {
            // PRE LIBRARY LOAD
            //RegistryHandler.RegistrySettingsPath = @"HKEY_CURRENT_USER\Software\MassEffect2Randomizer";
            //RegistryHandler.CurrentUserRegistrySubpath = @"Software\MassEffect2Randomizer";
            LegendaryExplorerCoreLib.SetSynchronizationContext(TaskScheduler.FromCurrentSynchronizationContext());

            try
            {
                // This is in a try catch because this is a critical no-crash zone that is before launch
                window.Title = $"{MERUI.GetRandomizerName()} {MLibraryConsumer.GetAppVersion()}";
                startTelemetry();
            }
            catch { }

            if (MLibraryConsumer.GetExecutablePath().StartsWith(Path.GetTempPath(), StringComparison.InvariantCultureIgnoreCase))
            {
                // Running from temp! This is not allowed
                await window.ShowMessageAsync("Cannot run from temp directory", $"{MERUI.GetRandomizerName()} cannot be run from the system's Temp directory. If this executable was run from within an archive, it needs to be extracted first.");
                Environment.Exit(1);
            }

            var pd = await window.ShowProgressAsync("Starting up", $"{MERUI.GetRandomizerName()} is starting up. Please wait.");
            pd.SetIndeterminate();
            NamedBackgroundWorker bw = new NamedBackgroundWorker("StartupThread");
            bw.DoWork += (a, b) =>
            {
                ME3TweaksCoreLibInitPackage package = new ME3TweaksCoreLibInitPackage()
                {
                    TrackErrorCallback = TelemetryController.TrackError,
                    TrackEventCallback = TelemetryController.TrackEvent,
                    CreateLogger = MERLog.CreateLogger,
                    RunOnUiThreadDelegate = RunOnUIThread,
                    LoadAuxiliaryServices = true,
                    AuxiliaryCombinedOnlineServicesEndpoint = new FallbackLink()
                    {
                        // These are reversed as we don't really need up-to-date info for randomizer. It does not change day over day like M3 can.
                        FallbackURL = @"https://me3tweaks.com/modmanager/services/combinedservicesfetch",
                        MainURL = @"https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/staticfiles/liveservices/services/combinedservices.json",
                    },
                    LECPackageSaveFailedCallback = x => MERLog.Error($@"Failed to save package: {x}"),
                    PropertyDatabasesToLoad = new[] { MERFileSystem.Game },
                    AllowedSigners = new[] { new BuildHelper.BuildSigner() { SigningName = "Michael Perez", DisplayName = "ME3Tweaks" } },
                    LoadBuildInfo = true,
                    BetaMode = true // Use Beta ASIs
                };


                // Initialize core libraries
                ME3TweaksCoreLib.Initialize(package);
                window.SetupCopyrightString(); // We should now have loaded this

                // Logger is now available

                // Setup the InteropPackage for the update check
                #region Update interop
                CancellationTokenSource ct = new CancellationTokenSource();

                AppUpdateInteropPackage interopPackage = new AppUpdateInteropPackage()
                {
                    // TODO: UPDATE THIS
                    GithubOwner = MERUpdater.GetGithubOwner(),
                    GithubReponame = MERUpdater.GetGithubRepoName(),
                    UpdateAssetPrefix = MERUpdater.GetGithubAssetPrefix(),
                    UpdateFilenameInArchive = MERUpdater.GetExpectedExeName(),
                    ShowUpdatePromptCallback = (title, text, updateButtonText, declineButtonText) =>
                    {
                        bool response = false;
                        object syncObj = new object();
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            if (Application.Current.MainWindow is MainWindow mw)
                            {
                                var result = await mw.ShowMessageAsync(title, text, MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings()
                                {
                                    AffirmativeButtonText = updateButtonText,
                                    NegativeButtonText = declineButtonText,
                                    DefaultButtonFocus = MessageDialogResult.Affirmative
                                });
                                response = result == MessageDialogResult.Affirmative;
                                lock (syncObj)
                                {
                                    Monitor.Pulse(syncObj);
                                }
                            }
                        });
                        lock (syncObj)
                        {
                            Monitor.Wait(syncObj);
                        }
                        return response;
                    },
                    ShowUpdateProgressDialogCallback = (title, initialmessage, canCancel) =>
                    {
                        // We don't use this as we are already in a progress dialog
                        pd.SetCancelable(canCancel);
                        pd.SetMessage(initialmessage);
                        pd.SetTitle(title);
                    },
                    SetUpdateDialogTextCallback = s =>
                    {
                        pd.SetMessage(s);
                    },
                    ProgressCallback = (done, total) =>
                    {
                        pd.SetProgress(done * 1d / total);
                        pd.SetMessage($"Downloading update {FileSize.FormatSize(done)} / {FileSize.FormatSize(total)}");
                    },
                    ProgressIndeterminateCallback = () =>
                    {
                        pd.SetIndeterminate();
                    },
                    ShowMessageCallback = (title, message) =>
                    {
                        object syncObj = new object();
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            if (Application.Current.MainWindow is MainWindow mw)
                            {
                                await mw.ShowMessageAsync(title, message);
                                lock (syncObj)
                                {
                                    Monitor.Pulse(syncObj);
                                }
                            }
                        });
                        lock (syncObj)
                        {
                            Monitor.Wait(syncObj);
                        }
                    },
                    NotifyBetaAvailable = () =>
                    {
                        App.BetaAvailable = true;
                    },
                    DownloadCompleted = () =>
                    {
                        pd.SetCancelable(false);
                    },
                    cancellationTokenSource = ct,
                    ApplicationName = MERUI.GetRandomizerName(),
                    RequestHeader = MERUI.GetRandomizerName().Replace(" ", "").Replace("(", "").Replace(")", ""),
                    ForcedUpgradeMaxReleaseAge = 3,
                    TagPrefix = MERUtilities.GetRandomizerShortName() + "-", // LE1R-,LE2R-,LE3R- are tag prefixes we will use
                };

                #endregion

                pd.SetMessage("Checking for application updates");
                pd.Canceled += (sender, args) =>
                {
                    ct.Cancel();
                };
                AppUpdater.PerformGithubAppUpdateCheck(interopPackage);

                // If user aborts download
                pd.SetCancelable(false);
                pd.SetIndeterminate();
                pd.SetTitle("Starting up");

                void setStatus(string message)
                {
                    pd.SetIndeterminate();
                    pd.SetMessage(message);
                }

                GameTarget target = null;
                try
                {
                    pd.SetMessage($"Loading {MERUI.GetRandomizerName()} framework");
                    ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(Control),
                        new FrameworkPropertyMetadata(true));
                    ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject),
                        new FrameworkPropertyMetadata(int.MaxValue));
                    MEPackageHandler.GlobalSharedCacheEnabled = false; // ME2R does not use the global shared cache.

                    TargetHandler.LoadTargets(); // Load game target

                    pd.SetMessage("Performing startup checks");
                    MERStartupCheck.PerformStartupCheck((title, message) =>
                    {
                        object o = new object();
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            if (Application.Current.MainWindow is MainWindow mw)
                            {
                                await mw.ShowMessageAsync(title, message);
                                lock (o)
                                {
                                    Monitor.Pulse(o);
                                }
                            }
                        });
                        lock (o)
                        {
                            Monitor.Wait(o);
                        }
                    }, x => pd.SetMessage(x));

                    // force initial refresh
                    Application.Current.Dispatcher.Invoke(async () =>
                    {
                        if (Application.Current.MainWindow is MainWindow mw)
                        {
                            mw.MERPeriodicRefresh(null, null);

                            // Start periodic
                            PeriodicRefresh.OnPeriodicRefresh += mw.MERPeriodicRefresh;
                            PeriodicRefresh.StartPeriodicRefresh();
                        }
                    });
                }
                catch (Exception e)
                {
                    MERUILog.Exception(e, @"There was an error starting up the framework!");
                }

                pd.SetMessage("Preparing interface");
                Thread.Sleep(250); // This will allow this message to show up for moment so user can see it.

                Application.Current.Dispatcher.Invoke(async () =>
                {
                    if (Application.Current.MainWindow is MainWindow mw)
                    {
                        mw.SetupTargetDescriptionText();
                        mw.FinalizeInterfaceLoad();
                    }
                });
            };
            bw.RunWorkerCompleted += async (a, b) =>
                        {
                            // Post critical startup

                            Random random = new Random();
                            // LE2R does not use images like ME2R
#if __GAME1__
                            window.ImageCredits.ReplaceAll(ImageCredit.LoadImageCredits("imagecredits.txt", false));
#endif
                            window.ContributorCredits.ReplaceAll(window.GetContributorCredits());
                            window.LibraryCredits.ReplaceAll(LibraryCredit.LoadLibraryCredits("librarycredits.txt"));
                            // Todo: remove seed textbox as it's not used anymore, we don't support deterministic randomization
#if !DEBUG
                        window.SeedTextBox.Text = 529572808.ToString();
#else
                            window.SeedTextBox.Text = random.Next().ToString();
#endif
                            window.TextBlock_AssemblyVersion.Text = $"Version {MLibraryConsumer.GetAppVersion()}";

                            if (!MERSettings.GetSettingBool(ESetting.SETTING_FIRSTRUN))
                            {
                                window.FirstRunFlyoutOpen = true;
                            }
                            await pd.CloseAsync();
                        };
            bw.RunWorkerAsync();
        }

        public static object Prop { get; set; }

        private static void RunOnUIThread(Action obj)
        {
            Application.Current.Dispatcher.Invoke(obj);
        }
    }
}