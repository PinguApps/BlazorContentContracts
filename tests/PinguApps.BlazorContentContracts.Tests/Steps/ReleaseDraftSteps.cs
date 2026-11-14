using PinguApps.BlazorContentContracts.Tests.Support;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorContentContracts.Tests.Steps;

[Binding]
public sealed class ReleaseDraftSteps
{
    private int exitCode;
    private string output = "";

    [When("a release draft is selected for {string}")]
    public void WhenSelected(string scenario)
    {
        (exitCode, output) = ReleaseScript.Run("../tests/PinguApps.BlazorContentContracts.Tests/Support/Test-ReleaseDraft.ps1", "-Scenario", scenario);
    }

    [Then("draft selection has outcome {string}")]
    public void ThenOutcome(string outcome)
    {
        if (outcome == "fail")
        {
            Assert.NotEqual(0, exitCode);
            return;
        }

        Assert.Equal(0, exitCode);

        if (outcome == "skip")
        {
            Assert.Equal("", output.Trim());
            return;
        }

        Assert.Contains("\"tag\":\"v1.1.0\"", output);
        Assert.Contains("\"source\":", output);
    }
}

