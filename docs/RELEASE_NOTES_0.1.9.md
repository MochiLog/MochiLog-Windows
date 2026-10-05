# MochiLog Windows 0.1.9 Alpha

## 日本語

iPhone・iPadへ送信済みの電池ログをPC側から削除しても、取得済みの日付を保持します。必要なiPhone本体とApple Watchのログが揃った日は、自動収集を翌日まで休止します。手動収集はいつでも使えます。

電池ログではない短い解析ファイルの再取得が続く問題も修正しました。内容が複数回変わらないことを確認してから対象外にします。

Windows 11とiOS/iPadOS 27向けのアルファ版です。初回のUSB信頼設定にはApple DevicesまたはApple公式サイトのクラシック版iTunesが必要です。解析ログの収集時は端末をロック解除してください。インストーラーは現在コード署名されていません。

## English

Windows now remembers confirmed daily logs after their raw files are delivered and removed. Automatic collection pauses until the next day once the required iPhone and Apple Watch logs are available. Manual collection remains available.

The collector also stops repeatedly downloading short non-battery analytics files after confirming unchanged content across multiple attempts.

This alpha supports Windows 11 and iOS/iPadOS 27. Initial USB trust requires Apple Devices or the classic iTunes installer from Apple's website. Keep the device unlocked while collecting analytics logs. The installer is not currently code signed.
