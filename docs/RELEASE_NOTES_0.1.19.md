# MochiLog Windows 0.1.19 Alpha

## 日本語

各ページの本文幅・左右の余白・見出し位置を統一しました。ホーム、現在のバッテリー、電池ログ、設定、アプリ情報、ライセンスを移動しても位置が揃います。広いウィンドウではサイドバーを展開し、狭いウィンドウでは標準のコンパクト表示へ切り替えます。サイドバーの幅も広げました。

ホームのカードは実際の本文領域の幅で配置を決めるようにしました。サイドバー開閉・ウィンドウサイズ変更・表示倍率による幅のずれと、狭い画面で固定の最小幅がはみ出す問題を修正しました。端末のペアリングや保管済みログは引き継ぎます。

## English

All pages now share content width, margins and heading placement: Home, Live Battery, Battery Logs, Settings, About and Licenses. The sidebar expands on wide windows and uses native compact navigation on smaller windows. The expanded pane is wider.

Home cards use the measured content viewport instead of subtracting an estimated sidebar from the window size. Resizing, pane changes and display scaling no longer use stale width estimates; narrow windows no longer force a minimum content width. Existing pairings and stored logs are retained.
