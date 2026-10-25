# WPF UI操作の修正と検証

対象は main の bb7538ff3feaf2e08592d20efdd1c9903778a071（機能強化）である。変更する製品コードは `Node/Editor/View` と `Node/Editor/Control` に限定する。Graph、保存形式、接続の妥当性判定、評価器、Undo/Redoの実装には変更を加えない。

## 確認した原因と修正

| 操作・条件 | 原因 | 修正 |
| --- | --- | --- |
| ポートからドラッグ後に離す、またはキャプチャが失われる | PortViewで状態を消してもConnectionAdornerの保持済み描画が無効化されない。PortViewがUpを処理済みにするためGraphViewのUpによる再描画に届かない | GraphViewがプレビュー状態のPropertyChangedを監視し、終了・中断を含めて描画を無効化する |
| 接続確定中のキャプチャ解除とビュー再構築 | ReleaseMouseCaptureがLostMouseCaptureを発生させ、接続コマンドがノード表示を作り直す | 対象を先にヒットテストし、操作所有者と一時状態を先に消し、キャプチャを解放した後に接続コマンドを一度呼ぶ |
| 非入力対象の図形がポートに重なる | VisualTreeHelper.HitTestは入力ポリシーとは異なる低レベル判定 | InputHitTestを使用し、背景へのドロップで起点ポートへフォールバックしない |
| ポートの再バインド・削除・タブ切替 | 終了時に祖先からGraphViewModelを探すだけでは、既に切り離されたビューの操作所有者を取得できない | 開始時のGraphViewModelを保持し、DataContextChangedとUnloadedでも中断する。座標更新の遅延処理もDataContextの同一性とLoadedを確認する |
| Panモードでタブを表示する | OnLoadedの早期returnが接続Adornerの生成まで飛ばす | 接続Adornerは全モードで生成する |
| モード変更・DataContext変更・再表示 | 削除した選択Adornerの参照、接続Adornerの旧DataContext、Unloaded後に復元されない購読 | モード変更時の操作中断、参照の初期化、描画用DataContextの更新、Loaded/Unloadedに対応する弱いプロパティ購読 |
| 選択のキャプチャ喪失 | 意図しない割り込みでも選択を確定していた | 通常の左ボタンUpでだけ選択を適用し、中断時は一時描画を消す。通常Upのキャプチャ解除では投げ縄の点列を消さない |
| 子コントロールのLostMouseCapture | バブリングで親のハンドラーも呼ばれる | OriginalSourceが所有者自身である場合だけ操作を終了する |
| 左・中・Ctrl+右ボタンが重なる | パン操作の開始ボタンを保持せず別ボタンのUpでも終了する | パン開始ボタンを保持し、同時操作による上書きを防ぐ |
| Ctrl+右パン後の通常右クリック | 処理済みUpでメニューが開かないと抑止フラグが次の右クリックまで残る | 新たな右クリック開始時に前の抑止状態を解除する |
| ノード表示の再バインド・アンロード | ドラッグ終了先が旧グラフと一致しない可能性、引数編集イベントの匿名購読が残る | 操作所有者を保持し、一度だけドラッグを終了する。引数編集イベントを名前付きハンドラーで購読・解除する |
| 数値ドラッグ中のキャプチャ喪失 | クリック状態、非表示カーソル、編集区間がUpまで残る | キャプチャ喪失・アンロード・取得失敗・例外で状態とカーソルを復元し編集を終了する |
| 色選択・ベジェ編集 | 任意ボタンで開始・終了でき、キャプチャ喪失を処理しない | 操作を所有するボタンのみで終了し、キャプチャ喪失時にも一時状態を消す |
| ノード追加のカテゴリサブメニュー | DispatcherTimerを開始するがTickを登録しておらず、開いている子があると行から離れてもタイマーを開始しない | Tickでサブメニューを閉じ、子への移動中は猶予時間内にタイマーを停止する。親の閉鎖時に子孫のPopupも閉じ、メニュー終了・再構築時にはタイマーを停止する |

## 検証方法と限界

`tests/Node.UiRegression/Node.UiRegression.csproj` は、変更した7個のUIコード、GraphView/PortView/NumberPort/ColorPickerの製品XAML、関連Adorner・スタイル・ベジェ処理を直接リンクする。NuGetのテストフレームワークやYMM4 DLLを必要とせず、WindowsのSTAスレッド上でWPFのWindow、Dispatcher、実マウスキャプチャ、ルーティング、ヒットテスト、保持描画を検証する。グラフとノードのViewModel、ダイアログ、翻訳文字列はテスト用代替であり、NodeViewのXAMLも操作ハンドラーだけを接続した最小構成である。したがって、YMM4内の全体統合試験の代替にはならない。

実行コマンドは `dotnet run --project tests/Node.UiRegression/Node.UiRegression.csproj -c Release` である。GitHub Actionsの `WPF UI regression` で同じテストを実行する。個々のテストは結果をPASS/FAILで表示し、失敗が一つでもあれば終了コード1を返す。合計20ケースであり、通常ドロップ、背景ドロップ、キャプチャ喪失、ビュー再バインド、アンロード、Panモード、モード変更、タブ再表示、投げ縄の正常確定、選択中断、子イベント、複数ボタン、ノード移動終了の一回性、引数編集購読、コンテキストメニュー抑止の持越し、数値・色・ベジェ編集、サブメニュータイマーを対象とする。

静的確認の対象にはタブ管理、グラフライブラリ、引数編集ダイアログ、ノード追加Popup、基本ポートコントロールも含める。ただし、すべてのUI経路の網羅や副作用の不存在を証明したものではない。ホスト依存のカスタムエディター置換、共有グラフ選択、実際の接続によるViewModel再構築、ホスト側Undo/Redo、モーダル表示、DPI変更、ウィンドウ非アクティブ化との組合せは実機統合確認が残る。表示ライフサイクルの変更が編集終了通知のタイミングに影響する可能性もあるため、以下をYMM4で確認する。

1. 型の一致・不一致、同方向、自己接続、循環、既存入力への差替えを含む接続操作を両方向から行い、保存・再読込とUndo/Redoで結果を確認する。
2. 背景・別ウィンドウ・ウィンドウ外へのドロップ、Alt+Tab、操作中のタブ切替・閉鎖・ノード削除でプレビューやカーソルが残らないことを確認する。
3. 通常クリック、Ctrl/Shiftによる選択、複数ノード移動、矩形・投げ縄選択、各パン操作、ズーム、Fit to screenを組み合わせて確認する。
4. 数値のクリック編集・ドラッグ・Enter確定・フォーカス移動、色とベジェの編集を中断し、値とUndo/Redoのまとまりを確認する。
5. 既存プロジェクト、共有グラフ選択、タブ名変更、ホストのカスタムプロパティエディターで従来の操作と表示を確認する。

## WPF仕様の参照

- https://learn.microsoft.com/en-us/dotnet/api/system.windows.uielement.lostmousecapture?view=windowsdesktop-10.0 — 子要素のキャプチャ喪失もバブリングする。
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.uielement.addhandler?view=windowsdesktop-10.0 — 処理済みルーティングイベントとhandledEventsToo。
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/hit-testing-in-the-visual-layer — 入力要素の判定とVisualレベルのヒットテスト。
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/adorners-overview — Adornerによる描画。
