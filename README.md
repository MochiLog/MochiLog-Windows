# MochiLog Windows

**PCにログを集めて、iPhone・iPadでバッテリーの変化を記録。**

MochiLog Windowsは、iPhone・iPadのバッテリー解析ログを収集し、スマホ版MochiLogへ暗号化して転送するコンパニオンアプリです。Apple Watchのログがペアリング先のiPhoneに保存されている場合は、それも収集できます。

[最新版をダウンロード](https://github.com/MochiLog/MochiLog-Windows/releases/latest) · [利用ガイド](docs/USER_GUIDE.md) · [スマホ版MochiLog](https://github.com/MochiLog/MochiLog) · [公式サイト](https://mochilog.ryuya-dev.net/)

## 必要なもの

- Windows 11のPC
- iOS / iPadOS 27以降の端末と、スマホ版MochiLog
- 初回の信頼設定に使う、データ通信対応のUSBケーブル
- **Apple DevicesまたはApple公式サイト配布のクラシック版iTunesのどちらか一方**

Pythonや.NETを別途インストールする必要はありません。Apple側の接続ソフトは別途必要です。Microsoft Store版iTunesとiCloud for Windowsは、この接続ソフトの代わりにはなりません。導入先と詳しい条件は[利用ガイド](docs/USER_GUIDE.md)をご確認ください。

## はじめ方

1. [最新版のインストーラー](https://github.com/MochiLog/MochiLog-Windows/releases/latest)をダウンロードしてインストールします。インストール時の注意点は利用ガイドで案内しています。
2. Apple Devicesまたはクラシック版iTunesを開き、端末をUSB接続してロック解除します。「このコンピュータを信頼」を許可し、Apple側のアプリに端末が表示されることを確認します。
3. MochiLog Windowsの「端末」で対象を選び、「USBの信頼設定」を実行します。完了まで端末のロックを解除したまま、進捗表示を確認してください。
4. 設定後にUSBケーブルを外し、同じWi-Fiで無線接続を確認します。Windowsに表示されたQRを、端末の「MochiLog → 設定 → 自動ログ収集 → PC連携」で読み取り、確認コードを入力します。
5. Windowsアプリを起動したままにします。端末でMochiLogを開くと、収集済みログを受信して解析・記録します。

接続済み端末の選択や、すでに信頼設定済みの端末での操作は、アプリ内の案内に従ってください。アンインストールはWindowsの「設定 → アプリ」から行えます。

## 日々の使い方

- **ログの収集：** 端末のロック解除中に収集します。必要なときは「今すぐログを収集」を使えます。
- **ログの受信：** スマホ版を開いて受信します。手動で「今すぐ受信」を押すこともできます。
- **現在のバッテリー：** PCの専用画面や、スマホ版で有効にした現在値タブから確認できます。現在値は履歴には保存しません。
- **電池ログの管理：** 転送待ち・送信済みのファイルを確認し、書き出しや再送ができます。送信後の保管期間と容量上限も設定できます。
- **常駐と更新：** サインイン時の起動、タスクトレイでの常駐、自動更新確認を設定できます。

外出先から収集済みログを受け取る場合は、任意のTailscale接続とスマホ側のモバイル通信許可を利用できます。新しいログの収集には、ローカルの無線接続と端末のロック解除が必要です。

## 困ったときは

Apple Devicesに端末が出ない場合は、[利用ガイドのトラブル対応](docs/USER_GUIDE.md)をご確認ください。エクスプローラーに表示されるだけでは、信頼設定が完了しているとは限りません。アプリ内のサポート画面から、発生日を指定して診断情報を添えた問い合わせも作成できます。

[サポート](https://mochilog.ryuya-dev.net/support?lang=ja) · [プライバシーポリシー](https://mochilog.ryuya-dev.net/privacy?lang=ja) · [利用規約](https://mochilog.ryuya-dev.net/terms?lang=ja)

## English

MochiLog Windows collects iPhone and iPad battery analytics files and transfers them over an encrypted connection to the mobile MochiLog app. Eligible Apple Watch files stored on the paired iPhone are included. The mobile app analyzes the files and saves the records.

You need Windows 11 and an iPhone or iPad running iOS/iPadOS 27+. Initial trust setup also requires a USB data cable and **either Apple Devices or classic iTunes from Apple's website**. Microsoft Store iTunes and iCloud for Windows do not replace this setup. The MochiLog installer includes its other runtimes, so Python and .NET do not need separate installation.

Download the latest [installer](https://github.com/MochiLog/MochiLog-Windows/releases/latest), approve **Trust This Computer** on the unlocked device, and follow **Devices** in MochiLog Windows. After wireless access is confirmed, scan the QR in **MochiLog → Settings → Automatic Log Collection → PC Transfer** and enter the confirmation code. Leave the PC app running; open the mobile app to receive collected logs.

Battery Logs, Live Battery, sign-in launch, tray mode and update checks are available in the app. Use Windows Settings to uninstall. See the [Japanese and English user guide](docs/USER_GUIDE.md) for full setup and troubleshooting.

---

[開発者向け文書 / Developer documentation](docs/DEVELOPMENT.md) · [ライセンス / License](LICENSE)
