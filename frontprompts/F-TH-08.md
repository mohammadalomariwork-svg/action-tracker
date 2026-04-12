# F-TH-08 — DataTable, Pagination, ConfirmDialog, CommentsSection, DocumentsSection

## Context
This prompt reskins all shared data and interaction components.

## Requirements

### 1. `DataTableComponent` SCSS
**Table wrapper:** card treatment with `overflow: hidden; padding: 0`
**Filter/toolbar bar:** `padding: 14px 16px; border-bottom: 1px solid var(--bd)`, flex wrap, mobile stacks vertically
**Table headers:** `font-size: 10px; font-weight: 700; letter-spacing: 0.07em; text-transform: uppercase; color: var(--t3); background: var(--sf); border-bottom: 1px solid var(--bd)`
**Table rows:** transparent bg, `border-bottom: 1px solid var(--bd)`, `font-size: 13px; color: var(--t2)`. Hover: `background: var(--sfh)`, text brightens to `var(--t1)`
**Code tags (ACT-001, PRJ-xxx):** `font-weight: 700; font-size: 11px; color: var(--accent)`
**Title cells:** `font-weight: 600; color: var(--t1); white-space: normal; min-width: 200px`
**Overdue rows:** `border-left: 3px solid #ef4444`
**Table scroll:** `overflow-x: auto` wrapper, `min-width: 700px` on table

**Mobile card view (<600px):** Hide table, show `.mobile-cards` — each row becomes a card with `var(--sf)` bg, `var(--bd)` border, `var(--r-md)` radius. Code + status in header row, title below, key-value pairs for other fields.

### 2. Pagination
- `padding: 10px 14px; border-top: 1px solid var(--bd); flex-wrap: wrap`
- "Showing X–Y" text: `font-size: 11px; color: var(--t3)`
- Page buttons: `28px` square, `var(--r-sm)` radius, `var(--sf)` bg, `var(--bd)` border
- Active page: `background: var(--accent); color: #000; box-shadow: var(--neon)`

### 3. `ConfirmDialogComponent` SCSS
- Modal backdrop: `rgba(0,0,0,0.50); backdrop-filter: blur(4px)`
- Dialog: card-elevated treatment, `var(--r-xl)` radius, `padding: 28px`
- Warning icon: large, centered, themed color (red for delete, amber for warning)
- Title: `font-size: 16px; font-weight: 800; color: var(--t1)`
- Message: `font-size: 13px; color: var(--t2)`
- Buttons: Cancel (ghost) + Confirm (neon primary or neon danger)

### 4. `CommentsSectionComponent` SCSS
- Section wrapper: card treatment
- Individual comments: `var(--sf)` inner areas, `var(--bd)` bottom borders
- Author: `font-weight: 600; color: var(--t1)`. Time: `font-size: 10px; color: var(--t3)`
- Comment body: `font-size: 13px; color: var(--t2)`
- High-importance: `border-left: 3px solid var(--warning)`
- Add comment input: themed input + neon submit button

### 5. `DocumentsSectionComponent` SCSS
- Section wrapper: card treatment
- File rows: `var(--sf)` bg, icon + filename + size + download button
- Upload area: `border: 2px dashed var(--bd); border-radius: var(--r-lg)`, hover brightens border to accent
- File type icons: colored by type (PDF=red, XLSX=green, DOC=blue)

## Rules
- SCSS only per component
- No template or TypeScript changes
- Mobile card view: if the HTML for mobile cards doesn't exist, add a CSS class `.mobile-cards` to existing alternate layout or create minimal HTML wrapper that shows/hides based on media query
