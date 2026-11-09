Feature: Public contract declarations
  Authors describe content without running application code.

  Scenario Outline: Validate contract declarations
    Given a contract declaration '<declaration>'
    When declaration analysis runs
    Then the declaration diagnostic is '<diagnostic>'

    Examples:
      | declaration | diagnostic |
      | components | none |
      | html | none |
      | typed | none |
      | nullable | none |
      | inherited parameter | none |
      | output | none |
      | method | none |
      | typed method | none |
      | forwarding | none |
      | inherited forwarding | none |
      | identical override | none |
      | invalid type | BCC002 |
      | invalid generic | BCC002 |
      | invalid element | BCC002 |
      | null types | BCC002 |
      | null elements | BCC002 |
      | wrong property | BCC002 |
      | missing parameter | BCC002 |
      | static parameter | BCC002 |
      | wrong component | BCC002 |
      | wrong method | BCC002 |
      | missing forward | BCC002 |
      | duplicate forward | BCC002 |
      | hidden forward | BCC002 |
      | inherited hidden forward | BCC002 |
      | conflicting override | BCC003 |
      | conflicting output | BCC003 |
      | conflicting forwarding | BCC003 |
      | conflicting method | BCC003 |

  Scenario: Public schema survives compilation
    Given a contract declaration 'forwarding'
    When the declaration is emitted and read as metadata
    Then schema v1 identities and defaults are preserved
