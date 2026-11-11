using System;
using PinguApps.BlazorContentContracts.Tests.Support;
using Reqnroll;

namespace PinguApps.BlazorContentContracts.Tests.Steps;

[Binding]
public sealed class CompiledLibrarySteps : IDisposable
{
    private readonly CompiledLibraryFixture fixture = new();

    [Given("independently packed libraries with a {string} wrapper")]
    public void GivenLibraries(string wrapper)
    {
        fixture.Create(wrapper);
    }

    [When("the packed composition consumer builds")]
    public void WhenBuild()
    {
        fixture.Build();
    }

    [Then("the packed consumer reports {string} exactly {int} times")]
    public void ThenDiagnostics(string rule, int count)
    {
        fixture.Verify(rule, count);
    }

    public void Dispose()
    {
        fixture.Dispose();
    }
}
