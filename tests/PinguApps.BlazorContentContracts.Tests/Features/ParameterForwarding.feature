Feature: Parameter contracts survive forwarding

    Scenario Outline: Compare the source permissions at the receiving boundary
        Given a forwarding parameter allowing '<permissions>' with usage '<usage>'
        When the real Razor SDK builds the consumer
        Then the forwarding build reports '<rule>' exactly <count> times

        Examples:
            | permissions | usage | rule | count |
            | typeof(Good) | <Wrapper><Good /></Wrapper> | BCC006 | 0 |
            | typeof(Good) | <Wrapper><span /></Wrapper> | BCC005 | 1 |
            | typeof(Bad) | <Wrapper /> | BCC009 | 1 |
            | typeof(Good), HtmlElements = new[] { "span" } | <Wrapper /> | BCC009 | 1 |
            | typeof(Good), AllowText = true | <Wrapper /> | BCC009 | 1 |
            | typeof(Good) | <Wrapper /> | BCC009 | 0 |
            | typed | <Wrapper><Good /></Wrapper> | BCC006 | 0 |
            | inherited | <Wrapper><span /></Wrapper> | BCC005 | 1 |
            | conflict | <Wrapper><span /></Wrapper> | BCC003 | 1 |
            | opaque | <Wrapper /> | BCC006 | 1 |
            | typeof(DerivedGood) | <Wrapper /> | BCC009 | 0 |
            | typeof(Row<int>) | <Wrapper /> | BCC009 | 1 |
            | typeof(Row<int>) => typeof(Row<>) | <Wrapper /> | BCC009 | 0 |
            | typeof(Row<>) => typeof(Row<int>) | <Wrapper /> | BCC009 | 1 |
            | typeof(Row<int>) => typeof(Row<string>) | <Wrapper /> | BCC009 | 1 |
            | typeof(Row<int>) => typeof(Row<int>) | <Wrapper /> | BCC009 | 0 |
