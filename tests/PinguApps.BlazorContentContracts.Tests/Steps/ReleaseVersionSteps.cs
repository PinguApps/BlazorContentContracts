using PinguApps.BlazorContentContracts.Tests.Support;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorContentContracts.Tests.Steps;

[Binding]
public sealed class ReleaseVersionSteps
{
    private int exitCode;
    private string output = "";

    [When("the release version is resolved for tag {string} and name {string}")]
    public void WhenResolved(string tag, string name)
    {
        (exitCode, output) = ReleaseScript.Run("Get-ReleaseVersion.ps1", "-Tag", tag, "-Name", name);
    }

    [Then("release version resolution fails")]
    public void ThenFails()
    {
        Assert.NotEqual(0, exitCode);
    }

    [Then("the resolved release version is {string}")]
    public void ThenVersion(string version)
    {
        Assert.Equal(0, exitCode);
        Assert.Equal(version, output.Trim());
    }
}
