Feature: Editor diagnostics use Razor's generated source mappings

    Scenario: Editor analysis reports on the generated component identifier
        Given a composed Razor consumer using '<Slot><Bad /></Slot>'
        And editor diagnostic locations are requested
        When the real Razor SDK builds the consumer
        Then the diagnostic targets the generated component identifier for editor mapping
