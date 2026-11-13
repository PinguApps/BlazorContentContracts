Feature: Stable release versions
  Stable publication accepts only an unambiguous matching version.

  Scenario Outline: Reject unsafe release identities
    When the release version is resolved for tag "<tag>" and name "<name>"
    Then release version resolution fails

    Examples:
      | tag           | name         |
      | v1.2.3        | v1.2.4       |
      | v01.2.3       | v01.2.3      |
      | v1.2.3-beta.1 | v1.2.3-beta.1 |
      | v0.9.0        | v0.9.0       |
      | 1.2.3         | 1.2.3        |

  Scenario: Resolve a matching stable release
    When the release version is resolved for tag "v2.3.4" and name "v2.3.4"
    Then the resolved release version is "2.3.4"
