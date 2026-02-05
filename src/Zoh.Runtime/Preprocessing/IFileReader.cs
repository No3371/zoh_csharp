namespace Zoh.Runtime.Preprocessing;

public interface IFileReader
{
    string ReadAllText(string path);

    /// <summary>
    /// Resolves a path relative to a base path.
    /// </summary>
    string ResolvePath(string basePath, string relativePath);
}

public class DefaultFileReader : IFileReader
{
    public string ReadAllText(string path) => File.ReadAllText(path);

    public string ResolvePath(string basePath, string relativePath)
    {
        var dir = Path.GetDirectoryName(basePath) ?? "";
        return Path.GetFullPath(Path.Combine(dir, relativePath));
    }
}
