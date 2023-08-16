using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LibXboxOne
{
    public static class AppDirs
    {
        internal static string GetApplicationBaseDirectory()
        {
            return Environment.GetFolderPath(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    /*
                     * Windows
                     * Result: C:\Users\<username>\AppData\Local
                     */
                    ? Environment.SpecialFolder.LocalApplicationData
                    /*
                     * Mac OS X
                     * Result: /Users/<username>/.config
                     *
                     * Linux
                     * Result: /home/<username>/.config
                     */
                    : Environment.SpecialFolder.ApplicationData);
        }

        public static string GetApplicationConfigDirectory(string appName)
        {
            /*
             * Windows: C:\Users\<username>\AppData\Local\<appName>
             * Linux: /home/<username>/.config/<appName>
             * Mac OS X: /Users/<username>/.config/<appName>
             */
            return Path.Combine(GetApplicationBaseDirectory(), appName);
        }
    }
}