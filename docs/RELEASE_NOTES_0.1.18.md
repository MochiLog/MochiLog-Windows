# 日本語

- 端末内取得へ既存のOSペアリングを引き継ぐ際、Windowsのコンピューター名の大文字・小文字の変換により認証に失敗する問題を修正しました。信頼設定を行ったときのホスト名を正確に使用します。
- 既存のMochiLogペアリング、記録、保存したログは引き継がれます。
- 更新後のWindows実機から認証情報を引き継ぎ、iPadのLocalDevVPN経由のログ取得と既存記録の重複防止を確認しました。

旧版から端末内取得へ認証情報を取り込み済みの場合は、更新後に「既存ペアリングを利用」を押して取り直してください。QRやUSBの信頼設定のやり直しは不要です。

# English

- Fixed authentication failures when reusing an existing OS pairing for on-device collection. The hostname now matches the original pairing identity instead of the normalized Windows machine name.
- Existing MochiLog pairings, records, and stored logs are retained.
- Verified on-device log acquisition through LocalDevVPN on an iPad using credentials reused from the updated Windows app, including duplicate prevention for existing records.

If credentials were already imported for on-device collection using an older companion, reuse the existing pairing again after this update. Do not delete your QR pairing or reset USB trust.
