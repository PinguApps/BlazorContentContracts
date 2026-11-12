Feature: Editor diagnostics use Razor's generated source mappings

    Scenario: Editor analysis reports on the generated component identifier
        Given a composed Razor consumer using '<Slot><Bad /></Slot>'
        And editor diagnostic locations are requested
        When the real Razor SDK builds the consumer
        Then the diagnostic targets the generated component identifier for editor mapping
        When a normal build follows editor analysis
        Then the normal diagnostic uses Razor and the editor configuration remains intact

    Scenario: The suppression action preserves sibling and invalid-content checks
        Given the suppression action has acknowledged the first of two unknown fragments beside invalid content
        And unknown output severity is "error"
        When the real Razor SDK builds the consumer
        Then suppression diagnostics are 'BCC005,BCC006'
