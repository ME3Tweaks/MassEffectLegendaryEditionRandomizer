﻿using System;
using System.Collections.Generic;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

namespace RandomizerUI.Classes.Controllers
{
    /// <summary>
    /// Maps the internal telemetry calls to Microsoft AppCenter calls
    /// </summary>
    public static class TelemetryController
    {
        public static void TrackEvent(string eventName, Dictionary<string, string> eventData)
        {
            Analytics.TrackEvent(eventName, eventData);
        }

        public static void TrackError(Exception exception, Dictionary<string, string> data)
        {
            Crashes.TrackError(exception, data);
        }
    }
}