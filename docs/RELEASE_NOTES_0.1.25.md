# MochiLog Windows 0.1.25 alpha

## 日本語

Mac版で見つかった収集の修正をWindows版にも反映しました。新しいログを先に取得し、読めないファイルはファイルごとに再試行を待ちます。当日のファイルを早めに再確認し、失敗したログを転送済み・対象外にしないようにしています。

現在のバッテリー情報を一時的に取得できない場合に、内部的な「キーが見つからない」エラーが出る問題を修正しました。取得不能の応答を正しく扱い、以前取得できた値がある場合は取得時刻とともに保持します。

当日ログが端末内に見つかった時点、取得失敗の理由、次回再試行時刻をデバッグログに記録します。既存のペアリング・暗号化・未転送ログの保護は維持しています。

## English

Applied the collection fixes found on Mac to Windows. Newer reports are collected first, and unreadable files receive individual retry times. Current-day reports are checked again sooner; failed reads are never treated as delivered or permanently excluded.

Fixed an internal "key was not present in the dictionary" error when the current battery service temporarily returns an unavailable response. Such responses are now handled explicitly, while previously acquired values retain their acquisition time.

Debug logs record when current-day reports become visible, per-file failure reasons and the next retry time. Existing pairing, encryption and protection of unsent reports are preserved.
