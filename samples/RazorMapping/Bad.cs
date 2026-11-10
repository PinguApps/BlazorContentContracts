using Microsoft.AspNetCore.Components;

public sealed class Bad : ComponentBase
{
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        builder.AddContent(0, "forbidden output");
    }
}
