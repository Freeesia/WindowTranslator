# TraduçãoMódulo

WindowTranslatorでは, 複数のTraduçãoMódulode選択して利用できます.  
各Móduloには特徴があり, 用途に応じて適切なMóduloを選択することで, より快適にTraduçãoを利用できます.

## Bergamot ![Padrão](https://img.shields.io/badge/Padrão-brightgreen)

オフラインで動作する機械TraduçãoMóduloです.

### Vantagens
- **Totalmente gratuito**: 料金は一切かかりません
- **Sem limite de tradução**: 何度でもTraduçãoできます
- **高速**: ローカルで処理されるためTradução rápidaです
- **プライバシー**: インターネット接続不要で, データが外部に送信されません
- **安定性**: ネットワークの影響を受けません

### デVantagens
- **Tradução精度**: クラウドベースのサービスと比較するとTradução精度が劣ります
- **メモリ使用量**: Tradução処理に一定のメモリを使用します
- **対応言語**: 一部の言語ペアのみサポートされています

### 推奨される利用シーン
- 無料で利用したい場合
- オフライン環境での利用
- プライバシーを重視する場合
- 高頻度でTraduçãoを行う場合

---

## GoogleTradução

GoogleのTraduçãoサービスを利用したTraduçãoMóduloです.

### Vantagens
- **Totalmente gratuito**: APIキーなしで利用できます
- **多言語対応**: 多くの言語ペアに対応しています
- **簡単**: 特別な設定が不要です

### デVantagens
- **Tradução上限**: 1日あたりのTradução可能な文字数に制限があります
- **Tradução精度**: 他の有料サービスと比較すると精度が劣る場合があります
- **速度**: ネットワークの影響を受けます
- **安定性**: 利用制限により突然使えなくなる可能性があります

### 推奨される利用シーン
- 低頻度での利用
- すぐに使い始めたい場合
- 多様な言語ペアでTraduçãoしたい場合

---

## DeepL

高品質なTraduçãoで知られるDeepLのTraduçãoサービスを利用したMóduloです.

### Vantagens
- **高精度**: 自然で高品質なTraduçãoが得られます
- **無料枠が充実**: 月50万文字まで無料で利用できます（無料API）
- **高速**: Tradução処理が速いです
- **用語集対応**: 用語集を利用してTraduçãoの一貫性を保てます

### デVantagens
- **API登録必要**: DeepL APIの登録とAPIキーの設定が必要です
- **無料枠制限**: 無料枠を超えた場合, 有料プランへの移行が必要です
- **対応言語**: 対応言語がGoogleなどと比べて限定的です

### 推奨される利用シーン
- 高品質なTraduçãoを求める場合
- 中程度の頻度での利用

---

## Google AI (Gemini)

Googleの最新AI技術を活用したTraduçãoMóduloです.

### Vantagens
- **最高精度**: 文脈を理解した非常に高品質なTraduçãoが可能です
- **柔軟性**: プロンプトをカスタマイズしてTraduçãoスタイルを調整可能
- **用語集対応**: 用語集を利用してTraduçãoの一貫性を保てます

### デVantagens
- **APIキー必要**: Google AI StudiodeAPIキーの取得と設定が必要です
- **従量課金**: 利用量に応じた課金が発生します（ただし少額）
- **速度**: LLMベースのため, 他のMóduloより処理時間がかかります

### 推奨される利用シーン
- 最高品質のTraduçãoを求める場合
- カスタマイズされたTraduçãoスタイルが必要な場合
- 文脈を重視したTraduçãoが必要な場合

---

## ChatGPT API (OR LLM Local)

ChatGPT APIまたはLLM Localを利用したTraduçãoMóduloです.

### Vantagens
- **最高精度**: 大規模言語モデルによる高品質なTradução
- **柔軟性**: プロンプトをカスタマイズしてTraduçãoスタイルを調整可能
- **用語集対応**: 用語集を利用してTraduçãoの一貫性を保てます
- **LLM Local対応**: 自前のLLMサーバーも利用可能

### デVantagens
- **APIキー必要**: 各サービスのAPIキーの設定が必要です（LLM Localを除く）
- **従量課金**: 利用量に応じた課金が発生します（LLM Localを除く）
- **速度**: 処理時間が長めです
- **LLM Localの要件**: 自前のLLMを動かす場合, Requer PC de alto desempenhoです

### 推奨される利用シーン
- 最高品質のTraduçãoを求める場合
- カスタマイズされたTraduçãoスタイルが必要な場合
- プライバシーを重視しつつ高品質なTraduçãoを行いたい場合（LLM Local）

---

## PLaMo

日本語に特化したLLM Localを利用したTraduçãoMóduloです.

### Vantagens
- **日本語特化**: 日本語のTraduçãoに最適化されています
- **Totalmente gratuito**: オープンソースモデルで料金不要
- **プライバシー**: ローカルで動作するため, データが外部に送信されません
- **オフライン**: インターネット接続不要

### デVantagens
- **高スペック要件**: GPUを含む高性能なPCが必要です
- **メモリ使用量**: 大量のメモリが必要です（8GB以上推奨）
- **速度**: GPUなしの場合, 処理に時間がかかります

### 推奨される利用シーン
- 高性能なPCを所有している場合
- プライバシーを最重視する場合
- 日本語のTradução品質を重視する場合

---

## Móduloの選び方

| 目的                 | 推奨Módulo                            |
| -------------------- | ----------------------------------------- |
| すぐに使い始めたい   | **Bergamot** または **GoogleTradução**        |
| 最高品質のTraduçãoが必要 | **Google AI** または **ChatGPT API**      |
| コストを抑えたい     | **Bergamot** または **DeepL（無料枠内）** |
| プライバシー重視     | **Bergamot** または **PLaMo**             |
| 高頻度で使用         | **Bergamot** または **DeepL**             |
