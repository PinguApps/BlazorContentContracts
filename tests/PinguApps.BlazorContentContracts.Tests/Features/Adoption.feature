Feature: Runnable adoption examples
  The published sample sources must work with the real analyzer and keep checking descendants.

  Scenario: Adoption examples compile
    Given the adoption samples with "no changes"
    When the real Razor SDK builds the consumer
    Then the markup build reports "BCC005" exactly 0 times

  Scenario: Allowed HTML still checks nested content
    Given the adoption samples with "invalid descendant"
    When the real Razor SDK builds the consumer
    Then the markup build reports "BCC005" exactly 1 times

