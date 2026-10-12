# MochiLog Windows 0.1.26 alpha

## 日本語

- 日付別デバッグログの交換を、通知された受信範囲までに制限します。元のログが増えても保存済み位置から再開し、旧版との互換性を維持します。
- スマホ側のデバッグログをどこまで受信できたか、日付・保存済みバイト数・今回の範囲の完了を記録します。重複断片を保存しなかった場合も追跡できます。
- 診断サービスへ接続する経路と、アドレスが手動指定か転送時の接続元かを記録します。転送元アドレスへの到達と、Appleの診断サービスへの到達を区別します。
- ツールがエラーを報告した場合の「終了コード0」という紛らわしい表示を8言語で改善しました。

## English

- Daily diagnostic history resumes within the advertised snapshot boundary, preserving saved offsets and compatibility with older peers when the source log grows.
- Diagnostic events show the received day, saved byte count, completion of the advertised range, and rejected duplicate chunks.
- Collection diagnostics identify the selected transport and distinguish a manually configured address from an authenticated transfer-peer address. Reaching the app does not imply that Apple's diagnostic service is reachable through the same address.
- Helper-reported errors no longer misleadingly appear as exit-code-zero failures; the message is available in all eight supported languages.
