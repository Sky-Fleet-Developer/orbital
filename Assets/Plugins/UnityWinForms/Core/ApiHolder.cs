﻿using UnityWinForms.Core.API;

namespace UnityWinForms.Core
{
    /// <summary>
    /// Replace with your own implementation if needed.
    /// </summary>
    public static class ApiHolder
    {
        public static IApiGraphics Graphics { get; set; }
        public static IApiInput Input { get; set; }
        public static IApiSystem System { get; set; }
        public static IApiTiming Timing { get; set; }
    }
}