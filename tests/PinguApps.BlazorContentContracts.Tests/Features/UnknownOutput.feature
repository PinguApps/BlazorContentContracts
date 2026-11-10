Feature: Unknown output retains partial knowledge

    Scenario Outline: Explain unresolved output alongside definite violations
        Given a composed Razor consumer using '<usage>'
        When the real Razor SDK builds the consumer
        Then unresolved output has <unknown> warnings and <invalid> definite violations

        Examples:
            | usage | unknown | invalid |
            | <Slot ChildContent="@Fragment" />@code { private RenderFragment Fragment { get; set; } } | 1 | 0 |
            | <Slot>@Fragment<Bad /></Slot>@code { private RenderFragment Fragment { get; set; } } | 1 | 1 |
            | <Slot>@if (true) { @Fragment } else { <Bad /> }</Slot>@code { private RenderFragment Fragment { get; set; } } | 1 | 1 |
            | <Slot>@GetFragment()</Slot>@code { private RenderFragment GetFragment() => null; } | 1 | 0 |
            | <Slot><DynamicComponent Type="@typeof(Bad)" /></Slot> | 1 | 0 |
            | <Slot>@((RenderFragment)(builder => { Emit(builder); builder.OpenComponent<Bad>(0); builder.CloseComponent(); }))</Slot>@code { private void Emit(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) { } } | 1 | 1 |
            | <Slot>@Fragment@Fragment</Slot>@code { private RenderFragment Fragment { get; set; } } | 1 | 0 |

    Scenario: Promote uncertainty through editor configuration
        Given a composed Razor consumer using '<Slot>@Fragment</Slot>@code { private RenderFragment Fragment { get; set; } }'
        And unknown output severity is "error"
        When the real Razor SDK builds the consumer
        Then unresolved output is an error
