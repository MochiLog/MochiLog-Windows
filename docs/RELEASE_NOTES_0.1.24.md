# MochiLog Windows 0.1.24 alpha

## 日本語

Mac版で見つかったデバッグログ表示の重い処理をWindows版にも修正しました。保存済みログの読み込み・機能分類・行分割をバックグラウンドで行い、接続状態が更新されるたびにログ全文を読み直す処理を止めました。読み込み中の表示を追加し、日付・端末・機能を切り替えた後に古い結果が混ざらないようにしました。全文コピー・保存済みログ・サポート添付・既存ペアリングは維持します。

## English

Applied the support-log performance fix found on Mac to Windows. Archive reading, feature categorization, and line preparation now run in the background. Connection status updates no longer reread entire logs. Loading indicators show ongoing work, and stale results cannot replace a newer date, device, or category selection. Full-text copying, saved archives, support attachments, and existing pairing are preserved.
