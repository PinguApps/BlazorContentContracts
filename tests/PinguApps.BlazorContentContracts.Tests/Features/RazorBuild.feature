Feature: Real Razor build diagnostics
    Scenario Outline: Static restricted slots use original Razor locations
        Given a Razor consumer with "<content>" content
        When the real Razor SDK builds the consumer
        Then the build has "<outcome>" content contract diagnostics

        Examples:
            | content | outcome |
            | invalid | one error |
            | valid   | none |
            | empty   | none |
            | comment | one error |

    Scenario Outline: Declaration annotations work in a real packaged Razor consumer
        Given a Razor consumer with "<content>" content
        When the real Razor SDK builds the consumer
        Then the build has "<outcome>" declaration diagnostics

        Examples:
            | content                | outcome   |
            | declarations           | none      |
            | malformed declarations | one error |

    Scenario Outline: Component families retain inherited slot permissions
        Given a family contract allowing "<allowance>" with '<usage>' and "<inheritance>"
        When the real Razor SDK builds the consumer
        Then the family build reports "<rule>" exactly <errors> times

        Examples:
            | allowance             | usage                                      | inheritance | errors | rule |
            | Families.Family<>     | <Family T="int" /><Family T="string" />   |             | 0      | BCC001 |
            | Families.Family<int>  | <Derived />                                |             | 0      | BCC001 |
            | Families.Family<int>  | <Family T="string" />                      |             | 1      | BCC001 |
            | Families.Family<>     | <Other.Family T="int" />                   |             | 1      | BCC001 |
            | Families.Family<>     | <Family Value="123" />                     |             | 0      | BCC001 |
            | Families.Family<int>  | <Family Value="123" />                     |             | 0      | BCC001 |
            | Families.Family<string> | <Family Value="123" />                   |             | 1      | BCC001 |
            | Families.Family<>     | <Families.Family T="int"><Bad /></Families.Family> |      | 0      | BCC001 |
            | Good                  | <Bad />                                    | public override Microsoft.AspNetCore.Components.RenderFragment ChildContent { get; set; } | 1 | BCC001 |
            | Good                  | <Bad />                                    | public new Microsoft.AspNetCore.Components.RenderFragment ChildContent { get; set; } | 1 | BCC001 |
            | Good | <Good /> | [Parameter, PinguApps.BlazorContentContracts.AllowedChildren(AllowText = true)] public override RenderFragment ChildContent { get; set; } | 1 | BCC003 |
            | Good | <Good /> | [Parameter, PinguApps.BlazorContentContracts.AllowedChildren(AllowText = true)] public new RenderFragment ChildContent { get; set; } | 1 | BCC003 |
            | Families.Family<> | <Derived /> | | 0 | BCC001 |
            | Families.Family<string> | <Derived /> | | 1 | BCC001 |
