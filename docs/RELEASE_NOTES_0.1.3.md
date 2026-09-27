# MochiLog Windows 0.1.3 Alpha

## 日本語

Windows 11向けの初期アルファ版です。PCがロック解除中のiPhone・iPadから解析ログを収集して一時保存し、MochiLogを開いた端末へ暗号化して転送します。解析と記録は端末側で行います。iOS/iPadOS 27が必要です。

- 複数の端末が見つかった場合に、ペアリングする端末を選べます。端末一覧には機種、USB接続・信頼・無線の状態をアイコンと色付きの表示で示します。
- USBの信頼設定が未完了の端末では、その設定を先に案内します。機種を確認できないままQRを作らないようにし、QR表示時のクラッシュも修正しました。
- アプリに収集ツールと必要なPython依存を同梱し、スタートアップ起動、タスクトレイ、診断ログ、サポート、個別のペアリング解除、ライセンス表示に対応しました。インストーラーはアンインストーラーも登録します。
- 端末が新しい解析ログを提供できる状態ならPCで収集し、転送後は両側の確認応答を使って重複受信を防ぎます。

**初回設定:** Windows 11と、Apple DevicesまたはApple公式サイト配布の従来版iTunesが必要です。Microsoft Store版iTunesは使用しないでください。端末をロック解除して一度USBで接続し、「このコンピュータを信頼」を承認します。その後、PCで対象端末を選び、QRを端末のMochiLogで読み取ってください。

**アルファ版の制限:** 新しい端末内ログの収集には、端末のロック解除とAppleの診断サービスへ接続できるローカル無線環境が必要です。Tailscaleのモバイル通信経由では、PCが収集済みのログの転送だけが可能です。環境によって接続や収集が失敗する場合があります。アプリ内のデバッグログとサポート機能をご利用ください。

## English

This early alpha for Windows 11 collects analytics logs from an unlocked iPhone or iPad, queues them temporarily, and transfers them over an encrypted connection when MochiLog opens on the device. Parsing and recording stay on the mobile device. iOS/iPadOS 27 is required.

- Select the intended device when multiple devices are found. The list shows the model and USB connection, trust, or wireless state with icons and color-coded badges.
- Complete USB Trust before pairing an untrusted device. The app no longer creates a QR code when the model is unknown, and fixes a crash while displaying the QR code.
- Bundles the collector and its Python dependencies. Includes launch at login, a system tray icon, diagnostics, support, individual pairing removal, license notices, and an uninstall entry.
- Collects eligible new analytics logs, then uses acknowledgements on both sides of the transfer to prevent duplicate imports.

**First-time setup:** Install Windows 11 and either Apple Devices or the classic iTunes installer from Apple's website. Do not use the Microsoft Store edition of iTunes. Unlock the device, connect it once by USB, and approve “Trust This Computer.” Select that device in the PC app, then scan its QR code in MochiLog on the device.

**Alpha limits:** Collecting new on-device logs requires an unlocked device and a local wireless route to Apple's diagnostic service. Tailscale over cellular can transfer files the PC already collected, but cannot collect new system analytics files from the device. Connection and collection can fail on some networks; use the in-app debug log and support flow to report problems.
