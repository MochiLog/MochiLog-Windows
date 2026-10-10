# MochiLog Windows 0.1.21 Alpha

## 日本語

診断ログを日付・機能別ファイルに分割しました。ファイル先頭には形式バージョン2、アプリ・ビルド情報、作成日時とタイムゾーンを記録します。行ごとには繰り返しません。旧ログも引き続き閲覧でき、旧バージョンのスマホとの暗号化された診断ログ交換・サポート添付に使う集約ファイルは受信済みの位置を保ったまま継続します。削除や保存期限の掃除は分割ファイルにも適用します。

収集、転送、暗号化、現在のバッテリー表示は従来どおり使用できます。Windows 11向けのアルファ版です。インストーラーは引き続きコード署名されていません。

## English

Diagnostics now use daily feature files with format-version-2 headers identifying the app/build, creation time and time zone, without repeating metadata per line. Legacy logs remain readable. The append-only combined stream preserves encrypted diagnostic exchange with older mobile versions, support attachments and existing receive offsets. Retention and user deletion also remove the split files.

Existing collection, transfer, encryption and current battery functionality remain available. This is a Windows 11 alpha. The installer remains unsigned.
