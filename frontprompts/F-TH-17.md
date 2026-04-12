# F-TH-17 — Notification Components: NotificationBell, NotificationsPage

## Context
If the notification components were created from the earlier B-IN / F-IN prompt series, apply the dark neon theme. If they don't exist yet, skip this prompt entirely.

## Requirements

### 1. `NotificationBellComponent` SCSS (header dropdown)
- Bell icon button: same `icon-btn` style as other header buttons (36px, card bg, border)
- Unread count badge: `background: #ef4444; color: #fff; box-shadow: 0 0 6px rgba(239,68,68,0.50)`
- Dropdown panel: `var(--el)` bg, `var(--bd)` border, `var(--r-xl)` radius, `backdrop-filter: blur(24px)`, `box-shadow: 0 0 30px rgba(0,0,0,0.30)`, width 360px, max-height 440px, z-index 150
- Header row: title `var(--t1)` + "Mark all read" `var(--accent)`
- Notification items: `padding: 11px 14px; border-bottom: 1px solid var(--bd)`
  - Unread: `background: rgba(var(--ar), 0.03)`
  - Unread dot: `6px; var(--accent); box-shadow glow`
  - Icon wrap: 34px rounded square, themed bg per type (accent for actions, green for milestones, red for risks)
  - Title: `12px; 700 weight` (bold if unread)
  - Message: `10.5px; var(--t3); truncated`
  - Time: `9.5px; var(--t3)`
  - Hover: `var(--sfh)` bg
- Footer: "View all" link in `var(--accent)`
- Mobile (<576px): dropdown full-width

### 2. `NotificationsPageComponent` SCSS
- Max-width 800px centered
- Filters: type dropdown + read-status pills (same tab treatment from workspace tabs)
- Notification cards: `var(--sf)` bg, `var(--bd)` border, `var(--r-md)` radius
  - Unread: `border-left: 3px solid var(--accent); var(--sfh)` bg
  - Read: `opacity: 0.65`
  - Hover: card-glow effect
- Date group headers: `font-size: 10px; font-weight: 700; var(--t3); uppercase; letter-spacing: 0.07em`
- "Load more" button: full-width ghost button
- Empty state: large muted icon + message in `var(--t3)`
- Action buttons (mark read, delete): ghost icon buttons, dropdown menu

## Rules
- SCSS only
- If components don't exist, skip this prompt
- No changes to SignalR connections, service calls, or notification logic
