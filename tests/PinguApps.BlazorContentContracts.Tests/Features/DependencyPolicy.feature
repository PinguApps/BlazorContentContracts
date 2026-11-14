Feature: Dependency compatibility approval
  Compiler and SDK upgrades outside the qualified baseline need approval.

  Scenario Outline: Protect qualified toolchain versions
    When dependency "<package>" is proposed at "<version>"
    Then the local dependency policy requires approval "<approval>"

    Examples:
      | package                                   | version  | approval |
      | Microsoft.CodeAnalysis.CSharp             | 5.9.1    | false    |
      | Microsoft.CodeAnalysis.CSharp             | 5.12.0   | true     |
      | Microsoft.CodeAnalysis.Analyzers          | 5.9.1    | false    |
      | Microsoft.CodeAnalysis.Analyzers          | 5.12.0   | true     |
      | Microsoft.CodeAnalysis.CSharp.Workspaces  | 5.9.1    | false    |
      | Microsoft.CodeAnalysis.CSharp.Workspaces  | 5.12.0   | true     |
      | dotnet-sdk                                | 11.0.101 | false    |
      | dotnet-sdk                                | 11.0.200 | true     |
      | dotnet-sdk                                | 12.0.100 | true     |
