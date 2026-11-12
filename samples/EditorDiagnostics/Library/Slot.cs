using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using PinguApps.BlazorContentContracts;

namespace EditorContracts.Library;

public sealed class Slot : ComponentBase
{
    [Parameter, AllowedChildren(typeof(Good))]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.AddContent(0, ChildContent);
    }
}
