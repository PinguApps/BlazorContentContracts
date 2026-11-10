Feature: Razor suppressions acknowledge one unknown usage

    Scenario Outline: Suppress only the next usage and specified diagnostic
        Given a composed Razor consumer using '<usage>'
        And unknown output severity is "error"
        When the real Razor SDK builds the consumer
        Then suppression diagnostics are '<rules>'

        Examples:
            | usage | rules |
            | <Slot>@* bcc-suppress-next BCC006: reviewed *@ @Fragment</Slot>@code { private RenderFragment Fragment { get; set; } } | none |
            | <Slot>@* bcc-suppress-next BCC006: reviewed *@ @Fragment @Fragment</Slot>@code { private RenderFragment Fragment { get; set; } } | BCC006 |
            | <Slot>@Fragment @* bcc-suppress-next BCC006: reviewed *@ @Fragment</Slot>@code { private RenderFragment Fragment { get; set; } } | BCC006,BCC008 |
            | <Slot><ChildContent>@* bcc-suppress-next BCC006: reviewed *@ <DynamicComponent Type="@typeof(Bad)" /></ChildContent></Slot> | BCC006,BCC008 |
            | <Slot>@* bcc-suppress-next BCC006: reviewed *@ <DynamicComponent Type="@typeof(Bad)" /><Bad /></Slot> | BCC001 |
            | @* bcc-suppress-next BCC006: reviewed *@ <Slot ChildContent="@Fragment" /><Slot ChildContent="@Fragment" />@code { private RenderFragment Fragment { get; set; } } | BCC006 |
            | <Slot>@* bcc-suppress-next BCC004: reviewed *@ @((MarkupString)"<b />")</Slot> | none |
            | <Slot>@* bcc-suppress-next BCC006: reviewed *@ @((MarkupString)"<b />")</Slot> | BCC004,BCC008 |
            | <Slot>@* bcc-suppress-next BCC004: reviewed *@ @Fragment</Slot>@code { private RenderFragment Fragment { get; set; } } | BCC006,BCC008 |
            | <Slot>@* bcc-suppress-next BCC006: *@ @Fragment</Slot>@code { private RenderFragment Fragment { get; set; } } | BCC006,BCC007 |
            | <Slot>@* bcc-suppress-next BCC001: reviewed *@ <Bad /></Slot> | BCC001,BCC007 |
            | <Slot>@* bcc-suppress-all BCC006: reviewed *@ @Fragment</Slot>@code { private RenderFragment Fragment { get; set; } } | BCC006,BCC007 |
            | <Slot>@* bcc-suppress-next BCC006: reviewed *@ <Good />@Fragment</Slot>@code { private RenderFragment Fragment { get; set; } } | BCC006,BCC008 |
            | <Slot>@* bcc-suppress-next BCC006: reviewed *@ text @Fragment</Slot>@code { private RenderFragment Fragment { get; set; } } | BCC005,BCC006,BCC008 |
            | <Slot>@* bcc-suppress-next BCC006: reviewed *@</Slot> | BCC008 |
            | <Slot>@("@* bcc-suppress-next BCC006: literal text *@")@Fragment</Slot>@code { private RenderFragment Fragment { get; set; } } | BCC005,BCC006 |
            | <Slot>@($"@* bcc-suppress-next BCC006: literal text *@")@Fragment</Slot>@code { private RenderFragment Fragment { get; set; } } | BCC005,BCC006 |
            | <Slot>@Fragment</Slot>@code { /* @* bcc-suppress-next BCC006: C# comment *@ */ private RenderFragment Fragment { get; set; } } | BCC006 |
            | <Slot>@* bcc-suppress-next BCC006: reviewed builder output *@ @((RenderFragment)(builder => { Emit(builder); }))</Slot>@code { private void Emit(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) { } } | none |
            | <Slot>@* bcc-suppress-next BCC006: reviewed builder output *@ @((RenderFragment)(builder => { Emit(builder); builder.OpenComponent<Bad>(0); builder.CloseComponent(); }))</Slot>@code { private void Emit(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) { } } | BCC001 |

    Scenario: Whitespace attaches the comment across lines
        Given a composed Razor consumer using
            """
            <Slot>
                @* bcc-suppress-next BCC006: reviewed *@

                @Fragment
            </Slot>
            @code { private RenderFragment Fragment { get; set; } }
            """
        And unknown output severity is "error"
        When the real Razor SDK builds the consumer
        Then suppression diagnostics are 'none'

    Scenario: Suppression comments preserve rendered output
        Given a Razor rendering comparison with a suppression comment
        When the real Razor SDK builds the consumer
        Then the suppressed and original components render identical markup
