using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DSB.GC;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

public sealed class GCRuntimeInfoTests
{
    [Test]
    public void RuntimeInfoShapeCanSerializePackageIdentityFields()
    {
        var manifest = ReadPackageManifest();
        var runtimeInfo = GCEditorPackageIdentity.Resolve().ToRuntimeInfo();
        var json = JsonUtility.ToJson(runtimeInfo);

        Assert.That(runtimeInfo.platform, Is.EqualTo(GCEditorPackageIdentity.Platform));
        Assert.That(runtimeInfo.packageName, Is.EqualTo(manifest.name));
        Assert.That(runtimeInfo.packageVersion, Is.EqualTo(manifest.version));
        Assert.That(runtimeInfo.gameProtocolVersion, Is.EqualTo(GCEditorPackageIdentity.GameProtocolVersion));
        Assert.That(json, Does.Contain("\"platform\":\"" + GCEditorPackageIdentity.Platform + "\""));
        Assert.That(json, Does.Contain("\"packageName\":\"" + manifest.name + "\""));
        Assert.That(json, Does.Contain("\"packageVersion\":\"" + manifest.version + "\""));
        Assert.That(json, Does.Contain("\"gameProtocolVersion\":" + GCEditorPackageIdentity.GameProtocolVersion));
    }

    [Test]
    public void RuntimeInfoIsSerializableDtoOnly()
    {
        var fields = typeof(GCRuntimeInfo).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly
        );
        var staticFields = typeof(GCRuntimeInfo).GetFields(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
        );
        var declaredMethods = typeof(GCRuntimeInfo).GetMethods(
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
        );

