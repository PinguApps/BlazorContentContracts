using System;
using PinguApps.BlazorContentContracts.Tests.Support;
using Reqnroll;

namespace PinguApps.BlazorContentContracts.Tests.Steps;

[Binding]
public sealed class RazorBuildSteps : IDisposable
{
    private readonly RazorBuildFixture fixture = new();

    [Then("the Razor build explains {string}")]
    public void ThenOutputContains(string text)
    {
        fixture.ThenOutputContains(text);
    }

    [Given("a declared output factory with body {string} and usage {string}")]
    public void GivenOutput(string body, string usage)
    {
        fixture.GivenOutput(body, usage);
    }

    [Given("a separately compiled output library declaring {string}")]
    public void GivenPublishedOutput(string permission)
    {
        fixture.GivenPublishedOutput(permission);
    }

    [Given("a locally suppressed output factory emitting {string}")]
    public void GivenOutputSuppression(string body)
    {
        fixture.GivenOutputSuppression(body);
    }

    [Given("a declared component output with markup {string}")]
    public void GivenComponentOutput(string markup)
    {
        fixture.GivenComponentOutput(markup);
    }

    [Given("fixed and forwarded output received by {string} with content {string}")]
    public void GivenCombinedOutput(string permissions, string child)
    {
        fixture.GivenCombinedOutput(permissions, child);
    }

    [Then("the forwarding build reports {string} exactly {int} times")]
    public void ThenForwarding(string rule, int count)
    {
        fixture.ThenForwarding(rule, count);
    }

    [Given("a forwarding parameter allowing {string} with usage {string}")]
    public void GivenForwarding(string permissions, string usage)
    {
        fixture.GivenForwarding(permissions, usage);
    }

    [Given("a local wrapper with output {string} and usage {string}")]
    public void GivenWrapper(string output, string usage)
    {
        fixture.GivenWrapper(output, usage);
    }

    [Given("bounded output containing a {string}")]
    public void GivenBoundedOutput(string kind)
    {
        fixture.GivenBoundedOutput(kind);
    }

    [Then("bounded analysis reports {string} and preserves the invalid sibling")]
    public void ThenBoundedOutput(string rule)
    {
        fixture.ThenBoundedOutput(rule);
    }

    [Then("the wrapper violation names its path at the consumer")]
    public void ThenWrapperPath()
    {
        fixture.ThenWrapperPath();
    }

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

    [Then("EditForm output has {int} definite violations and version-specific uncertainty")]
    public void ThenEditForm(int invalid)
    {
        fixture.ThenUnknown(Environment.GetEnvironmentVariable("BCC_CONSUMER_FRAMEWORK") == "net10.0" ? 0 : 1, invalid);
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
