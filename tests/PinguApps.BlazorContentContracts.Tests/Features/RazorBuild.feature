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
            | Families.Family<>     | <Family T="int" /><Family T="string" />   |             | 0      | BCC005 |
            | Families.Family<int>  | <Derived />                                |             | 0      | BCC005 |
            | Families.Family<int>  | <Family T="string" />                      |             | 1      | BCC005 |
            | Families.Family<>     | <Other.Family T="int" />                   |             | 1      | BCC005 |
            | Families.Family<>     | <Family Value="123" />                     |             | 0      | BCC005 |
            | Families.Family<int>  | <Family Value="123" />                     |             | 0      | BCC005 |
            | Families.Family<string> | <Family Value="123" />                   |             | 1      | BCC005 |
            | Families.Family<>     | <Families.Family T="int"><Bad /></Families.Family> |      | 0      | BCC005 |
            | Good                  | <Bad />                                    | public override Microsoft.AspNetCore.Components.RenderFragment ChildContent { get; set; } | 1 | BCC005 |
            | Good                  | <Bad />                                    | public new Microsoft.AspNetCore.Components.RenderFragment ChildContent { get; set; } | 1 | BCC005 |
            | Good | <Good /> | [Parameter, PinguApps.BlazorContentContracts.AllowedChildren(AllowText = true)] public override RenderFragment ChildContent { get; set; } | 1 | BCC003 |
            | Good | <Good /> | [Parameter, PinguApps.BlazorContentContracts.AllowedChildren(AllowText = true)] public new RenderFragment ChildContent { get; set; } | 1 | BCC003 |
            | Families.Family<> | <Derived /> | | 0 | BCC005 |
            | Families.Family<string> | <Derived /> | | 1 | BCC005 |

    Scenario Outline: HTML containers and text permissions remain independent
        Given a markup contract allowing "<html>" with text "<text>" and usage '<usage>'
        When the real Razor SDK builds the consumer
        Then the markup build reports "<rule>" exactly <count> times

        Examples:
            | html | text | usage | rule | count |
            | span | false | <span /> | BCC005 | 0 |
            | SPAN | false | <span><span /></span> | BCC005 | 0 |
            | span | false | <span><Bad /></span> | BCC005 | 1 |
            | span | false | <div /> | BCC005 | 1 |
            | span | false | <span>visible</span> | BCC005 | 1 |
            | span | true | <span>visible</span> | BCC005 | 0 |
            | span | false | visible | BCC005 | 1 |
            | span | true | visible | BCC005 | 0 |
            | span | false |   @* ignored *@  | BCC005 | 0 |
            | my-element | false | <my-element /> | BCC005 | 0 |
            | svg | false | <svg><path /></svg> | BCC005 | 1 |
            | svg | false | <svg /> | BCC005 | 0 |
            | lineargradient | false | <linearGradient /> | BCC005 | 0 |
            | span | false | @("known") | BCC005 | 1 |
            | span | true | @("known") | BCC005 | 0 |
            | span | true | @((Microsoft.AspNetCore.Components.MarkupString)"<span />") | BCC004 | 1 |
            | span | false | @(System.DateTime.Now.ToString()) | BCC005 | 1 |
            | span | true | @(System.DateTime.Now.ToString()) | BCC005 | 0 |
            | span | true | <div>visible</div> | BCC005 | 1 |
            | span | false | <!-- ignored --> | BCC005 | 0 |
            | span | false | &#32; | BCC005 | 0 |
            | span | false | &amp; | BCC005 | 1 |
            | span | true | <span title="a > b" /> | BCC005 | 0 |
            | span | true | @((Microsoft.AspNetCore.Components.MarkupString)System.DateTime.Now.ToString()) | BCC004 | 1 |

    Scenario Outline: Composition checks every emitted branch and the correct slot
        Given a composed Razor consumer using '<usage>'
        When the real Razor SDK builds the consumer
        Then the markup build reports "BCC005" exactly <count> times

        Examples:
            | usage | count |
            | <Slot><ChildContent><Good /></ChildContent><Header><Bad /></Header><Template Context="item"><Good /></Template></Slot> | 0 |
            | <Slot><ChildContent><Bad /></ChildContent><Header><Good /></Header><Template Context="item"><Bad /></Template></Slot> | 3 |
            | <Slot>@if (System.DateTime.Now.Ticks > 0) { <Bad /> } else { <Bad /> }</Slot> | 2 |
            | <Slot>@switch (System.DateTime.Now.Day) { case 1: <Bad /> break; default: <Bad /> break; }</Slot> | 2 |
            | <Slot>@foreach (var item in new int[0]) { <Bad /> }</Slot> | 1 |
            | <Slot>@for (var i = 0; i < 0; i++) { <Bad /> }</Slot> | 1 |
            | <Slot><Template Context="item">@if (item == 0) { <Bad /> } else { <Good /> }</Template></Slot> | 1 |
            | <Slot><Good><Bad /></Good></Slot> | 0 |
            | <Slot>@((RenderFragment)(builder => { builder.OpenComponent<Bad>(0); builder.CloseComponent(); }))</Slot> | 1 |
            | <Slot><Good @key="1" />@* ignored *@</Slot> | 0 |
            | <Slot>@{ RenderFragment fragment = @<Bad />; }@fragment</Slot> | 1 |
            | <Slot>@{ RenderFragment<int> fragment = item => @<Bad />; }@fragment(1)</Slot> | 1 |
            | <Slot>@{ RenderFragment fragment = @<Bad />; }<Good /></Slot> | 0 |
            | <Slot>@while (System.DateTime.Now.Ticks < 0) { <Bad /> }</Slot> | 1 |
            | <Slot><Template Context="item">@{ RenderFragment fragment = @<Bad />; }@fragment</Template></Slot> | 1 |
            | <Slot><Good @ref="component" /></Slot>@code { private Good component; } | 0 |
            | <Slot><Good /><Good /></Slot> | 0 |
            | <Slot><Good Activated="@(() => { var ignored = 1; })" /></Slot> | 0 |
            | <Slot ChildContent="@((RenderFragment)(builder => { builder.OpenComponent<Bad>(0); builder.CloseComponent(); }))" /> | 1 |
