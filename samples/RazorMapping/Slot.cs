using Microsoft.AspNetCore.Components;
using PinguApps.BlazorContentContracts;

public sealed class Slot : ComponentBase
{
    [Parameter, AllowedChildren(typeof(Good))]
    public RenderFragment? ChildContent { get; set; }
}
