using PinguApps.BlazorContentContracts.Tests.Support;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorContentContracts.Tests.Steps;

[Binding]
public sealed class ReleaseArtifactSteps
{
    private readonly ReleaseArtifactFixture fixture = new();
    private int exitCode;

    [Given("a release artifact manifest with {string}")]
    public void GivenManifest(string condition)
    {
        fixture.Create(condition);
    }

    [When("the release artifact manifest is verified")]
    public void WhenVerified()
    {
        try
        {
            exitCode = fixture.Verify();
        }

        finally
        {
            fixture.Dispose();
        }
    }

    [Then("artifact verification {string}")]
    public void ThenResult(string result)
    {
        Assert.Equal(result == "succeeds", exitCode == 0);
    }
}
