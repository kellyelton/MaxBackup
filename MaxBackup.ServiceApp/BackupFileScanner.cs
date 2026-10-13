using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace MaxBackup.ServiceApp;

internal static class BackupFileScanner
{
    internal static Matcher CreateMatcher(string source, IEnumerable<string> includes,
        IEnumerable<string> excludes)
    {
        var matcher = new Matcher();
        foreach (var include in includes) matcher.AddInclude(include);
        foreach (var exclude in excludes) matcher.AddExclude(exclude);
        var drive = Path.GetPathRoot(source);
        if (drive != null && drive.Length == 3 && drive.EndsWith(":\\", StringComparison.OrdinalIgnoreCase))
        {
            matcher.AddExclude("$Recycle.Bin");
            matcher.AddExclude("System Volume Information");
            matcher.AddExclude("*~");
        }
        return matcher;
    }

    internal static IEnumerable<string> Scan(string source, Matcher matcher)
    {
        source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        var result = matcher.Execute(new IgnoreDirectory(new DirectoryInfo(source), null, [], source));
        return result.Files.Select(file => Path.GetFullPath(Path.Combine(source, file.Path))).ToList();
    }

    private sealed record IgnoreRule(string Owner, Matcher Pattern, bool DirectoryOnly)
    {
        internal bool Matches(FileSystemInfo entry) => (!DirectoryOnly || entry is DirectoryInfo)
            && Pattern.Match(Path.GetRelativePath(Owner, entry.FullName).Replace('\\', '/')).HasMatches;
    }

    private sealed class IgnoreDirectory(DirectoryInfo directory, IgnoreDirectory? parent,
        IReadOnlyList<IgnoreRule> inherited, string scanRoot, string? name = null) : DirectoryInfoBase
    {
        private readonly IReadOnlyList<IgnoreRule> ancestorRules = inherited;
        private IReadOnlyList<IgnoreRule>? rules;
        public override string Name => name ?? directory.Name;
        public override string FullName => directory.FullName;
        public override DirectoryInfoBase ParentDirectory => parent!;

        private IReadOnlyList<IgnoreRule> Rules
        {
            get
            {
                if (rules != null) return rules;
                if (!string.Equals(FullName, scanRoot, StringComparison.OrdinalIgnoreCase)
                    && !FullName.StartsWith(Path.EndsInDirectorySeparator(scanRoot) ? scanRoot : scanRoot + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    return rules = [];
                var path = Path.Combine(FullName, ".maxbackupignore");
                string[] lines;
                try { lines = File.ReadAllLines(path); }
                catch (FileNotFoundException) { return rules = ancestorRules; }
                catch (DirectoryNotFoundException) when (!Directory.Exists(FullName)) { return rules = ancestorRules; }
                var combined = new List<IgnoreRule>(ancestorRules);
                for (var i = 0; i < lines.Length; i++)
                {
                    var pattern = lines[i].Trim().TrimStart('\uFEFF').Trim();
                    if (pattern.Length == 0 || pattern.StartsWith('#')) continue;
                    if (pattern.StartsWith('!') || pattern.StartsWith("//", StringComparison.Ordinal)
                        || pattern.Contains('\\') || pattern.Contains(':')
                        || pattern.Split('/').Any(segment => segment is ".." or "."))
                        throw new FormatException($"Unsupported ignore rule in '{path}' at line {i + 1}: '{pattern}'. Use relative slash-separated exclusion patterns without negation or traversal.");
                    var anchored = pattern.StartsWith('/');
                    var directoryOnly = pattern.EndsWith('/');
                    pattern = pattern.Trim('/');
                    if (pattern.Length == 0)
                        throw new FormatException($"Empty ignore rule in '{path}' at line {i + 1}.");
                    if (!anchored && !pattern.Contains('/')) pattern = "**/" + pattern;
                    combined.Add(new IgnoreRule(FullName, new Matcher().AddInclude(pattern), directoryOnly));
                }
                return rules = combined;
            }
        }

        public override IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos()
        {
            var active = Rules;
            FileSystemInfo[] entries;
            try { entries = directory.GetFileSystemInfos(); }
            catch (DirectoryNotFoundException) when (!Directory.Exists(FullName)) { yield break; }
            foreach (var entry in entries)
            {
                if (active.Any(rule => rule.Matches(entry))) continue;
                if (entry is DirectoryInfo child)
                    yield return new IgnoreDirectory(child, this, active, scanRoot);
                else if (entry is FileInfo file)
                    yield return new FileInfoWrapper(file);
            }
        }

        public override DirectoryInfoBase GetDirectory(string path)
        {
            if (path == ".." && directory.Parent is { } outer)
                return parent != null
                    ? new IgnoreDirectory(outer, (IgnoreDirectory?)parent.ParentDirectory, parent.ancestorRules, scanRoot, "..")
                    : new IgnoreDirectory(outer, null, [], scanRoot, "..");
            return EnumerateFileSystemInfos().OfType<DirectoryInfoBase>()
                .FirstOrDefault(entry => string.Equals(entry.Name, path, StringComparison.OrdinalIgnoreCase))!;
        }

        public override FileInfoBase GetFile(string path) =>
            EnumerateFileSystemInfos().OfType<FileInfoBase>()
                .FirstOrDefault(entry => string.Equals(entry.Name, path, StringComparison.OrdinalIgnoreCase))!;
    }
}
