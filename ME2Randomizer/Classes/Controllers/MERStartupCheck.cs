﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using ME3TweaksCore;
using ME3TweaksCore.Helpers;
using Microsoft.Win32;
using NickStrupat;
using Randomizer.MER;

namespace RandomizerUI.Classes.Controllers
{
    class MERStartupCheck
    {
        /// <summary>
        /// Handles application framework startup checks
        /// </summary>
        /// <param name="messageCallback"></param>
        public static void PerformStartupCheck(Action<string, string> messageCallback, Action<string> setStartupMessageText)
        {
            PerformOperatingSystemCheck(messageCallback);
            PerformRAMCheck(messageCallback);
            PerformWriteCheck(messageCallback);
            PerformUACCheck();
        }

        private static void PerformOperatingSystemCheck(Action<string, string> messageCallback)
        {
            var os = Environment.OSVersion.Version;
            if (os < ME3TweaksCoreLib.MIN_SUPPORTED_OS)
            {
                MERUILog.Fatal($@"This operating system version is below the minimum supported version: {os}, minimum supported: {ME3TweaksCoreLib.MIN_SUPPORTED_OS}");
                messageCallback?.Invoke("This operating system is not supported", $"{MERUI.GetRandomizerName()} is not supported on this operating system. The application may not work. To ensure application compatibility or receive support from ME3Tweaks, upgrade to a version of Windows that is supported by Microsoft.");
            }
        }

        private static void PerformRAMCheck(Action<string, string> messageCallback)
        {
            var ramAmountsBytes = MUtilities.GetInstalledRamAmount();
            var installedRamGB = ramAmountsBytes * 1.0d / (2 ^ 30);
            if (ramAmountsBytes > 0 && installedRamGB < 10)
            {
                messageCallback?.Invoke("System memory is less than 8 GB", "Randomization can use significant amounts of memory (up to 3GB) in multithreaded mode. It is recommended that you disable multithreaded randomization if your system has less than 8GB of memory. This will increase randomization time but will reduce the memory required to randomize.");
            }
#if WINDOWS
            //Check pagefile
            try
            {
                //Current
                var pageFileLocations = new List<string>();
                using (var query = new ManagementObjectSearcher("SELECT Caption,AllocatedBaseSize FROM Win32_PageFileUsage"))
                {
                    foreach (ManagementBaseObject obj in query.Get())
                    {
                        string pagefileName = (string)obj.GetPropertyValue("Caption");
                        MERUILog.Information("Detected pagefile: " + pagefileName);
                        pageFileLocations.Add(pagefileName.ToLower());
                    }
                }

                //Max
                using (var query = new ManagementObjectSearcher("SELECT Name,MaximumSize FROM Win32_PageFileSetting"))
                {
                    foreach (ManagementBaseObject obj in query.Get())
                    {
                        string pagefileName = (string)obj.GetPropertyValue("Name");
                        uint max = (uint)obj.GetPropertyValue("MaximumSize");
                        if (max > 0)
                        {
                            // Not system managed
                            pageFileLocations.RemoveAll(x => Path.GetFullPath(x).Equals(Path.GetFullPath(pagefileName)));
                            MERUILog.Error($"Pagefile has been modified by the end user. The maximum page file size on {pagefileName} is {max} MB. Does this user **actually** know what capping a pagefile does?");
                        }
                    }
                }

                if (pageFileLocations.Any())
                {
                    MERUILog.Information("We have a usable system managed page file - OK");
                }
                else
                {
                    MERUILog.Error("We have no uncapped or available pagefiles to use! Very high chance application will run out of memory");
                    messageCallback?.Invoke($"Pagefile is off or size has been capped", $"The system pagefile (virtual memory) settings are not currently managed by Windows, or the pagefile is off. Mass Effect 2 Randomizer uses significant amounts of memory and will crash if the system runs low on memory. You should always leave page files managed by Windows.");
                }
            }
            catch (Exception e)
            {
                MERUILog.Exception(e, "Unable to check pagefile settings:");
            }
#endif
        }

