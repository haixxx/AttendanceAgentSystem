using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace AttendanceAgent.Core.Services.Devices
{
    public static class ZKTecoInterop
    {
        public static zkemkeeper.CZKEMClass Create()
        {
            try
            {
                return new zkemkeeper.CZKEMClass();
            }
            catch (COMException ex) when ((uint)ex.ErrorCode == 0x80040154)
            {
                // REGDB_E_CLASSNOTREG
                var dllPath = Path.Combine(AppContext.BaseDirectory, "Dependencies", "64bit", "zkemkeeper.dll");
                if (!File.Exists(dllPath))
                    throw new FileNotFoundException($"zkemkeeper.dll not found at {dllPath}");

                RegisterCom(dllPath);
                return new zkemkeeper.CZKEMClass();
            }
        }

        private static void RegisterCom(string dllPath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "regsvr32.exe"), // 64-bit
                Arguments = $"/s \"{dllPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit();
            if (proc == null || proc.ExitCode != 0)
                throw new InvalidOperationException($"regsvr32 failed for {dllPath} (exit {proc?.ExitCode ?? -1}). Run as Administrator.");
        }
    }
}