#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wino.Core.Domain.Interfaces;
using Wino.Core.Domain.Models.Translations;

namespace Wino.Services;

public sealed class MailTranslationSettingsStore(IConfigurationService configuration, ISecureItemStore secrets) : IMailTranslationSettingsStore
{
    private const string SettingsKey = "MailTranslationSettings";
    private const string SecretScope = "mail-translation-api-keys-v1";

    public MailTranslationSettings Load()
    {
        var json = configuration.Get(SettingsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize(json, MailTranslationJsonContext.Default.MailTranslationSettings) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public void Save(MailTranslationSettings settings)
        => configuration.Set(SettingsKey, JsonSerializer.Serialize(settings, MailTranslationJsonContext.Default.MailTranslationSettings));

    public string GetApiKey(MailTranslationProvider provider)
    {
        var bytes = secrets.Read(SecretScope, provider.ToString());
        if (bytes is null) return string.Empty;
        try { return Encoding.UTF8.GetString(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public void SetApiKey(MailTranslationProvider provider, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            secrets.Delete(SecretScope, provider.ToString());
            return;
        }
        var bytes = Encoding.UTF8.GetBytes(apiKey.Trim());
        try { secrets.Write(SecretScope, provider.ToString(), bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}

[JsonSerializable(typeof(MailTranslationSettings))]
internal partial class MailTranslationJsonContext : JsonSerializerContext;
