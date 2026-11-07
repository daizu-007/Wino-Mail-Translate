# Windowsで常用するためのローカルビルド

このチェックアウトからRelease x64（Native AOT）の署名済みMSIX bundleを作成します。
公式のAzure署名アカウントやストアへの公開権限は不要です。未コミットのソース変更も含めてビルドします。
自己署名は個人利用・信頼できる相手への配布向けです。

## 必要なもの

- Windows x64、PowerShell 7.2以降（`pwsh`）。
- [global.json](global.json)で指定された.NET 10 SDK。
- Visual Studio 2022/Build ToolsのMSBuildとC++ x86/x64ビルドツール。
- Windows SDKのMakeAppx、MakePRI、SignTool。
- [nuget.config](nuget.config)のパッケージソースへのアクセス。
- インストール先にはMicrosoft Edge WebView2 Runtime。未導入なら[Microsoft公式ページ](https://developer.microsoft.com/microsoft-edge/webview2/)からEvergreen Runtimeを導入してください。

ビルド中に依存関係を復元します。開発用のF5起動ではなく、スタートメニューから使うパッケージを作ります。
ローカルスクリプトはランタイムとNative AOTコンパイラに.NET 10.0.14を指定します。
SDK 10.0.402の既定値10.0.13は[既知の脆弱性](https://github.com/advisories/GHSA-p3jp-9v83-76w9)の対象です。
将来の修正版は`-RuntimeFrameworkVersion 10.0.xx`で指定できます。監査や警告のエラー扱いは無効にしません。

## このPCでビルドしてインストール

リポジトリのルートで、通常権限のPowerShellから実行します。

```powershell
pwsh -NoProfile -File .\scripts\release\windows\build-local.ps1 -Install
```

初回はマニフェストのPublisherと一致する自己署名証明書を`Cert:\CurrentUser\My`に作成します。
有効期間は3年です。秘密鍵はWindowsの証明書ストアに保存し、ビルド成果物には出力しません。
次回以降は同じ証明書を使います。証明書が期限切れの場合は停止します。
翻訳の自動生成やアップロードは行いません。

ビルド終了後、公開証明書をローカルコンピューターの「信頼されたユーザー」（`TrustedPeople`）へ登録するときだけUACが表示されます。
管理者の承認後、呼び出したユーザーにパッケージをインストールします。
Winoを終了してから実行し、完了後はスタートメニューのWino Mailを開いてください。
Releaseのexeを直接起動しないでください。

ビルドだけなら`-Install`を省略します。出力例（バージョンはソースに従います）：

```text
artifacts/local-release/<version>/Store/
  WinoMail_Store_<version>.msixbundle
  WinoMail_Store_TestCertificate.cer
  certificate-thumbprint.txt
  install-local.ps1
  INSTALL.txt
  WinoMail_Store_<version>.msixupload
```

インストールに使うのは`.msixbundle`です。`.msixupload`は不要です。
シンボルは同じバージョンの`Symbols`フォルダーに保存されます。
失敗時のログは出力ルートの`.staging`以下に残ります。

既存の出力は上書きしません。同じソースバージョンで再ビルドする場合は別の出力先を指定します。

```powershell
pwsh -NoProfile -File .\scripts\release\windows\build-local.ps1 -OutputRoot .\artifacts\personal-build-2
```

このパッケージはチェックイン済みのStore版と同じパッケージID・Publisherを保持します。
既存のStore版、別の署名でインストールした版、開発版がある場合、インストールヘルパーは置き換えず停止します。
既存アプリやデータを削除せず、別PCまたは既存パッケージのないWindowsユーザーで利用してください。
Storeからの後日の更新が、このローカルビルドを置き換える場合があります。

## 別PCで署名を信頼してインストール

1. 上記の`Store`フォルダーから`.msixbundle`、`.cer`、`install-local.ps1`を別PCの同じフォルダーへコピーします。
2. ビルドPCのコンソールに表示された証明書の拇印（40桁）を、別の信頼できる経路で確認します。配布フォルダー内の`certificate-thumbprint.txt`だけを根拠にしないでください。
3. 別PCでPowerShell 7.2以降を開き、コピー先へ移動して実行します。`<確認済みの40桁の拇印>`を実際の値に置き換えます。

```powershell
pwsh -NoProfile -File .\install-local.ps1 -ExpectedThumbprint '<確認済みの40桁の拇印>'
```

ヘルパーが公開証明書とbundleの署名を照合し、必要な場合のみUACで証明書登録を要求します。
証明書は「ローカルコンピューター → 信頼されたユーザー」に登録します。「信頼されたルート証明機関」には登録しません。
証明書登録後に署名が有効であることを再確認し、アプリをインストールします。
別PCには.NET SDK、Visual Studio、秘密鍵は不要です。PowerShellとWebView2 Runtimeは必要です。
Windowsの組織ポリシーでサイドロードが禁止されている場合は管理者に確認してください。

手動で登録する場合は、**管理者権限のPowerShell**で次を実行します。

```powershell
$cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new((Resolve-Path .\WinoMail_Store_TestCertificate.cer).Path)
$cert | Format-List Subject, Thumbprint, NotAfter
# 拇印をビルドPCの値と照合してから登録する
Import-Certificate -FilePath .\WinoMail_Store_TestCertificate.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

その後、通常権限のPowerShellで`install-local.ps1`を実行します。
この証明書を信頼すると、その秘密鍵で署名されたMSIXをWindowsが信頼します。信頼できる作成者の証明書だけを登録してください。
Microsoftの[パッケージ署名用証明書の説明](https://learn.microsoft.com/windows/msix/package/create-certificate-package-signing)も参照してください。

## 更新と証明書のバックアップ

更新は同じ証明書で署名し、`src/Wino.Mail.WinUI/Package.appxmanifest`のVersionをインストール済みより大きくしてビルドします。
4要素のバージョンの最後は`0`を維持してください。バージョン変更は他プラットフォームにも影響します。
同じ版や古い版へのインストールはヘルパーが停止します。通常の更新ではアプリデータが引き継がれます。
このローカル配布には自動更新フィードを設定していません。

ビルドPCの移行に備え、秘密鍵を含む証明書をパスワード付きPFXで安全な場所へバックアップできます。
次の例の拇印と保存先を置き換えてください。パスワードは対話入力します。

```powershell
$password = Read-Host 'PFXのバックアップ用パスワード' -AsSecureString
Export-PfxCertificate -Cert 'Cert:\CurrentUser\My\<40桁の拇印>' -FilePath 'E:\PrivateBackup\WinoMail-signing.pfx' -Password $password
```

PFXとパスワードを配布・コミットしないでください。別PCでアプリを使うだけなら公開`.cer`だけを渡します。
ビルドPCを移行するときだけ、新しいビルドPCの`Cert:\CurrentUser\My`にPFXをインポートし、拇印を明示します。

```powershell
$password = Read-Host 'PFXのパスワード' -AsSecureString
Import-PfxCertificate -FilePath 'E:\PrivateBackup\WinoMail-signing.pfx' -CertStoreLocation Cert:\CurrentUser\My -Password $password
pwsh -NoProfile -File .\scripts\release\windows\build-local.ps1 -CertificateThumbprint '<40桁の拇印>'
```

証明書を更新・再作成した場合は、配布先でも新しい拇印を確認して信頼を登録し直す必要があります。
ヘルパーは署名の変わった既存パッケージを自動置換しません。
既存データのバックアップと移行方針を確認してから対応してください。

詳細なビルド規則は[Windows開発ガイド](docs/harness/development-windows.md)、既存のパッケージ処理は[Windowsリリースガイド](docs/releases-windows.md)を参照してください。
