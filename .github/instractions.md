# 指示の理解方針

## 誤解の事例分析

### PR #606 での誤解

**ユーザーの指示（PRレビューコメント）：**
> `SettingsPropertyGrid`への機能追加なので`SettingsPropertyGrid`でほぼ完結するようにして。
> `IModelHistoryStore`をコントロールに渡す処理だけアプリ層の実装として許容する。

**一言一句の分析：**

| 句 | AIの誤った解釈 | 正しい解釈 |
|---|---|---|
| `SettingsPropertyGrid`でほぼ完結するようにして | `AllSettingsDialog.xaml.cs` に `IModelHistoryStore` を注入し、`operator` に渡す処理をView層に置いた | 履歴の読み込み・保存・`EditableItemsSourceAttribute` の検出をすべて `SettingsPropertyGrid` 内部で完結させる |
| `IModelHistoryStore`をコントロールに渡す処理だけアプリ層の実装として許容する | 「コントロールに渡す」= `operator.HistoryStore = modelHistoryStore` をView層で行う（部分的に正しかった） | `SettingsPropertyGrid` 自体に `IModelHistoryStore HistoryStore` DPを持たせ、アプリ層はそこに渡すだけ。読み取り・保存ロジックはすべてコントロール内 |

**誤解の原因：**  
「`SettingsPropertyGrid`でほぼ完結する」という指示を、`SettingsPropertyGridOperator` まで含めた広い範囲での対応と解釈した。  
また指示の取得漏れがあった：`get_comments`（一般コメント）のみ取得し、`get_review_comments`（レビューコメント）を取得しなかったため、この指示自体を見逃す場面があった。

---

## 理解の方針

### 1. 指示に含まれない要素は変更しない

- ユーザーが明示的に変更を求めていないコンポーネント・インターフェース・アーキテクチャには手を加えない
- 「A を削除して B で管理する」→ A の削除と B への移動のみ行う。それ以外の要素（A に関連する属性の設計、C の構造など）は変更しない

### 2. 「管理する場所を変える」は「実装方法を変える」ではない

- 「X側で管理する」=「X（クラス/モジュール）にプロパティ/ロジックを移動する」
- 既存の仕組み（属性の引数、インターフェースの設計など）自体を変えることではない
- 移動先での実装方法を指示なしに変更するのは過剰な解釈

### 3. 指示が曖昧な場合は最小変更を選ぶ

- 複数の解釈が可能な場合、最も小さい変更範囲（字義通り）を選択する
- 「より良い実装」への自発的な変更は禁止
- `copilot-instructions.md` の「指定された指示には必ず従う」を厳守する

### 5. PRへの指示はレビューコメントも必ず確認する

- PRに対して指示が出ている場合、`get_comments`（一般コメント）だけでなく `get_review_comments`（インラインレビューコメント）も取得する
- レビューコメントには行単位の具体的な指示が含まれることが多く、見逃すと誤った分析・実装につながる

