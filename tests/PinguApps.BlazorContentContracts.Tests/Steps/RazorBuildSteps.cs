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

    [Given("a markup contract allowing {string} with text {string} and usage {string}")]
    public void GivenMarkup(string html, string text, string usage)
    {
        fixture.GivenMarkup(html, text, usage);
    }

    [Then("the markup build reports {string} exactly {int} times")]
    public void ThenMarkupDiagnostics(string rule, int count)
    {
        fixture.ThenMarkupDiagnostics(rule, count);
    }

    public void Dispose()
    {
        fixture.Dispose();
    }

    [Given("a composed Razor consumer using {string}")]
    public void GivenComposition(string usage)
    {
        fixture.GivenComposition(usage);
    }

    [Given("a composed Razor consumer using")]
    public void GivenMultilineComposition(string usage)
    {
        fixture.GivenComposition(usage);
    }

    [Given("a Razor rendering comparison with a suppression comment")]
    public void GivenRendering()
    {
        fixture.GivenRendering();
    }

    [Then("the suppressed and original components render identical markup")]
    public void ThenRendering()
    {
        fixture.ThenRendering();
    }

    [Given("unknown output severity is {string}")]
    public void GivenSeverity(string severity)
    {
        fixture.GivenSeverity(severity);
    }

    [Then("unresolved output has {int} warnings and {int} definite violations")]
    public void ThenUnknown(int unknown, int invalid)
    {
        fixture.ThenUnknown(unknown, invalid);
    }

    [Then("unresolved output is an error")]
    public void ThenUnknownError()
    {
        fixture.ThenUnknown(1, 0, "error");
    }

    [Then("suppression diagnostics are {string}")]
    public void ThenSuppressions(string rules)
    {
        fixture.ThenSuppressions(rules);
    }
}
