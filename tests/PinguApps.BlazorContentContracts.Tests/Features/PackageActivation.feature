@Sdk
Feature: Automatic package activation
  Consumers get checking through real dependency graphs without analyzer setup.

  Scenario Outline: Clean consumers activate exactly one analyzer
    Given a clean package graph using "<mode>"
    When the activation consumer builds
    Then package checking is "<enabled>"

    Examples:
      | mode             | enabled |
      | central          | enabled |
      | multi            | enabled |
      | downloaded packs | enabled |
      | duplicate        | enabled |
      | version          | enabled |
      | private          | disabled |

  @Net10
  Scenario Outline: Supported hosts activate checking through package graphs
    Given a clean package graph using "<mode>"
    When the activation consumer builds
    Then package checking is "<enabled>"

    Examples:
      | mode             | enabled |
      | direct           | enabled |
      | chain            | enabled |
      | project          | enabled |
      | opt-out          | disabled |
