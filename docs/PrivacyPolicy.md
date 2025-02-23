---
title: プライバシーポリシー
description: WindowTranslator プライバシーポリシー
---

本プライバシーポリシーは、「WindowTranslator」（以下「本アプリ」といいます）の利用に際し、ユーザーの個人情報およびデータの取り扱いについて定めたものです。

## はじめに
本アプリはオープンソースとしてGitHub（ https://github.com/Freeesia/WindowTranslator ）にて公開されており、アプリ自体にはユーザーの個人情報や利用状況を収集する仕組みはありません。また、エラーログやクラッシュレポート等の収集も行っておりません。

## 個人情報の収集について
### アプリ自体による情報収集
本アプリは、ユーザーの入力情報、操作履歴、端末情報などの個人情報を直接収集しません。

### エラーログ・クラッシュレポート
エラーログやクラッシュレポート等、ユーザーに関するデータの自動収集は実施しておりません。

## Googleユーザーデータに関する取り扱い
本アプリはGoogle Apps Scriptサービスへアクセスする際のみ、以下の通りGoogleユーザーデータに関連する権限を取得します。

### アクセスするデータ
本アプリは、Google Apps Scriptを利用するために必要な最小限の認証トークンのみを取得します。  
ユーザーの個人情報自体は一切利用しません。

### データの利用方法
取得した認証情報は、アプリ内でGoogle Apps Scriptを実行するためにのみ使用されます。  
この認証は、ユーザーがGoogleサービスを利用するための認証目的に限定され、その他の目的では使用されません。

### データの共有・転送
認証情報やその他のGoogleユーザーデータは、本アプリ提供者によって一切収集、保存、共有、または第三者への転送は行われません。  
すべての認証情報はユーザーのPC上に保存され、Googleのサービス利用のためにのみ利用されます。

### データ保護の仕組み
認証情報はユーザーのPC上に安全に保存され、アプリ提供者はこれらの情報へのアクセス権限を一切有しておりません。  
したがって、データ保護に関する追加のセキュリティ対策は、ユーザー自身のPCのセキュリティ管理に依存します。

### データの保持・削除
認証情報（Googleの認証トークン等）は、ユーザーのPC上にのみ保存されます。  
認証情報の削除は、ユーザーがPC上の `%APPDATA%\StudioFreesia\WindowTranslator\GoogleAppsScriptPlugin` フォルダを削除することで完結し、アプリ提供者側で管理・削除することはありません。

## 翻訳エンジンおよび第三者ライブラリの利用について
本アプリは、Google翻訳、DeepLなど、複数の翻訳エンジンを利用可能な設計となっております。これらの翻訳エンジンおよび連携ライブラリは、各サービスの独自のプライバシーポリシーに基づいてユーザー情報や利用状況の収集を行う可能性があります。
ユーザーの皆様には、本アプリ利用に際してご利用の翻訳エンジン各社のプライバシーポリシーも併せてご確認いただくことを強く推奨いたします。

## お問い合わせ先
本プライバシーポリシーに関するご質問・ご意見は、下記GitHubアカウントまでご連絡ください。

GitHubアカウント: [Freeesia](https://github.com/Freeesia)

## 本プライバシーポリシーの変更について
法令の改正や本アプリの機能変更等に伴い、本プライバシーポリシーを予告なく変更する場合があります。変更が行われた場合は、本アプリ内またはGitHubリポジトリ等を通じて速やかに周知いたします。

以上
