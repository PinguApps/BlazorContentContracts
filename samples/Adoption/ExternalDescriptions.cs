using System;
using PinguApps.BlazorContentContracts;

// A consumer-owned assertion for a referenced component. This replaces its
// complete output/forwarding description; maintain it when the dependency changes.
[assembly: ExternalContent(new Type[0], typeof(Microsoft.AspNetCore.Components.Sections.SectionOutlet),
    HtmlElements = new[] { "span" }, AllowText = true, Trusted = true)]
