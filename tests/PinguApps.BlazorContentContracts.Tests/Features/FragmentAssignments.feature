Feature: Fragment assignments preserve every possible output

    Scenario Outline: Check lifecycle and event writes to fragment storage
        Given a composed Razor consumer using
            """
            <Slot>@fragment</Slot>
            @code {
                private RenderFragment fragment = builder => { };
                <write>
            }
            """
        When the real Razor SDK builds the consumer
        Then unresolved output has <unknown> warnings and <invalid> definite violations

        Examples:
            | write | unknown | invalid |
            | protected override void OnInitialized() { fragment = b => { b.AddContent(0, "bad"); }; } | 0 | 1 |
            | private void Click() { fragment += b => { b.AddContent(0, "bad"); }; } | 0 | 1 |
            | private void Click() { fragment = Factory(); } private RenderFragment Factory() => b => { b.AddContent(0, "bad"); }; | 0 | 1 |
            | private void Click() { fragment = Service(); } private RenderFragment Service() => throw new System.Exception(); | 1 | 0 |
            | private void Click() { fragment -= b => { }; } | 1 | 0 |
            | private void Click() { fragment = b => { b.AddContent(0, "bad"); }; fragment = Service(); } private RenderFragment Service() => throw new System.Exception(); | 1 | 1 |

    Scenario: Check a reassigned local forwarded to a slot
        Given a composed Razor consumer using
            """
            @{ RenderFragment value = b => { }; value = b => { b.AddContent(0, "bad"); }; }
            <Slot ChildContent="@value" />
            """
        When the real Razor SDK builds the consumer
        Then unresolved output has 0 warnings and 1 definite violations

    Scenario: Check a typed fragment reassigned during lifecycle
        Given a composed Razor consumer using
            """
            <Slot>@fragment(1)</Slot>
            @code {
                private RenderFragment<int> fragment = item => b => { };
                protected override void OnInitialized() { fragment = item => b => { b.AddContent(0, "bad"); }; }
            }
            """
        When the real Razor SDK builds the consumer
        Then unresolved output has 0 warnings and 1 definite violations

    Scenario: Preserve opaque output through a local alias
        Given a composed Razor consumer using
            """
            @{ RenderFragment value = Service(); RenderFragment alias = value; }
            <Slot ChildContent="@alias" />
            @code { private RenderFragment Service() => throw new System.Exception(); }
            """
        When the real Razor SDK builds the consumer
        Then unresolved output has 1 warnings and 0 definite violations

    Scenario: Check writes to an annotated parameter used by a restricted slot
        Given a composed Razor consumer using
            """
            @using PinguApps.BlazorContentContracts
            <Slot ChildContent="@Content" />
            @code {
                [Parameter, AllowedChildren(typeof(Good))] public RenderFragment Content { get; set; }
                protected override void OnInitialized() { Content = b => { b.AddContent(0, "bad"); }; }
            }
            """
        When the real Razor SDK builds the consumer
        Then the forwarding build reports 'BCC010' exactly 1 times

    Scenario: Delegate addition checks both operands
        Given a composed Razor consumer using
            """
            @{ RenderFragment good = b => { }; RenderFragment bad = b => { b.AddContent(0, "bad"); }; RenderFragment combined = good + bad; }
            <Slot ChildContent="@combined" />
            """
        When the real Razor SDK builds the consumer
        Then unresolved output has 0 warnings and 1 definite violations

    Scenario: Inspect getter return values
        Given a composed Razor consumer using
            """
            <Slot>@Content</Slot>
            @code { private RenderFragment Content { get { RenderFragment value = b => { }; value = b => { b.AddContent(0, "bad"); }; return value; } } }
            """
        When the real Razor SDK builds the consumer
        Then unresolved output has 0 warnings and 1 definite violations

    Scenario: Check an unused contracted property write
        Given a composed Razor consumer using
            """
            @using PinguApps.BlazorContentContracts
            <Slot />
            @code {
                [Parameter, AllowedChildren(typeof(Good))] public RenderFragment<int> Content { get; set; }
                private void Click() { Content = item => b => { b.AddContent(0, "bad"); }; }
            }
            """
        When the real Razor SDK builds the consumer
        Then the forwarding build reports 'BCC010' exactly 1 times

    Scenario: An opaque write to a contracted property remains unknown
        Given a composed Razor consumer using
            """
            @using PinguApps.BlazorContentContracts
            <Slot />
            @code {
                [Parameter, AllowedChildren(typeof(Good))] public RenderFragment Content { get; set; }
                private RenderFragment Service() => throw new System.Exception();
                private void Click() { Content = Service(); }
            }
            """
        When the real Razor SDK builds the consumer
        Then the forwarding build reports 'BCC011' exactly 1 times

    Scenario: Declared factory returns include local reassignment
        Given a composed Razor consumer using
            """
            @using PinguApps.BlazorContentContracts
            <Slot />
            @code {
                [ProducesContent(typeof(Good))] private RenderFragment Factory()
                {
                    RenderFragment value = b => { };
                    value = b => { b.AddContent(0, "bad"); };
                    return value;
                }
            }
            """
        When the real Razor SDK builds the consumer
        Then the forwarding build reports 'BCC010' exactly 1 times

    Scenario: Inherited contract storage writes are checked
        Given a family contract allowing 'Good' with '' and 'public override RenderFragment ChildContent { get; set; } = b => { b.AddContent(0, "bad"); };'
        When the real Razor SDK builds the consumer
        Then the family build reports 'BCC010' exactly 1 times

    Scenario: Captured factory output remains unknown
        Given a composed Razor consumer using
            """
            @{ RenderFragment captured = b => { }; System.Func<RenderFragment> factory = () => captured; }
            <Slot>@factory()</Slot>
            """
        When the real Razor SDK builds the consumer
        Then unresolved output has 1 warnings and 0 definite violations
