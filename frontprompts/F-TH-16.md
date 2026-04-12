# F-TH-16 — Auth Pages: Login, Unauthorized, AccessDenied

## Context
Auth pages are full-screen layouts WITHOUT the header/footer shell. The animated background is still visible behind them.

## Requirements

### 1. `LoginComponent` SCSS + minimal HTML

**Full-screen centered layout:**
- `display: flex; align-items: center; justify-content: center; min-height: 100vh`
- The animated background (bg layers from F-TH-02) shows behind the card
- No header, no footer, no side rails on this page

**Login card:**
- `var(--el)` bg, `var(--r-xl)` radius, `backdrop-filter: blur(24px)`, `box-shadow: var(--neon)` subtle
- `max-width: 400px; width: 100%; padding: 40px`
- Mobile: nearly full-width with 16px margins, padding 28px

**Card content (do not change content, only style):**
- Logo: Actions Center SVG logo mark, 72px, centered, `drop-shadow` neon glow
- Title "Welcome back": `font-size: 20px; font-weight: 800; color: var(--t1)`
- Subtitle: `font-size: 12px; color: var(--t3)`
- Azure AD button: full-width neon primary, Microsoft icon, "Sign in with Khalifa University"
- Divider line with "OR" text: `var(--bd)` line, `var(--t3)` text, `font-size: 9.5px; letter-spacing: 0.07em`
- Email + password inputs: themed from F-TH-06
- Local sign-in button: full-width neon primary
- Error message: `rgba(239,68,68,0.08)` bg, `#ef4444` text, `var(--r-md)` radius

### 2. `AccessDeniedComponent` SCSS

**Full-screen centered layout (same pattern):**
- Card: `var(--el)` bg, max-width 440px, centered, padded
- Icon: large `bi-shield-x`, `color: #ef4444`, 48px
- Title "Access Denied": `20px; 800 weight; var(--t1)`
- Message: `13px; var(--t2)`
- Buttons: "Dashboard" neon primary + "Go Back" ghost

### 3. `UnauthorizedComponent` SCSS

**Same pattern:**
- Icon: `bi-person-x`, `color: var(--warning)`, 48px
- Title "Session Expired"
- Button: "Sign In" neon primary

## Rules
- SCSS only per component (plus adding logo SVG reference in login template if not present)
- Do NOT change any auth logic, MSAL integration, form submission, or redirect logic
- These pages must work WITHOUT the LayoutComponent wrapper (they have their own full-screen layout)
- The animated background div may need to be duplicated in these components OR they should share the same root-level background from AppComponent
