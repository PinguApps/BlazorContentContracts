Feature: Relocated and nontransparent framework output

    Scenario Outline: Classify local output rather than apparent invisibility
        Given a composed Razor consumer using '<usage>'
        When the real Razor SDK builds the consumer
        Then unresolved output has <unknown> warnings and <invalid> definite violations

        Examples:
            | usage | unknown | invalid |
            | <Slot><Microsoft.AspNetCore.Components.Web.PageTitle>Title</Microsoft.AspNetCore.Components.Web.PageTitle></Slot> | 0 | 0 |
            | <Slot><Microsoft.AspNetCore.Components.Web.HeadContent><div /></Microsoft.AspNetCore.Components.Web.HeadContent></Slot> | 0 | 0 |
            | <Slot><Microsoft.AspNetCore.Components.Sections.SectionContent SectionName="menu"><Bad /></Microsoft.AspNetCore.Components.Sections.SectionContent></Slot> | 0 | 0 |
            | <Slot><Microsoft.AspNetCore.Components.Forms.DataAnnotationsValidator /></Slot> | 0 | 0 |
            | <Slot><Microsoft.AspNetCore.Components.Sections.SectionOutlet SectionName="menu" /><Bad /></Slot> | 1 | 1 |
            | <Slot><Microsoft.AspNetCore.Components.Web.HeadOutlet /></Slot> | 1 | 0 |

            | <Slot><Microsoft.AspNetCore.Components.Forms.AntiforgeryToken /></Slot> | 0 | 1 |
            | <Slot><Microsoft.AspNetCore.Components.Routing.FocusOnNavigate Selector="h1" /></Slot> | 0 | 1 |

    Scenario Outline: Focus output depends on the actual assigned rendering mode
        Then actual FocusOnNavigate output in '<mode>' mode is '<output>'

        Examples:
            | mode | output |
            | static | blazor-focus-on-navigate |
            | interactive | empty |

    Scenario Outline: Token services determine actual emitted output
        Then actual AntiforgeryToken output with token '<token>' is '<output>'

        Examples:
            | token | output |
            | available | input |
            | unavailable | empty |

    Scenario Outline: Allowed possible HTML still checks descendants
        Given a markup contract allowing '<html>' with text 'false' and usage '<usage>'
        When the real Razor SDK builds the consumer
        Then unresolved output has 0 warnings and <invalid> definite violations

        Examples:
            | html | usage | invalid |
            | input | <Microsoft.AspNetCore.Components.Forms.AntiforgeryToken /> | 0 |
            | blazor-focus-on-navigate | <Microsoft.AspNetCore.Components.Routing.FocusOnNavigate Selector="h1" /> | 0 |

    Scenario: EditForm preserves known violations alongside version-specific uncertainty
        Given a composed Razor consumer using '<Slot><Microsoft.AspNetCore.Components.Forms.EditForm Model="@(new object())"><Bad /></Microsoft.AspNetCore.Components.Forms.EditForm></Slot>'
        When the real Razor SDK builds the consumer
        Then EditForm output has 3 definite violations and version-specific uncertainty