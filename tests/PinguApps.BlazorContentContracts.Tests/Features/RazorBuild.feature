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
