using System;
using System.Threading;
using PinguApps.BlazorContentContracts.Analyzers;
using PinguApps.BlazorContentContracts.Tests.Support;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorContentContracts.Tests.Steps;

[Binding]
public sealed class AnalysisInvalidationSteps
{
    [Then("measure bounded SDK builds for {string}")]
    public void ThenMeasure(string shape)
    {
        using var fixture = new RazorBuildFixture();
        fixture.MeasureBoundedBuild(shape);
    }

    [Then("cancellation interrupts content traversal before more work is accepted")]
    public void ThenCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var traversal = new AnalysisTraversal(cancellation.Token);
        Assert.True(traversal.Enter(new object(), "root"));
        var child = traversal.Fork();

        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => child.Enter(new object(), "child"));
        Assert.Throws<OperationCanceledException>(() => traversal.Cycle("root"));
    }

    [Then("repeated SDK builds invalidate a changed {string}")]
    public void ThenEditsInvalidate(string source)
    {
        using var fixture = new RazorBuildFixture();
        fixture.VerifyEditInvalidation(source);
    }
}
