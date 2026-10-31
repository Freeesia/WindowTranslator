# WindowTranslator Sarashina3 Translator Plugin

## ja

[WindowTranslator](https://github.com/Freeesia/WindowTranslator)で、Sarashina3のTranslate APIを利用する翻訳プラグインです。

- `POST https://api.sarashina.ai/v1/translate` を使った翻訳
- 翻訳元・翻訳先が日本語と英語の場合は専用のTranslate APIを使用
- それ以外の言語の組み合わせでは、Chat Completions API (`sarashina3-mini`) にフォールバック
- Sarashina APIキーをBearer認証で送信
- 翻訳にはインターネット接続が必要です。翻訳対象のテキストはSarashina APIに送信されます。
- レート制限（Translate APIとChat Completions APIの`sarashina3-mini`は1分あたり6リクエスト）に達した場合は、`Retry-After`に従って待機してから再試行します。そのため、テキストが多いと翻訳に時間がかかります。
- Sarashinaサービス自体が無償ベータ版として提供されています。提供内容や提供条件、レート制限は変更または終了される可能性があります。
- 無償プランでは、送信したテキストがモデルの学習などに利用される場合があります。個人情報や機密情報を含むウィンドウでは使用しないでください。利用には[Sarashinaサービス利用規約](https://sarashina.ai/terms)への同意が必要です。個人情報の取り扱いは[SB Intuitionsの個人情報保護方針](https://www.sbintuitions.co.jp/privacy-policy/)を参照してください。

Sarashina APIの利用登録とAPIキーの取得については、[Sarashina](https://sarashina.ai/)を確認してください。API仕様は[公式Translate APIリファレンス](https://docs.sarashina.ai/api-reference/translate)を参照してください。

## en

A [WindowTranslator](https://github.com/Freeesia/WindowTranslator) translation plugin that uses the Sarashina3 Translate API.

- Uses `POST https://api.sarashina.ai/v1/translate`
- Uses the dedicated Translate API when both languages are Japanese/English
- Falls back to the Chat Completions API (`sarashina3-mini`) for any other language pair
- Sends the Sarashina API key as a bearer token in the Authorization header
- An internet connection is required. Text to translate is sent to the Sarashina API.
- When the rate limit is reached (6 requests per minute for the Translate API and for `sarashina3-mini` Chat Completions), the plugin waits for the `Retry-After` period and retries. Translating many texts can therefore be slow.
- The Sarashina service itself is currently offered as a free beta. Features, terms, and rate limits may change or the service may end.
- On the free plan, the text you send may be used for model training and similar purposes. Do not use it with windows that contain personal or confidential information. Use of the service requires agreeing to the [Sarashina Terms of Service](https://sarashina.ai/terms). For how personal data is handled, see the [SB Intuitions Privacy Policy](https://www.sbintuitions.co.jp/privacy-policy/).

See [Sarashina](https://sarashina.ai/) to register and obtain an API key. Refer to the [official Translate API reference](https://docs.sarashina.ai/api-reference/translate) for API details.
