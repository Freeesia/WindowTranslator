# WindowTranslator Sarashina3 Translator Plugin

## ja

[WindowTranslator](https://github.com/Freeesia/WindowTranslator)で、Sarashina3のTranslate APIを利用する翻訳プラグインです。

- `POST https://api.sarashina.ai/v1/translate` を使った翻訳
- 翻訳元・翻訳先に日本語または英語を指定
- Sarashina APIキーをBearer認証で送信
- 翻訳にはインターネット接続が必要です。翻訳対象のテキストはSarashina APIに送信されます。

Sarashina APIの利用登録とAPIキーの取得については、[Sarashina](https://sarashina.ai/)を確認してください。API仕様は[公式Translate APIリファレンス](https://docs.sarashina.ai/api-reference/translate)を参照してください。

## en

A [WindowTranslator](https://github.com/Freeesia/WindowTranslator) translation plugin that uses the Sarashina3 Translate API.

- Uses `POST https://api.sarashina.ai/v1/translate`
- Supports Japanese and English as source and target languages
- Sends the Sarashina API key as a bearer token in the Authorization header
- An internet connection is required. Text to translate is sent to the Sarashina API.

See [Sarashina](https://sarashina.ai/) to register and obtain an API key. Refer to the [official Translate API reference](https://docs.sarashina.ai/api-reference/translate) for API details.
