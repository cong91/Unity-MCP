/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Unity-MCP)    │
│  Copyright (c) 2025 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using com.IvanMurzak.McpPlugin.Common;
using UnityEngine;
using static com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Unity.MCP.Editor.Utils
{
    public class YamlAiAgentConfig : AiAgentConfig
    {
        private readonly Dictionary<string, (object value, bool required, ValueComparisonMode comparison)> _properties = new();
        private readonly HashSet<string> _propertiesToRemove = new();
        private readonly Dictionary<string, (string value, bool required, ValueComparisonMode comparison)> _requiredListValues = new();

        public override string ExpectedFileContent
        {
            get
            {
                var lines = new List<string>
                {
                    $"{BodyPath}:"
                };
                lines.AddRange(RenderServerBlock(DefaultMcpServerName, _properties.ToDictionary(x => x.Key, x => x.Value.value)));

                foreach (var listRequirement in _requiredListValues.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    lines.Add(string.Empty);
                    lines.AddRange(RenderListSection(listRequirement.Key, listRequirement.Value.value));
                }

                return string.Join(Environment.NewLine, lines) + Environment.NewLine;
            }
        }

        public YamlAiAgentConfig(
            string name,
            string configPath,
            string bodyPath = Consts.MCP.Server.DefaultBodyPath)
            : base(name: name, configPath: configPath, bodyPath: bodyPath)
        {
        }

        public YamlAiAgentConfig SetProperty(string key, object value, bool requiredForConfiguration = false, ValueComparisonMode comparison = ValueComparisonMode.Exact)
        {
            _properties[key] = (value, requiredForConfiguration, comparison);
            return this;
        }

        public YamlAiAgentConfig SetProperty(string key, object[] values, bool requiredForConfiguration = false, ValueComparisonMode comparison = ValueComparisonMode.Exact)
        {
            _properties[key] = (values, requiredForConfiguration, comparison);
            return this;
        }

        public YamlAiAgentConfig SetPropertyToRemove(string key)
        {
            _propertiesToRemove.Add(key);
            return this;
        }

        public YamlAiAgentConfig RequireListContains(string path, string value, bool requiredForConfiguration = false, ValueComparisonMode comparison = ValueComparisonMode.Exact)
        {
            _requiredListValues[path] = (value, requiredForConfiguration, comparison);
            return this;
        }

        public new YamlAiAgentConfig AddIdentityKey(string key)
        {
            base.AddIdentityKey(key);
            return this;
        }

        public override void ApplyHttpAuthorization(bool isRequired, string? token)
        {
            SetPropertyToRemove("headers");
            if (isRequired && !string.IsNullOrEmpty(token))
                SetProperty("bearer_token", token, requiredForConfiguration: true);
            else
                SetPropertyToRemove("bearer_token");
        }

        public override void ApplyStdioAuthorization(bool isRequired, string? token)
        {
            SetPropertyToRemove("headers");
            SetPropertyToRemove("bearer_token");

            if (!_properties.TryGetValue("args", out var argsProp) || !(argsProp.value is object[] currentArgs))
                return;

            var tokenPrefix = $"--{Args.Token}=";
            var filtered = currentArgs
                .Select(x => x?.ToString() ?? string.Empty)
                .Where(x => !x.StartsWith(tokenPrefix, StringComparison.Ordinal))
                .Cast<object>()
                .ToList();

            if (isRequired && !string.IsNullOrEmpty(token))
                filtered.Add($"{tokenPrefix}{token}");

            SetProperty("args", filtered.ToArray(), argsProp.required, argsProp.comparison);
        }

        public override bool Configure()
        {
            if (string.IsNullOrEmpty(ConfigPath))
                return false;

            try
            {
                var directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var lines = File.Exists(ConfigPath)
                    ? File.ReadAllLines(ConfigPath).ToList()
                    : new List<string>();

                UpsertServerBlock(lines, DefaultMcpServerName, _properties.ToDictionary(x => x.Key, x => x.Value.value));

                foreach (var requirement in _requiredListValues)
                    EnsureListContains(lines, requirement.Key, requirement.Value.value);

                File.WriteAllText(ConfigPath, string.Join(Environment.NewLine, lines).TrimEnd() + Environment.NewLine);
                return IsConfigured();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error configuring YAML file: {ex.Message}");
                Debug.LogException(ex);
                return false;
            }
        }

        public override bool Unconfigure()
        {
            if (string.IsNullOrEmpty(ConfigPath) || !File.Exists(ConfigPath))
                return false;

            try
            {
                var lines = File.ReadAllLines(ConfigPath).ToList();
                var changed = RemoveServerBlock(lines, DefaultMcpServerName);

                foreach (var deprecatedName in DeprecatedMcpServerNames)
                    changed = RemoveServerBlock(lines, deprecatedName) || changed;

                foreach (var requirement in _requiredListValues)
                    changed = RemoveListValue(lines, requirement.Key, requirement.Value.value) || changed;

                if (!changed)
                    return false;

                File.WriteAllText(ConfigPath, string.Join(Environment.NewLine, lines).TrimEnd() + Environment.NewLine);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error unconfiguring YAML file: {ex.Message}");
                Debug.LogException(ex);
                return false;
            }
        }

        public override bool IsDetected()
        {
            if (string.IsNullOrEmpty(ConfigPath) || !File.Exists(ConfigPath))
                return false;

            try
            {
                var lines = File.ReadAllLines(ConfigPath).ToList();
                if (FindServerBlockStart(lines, DefaultMcpServerName) >= 0)
                    return true;

                foreach (var deprecatedName in DeprecatedMcpServerNames)
                    if (FindServerBlockStart(lines, deprecatedName) >= 0)
                        return true;

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error reading YAML config file: {ex.Message}");
                Debug.LogException(ex);
                return false;
            }
        }

        public override bool IsConfigured()
        {
            if (string.IsNullOrEmpty(ConfigPath) || !File.Exists(ConfigPath))
                return false;

            try
            {
                var lines = File.ReadAllLines(ConfigPath).ToList();
                var entry = ParseServerProperties(lines, DefaultMcpServerName);
                if (entry == null)
                    return false;

                foreach (var property in _properties)
                {
                    if (!property.Value.required)
                        continue;

                    if (!entry.TryGetValue(property.Key, out var actualValue))
                        return false;

                    if (!ValuesMatch(property.Value.comparison, property.Value.value, actualValue))
                        return false;
                }

                foreach (var propertyToRemove in _propertiesToRemove)
                {
                    if (entry.ContainsKey(propertyToRemove))
                        return false;
                }

                foreach (var requirement in _requiredListValues)
                {
                    if (!requirement.Value.required)
                        continue;

                    var values = ParseListValues(lines, requirement.Key);
                    if (!values.Any(x => ValuesMatch(requirement.Value.comparison, requirement.Value.value, x)))
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Consts.Log.Tag} Error validating YAML config file: {ex.Message}");
                Debug.LogException(ex);
                return false;
            }
        }

        private void UpsertServerBlock(List<string> lines, string serverName, Dictionary<string, object> properties)
        {
            var sectionStart = FindOrCreateTopLevelSection(lines, BodyPath);
            var sectionEnd = FindTopLevelSectionEnd(lines, sectionStart);
            var blockStart = FindServerBlockStart(lines, serverName, sectionStart, sectionEnd);
            var rendered = RenderServerBlock(serverName, properties);

            if (blockStart >= 0)
            {
                var blockEnd = FindIndentedBlockEnd(lines, blockStart, 2, sectionEnd);
                lines.RemoveRange(blockStart, blockEnd - blockStart);
                lines.InsertRange(blockStart, rendered);
            }
            else
            {
                lines.InsertRange(sectionEnd, rendered);
            }
        }

        private bool RemoveServerBlock(List<string> lines, string serverName)
        {
            var sectionStart = FindTopLevelSection(lines, BodyPath);
            if (sectionStart < 0)
                return false;

            var sectionEnd = FindTopLevelSectionEnd(lines, sectionStart);
            var blockStart = FindServerBlockStart(lines, serverName, sectionStart, sectionEnd);
            if (blockStart < 0)
                return false;

            var blockEnd = FindIndentedBlockEnd(lines, blockStart, 2, sectionEnd);
            lines.RemoveRange(blockStart, blockEnd - blockStart);
            return true;
        }

        private static List<string> RenderServerBlock(string serverName, Dictionary<string, object> properties)
        {
            var lines = new List<string> { $"  {serverName}:" };

            foreach (var property in properties.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (!(property.Value is string) && property.Value is Array array)
                {
                    lines.Add($"    {property.Key}:"
                    );
                    foreach (var item in array)
                        lines.Add($"      - {FormatYamlScalar(item)}");
                }
                else
                {
                    lines.Add($"    {property.Key}: {FormatYamlScalar(property.Value)}");
                }
            }

            return lines;
        }

        private static List<string> RenderListSection(string path, string value)
        {
            var segments = path.Split('.');
            if (segments.Length != 2)
                throw new InvalidOperationException($"Only two-segment YAML list paths are supported. Received: {path}");

            return new List<string>
            {
                $"{segments[0]}:",
                $"  {segments[1]}:",
                $"    - {FormatYamlScalar(value)}"
            };
        }

        private static string FormatYamlScalar(object? value)
        {
            return value switch
            {
                null => "null",
                bool boolValue => boolValue ? "true" : "false",
                int intValue => intValue.ToString(),
                long longValue => longValue.ToString(),
                string stringValue => $"\"{stringValue.Replace("\\", "/").Replace("\"", "\\\"")}\"",
                _ => $"\"{value.ToString()!.Replace("\\", "/").Replace("\"", "\\\"")}\""
            };
        }

        private static int FindTopLevelSection(List<string> lines, string name)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim() == $"{name}:")
                    return i;
            }
            return -1;
        }

        private static int FindOrCreateTopLevelSection(List<string> lines, string name)
        {
            var existing = FindTopLevelSection(lines, name);
            if (existing >= 0)
                return existing;

            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                lines.Add(string.Empty);

            lines.Add($"{name}:");
            return lines.Count - 1;
        }

        private static int FindTopLevelSectionEnd(List<string> lines, int start)
        {
            for (int i = start + 1; i < lines.Count; i++)
            {
                var line = lines[i];
                if (!string.IsNullOrWhiteSpace(line) && !char.IsWhiteSpace(line[0]))
                    return i;
            }
            return lines.Count;
        }

        private int FindServerBlockStart(List<string> lines, string serverName)
        {
            var sectionStart = FindTopLevelSection(lines, BodyPath);
            if (sectionStart < 0)
                return -1;

            var sectionEnd = FindTopLevelSectionEnd(lines, sectionStart);
            return FindServerBlockStart(lines, serverName, sectionStart, sectionEnd);
        }

        private static int FindServerBlockStart(List<string> lines, string serverName, int sectionStart, int sectionEnd)
        {
            var header = $"  {serverName}:";
            for (int i = sectionStart + 1; i < sectionEnd; i++)
            {
                if (lines[i].TrimEnd() == header)
                    return i;
            }
            return -1;
        }

        private static int FindIndentedBlockEnd(List<string> lines, int start, int parentIndent, int maxEnd)
        {
            for (int i = start + 1; i < maxEnd; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var indent = CountIndent(lines[i]);
                if (indent <= parentIndent)
                    return i;
            }
            return maxEnd;
        }

        private Dictionary<string, object>? ParseServerProperties(List<string> lines, string serverName)
        {
            var sectionStart = FindTopLevelSection(lines, BodyPath);
            if (sectionStart < 0)
                return null;

            var sectionEnd = FindTopLevelSectionEnd(lines, sectionStart);
            var blockStart = FindServerBlockStart(lines, serverName, sectionStart, sectionEnd);
            if (blockStart < 0)
                return null;

            var blockEnd = FindIndentedBlockEnd(lines, blockStart, 2, sectionEnd);
            var result = new Dictionary<string, object>(StringComparer.Ordinal);

            for (int i = blockStart + 1; i < blockEnd; i++)
            {
                var rawLine = lines[i];
                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                if (CountIndent(rawLine) != 4)
                    continue;

                var trimmed = rawLine.Trim();
                var separatorIndex = trimmed.IndexOf(':');
                if (separatorIndex < 0)
                    continue;

                var key = trimmed.Substring(0, separatorIndex).Trim();
                var tail = trimmed.Substring(separatorIndex + 1).Trim();
                if (tail.Length > 0)
                {
                    result[key] = ParseYamlScalar(tail);
                    continue;
                }

                var listValues = new List<string>();
                var j = i + 1;
                while (j < blockEnd && CountIndent(lines[j]) > 4)
                {
                    var match = lines[j].Trim().StartsWith("-")
                        ? lines[j].Trim().Substring(1).Trim()
                        : null;
                    if (!string.IsNullOrEmpty(match))
                        listValues.Add(UnquoteYamlScalar(match));
                    j++;
                }

                result[key] = listValues.ToArray();
            }

            return result;
        }

        private List<string> ParseListValues(List<string> lines, string path)
        {
            var segments = path.Split('.');
            if (segments.Length != 2)
                return new List<string>();

            var sectionStart = FindTopLevelSection(lines, segments[0]);
            if (sectionStart < 0)
                return new List<string>();

            var sectionEnd = FindTopLevelSectionEnd(lines, sectionStart);
            var propertyStart = -1;
            for (int i = sectionStart + 1; i < sectionEnd; i++)
            {
                if (lines[i].TrimStart().StartsWith($"{segments[1]}:") && CountIndent(lines[i]) == 2)
                {
                    propertyStart = i;
                    break;
                }
            }

            if (propertyStart < 0)
                return new List<string>();

            var trimmed = lines[propertyStart].Trim();
            var inlineStart = trimmed.IndexOf('[');
            var inlineEnd = trimmed.LastIndexOf(']');
            if (inlineStart >= 0 && inlineEnd > inlineStart)
            {
                var inner = trimmed.Substring(inlineStart + 1, inlineEnd - inlineStart - 1).Trim();
                if (string.IsNullOrEmpty(inner))
                    return new List<string>();

                return inner.Split(',').Select(x => UnquoteYamlScalar(x.Trim())).Where(x => !string.IsNullOrEmpty(x)).ToList();
            }

            var values = new List<string>();
            for (int i = propertyStart + 1; i < sectionEnd; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var indent = CountIndent(lines[i]);
                if (indent <= 2)
                    break;

                var trimmedItem = lines[i].Trim();
                if (trimmedItem.StartsWith("-"))
                    values.Add(UnquoteYamlScalar(trimmedItem.Substring(1).Trim()));
            }

            return values;
        }

        private bool EnsureListContains(List<string> lines, string path, string value)
        {
            var segments = path.Split('.');
            if (segments.Length != 2)
                return false;

            var sectionStart = FindOrCreateTopLevelSection(lines, segments[0]);
            var sectionEnd = FindTopLevelSectionEnd(lines, sectionStart);
            var values = ParseListValues(lines, path);
            if (!values.Contains(value))
                values.Add(value);

            var rendered = new List<string> { $"  {segments[1]}:" };
            rendered.AddRange(values.Select(x => $"    - {FormatYamlScalar(x)}"));

            for (int i = sectionStart + 1; i < sectionEnd; i++)
            {
                if (CountIndent(lines[i]) == 2 && lines[i].TrimStart().StartsWith($"{segments[1]}:"))
                {
                    var propertyEnd = FindIndentedBlockEnd(lines, i, 2, sectionEnd);
                    lines.RemoveRange(i, propertyEnd - i);
                    lines.InsertRange(i, rendered);
                    return true;
                }
            }

            lines.InsertRange(sectionEnd, rendered);
            return true;
        }

        private bool RemoveListValue(List<string> lines, string path, string value)
        {
            var segments = path.Split('.');
            if (segments.Length != 2)
                return false;

            var sectionStart = FindTopLevelSection(lines, segments[0]);
            if (sectionStart < 0)
                return false;

            var sectionEnd = FindTopLevelSectionEnd(lines, sectionStart);
            for (int i = sectionStart + 1; i < sectionEnd; i++)
            {
                if (CountIndent(lines[i]) != 2 || !lines[i].TrimStart().StartsWith($"{segments[1]}:"))
                    continue;

                var values = ParseListValues(lines, path);
                var removed = values.RemoveAll(x => string.Equals(NormalizePath(x), NormalizePath(value), StringComparison.Ordinal)) > 0;
                if (!removed)
                    return false;

                var propertyEnd = FindIndentedBlockEnd(lines, i, 2, sectionEnd);
                lines.RemoveRange(i, propertyEnd - i);
                if (values.Count > 0)
                {
                    var rendered = new List<string> { $"  {segments[1]}:" };
                    rendered.AddRange(values.Select(x => $"    - {FormatYamlScalar(x)}"));
                    lines.InsertRange(i, rendered);
                }
                return true;
            }

            return false;
        }

        private static int CountIndent(string line)
        {
            var count = 0;
            while (count < line.Length && char.IsWhiteSpace(line[count]))
                count++;
            return count;
        }

        private static object ParseYamlScalar(string text)
        {
            var trimmed = text.Trim();
            if (bool.TryParse(trimmed, out var boolValue))
                return boolValue;
            if (int.TryParse(trimmed, out var intValue))
                return intValue;
            return UnquoteYamlScalar(trimmed);
        }

        private static string UnquoteYamlScalar(string text)
        {
            var trimmed = text.Trim();
            if ((trimmed.StartsWith("\"") && trimmed.EndsWith("\"")) || (trimmed.StartsWith("'") && trimmed.EndsWith("'")))
                trimmed = trimmed.Substring(1, trimmed.Length - 2);

            return trimmed.Replace("\\\"", "\"").Replace("\\", "/");
        }

        private static bool ValuesMatch(ValueComparisonMode comparison, object expected, object actual)
        {
            return (expected, actual) switch
            {
                (string e, string a) => AreStringsEquivalent(comparison, e, a),
                (string[] e, string[] a) => e.Length == a.Length && e.Zip(a, (x, y) => AreStringsEquivalent(comparison, x, y)).All(x => x),
                (object[] e, string[] a) => e.Select(x => x.ToString() ?? string.Empty).SequenceEqual(a, new StringComparerAdapter(comparison)),
                (bool e, bool a) => e == a,
                (int e, int a) => e == a,
                _ => string.Equals(expected.ToString(), actual.ToString(), StringComparison.Ordinal)
            };
        }

        private static bool AreStringsEquivalent(ValueComparisonMode comparison, string expected, string actual)
        {
            return comparison switch
            {
                ValueComparisonMode.Path => NormalizePath(expected) == NormalizePath(actual),
                ValueComparisonMode.Url => string.Equals(NormalizeUrl(expected), NormalizeUrl(actual), StringComparison.OrdinalIgnoreCase),
                _ => expected == actual
            };
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }

        private static string NormalizeUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            return url.TrimEnd('/');
        }

        private sealed class StringComparerAdapter : IEqualityComparer<string>
        {
            private readonly ValueComparisonMode _comparison;

            public StringComparerAdapter(ValueComparisonMode comparison)
            {
                _comparison = comparison;
            }

            public bool Equals(string? x, string? y)
            {
                if (x == null || y == null)
                    return x == y;
                return AreStringsEquivalent(_comparison, x, y);
            }

            public int GetHashCode(string obj)
            {
                return _comparison switch
                {
                    ValueComparisonMode.Path => NormalizePath(obj).GetHashCode(),
                    ValueComparisonMode.Url => NormalizeUrl(obj).GetHashCode(),
                    _ => obj.GetHashCode()
                };
            }
        }
    }
}
