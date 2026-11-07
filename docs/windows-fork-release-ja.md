# GitHub Actions で配布する Windows フォーク版

`daizu-007/Wino-Mail-Translate` の `main` への push で Release x64（Native AOT）をビルドし、自己署名した MSIX bundle を GitHub のプレリリースに添付します。タグは `windows-fork-<実行番号>-<コミット>` です。ZIP には `.msixbundle`、公開証明書 `WinoMailTranslate.cer`、`install-local.ps1`、SDK が出力した依存パッケージが含まれます。

パッケージ名は `Daizu007.WinoMailTranslate`、Publisher は `CN=Daizu007.WinoMailTranslate`、スタートメニューのメールアプリ名は `Wino Mail Translate` です。公式版と別のパッケージとして登録され、アプリデータも分かれます。両方をインストールした場合、`mailto:` や Wino アカウントの `wino:` コールバックを開くアプリは Windows の関連付けで決まります。

## 初回にリポジトリ管理者が行う作業

1. Windows の PowerShell で次を一度実行し、署名証明書を作成します。PFX はリポジトリの外に保存し、パスワードとともに安全にバックアップしてください。表示された拇印はインストール時の照合に使います。

   ```powershell
   $certificate = New-SelfSignedCertificate -Type Custom `
       -Subject 'CN=Daizu007.WinoMailTranslate' -FriendlyName 'Wino Mail Translate MSIX' `
       -KeyAlgorithm RSA -KeyLength 3072 -KeyUsage DigitalSignature -HashAlgorithm SHA256 `
       -KeyExportPolicy Exportable -CertStoreLocation Cert:\CurrentUser\My `
       -NotAfter (Get-Date).AddYears(3) `
       -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
   $password = Read-Host 'PFX のパスワード（空欄不可）' -AsSecureString
   $pfx = Join-Path $env:USERPROFILE 'WinoMailTranslate-signing.pfx'
   Export-PfxCertificate -Cert $certificate -FilePath $pfx -Password $password
   $certificate | Format-List Subject, Thumbprint, NotAfter
   ```

2. GitHub の **Settings → Secrets and variables → Actions** の **Repository secrets** に次の 2 件を登録します。PFX とパスワードはコミットや配布物に含めず、更新ビルドでも同じ証明書を使います。[Actions Secrets の公式手順](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets)

   | Secret 名 | 値 |
   | --- | --- |
   | `MSIX_SIGNING_PFX_BASE64` | PFX ファイル全体の Base64 文字列 |
   | `MSIX_SIGNING_PFX_PASSWORD` | PFX のエクスポート時に入力したパスワード |

   Base64 文字列は、上のコマンドを実行した PowerShell で次を実行するとクリップボードにコピーできます。Secret に貼り付けたらクリップボードを消去してください。

   ```powershell
   [Convert]::ToBase64String([IO.File]::ReadAllBytes($pfx)) | Set-Clipboard
   ```

3. フォークの **Actions** タブでワークフローが無効なら有効化し、この変更をコミットして `origin` の `main` に push します。初回 push のあとに Secrets を設定した場合は、失敗した run を再実行できます。公開にはワークフローの `GITHUB_TOKEN` を使います。

<a id="install"></a>

## インストールと更新

GitHub の **Releases** から `WinoMailTranslate_*_x64.zip` をダウンロードし、全ファイルを同じフォルダーに展開します。配布元に別経路で確認した公開証明書の拇印を使い、展開先で PowerShell 7.2 以降から実行します。

```powershell
pwsh -NoProfile -File .\install-local.ps1 -CertificatePath .\WinoMailTranslate.cer `
    -ExpectedThumbprint '<確認済みの40桁の拇印>'
```

初回は証明書を Windows の「信頼されたユーザー」に登録するため、管理者の承認が必要です。インストール先には Microsoft Edge WebView2 Runtime が必要です。[MSIX 署名証明書について](https://learn.microsoft.com/windows/msix/package/create-certificate-package-signing) · [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)

更新は新しい ZIP を展開して同じ手順で行います。MSIX のバージョンにはソースの `major.minor.build` と Actions の実行番号（`1`〜`65535`）を使います。ソース側の `major.minor.build` を下げると Windows は更新として受け付けません。証明書の期限前に更新計画を立ててください。インストーラーは署名証明書が異なるパッケージを自動で置き換えません。
