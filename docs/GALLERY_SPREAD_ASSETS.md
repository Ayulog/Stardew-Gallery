# Gallery Spread Assets v3

This is an asset/layout handoff, not menu integration or in-game acceptance.
`GallerySpreadLayout.cs` defines the internal pure BCL `GallerySpreadLayout` and
`GalleryUiAssets` classes. All rectangles are named value tuples
`(int X, int Y, int Width, int Height)` in logical texture coordinates, with
exclusive right/bottom edges. Consumers own XNA conversion and viewport scaling.
There are no Stardew, SMAPI, graphics, or condition-model dependencies in this file.

## Bounds API

Constants: `LogicalWidth = 1672`, `LogicalHeight = 941`, `LeftRowCount = 6`,
`EventColumns = 2`, `EventVisibleRows = 3`, `ScrollbarX = 1508`,
`FooterBaseline = 842`, `IconSize = 16`.
`FooterBaseline` is the intended text baseline, not the top of the footer.

| Member | (X, Y, Width, Height) |
| --- | --- |
| `LeftPageBounds` | `(120, 64, 565, 786)` |
| `PortraitBounds` | `(240, 120, 380, 270)` |
| `LeftRowBounds(row)` | `(195, 432 + 63 * row, 445, 48)`, row 0 through 5 |
| `RightPageBounds` | `(715, 64, 835, 786)` |
| `TitleBounds` | `(895, 68, 530, 56)` |
| `EventCardBounds(slot)` | `(755 + 365 * col, 140 + 225 * row, 345, 205)` |
| `EventCardHeaderBounds(slot)` | `(card.X + 16, card.Y + 10, 213, 34)` |
| `EventCardDetailsBounds(slot)` | `(card.X + 233, card.Y + 8, 98, 36)` |
| `EventCardThumbnailBounds(slot)` | `(card.X + 40, card.Y + 50, 265, 149)` |
| `AlbumScrollTrackBounds` | `(1508, 180, 24, 600)` |
| `DetailHeaderBounds` | `(755, 140, 710, 150)` |
| `DetailMetadataBounds` | `(755, 140, 425, 150)` |
| `DetailEventIdBounds` | `(755, 140, 425, 42)` |
| `DetailLocationBounds` | `(755, 190, 425, 92)` |
| `DetailThumbnailBounds` | `(1200, 140, 265, 149)` |
| `ConditionHeadingBounds` | `(755, 310, 710, 40)` |
| `ConditionViewportBounds` | `(755, 365, 710, 420)` |
| `DetailScrollTrackBounds` | `(1508, 365, 24, 420)` |
| `FooterBounds` | `(755, 810, 710, 40)` |
| `BackButtonBounds` | `(755, 810, 280, 40)` |
| `ReplayButtonBounds` | `(1185, 810, 280, 40)` |
| `ConditionCheckSource` | `(0, 0, 16, 16)` |
| `ConditionCrossSource` | `(16, 0, 16, 16)` |
| `ConditionQuestionSource` | `(32, 0, 16, 16)` |
| `ReplayGlyphSource` | `(0, 0, 16, 16)` |

Card slots are row-major 0 through 5: `col = slot % 2`, `row = slot / 2`.
Invalid row/slot arguments throw `ArgumentOutOfRangeException`.
Page bounds describe usable paper, not the outside leather silhouette.
Metadata contains its two text regions; the header contains metadata and thumbnail.
These are layout regions, not nested drawn frames.

The card grid ends at `(1465, 795)`, before the footer separator at Y=804.
Both layers share the right-page footer and title. The detail condition viewport is
710 pixels wide, aligned with the header/footer, and does not reach the scrollbar.
The footer actions are deliberately moved inside the right page, not on the binding
or over the preserved left page. No buttons, labels, status text, or thumbnail images
are baked into either spread.

## Assets And Provenance

| Asset constant | Path | PNG size |
| --- | --- | --- |
| `EventAlbum` | `assets/GalleryEventAlbum-v3.png` | 1672 x 941 |
| `EventDetail` | `assets/GalleryEventDetail-v3.png` | 1672 x 941 |
| `ConditionStatusIcons` | `assets/ConditionStatusIcons.png` | 48 x 16 |
| `ReplayGlyph` | `assets/ReplayGlyph.png` | 16 x 16 |
| `EventThumbnailAsset.Placeholder` | `assets/EventPlaceholder.png` | 640 x 360 |

The first four constants belong to `GalleryUiAssets`. The placeholder keeps the
existing `EventThumbnailAsset` path and `For` behavior unchanged.

