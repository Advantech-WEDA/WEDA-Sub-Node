namespace Weda.SubNode.Abstractions.Utilities;

public static class PathHelper
{
    /// <summary>
    /// Finds the project root directory by looking for .csproj file.
    /// Searches from current directory upward through parent directories.
    /// </summary>
    /// <returns>Project root path if found, null otherwise.</returns>
    public static string? FindProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory != null)
        {
            if (directory.GetFiles("*.csproj").Length > 0)
                return directory.FullName;

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Resolves a storage directory path.
    /// If the path is absolute, returns it as-is.
    /// If relative, resolves from project root (or current directory if not found).
    /// </summary>
    /// <param name="storageDirectory">The storage directory path (absolute or relative).</param>
    /// <returns>Resolved absolute path.</returns>
    public static string ResolveStorageDirectory(string storageDirectory)
    {
        if (Path.IsPathRooted(storageDirectory))
            return storageDirectory;

        var projectRoot = FindProjectRoot() ?? Directory.GetCurrentDirectory();

        // Remove leading ./ or .\ only (preserve .weda style paths)
        var normalizedPath = storageDirectory;
        if (normalizedPath.StartsWith("./") || normalizedPath.StartsWith(".\\"))
            normalizedPath = normalizedPath[2..];

        return Path.Combine(projectRoot, normalizedPath);
    }
}
