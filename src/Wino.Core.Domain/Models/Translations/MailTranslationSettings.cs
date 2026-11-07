#nullable enable
namespace Wino.Core.Domain.Models.Translations;

public enum MailTranslationProvider
{
    OpenAiCompatible,
    Plamo,
    Sarashina,
    DeepL
}

public sealed record MailTranslationSettings
{
    public MailTranslationProvider Provider { get; init; }
    public string TargetLanguage { get; init; } = "ja-JP";
    public string SourceLanguage { get; init; } = string.Empty;
    public string OpenAiEndpoint { get; init; } = "https://api.openai.com/v1";
    public string OpenAiModel { get; init; } = string.Empty;
    public string Prompt { get; init; } = DefaultPrompt;
    public string PlamoEndpoint { get; init; } = "http://localhost:1234/v1";
    public string PlamoModel { get; init; } = "plamo-3-translate";
    public bool DeepLFreeApi { get; init; } = true;

    public static string DefaultPrompt => Translator.MailTranslation_DefaultPrompt;
}
