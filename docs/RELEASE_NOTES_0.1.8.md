# MochiLog Windows 0.1.8 Alpha

## 日本語

解析ログの取得を端末側の制約で後から再試行する場合、その状態を収集失敗と誤表示しないよう修正しました。状態表示とサポート用診断情報で、再試行待ちと実際のエラーを区別できます。

更新インストーラーの完了後にアプリを再起動するよう修正しました。更新中にアプリが早く再起動してファイルが使用中になり、インストールが失敗する問題を防ぎます。

Windows 11 と iOS/iPadOS 27 向けのアルファ版です。初回の USB 信頼設定には Apple Devices または Apple 公式サイトのクラシック版 iTunes が必要です。解析ログの収集時は端末をロック解除してください。インストーラーは現在コード署名されていません。

## English

Deferred analytics-log collection is no longer reported as a collection failure. The app and support diagnostics now distinguish a pending retry from an actual error.

The app now restarts after the update installer finishes. This prevents an early restart from locking files and causing the installation to fail.

This alpha supports Windows 11 and iOS/iPadOS 27. Initial USB trust requires Apple Devices or the classic iTunes installer from Apple's website. Keep the device unlocked while collecting analytics logs. The installer is not currently code signed.
