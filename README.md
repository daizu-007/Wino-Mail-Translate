# Wino Mail Translate
カスタマイズ可能な翻訳機能を搭載した[Wino Mail](https://www.winomail.app/)の非公式フォーク

## 概要
[Wino Mail](https://www.winomail.app/)は美しいデザインと使いやすさを兼ね備えたオープンソースのメールクライアントです。  
このフォークでは、[Wino Mailに公式搭載されているクラウド翻訳機能](https://www.winomail.app/intelligence)から独立したカスタマイズ性の高い翻訳機能を追加しています。

Windows以外のOSでの使用は想定していません。

## 翻訳
翻訳方式は以下の4つから選択できます。

### 1. PLaMo 3 Translate
ローカルのLMStudioで動作するPLaMo 3 Translateを用いた翻訳方式です。外部にメール本文を送信することなく、プライベートに翻訳できます。  
このフォークを作成した最大の理由であり、プライバシーを保護しつつ自然な日本語に翻訳できます。

### 2. LLM翻訳
OpenAI APIと互換性のある任意のプロバイダのAPIを用いてLLMで翻訳を行います。
OllamaやLMStudioを用いたローカル翻訳も可能です。

### 3. Sarashina 3.2 Translate
Sarashinaの翻訳APIを用いた翻訳方式です。外部にデータ送信され、モデルの学習に利用されますが、PCスペックを問わず無料で高速に自然な日本語への翻訳を実現できます。

### 4. DeepL翻訳
定番の翻訳サービス、DeepLの公式APIを用います。動作確認はしていません。

## アカウント数制限
無料版Wino Mailはアカウント数が3つまでに制限されています。フォークではMicrosoft Storeでのアドオン購入を利用できないと考えられるため、アカウント数制限は削除されています。Wino Mailはコード改変によるアカウント数制限解除を禁止していませんが、このフォークの種目的ではないためアカウント数の制限解除が目的であれば公式アドオンの購入をおすすめします。

> If you are comfortable building software, you can build Wino from source and change the account limit. Buying an add-on is a way to support the project, not a requirement to use its source code.

https://www.winomail.app/pricing

## インストール
1. [release](https://github.com/daizu-007/Wino-Mail-Translate/releases)から「WinoMailTranslate_バージョン_x64.zip」をダウンロード
2. zipを解凍し、`pwsh -NoProfile -ExecutionPolicy Bypass -File .\install-local.ps1 -CertificatePath .\WinoMailTranslate.cer -ExpectedThumbprint 'AF81680204D210D836843DCC17D3862199EDB61C'`を実行
3. UACが表示されるので確認して「はい」をクリック

## 寄付
Wino Mail Translateはあまり労力を書けずに作られた概念実証的側面を持つ小規模なフォークです。継続的なメンテナンスを行うつもりもありません。もし少しでもこのプロジェクトを気に入ったかたは、フォーク元である[Wino Mail](https://www.winomail.app/)のアドオン購入を検討してみてください。

https://www.winomail.app/pricing

Wino Mailには2つのアドオンがあります。
1. 一度きりの支払いで永久に利用できる、アカウント数制限を解除する「Unlimited Accounts」
2. 月額課金で翻訳機能だけでなく、メールの分類や書き換え、ブリーフィングなどのAI機能を利用できる「Wino Intelligence」

いずれもMicrosoft Storeから購入できます。  
  
  
  
また、Wino Mailの開発者である[Burak Kaan Köse](https://github.com/bkaankose)氏のGitHub Sponsorsでスポンサーになることもできます。

https://github.com/sponsors/bkaankose

## AIポリシー
このフォークの変更は、このREADMEを除きほぼすべて生成AIによってVibe Codeで行われました。いかなる問題が生じても一切の責任を負いませんし、サポートも行えません。ただし、CodexのGPT-6.1 Solはフォーク主であるdaizu-007よりも優秀なコーディング能力を持っています。

## 変更内容
変更日: 2026年11月7日
変更者: daizu-007
変更内容:
- 独自の翻訳機能を追加
- アカウント数制限を削除
- ローカルでのビルド等実験用ファイルの追加
- READMEの書き直し

## クレジット
このプロジェクトは、GPL-3.0に基づく[Wino Mail](https://github.com/bkaankose/Wino-Mail)の非公式フォークです。  
オリジナル版を開発した[Burak Kaan Köse](https://github.com/bkaankose)氏をはじめ、[Wino Mail](https://www.winomail.app/)の開発に携わったすべての方々に感謝します。
