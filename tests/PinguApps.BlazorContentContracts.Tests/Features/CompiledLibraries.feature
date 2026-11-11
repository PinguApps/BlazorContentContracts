Feature: Contracts survive independent NuGet packages

    Scenario Outline: Consume fixed and forwarded output without library source
        Given independently packed libraries with a '<wrapper>' wrapper
        When the packed composition consumer builds
        Then the packed consumer reports '<rule>' exactly <count> times

        Examples:
            | wrapper | rule | count |
            | csharp | BCC006 | 0 |
            | explicit | BCC006 | 0 |
            | project reference | BCC006 | 0 |
            | razor | BCC006 | 0 |
            | generic forwarding | BCC006 | 0 |
            | typed forwarding | BCC006 | 0 |
            | invalid forwarding | BCC005 | 1 |
            | unknown | BCC006 | 1 |
            | default forwarding | BCC006 | 1 |
            | mutated forwarding | BCC006 | 1 |
            | conditional frames | BCC006 | 1 |
            | missing | BCC006 | 1 |
            | incompatible | BCC006 | 1 |