The spreads derive from the repository-owned `assets/GalleryDetail-alpha-v2.png`.
All pixels at X < 700 are preserved exactly, including hidden RGB under transparent
pixels. The portrait rectangle retains the source's exact alpha mask: 88,889 pixels
are fully transparent, while the original frame overlaps the rectangle edges.
Clearing the entire portrait rectangle would damage that frame and is not done.
The six left-page information rows, title plaque, binding and outside edges remain.
The source asset and left-panel source file are not modified.

The right-page parchment is reconstructed from a clean patch of that same owned
image, mirror-tiled and feathered at the replacement boundary. Album cards use
original small brown pixel corners, not full orange frames. Layer 3 instead has
one compact thumbnail slot, header rules and an unframed blank condition area.
Both have a thin footer rule and scrollbar guide; the interactive thumb is not baked in.

The hand-authored 16-pixel masks are original art, with transparent backgrounds and
one-pixel brown edges. Atlas order is muted green check, muted red cross, ochre
question. The separate replay glyph is a brown right-pointing triangle. Use nearest
neighbor sampling and the exact source rectangles. Unknown uses the question,
never the cross. No fonts, game assets, other mod assets, downloads or third-party
libraries are used by the generator. Art follows the repository license.

The shared event placeholder is an original generic Stardew-inspired landscape:
pale sage sky, warm clouds and sun, layered green hills, teal trees, meadow grasses
and a winding stream. It is not a depiction of any particular event or location.
The fully opaque scene bleeds to all four edges, with no outer border, inset frame,
NPC, text or question symbol. The album/detail background owns thumbnail borders;
the image must not add a second frame. Its fixed flat-color composition is drawn
at 160 x 90 and enlarged to 640 x 360 with exact 4 x 4 pixel blocks, without
gradients, antialiasing, random state or third-party source art.

## Reproduction And Verification

Run from the repository root with PowerShell 7 on Windows:

```powershell
pwsh -NoProfile -File tools/Generate-GallerySpreadAssets.ps1
pwsh -NoProfile -File tools/Generate-GallerySpreadAssets.ps1 -VerifyOnly
```

The generator compiles the actual BCL layout alongside its embedded C# image code
using the runtime's `System.Drawing`, without NuGet or a project change. Paths are
resolved relative to the script, independent of the caller's working directory.
Normal mode writes only the five output PNGs. Verify-only mode performs no writes;
it regenerates expected pixels in memory and compares every decoded output pixel.
Generation is deterministic; PNG byte identity was checked across reruns on the
current PowerShell/System.Drawing runtime. Encoder bytes may differ across runtime
versions while decoded pixels remain identical.

Built-in validation covers dimensions, layout containment, card and detail-region
non-overlap, left-page RGBA identity, portrait alpha preservation, separate detail
art, placeholder opacity and 640 x 360 dimensions, and lossless PNG pixel round
trips for all five assets. The generated PNGs were also opened and
visually inspected. Native menu text fitting, UI-scale readability and controller
navigation require the integrating agents' in-game acceptance; artwork checks
alone do not establish those behaviors.

### Scenic Placeholder Validation (2026-09-07)

- Branch: `feature/2.1.0-event-detail`; base and unchanged HEAD:
  `dcfac2c3ec8a86e8e845ae2d0eb7becc99d6d198`. No commit created.
- Scope: generator, `assets/EventPlaceholder.png`, and this handoff only; existing
  integration work was preserved. `EventThumbnailAsset.Placeholder` and `For`
  remain unchanged, confirmed by the unchanged `GalleryUiRules.cs` SHA-256.
- Generation and `-VerifyOnly` passed for all five assets. A second generation
  produced identical SHA-256 hashes for all five PNGs.
- The four pre-existing generator outputs retained their exact pre-edit file
  hashes, proving byte as well as pixel identity. The source spread also retained
  its file hash. Both generated spreads passed exact X < 700 RGBA comparison and
  retained 88,889 fully transparent portrait pixels apiece.
- Placeholder SHA-256:
  `07AC742FB4E5F894F6A7E2E457AEF194940236BB99985716BB4CD33387B9D1A2`.
- The new PNG was opened and visually inspected: full-bleed landscape, crisp
  pixels, no frame or question motif. `git diff --check` passed with only existing
  worktree LF/CRLF warnings. No game build or runtime checks were run for this
  asset-only change; in-game thumbnail scaling/layout acceptance remains pending.
