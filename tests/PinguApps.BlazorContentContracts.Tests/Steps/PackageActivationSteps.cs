using System;
using PinguApps.BlazorContentContracts.Tests.Support;
using Reqnroll;

namespace PinguApps.BlazorContentContracts.Tests.Steps;

[Binding]
public sealed class PackageActivationSteps : IDisposable
{
    private readonly PackageActivationFixture fixture = new();

    [Given("a clean package graph using {string}")]
    public void GivenGraph(string mode)
    {
        fixture.Create(mode);
    }

    [When("the activation consumer builds")]
    public void WhenBuild()
    {
        fixture.Build();
    }

    [Then("package checking is {string}")]
    public void ThenChecking(string enabled)
    {
        fixture.Verify(enabled == "enabled");
    }

    public void Dispose()
    {
        fixture.Dispose();
    }
}
