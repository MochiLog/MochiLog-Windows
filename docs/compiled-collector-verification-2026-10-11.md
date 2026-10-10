# Windowsコンパイル済みヘルパー検証（2026-10-11）

[CI 38063916027](https://github.com/MochiLog/MochiLog-Windows/actions/runs/38063916027)は成功。pymobiledevice3 11.19.1全体をNuitka 4.3、CPython 3.13.15、MSVC、並列数2でコンパイルした。C#アプリはRelease/self-containedで発行。

インストーラーからのインストール、アンインストーラー登録、アンインストールと登録削除、再インストールが合格。インストール後のヘルパーをPythonのないPATHと無効なPYTHONHOME/PYTHONPATHで検証し、7コンパイル済みモジュール、7CLI経路、暗号・TLS・schema・型保持plist・Windows providerが合格（9.922秒、326,060,026 bytes）。

Windows実機を完成インストーラーで0.1.22へ上書き更新。Session1で起動し応答中であることを確認。PATHをSystem32だけにしPythonHOME/PATHを無効にした実機でも同梱ヘルパーの7モジュールとオフライン検証が成功。既存データの削除・初回信頼設定のやり直しは行っていない。今回の起動試験は新たな実機バッテリーログ収集完了の証明ではない。

GitHub v0.1.22 alphaのインストーラーはCIのed45d843f1dcfc51c7c94f042625713f8ecbebc3から作成。SHA-256は7a71c5b2542c8e3900a0601cc29bc4ed88d5320a50edd265d1b1311607ca90d0。後続のコミットは文書・Mac向け共有スクリプト分岐・検証出力・CIキャッシュを整備。次回以降のコンパイルにはキャッシュと180分の工程枠を用意した。ユーザーがPythonをインストールする必要はない。
