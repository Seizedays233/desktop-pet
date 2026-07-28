using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace OrangeCatPetMac;

internal static class DesktopTrash
{
    internal sealed record MoveResult(int SucceededCount, int FailedCount);

    public static string[] GetSupportedPaths(IEnumerable<string> paths)
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            return Array.Empty<string>();
        }

        var comparer = OperatingSystem.IsLinux()
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

        return paths
            .Select(TryNormalizePath)
            .Where(path => path is not null)
            .Cast<string>()
            .Distinct(comparer)
            .ToArray();
    }

    public static MoveResult Move(IEnumerable<string> paths)
    {
        var succeeded = 0;
        var failed = 0;

        foreach (var path in GetSupportedPaths(paths))
        {
            try
            {
                var moved = OperatingSystem.IsMacOS()
                    ? MacTrash.TryMove(path)
                    : LinuxTrash.TryMove(path);

                if (moved)
                {
                    succeeded++;
                }
                else
                {
                    failed++;
                }
            }
            catch
            {
                failed++;
            }
        }

        return new MoveResult(succeeded, failed);
    }

    private static string? TryNormalizePath(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                return null;
            }

            var root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(root) &&
                string.Equals(
                    Path.TrimEndingDirectorySeparator(fullPath),
                    Path.TrimEndingDirectorySeparator(root),
                    OperatingSystem.IsLinux()
                        ? StringComparison.Ordinal
                        : StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return fullPath;
        }
        catch
        {
            return null;
        }
    }

    private static class MacTrash
    {
        private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";
        private const string FoundationLibrary =
            "/System/Library/Frameworks/Foundation.framework/Foundation";

        private static readonly Lazy<IntPtr> FoundationHandle =
            new(() => NativeLibrary.Load(FoundationLibrary));

        public static bool TryMove(string path)
        {
            _ = FoundationHandle.Value;

            var poolClass = ObjcGetClass("NSAutoreleasePool");
            var pool = Send(Send(poolClass, Selector("alloc")), Selector("init"));
            try
            {
                var pathBuffer = Marshal.StringToCoTaskMemUTF8(path);
                try
                {
                    var nsPath = Send(
                        ObjcGetClass("NSString"),
                        Selector("stringWithUTF8String:"),
                        pathBuffer);
                    var fileUrl = Send(
                        ObjcGetClass("NSURL"),
                        Selector("fileURLWithPath:"),
                        nsPath);
                    var fileManager = Send(
                        ObjcGetClass("NSFileManager"),
                        Selector("defaultManager"));

                    return SendTrash(
                        fileManager,
                        Selector("trashItemAtURL:resultingItemURL:error:"),
                        fileUrl,
                        IntPtr.Zero,
                        IntPtr.Zero);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pathBuffer);
                }
            }
            finally
            {
                _ = Send(pool, Selector("drain"));
            }
        }

        private static IntPtr Selector(string name) => SelRegisterName(name);

        [DllImport(ObjectiveCLibrary, EntryPoint = "objc_getClass")]
        private static extern IntPtr ObjcGetClass(string name);

        [DllImport(ObjectiveCLibrary, EntryPoint = "sel_registerName")]
        private static extern IntPtr SelRegisterName(string name);

        [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

        [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument);

        [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SendTrash(
            IntPtr receiver,
            IntPtr selector,
            IntPtr url,
            IntPtr resultingUrl,
            IntPtr error);
    }

    private static class LinuxTrash
    {
        public static bool TryMove(string path)
        {
            try
            {
                if (TryMoveWithGio(path))
                {
                    return true;
                }
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }

            return TryMoveToHomeTrash(path);
        }

        private static bool TryMoveWithGio(string path)
        {
            var file = GFileNewForPath(path);
            if (file == IntPtr.Zero)
            {
                return false;
            }

            IntPtr error = IntPtr.Zero;
            try
            {
                return GFileTrash(file, IntPtr.Zero, out error) != 0;
            }
            finally
            {
                if (error != IntPtr.Zero)
                {
                    GErrorFree(error);
                }

                GObjectUnref(file);
            }
        }

        private static bool TryMoveToHomeTrash(string path)
        {
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrWhiteSpace(dataHome) || !Path.IsPathFullyQualified(dataHome))
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(userProfile))
                {
                    return false;
                }

                dataHome = Path.Combine(userProfile, ".local", "share");
            }

            var trashRoot = Path.GetFullPath(Path.Combine(dataHome, "Trash"));
            if (IsWithinDirectory(path, trashRoot))
            {
                return false;
            }

            var filesDirectory = Path.Combine(trashRoot, "files");
            var infoDirectory = Path.Combine(trashRoot, "info");
            Directory.CreateDirectory(filesDirectory);
            Directory.CreateDirectory(infoDirectory);
            TrySetUnixFileMode(
                trashRoot,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            TrySetUnixFileMode(
                filesDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            TrySetUnixFileMode(
                infoDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var originalName = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            for (var suffix = 0; suffix < 10_000; suffix++)
            {
                var trashName = suffix == 0 ? originalName : $"{originalName}.{suffix}";
                var destination = Path.Combine(filesDirectory, trashName);
                var infoPath = Path.Combine(infoDirectory, $"{trashName}.trashinfo");
                if (File.Exists(destination) || Directory.Exists(destination))
                {
                    continue;
                }

                try
                {
                    using (var stream = new FileStream(
                               infoPath,
                               FileMode.CreateNew,
                               FileAccess.Write,
                               FileShare.None))
                    using (var writer = new StreamWriter(
                               stream,
                               new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                    {
                        writer.NewLine = "\n";
                        writer.WriteLine("[Trash Info]");
                        writer.WriteLine($"Path={EncodeTrashPath(path)}");
                        writer.WriteLine(
                            $"DeletionDate={DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)}");
                    }

                    TrySetUnixFileMode(
                        infoPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch (IOException)
                {
                    continue;
                }

                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Move(path, destination);
                    }
                    else
                    {
                        File.Move(path, destination);
                    }

                    return true;
                }
                catch
                {
                    try
                    {
                        File.Delete(infoPath);
                    }
                    catch
                    {
                    }

                    return false;
                }
            }

            return false;
        }

        private static bool IsWithinDirectory(string path, string directory)
        {
            var normalizedPath = Path.TrimEndingDirectorySeparator(path);
            var normalizedDirectory = Path.TrimEndingDirectorySeparator(directory);
            return string.Equals(normalizedPath, normalizedDirectory, StringComparison.Ordinal) ||
                   normalizedPath.StartsWith(
                       normalizedDirectory + Path.DirectorySeparatorChar,
                       StringComparison.Ordinal);
        }

        private static void TrySetUnixFileMode(string path, UnixFileMode mode)
        {
            if (!OperatingSystem.IsLinux())
            {
                return;
            }

            try
            {
                File.SetUnixFileMode(path, mode);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string EncodeTrashPath(string path)
        {
            return Uri.EscapeDataString(path)
                .Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
        }

        [DllImport("libgio-2.0.so.0", EntryPoint = "g_file_new_for_path")]
        private static extern IntPtr GFileNewForPath(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

        [DllImport("libgio-2.0.so.0", EntryPoint = "g_file_trash")]
        private static extern int GFileTrash(
            IntPtr file,
            IntPtr cancellable,
            out IntPtr error);

        [DllImport("libgobject-2.0.so.0", EntryPoint = "g_object_unref")]
        private static extern void GObjectUnref(IntPtr instance);

        [DllImport("libglib-2.0.so.0", EntryPoint = "g_error_free")]
        private static extern void GErrorFree(IntPtr error);
    }
}