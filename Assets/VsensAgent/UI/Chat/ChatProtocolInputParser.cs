using System;
using System.Collections.Generic;

namespace VsensAgent.UI
{
    public static class ChatProtocolInputParser
    {
        public static bool TryParseSelection(
            string input,
            string[] optionIds,
            string[] optionLabels,
            bool allowMultiple,
            out string[] selectedIds)
        {
            selectedIds = Array.Empty<string>();

            if (string.IsNullOrWhiteSpace(input) ||
                optionIds == null ||
                optionLabels == null ||
                optionIds.Length == 0 ||
                optionIds.Length != optionLabels.Length)
            {
                return false;
            }

            var trimmed = input.Trim();

            if (TryParseNumericSelection(trimmed, optionIds, allowMultiple, out selectedIds))
            {
                return true;
            }

            return TryParseNamedSelection(trimmed, optionIds, optionLabels, allowMultiple, out selectedIds);
        }

        private static bool TryParseNumericSelection(
            string input,
            string[] optionIds,
            bool allowMultiple,
            out string[] selectedIds)
        {
            selectedIds = Array.Empty<string>();
            var tokens = input.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return false;
            }

            var results = new List<string>();
            foreach (var token in tokens)
            {
                if (!int.TryParse(token, out var index))
                {
                    return false;
                }

                if (index < 1 || index > optionIds.Length)
                {
                    return false;
                }

                var optionId = optionIds[index - 1];
                if (!results.Contains(optionId))
                {
                    results.Add(optionId);
                }
            }

            if (!allowMultiple && results.Count != 1)
            {
                return false;
            }

            selectedIds = results.ToArray();
            return selectedIds.Length > 0;
        }

        private static bool TryParseNamedSelection(
            string input,
            string[] optionIds,
            string[] optionLabels,
            bool allowMultiple,
            out string[] selectedIds)
        {
            selectedIds = Array.Empty<string>();
            var tokens = allowMultiple
                ? input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                : new[] { input };

            if (!allowMultiple && tokens.Length != 1)
            {
                return false;
            }

            var results = new List<string>();
            foreach (var rawToken in tokens)
            {
                var token = rawToken.Trim();
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                var match = FindMatchingOptionId(token, optionIds, optionLabels);
                if (string.IsNullOrEmpty(match))
                {
                    return false;
                }

                if (!results.Contains(match))
                {
                    results.Add(match);
                }
            }

            if (!allowMultiple && results.Count != 1)
            {
                return false;
            }

            selectedIds = results.ToArray();
            return selectedIds.Length > 0;
        }

        private static string FindMatchingOptionId(string token, string[] optionIds, string[] optionLabels)
        {
            for (var index = 0; index < optionIds.Length; index++)
            {
                if (string.Equals(token, optionIds[index], StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, optionLabels[index], StringComparison.OrdinalIgnoreCase))
                {
                    return optionIds[index];
                }
            }

            return string.Empty;
        }
    }
}
