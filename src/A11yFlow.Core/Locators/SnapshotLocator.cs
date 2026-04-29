using A11yFlow.Core.Models;
using A11yFlow.Core.Refs;
using A11yFlow.Core.Snapshots;

namespace A11yFlow.Core.Locators;

public sealed class SnapshotLocator
{
    public LocateResult Locate(SnapshotResult snapshot, SelectorParseResult parseResult)
    {
        if (!parseResult.IsSuccess)
        {
            return new LocateResult(
                LocateStatus.InvalidSelector,
                null,
                Array.Empty<ElementCandidate>(),
                parseResult.Error!.Message,
                new Dictionary<string, string?>
                {
                    ["error_position"] = parseResult.Error.Position.ToString(),
                });
        }

        var attemptNotes = new List<string>();

        for (var i = 0; i < parseResult.Alternatives.Count; i++)
        {
            var alternative = parseResult.Alternatives[i];
            var candidates = LocateSingle(snapshot.Root, alternative)
                .OrderByDescending(candidate => candidate.Confidence)
                .ThenBy(candidate => candidate.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (candidates.Count == 0)
            {
                attemptNotes.Add($"alt[{i}] no_match ({DescribeStrategy(alternative)})");
                continue;
            }

            if (candidates.Count == 1)
            {
                attemptNotes.Add($"alt[{i}] matched ({DescribeStrategy(alternative)})");
                return new LocateResult(
                    LocateStatus.Found,
                    candidates[0],
                    candidates,
                    $"Matched using alternative {i + 1}: {DescribeStrategy(alternative)}.",
                    new Dictionary<string, string?>
                    {
                        ["strategy_used"] = candidates[0].Strategy,
                        ["selector_alternative"] = i.ToString(),
                        ["candidate_count"] = candidates.Count.ToString(),
                        ["attempts"] = string.Join("; ", attemptNotes),
                    });
            }

            attemptNotes.Add($"alt[{i}] ambiguous ({DescribeStrategy(alternative)})");
            return new LocateResult(
                LocateStatus.Ambiguous,
                candidates[0],
                candidates,
                $"Selector matched {candidates.Count} candidates using alternative {i + 1}.",
                new Dictionary<string, string?>
                {
                    ["strategy_used"] = candidates[0].Strategy,
                    ["selector_alternative"] = i.ToString(),
                    ["candidate_count"] = candidates.Count.ToString(),
                    ["attempts"] = string.Join("; ", attemptNotes),
                });
        }

        return new LocateResult(
            LocateStatus.NotFound,
            null,
            Array.Empty<ElementCandidate>(),
            "No candidate matched the selector.",
            new Dictionary<string, string?>
            {
                ["attempts"] = string.Join("; ", attemptNotes),
            });
    }

    public RefDescription? Describe(ElementNode root, WindowRef windowRef, string reference)
    {
        return DescribeCore(root, windowRef, reference, null);
    }

    private static RefDescription? DescribeCore(ElementNode node, WindowRef windowRef, string reference, ElementNode? parent)
    {
        if (string.Equals(node.Ref.Value, reference, StringComparison.Ordinal))
        {
            return new RefDescription(
                node.Ref,
                windowRef,
                node.Role,
                node.Name,
                node.AutomationId,
                node.ClassName,
                node.States,
                node.Actions,
                parent?.Ref,
                node.Children.Select(child => child.Ref).ToList(),
                new Dictionary<string, string?>
                {
                    ["child_count"] = node.Children.Count.ToString(),
                    ["has_parent"] = (parent is not null).ToString().ToLowerInvariant(),
                });
        }

        foreach (var child in node.Children)
        {
            var match = DescribeCore(child, windowRef, reference, node);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static List<ElementCandidate> LocateSingle(ElementNode root, ParsedSelector selector)
    {
        var strategy = DescribeStrategy(selector);
        var matches = EnumerateSelfAndDescendants(root)
            .SelectMany(node => MatchSegment(node, selector, 0));
        return matches
            .DistinctBy(node => node.Ref.Value)
            .Select(node => ToCandidate(node, selector, strategy))
            .ToList();
    }

    private static IEnumerable<ElementNode> EnumerateSelfAndDescendants(ElementNode node)
    {
        yield return node;

        foreach (var descendant in EnumerateDescendants(node))
        {
            yield return descendant;
        }
    }

    private static IEnumerable<ElementNode> MatchSegment(ElementNode current, ParsedSelector selector, int index)
    {
        var segment = selector.Segments[index];

        if (!Matches(current, segment))
        {
            yield break;
        }

        if (index == selector.Segments.Count - 1)
        {
            yield return current;
            yield break;
        }

        var relation = selector.Relations[index];
        foreach (var next in EnumerateNext(current, relation))
        {
            foreach (var match in MatchSegment(next, selector, index + 1))
            {
                yield return match;
            }
        }
    }

    private static IEnumerable<ElementNode> EnumerateNext(ElementNode node, SelectorRelation relation)
    {
        if (relation == SelectorRelation.Child)
        {
            foreach (var child in node.Children)
            {
                yield return child;
            }

            yield break;
        }

        foreach (var descendant in EnumerateDescendants(node))
        {
            yield return descendant;
        }
    }

    private static IEnumerable<ElementNode> EnumerateDescendants(ElementNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;

            foreach (var nested in EnumerateDescendants(child))
            {
                yield return nested;
            }
        }
    }

    private static bool Matches(ElementNode node, SelectorSegment segment)
    {
        if (segment.Role is not null && !string.Equals(node.Role, segment.Role, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var predicate in segment.Predicates)
        {
            if (!MatchesPredicate(node, predicate))
            {
                return false;
            }
        }

        if (segment.Text is not null && !MatchesText(node, segment.Text))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesPredicate(ElementNode node, SelectorPredicate predicate)
    {
        var candidate = predicate.Field.ToLowerInvariant() switch
        {
            "name" => node.Name,
            "automation_id" => node.AutomationId,
            "class_name" => node.ClassName,
            "role" => node.Role,
            "enabled" => node.States.Contains("enabled", StringComparer.OrdinalIgnoreCase).ToString().ToLowerInvariant(),
            "visible" => node.States.Contains("visible", StringComparer.OrdinalIgnoreCase).ToString().ToLowerInvariant(),
            "focused" => node.States.Contains("focused", StringComparer.OrdinalIgnoreCase).ToString().ToLowerInvariant(),
            _ => null,
        };

        if (candidate is null)
        {
            return false;
        }

        return predicate.Operator switch
        {
            SelectorOperator.Equals => string.Equals(candidate, predicate.Value, StringComparison.OrdinalIgnoreCase),
            SelectorOperator.Contains => candidate.Contains(predicate.Value, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static bool MatchesText(ElementNode node, TextSelector text)
    {
        var haystack = node.Name ?? string.Empty;
        return text.Kind switch
        {
            TextMatchKind.Exact => string.Equals(haystack, text.Value, StringComparison.OrdinalIgnoreCase),
            TextMatchKind.Contains => haystack.Contains(text.Value, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static ElementCandidate ToCandidate(ElementNode node, ParsedSelector selector, string strategy)
    {
        var score = 0.55d;

        if (selector.Segments.Count > 1)
        {
            score += 0.15d;
        }

        if (selector.Relations.Contains(SelectorRelation.Descendant))
        {
            score += 0.05d;
        }

        var predicateCount = selector.Segments.Sum(segment => segment.Predicates.Count);
        score += Math.Min(0.2d, predicateCount * 0.05d);

        if (selector.Segments.Any(segment => segment.Text is not null))
        {
            score += 0.05d;
        }

        score = Math.Min(0.99d, score);

        return new ElementCandidate(
            node.Ref,
            node.Role,
            node.Name,
            score,
            strategy,
            $"Matched {node.Role} \"{node.Name ?? string.Empty}\" with {predicateCount} attribute filters.");
    }

    private static string DescribeStrategy(ParsedSelector selector)
    {
        var usesDescendant = selector.Relations.Contains(SelectorRelation.Descendant);
        var usesStructure = selector.Segments.Count > 1;
        var usesText = selector.Segments.Any(segment => segment.Text is not null);

        if (usesText && usesStructure)
        {
            return usesDescendant ? "text_descendant_search" : "text_structural_match";
        }

        if (usesText)
        {
            return "text_match";
        }

        if (usesStructure)
        {
            return usesDescendant ? "descendant_search" : "structural_match";
        }

        return "strict_match";
    }
}
