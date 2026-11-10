Feature: Local wrappers contribute their actual output

    Scenario Outline: Expand output and every emitted template at the consumer
        Given a local wrapper with output '<output>' and usage '<usage>'
        When the real Razor SDK builds the consumer
        Then unresolved output has <unknown> warnings and <invalid> definite violations

        Examples:
            | output | usage | unknown | invalid |
            | <Good /> | <Wrapper /> | 0 | 0 |
            | | <Wrapper /> | 0 | 0 |
            | <span /> | <Wrapper /> | 0 | 1 |
            | visible | <Wrapper /> | 0 | 1 |
            | @Header@Body@Footer | <Wrapper><Header><Good /></Header><Body><span /></Body><Footer>bad</Footer></Wrapper> | 0 | 2 |
            | @if (true) { @Header } else { @Footer } | <Wrapper><Header><span /></Header><Footer>bad</Footer></Wrapper> | 0 | 2 |
            | @Body | <Wrapper><Header><span /></Header><Body><Good /></Body></Wrapper> | 0 | 0 |
            | <Good>@Body</Good> | <Wrapper><Body><span /></Body></Wrapper> | 0 | 0 |
            | @Factory() | <Wrapper /> | 1 | 0 |

            | <Inner>@Body</Inner> | <Wrapper><Body><span /></Body></Wrapper> | 0 | 1 |
            | @Template(1) | <Wrapper><Template Context="item"><Good /></Template></Wrapper> | 0 | 0 |
            | @Template(1) | <Wrapper><Template Context="item"><span /></Template></Wrapper> | 0 | 1 |
            | <Microsoft.AspNetCore.Components.Web.HeadOutlet /> | <Wrapper /> | 1 | 0 |

            | <GenericWrapper Value="123">@Body</GenericWrapper> | <Wrapper><Body><span /></Body></Wrapper> | 0 | 1 |

    Scenario: Explain the wrapper path at its consumer usage
        Given a local wrapper with output '<span />' and usage '<Wrapper />'
        When the real Razor SDK builds the consumer
        Then the wrapper violation names its path at the consumer
