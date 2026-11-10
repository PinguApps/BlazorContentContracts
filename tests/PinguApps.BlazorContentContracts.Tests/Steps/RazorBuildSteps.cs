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

    [Then("the build has {string} declaration diagnostics")]
    public void ThenDeclarationDiagnostics(string expected)
    {
        fixture.ThenDeclarationDiagnostics(expected);
    }

    [Given("a family contract allowing {string} with {string} and {string}")]
    public void GivenFamily(string allowance, string usage, string inheritance)
    {
        fixture.GivenFamily(allowance, usage, inheritance);
    }

    [Then("the family build reports {string} exactly {int} times")]
    public void ThenFamilyDiagnostics(string rule, int count)
    {
        fixture.ThenFamilyDiagnostics(rule, count);
    }

    public void Dispose()
    {
        fixture.Dispose();
    }
}
