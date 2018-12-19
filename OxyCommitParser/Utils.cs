﻿using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;

namespace OxyCommitParser
{
    public class ENoCore : Exception
    {
        public ENoCore() :
            base("This is not a valid xrCore library, Win32 image or there's no library at all!")
        {
        }
    }

    public class ENoEntryPoint : Exception
    {
        public ENoEntryPoint() :
            base("This xrCore library is too old, invalid or not supposed to be used as updatable!")
        {
        }
    }

    public static class Utils
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr hModule);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        delegate IntPtr GetCurrentHashDelegate();

        private static T DownloadSerializedJsonData<T>(string url) where T : class
        {
            using (var w = new WebClient())
            {
                w.Headers["User-Agent"] =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.100 Safari/537.36";

                var json_data = string.Empty;

                try
                {
                    json_data = w.DownloadString(url);
                }
                catch (Exception)
                {
                    return null;
                }

                return !string.IsNullOrEmpty(json_data) ? JsonConvert.DeserializeObject<T>(json_data) : null;
            }
        }

        public static string GetReleaseHash(string corePath)
        {
            IntPtr libHandle = LoadLibrary(corePath.Trim());

            if (libHandle == IntPtr.Zero)
                throw new ENoCore();

            IntPtr procAddress = GetProcAddress(libHandle, "GetCurrentHash");
            if (procAddress == IntPtr.Zero)
                throw new ENoEntryPoint();

            GetCurrentHashDelegate getLocalHash =
                Marshal.GetDelegateForFunctionPointer<GetCurrentHashDelegate>(procAddress);

            string localHash = Marshal.PtrToStringAnsi(getLocalHash());

            FreeLibrary(libHandle);

            return localHash;
        }

        public static GithubRelease GetRemoteRelease() =>
            DownloadSerializedJsonData<GithubRelease>(
                "https://api.github.com/repos/xrOxygen/xray-oxygen/releases/latest");

        public static GithubRelease GetRemoteRelease(string hash)
        {
            GithubRelease[] releases =
                DownloadSerializedJsonData<GithubRelease[]>(
                    "https://api.github.com/repos/xrOxygen/xray-oxygen/releases");

            GithubRelease release = null;

            foreach (GithubRelease item in releases)
            {
                if (!item.target_commitish.StartsWith(hash)) continue;
                release = item;
                break;
            }

            return release;
        }

        public static void CopyFolder(string sourceFolder, string destFolder)
        {
            if (!Directory.Exists(destFolder))
                Directory.CreateDirectory(destFolder);

            string[] files = Directory.GetFiles(sourceFolder);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                string dest = Path.Combine(destFolder, name);
                File.Copy(file, dest, true);
            }

            string[] folders = Directory.GetDirectories(sourceFolder);
            foreach (string folder in folders)
            {
                string name = Path.GetFileName(folder);
                string dest = Path.Combine(destFolder, name);
                CopyFolder(folder, dest);
            }
        }
    }
}
