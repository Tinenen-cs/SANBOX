using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Linq;
#endif

namespace Mossmere
{
    public static class WindowsGameDisplay
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void StartFullScreen()
        {
            // Normal launches always fill the monitor, including machines with old windowed preferences.
            // Automated content checks can retain a fixed test resolution; the display check opts in.
            if(Application.isBatchMode && !Environment.GetCommandLineArgs().Contains("-verifyFullscreen"))return;
            Screen.SetResolution(Display.main.systemWidth,Display.main.systemHeight,FullScreenMode.FullScreenWindow);
        }
#endif
    }
}
