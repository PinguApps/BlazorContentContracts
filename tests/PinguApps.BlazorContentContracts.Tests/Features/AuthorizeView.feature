Feature: AuthorizeView branches preserve content checking

    Scenario Outline: Every possible authorization branch obeys the receiving contract
        Given a composed Razor consumer using '<usage>'
        When the real Razor SDK builds the consumer
        Then unresolved output has 0 warnings and <invalid> definite violations

        Examples:
            | usage | invalid |
            | <Slot><Microsoft.AspNetCore.Components.Authorization.AuthorizeView><Authorized><Good /></Authorized><NotAuthorized><Good /></NotAuthorized><Authorizing><Good /></Authorizing></Microsoft.AspNetCore.Components.Authorization.AuthorizeView></Slot> | 0 |
            | <Slot><Microsoft.AspNetCore.Components.Authorization.AuthorizeView><ChildContent><Good /></ChildContent><NotAuthorized><Good /></NotAuthorized><Authorizing><Good /></Authorizing></Microsoft.AspNetCore.Components.Authorization.AuthorizeView></Slot> | 0 |
            | <Slot><Microsoft.AspNetCore.Components.Authorization.AuthorizeView><Good /></Microsoft.AspNetCore.Components.Authorization.AuthorizeView></Slot> | 0 |
            | <Slot><Microsoft.AspNetCore.Components.Authorization.AuthorizeView /></Slot> | 0 |
            | <Slot><Microsoft.AspNetCore.Components.Authorization.AuthorizeView><Authorized><Bad /></Authorized></Microsoft.AspNetCore.Components.Authorization.AuthorizeView></Slot> | 1 |
            | <Slot><Microsoft.AspNetCore.Components.Authorization.AuthorizeView><ChildContent><Bad /></ChildContent></Microsoft.AspNetCore.Components.Authorization.AuthorizeView></Slot> | 1 |
            | <Slot><Microsoft.AspNetCore.Components.Authorization.AuthorizeView><Authorized><Good /></Authorized><NotAuthorized><div /></NotAuthorized></Microsoft.AspNetCore.Components.Authorization.AuthorizeView></Slot> | 1 |
            | <Slot><Microsoft.AspNetCore.Components.Authorization.AuthorizeView><Authorized><Good /></Authorized><Authorizing>Loading</Authorizing></Microsoft.AspNetCore.Components.Authorization.AuthorizeView></Slot> | 1 |
            | <Slot><Microsoft.AspNetCore.Components.Authorization.AuthorizeView><Authorized><Bad /></Authorized><NotAuthorized><Bad /></NotAuthorized><Authorizing><Bad /></Authorizing></Microsoft.AspNetCore.Components.Authorization.AuthorizeView></Slot> | 3 |