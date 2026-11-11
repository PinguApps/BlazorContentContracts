using System.Threading.Tasks;
using PinguApps.BlazorContentContracts.Tests.Support;
using Reqnroll;

namespace PinguApps.BlazorContentContracts.Tests.Steps;

[Binding]
public sealed class MetadataCompatibilitySteps
{
    [Then("an annotation assembly with {string} reports an incompatible metadata warning")]
    public Task Verify(string conflict)
    {
        return MetadataConflictFixture.Verify(conflict);
    }
}
