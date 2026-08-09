# マウスカーソル自動切替 (MouseCursorSupporter)

Windows 11 のマウスカーソルを、ZIP配布されているカーソルパックから簡単に登録し、
一定間隔・時間帯・Windows起動時などのタイミングで自動的に切り替えるタスクトレイ常駐アプリです。

登録したデザインは Windows 標準の「マウスのプロパティ > ポインター」の一覧にも
通常のカーソルスキームとして表示され、そちらから手動で選ぶこともできます。

## 主な機能

- カーソルパックのZIPを取り込み、17種のカーソル役割(矢印・手のひら・待機など)を自動判定して登録
  - 自動判定できなかったものは確認画面で手動割り当て
- 登録したデザインから任意のサブセットを「リスト」として作成し、自動切替の対象を絞り込み可能
- 自動切替のタイミング
  - 手動切替のみ
  - 一定間隔ごとに切替
  - 時間帯テーブル(時刻→デザイン)による切替
  - Windows起動/ログオン時にも切替(オプション)
- 切替時の選び方は「順番にローテーション」または「ランダム」から選択
- Windowsログオン時にアプリ自身を自動起動するオプション

管理者権限は不要です(レジストリは `HKEY_CURRENT_USER` 配下のみを使用し、
カーソルファイルは `%LocalAppData%` にインストール、実データは `%AppData%\MouseCursorSupporter` に保存します)。

## インストール

1. [Releases](../../releases) から最新の `MouseCursorSupporterSetup-*.exe` をダウンロード
2. 実行してインストール(インストール先はユーザーごとの `%LocalAppData%` 配下)

> **注意:** このインストーラーはコード署名を行っていないため、実行時に
> Windows SmartScreen の警告が表示されることがあります。「詳細情報」→「実行」を選択してください。
> 心配な場合はソースコードを確認の上、[ソースからビルド](#ソースからビルド)してご利用ください。

## カーソルパックの入手について

このアプリ自体にはカーソルデザインは同梱していません。配布されているカーソルパック(ZIP)を
各自で用意し、アプリの「パック管理」タブから読み込んでください。
配布元の利用規約(個人利用限定・再配布禁止など)に従ってご利用ください。

## ソースからビルド

.NET 10 SDK が必要です。

```powershell
git clone https://github.com/Minakami1124/MouseCursorSupporter.git
cd MouseCursorSupporter
dotnet run
```

インストーラーは [Inno Setup](https://jrsoftware.org/isinfo.php) で `installer/installer.iss` から
ビルドできます(GitHub Actions の `.github/workflows/release.yml` が `v*.*.*` タグのpush時に
自動ビルド・Releaseへの添付を行います)。

## ライセンス

[MIT License](LICENSE)
