# MochiLog Windows 0.1.21 Alpha

## 日本語

診断ログを日付・機能別ファイルに分割しました。ファイル先頭には形式バージョン2、アプリ・ビルド情報、作成日時とタイムゾーンを記録します。行ごとには繰り返しません。旧ログも引き続き閲覧でき、旧バージョンのスマホとの暗号化された診断ログ交換・サポート添付に使う集約ファイルは受信済みの位置を保ったまま継続します。削除や保存期限の掃除は分割ファイルにも適用します。

収集、転送、暗号化、現在のバッテリー表示は従来どおり使用できます。Windows 11向けのアルファ版です。インストーラーは引き続きコード署名されていません。

PCのログと受信済みスマホのログを、日付と機能別に絞って閲覧・コピーできます。「すべて」は元の内容をそのまま表示し、旧形式・将来の未対応形式も隠しません。分類名は8言語に対応します。

## English

Diagnostics now use daily feature files with format-version-2 headers identifying the app/build, creation time and time zone, without repeating metadata per line. Legacy logs remain readable. The append-only combined stream preserves encrypted diagnostic exchange with older mobile versions, support attachments and existing receive offsets. Retention and user deletion also remove the split files.

Existing collection, transfer, encryption and current battery functionality remain available. This is a Windows 11 alpha. The installer remains unsigned.

The viewer can filter and copy local PC and received phone logs by date and feature. All preserves the original text; legacy and unsupported future formats remain visible. Category labels support all eight languages.
