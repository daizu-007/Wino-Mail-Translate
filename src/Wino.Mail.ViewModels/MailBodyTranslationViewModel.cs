#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wino.Core.Domain;
using Wino.Core.Domain.Interfaces;
using Wino.Core.Domain.Models.Translations;

namespace Wino.Mail.ViewModels;

public sealed partial class MailBodyTranslationViewModel(IMailTranslationService service, IMailTranslationSettingsStore settingsStore) : ObservableObject
{
    private string _originalHtml = string.Empty;
    private MailBodyTranslationResult? _translatedBody;
    private MailTranslationSettings? _translatedSettings;
    private CancellationTokenSource? _request;
    private int _contentVersion;

    public event Func<Task>? RerenderRequested;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TranslateCommand))]
    [NotifyPropertyChangedFor(nameof(CanTranslate))]
    public partial bool HasContent { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TranslateCommand))]
    [NotifyPropertyChangedFor(nameof(CanTranslate))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText))]
    public partial bool IsShowingTranslation { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string Error { get; set; } = string.Empty;

    public bool CanTranslate => HasContent && !IsBusy;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string ButtonText => IsShowingTranslation ? Translator.WinoIntelligence_ShowOriginal : Translator.WinoIntelligence_Translate;

    public void SetContent(string html)
    {
        _contentVersion++;
        _request?.Cancel();
        _request = null;
        _originalHtml = html;
        _translatedBody = null;
        _translatedSettings = null;
        IsShowingTranslation = false;
        IsBusy = false;
        Status = string.Empty;
        Error = string.Empty;
        HasContent = !string.IsNullOrWhiteSpace(html);
    }

    public string ResolveHtml(string html) => IsShowingTranslation && _translatedBody is { } translated ? translated.Html : html;
    public string ResolveAccessibleText(string original) => IsShowingTranslation && _translatedBody is { } translated ? translated.PlainText : original;

    [RelayCommand(CanExecute = nameof(CanTranslate), AllowConcurrentExecutions = true)]
    private async Task TranslateAsync()
    {
        if (IsBusy) return;
        Error = string.Empty;
        var version = _contentVersion;
        using var request = new CancellationTokenSource();
        _request = request;
        IsBusy = true;
        Status = Translator.WinoIntelligence_Translating;
        try
        {
            if (IsShowingTranslation)
            {
                IsShowingTranslation = false;
                Status = Translator.MailTranslation_ShowingOriginal;
                if (RerenderRequested is { } restore) await restore();
                return;
            }
            var settings = settingsStore.Load();
            var translated = _translatedBody is { } cached && settings == _translatedSettings
                ? cached
                : await service.TranslateHtmlAsync(_originalHtml, settings, request.Token);
            if (request.IsCancellationRequested || version != _contentVersion) return;
            _translatedBody = translated;
            _translatedSettings = settings;
            IsShowingTranslation = true;
            Status = string.Format(Translator.MailTranslation_TranslatedTo, settings.TargetLanguage);
            if (RerenderRequested is { } render) await render();
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            if (version == _contentVersion && ReferenceEquals(_request, request)) Status = string.Empty;
        }
        catch (Exception exception)
        {
            if (version != _contentVersion || request.IsCancellationRequested) return;
            IsShowingTranslation = false;
            Status = string.Empty;
            Error = exception is InvalidOperationException ? exception.Message : Translator.MailTranslation_Failed;
        }
        finally
        {
            if (ReferenceEquals(_request, request))
            {
                _request = null;
                IsBusy = false;
            }
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _request?.Cancel();
        _request = null;
        IsBusy = false;
        Status = string.Empty;
    }
}
