namespace Wino.Core.Domain.Models.Translations;

public sealed record MailTranslationProgress(int CompletedParts, int TotalParts, int WaitingSeconds = 0);
