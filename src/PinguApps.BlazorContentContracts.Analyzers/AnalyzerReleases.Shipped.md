; All new diagnostic entries belong directly under the intended stable version.

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
BCC001 | ContentContracts | Error | A direct child component violates a restricted fragment contract
BCC002 | ContentContracts | Error | Invalid content contract declaration; fix the target, types, tag names or forwarded parameters
BCC003 | ContentContracts | Error | Conflicting inherited declaration; preserve the inherited contract
BCC004 | ContentContracts | Warning | Opaque or unresolved markup output requires inspectable content
BCC005 | ContentContracts | Error | HTML element or visible text violates a restricted fragment contract
BCC006 | ContentContracts | Warning | Unresolved fragment, dynamic component selection or unsupported manual builder output; use inspectable output
