﻿using Randomizer.MER;

namespace RandomizerUI.Classes
{
    internal class MERUI
    {
        /// <summary>
        /// Returns randomizer name for UI display
        /// </summary>
        /// <returns></returns>
        public static string GetRandomizerName()
        {
            return MERUtilities.GetGameUIName(true);
        }
    }
}
