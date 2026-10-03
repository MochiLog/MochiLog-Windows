# MochiLog Windows 0.1.7 Alpha

## 日本語

Windows が標準の転送ポートを予約している環境で、アプリが起動しても iPhone・iPad から接続できない問題を修正しました。利用可能なポートを自動選択して保存し、ペアリング QR と端末検出に正しいポートを掲載します。既存のペアリングは維持されます。

当日のログが揃った後は自動再走査を翌朝まで休止します。手動収集と転送待受は引き続き利用できます。

Windows 11 と iOS/iPadOS 27 向けのアルファ版です。初回の USB 信頼設定には Apple Devices または Apple 公式サイトのクラシック版 iTunes が必要です。解析ログの収集時は端末をロック解除してください。インストーラーは現在コード署名されていません。

## English

Fixed an issue where Windows reserved the default transfer port, leaving the app open but unreachable from an iPhone or iPad. The app now selects and remembers an available port, then advertises the actual port in pairing QR codes and local discovery. Existing pairings are preserved.

Automatic rescans pause until the next morning after today's required logs are stored. Manual collection and the transfer listener remain available.

This alpha supports Windows 11 and iOS/iPadOS 27. Initial USB trust requires Apple Devices or the classic iTunes installer from Apple's website. Keep the device unlocked while collecting analytics logs. The installer is not currently code signed.