        private static bool PerformWriteCheck(Action<string, string> messageCallback)
        {
            MERUILog.Information("Performing write check on game directory");
            var target = TargetHandler.GetTarget();
            try
            {
                List<string> directoriesToGrant = new List<string>();
                // Check all folders are writable
                bool isFullyWritable = true;
                var testDirectories = Directory.GetDirectories(target.TargetPath, "*", SearchOption.AllDirectories);
                foreach (var d in testDirectories)
                {
                    isFullyWritable &= MUtilities.IsDirectoryWritable(d);
                }

                bool isAdmin = MUtilities.IsAdministrator();

                if (directoriesToGrant.Any())
                {
                    string args = "";
                    // Some directories not writable
                    foreach (var dir in directoriesToGrant)
                    {
                        if (args != "")
                        {
                            args += " ";
                        }

                        args += $"\"{dir}\"";
                    }

                    args = $"\"{System.Security.Principal.WindowsIdentity.GetCurrent().Name}\" {args}";

                    throw new Exception("not implemented.");
                    /*
                    var permissionsGranterExe = Path.Combine(Locations.ResourcesDir, "Binaries", "PermissionsGranter.exe");

                    //need to run write permissions program
                    if (isAdmin)
                    {
                        int result = Utilities.RunProcess(permissionsGranterExe, args, true, true, true, true);
                        if (result == 0)
                        {
                            MERUILog.Information("Elevated process returned code 0, directories are hopefully writable now.");
                            return true;
                        }
                        else
                        {
                            MERUILog.Error("Elevated process returned code " + result +
                                      ", directories probably aren't writable.");
                            return false;
                        }
                    }
                    else
                    {
                        string message = $"The Mass Effect 2 game folder is not writeable by your user account. Mass Effect 2 Randomizer will attempt to grant access to these folders/registry with the PermissionsGranter.exe program:\n";
                        
                        foreach (string str in directoriesToGrant)
                        {
                            message += "\n" + str;
                        }

                        messageCallback?.Invoke("Write permissions required for modding", message);
                        int result = Utilities.RunProcess(permissionsGranterExe, args, true, true, true, true);
                        if (result == 0)
                        {
                            MERUILog.Information("Elevated process returned code 0, directories are hopefully writable now.");
                            return true;
                        }
                        else
                        {
                            MERUILog.Error($"Elevated process returned code {result}, directories probably aren't writable.");
                            return false;
                        }
                    }
                    */
                }
            }
            catch (Exception e)
            {
                MERUILog.Exception(e, "Error checking for write privileges. This may be a significant sign that an installed game is not in a good state.");
                return false;
            }
            return true;
        }

        private static void PerformUACCheck()
        {
            bool isAdmin = MUtilities.IsAdministrator();

            //Check if UAC is off
            bool uacIsOn = true;
            string softwareKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";

            int? value = (int?)Registry.GetValue(softwareKey, "EnableLUA", null);
            if (value != null)
            {
                uacIsOn = value > 0;
                MERUILog.Information("UAC is on: " + uacIsOn);
            }
            if (isAdmin && uacIsOn)
            {
                MERUILog.Warning("This session is running as administrator.");
                //await this.ShowMessageAsync($"{Utilities.GetAppPrefixedName()} Installer should be run as standard user", $"Running {Utilities.GetAppPrefixedName()} Installer as an administrator will disable drag and drop functionality and may cause issues due to the program running in a different user context. You should restart the application without running it as an administrator unless directed by the developers.");
            }
        }

        /// <summary>
        /// Gets the amount of installed memory in bytes
        /// </summary>
        /// <returns></returns>
        public static ulong GetInstalledRamAmount()
        {
            var computerInfo = new ComputerInfo();
            return computerInfo.TotalPhysicalMemory;
        }
    }
}
