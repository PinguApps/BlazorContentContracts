using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PinguApps.BlazorContentContracts.Tests.Support;

internal static class ReadmeExamples
{
    internal static void Write(string directory, string readme, string example)
    {
        var project = Path.Combine(directory, "Consumer.csproj");
        File.WriteAllText(project, File.ReadAllText(project).Replace("<PropertyGroup>", "<PropertyGroup><Nullable>enable</Nullable>", StringComparison.Ordinal));

        string[] Blocks(string heading, string language = "razor")
        {
            var start = readme.IndexOf(heading, StringComparison.Ordinal);
            var end = readme.IndexOf("\n## ", start + heading.Length, StringComparison.Ordinal);
            var section = readme.Substring(start, (end < 0 ? readme.Length : end) - start);
            return Regex.Matches(section, "```" + language + "\\r?\\n([\\s\\S]*?)```")
                .Select(match => match.Groups[1].Value).ToArray();
        }

        var menu = Blocks("## Accept only specific components");
        var terminal = Blocks("## Accepted components stop inspection");
        var html = Blocks("## Allow selected HTML and text");
        var grouped = Blocks("## Accepted HTML still checks its contents");
        var wrappers = Blocks("## Extract wrappers without losing checking");
        var composition = Blocks("## Generics, named slots and composition");
        var unknown = Blocks("## Understand unknown output");
        File.Delete(Path.Combine(directory, "Slot.cs"));
        File.Delete(Path.Combine(directory, "Good.cs"));
        File.Delete(Path.Combine(directory, "Bad.cs"));
        File.WriteAllText(Path.Combine(directory, "_Imports.razor"),
            "@using Microsoft.AspNetCore.Components\n@using PinguApps.BlazorContentContracts\n@using Consumer");
        File.WriteAllText(Path.Combine(directory, "Menu.razor"), menu[0]);
        File.WriteAllText(Path.Combine(directory, "MenuItem.razor"), menu[1]);
        File.WriteAllText(Path.Combine(directory, "Bad.razor"), menu[4]);
        File.WriteAllText(Path.Combine(directory, "InlineContent.razor"), html[0]);
        File.WriteAllText(Path.Combine(directory, "GroupedMenu.razor"), grouped[0]);
        File.WriteAllText(Path.Combine(directory, "MenuGroup.razor"), wrappers[0]);
        File.WriteAllText(Path.Combine(directory, "MenuItems.razor"), wrappers[3]);
        File.WriteAllText(Path.Combine(directory, "Row.razor"), composition[0]);
        File.WriteAllText(Path.Combine(directory, "Rows.razor"), composition[1]);
        File.WriteAllText(Path.Combine(directory, "PageSection.razor"), composition[4]);
        File.WriteAllText(Path.Combine(directory, "RowTemplate.razor"), composition[7]);
        File.WriteAllText(Path.Combine(directory, "Wrapper.razor"), composition[9]);
        File.WriteAllText(Path.Combine(directory, "MenuFragments.cs"),
            Blocks("## Generics, named slots and composition", "csharp")[1].Replace("YourApp.Components", "Consumer", StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(directory, "PublishedMenuItems.razor"), composition[11]);
        var usage = example switch
        {
            "menu items" => menu[2],
            "HTML and text" => menu[3],
            "excluded component" => menu[5],
            "accepted component contents" => terminal[2],
            "restricted item contents" => "<Menu><MenuItem><Bad /></MenuItem></Menu>",
            "inline HTML" => html[1],
            "nested forbidden HTML" => html[2],
            "grouped items" => grouped[1],
            "grouped violations" => grouped[2],
            "transparent wrapper" => wrappers[1],
            "invalid wrapper" => wrappers[1],
            "forwarded fragments" => wrappers[4],
            "branches" => Blocks("## Check every possible branch")[0],
            "open generics" => composition[2],
            "closed generics" => composition[3],
            "named slots" => composition[5],
            "wrong named slot" => composition[6],
            "typed template" => composition[8],
            "forwarding promise" => "<Wrapper><MenuItem /></Wrapper>",
            "widened forwarding promise" => "<Wrapper><MenuItem /></Wrapper>",
            "factory promise" => composition[10],
            "contradicted factory promise" => composition[10],
            "compiled forwarding declaration" => composition[12],
            "unknown fragment" => unknown[0],
            "mixed findings" => unknown[1] + unknown[0].Substring(unknown[0].IndexOf("@code", StringComparison.Ordinal)),
            "raw markup" => unknown[2],
            _ => throw new ArgumentException(example)
        };

        if (example is "accepted component contents" or "restricted item contents")
        {
            var item = terminal[1];

            if (example == "restricted item contents")
            {
                item = item.Replace("[Parameter]", "[Parameter, AllowedChildren(HtmlElements = new[] { \"strong\" }, AllowText = true)]", StringComparison.Ordinal);
            }

            File.WriteAllText(Path.Combine(directory, "MenuItem.razor"), item);
        }

        if (example == "invalid wrapper")
        {
            File.WriteAllText(Path.Combine(directory, "MenuGroup.razor"), wrappers[2]);
        }

        if (example == "closed generics")
        {
            File.WriteAllText(Path.Combine(directory, "Rows.razor"), composition[1].Replace("Row<>", "Row<int>", StringComparison.Ordinal));
        }

        if (example == "widened forwarding promise")
        {
            File.WriteAllText(Path.Combine(directory, "Wrapper.razor"), composition[9]
                .Replace("AllowedChildren(typeof(MenuItem))", "AllowedChildren(typeof(MenuItem), typeof(Bad))", StringComparison.Ordinal));
        }

        if (example == "contradicted factory promise")
        {
            var factory = Path.Combine(directory, "MenuFragments.cs");
            File.WriteAllText(factory, File.ReadAllText(factory).Replace("builder.OpenComponent<MenuItem>(0);", "builder.OpenElement(0, \"button\");", StringComparison.Ordinal)
                .Replace("builder.CloseComponent();", "builder.CloseElement();", StringComparison.Ordinal));
        }

        File.WriteAllText(Path.Combine(directory, "Usage.razor"), usage);
    }
}
