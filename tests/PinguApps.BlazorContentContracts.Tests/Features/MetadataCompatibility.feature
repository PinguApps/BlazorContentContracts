Feature: Compiled annotation identity and versions are checked

    Scenario Outline: Explain annotation incompatibility
        Then an annotation assembly with '<conflict>' reports an incompatible metadata warning

        Examples:
            | conflict |
            | unsupported version |
            | conflicting identity |
