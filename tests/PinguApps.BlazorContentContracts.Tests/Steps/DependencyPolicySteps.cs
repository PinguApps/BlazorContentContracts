using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorContentContracts.Tests.Steps;

[Binding]
public sealed class DependencyPolicySteps
{
    private bool approval;

    [When("dependency {string} is proposed at {string}")]
    public void WhenProposed(string package, string version)
    {
        var root = Environment.GetEnvironmentVariable("BCC_REPOSITORY_ROOT")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        using var policy = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "renovate.json")));
        var rules = policy.RootElement.GetProperty("packageRules").EnumerateArray();

        approval = rules.Any(rule => rule.TryGetProperty("dependencyDashboardApproval", out var required)
            && required.GetBoolean()
            && rule.GetProperty("matchPackageNames").EnumerateArray().Any(name => name.GetString() == package)
            && Matches(rule.GetProperty("matchNewValue").GetString()!, version));
    }

    [Then("the local dependency policy requires approval {string}")]
    public void ThenApproval(string expected)
    {
        Assert.Equal(bool.Parse(expected), approval);
    }

    private static bool Matches(string expression, string version)
    {
        Assert.StartsWith("!/", expression);
        Assert.EndsWith("/", expression);

        return !Regex.IsMatch(version, expression.Substring(2, expression.Length - 3));
    }
}
