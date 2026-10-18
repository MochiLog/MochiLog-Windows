# MochiLog Windows 0.1.33 Alpha

## 日本語

- ソースコード全体の整形を反映したメンテナンス版です。読みやすいコードを維持するため、開発用の整形設定と再実行用コマンドを追加しました。
- ビルド時のコンパイル並列数を、CIのCPU数とメモリ容量に合わせて選択するよう改善しました。初回ビルドも対象で、キャッシュなしの計測と工程別の時間記録に対応しました。
- 利用者によるPython環境の準備は引き続き不要です。既存のペアリング、保管庫、設定、暗号化・重複確認の仕組みを引き継ぎます。
- Windows 11向けのアルファ版です。インストーラーには現時点でコード署名を付けていません。

## English

- Maintenance release incorporating repository-wide source formatting, with repeatable formatting commands and configuration for contributors.
- Compiler parallelism now follows the CI runner's CPU and memory budget, including first builds. Cache-free measurement and per-stage timing reports are available.
- No user-installed Python environment is required. Existing pairings, stored reports, settings, encryption and duplicate-preflight behavior are retained.
- This is an alpha release for Windows 11. The installer is not currently code-signed.
