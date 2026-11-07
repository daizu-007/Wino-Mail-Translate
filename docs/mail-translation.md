# Message translation

The Windows reader offers a **Translate** button above the message body. It uses your own provider settings independently of Wino Intelligence subscriptions. The translation service, settings contracts, and ViewModels are shared; the new settings and reader controls are implemented in the Windows application.

## Setup and use

Open **Settings > Reader & Composer > Message translation**, select a provider and the one-click translation language, enter the connection details, and select **Save**. The gear button beside **Translate** opens the same settings page.

| Provider | Configuration |
| --- | --- |
| OpenAI-compatible API | API base URL including `/v1`, model identifier, and API key if required. The default base URL is `https://api.openai.com/v1`; enter a model available to your account. Requests use `POST chat/completions`. |
| LM Studio · PLaMo 3 Translate | Start LM Studio's server, load a PLaMo 3 Translate model, and copy its API identifier into the model field. The default base URL is `http://localhost:1234/v1`. Requests use `POST completions` with the model's PFML3 translation format. Authentication is optional when the local server allows it. |
| Sarashina Translate API | Enter the Sarashina API key. Select Japanese as source and English as target, or English as source and Japanese as target. Automatic source detection is unavailable for this provider. |
| DeepL API | Enter a DeepL **API** key and select API Free or API Pro to match the key. Source detection is supported. |

OpenAI-compatible prompts support `{targetLanguage}` (required) and `{sourceLanguage}`. The model receives the text and surrounding sentence separately from the instructions. **Restore default prompt** restores the supplied prompt; **Save** applies it.

Select **Translate** in the reader to apply the saved language. **Show original** restores the original body; selecting **Translate** again reuses the translation while the content and settings match. **Cancel** abandons the current translation. Changing messages cancels the old request and prevents its result from appearing on the new message. A failure leaves the original body available and displays an error beside the reader.

## Content and storage

The existing translation projector separates text from HTML. Original images, URLs, styling, tables, and inline formatting stay in the document. Structural projector markers stay on the device; text runs are translated and applied as text, so a provider cannot introduce executable HTML. Inline fragments receive surrounding sentence context where the provider supports it. Long text runs are split at punctuation or whitespace, with a Unicode-safe fallback. The original MIME message is never overwritten, and translations stay in memory for the current reader content.

Only text is sent to the selected provider. Local PLaMo requests use the configured LM Studio server. Custom endpoints require HTTPS, with HTTP allowed for loopback servers. Redirects are disabled to avoid forwarding API credentials. API keys are stored per provider in the platform secure item store, with protected files using the platform secret protector on Windows. They are separate from the ordinary translation settings and preference exports.

Sarashina groups text runs into requests within the 5,000-character limit and reuses translations for identical runs. Temporary delimiters identify the runs; every delimiter must return exactly once, in order, before any translation is assigned to the original HTML. A changed or missing delimiter retries in smaller groups, then falls back to individual requests if necessary. The current documented quota is 30 requests per minute: requests are counted in a rolling 60-second window without adding a fixed delay to every request. HTTP 429 responses respect `Retry-After` with bounded, cancellable retries. Sarashina network requests time out after 60 seconds. The reader shows completed text parts and quota waits, so long translations do not appear stalled. Large messages can still take longer, especially with a local model or provider rate limits.

Provider references: [LM Studio compatible endpoints](https://lmstudio.ai/docs/developer/openai-compat), [PLaMo 3 Translate model and PFML3 format](https://huggingface.co/pfnet/plamo-3-translate-8b), [Sarashina Translate API](https://docs.sarashina.ai/api-reference/translate), [Sarashina rate limits](https://docs.sarashina.ai/rate-limits), [DeepL API specification](https://github.com/DeepL/openapi/blob/main/openapi.yaml).

## Verification handoff

Windows checks use the [local Docker lab](../tools/local-lab/README.md) and [WinApp CLI](harness/development-windows.md#agent-ui-checks). Check all four providers with configured credentials/models; save and reopen settings; translate plain text and HTML messages with headings, lists, tables, links, inline images, and quoted text; restore the original; cancel; navigate away during a slow request; exercise invalid keys, unavailable servers, incomplete responses, and HTTP 429. Verify light/dark mode, narrow reader widths, keyboard operation, and accessible names. Check that mail storage, remote-content policy, and link destinations remain unchanged. These runtime checks need a prepared lab and are separate from compilation.

Shared-code builds pending on a suitably configured host:

```powershell
dotnet build src/Wino.Mail.Android/Wino.Mail.Android.csproj -c Debug -r android-x64 -p:WinoTargetPlatform=Android
```

On a Mac with the repository's workloads:

```sh
~/.dotnet/dotnet build src/Wino.Mail.MacOS/Wino.Mail.MacOS.csproj -c Debug -r osx-arm64 -p:WinoTargetRuntimeIdentifier=osx-arm64 -p:WinoTargetPlatform=MacOS -p:EnableWindowsTargeting=true
~/.dotnet/dotnet build src/Wino.Mail.MacOS/Wino.Mail.MacOS.csproj -c Debug -r osx-x64 -p:WinoTargetRuntimeIdentifier=osx-x64 -p:WinoTargetPlatform=MacOS -p:EnableWindowsTargeting=true
~/.dotnet/dotnet build src/Wino.Mail.iOS/Wino.Mail.iOS.csproj -c Debug -r iossimulator-arm64 -p:WinoTargetPlatform=iOS -p:EnableWindowsTargeting=true
```
