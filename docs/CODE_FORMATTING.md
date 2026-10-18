# コード整形

C#とXAMLはCSharpier 1.2.1で4スペース・100桁を目安に整形します。CSharpierは整形後の構文を自動検証します。PythonはRuff 0.14.0を使用します。バージョンは各設定ファイルと整形スクリプトで固定しています。

開発には.NET SDK、Python 3.10以降、`uv`を使用します。アプリ利用者には追加ツールは不要です。初回はリポジトリのルートでローカルツールを復元してください。

```sh
dotnet tool restore
python scripts/format-code.py
python scripts/format-code.py --check
```

生成された `bin`・`obj`、コンパイル済み収集ツール、診断データは対象に含めません。

## 2026-10-19の確認

CSharpierによるC#の構文検証と、XAMLの要素・属性・内容の比較が成功。Python 10件が成功。Windows上で暗号化転送・ACK・再送防止・プロセス制限・保持管理の既存テストが成功し、Releaseビルドは警告0・エラー0。MacではWindows専用DPAPIが利用できないため、プロトコル全体の検証はWindowsで実行。
