# Actions Center — Dark Neon Design Specification

## Design Direction
Dark-first "Command Center" aesthetic with neon cyan accents, animated background particles, traveling border rail lights, and a light mode toggle. Every surface uses dark glass transparency. The interface feels like a mission control dashboard.

---

## 1. Theme System (CSS Custom Properties)

All colors are defined as CSS variables on `[data-theme="dark"]` and `[data-theme="light"]`. The theme is toggled by setting `data-theme` attribute on `<html>`. The user's choice is persisted in `localStorage` as `ku-theme`.

### Dark Theme (Default)
```scss
$bg-base: #0B1930;
$bg-deep: #071224;
$bg-surface: rgba(255,255,255,0.04);
$bg-surface-hover: rgba(255,255,255,0.07);
$bg-elevated: rgba(255,255,255,0.06);
$bg-input: rgba(255,255,255,0.05);
$bg-input-focus: rgba(255,255,255,0.09);
$border-default: rgba(255,255,255,0.07);
$border-hover: rgba(255,255,255,0.14);
$text-1: #E2E8F0;  // primary text — 10.5:1 contrast ✓
$text-2: #94A3B8;  // secondary — 5.4:1 ✓
$text-3: #64748B;  // muted — 3.2:1 (large text only) ✓
$accent: #00E5FF;
$accent2: #7C5CFC;
$nav-bg: rgba(11,25,48,0.88);
```

### Light Theme
```scss
$bg-base: #F2F4F8;
$bg-deep: #E8EBF0;
$bg-surface: rgba(255,255,255,0.82);
$bg-surface-hover: rgba(255,255,255,0.95);
$bg-elevated: rgba(255,255,255,0.90);
$bg-input: rgba(255,255,255,0.85);
$bg-input-focus: rgba(255,255,255,1);
$border-default: rgba(0,0,0,0.06);
$border-hover: rgba(0,0,0,0.12);
$text-1: #0B1222;
$text-2: #4A5568;
$text-3: #94A3B8;
$accent: #0055FF;  // shifts to deeper blue for light bg contrast
$nav-bg: rgba(255,255,255,0.85);
```

### Semantic Colors (Same Both Themes)
```scss
$success: #22c55e;
$warning: #f59e0b;
$danger: #ef4444;
$info: #3b82f6;
```

### Neon Glow Shadows
```scss
$neon: 0 0 8px rgba($accent, 0.50), 0 0 24px rgba($accent, 0.25), 0 0 48px rgba($accent, 0.10);
$neon-intense: 0 0 12px rgba($accent, 0.70), 0 0 36px rgba($accent, 0.40), 0 0 72px rgba($accent, 0.15);
```

---

## 2. Typography
- Font: `Plus Jakarta Sans` (already in project), weights 400–800
- No font changes — same family, same sizes throughout
- Text colors change via CSS variables only

---

## 3. Animated Background (Fixed, Behind All Content)

**5 layers, all `position: fixed`, `z-index: 0`:**

1. **Diagonal sweep lines** — repeating -45deg lines at 0.025 accent opacity, animating diagonally over 18s
2. **Wave bands** — 3 horizontal luminous gradient strips breathing at 60% opacity
3. **Pulsing orbs** — 2 large blurred gradient circles (cyan + purple) pulsing scale/opacity
4. **Multi-colored particles** — 90 dots in 6 colors (cyan 40%, purple 20%, pink 12%, green 10%, amber 10%, blue 8%), 3 size tiers, random direction movement
5. Light mode: all effects reduced to 6–30% opacity

---

## 4. Animated Border Rails

- 2px vertical lines at left/right margins (`--M: 44px` desktop, 28px tablet, 16px phone, 8px small phone)
- Static dim base: `rgba($accent, 0.08)`
- **Left rail:** 180px bright beam travels top→bottom over 4.5s, with triple-layer neon glow
- **Right rail:** same beam travels bottom→top, offset by 2.25s
- **Burst nodes:** 6 pulsing cyan dots along the rails
- **Corner brackets:** L-shaped accent marks at all 4 corners of the content area
- Rails start at `top: 60px` (below header) and end at `bottom: 50px` (above footer)

