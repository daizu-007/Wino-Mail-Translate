#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wino.Core.Domain;
using Wino.Core.Domain.Interfaces;
using Wino.Core.Domain.Models.Ai;
using Wino.Core.Domain.Models.Translations;

namespace Wino.Mail.ViewModels;

public sealed partial class MailTranslationSettingsViewModel : ObservableObject
{
    private readonly IMailTranslationSettingsStore _store;
    private readonly Dictionary<int, string> _pendingKeys = [];
    private bool _loading;

    public List<string> Providers { get; } = [Translator.MailTranslation_OpenAiCompatible, "LM Studio · PLaMo 3 Translate", "Sarashina Translate API", "DeepL API"];
    public List<AiTranslateLanguageOption> Languages { get; } = AiActionCatalog.GetTranslateLanguageOptions().ToList();
    public List<AiTranslateLanguageOption> SourceLanguages { get; } = new AiTranslateLanguageOption[]
        { new(string.Empty, Translator.WinoIntelligence_DetectLanguage) }.Concat(AiActionCatalog.GetTranslateLanguageOptions()).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpenAi))]
    [NotifyPropertyChangedFor(nameof(IsPlamo))]
    [NotifyPropertyChangedFor(nameof(IsSarashina))]
    [NotifyPropertyChangedFor(nameof(IsDeepL))]
    public partial int ProviderIndex { get; set; }

    public bool IsOpenAi => ProviderIndex == (int)MailTranslationProvider.OpenAiCompatible;
    public bool IsPlamo => ProviderIndex == (int)MailTranslationProvider.Plamo;
    public bool IsSarashina => ProviderIndex == (int)MailTranslationProvider.Sarashina;
    public bool IsDeepL => ProviderIndex == (int)MailTranslationProvider.DeepL;

    [ObservableProperty] public partial AiTranslateLanguageOption? TargetLanguage { get; set; }
    [ObservableProperty] public partial AiTranslateLanguageOption? SourceLanguage { get; set; }
    [ObservableProperty] public partial string OpenAiEndpoint { get; set; } = string.Empty;
    [ObservableProperty] public partial string OpenAiModel { get; set; } = string.Empty;
    [ObservableProperty] public partial string Prompt { get; set; } = string.Empty;
    [ObservableProperty] public partial string PlamoEndpoint { get; set; } = string.Empty;
    [ObservableProperty] public partial string PlamoModel { get; set; } = string.Empty;
    [ObservableProperty] public partial bool DeepLFreeApi { get; set; }
    [ObservableProperty] public partial string ApiKey { get; set; } = string.Empty;
    [ObservableProperty] public partial string Status { get; set; } = string.Empty;

    public MailTranslationSettingsViewModel(IMailTranslationSettingsStore store)
    {
        _store = store;
        Reload();
    }

    public void Reload()
    {
        _loading = true;
        try
        {
            _pendingKeys.Clear();
            ApiKey = string.Empty;
            var settings = _store.Load();
            ProviderIndex = Enum.IsDefined(settings.Provider) ? (int)settings.Provider : 0;
            TargetLanguage = Languages.Find(x => x.Code == settings.TargetLanguage) ?? Languages.Find(x => x.Code == "ja-JP");
            SourceLanguage = SourceLanguages.Find(x => x.Code == settings.SourceLanguage) ?? SourceLanguages[0];
            OpenAiEndpoint = settings.OpenAiEndpoint;
            OpenAiModel = settings.OpenAiModel;
            Prompt = settings.Prompt;
            PlamoEndpoint = settings.PlamoEndpoint;
            PlamoModel = settings.PlamoModel;
            DeepLFreeApi = settings.DeepLFreeApi;
            ApiKey = _store.GetApiKey((MailTranslationProvider)ProviderIndex);
            Status = string.Empty;
        }
        catch (Exception)
        {
            Status = Translator.MailTranslation_SettingsFailed;
        }
        finally { _loading = false; }
    }

    partial void OnProviderIndexChanged(int oldValue, int newValue)
    {
        if (_loading || newValue < 0 || newValue >= Providers.Count) return;
        if (oldValue >= 0 && oldValue < Providers.Count) _pendingKeys[oldValue] = ApiKey;
        ApiKey = string.Empty;
        try
        {
            ApiKey = _pendingKeys.TryGetValue(newValue, out var pending) ? pending : _store.GetApiKey((MailTranslationProvider)newValue);
            Status = string.Empty;
        }
        catch (Exception) { Status = Translator.MailTranslation_SettingsFailed; }
    }

    [RelayCommand]
    private void ResetPrompt() => Prompt = MailTranslationSettings.DefaultPrompt;

    [RelayCommand]
    private void Save()
    {
        try
        {
            if (ProviderIndex < 0 || ProviderIndex >= Providers.Count || TargetLanguage is null || SourceLanguage is null)
                throw new InvalidOperationException(Translator.MailTranslation_InvalidSettings);
            if (IsOpenAi && (string.IsNullOrWhiteSpace(OpenAiModel) || !Prompt.Contains("{targetLanguage}", StringComparison.Ordinal)))
                throw new InvalidOperationException(Translator.MailTranslation_InvalidSettings);
            if (IsPlamo && string.IsNullOrWhiteSpace(PlamoModel))
                throw new InvalidOperationException(Translator.MailTranslation_InvalidSettings);
            if (IsSarashina && (SourceLanguage.Code is not ("ja-JP" or "en-US") || TargetLanguage.Code is not ("ja-JP" or "en-US") || SourceLanguage.Code == TargetLanguage.Code))
                throw new InvalidOperationException(Translator.MailTranslation_SarashinaLanguages);
            if ((IsSarashina || IsDeepL) && string.IsNullOrWhiteSpace(ApiKey))
                throw new InvalidOperationException(Translator.MailTranslation_KeyRequired);
            var endpoint = IsPlamo ? PlamoEndpoint : OpenAiEndpoint;
            if ((IsOpenAi || IsPlamo) && (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) ||
                uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0))
                throw new InvalidOperationException(Translator.MailTranslation_InvalidEndpoint);
            var settings = new MailTranslationSettings
            {
                Provider = (MailTranslationProvider)ProviderIndex,
                TargetLanguage = TargetLanguage.Code,
                SourceLanguage = SourceLanguage.Code,
                OpenAiEndpoint = OpenAiEndpoint.Trim(),
                OpenAiModel = OpenAiModel.Trim(),
                Prompt = Prompt,
                PlamoEndpoint = PlamoEndpoint.Trim(),
                PlamoModel = PlamoModel.Trim(),
                DeepLFreeApi = DeepLFreeApi
            };
            _pendingKeys[ProviderIndex] = ApiKey;
            foreach (var (provider, key) in _pendingKeys)
                _store.SetApiKey((MailTranslationProvider)provider, key);
            _store.Save(settings);
            _pendingKeys.Clear();
            Status = Translator.MailTranslation_SettingsSaved;
        }
        catch (Exception exception)
        {
            Status = exception is InvalidOperationException ? exception.Message : Translator.MailTranslation_SettingsFailed;
        }
    }
}
