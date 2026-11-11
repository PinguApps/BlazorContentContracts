Feature: Versioned framework behaviour catalogue

    Scenario Outline: Constructed framework components select their definition
        Given the installed framework component '<component>' from version '<version>'
        And the selected framework component is constructed with int
        When the framework catalogue selects a description
        Then the framework output kind is '<kind>' with slots '<slots>' and defaults '<defaults>'

        Examples:
            | component | version | kind | slots | defaults |
            | Microsoft.AspNetCore.Components.CascadingValue`1 | 10 | forwarding | ChildContent | |
            | Microsoft.AspNetCore.Components.CascadingValue`1 | 11 | forwarding | ChildContent | |
            | Microsoft.AspNetCore.Components.Web.Virtualization.Virtualize`1 | 10 | parameter-dependent | ChildContent,ItemContent,Placeholder,EmptyContent | div |
            | Microsoft.AspNetCore.Components.Web.Virtualization.Virtualize`1 | 11 | parameter-dependent | ChildContent,ItemContent,Placeholder,EmptyContent | div |

    Scenario Outline: Constructed derived behaviour stays unknown
        Given the installed framework component 'Microsoft.AspNetCore.Components.CascadingValue`1' from version '<version>'
        And a constructed component derived from the selected framework component
        When the framework catalogue selects a description
        Then no framework description is granted
        And framework uncertainty explains 'declare its output explicitly'

        Examples:
            | version |
            | 10 |
            | 11 |

    Scenario Outline: Select supported dependency identities independently of the host
        Given a framework identity with version '<version>' and authentic key 'yes'
        When the framework catalogue selects a description
        Then the framework output kind is 'forwarding' with slots 'ChildContent' and defaults ''

        Examples:
            | version |
            | 10.0.0.0 |
            | 11.0.0.0 |

    Scenario Outline: Unsupported dependency versions remain unknown
        Given a framework identity with version '<version>' and authentic key 'yes'
        When the framework catalogue selects a description
        Then no framework description is granted
        And framework uncertainty explains 'use a supported framework version'

        Examples:
            | version |
            | 9.0.0.0 |
            | 12.0.0.0 |
            | 11.1.0.0 |

    Scenario: Assembly and full-name impersonation receives no exemption
        Given a framework identity with version '11.0.0.0' and authentic key 'no'
        When the framework catalogue selects a description
        Then no framework description is granted

    Scenario Outline: Resolve actual framework symbols independently
        Given the installed framework component '<component>' from version '<version>'
        When the framework catalogue selects a description
        Then the framework output kind is '<kind>' with slots '<slots>' and defaults '<defaults>'

        Examples:
            | version | component | kind | slots | defaults |
            | 10 | Microsoft.AspNetCore.Components.CascadingValue`1 | forwarding | ChildContent | |
            | 11 | Microsoft.AspNetCore.Components.CascadingValue`1 | forwarding | ChildContent | |
            | 10 | Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState | forwarding | ChildContent | |
            | 11 | Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState | forwarding | ChildContent | |
            | 10 | Microsoft.AspNetCore.Components.Authorization.AuthorizeView | alternatives | ChildContent,Authorized,NotAuthorized,Authorizing | |
            | 11 | Microsoft.AspNetCore.Components.Authorization.AuthorizeView | alternatives | ChildContent,Authorized,NotAuthorized,Authorizing | |
            | 10 | Microsoft.AspNetCore.Components.Forms.InputRadioGroup`1 | forwarding | ChildContent | |
            | 11 | Microsoft.AspNetCore.Components.Forms.InputRadioGroup`1 | forwarding | ChildContent | |
            | 10 | Microsoft.AspNetCore.Components.Forms.FormMappingScope | forwarding | ChildContent | |
            | 11 | Microsoft.AspNetCore.Components.Forms.FormMappingScope | forwarding | ChildContent | |
            | 10 | Microsoft.AspNetCore.Components.Forms.DataAnnotationsValidator | empty | | |
            | 11 | Microsoft.AspNetCore.Components.Forms.DataAnnotationsValidator | empty | | |
            | 10 | Microsoft.AspNetCore.Components.Forms.EditForm | unresolved | ChildContent | form |
            | 11 | Microsoft.AspNetCore.Components.Forms.EditForm | unresolved | ChildContent | form |
            | 10 | Microsoft.AspNetCore.Components.Web.HeadContent | relocated | ChildContent | |
            | 11 | Microsoft.AspNetCore.Components.Web.HeadContent | relocated | ChildContent | |
            | 10 | Microsoft.AspNetCore.Components.Web.PageTitle | relocated | ChildContent | |
            | 11 | Microsoft.AspNetCore.Components.Web.PageTitle | relocated | ChildContent | |
            | 10 | Microsoft.AspNetCore.Components.Sections.SectionContent | relocated | ChildContent | |
            | 11 | Microsoft.AspNetCore.Components.Sections.SectionContent | relocated | ChildContent | |
            | 10 | Microsoft.AspNetCore.Components.Sections.SectionOutlet | unresolved | | |
            | 11 | Microsoft.AspNetCore.Components.Sections.SectionOutlet | unresolved | | |
            | 10 | Microsoft.AspNetCore.Components.Web.HeadOutlet | unresolved | | |
            | 11 | Microsoft.AspNetCore.Components.Web.HeadOutlet | unresolved | | |
            | 10 | Microsoft.AspNetCore.Components.Web.ErrorBoundary | defaults | ChildContent,ErrorContent | div |
            | 11 | Microsoft.AspNetCore.Components.Web.ErrorBoundary | defaults | ChildContent,ErrorContent | div |
            | 10 | Microsoft.AspNetCore.Components.Web.Virtualization.Virtualize`1 | parameter-dependent | ChildContent,ItemContent,Placeholder,EmptyContent | div |
            | 11 | Microsoft.AspNetCore.Components.Web.Virtualization.Virtualize`1 | parameter-dependent | ChildContent,ItemContent,Placeholder,EmptyContent | div |
            | 10 | Microsoft.AspNetCore.Components.Forms.AntiforgeryToken | unresolved | | |
            | 11 | Microsoft.AspNetCore.Components.Forms.AntiforgeryToken | unresolved | | |
            | 10 | Microsoft.AspNetCore.Components.Routing.FocusOnNavigate | unresolved | | |
            | 11 | Microsoft.AspNetCore.Components.Routing.FocusOnNavigate | unresolved | | |

    Scenario: A same-named user component receives no exemption
        Given a user component named CascadingValue
        When the framework catalogue selects a description
        Then no framework description is granted

    Scenario: Derived behaviour is conservative
        Given a derived framework component
        When the framework catalogue selects a description
        Then no framework description is granted
        And framework uncertainty explains 'declare its output explicitly'
