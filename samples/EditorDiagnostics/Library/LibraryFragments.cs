using System;
using Microsoft.AspNetCore.Components;

namespace EditorContracts.Library;

public static class LibraryFragments
{
    public static RenderFragment Unknown()
    {
        throw new NotSupportedException("This is a compile-time diagnostic fixture.");
    }
}
