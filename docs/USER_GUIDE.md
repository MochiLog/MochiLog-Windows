# MochiLog Windows アルファ版 利用ガイド / User guide

## 日本語

### できることと必要なもの

MochiLog Windows は、iPhone・iPad の解析ログを PC に一時保管し、端末で MochiLog を開いたときに暗号化して渡します。iPhone に保存された Apple Watch のログも対象です。解析と記録はスマホ版が行います。PC 連携を設定しなくても、スマホ版の手動読み込みと記録は使えます。

Windows 11 と iOS/iPadOS 27 以降が必要です。初回の USB 信頼設定には **Apple Devices** または **Apple 公式サイト配布のクラシック版 iTunes（EXE）** のどちらか一方が必要です。[Apple Devices の案内](https://support.apple.com/guide/devices-windows/mchl5ded2763/windows)／[クラシック版 iTunes](https://www.apple.com/itunes/download/win64)。Microsoft Store 版 iTunes と iCloud for Windows は、この信頼設定の代わりになりません。MochiLog のインストーラーに Python・.NET を別途追加する必要はありません。

### 初回設定

1. [GitHub Releases の最新版](https://github.com/MochiLog/MochiLog-Windows/releases)から `MochiLog-Windows-Alpha-Setup.exe` を入手してインストールします。現在のアルファ版インストーラーはコード署名されていません。Windows の設定からアンインストールできます。
2. Apple Devices またはクラシック版 iTunes を起動し、iPhone・iPad をデータ対応 USB ケーブルで PC に接続します。端末のロックを解除して「このコンピュータを信頼」を許可し、そのアプリに端末が表示されることを確認します。
3. MochiLog Windows の「端末」で設定する端末を選び、「USB の信頼設定」を進めます。初回は数分かかることがあります。進行中は画面の状態表示を確認し、端末のロックを解除したままにしてください。完了後は USB を外して、PC と端末を同じ Wi-Fi に置きます。
4. 無線の診断接続が確認できたら、Windows 側でペアリング QR を表示します。端末の MochiLog で「設定 → 自動ログ収集 → パソコン連携」を開き、**Windows** を選んで QR を読み取り、PC に表示された6桁コードを入力します。Mac・Windowsのペアリングをここで管理できます。
5. Windows アプリを起動したままにします。端末のロック解除中に解析ログを収集し、スマホ版を開くと受信・解析・記録が始まります。必要なときは PC の「今すぐログを収集」や端末の「今すぐ受信」を使えます。

### 日々の使い方

PC は端末のロック解除中に、ローカルの無線診断接続から新しいファイルを収集します。収集済みファイルは、スマホ版を開くまで転送待ちになります。必要な当日分が揃うと不要な自動再走査を休止しますが、手動操作と転送待受は続きます。複数の PC を同じ端末に連携しても、すでに受信したログは重複登録しないよう確認します。

「電池ログ」画面では転送待ち・送信済みの生ログを確認、書き出し、再送できます。初期設定ではスマホ側の受信確認後に送信済みログを削除します。「送信後も保管」を選ぶと保存期間と容量上限を変更できます。未転送分は自動整理から保護されます。ログの中身を Windows アプリで解析することはありません。

設定では Windows へのサインイン時の起動やタスクトレイでの常駐を選べます。トレイ常駐中にウィンドウを閉じても収集・転送は続き、トレイの「終了」で停止します。

Tailscale は任意です。両端末で接続し、スマホ版でモバイル通信の転送を許可すると、PC に**収集済み**のファイルを外出先から受け取れます。モバイル通信だけで端末内の新しい解析ログを PC が収集することはできません。

### 困ったときは

- **Apple Devices に端末が出ない:** データ対応ケーブルを PC 本体へ直結し、ロック解除・信頼の確認をやり直します。Apple Devices または iTunes を更新・再起動し、再度端末が表示されてから MochiLog の USB 設定を試してください。エクスプローラーだけに端末が出ていても、信頼設定が完了しているとは限りません。
- **ログが生成されない:** 端末の「設定 → プライバシーとセキュリティ → 解析と改善」で解析の共有を確認します。OS 更新後も再確認してください。オンにした直後は次のログ生成を待ちます。
- **PC では収集済みだが記録が増えない:** 該当端末で MochiLog を開き、「パソコン連携」の接続状態と処理結果を確認します。すでに読み込んだログは重複として省略されます。
- **接続できない:** 端末のロック、PC と端末の Wi-Fi、Windows アプリの起動状態を確認し、再検索します。ネットワークによっては IP の手動指定も利用できます。Windows が標準ポートを予約している場合、MochiLog Windows は別のポートを選んで端末に通知します。
- **改善しない:** Windows と端末の「パソコン連携」で日付別のデバッグログを確認し、サポート画面から発生日を指定して関連ログを添付してください。生の解析ログとペアリング鍵は自動添付されません。

これはアルファ版です。問題の解決には時間がかかり、個別に返信できない場合があります。[プライバシーポリシー](https://mochilog.ryuya-dev.net/privacy)と[利用規約](https://mochilog.ryuya-dev.net/terms)も参照してください。

## English

### What you need

MochiLog Windows collects Apple analytics files from an iPhone or iPad and sends them over an encrypted connection when you open MochiLog on that device. Eligible Apple Watch files stored on its paired iPhone are included. The mobile app parses and records the files, and it also works without computer pairing.

You need Windows 11 and iOS/iPadOS 27 or later. For initial USB trust, install **either Apple Devices or the classic iTunes EXE from Apple's website**: [Apple Devices help](https://support.apple.com/guide/devices-windows/mchl5ded2763/windows) / [classic iTunes](https://www.apple.com/itunes/download/win64). Microsoft Store iTunes and iCloud for Windows do not replace this setup. The MochiLog installer includes its other runtimes; users do not need to install Python or .NET separately.

### Set up

1. Download `MochiLog-Windows-Alpha-Setup.exe` from [GitHub Releases](https://github.com/MochiLog/MochiLog-Windows/releases) and install it. This alpha installer is not currently code signed. You can uninstall it in Windows Settings.
2. Open Apple Devices or classic iTunes. Connect the unlocked device directly to the PC with a USB data cable, approve **Trust This Computer**, and confirm the device appears in Apple's app.
3. Select the device in MochiLog Windows and run **Set Up USB Trust**. Initial setup can take a few minutes. Keep the device unlocked and watch the progress indicator. After it finishes, unplug the cable and put the PC and device on the same Wi-Fi.
4. Once wireless diagnostic access is confirmed, show the pairing QR in MochiLog Windows. On the device, open **MochiLog → Settings → Automatic Log Collection → PC Link**, select **Windows**, scan the QR, and enter the six-digit code shown on the PC. This screen manages both Mac and Windows pairings.
5. Leave the Windows app running. It collects files while the device is unlocked; open the mobile app to receive and record them. Use **Collect Logs Now** or **Receive Now** to retry manually.

### Everyday use and help

New analytics collection needs an unlocked device and a local wireless diagnostic connection. Files already collected wait on the PC until the mobile app opens. Automatic rescans pause when the required daily files are present; manual actions and the transfer listener remain available. Multiple paired computers check for already received files before sending them again.

The **Battery Logs** page lists pending and delivered raw files and supports export and resend. Delivered files are deleted after the phone confirms receipt by default. Optional retention has configurable time and size limits; pending files are protected from automatic cleanup. Windows does not parse the logs. Settings can launch the app at sign-in and keep it in the system tray; use **Quit MochiLog** in the tray menu to stop it.

Tailscale is optional. With both devices connected and mobile transfer enabled, you can receive files **already collected** by the PC while away. Cellular plus Tailscale cannot collect new system analytics from the device.

If Apple Devices does not show the device, try a known data cable, direct PC port, unlocked screen, and the Trust prompt; restart or update Apple's app. Seeing the device only in File Explorer does not establish trust. If logs do not appear, check **Settings → Privacy & Security → Analytics & Improvements** on the iPhone or iPad, especially after an OS update. If files are queued, open MochiLog on that device and inspect **Mac Transfer**. Already imported files are not recorded twice. For persistent problems, review the dated debug logs on both sides and use support to attach logs for the incident date. Raw analytics and pairing keys are not attached automatically.

This is an alpha. Fixes may take time and individual replies may not always be possible. See the [privacy policy](https://mochilog.ryuya-dev.net/privacy) and [terms](https://mochilog.ryuya-dev.net/terms).

## 現在のバッテリー値（ベータ）

ペアリングしたiPhone・iPadの充放電回数、設計容量、最大容量などを、PCの「現在のバッテリー」画面で端末ごとに確認できます。スマホでも使う場合は **設定 → 高度な設定 → 現在のバッテリー** をオンにしてください。初期状態はオフです。既存のPCペアリングを使うため、この機能のための再ペアリングは不要です。

アプリを開いている間は定期的に取得し、変化した値だけを暗号化して送ります。最終取得日時を表示し、取得できない項目は空欄として扱います。接続できない場合は最後の値を過去の値として表示します。**今すぐ受信／送信**で手動更新もできます。スマホからのPC更新要求は、PCの取得完了後に次の受信で反映されます。

この表示は日次の解析ログとは別の現在値です。履歴・バッテリー記録・iCloudには保存しません。アプリ終了後は値を保持しません。診断項目の意味や取得可否はOS・機種で異なるため、日次ログと一致する保証はありません。ロック中に取得できた場合もありますが、長時間のロックや接続条件で取得できないことがあります。Apple Watchの現在値を測る機能ではありません。スマホ単体の手動ログ読み込みはこれまでどおり使えます。

スマホはMochiLog 4.0.0の新しいベータ、PCはMochiLog Mac 0.2.14／MochiLog Windows 0.1.11以降に更新してください。モバイル通信ではPC連携のモバイル通信設定とTailscaleによる接続が必要です。

## Current battery values (beta)

View cycle count, design capacity and other current capacity fields for each paired iPhone or iPad in the computer’s Live Battery tab. On mobile, enable **Settings → Advanced Settings → Live Battery** to show the new tab. It is **off by default**. It uses your existing computer pairing; no new pairing is required.

Values refresh periodically while the app is open. Only changed values are sent, using encrypted transfer. The display includes the last acquisition time; unavailable fields remain empty, and a failed refresh leaves the previous values marked as outdated. Use **Receive Now / Send Now** for a manual update. A mobile request to refresh the computer appears on a subsequent receive after acquisition completes.

These are current diagnostic values, separate from daily Analytics files. They are not saved as history, battery records or iCloud data, and are discarded when the app exits. Available fields and their meaning depend on the device and OS; they may differ from daily Analytics values. A locked-device query has succeeded in testing, but long locks and connection conditions can prevent acquisition. This feature does not measure live Apple Watch battery values. Manual log import on mobile remains available without a computer.

Use the new MochiLog 4.0.0 beta with MochiLog Mac 0.2.14 or MochiLog Windows 0.1.11 or later. Cellular access requires the companion cellular setting and connectivity through Tailscale.

「APIの全項目」を展開すると、APIが返す製造情報・状態フラグ・バッテリー識別情報なども確認できます。元の項目名・値を表示し、単位は推測しません。これらもメモリ内だけで扱い、履歴・サポートログには保存しません。TailscaleとPC連携のモバイル通信許可を使って外出先からも受信できます。PCから新しい値を取得するには端末の診断サービスに接続できる必要があり、VPNの接続だけで取得を保証するものではありません。

Expand **All API fields** to view manufacturing metadata, flags, battery identifiers and other returned fields. Original names and values are preserved without guessing units; fields remain in memory and are not saved to history or support logs. Existing Tailscale routes and the PC Link cellular permission also allow receiving outside the local network. Fresh acquisition additionally requires a reachable device diagnostics service; VPN connectivity alone does not guarantee acquisition.

## 複数のiPhone・iPadでログを受け取る

同じPCとペアリングした端末で、同じApple AccountのiCloud同期が双方オンと確認できる場合だけ、他の端末のログも受信できます。片方がオフ・別アカウント・未確認なら共有しません。共有元のアプリをしばらく開いていない場合は再確認まで保留します。元の端末の履歴として保存し、他の端末の受信確認で元端末への未転送ログを削除しません。

### Receiving logs across devices

Devices paired with the same computer can receive another device’s logs only when both have confirmed iCloud sync enabled on the same Apple Account. Sharing is withheld for disabled sync, different accounts or unconfirmed devices. If the source app has not been opened for a while, sharing waits for renewed confirmation. Records retain their original device identity; another recipient’s acknowledgement never deletes the source’s pending log.

## 自動ログ収集と更新確認 / Automatic collection and updates

スマホの「設定 → 自動ログ収集」では、パソコン連携と端末内取得（実験機能）を別々に選べます。端末内取得は初期状態でオフです。対応するVPN・リフレクター経路と、その端末自身のOS信頼設定が必要です。明示的に選ぶと認証済みPCから自分のOSペアリング情報だけを引き継げます。初回の完全無線化、バックグラウンド、Developer Modeオフでの取得は保証しません。共通の取り込み処理で重複を防ぎます。

同じApple Accountで双方のiCloud同期がオンと確認できた端末は、現在のバッテリー値も共有できます。同期オフ・別アカウント・未確認では他の端末の値を共有しません。

PCの自動更新確認は初期状態でオフです。初回の確認画面または設定で有効にできます。手動の更新確認はいつでも使えます。

On mobile, **Settings → Automatic Log Collection** has separate controls for PC Link and experimental on-device collection. On-device collection is off by default and needs a compatible VPN/reflector route plus its own OS trust. Explicitly choosing reuse transfers only that device’s OS pairing over the authenticated PC connection. Fully wireless initial setup, background collection and Developer Mode-off operation are not guaranteed. Both routes use the same import and duplicate prevention.

Current battery values can also be shared when both devices have confirmed iCloud sync enabled on the same Apple Account. Disabled sync, different accounts and unconfirmed devices do not share other devices’ values.

Automatic update checks on the computer are off by default. Choose in the initial prompt or settings; manual checks remain available.
