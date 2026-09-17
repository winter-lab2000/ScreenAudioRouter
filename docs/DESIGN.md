# DESIGN — Screen Audio Router

## Style anchor
macOS System Settings + Finder sidebar utility: calm light gray canvas,
white content cards, green/blue system switches, no chrome noise.

## Palette
| token | hex | use |
|-------|-----|-----|
| window bg | #F5F5F7 | app background |
| surface | #FFFFFF | cards |
| sidebar | #ECECEE | left rail |
| ink | #1D1D1F | titles |
| ink secondary | #6E6E73 | body/labels |
| ink tertiary | #8E8E93 | captions |
| accent | #0071E3 | primary actions / selection ring |
| success | #34C759 | master toggle ON |
| separator | #D2D2D7 | hairlines / chrome |

## Typography
- Display: Segoe UI Variable Display / SemiBold — page title 22px
- Body: Segoe UI Variable Text / Microsoft YaHei UI — 13px
- Mono: Cascadia Code / Consolas — log strip
- Sidebar captions 11px semi-bold tertiary

## Layout
```
┌ traffic ● ● ●    Screen Audio Router      [status] (toggle) ┐
├──────────────┬──────────────────────────────────────────────┤
│ 显示器 list  │  按显示器路由 (22)                            │
│  ● 主屏 1    │  subtitle                                     │
│  ○ 显示器 2  │  ┌ topology map (live monitor cards) ┐        │
│              │  └───────────────────────────────────┘        │
│ 状态 card    │  ┌ 映射 card: 输出 / 输入 / 保存 ┐            │
│ 打开日志/配置│  └──────────────────────────────────┘          │
│              │  ┌ 选项 card ┐  ┌ dark log strip ┐            │
└──────────────┴──────────────────────────────────────────────┘
```
- Sidebar 220px, content padding 28/20
- Card radius 12–16, buttons radius 10
- Window rounded 12 via DWMWCP_ROUND

## Signature
Monitor topology map: cards laid out at real screen positions,
accent ring on selection, bound speaker name under each card.

## Risk
Traffic-light close/minimize (macOS metaphor on Windows) + green
pill toggle — recognizable product feel without fake Apple icons.

## Tray
Programmatic glyph: blue rect + dark rect + wave arc.
Menu: 打开主面板 / 路由开/关 / 退出.
