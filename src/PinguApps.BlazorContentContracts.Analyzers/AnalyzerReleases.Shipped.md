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
BCC007 | ContentContracts | Warning | Malformed Razor suppression; specify next usage, supported diagnostic ID and reason
BCC008 | ContentContracts | Warning | Unused Razor suppression; remove or move the comment
BCC009 | ContentContracts | Error | Declared output or forwarded fragment permissions exceed the receiving contract
BCC010 | ContentContracts | Error | Known implementation output or fragment storage write contradicts its declared content contract
BCC011 | ContentContracts | Warning | Declared implementation output cannot be verified; acknowledge locally at the implementation boundary
