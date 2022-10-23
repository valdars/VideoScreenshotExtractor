namespace VideoScreenshotExtractor
{
    internal class PathHelper
    {
        public static string RelativePathToAbsolute(string path, string basePath)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }
            path = Path.Combine(basePath, path);
            path = Path.GetFullPath((new Uri(path)).LocalPath);
            return path;
        }

        public static List<string> GetFilesOfExtensionsFromPath(string path, IEnumerable<string> extensions)
        {
            return Directory
                .EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                .Where(s => extensions.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant())).ToList();
        }
    }
}
