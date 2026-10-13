# MochiLog Windows 0.1.29 Alpha

## 日本語

設定に「常駐時に画面のメモリを解放」を追加しました。初期設定はオンです。トレイに収納すると画面とナビゲーション履歴を破棄し、再表示時に直前のページを作り直します。入力途中のフォームはリセットされます。オフにすると画面を保持します。ログ収集・転送・ペアリングは画面の寿命から独立して続きます。

破棄時にページのイベント購読と古いダイアログを解除し、画面を参照し続けないようにしました。画面解放・再作成とメモリ測定結果を動作ログへ記録します。設定と説明は8言語に対応しています。

0.1.28で追加したサイズ制限付きのネイティブ診断、実行時間・出力・メモリを制限する通信ツールの管理も含みます。既存のペアリングと未転送ログは維持されます。

## English

Settings now includes **Release screen memory in the background**, enabled by default. Sending the app to the tray discards its visual tree and navigation history; reopening rebuilds the last page. Unsubmitted forms are reset. Turn the setting off to retain the screen. Collection, transfers and pairing keep their independent background lifetime.

Page subscriptions and old dialogs are detached when the screen is released. Screen release/recreation and memory measurements are recorded in activity logs. The setting and explanation support all eight app languages.

This release includes the bounded native diagnostics and supervised device helpers from 0.1.28. Existing pairings and pending battery logs are preserved.
