Feature: Analyzer smoke check
    Scenario: A valid compilation runs without failures
        Given a valid consumer compilation
        When the content contract analyzer runs
        Then no diagnostics are reported
