﻿//------------------------------------------------------------------------------
// <auto-generated>
//     このコードはツールによって生成されました。
//     ランタイム バージョン:4.0.30319.42000
//
//     このファイルへの変更は、以下の状況下で不正な動作の原因になったり、
//     コードが再生成されるときに損失したりします。
// </auto-generated>
//------------------------------------------------------------------------------

namespace WindowTranslator.Plugin.LLMPlugin.Properties {
    using System;
    
    
    /// <summary>
    ///   ローカライズされた文字列などを検索するための、厳密に型指定されたリソース クラスです。
    /// </summary>
    // このクラスは StronglyTypedResourceBuilder クラスが ResGen
    // または Visual Studio のようなツールを使用して自動生成されました。
    // メンバーを追加または削除するには、.ResX ファイルを編集して、/str オプションと共に
    // ResGen を実行し直すか、または VS プロジェクトをビルドし直します。
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   このクラスで使用されているキャッシュされた ResourceManager インスタンスを返します。
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("WindowTranslator.Plugin.LLMPlugin.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   すべてについて、現在のスレッドの CurrentUICulture プロパティをオーバーライドします
        ///   現在のスレッドの CurrentUICulture プロパティをオーバーライドします。
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   APIキー に類似しているローカライズされた文字列を検索します。
        /// </summary>
        internal static string ApiKey {
            get {
                return ResourceManager.GetString("ApiKey", resourceCulture);
            }
        }
        
        /// <summary>
        ///   認識補正 に類似しているローカライズされた文字列を検索します。
        /// </summary>
        internal static string CorrectMode {
            get {
                return ResourceManager.GetString("CorrectMode", resourceCulture);
            }
        }
        
        /// <summary>
        ///   画像認識による補正 に類似しているローカライズされた文字列を検索します。
        /// </summary>
        internal static string CorrectMode_Image {
            get {
                return ResourceManager.GetString("CorrectMode_Image", resourceCulture);
            }
        }
        
        /// <summary>
        ///   補正なし に類似しているローカライズされた文字列を検索します。
        /// </summary>
        internal static string CorrectMode_None {
            get {
                return ResourceManager.GetString("CorrectMode_None", resourceCulture);
            }
        }
        
        /// <summary>
        ///   テキストのみの補正 に類似しているローカライズされた文字列を検索します。
        /// </summary>
        internal static string CorrectMode_Text {
            get {
                return ResourceManager.GetString("CorrectMode_Text", resourceCulture);
            }
        }
        
        /// <summary>
        ///   補正サンプル に類似しているローカライズされた文字列を検索します。
        /// </summary>
        internal static string CorrectSample {
            get {
                return ResourceManager.GetString("CorrectSample", resourceCulture);
            }
        }
        
        /// <summary>
        ///   ローカルLLM接続先 に類似しているローカライズされた文字列を検索します。
        /// </summary>
        internal static string Endpoint {
            get {
                return ResourceManager.GetString("Endpoint", resourceCulture);
            }
        }
        
        /// <summary>
        ///   OpenAIを利用する場合は空 に類似しているローカライズされた文字列を検索します。
        /// </summary>
        internal static string Endpoint_Desc {
            get {
                return ResourceManager.GetString("Endpoint_Desc", resourceCulture);
            }
        }
        
        /// <summary>
        ///   用語集パス に類似しているローカライズされた文字列を検索します。
        /// </summary>
        internal static string GlossaryPath {
            get {
                return ResourceManager.GetString("GlossaryPath", resourceCulture);
            }
        }
        
        /// <summary>
        ///   ChatGPT API設定 に類似しているローカライズされた文字列を検索します。
        /// </summary>
        internal static string LLMOptions {
            get {
                return ResourceManager.GetString("LLMOptions", resourceCulture);
            }
        }
        
        /// <summary>
        ///   ChatGPT API翻訳 に類似しているローカライズされた文字列を検索します。
        /// </summary>
        internal static string LLMTranslator {
            get {
                return ResourceManager.GetString("LLMTranslator", resourceCulture);
            }
        }
        
        /// <summary>
        ///   使用するモデル に類似しているローカライズされた文字列を検索します。
        /// </summary>
        internal static string Model {
            get {
                return ResourceManager.GetString("Model", resourceCulture);
            }
        }
        
        /// <summary>
        ///   翻訳時に利用する文脈情報 に類似しているローカライズされた文字列を検索します。
        /// </summary>
        internal static string TranslateContext {
            get {
                return ResourceManager.GetString("TranslateContext", resourceCulture);
            }
        }
        
        /// <summary>
        ///   補正が完了してから翻訳を行う に類似しているローカライズされた文字列を検索します。
        /// </summary>
        internal static string WaitCorrect {
            get {
                return ResourceManager.GetString("WaitCorrect", resourceCulture);
            }
        }
    }
}
