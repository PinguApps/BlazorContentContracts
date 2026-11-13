Feature: Analysis invalidation

    Scenario Outline: SDK reanalysis observes edits and recovery
        Then repeated SDK builds invalidate a changed '<source>'

        Examples:
            | source             |
            | wrapper            |
            | contract           |
            | assignment         |
            | referenced summary |

    Scenario Outline: Measure bounded real SDK builds
        Then measure bounded SDK builds for '<shape>'

        Examples:
            | shape       |
            | depth limit |
            | work limit  |

    Scenario: Cancellation interrupts an active traversal
        Then cancellation interrupts content traversal before more work is accepted
