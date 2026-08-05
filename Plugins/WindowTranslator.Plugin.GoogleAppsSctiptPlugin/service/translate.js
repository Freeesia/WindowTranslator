/**
 * Apps Script API (scripts.run) から呼び出す翻訳関数
 * @param {string} sourceLanguage - 翻訳元言語コード
 * @param {string} targetLanguage - 翻訳先言語コード
 * @param {string[]} texts - 翻訳対象の文字列配列
 * @returns {string[]} 翻訳結果の文字列配列
 */
function translate(sourceLanguage, targetLanguage, texts) {
  var translatedTexts = [];
  for (var i = 0; i < texts.length; i++) {
    var translatedText = LanguageApp.translate(texts[i], sourceLanguage, targetLanguage);
    translatedTexts.push(translatedText);
  }
  return translatedTexts;
}
