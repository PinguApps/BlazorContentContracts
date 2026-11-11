Feature: ErrorBoundary output preserves content checking

    Scenario Outline: Normal and error templates obey the receiving contract
        Given a composed Razor consumer using '<usage>'
        When the real Razor SDK builds the consumer
        Then unresolved output has 0 warnings and <invalid> definite violations

        Examples:
            | usage | invalid |
            | <Slot><Microsoft.AspNetCore.Components.Web.ErrorBoundary><Good /></Microsoft.AspNetCore.Components.Web.ErrorBoundary></Slot> | 1 |
            | <Slot><Microsoft.AspNetCore.Components.Web.ErrorBoundary /></Slot> | 1 |
            | <Slot><Microsoft.AspNetCore.Components.Web.ErrorBoundary><ChildContent><Good /></ChildContent><ErrorContent><Good /></ErrorContent></Microsoft.AspNetCore.Components.Web.ErrorBoundary></Slot> | 0 |
            | <Slot><Microsoft.AspNetCore.Components.Web.ErrorBoundary><ChildContent><Good /></ChildContent><ErrorContent></ErrorContent></Microsoft.AspNetCore.Components.Web.ErrorBoundary></Slot> | 0 |
            | <Slot><Microsoft.AspNetCore.Components.Web.ErrorBoundary><ChildContent><Bad /></ChildContent><ErrorContent><Bad /></ErrorContent></Microsoft.AspNetCore.Components.Web.ErrorBoundary></Slot> | 2 |
            | <Slot><Microsoft.AspNetCore.Components.Web.ErrorBoundary><ErrorContent Context="error">@{ System.Exception typed = error; }<Good /></ErrorContent></Microsoft.AspNetCore.Components.Web.ErrorBoundary></Slot> | 0 |
            | <Slot><Microsoft.AspNetCore.Components.Web.ErrorBoundary><ErrorContent><div /></ErrorContent></Microsoft.AspNetCore.Components.Web.ErrorBoundary></Slot> | 1 |

    Scenario Outline: Allowed HTML remains recursive and explicit components remain terminal
        Given a forwarding parameter allowing '<permissions>' with usage '<usage>'
        When the real Razor SDK builds the consumer
        Then unresolved output has 0 warnings and <invalid> definite violations

        Examples:
            | permissions | usage | invalid |
            | typeof(Good) => typeof(Good), HtmlElements = new[] { "div" } | <Slot><Microsoft.AspNetCore.Components.Web.ErrorBoundary><Good /></Microsoft.AspNetCore.Components.Web.ErrorBoundary></Slot> | 0 |
            | typeof(Good) => typeof(Good), HtmlElements = new[] { "div" } | <Slot><Microsoft.AspNetCore.Components.Web.ErrorBoundary><ErrorContent><div><Bad /></div></ErrorContent></Microsoft.AspNetCore.Components.Web.ErrorBoundary></Slot> | 1 |
            | typeof(Microsoft.AspNetCore.Components.Web.ErrorBoundary) => typeof(Microsoft.AspNetCore.Components.Web.ErrorBoundary) | <Slot><Microsoft.AspNetCore.Components.Web.ErrorBoundary><ChildContent><Bad /></ChildContent><ErrorContent><div /></ErrorContent></Microsoft.AspNetCore.Components.Web.ErrorBoundary></Slot> | 0 |
