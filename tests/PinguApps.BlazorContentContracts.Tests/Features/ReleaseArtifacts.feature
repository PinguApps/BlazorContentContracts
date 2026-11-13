Feature: Release artifact integrity
  Only the exact validated package set may reach the publisher.

  Scenario Outline: Verify the publishing boundary
    Given a release artifact manifest with "<condition>"
    When the release artifact manifest is verified
    Then artifact verification "<result>"

    Examples:
      | condition       | result   |
      | original bytes  | succeeds |
      | changed bytes   | fails    |
      | wrong source    | fails    |
      | extra package   | fails    |
