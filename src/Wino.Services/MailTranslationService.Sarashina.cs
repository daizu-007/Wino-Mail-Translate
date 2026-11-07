#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wino.Core.Domain.Models.Translations;
using Wino.Mail.AI.Abstractions;

namespace Wino.Services;

public sealed partial class MailTranslationService
{
    private const int SarashinaCharacterLimit = 5000;
    private const int SarashinaRequestsPerMinute = 30;
    private readonly Queue<long> _sarashinaRequests = new();

    private async Task<Dictionary<string, string>> TranslateSarashinaSegmentsAsync(
        IEnumerable<MailContentSegment> segments, MailTranslationSettings settings, string apiKey, CancellationToken token,
        IProgress<MailTranslationProgress>? progress)
    {
        var texts = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var segment in segments)
        {
            token.ThrowIfCancellationRequested();
            foreach (var part in ProjectionMarker().Split(segment.Text))
            {
                if (ProjectionMarker().IsMatch(part)) continue;
                foreach (var chunk in SplitText(part, SarashinaCharacterLimit))
                {
                    var text = chunk.Trim();
                    if (text.Any(char.IsLetter) && seen.Add(text)) texts.Add(text);
                }
            }
        }

        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        void Report(TimeSpan wait) => progress?.Report(new MailTranslationProgress(translations.Count, texts.Count, (int)Math.Ceiling(wait.TotalSeconds)));
        Report(TimeSpan.Zero);
        var batch = new List<string>();
        var batchLength = 64;
        foreach (var text in texts)
        {
            token.ThrowIfCancellationRequested();
            if (batch.Count > 0 && (batchLength + text.Length + 64 > SarashinaCharacterLimit || batch.Count == 24))
            {
                await TranslateSarashinaBatchAsync(batch, translations, settings, apiKey, token, Report).ConfigureAwait(false);
                batch.Clear();
                batchLength = 64;
            }
            if (text.Length + 128 > SarashinaCharacterLimit)
            {
                translations.Add(text, await SendAsync(text, string.Empty, settings, apiKey, token, Report).ConfigureAwait(false));
                Report(TimeSpan.Zero);
                continue;
            }
            batch.Add(text);
            batchLength += text.Length + 64;
        }
        if (batch.Count > 0)
            await TranslateSarashinaBatchAsync(batch, translations, settings, apiKey, token, Report).ConfigureAwait(false);
        return translations;
    }

    private async Task TranslateSarashinaBatchAsync(List<string> texts, Dictionary<string, string> translations,
        MailTranslationSettings settings, string apiKey, CancellationToken token, Action<TimeSpan> report, bool allowSplit = true)
    {
        if (texts.Count > 1)
        {
            // Long identifiers add generation time to every run; reserve them for collisions with source text.
            var prefix = "[[WINO_";
            while (texts.Any(text => text.Contains(prefix, StringComparison.Ordinal))) prefix = $"[[WINO_{Guid.NewGuid():N}_";
            var markers = Enumerable.Range(0, texts.Count + 1).Select(index => $"{prefix}{index}]]").ToArray();
            var payload = new StringBuilder();
            for (var i = 0; i < texts.Count; i++)
                payload.Append(markers[i]).Append('\n').Append(texts[i]).Append('\n');
            payload.Append(markers[^1]);
            var result = await SendAsync(payload.ToString(), string.Empty, settings, apiKey, token, report).ConfigureAwait(false);
            if (TryReadSarashinaBatch(result, markers, prefix, out var values))
            {
                for (var i = 0; i < texts.Count; i++) translations.Add(texts[i], values[i]);
                report(TimeSpan.Zero);
                return;
            }
            if (allowSplit && texts.Count > 2)
            {
                var middle = texts.Count / 2;
                await TranslateSarashinaBatchAsync(texts.GetRange(0, middle), translations, settings, apiKey, token, report, allowSplit: false).ConfigureAwait(false);
                await TranslateSarashinaBatchAsync(texts.GetRange(middle, texts.Count - middle), translations, settings, apiKey, token, report, allowSplit: false).ConfigureAwait(false);
                return;
            }
        }

        // Translation models may alter delimiters. Never guess which HTML run a result belongs to.
        foreach (var text in texts)
        {
            translations.Add(text, await SendAsync(text, string.Empty, settings, apiKey, token, report).ConfigureAwait(false));
            report(TimeSpan.Zero);
        }
    }

    private static bool TryReadSarashinaBatch(string result, string[] markers, string prefix, out string[] values)
    {
        values = new string[markers.Length - 1];
        var position = 0;
        for (var i = 0; i < markers.Length; i++)
        {
            var markerPosition = result.IndexOf(markers[i], position, StringComparison.Ordinal);
            if (markerPosition < 0 || result.LastIndexOf(markers[i], StringComparison.Ordinal) != markerPosition) return false;
            var text = result[position..markerPosition].Trim();
            if (i == 0 ? text.Length != 0 : text.Length == 0 || text.Contains(prefix, StringComparison.Ordinal)) return false;
            if (i > 0) values[i - 1] = text;
            position = markerPosition + markers[i].Length;
        }
        return string.IsNullOrWhiteSpace(result[position..]);
    }

    private async Task WaitForSarashinaQuotaAsync(CancellationToken token, Action<TimeSpan>? waiting)
    {
        await _sarashinaGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                while (_sarashinaRequests.TryPeek(out var started) && Stopwatch.GetElapsedTime(started) >= TimeSpan.FromMinutes(1))
                    _sarashinaRequests.Dequeue();
                if (_sarashinaRequests.Count < SarashinaRequestsPerMinute)
                {
                    _sarashinaRequests.Enqueue(Stopwatch.GetTimestamp());
                    return;
                }
                var delay = TimeSpan.FromMinutes(1) - Stopwatch.GetElapsedTime(_sarashinaRequests.Peek());
                if (delay <= TimeSpan.Zero) continue;
                waiting?.Invoke(delay);
                await Task.Delay(delay, token).ConfigureAwait(false);
                waiting?.Invoke(TimeSpan.Zero);
            }
        }
        finally { _sarashinaGate.Release(); }
    }
}
