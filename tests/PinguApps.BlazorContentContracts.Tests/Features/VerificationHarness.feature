Feature: Direct compiler tests fail closed on unsupported settings

    Scenario Outline: Project settings cannot be silently ignored
        Given a Razor consumer with "valid" content
        Then the direct compiler requires SDK coverage for property "<property>" with value "<value>"

        Examples:
            | property             | value   |
            | TreatWarningsAsErrors | true   |
            | NoWarn               | BCC005  |
            | DefineConstants      | DEBUG   |
            | TargetFramework      | net10.0 |
