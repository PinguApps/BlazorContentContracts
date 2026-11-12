using Microsoft.AspNetCore.Components.Forms;

namespace PinguApps.BlazorContentContracts.Tests.Support;

internal sealed class TestAntiforgeryStateProvider(bool available) : AntiforgeryStateProvider
{
    public override AntiforgeryRequestToken? GetAntiforgeryToken()
    {
        return available ? new AntiforgeryRequestToken("test-token", "test-field") : null;
    }
}