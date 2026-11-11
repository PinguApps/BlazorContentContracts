Feature: Declared output is checked once at its implementation

    Scenario Outline: Validate declared fragment factories without repeated caller uncertainty
        Given a declared output factory with body '<body>' and usage '<usage>'
        When the real Razor SDK builds the consumer
        Then the forwarding build reports '<rule>' exactly <count> times

        Examples:
            | body | usage | rule | count |
            | builder => { builder.OpenComponent<Good>(0); builder.CloseComponent(); } | @Factory() @Factory() | BCC011 | 0 |
            | builder => { builder.OpenComponent<Bad>(0); builder.CloseComponent(); } | @Factory() | BCC010 | 1 |
            | System.Activator.CreateInstance<RenderFragment>() | @Factory() @Factory() | BCC011 | 1 |
            | context => builder => { builder.OpenComponent<Good>(0); builder.CloseComponent(); } | @Factory()(1) | BCC011 | 0 |
            | builder => { builder.OpenElement(0, "span"); builder.CloseElement(); } | @Factory() | BCC010 | 1 |

    Scenario Outline: Trust compatible declarations from real referenced assemblies
        Given a separately compiled output library declaring '<permissions>'
        When the real Razor SDK builds the consumer
        Then the forwarding build reports '<rule>' exactly <count> times

        Examples:
            | permissions | rule | count |
            | typeof(Good) | BCC006 | 0 |
            | HtmlElements = new[] { "span" } | BCC009 | 2 |

    Scenario Outline: Suppress implementation uncertainty while preserving definite errors
        Given a locally suppressed output factory emitting '<body>'
        When the real Razor SDK builds the consumer
        Then the forwarding build reports '<rule>' exactly <count> times

        Examples:
            | body | rule | count |
            | builder.AddContent(0, System.Activator.CreateInstance<RenderFragment>()); | BCC011 | 0 |
            | builder.AddContent(0, System.Activator.CreateInstance<RenderFragment>()); builder.OpenElement(1, "span"); builder.CloseElement(); | BCC010 | 1 |

    Scenario Outline: Check component bodies independently of repeated consumers
        Given a declared component output with markup '<markup>'
        When the real Razor SDK builds the consumer
        Then the forwarding build reports '<rule>' exactly <count> times

        Examples:
            | markup | rule | count |
            | <Good /> | BCC011 | 0 |
            | <span>bad</span> | BCC010 | 2 |
            | @(System.Activator.CreateInstance<RenderFragment>()) | BCC011 | 1 |
            | inherited | BCC010 | 1 |
