using SevenZip;
using System.IO;
using System.Runtime.InteropServices;
using CC = System.Common.Constants;

namespace System.Application.Services.Implementation
{
    internal sealed class SevenZipHelper : ISevenZipHelper
    {
        public static bool IsInitialized { get; private set; }

        static void Initialize()
        {
            if (IsInitialized) return;
            // ✅ AppContext.BaseDirectory
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
            var sevenZipLibraryPath = Path.Combine(AppContext.BaseDirectory, "7z.dll");
            if (!File.Exists(sevenZipLibraryPath))
            {
                sevenZipLibraryPath = Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X86 => "win-x86",
                    Architecture.X64 => "win-x64",
                    Architecture.Arm => "win-arm",
                    Architecture.Arm64 => "win-arm64",
                    _ => throw new PlatformNotSupportedException(),
                }, "native", "7z.dll");
            }
            SevenZipBase.SetLibraryPath(sevenZipLibraryPath);
            IsInitialized = true;
        }

        public bool Unpack(string filePath, string dirPath,
          IProgress<float>? progress = null,
          float maxProgress = CC.MaxProgress)
        {
            Initialize();

            if (!File.Exists(filePath)) return false;
            if (Directory.Exists(dirPath)) return false;

            using var fs = File.OpenRead(filePath);
            float length = fs.Length;
            Directory.CreateDirectory(dirPath);
            using var archive = new SevenZipExtractor(fs);
            var hasProgress = progress != null;
            if (hasProgress)
            {
                archive.Extracting += (_, e) =>
                {
                    progress!.Report(e.PercentDone);
                };
            }
            archive.ExtractArchive(dirPath);
            if (hasProgress)
            {
                progress!.Report(maxProgress);
            }
            return true;
        }
    }
}
