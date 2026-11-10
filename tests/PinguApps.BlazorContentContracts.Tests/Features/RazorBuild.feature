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

    Scenario Outline: HTML containers and text permissions remain independent
        Given a markup contract allowing "<html>" with text "<text>" and usage '<usage>'
        When the real Razor SDK builds the consumer
        Then the markup build reports "<rule>" exactly <count> times

        Examples:
            | html | text | usage | rule | count |
            | span | false | <span /> | BCC001 | 0 |
            | SPAN | false | <span><span /></span> | BCC001 | 0 |
            | span | false | <span><Bad /></span> | BCC001 | 1 |
            | span | false | <div /> | BCC005 | 1 |
            | span | false | <span>visible</span> | BCC005 | 1 |
            | span | true | <span>visible</span> | BCC001 | 0 |
            | span | false | visible | BCC005 | 1 |
            | span | true | visible | BCC001 | 0 |
            | span | false |   @* ignored *@  | BCC001 | 0 |
            | my-element | false | <my-element /> | BCC001 | 0 |
            | svg | false | <svg><path /></svg> | BCC005 | 1 |
            | svg | false | <svg /> | BCC001 | 0 |
            | lineargradient | false | <linearGradient /> | BCC001 | 0 |
            | span | false | @("known") | BCC005 | 1 |
            | span | true | @("known") | BCC001 | 0 |
            | span | true | @((Microsoft.AspNetCore.Components.MarkupString)"<span />") | BCC004 | 1 |
            | span | false | @(System.DateTime.Now.ToString()) | BCC005 | 1 |
            | span | true | @(System.DateTime.Now.ToString()) | BCC001 | 0 |
            | span | true | <div>visible</div> | BCC005 | 1 |
            | span | false | <!-- ignored --> | BCC001 | 0 |
            | span | false | &#32; | BCC001 | 0 |
            | span | false | &amp; | BCC005 | 1 |
            | span | true | <span title="a > b" /> | BCC001 | 0 |
            | span | true | @((Microsoft.AspNetCore.Components.MarkupString)System.DateTime.Now.ToString()) | BCC004 | 1 |
