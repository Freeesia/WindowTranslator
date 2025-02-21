function doPost(e) {
  // リクエストボディをパースして文字列配列を取得
  var requestBody = JSON.parse(e.postData.contents);
  var texts = requestBody.texts;
  var sourceLanguage = requestBody.sourceLanguage
  var targetLanguage = requestBody.targetLanguage;

  // 翻訳結果を格納する配列
  var translatedTexts = [];

  // 各文字列を翻訳
  for (var i = 0; i < texts.length; i++) {
    var translatedText = LanguageApp.translate(texts[i], sourceLanguage, targetLanguage);
    translatedTexts.push(translatedText);
  }

  // 翻訳結果をjsonとして返す
  var jsonResponse = JSON.stringify(translatedTexts);
  return ContentService.createTextOutput(jsonResponse)
    .setMimeType(ContentService.MimeType.JSON);
}
