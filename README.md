# MochiLog Windows

MochiLog Windows は、iPhone・iPad のバッテリー解析ログを PC で収集し、端末で MochiLog を開いたときに暗号化して渡すアルファ版アプリです。Apple Watch のログがペアリング先の iPhone に保存されていれば、それも対象です。解析と記録は端末側で行います。PC 連携を設定しなくても、スマホ版の手動読み込みは使えます。

Windows 11 と iOS/iPadOS 27 向けです。[GitHub Releases](https://github.com/MochiLog/MochiLog-Windows/releases)からインストーラーを入手してください。Python や .NET を別途入れる必要はありません。**初回の USB 信頼設定には、Apple Devices または Apple 公式サイト配布のクラシック版 iTunes が必要**です。どちらか一方を用意してください。現在のアルファ版インストーラーはコード署名されていません。

初回は端末をロック解除して USB 接続し、「このコンピュータを信頼」を許可します。Windows アプリの「端末」で無線接続を確認したら、PC の QR を端末の「MochiLog → 設定 → 自動ログ収集 → PC 連携」で読み取り、確認コードを入力します。以後は PC に収集済みのログを、端末で MochiLog を開くと受信できます。Windows もスマホ版の「PC 連携」画面で管理します。

詳しい画面ごとの手順、Apple Devices に端末が出ない場合の対処、日々の使い方は[日本語・英語の利用ガイド](docs/USER_GUIDE.md)をご覧ください。開発・ビルド・依存関係・転送プロトコルの情報は[開発者向け文書](docs/DEVELOPMENT.md)にあります。

## 現在のバッテリー値（ベータ）

スマホの高度な設定でオンにすると、新しいタブで現在の充放電回数・容量を確認できます。履歴には保存しません。PCの「現在のバッテリー」画面にも端末ごとに表示します。詳しくは利用ガイドをご覧ください。

---

MochiLog Windows is an alpha companion that collects iPhone and iPad battery analytics files and transfers them over an encrypted connection when you open MochiLog on the device. It also handles eligible Apple Watch files stored on a paired iPhone. Parsing and record creation happen on the mobile device. The mobile app works without PC pairing.

It supports Windows 11 and iOS/iPadOS 27. Download the installer from [GitHub Releases](https://github.com/MochiLog/MochiLog-Windows/releases). Python and .NET are included. **Initial USB trust requires either Apple Devices or the classic iTunes EXE from Apple's website.** The current alpha installer is not code signed.

Unlock the device, connect it by USB once, and approve **Trust This Computer**. After the Windows app confirms wireless access, scan its QR in **MochiLog → Settings → Automatic Log Collection → PC Transfer** and enter the confirmation code. Windows pairings also appear on that mobile screen.

See the [Japanese and English user guide](docs/USER_GUIDE.md) for detailed setup, Apple Devices troubleshooting, and everyday use. Build, dependency, and protocol details are in the [developer notes](docs/DEVELOPMENT.md).

Current battery values are also available in the computer’s Live Battery screen and an optional mobile tab, disabled by default. These values are not saved as history. See the user guide for details.

同じPCとペアリングした複数のiPhone・iPadでは、同じApple Accountで双方のiCloud同期が有効と確認できる場合に限り、他の端末のログも暗号化して受信できます。未確認・同期オフ・別アカウントなら共有しません。共有元のアプリをしばらく開いていない場合は再確認まで保留します。詳しくは[利用ガイド](docs/USER_GUIDE.md)を参照してください。

Devices paired with the same computer can receive each other’s logs when both have confirmed iCloud sync enabled on the same Apple Account. Disabled sync, different accounts or unconfirmed permissions prevent sharing. If the source app has not been opened for a while, sharing waits for renewed confirmation. See the [user guide](docs/USER_GUIDE.md).

自動更新確認は初期状態でオフです。初回の選択画面または設定でオンにできます。同じApple AccountのiCloud同期を双方で有効にしている場合は、現在のバッテリー値も他の端末へ共有します。スマホの実験的な端末内取得はPC連携と別々に切り替えられ、初期状態でオフです。[使い方と条件](https://github.com/MochiLog/MochiLog/blob/experiment/mac-log-transfer/docs/automatic-log-collection.md)をご確認ください。

Automatic update checks are off by default and can be enabled in the initial prompt or settings. Current battery values can also be shared between devices with confirmed iCloud sync on the same Apple Account. Experimental on-device collection in the mobile app is independently configurable and off by default. See the [guide and requirements](https://github.com/MochiLog/MochiLog/blob/experiment/mac-log-transfer/docs/automatic-log-collection.md).

次のスマホ版ベータでは、[idevice_pairからのペアリングファイル直接インストール](https://github.com/MochiLog/MochiLog#次のベータで対応予定)にも対応予定です。公式ツールへのアプリ登録は[PR #84](https://github.com/jkcoxson/idevice_pair/pull/84)で提案中です。PC連携は引き続きiOS/iPadOS 27以降が対象で、端末内取得は17以降、端末内の初回ペアリングは27以降です。

The next mobile beta plans to support direct pairing-file installation from idevice_pair. Official tool registration is proposed in [PR #84](https://github.com/jkcoxson/idevice_pair/pull/84). PC transfer still targets iOS/iPadOS 27+; on-device collection targets 17+, with device-only initial pairing requiring 27+. See the [mobile beta announcement](https://github.com/MochiLog/MochiLog#次のベータで対応予定).
