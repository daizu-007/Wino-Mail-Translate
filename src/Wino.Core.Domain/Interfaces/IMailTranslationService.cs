using System;
using System.Threading;
using System.Threading.Tasks;
using Wino.Core.Domain.Models.Translations;

namespace Wino.Core.Domain.Interfaces;

public interface IMailTranslationService
{
    Task<MailBodyTranslationResult> TranslateHtmlAsync(string html, MailTranslationSettings settings, CancellationToken cancellationToken = default,
        IProgress<MailTranslationProgress>? progress = null);
}

public interface IMailTranslationSettingsStore
{
    MailTranslationSettings Load();
    void Save(MailTranslationSettings settings);
    string GetApiKey(MailTranslationProvider provider);
    void SetApiKey(MailTranslationProvider provider, string apiKey);
}
