Feature: Guarded stable draft selection
  Weekly releases include all merged changes and reject unsafe release identities.

  Scenario Outline: Select a draft against real git history without publishing
    When a release draft is selected for "<case>"
    Then draft selection has outcome "<outcome>"

    Examples:
      | case             | outcome |
      | dependency       | publish |
      | docs             | publish |
      | metadata         | publish |
      | no commits       | skip    |
      | no stable        | skip    |
      | no draft         | skip    |
      | multiple drafts  | fail    |
      | prerelease       | fail    |
      | invalid tag      | fail    |
      | same version     | fail    |
      | lower version    | fail    |
      | outside main     | fail    |
      | stale tag        | fail    |
      | baseline outside | fail    |
      | matching tag     | publish |
      | empty notes      | fail    |
      | two pages        | publish |
      | empty pages      | skip    |
      | malformed pages  | fail    |
