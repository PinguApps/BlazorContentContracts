Feature: Bounded analysis

    Scenario Outline: Recursive and deeply nested output preserves independent findings
        Given bounded output containing a '<kind>'
        When the real Razor SDK builds the consumer
        Then bounded analysis reports '<rule>' and preserves the invalid sibling

        Examples:
            | kind | rule |
            | cycle | BCC012 |
            | fragment cycle | BCC012 |
            | depth limit | BCC013 |
            | work limit | BCC013 |

    Scenario: Wrapper memoization preserves distinct template arguments
        Given a local wrapper with output '@Body' and usage '<Wrapper><Body><Good /></Body></Wrapper><Wrapper><Body><span /></Body></Wrapper>'
        When the real Razor SDK builds the consumer
        Then unresolved output has 0 warnings and 1 definite violations

    Scenario: Wrapper memoization preserves different generic constructions
        Given a local wrapper with output '@Body' and usage '<GenericWrapper Value="123"><Good /></GenericWrapper><GenericWrapper Value="@("text")"><span /></GenericWrapper>'
        When the real Razor SDK builds the consumer
        Then unresolved output has 0 warnings and 1 definite violations

    Scenario: Wrapper memoization preserves distinct receiving contracts
        Given bounded output containing a 'contracts'
        When the real Razor SDK builds the consumer
        Then unresolved output has 0 warnings and 1 definite violations

    Scenario: Reusing a completed fragment is not recursion
        Given a composed Razor consumer using '<Slot>@Item@Item<span /></Slot> @code { private RenderFragment Item => builder => { builder.OpenComponent<Good>(0); builder.CloseComponent(); }; }'
        When the real Razor SDK builds the consumer
        Then unresolved output has 0 warnings and 1 definite violations
