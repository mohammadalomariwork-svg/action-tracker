# Actions Center — Brand Assets Setup Guide

## File Placement

Copy the files into your Angular project as follows:

```
action-tracker-frontend/
├── src/
│   ├── index.html              ← UPDATE (add favicon/manifest links)
│   ├── manifest.webmanifest    ← COPY HERE
│   └── assets/
│       ├── icons/
│       │   ├── favicon.svg
│       │   ├── favicon.ico
│       │   ├── favicon-16x16.png
│       │   ├── favicon-32x32.png
│       │   ├── favicon-192x192.png
│       │   ├── favicon-512x512.png
│       │   └── apple-touch-icon.png
│       └── images/
│           ├── logo-mark.svg           (icon only, transparent bg)
│           ├── logo-mark-64.png
│           ├── logo-mark-128.png
│           ├── logo-mark-256.png
│           ├── logo-full-dark.svg      (icon + text, for dark backgrounds)
│           ├── logo-full-dark.png
│           ├── logo-full-light.svg     (icon + text, for light backgrounds)
│           ├── logo-full-light.png
│           ├── logo-header.svg         (compact, for nav bar)
│           └── logo-header.png
```

## index.html Updates

Replace the existing favicon/title section in `src/index.html` with:

```html
<head>
  <meta charset="utf-8">
  <title>KU Actions Center</title>
  <base href="/">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="theme-color" content="#0B1930">
  <meta name="description" content="Khalifa University Actions Center — Project & Action Items Management">

  <!-- Favicons -->
  <link rel="icon" type="image/svg+xml" href="assets/icons/favicon.svg">
  <link rel="icon" type="image/x-icon" href="assets/icons/favicon.ico">
  <link rel="icon" type="image/png" sizes="32x32" href="assets/icons/favicon-32x32.png">
  <link rel="icon" type="image/png" sizes="16x16" href="assets/icons/favicon-16x16.png">
  <link rel="apple-touch-icon" sizes="180x180" href="assets/icons/apple-touch-icon.png">

  <!-- PWA Manifest -->
  <link rel="manifest" href="manifest.webmanifest">
</head>
```

## angular.json Updates

Add the manifest and icon assets to the `assets` array in `angular.json`:

```json
"assets": [
  "src/favicon.ico",
  "src/assets",
  "src/manifest.webmanifest"
]
```

## Usage in Components

### Header logo (dark theme)
```html
<img src="assets/images/logo-header.svg" alt="Actions Center" height="34">
```
Or use inline SVG for theme-reactive colors (recommended — see the HTML preview files).

### Login page logo
```html
<img src="assets/images/logo-mark.svg" alt="Actions Center" width="72" height="72">
```

### Theme-aware logo switching
```html
<img [src]="isDark ? 'assets/images/logo-full-dark.svg' : 'assets/images/logo-full-light.svg'"
     alt="Actions Center" height="40">
```

## Files Summary

| File | Size | Purpose |
|------|------|---------|
| `favicon.svg` | 1.5 KB | Modern browsers (best quality at any size) |
| `favicon.ico` | 5.1 KB | Legacy browsers (16+32+48px) |
| `favicon-16x16.png` | 630 B | Smallest tab icon |
| `favicon-32x32.png` | 1.5 KB | Standard tab icon |
| `favicon-192x192.png` | 12 KB | Android home screen / PWA |
| `favicon-512x512.png` | 34 KB | PWA splash screen |
| `apple-touch-icon.png` | 12 KB | iOS home screen (180x180) |
| `logo-mark.svg` | 1.5 KB | Icon only, transparent bg |
| `logo-mark-64.png` | 3.7 KB | Icon 64px |
| `logo-mark-128.png` | 7.8 KB | Icon 128px |
| `logo-mark-256.png` | 17 KB | Icon 256px (high-res) |
| `logo-full-dark.svg` | 1.9 KB | Full logo for dark backgrounds |
| `logo-full-dark.png` | 18 KB | Full logo PNG (dark bg) |
| `logo-full-light.svg` | 1.9 KB | Full logo for light backgrounds |
| `logo-full-light.png` | 17 KB | Full logo PNG (light bg) |
| `logo-header.svg` | 1.4 KB | Compact header logo (no subtitle) |
| `logo-header.png` | 11 KB | Compact header PNG |
| `manifest.webmanifest` | 513 B | PWA manifest |