---

## 5. Layout Structure

```
<header class="hdr">  ← FULL WIDTH, z-index 100, sticky
<div class="wrap">     ← margin: 0 var(--M), side borders
  <div class="ct">     ← padding: 24px
    content...
  </div>
</div>
<footer class="ft">   ← FULL WIDTH, z-index 100
```

- Header and footer span 100% viewport width, no side margins
- Content container has responsive side margins matching the border rails
- Header: 60px desktop, 56px mobile
- Footer: glass background matching header

---

## 6. Component Styles

### Cards
```scss
background: var(--bg-surface);
border: 1px solid var(--border-default);
border-radius: 16px;
backdrop-filter: blur(16px);
box-shadow: var(--card-shadow);
// Hover: border brightens, optional neon glow
```

### Buttons — Neon Primary
```scss
background: var(--accent);
color: #000;
border-radius: 12px;
font-weight: 700;
box-shadow: var(--neon);
// Hover: translateY(-1px), neon-intense glow, shimmer overlay
// Active: scale(0.97)
```

### Buttons — Ghost
```scss
background: var(--bg-surface);
color: var(--text-2);
border: 1px solid var(--border-default);
border-radius: 12px;
backdrop-filter: blur(8px);
// Hover: brighter bg, stronger border
```

### Inputs
```scss
background: var(--bg-input);
border: 1px solid var(--border-default);
border-radius: 12px;
color: var(--text-1);
// Focus: brighter bg, accent border, 3px accent ring + 16px accent glow
```

### Badges (Status/Priority)
```scss
border-radius: 9999px;
font-size: 10px;
font-weight: 700;
letter-spacing: 0.05em;
text-transform: uppercase;
// ::before pseudo dot (5px circle, currentColor)
// Each type gets its own rgba background + currentColor text:
// cyan (accent), green (success), amber (warning), red (danger), purple (accent2), gray (muted)
```

### Progress Bars
```scss
track: rgba($accent, 0.06), 6px height, full radius
fill: linear-gradient(90deg, $accent, $accent2), glow shadow
```

### Data Tables
```scss
// Wrapper: card treatment
// Headers: bg-surface, 10px uppercase, text-3 color
// Rows: transparent bg, border-bottom subtle
// Row hover: bg-surface-hover, text brightens to text-1
// Overdue rows: 3px solid left border in $danger
// Mobile (<600px): table hidden, card-based layout shown instead
```

### Notification Items
```scss
// Unread: rgba($accent, 0.03) background
// Unread dot: 6px circle, $accent, 8px glow shadow
// Icon wrap: 34px rounded square, themed bg per type
```

---

## 7. Responsive Breakpoints & Margins

| Breakpoint | Side Margin | Stat Cards | Detail Grid |
|---|---|---|---|
| ≤480px | 8px | 2 cols | stacked |
| 481–600px | 16px | 3 cols | stacked |
| 601–768px | 16px | 3 cols | stacked |
| 769–1024px | 28px | 4 cols | stacked |
| ≥1200px | 44px | 6 cols | 5:2 split |

---

## 8. Navigation

- Desktop: horizontal nav links in header
- Mobile (<768px): nav hidden, hamburger button shows, opens glass off-canvas drawer from right
- Off-canvas: dark glass background, full nav with icons, close button

---

## 9. Drag-and-Drop Section Reordering

Dashboard sections are sortable via drag handles. Order persists in `localStorage` as `ku-layout`. Reset button clears saved order.

---

## 10. Critical Rules

- **SCSS only** — do NOT change any TypeScript logic, data bindings, or component behavior
- **HTML changes minimal** — only add CSS classes, theme wrapper elements, or background/rail DOM elements
- **No content changes** — text, labels, data, routes, guards all remain untouched
- **CSS variables everywhere** — never hardcode colors, always use variables
- **Both themes must work** — every element must be readable in dark AND light mode
- **Mobile-first** — base styles are mobile, desktop overrides via min-width media queries
- **Reduced motion** — `@media (prefers-reduced-motion: reduce)` disables all animations
