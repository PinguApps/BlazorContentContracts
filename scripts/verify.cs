// Build, test, pack and exercise the actual packages in an independent consumer.
#:property PublishAot=false

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Xml.Linq;

var root = Directory.GetCurrentDirectory();
var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");

if (string.IsNullOrEmpty(dotnet))
{
    dotnet = (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator)
        .Select(directory => Path.Combine(directory, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"))
        .FirstOrDefault(File.Exists);
}

Require(!string.IsNullOrEmpty(dotnet), "Install the SDK selected by global.json first.");
dotnet = Path.GetFullPath(dotnet!);

var processPath = Environment.GetEnvironmentVariable("PATH");
var persistentPaths = PersistentPaths();

try
{
    Run(root, "build", "-c", "Release");


    var output = Path.Combine(root, "artifacts", "packages");
    Directory.CreateDirectory(output);

    Run(root, "pack", "-c", "Release", "--no-build", "-o", output);

    Run(root, "test", "-c", "Release", "--no-build");

    const string prefix = "PinguApps.BlazorContentContracts";

    foreach (var (package, expected) in new[]
    {
        (prefix, $"lib/net10.0/{prefix}.dll"),
        (prefix + ".Analyzers", $"analyzers/dotnet/cs/{prefix}.Analyzers.dll")
    })
    {
        using var archive = ZipFile.OpenRead(Path.Combine(output, $"{package}.1.0.0.nupkg"));
        var names = archive.Entries.Select(entry => entry.FullName).ToArray();
        Require(names.Contains(expected), $"{package}: missing {expected}");
        Require(names.Contains("README.md") && names.Contains("LICENSE"), $"{package}: missing package documentation");

        using var manifest = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
        var metadata = XDocument.Load(manifest).Descendants().Single(element => element.Name.LocalName == "metadata");
        Require(metadata.Elements().Any(element => element.Name.LocalName == "authors" && element.Value == "PinguApps"),
            $"{package}: missing author");
        Require(metadata.Elements().Any(element => element.Name.LocalName == "license" && element.Value == "MIT"),
            $"{package}: missing MIT licence");
        Require(metadata.Elements().Any(element => element.Name.LocalName == "readme" && element.Value == "README.md"),
            $"{package}: missing README metadata");
        Require(metadata.Elements().Any(element => element.Name.LocalName == "repository"
            && (string?)element.Attribute("url") == "https://github.com/PinguApps/BlazorContentContracts"
            && !string.IsNullOrEmpty((string?)element.Attribute("commit"))), $"{package}: missing repository commit metadata");

        using var symbols = ZipFile.OpenRead(Path.Combine(output, $"{package}.1.0.0.snupkg"));
        var pdb = symbols.Entries.Single(entry => entry.FullName.EndsWith(".pdb", StringComparison.Ordinal));
        using var pdbStream = new MemoryStream();
        using (var source = pdb.Open())
        {
            source.CopyTo(pdbStream);
        }

        pdbStream.Position = 0;
        using var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
        var reader = provider.GetMetadataReader();
        var sourceLinkKind = new Guid("CC110556-A091-4D38-9FEC-25AB9A351A6A");
        var sourceLink = reader.CustomDebugInformation.Select(reader.GetCustomDebugInformation)
            .Single(info => reader.GetGuid(info.Kind) == sourceLinkKind);
        var sourceLinkJson = System.Text.Encoding.UTF8.GetString(reader.GetBlobBytes(sourceLink.Value));
        Require(sourceLinkJson.Contains("raw.githubusercontent.com/PinguApps/BlazorContentContracts/", StringComparison.Ordinal),
            $"{package}: missing GitHub SourceLink mapping");
        Require(!names.Any(name => name.Contains("CodeAnalysis", StringComparison.Ordinal)),
            $"{package}: bundled Roslyn assets");

        if (package.EndsWith(".Analyzers", StringComparison.Ordinal))
        {
            Require(!names.Any(name => name.StartsWith("lib/", StringComparison.Ordinal)), $"{package}: unexpected runtime assets");
        }
    }

    var tracking = Path.Combine(root, "src", prefix + ".Analyzers");
    Require(File.ReadAllText(Path.Combine(tracking, "AnalyzerReleases.Shipped.md")).Contains("## Release 1.0.0", StringComparison.Ordinal),
        "Missing shipped 1.0.0 release tracking.");

    Require(File.ReadLines(Path.Combine(tracking, "AnalyzerReleases.Unshipped.md"))
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .SequenceEqual(new[]
        {
            "### New Rules",
            "Rule ID | Category | Severity | Notes",
            "--------|----------|----------|-------",
            "### Removed Rules",
            "Rule ID | Category | Severity | Notes",
            "--------|----------|----------|-------",
            "### Changed Rules",
            "Rule ID | New Category | New Severity | Old Category | Old Severity | Notes",
            "--------|--------------|--------------|--------------|--------------|-------"
        }),
        "Unshipped release tracking must retain the blank template.");

    var consumer = Directory.CreateTempSubdirectory("content-contract-consumer-").FullName;

    try
    {
        File.Copy(Path.Combine(root, "global.json"), Path.Combine(consumer, "global.json"));

        File.WriteAllText(Path.Combine(consumer, "Consumer.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="{{prefix}}" Version="1.0.0" />
                <PackageReference Include="{{prefix}}.Analyzers" Version="1.0.0" />
              </ItemGroup>
              <Target Name="RecordAnalyzers" AfterTargets="CoreCompile">
                <WriteLinesToFile File="analyzers.txt" Lines="@(Analyzer)" Overwrite="true" />
              </Target>
            </Project>
            """);

        File.WriteAllText(Path.Combine(consumer, "Program.cs"), "System.Console.WriteLine(\"Package consumer passed\");");
        Run(consumer, "restore", "--source", output, "--packages", Path.Combine(consumer, "packages"));
        Run(consumer, "build", "--no-restore", "-c", "Release");
        Require(File.ReadAllText(Path.Combine(consumer, "analyzers.txt")).Contains($"{prefix}.Analyzers.dll", StringComparison.Ordinal),
            "Consumer did not discover the packaged analyzer.");

        using var deps = JsonDocument.Parse(File.ReadAllText(Path.Combine(consumer, "bin", "Release", "net10.0", "Consumer.deps.json")));
        var runtime = deps.RootElement.GetProperty("targets").EnumerateObject()
            .SelectMany(target => target.Value.EnumerateObject())
            .Where(entry => entry.Value.TryGetProperty("runtime", out _))
            .SelectMany(entry => entry.Value.GetProperty("runtime").EnumerateObject())
            .Select(asset => asset.Name).ToArray();

        Require(runtime.Any(asset => asset.EndsWith($"/{prefix}.dll", StringComparison.Ordinal)),
            "Consumer runtime is missing the annotation assembly.");

        Require(!runtime.Any(asset => asset.Contains(".Analyzers.dll", StringComparison.Ordinal) || asset.Contains("CodeAnalysis", StringComparison.Ordinal)),
            "Consumer runtime contains analyzer or Roslyn assets.");

        Run(consumer, "run", "--no-build", "--no-restore", "-c", "Release");
    }

    finally
    {
        Directory.Delete(consumer, recursive: true);
    }
}

finally
{
    Require(PersistentPaths() == persistentPaths,
        "Persistent PATH changed; inspect shared settings before any restoration.");

    Require(Environment.GetEnvironmentVariable("PATH") == processPath, "Process PATH changed.");
}

Console.WriteLine("Build, tests, release tracking, package layout and independent consumer passed.");

void Run(string cwd, params string[] arguments)
{
    var start = new ProcessStartInfo(dotnet!) { WorkingDirectory = cwd, UseShellExecute = false };

    foreach (var argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }

    start.Environment["BCC_PACKAGES"] = Path.Combine(root, "artifacts", "packages");
    start.Environment["DOTNET_HOST_PATH"] = dotnet!;
    start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
    start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
    start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet.");
    process.WaitForExit();

    Require(process.ExitCode == 0, $"dotnet {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.");
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static (string? User, string? Machine) PersistentPaths() => OperatingSystem.IsWindows()
    ? (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User),
       Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine))
    : (null, null);

