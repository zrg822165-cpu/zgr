using System.Text;
using A11yFlow.Core.Abstractions;
using A11yFlow.Core.Models;
using A11yFlow.Core.Refs;

namespace A11yFlow.Core.Snapshots;

public sealed class SnapshotTextFormatter : ISnapshotFormatter
{
    public string Format(WindowSummary window, ElementNode root, string snapshotVersion, ElementRef? focusedElementRef)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Window: {window.Title} [ref={window.Ref}]");
        builder.AppendLine($"Snapshot: {snapshotVersion}");

        if (focusedElementRef is not null)
        {
            var focusedNode = FindNode(root, focusedElementRef);
            if (focusedNode is not null)
            {
                builder.AppendLine($"Focused: {focusedNode.Role} \"{focusedNode.Name ?? string.Empty}\" [ref={focusedNode.Ref}]");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Actionable elements:");

        foreach (var node in EnumerateActionable(root))
        {
            builder.AppendLine($"- {node.Role} \"{node.Name ?? string.Empty}\" [ref={node.Ref}] actions=[{string.Join(", ", node.Actions)}]");
        }

        return builder.ToString().TrimEnd();
    }

    private static IEnumerable<ElementNode> EnumerateActionable(ElementNode root)
    {
        if (root.Actions.Count > 0)
        {
            yield return root;
        }

        foreach (var child in root.Children)
        {
            foreach (var nested in EnumerateActionable(child))
            {
                yield return nested;
            }
        }
    }

    private static ElementNode? FindNode(ElementNode root, ElementRef elementRef)
    {
        if (root.Ref == elementRef)
        {
            return root;
        }

        foreach (var child in root.Children)
        {
            var match = FindNode(child, elementRef);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
