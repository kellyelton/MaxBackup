using MaxBackup.ServiceApp;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Max.IntegrationTests;

public sealed class BackupFileScannerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "MaxBackup-scanner-" + Guid.NewGuid());

    public BackupFileScannerTests() => Directory.CreateDirectory(root);
    private void Write(string path, string text = "data")
    {
        var full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }
    private string[] Scan(string source = "", string[]? includes = null, string[]? excludes = null)
    {
        var matcher = BackupFileScanner.CreateMatcher(Path.Combine(root, source), includes ?? ["**/*"], excludes ?? []);
        return BackupFileScanner.Scan(Path.Combine(root, source), matcher)
            .Select(x => Path.GetRelativePath(Path.Combine(root, source), x).Replace('\\', '/')).Order().ToArray();
    }

    [Fact]
    public void BareDirectoryRulePrunesBeforeReadingChildIgnore()
    {
        Write(".maxbackupignore", "node_modules/");
        Write("app/node_modules/.maxbackupignore", "!unsupported");
        Write("app/node_modules/package.txt");
        Write("keep.txt");
        Assert.Equal([".maxbackupignore", "keep.txt"], Scan());
    }

    [Fact]
    public void NestedRulesStayWithinTheirOwnerAndAncestorRulesContinue()
    {
        Write(".maxbackupignore", "*.tmp");
        Write("one/.maxbackupignore", "/local.txt\nassets/generated/");
        Write("one/local.txt"); Write("one/deep/local.txt"); Write("one/assets/generated/x");
        Write("one/deep/assets/generated/x"); Write("two/local.txt"); Write("two/a.tmp");
        Assert.Equal([".maxbackupignore", "one/.maxbackupignore", "one/deep/assets/generated/x", "one/deep/local.txt", "two/local.txt"], Scan());
    }

    [Fact]
    public void AncestorIgnoreAboveBackupRootIsNotRead()
    {
        Write(".maxbackupignore", "!unsupported"); Write("sub/keep.txt");
        Assert.Equal(["keep.txt"], Scan("sub"));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("../sibling/**")]
    public void ParentTraversalIncludesPreserveExistingMatcherBehavior(string include)
    {
        Write("source/keep.txt"); Write("outside.txt"); Write("sibling/a.txt");
        var source = Path.Combine(root, "source");
        var expected = new Microsoft.Extensions.FileSystemGlobbing.Matcher().AddInclude(include)
            .GetResultsInFullPath(source).Select(x => Path.GetRelativePath(source, x).Replace('\\', '/')).Order().ToArray();
        Assert.NotEmpty(expected);
        Assert.Equal(expected, Scan("source", includes: [include]));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("source/")]
    public void ParentTraversalDoesNotReadOuterIgnoreAndReentryUsesSourceIgnore(string source)
    {
        Write(".maxbackupignore", "!unsupported");
        Write("source/.maxbackupignore", "ignored/"); Write("source/ignored/a.txt");
        Write("source/keep.txt");
        Assert.Empty(Scan(source, includes: ["../source/ignored/**"]));
        Assert.Equal([".maxbackupignore", "keep.txt"], Scan(source, includes: ["../source/**"]));
    }

    [Fact]
    public void MissingDirectoryPreservesExistingMatcherEmptyResult()
    {
        var source = Path.Combine(root, "removed");
        Directory.CreateDirectory(source); Directory.Delete(source);
        Assert.Empty(new Matcher().AddInclude("**/*").GetResultsInFullPath(source));
        Assert.Empty(Scan("removed"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultExclusionsPreserveOriginalSourcePathBehavior(bool relative)
    {
        Write("keep.txt"); Write("scratch~");
        Write("$Recycle.Bin"); Write("System Volume Information");
        var source = relative ? Path.GetRelativePath(Environment.CurrentDirectory, root) : root;
        var matcher = new Matcher().AddInclude("**/*");
        var drive = Path.GetPathRoot(source);
        if (drive != null && drive.Length == 3 && drive.EndsWith(":\\", StringComparison.OrdinalIgnoreCase))
            matcher.AddExclude("$Recycle.Bin").AddExclude("System Volume Information").AddExclude("*~");
        var expected = matcher.GetResultsInFullPath(source).Order().ToArray();

        Assert.Equal(relative ? 4 : 1, expected.Length);
        Assert.Equal(expected, BackupFileScanner.Scan(source,
            BackupFileScanner.CreateMatcher(source, ["**/*"], [])).Order().ToArray());
    }

    [Fact]
    public void DriveRootScanAppliesNestedIgnoreFiles()
    {
        Write("source/.maxbackupignore", "ignored/"); Write("source/ignored/a.txt"); Write("source/keep.txt");
        var driveRoot = Path.GetPathRoot(root)!;
        var include = Path.GetRelativePath(driveRoot, root).Replace('\\', '/') + "/source/**";
        var files = BackupFileScanner.Scan(driveRoot, BackupFileScanner.CreateMatcher(driveRoot, [include], []))
            .Select(x => Path.GetRelativePath(root, x).Replace('\\', '/')).Order().ToArray();
        Assert.Equal(["source/.maxbackupignore", "source/keep.txt"], files);
    }

    [Fact]
    public void EmptyCommentsBomAndDirectoryOnlyRules()
    {
        Write(".maxbackupignore", "\uFEFF # comment\n\n  cache/  \n");
        Write("cache/a"); Write("other/cache"); Write("normal.txt");
        Assert.Equal([".maxbackupignore", "normal.txt", "other/cache"], Scan());
    }

    [Fact]
    public void JobIncludesAndExcludesStillApply()
    {
        Write(".maxbackupignore", "ignored.txt"); Write("ignored.txt"); Write("keep.txt"); Write("excluded.txt"); Write("other.bin");
        Assert.Equal(["keep.txt"], Scan(includes: ["*.txt"], excludes: ["excluded.txt"]));
    }

    [Fact]
    public void ConfigExcludedDirectoryIsNotVisited()
    {
        Write("excluded/.maxbackupignore", "!unsupported"); Write("keep.txt");
        Assert.Equal(["keep.txt"], Scan(excludes: ["excluded/**"]));
    }

    [Fact]
    public void ExplicitIncludesCannotBypassIgnoredDirectories()
    {
        Write(".maxbackupignore", "ignored/");
        Write("ignored/.maxbackupignore", "!unsupported"); Write("ignored/a.txt");
        Assert.Empty(Scan(includes: ["ignored/a.txt"]));
    }

    [Fact]
    public void IncludesPruneUnselectedDirectoriesBeforeReadingIgnoreFiles()
    {
        Write("selected/keep.txt"); Write("other/.maxbackupignore", "!unsupported");
        Assert.Equal(["selected/keep.txt"], Scan(includes: ["selected/**"]));
    }

    [Fact]
    public void ExistingGitIgnoreAndNodeDirectoriesAreOptIn()
    {
        Write(".gitignore", "node_modules/"); Write("node_modules/package.txt"); Write(".git/config");
        Assert.Equal([".git/config", ".gitignore", "node_modules/package.txt"], Scan());
    }

    [Fact]
    public void DoubleStarPatternsRemainCaseInsensitive()
    {
        Write(".maxbackupignore", "**/BUILD/*.TMP"); Write("one/build/a.tmp"); Write("one/build/keep.txt");
        Assert.Equal([".maxbackupignore", "one/build/keep.txt"], Scan());
    }

    [Fact]
    public void LeadingSlashDirectoryRuleIsAnchoredAtOwner()
    {
        Write(".maxbackupignore", "/dist/"); Write("dist/a.txt"); Write("packages/dist/a.txt");
        Assert.Equal([".maxbackupignore", "packages/dist/a.txt"], Scan());
    }

    [Fact]
    public void ExcludedIgnoreFileStillControlsSelection()
    {
        Write(".maxbackupignore", ".maxbackupignore\n*.tmp"); Write("a.tmp"); Write("keep.txt");
        Assert.Equal(["keep.txt"], Scan());
    }

    [Fact]
    public void ChangedIgnoreIsReadOnEveryScan()
    {
        Write(".maxbackupignore", "a.txt"); Write("a.txt"); Write("b.txt");
        Assert.Equal([".maxbackupignore", "b.txt"], Scan());
        Write(".maxbackupignore", "b.txt");
        Assert.Equal([".maxbackupignore", "a.txt"], Scan());
    }

    [Theory]
    [InlineData("!keep.txt")]
    [InlineData("../outside")]
    [InlineData("a/../outside")]
    [InlineData("C:/outside")]
    [InlineData("a\\b")]
    [InlineData("//server/share")]
    public void UnsupportedRuleFailsWithFileAndLine(string rule)
    {
        Write(".maxbackupignore", "# comment\n" + rule);
        var error = Assert.Throws<FormatException>(() => Scan());
        Assert.Contains(".maxbackupignore", error.Message);
        Assert.Contains("2", error.Message);
    }

    [Fact]
    public void UnreadableExistingIgnoreFailsScan()
    {
        Write(".maxbackupignore", "*.tmp");
        using var locked = new FileStream(Path.Combine(root, ".maxbackupignore"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.ThrowsAny<IOException>(() => Scan());
    }

    public void Dispose() => Directory.Delete(root, true);
}
