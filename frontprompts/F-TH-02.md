# F-TH-02 — Animated Background, Particles, Border Rails, Corner Brackets

## Context
F-TH-01 established CSS variables. This prompt creates the animated background layers, floating particles, traveling neon border rails, burst nodes, and corner brackets. These are global elements rendered once in the app root.

## Requirements

### 1. Create `src/assets/styles/_background.scss`

All CSS for the 5 background layers:
- `.bg` — fixed container, inset 0, z-index 0, `background: var(--bg)`
- `.bg-diag` — repeating -45deg lines, `rgba(var(--ar), 0.025)`, animating diagonally over 18s
- `.bg-wave` (3 instances: `.w1`, `.w2`, `.w3`) — horizontal gradient strips, opacity 0.6, breathing animation 12–16s
- `.bg-orb` (2 instances: `.o1`, `.o2`) — large blurred circles (cyan + purple), pulsing scale/opacity 8–13s
- `.pf` (particle field) — container for JS-generated particles
- `.dot` — particle base style (absolute, rounded, pointer-events none)

Light mode overrides: diag 30% opacity, waves 20%, orbs 15%, dots 6%

### 2. Create `src/assets/styles/_rails.scss`

- `.rail` — fixed, 2px wide, `top: 60px`, `bottom: 50px`, z-index 50. `.L` left, `.R` right at `var(--M)`
- `.rail::before` — static dim base line `rgba(var(--ar), 0.08)`
- `.rail.L::after` — 180px traveling beam, top→down, 4.5s loop, triple neon glow
- `.rail.R::after` — 180px traveling beam, bottom→up, offset 2.25s
- `.burst` — 7px pulsing dots on rails, z-index 51 (6 total, 3 per side)
- `.crn` — 22px corner bracket marks at all 4 corners, `top: 60px` for top corners, `bottom: 50px` for bottom corners
- Mobile: rails `top: 56px`

All animation keyframes: `goD`, `goU`, `bP` (burst pulse), plus `@keyframes diagM`, `wF`, `oP` from background.

### 3. Add background and rail HTML to `AppComponent` or `LayoutComponent`

Add the following HTML elements at the TOP of the root component template (before the router-outlet or header). These are purely visual — no data bindings:

```html
<!-- Animated Background -->
<div class="bg">
  <div class="bg-diag"></div>
  <div class="bg-wave w1"></div>
  <div class="bg-wave w2"></div>
  <div class="bg-wave w3"></div>
  <div class="bg-orb o1"></div>
  <div class="bg-orb o2"></div>
  <div class="pf" id="particleField"></div>
</div>

<!-- Border Rails -->
<div class="rail L"></div>
<div class="rail R"></div>
<div class="burst L" style="top:15%"></div>
<div class="burst L" style="top:50%"></div>
<div class="burst L" style="top:78%"></div>
<div class="burst R" style="top:25%"></div>
<div class="burst R" style="top:62%"></div>
<div class="burst R" style="top:85%"></div>
<div class="crn tl"></div>
<div class="crn tr"></div>
<div class="crn bl"></div>
<div class="crn br"></div>
```

### 4. Create particle generator

In the root component's `ngAfterViewInit`, generate 90 multi-colored particles with random sizes, colors, and directions. Use `Renderer2` or direct DOM manipulation on the `#particleField` element.

6 colors with weighted distribution: cyan 40%, purple 20%, pink 12%, green 10%, amber 10%, blue 8%. 3 size tiers: small (1–2.5px, 55%), medium (2.5–4.5px, 30%), large (5–8.5px, 15%). Each particle gets a unique CSS animation with random angle and speed, using dynamically injected `@keyframes`.

### 5. Import the new partials in `styles.scss`

Add `_background` and `_rails` imports.

## Rules
- Background/rail elements are added ONCE in the root template
- No content, routing, or service changes
- Particle generation is the ONLY TypeScript addition (in root component `ngAfterViewInit`)
- All elements use CSS variables for colors
- `prefers-reduced-motion` disables all animations
