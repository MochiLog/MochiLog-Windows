# MochiLog Windows 0.1.4 Alpha

## 日本語

USBの信頼設定を押した後にダイアログ表示が衝突し、アプリが終了する不具合を修正しました。結果やエラーは画面内に表示され、複数のダイアログが同時に開かないようにしています。

端末の接続状態、初回設定、ペアリング済み端末を大きな画面で見渡せるように配置し直しました。想定外の終了時には例外の種類・発生時刻・スタック情報をPC内に残し、利用者がサポート報告を作成する際に確認できます。

この機能はWindows 11とiOS/iPadOS 27向けのアルファ版です。初回のUSB信頼設定にはApple DevicesまたはApple公式サイトのクラシック版iTunesが必要です。解析ログの収集には端末のロック解除が必要です。

## English

Fixed a crash after selecting USB trust setup. The result and any error now appear inline, and dialogs are coordinated so they cannot open on top of one another.

The dashboard now uses the available desktop space to show connection status, initial setup, and paired devices more clearly. If the app exits unexpectedly, it retains the exception type, time, and stack locally for review in a user-initiated support report.

This alpha supports Windows 11 and iOS/iPadOS 27. Initial USB trust requires Apple Devices or the classic iTunes installer from Apple's website. The device must be unlocked to collect analytics logs.
