using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.FileIO;

namespace OrangeCatPet;

internal static class WindowsRecycleBin
{
    internal sealed record MoveResult(int SucceededCount, int FailedCount);

    public static string[] GetSupportedPaths(IEnumerable<string> paths)
    {
        return paths
            .Select(TryGetFullPath)
            .Where(path => path != null && !IsFileSystemRoot(path))
            .Select(path => path!)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static MoveResult Move(IEnumerable<string> paths)
    {
        var succeeded = 0;
        var failed = 0;

        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    FileSystem.DeleteFile(
                        path,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin,
                        UICancelOption.ThrowException);
                    succeeded++;
                }
                else if (Directory.Exists(path) && !IsFileSystemRoot(path))
                {
                    FileSystem.DeleteDirectory(
                        path,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin,
                        UICancelOption.ThrowException);
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

    private static string? TryGetFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static bool IsFileSystemRoot(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root))
        {
            return true;
        }

        return string.Equals(
            path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }
}