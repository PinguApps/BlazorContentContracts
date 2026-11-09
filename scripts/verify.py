"""Build, test, pack and exercise the actual packages in an independent consumer."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
DOTNET = shutil.which("dotnet")
if not DOTNET:
    raise RuntimeError("Install the SDK selected by global.json first")


def persistent_path():
    if os.name != "nt":
        return None
    import winreg
    values = []
    for hive, key in [(winreg.HKEY_CURRENT_USER, "Environment"),
                      (winreg.HKEY_LOCAL_MACHINE, r"SYSTEM\CurrentControlSet\Control\Session Manager\Environment")]:
        with winreg.OpenKey(hive, key) as handle:
            values.append(winreg.QueryValueEx(handle, "Path"))
    return values


def run(*args, cwd=ROOT):
    subprocess.run([DOTNET, *args], cwd=cwd, env=ENV, check=True)


before = persistent_path()
process_path = os.environ.get("PATH")
ENV = dict(os.environ, DOTNET_ADD_GLOBAL_TOOLS_TO_PATH="0",
           DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1", DOTNET_CLI_TELEMETRY_OPTOUT="1")
try:
    run("build", "-c", "Release")
    run("test", "-c", "Release", "--no-build")
    output = ROOT / "artifacts" / "packages"
    output.mkdir(parents=True, exist_ok=True)
    run("pack", "-c", "Release", "--no-build", "-o", str(output))
    prefix = "PinguApps.BlazorContentContracts"
    for package, expected in [(prefix, f"lib/net10.0/{prefix}.dll"),
                              (prefix + ".Analyzers", f"analyzers/dotnet/cs/{prefix}.Analyzers.dll")]:
        with zipfile.ZipFile(output / f"{package}.1.0.0.nupkg") as archive:
            names = archive.namelist()
            assert expected in names, names
            assert not any("CodeAnalysis" in name for name in names), names
            if package.endswith(".Analyzers"):
                assert not any(name.startswith("lib/") for name in names), names
    tracking = ROOT / "src" / (prefix + ".Analyzers")
    assert "## Release 1.0.0" in (tracking / "AnalyzerReleases.Shipped.md").read_text()
    assert all(not line.strip() or line.startswith(";") for line in
               (tracking / "AnalyzerReleases.Unshipped.md").read_text().splitlines())
    with tempfile.TemporaryDirectory(prefix="content-contract-consumer-") as temporary:
        consumer = Path(temporary)
        (consumer / "global.json").write_bytes((ROOT / "global.json").read_bytes())
        (consumer / "Consumer.csproj").write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="{prefix}" Version="1.0.0" />
    <PackageReference Include="{prefix}.Analyzers" Version="1.0.0" />
  </ItemGroup>
  <Target Name="RecordAnalyzers" AfterTargets="CoreCompile">
    <WriteLinesToFile File="analyzers.txt" Lines="@(Analyzer)" Overwrite="true" />
  </Target>
</Project>''')
        (consumer / "Program.cs").write_text('System.Console.WriteLine("Package consumer passed");')
        run("restore", "--source", str(output), "--packages", str(consumer / "packages"), cwd=consumer)
        run("build", "--no-restore", "-c", "Release", cwd=consumer)
        assert f"{prefix}.Analyzers.dll" in (consumer / "analyzers.txt").read_text()
        deps = json.loads((consumer / "bin/Release/net10.0/Consumer.deps.json").read_text())
        runtime = str([entry.get("runtime", {}) for target in deps["targets"].values() for entry in target.values()])
        assert f"{prefix}.dll" in runtime
        assert ".Analyzers.dll" not in runtime and "CodeAnalysis" not in runtime
        run("run", "--no-build", "--no-restore", "-c", "Release", cwd=consumer)
    print("Build, tests, release tracking, package layout and independent consumer passed.")
finally:
    assert persistent_path() == before, "Persistent PATH changed; inspect shared settings before any restoration"
    assert os.environ.get("PATH") == process_path, "Process PATH changed"
