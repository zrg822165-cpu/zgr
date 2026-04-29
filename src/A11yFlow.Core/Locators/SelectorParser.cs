using System.Text;

namespace A11yFlow.Core.Locators;

public sealed class SelectorParser
{
    public SelectorParseResult Parse(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return new SelectorParseResult(Array.Empty<ParsedSelector>(), new SelectorParseError("Selector must not be empty.", 0));
        }

        var parts = SplitAlternatives(selector);
        var alternatives = new List<ParsedSelector>(parts.Count);

        foreach (var part in parts)
        {
            var parsed = ParseSingle(part.Text.Trim(), part.Offset);
            if (parsed.Error is not null)
            {
                return new SelectorParseResult(Array.Empty<ParsedSelector>(), parsed.Error);
            }

            alternatives.Add(parsed.Selector!);
        }

        return new SelectorParseResult(alternatives, null);
    }

    private static List<(string Text, int Offset)> SplitAlternatives(string source)
    {
        var parts = new List<(string Text, int Offset)>();
        var start = 0;
        var depth = 0;
        var inString = false;

        for (var i = 0; i < source.Length; i++)
        {
            var current = source[i];
            if (current == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (current == '[' || current == '(')
            {
                depth++;
                continue;
            }

            if (current == ']' || current == ')')
            {
                depth--;
                continue;
            }

            if (depth == 0 && current == '|' && i + 1 < source.Length && source[i + 1] == '|')
            {
                parts.Add((source[start..i], start));
                start = i + 2;
                i++;
            }
        }

        parts.Add((source[start..], start));
        return parts;
    }

    private static (ParsedSelector? Selector, SelectorParseError? Error) ParseSingle(string source, int offset)
    {
        var cursor = 0;
        SkipWhitespace(source, ref cursor);

        var scope = ParseScope(source, ref cursor, offset, out var scopeError);
        if (scopeError is not null)
        {
            return (null, scopeError);
        }

        SkipWhitespace(source, ref cursor);
        var segments = new List<SelectorSegment>();
        var relations = new List<SelectorRelation>();

        while (cursor < source.Length)
        {
            var segment = ParseSegment(source, ref cursor, offset, out var segmentError);
            if (segmentError is not null)
            {
                return (null, segmentError);
            }

            segments.Add(segment!);
            SkipWhitespace(source, ref cursor);

            if (cursor >= source.Length)
            {
                break;
            }

            if (source[cursor] != '>')
            {
                return (null, new SelectorParseError($"Unexpected token '{source[cursor]}'.", offset + cursor));
            }

            if (cursor + 1 < source.Length && source[cursor + 1] == '>')
            {
                relations.Add(SelectorRelation.Descendant);
                cursor += 2;
            }
            else
            {
                relations.Add(SelectorRelation.Child);
                cursor += 1;
            }

            SkipWhitespace(source, ref cursor);
        }

        if (segments.Count == 0)
        {
            return (null, new SelectorParseError("Selector must include at least one segment.", offset));
        }

        return (new ParsedSelector(scope!, segments, relations, source), null);
    }

    private static SelectorScope? ParseScope(string source, ref int cursor, int offset, out SelectorParseError? error)
    {
        error = null;
        const string activeScope = "scope:active_window";
        const string windowScopePrefix = "scope:window";

        if (source[cursor..].StartsWith(activeScope, StringComparison.Ordinal))
        {
            cursor += activeScope.Length;
            return new SelectorScope(SelectorScopeKind.ActiveWindow);
        }

        if (source[cursor..].StartsWith(windowScopePrefix, StringComparison.Ordinal))
        {
            cursor += windowScopePrefix.Length;
            SkipWhitespace(source, ref cursor);
            if (cursor >= source.Length || source[cursor] != '(')
            {
                error = new SelectorParseError("Expected window scope arguments.", offset + cursor);
                return null;
            }

            cursor++;
            SkipWhitespace(source, ref cursor);
            if (!source[cursor..].StartsWith("name", StringComparison.Ordinal))
            {
                error = new SelectorParseError("Window scope currently requires name=\"...\".", offset + cursor);
                return null;
            }

            cursor += 4;
            SkipWhitespace(source, ref cursor);
            if (cursor >= source.Length || source[cursor] != '=')
            {
                error = new SelectorParseError("Expected '=' after window scope name.", offset + cursor);
                return null;
            }

            cursor++;
            SkipWhitespace(source, ref cursor);
            var value = ParseQuotedString(source, ref cursor, offset, out error);
            if (error is not null)
            {
                return null;
            }

            SkipWhitespace(source, ref cursor);
            if (cursor >= source.Length || source[cursor] != ')')
            {
                error = new SelectorParseError("Expected ')' to close window scope.", offset + cursor);
                return null;
            }

            cursor++;
            return new SelectorScope(SelectorScopeKind.WindowByName, value);
        }

        return new SelectorScope(SelectorScopeKind.ActiveWindow);
    }

    private static SelectorSegment? ParseSegment(string source, ref int cursor, int offset, out SelectorParseError? error)
    {
        error = null;
        SkipWhitespace(source, ref cursor);

        string? role = null;
        TextSelector? text = null;
        var predicates = new List<SelectorPredicate>();

        if (source[cursor..].StartsWith("text(", StringComparison.Ordinal))
        {
            text = ParseTextSelector(source, ref cursor, offset, out error);
            if (error is not null)
            {
                return null;
            }

            return new SelectorSegment(null, predicates, text);
        }

        var roleStart = cursor;
        while (cursor < source.Length && (char.IsLetterOrDigit(source[cursor]) || source[cursor] == '_'))
        {
            cursor++;
        }

        if (cursor == roleStart)
        {
            error = new SelectorParseError("Expected selector role or text(...).", offset + cursor);
            return null;
        }

        role = source[roleStart..cursor];

        if (cursor < source.Length && source[cursor] == ':')
        {
            cursor++;
            if (!source[cursor..].StartsWith("text(", StringComparison.Ordinal))
            {
                error = new SelectorParseError("Only role:text(...) is supported in phase 2.", offset + cursor);
                return null;
            }

            text = ParseTextSelector(source, ref cursor, offset, out error);
            if (error is not null)
            {
                return null;
            }
        }

        SkipWhitespace(source, ref cursor);
        if (cursor < source.Length && source[cursor] == '[')
        {
            cursor++;
            while (true)
            {
                SkipWhitespace(source, ref cursor);
                var fieldStart = cursor;
                while (cursor < source.Length && (char.IsLetterOrDigit(source[cursor]) || source[cursor] == '_'))
                {
                    cursor++;
                }

                if (cursor == fieldStart)
                {
                    error = new SelectorParseError("Expected attribute name.", offset + cursor);
                    return null;
                }

                var field = source[fieldStart..cursor];
                SkipWhitespace(source, ref cursor);

                SelectorOperator op;
                if (cursor + 1 < source.Length && source[cursor] == '~' && source[cursor + 1] == '=')
                {
                    op = SelectorOperator.Contains;
                    cursor += 2;
                }
                else if (cursor < source.Length && source[cursor] == '=')
                {
                    op = SelectorOperator.Equals;
                    cursor += 1;
                }
                else
                {
                    error = new SelectorParseError("Expected '=' or '~=' in attribute filter.", offset + cursor);
                    return null;
                }

                SkipWhitespace(source, ref cursor);
                var value = ParseValue(source, ref cursor, offset, out error);
                if (error is not null)
                {
                    return null;
                }

                predicates.Add(new SelectorPredicate(field, op, value!));
                SkipWhitespace(source, ref cursor);

                if (cursor >= source.Length)
                {
                    error = new SelectorParseError("Expected ']' to close attribute list.", offset + cursor);
                    return null;
                }

                if (source[cursor] == ',')
                {
                    cursor++;
                    continue;
                }

                if (source[cursor] == ']')
                {
                    cursor++;
                    break;
                }

                error = new SelectorParseError($"Unexpected token '{source[cursor]}' in attribute list.", offset + cursor);
                return null;
            }
        }

        return new SelectorSegment(role, predicates, text);
    }

    private static TextSelector? ParseTextSelector(string source, ref int cursor, int offset, out SelectorParseError? error)
    {
        error = null;
        cursor += 5;
        SkipWhitespace(source, ref cursor);

        TextMatchKind kind;
        string value;

        if (cursor < source.Length && source[cursor] == '"')
        {
            kind = TextMatchKind.Exact;
            value = ParseQuotedString(source, ref cursor, offset, out error)!;
            if (error is not null)
            {
                return null;
            }
        }
        else if (source[cursor..].StartsWith("contains", StringComparison.Ordinal))
        {
            cursor += 8;
            SkipWhitespace(source, ref cursor);
            if (cursor >= source.Length || source[cursor] != '=')
            {
                error = new SelectorParseError("Expected '=' after contains.", offset + cursor);
                return null;
            }

            cursor++;
            SkipWhitespace(source, ref cursor);
            kind = TextMatchKind.Contains;
            value = ParseQuotedString(source, ref cursor, offset, out error)!;
            if (error is not null)
            {
                return null;
            }
        }
        else
        {
            error = new SelectorParseError("Unsupported text selector. Use text(\"...\") or text(contains=\"...\").", offset + cursor);
            return null;
        }

        SkipWhitespace(source, ref cursor);
        if (cursor >= source.Length || source[cursor] != ')')
        {
            error = new SelectorParseError("Expected ')' to close text selector.", offset + cursor);
            return null;
        }

        cursor++;
        return new TextSelector(kind, value);
    }

    private static string? ParseValue(string source, ref int cursor, int offset, out SelectorParseError? error)
    {
        error = null;
        if (cursor < source.Length && source[cursor] == '"')
        {
            return ParseQuotedString(source, ref cursor, offset, out error);
        }

        var start = cursor;
        while (cursor < source.Length && !char.IsWhiteSpace(source[cursor]) && source[cursor] != ',' && source[cursor] != ']')
        {
            cursor++;
        }

        if (cursor == start)
        {
            error = new SelectorParseError("Expected attribute value.", offset + cursor);
            return null;
        }

        return source[start..cursor];
    }

    private static string? ParseQuotedString(string source, ref int cursor, int offset, out SelectorParseError? error)
    {
        error = null;
        if (cursor >= source.Length || source[cursor] != '"')
        {
            error = new SelectorParseError("Expected quoted string.", offset + cursor);
            return null;
        }

        cursor++;
        var builder = new StringBuilder();
        while (cursor < source.Length)
        {
            var current = source[cursor];
            if (current == '"')
            {
                cursor++;
                return builder.ToString();
            }

            builder.Append(current);
            cursor++;
        }

        error = new SelectorParseError("Unterminated string literal.", offset + cursor);
        return null;
    }

    private static void SkipWhitespace(string source, ref int cursor)
    {
        while (cursor < source.Length && char.IsWhiteSpace(source[cursor]))
        {
            cursor++;
        }
    }
}
