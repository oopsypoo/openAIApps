using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace openAIApps.Native
{
    /// <summary>
    /// Provides functionality to display the Windows "Open With" dialog for selecting an application to open a
    /// specified file.
    /// </summary>
    /// <remarks>This class enables integration with the standard Windows "Open With" dialog, allowing users
    /// to choose an application to open a file at runtime. It is intended for use in desktop applications that require
    /// user-driven file association selection.</remarks>
    class ShellLauncher
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHOpenWithDialog(IntPtr hwndParent, ref OPENASINFO poainfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OPENASINFO
        {
            public string pcszFile;
            public string pcszClass;
            public uint oaInFlags;
        }

        // Flags to tell Windows to actually execute the app once selected
        private const uint OAIF_ALLOW_REGISTRATION = 0x01;
        private const uint OAIF_EXEC = 0x04;

        public static void ShowOpenWithDialog(Window parentWindow, string filePath)
        {
            OPENASINFO info = new OPENASINFO();
            info.pcszFile = filePath;
            info.pcszClass = null;
            info.oaInFlags = OAIF_ALLOW_REGISTRATION | OAIF_EXEC;

            // This gets the "Owner" handle so the dialog doesn't get lost behind your app
            IntPtr hwnd = new WindowInteropHelper(parentWindow).Handle;
            SHOpenWithDialog(hwnd, ref info);
        }
    }
}
