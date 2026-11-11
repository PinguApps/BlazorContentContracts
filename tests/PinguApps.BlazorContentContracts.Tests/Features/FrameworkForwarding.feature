Feature: Framework providers preserve content checking

    Scenario Outline: Known providers forward their actual child content
        Given a composed Razor consumer using '<usage>'
        When the real Razor SDK builds the consumer
        Then unresolved output has 0 warnings and <invalid> definite violations

        Examples:
            | usage | invalid |
            | <Slot><CascadingValue Value="1"><Good /></CascadingValue></Slot> | 0 |
            | <Slot><CascadingValue Value="1"><div /></CascadingValue></Slot> | 1 |
            | <Slot><CascadingValue Value="1">visible</CascadingValue></Slot> | 1 |
            | <Slot><CascadingValue Value="1"><Bad /></CascadingValue></Slot> | 1 |
            | <Slot><Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState><Good /></Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState></Slot> | 0 |
            | <Slot><Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState><div /></Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState></Slot> | 1 |
            | <Slot><Microsoft.AspNetCore.Components.Forms.InputRadioGroup TValue="int"><Good /></Microsoft.AspNetCore.Components.Forms.InputRadioGroup></Slot> | 0 |
            | <Slot><Microsoft.AspNetCore.Components.Forms.InputRadioGroup TValue="int"><div /></Microsoft.AspNetCore.Components.Forms.InputRadioGroup></Slot> | 1 |
            | <Slot><Microsoft.AspNetCore.Components.Forms.FormMappingScope Name="scope" Context="mapping"><Good /></Microsoft.AspNetCore.Components.Forms.FormMappingScope></Slot> | 0 |
            | <Slot><Microsoft.AspNetCore.Components.Forms.FormMappingScope Name="scope" Context="mapping"><div /></Microsoft.AspNetCore.Components.Forms.FormMappingScope></Slot> | 1 |
            | <Slot><CascadingValue Value="1"><Microsoft.AspNetCore.Components.Forms.FormMappingScope Name="scope" Context="mapping"><Good /></Microsoft.AspNetCore.Components.Forms.FormMappingScope></CascadingValue></Slot> | 0 |
            | <Slot><CascadingValue Value="1"><Microsoft.AspNetCore.Components.Forms.FormMappingScope Name="scope" Context="mapping"><Bad /></Microsoft.AspNetCore.Components.Forms.FormMappingScope></CascadingValue></Slot> | 1 |

    Scenario: A derived provider rendering override is inspected rather than exempted
        Given a composed Razor consumer using '<Slot><CustomProvider Value="1"><Good /></CustomProvider></Slot>@code { public class CustomProvider : CascadingValue<int> { protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) { builder.OpenElement(0, "div"); builder.CloseElement(); } } }'
        When the real Razor SDK builds the consumer
        Then unresolved output has 0 warnings and 1 definite violations
