# OrcaRouter 翻訳プラグイン

OrcaRouter の OpenAI 互換 API を使い、自動選択または指定したモデルで翻訳します。

1. プラグインストアからインストールし、WindowTranslator を再起動します。
2. 対象ごとの設定で「OrcaRouter翻訳」を選択します。
3. 「OrcaRouter設定」の「サインイン / サインアウト」を押し、ブラウザーで認証を許可します。
4. モデルを選択して、設定を保存・適用します。既定値は `orcarouter/auto` です。

API キーや接続先の入力は不要です。認証中にボタンを再度押すとキャンセルでき、5 分でタイムアウトします。
サインアウトは同じボタンを押してから設定を保存・適用してください。設定 JSON の API キーが削除されます。OrcaRouter 側のキーを無効にする場合は Authorized Apps から取り消してください。
認証情報とモデル選択は既存プラグインと同じく対象ごとの `PluginParams` に保存されます。API キーは設定 UI では非表示ですが、設定 JSON には保存されます。

モデルの料金はモデル一覧 API の取得値を使用し、入力 / 出力の順で 100 万トークンあたりの USD を表示します。
候補を開くと一覧を再取得します。通信に失敗しても、自動選択と保存済みのモデル ID は維持されます。
「翻訳時に利用する文脈情報」や、ヘッダーなしの原文・訳文 2 列の CSV 用語集も使用できます。

## 実装メモ

- 認証: [OrcaRouter PKCE 仕様](https://docs.orcarouter.ai/getting-started/sign-in-with-orcarouter) に従い、`Duende.IdentityModel` で Discovery を取得します。S256・ランダムな state・loopback callback を使用します。
- Discovery が現状返す `http://www.orcarouter.ai/...` は、正規ホストと標準ポートを検証して HTTPS に補正します。認証・キー交換とも平文 HTTP にはアクセスしません。
- 認可 URL へ課題 #692 で指定された紹介コードを付与します。`app_id` は指定しません。
- 翻訳には OpenAI .NET SDK を使い、Structured Output、assistant prefill、stop に依存しません。JSON の解析または出力件数の検証に失敗した場合は最大 5 回試行します。
- 本体の `DynamicItemsSource` 対応を含むバージョンが必要です。