        Assert.That(Attribute.IsDefined(typeof(GCRuntimeInfo), typeof(SerializableAttribute)), Is.True);
        Assert.That(Array.ConvertAll(fields, field => field.Name), Is.EquivalentTo(new[]
        {
            "platform",
            "packageName",
            "packageVersion",
            "gameProtocolVersion",
        }));
        Assert.That(typeof(GCRuntimeInfo).GetField("platform").FieldType, Is.EqualTo(typeof(string)));
        Assert.That(typeof(GCRuntimeInfo).GetField("packageName").FieldType, Is.EqualTo(typeof(string)));
        Assert.That(typeof(GCRuntimeInfo).GetField("packageVersion").FieldType, Is.EqualTo(typeof(string)));
        Assert.That(typeof(GCRuntimeInfo).GetField("gameProtocolVersion").FieldType, Is.EqualTo(typeof(int)));
        Assert.That(staticFields, Is.Empty);
        Assert.That(declaredMethods, Is.Empty);
    }

    [Test]
    public void WebGLRuntimeCallbackPathIsRemoved()
    {
        var bridgePath = Path.Combine(FindPackageRootPath(), "Plugins", "GamingCouch.jslib");
        var bridge = File.ReadAllText(bridgePath);
        var runtimePath = Path.Combine(FindPackageRootPath(), "Runtime", "GamingCouch.cs");
        var runtime = File.ReadAllText(runtimePath);

        Assert.That(runtime, Does.Not.Contain("GamingCouchRegisterRuntimeInfo"));
        Assert.That(runtime, Does.Not.Contain("SendRuntimeInfo"));
        Assert.That(runtime, Does.Not.Contain("GCRuntimeInfo.ToJson"));
        Assert.That(bridge, Does.Not.Contain("GamingCouchRegisterRuntimeInfo"));
        Assert.That(bridge, Does.Not.Contain("gamingCouchRegisterRuntimeInfo"));
        Assert.That(bridge, Does.Not.Contain("runtimeInfoJsonString"));
    }

    [Test]
    public void PackageManifestNameAndVersionAreNotHardcodedInPackageSources()
    {
        var packageRootPath = FindPackageRootPath();
        var manifest = ReadPackageManifest();
        var failures = new List<string>();

        foreach (var sourcePath in EnumeratePackageSourcePaths(packageRootPath))
        {
            var source = File.ReadAllText(sourcePath);
            var relativePath = ToPackageRelativePath(packageRootPath, sourcePath);

            if (SourceContainsStringLiteral(source, manifest.name))
            {
                failures.Add(relativePath + " hardcodes package name literal");
            }

            if (SourceContainsStringLiteral(source, manifest.version))
            {
                failures.Add(relativePath + " hardcodes package version literal");
            }
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    [Test]
    public void PackageLiteralScannerDetectsOnlyExactExecutableStringLiterals()
    {
        const string literal = "package.identity.example";

        var positiveSources = new[]
        {
            "var value = \"" + literal + "\";",
            "var value = @\"" + literal + "\";",
            "var value = $\"" + literal + "\";",
            "var value = $@\"" + literal + "\";",
            "var value = @$\"" + literal + "\";",
            "var value = '" + literal + "';",
            "const value = `" + literal + "`;",
            "var value = $\"{ \"" + literal + "\" }\";",
            "const value = `${\"" + literal + "\"}`;",
        };

        foreach (var source in positiveSources)
        {
            Assert.That(SourceContainsStringLiteral(source, literal), Is.True, source);
        }

        var negativeSources = new[]
        {
            "// \"" + literal + "\"",
            "/* `" + literal + "` */",
            "var value = \"prefix " + literal + "\";",
            "var value = \"" + literal + " suffix\";",
            "var value = \"\\\"" + literal + "\\\"\";",
            "var value = `prefix " + literal + " suffix`;",
            "var value = `prefix ${packageName} suffix`;",
        };

        foreach (var source in negativeSources)
        {
            Assert.That(SourceContainsStringLiteral(source, literal), Is.False, source);
        }
    }

    private static PackageManifest ReadPackageManifest()
    {
        var manifestPath = Path.Combine(FindPackageRootPath(), "package.json");
        return JsonUtility.FromJson<PackageManifest>(File.ReadAllText(manifestPath));
    }

    private static string FindPackageRootPath()
    {
        var packageInfo = PackageInfo.FindForAssembly(typeof(GamingCouch).Assembly);
        if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
        {
            return packageInfo.resolvedPath;
        }

        throw new InvalidOperationException("Could not resolve Gaming Couch package root.");
    }

    private static IEnumerable<string> EnumeratePackageSourcePaths(string packageRootPath)
    {
        foreach (var sourceDirectoryName in new[] { "Runtime", "Editor", "Plugins" })
        {
            var sourceDirectoryPath = Path.Combine(packageRootPath, sourceDirectoryName);
            if (!Directory.Exists(sourceDirectoryPath))
            {
                continue;
            }

            var sourcePaths = Directory.EnumerateFiles(sourceDirectoryPath, "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(sourceDirectoryPath, "*.jslib", SearchOption.AllDirectories))
                .OrderBy(path => path, StringComparer.Ordinal);

            foreach (var sourcePath in sourcePaths)
            {
                yield return sourcePath;
            }
        }
    }

    private static string ToPackageRelativePath(string packageRootPath, string sourcePath)
    {
        var normalizedPackageRootPath = Path.GetFullPath(packageRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedSourcePath = Path.GetFullPath(sourcePath);

        if (normalizedSourcePath.StartsWith(normalizedPackageRootPath, StringComparison.Ordinal))
        {
            return normalizedSourcePath.Substring(normalizedPackageRootPath.Length);
        }

        return normalizedSourcePath;
    }

    private static bool SourceContainsStringLiteral(string source, string value)
    {
        return SourceContainsStringLiteral(source, value, 0, source.Length);
    }

    // Small lexer for exact package-value literals: skip comments, avoid substring
    // matches inside larger literals, and recurse into interpolation expressions.
    private static bool SourceContainsStringLiteral(string source, string value, int startIndex, int endIndex)
    {
        var index = startIndex;
        while (index < endIndex)
        {
            var current = source[index];
            var next = index + 1 < endIndex ? source[index + 1] : '\0';

            if (current == '/' && next == '/')
            {
                index = SkipLineComment(source, index + 2, endIndex);
                continue;
            }

            if (current == '/' && next == '*')
            {
                index = SkipBlockComment(source, index + 2, endIndex);
                continue;
            }

            if (TryReadStringLiteral(source, index, endIndex, out var literal, out var nextIndex, out var interpolationRanges))
            {
                if (literal == value)
                {
                    return true;
                }

                foreach (var interpolationRange in interpolationRanges)
                {
                    if (SourceContainsStringLiteral(
                            source,
                            value,
                            interpolationRange.StartIndex,
                            interpolationRange.EndIndex
                        ))
                    {
                        return true;
                    }
                }

                index = nextIndex;
                continue;
            }

            index++;
        }

        return false;
    }

    private static bool TryReadStringLiteral(
        string source,
        int startIndex,
        int endIndex,
        out string literal,
        out int nextIndex,
        out List<SourceRange> interpolationRanges
    )
    {
        literal = string.Empty;
        nextIndex = startIndex;
        interpolationRanges = new List<SourceRange>();

        if (!TryGetStringLiteralStart(source, startIndex, endIndex, out var quoteIndex, out var quote, out var isVerbatim, out var isInterpolated))
        {
            return false;
        }

        var builder = new StringBuilder();
        var index = quoteIndex + 1;
        while (index < endIndex)
        {
            var current = source[index];
            var next = index + 1 < endIndex ? source[index + 1] : '\0';

            if (quote == '`')
            {
                if (current == '\\' && index + 1 < endIndex)
                {
                    builder.Append(current);
                    builder.Append(next);
                    index += 2;
                    continue;
                }

                if (current == '$' && next == '{')
                {
                    var interpolationEnd = FindInterpolationEnd(source, index + 2, endIndex);
                    if (interpolationEnd >= 0)
                    {
                        interpolationRanges.Add(new SourceRange(index + 2, interpolationEnd));
                        index = interpolationEnd + 1;
                        continue;
                    }
                }

                if (current == quote)
                {
                    literal = builder.ToString();
                    nextIndex = index + 1;
                    return true;
                }
            }

            if (quote == '"' && isInterpolated)
            {
                if (current == '{' && next == '{')
                {
                    builder.Append(current);
                    builder.Append(next);
                    index += 2;
                    continue;
                }

                if (current == '{')
                {
                    var interpolationEnd = FindInterpolationEnd(source, index + 1, endIndex);
                    if (interpolationEnd >= 0)
                    {
                        interpolationRanges.Add(new SourceRange(index + 1, interpolationEnd));
                        index = interpolationEnd + 1;
                        continue;
                    }
                }

                if (current == '}' && next == '}')
                {
                    builder.Append(current);
                    builder.Append(next);
                    index += 2;
                    continue;
                }
            }

            if (isVerbatim && quote == '"')
            {
                if (current == '"' && next == '"')
                {
                    builder.Append(current);
                    builder.Append(next);
                    index += 2;
                    continue;
                }

                if (current == quote)
                {
                    literal = builder.ToString();
                    nextIndex = index + 1;
                    return true;
                }
            }
            else
            {
                if (current == '\\' && index + 1 < endIndex)
                {
                    builder.Append(current);
                    builder.Append(next);
                    index += 2;
                    continue;
                }

                if (current == quote)
                {
                    literal = builder.ToString();
                    nextIndex = index + 1;
                    return true;
                }
            }

            builder.Append(current);
            index++;
        }

        literal = builder.ToString();
        nextIndex = endIndex;
        return true;
    }

    private static bool TryGetStringLiteralStart(
        string source,
        int startIndex,
        int endIndex,
        out int quoteIndex,
        out char quote,
        out bool isVerbatim,
        out bool isInterpolated
    )
    {
        quoteIndex = startIndex;
        quote = source[startIndex];
        isVerbatim = false;
        isInterpolated = false;

        var current = source[startIndex];
        var next = startIndex + 1 < endIndex ? source[startIndex + 1] : '\0';
        var following = startIndex + 2 < endIndex ? source[startIndex + 2] : '\0';

        if (current == '$' && next == '"')
        {
            quoteIndex = startIndex + 1;
            quote = '"';
            isInterpolated = true;
            return true;
        }

        if (current == '$' && next == '@' && following == '"')
        {
            quoteIndex = startIndex + 2;
            quote = '"';
            isVerbatim = true;
            isInterpolated = true;
            return true;
        }

        if (current == '@' && next == '"')
        {
            quoteIndex = startIndex + 1;
            quote = '"';
            isVerbatim = true;
            return true;
        }

        if (current == '@' && next == '$' && following == '"')
        {
            quoteIndex = startIndex + 2;
            quote = '"';
            isVerbatim = true;
            isInterpolated = true;
            return true;
        }

        if (current == '"' || current == '\'' || current == '`')
        {
            quote = current;
            isInterpolated = current == '`';
            return true;
        }

        return false;
    }

    private static int FindInterpolationEnd(string source, int startIndex, int endIndex)
    {
        var depth = 0;
        var index = startIndex;
        while (index < endIndex)
        {
            var current = source[index];
            var next = index + 1 < endIndex ? source[index + 1] : '\0';

            if (current == '/' && next == '/')
            {
                index = SkipLineComment(source, index + 2, endIndex);
                continue;
            }

            if (current == '/' && next == '*')
            {
                index = SkipBlockComment(source, index + 2, endIndex);
                continue;
            }

            if (TryReadStringLiteral(source, index, endIndex, out _, out var nextIndex, out _))
            {
                index = nextIndex;
                continue;
            }

            if (current == '{')
            {
                depth++;
                index++;
                continue;
            }

            if (current == '}')
            {
                if (depth == 0)
                {
                    return index;
                }

                depth--;
                index++;
                continue;
            }

            index++;
        }

        return -1;
    }

    private static int SkipLineComment(string source, int startIndex, int endIndex)
    {
        var index = startIndex;
        while (index < endIndex && source[index] != '\n')
        {
            index++;
        }

        return index;
    }

    private static int SkipBlockComment(string source, int startIndex, int endIndex)
    {
        var index = startIndex;
        while (index + 1 < endIndex)
        {
            if (source[index] == '*' && source[index + 1] == '/')
            {
                return index + 2;
            }

            index++;
        }

        return endIndex;
    }

    [Serializable]
    private sealed class PackageManifest
    {
        public string name;
        public string version;
    }

    private struct SourceRange
    {
        public SourceRange(int startIndex, int endIndex)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public int StartIndex { get; }
        public int EndIndex { get; }
    }
}
