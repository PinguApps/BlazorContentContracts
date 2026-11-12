using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using PinguApps.BlazorContentContracts.Tests.Support;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorContentContracts.Tests.Steps;

[Binding]
public sealed class FrameworkRenderingSteps
{
    [Then("actual AntiforgeryToken output with token {string} is {string}")]
    public async Task ThenTokenOutput(string available, string expected)
    {
        using var services = new ServiceCollection()
            .AddSingleton<Microsoft.AspNetCore.Components.Forms.AntiforgeryStateProvider>(new TestAntiforgeryStateProvider(available == "available"))
            .BuildServiceProvider();
        await using var renderer = new FrameworkOutputRenderer(services, false);
        var output = await renderer.Render(typeof(Microsoft.AspNetCore.Components.Forms.AntiforgeryToken), ParameterView.Empty);

        Assert.Equal(expected == "empty" ? Array.Empty<string>() : new[] { expected }, output);
    }

    [Then("actual FocusOnNavigate output in {string} mode is {string}")]
    public async Task ThenFocusOutput(string mode, string expected)
    {
        using var services = new ServiceCollection().AddSingleton<Microsoft.JSInterop.IJSRuntime, NoopJsRuntime>().BuildServiceProvider();
        await using var renderer = new FrameworkOutputRenderer(services, mode == "interactive");
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            ["RouteData"] = new RouteData(typeof(FocusOnNavigate), new Dictionary<string, object?>()),
            ["Selector"] = "h1"
        });
        var output = await renderer.Render(typeof(FocusOnNavigate), parameters);

        Assert.Equal(expected == "empty" ? Array.Empty<string>() : new[] { expected }, output);
    }
}