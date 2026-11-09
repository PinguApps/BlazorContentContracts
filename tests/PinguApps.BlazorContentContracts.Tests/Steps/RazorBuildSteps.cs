using System;
using PinguApps.BlazorContentContracts.Tests.Support;
using Reqnroll;

namespace PinguApps.BlazorContentContracts.Tests.Steps;

[Binding]
public sealed class RazorBuildSteps : IDisposable
{
    private readonly RazorBuildFixture fixture = new();

    [Given("a Razor consumer with {string} content")]
    public void GivenConsumer(string kind)
    {
        fixture.GivenConsumer(kind);
    }

    [When("the real Razor SDK builds the consumer")]
    public void WhenBuild()
    {
        fixture.WhenBuild();
    }

    [Then("the build has {string} content contract diagnostics")]
    public void ThenDiagnostics(string expected)
    {
        fixture.ThenDiagnostics(expected);
    }

    public void Dispose()
    {
        fixture.Dispose();
    }
}
