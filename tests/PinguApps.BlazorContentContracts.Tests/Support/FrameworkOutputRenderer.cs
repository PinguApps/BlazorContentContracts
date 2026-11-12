using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging.Abstractions;

namespace PinguApps.BlazorContentContracts.Tests.Support;

#pragma warning disable BL0006
internal sealed class FrameworkOutputRenderer(IServiceProvider services, bool interactive)
    : Renderer(services, NullLoggerFactory.Instance)
{
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    protected override IComponentRenderMode? GetComponentRenderMode(IComponent component)
    {
        return interactive ? new InteractiveServerRenderMode() : null;
    }

    protected override void HandleException(Exception exception)
    {
        throw exception;
    }

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        return Task.CompletedTask;
    }

    internal Task<string[]> Render(Type type, ParameterView parameters)
    {
        return Dispatcher.InvokeAsync(async () =>
        {
            var id = AssignRootComponentId(InstantiateComponent(type));
            await RenderRootComponentAsync(id, parameters);
            var frames = GetCurrentRenderTreeFrames(id);
            return frames.Array.Take(frames.Count).Where(frame => frame.FrameType == RenderTreeFrameType.Element)
                .Select(frame => frame.ElementName).ToArray();
        });
    }
}