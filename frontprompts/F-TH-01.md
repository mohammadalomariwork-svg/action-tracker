# F-TH-01 — Theme Variables, CSS Custom Properties, Global Resets

## Context
This is the first prompt. It establishes the entire CSS custom property system for dark and light themes. Read `DARK-NEON-SPEC.md` before implementing.

## Requirements

### 1. Create `src/assets/styles/_theme-variables.scss`

Define ALL CSS custom properties from the DARK-NEON-SPEC under `[data-theme="dark"]` and `[data-theme="light"]` selectors on `:root` or `html`.

Include every variable: `--bg`, `--bg2`, `--sf`, `--sfh`, `--el`, `--inp`, `--inpf`, `--bd`, `--bdh`, `--t1`, `--t2`, `--t3`, `--nav`, `--csh`, `--neon`, `--neon2`, `--accent`, `--ar` (accent rgb triplet), `--accent2`, `--a2r`, `--scr`, `--drag-outline`.

Also define static variables (same both themes): `--font`, `--r-sm`, `--r-md`, `--r-lg`, `--r-xl`, `--r-full`, `--ease`, `--M` (content margin).

Add responsive `--M` overrides:
- `≤480px`: 8px
- `481–768px`: 16px  
- `769–1024px`: 28px
- `≥1025px`: 44px

### 2. Create `src/assets/styles/_global-resets.scss`

Apply to `body`:
- `font-family: var(--font)`
- `background: var(--bg2)`
- `color: var(--t1)`
- `font-size: 15px`, `line-height: 1.6`
- `-webkit-font-smoothing: antialiased`
- `min-height: 100vh`
- `overflow-x: hidden`
- `transition: background 0.5s, color 0.3s`

Apply `::selection { background: rgba(var(--ar), 0.25) }`

Custom scrollbar:
- Width 5px, transparent track
- Thumb: `var(--scr)`, border-radius 3px

### 3. Update `src/styles.scss`

Import the two new partials BEFORE any Bootstrap imports so CSS variables are available globally. Keep all existing Bootstrap imports intact.

Ensure `Plus Jakarta Sans` is imported (it likely already is — do not duplicate).

### 4. Set default theme on `<html>` tag

In `src/index.html`, add `data-theme="dark"` attribute to the `<html>` element. This is the ONLY change to `index.html` in this prompt.

## Rules
- SCSS files only (plus one attribute on index.html)
- No TypeScript changes
- No component changes
- CSS variables must cover EVERY color, shadow, border, and background used in subsequent prompts
