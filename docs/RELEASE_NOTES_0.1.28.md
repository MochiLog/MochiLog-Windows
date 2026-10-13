# MochiLog Windows 0.1.28 alpha

## 日本語

診断情報の生成をアプリ内のC#処理へまとめました。アプリのメモリ使用量、端末ごとの接続候補、収集結果、直近の動作ログを定期的に保存し、サポートの書き出しにも含めます。診断データは件数・文字数・全体サイズを制限し、動作ログと同じ保存期間で整理します。ペアリングの鍵や現在のバッテリー値は保存しません。

端末通信ツールの実行管理も強化しました。実行時間と出力量を制限し、キャンセル・アプリ終了時には関連する子プロセスも回収します。既存のペアリングと未転送のログは維持します。

## English

Diagnostic reports are now generated inside the app in C#. The app periodically saves its process memory measurements, per-device connection candidates, collection results and recent activity, and includes a fresh report in support exports. Device counts, text lengths and total report size are bounded; snapshots use the same retention period as activity logs. Pairing keys and live battery values are excluded.

Device helper execution now has bounded runtime, output and memory, with owned child processes terminated on cancellation or app exit. Existing pairings and pending battery logs are preserved.
