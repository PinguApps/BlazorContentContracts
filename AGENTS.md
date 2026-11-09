# Repository conventions

## C# readability

Apply these conventions to all authored `.cs` files, including scripts and tests.
Use `scripts/verify.cs` as the formatting example.

- Use braces for every control-flow body, including a single statement.
- Insert a blank line before a statement or declaration that introduces a brace level (such as `if`, `try`, `foreach`, or a method), except immediately inside an opening brace. Keep the opening brace on its own line.
- Follow a closing brace with a blank line before the next statement or declaration. Consecutive closing braces may remain adjacent.
- Separate logical phases with blank lines: setup, execution and assertions; group closely related calls together.
- Keep short calls readable on one line; wrap long expressions where useful.

## Tests and packages

- Express every test as a Gherkin scenario using Reqnroll with xUnit.
- Put `.feature` files in `Features`, bindings in `Steps`, and all other test infrastructure (including hooks and helpers) in `Support`.
- Manage NuGet versions in the root `Directory.Packages.props`.
- Keep `AnalyzerReleases.Unshipped.md` as a standard blank release-tracking template until unshipped diagnostics are explicitly requested.

## GitHub workflows

Insert a blank line between workflow steps.
