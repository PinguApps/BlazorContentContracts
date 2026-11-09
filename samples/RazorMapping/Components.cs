using Microsoft.AspNetCore.Components;
using PinguApps.BlazorContentContracts;

public sealed class Slot : ComponentBase
{
    [Parameter, AllowedChildren(typeof(Good))]
    public RenderFragment? ChildContent { get; set; }
}

public sealed class Good : ComponentBase
{
}

public sealed class Bad : ComponentBase
{
}

