using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace PinguApps.BlazorContentContracts.Tests.Support;

internal sealed class ReleaseArtifactFixture : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("bcc-release-").FullName;

    public void Create(string condition)
    {
        var names = new[] { "PinguApps.BlazorContentContracts", "PinguApps.BlazorContentContracts.Analyzers" }
            .SelectMany(id => new[] { id + ".1.2.3.nupkg", id + ".1.2.3.snupkg" }).ToArray();

        foreach (var name in names)
        {
            File.WriteAllText(Path.Combine(directory, name), "validated bytes");
        }

        var files = names.Select(name => new
        {
            name,
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, name))))
        }).ToArray();
        File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(new
        {
            repository = "PinguApps/BlazorContentContracts",
            source = condition == "wrong source" ? "other" : "source",
            version = "1.2.3",
            files
        }));

        if (condition == "changed bytes")
        {
            File.AppendAllText(Path.Combine(directory, names[0]), "modified");
        }

        if (condition == "extra package")
        {
            File.WriteAllText(Path.Combine(directory, "extra.nupkg"), "unexpected");
        }
    }

    public int Verify()
    {
        return ReleaseScript.Run("Test-ReleaseArtifacts.ps1", "-Directory", directory,
            "-Repository", "PinguApps/BlazorContentContracts", "-Source", "source", "-Version", "1.2.3").ExitCode;
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }
}
