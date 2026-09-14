# Generated sky candidate

## Adopted wrapping version

The owner approved the candidate and requested direct replacement without an A/B trial. A second built-in image-tool edit repaired the horizontal border silhouettes and cloud continuity. The adopted output is `content/loading-bay/sky/mountain-panorama.png` (1774×887 RGBA, 2,540,675 bytes, SHA-256 `e964e788aea7d984f4c7f9725fd5d92dfa37362d67e14cd2e01599ff13719a94`). Its manifest records the original reference separately; no original WAD texture or manifest was overwritten. `LoadingBaySkyBackground` selects this generated texture for both the construction study and ordinary product, using the existing Engine CameraView/resource lifecycle.

The generated edge columns are not byte-identical. Their mean RGB absolute difference is 4.823/255, versus 3.525/255 between the first two columns; this is a supporting continuity measurement, not proof of an invisible rendered join. No procedural blur, mirroring or blending was applied. The wrap-edited output preserves the approved mountain palette and detail.

Wrap edit prompt:

Edit this approved mountain panorama into a horizontally seamless repeating game sky texture. Preserve its composition, fine rocky detail, grey overcast clouds, muted charcoal/olive palette, atmosphere and 2:1 aspect ratio. Repair ONLY the wrap: the leftmost and rightmost borders must join continuously at every height, with matching mountain silhouette heights, cloud brightness and rock details. Treat the horizontal axis as periodic, so placing two copies side by side makes one continuous landscape with no vertical cut, lighting jump, duplicated mirror motif or obvious seam. Keep the large central dark peak and distant left grey ridge. Do not add objects or text; do not blur the image or enlarge its pixels. Opaque panorama.

`mountain-sky-v1.png` was generated with the built-in image generation tool using the retained `content/doom-e1m1/textures/sky/SKY1.png` as a visual reference. It is a new generated interpretation, not an original Doom asset or a lossless upscale. The original and its content manifest remain unchanged. This initial candidate was superseded by the adopted wrapping version above.

Initial visual assessment: preserves muted grey/olive mountains and overcast atmosphere with much finer readable rock detail. More naturalistic than strict pixel art. The initial candidate's edge silhouettes differed; it was followed by the wrapping refinement below.

## Generation prompt

Use case: stylized-concept. Asset type: horizontal wrapping mountain sky background texture for a Doom-inspired retro FPS. Input image 1 is a visual reference for composition, palette, silhouettes and mood, not an image to enlarge mechanically. Generate a new, higher-detail reinterpretation of this mountain panorama. Preserve the feel of its close dark craggy mountains, layered distant grey ridges, sparse very dark olive vegetation, pale overcast grey sky, and oppressive quiet atmosphere. Broad rounded rocky peaks, not alpine snow spikes. Keep roughly the reference balance of mountains filling the lower three quarters and grey sky visible above and between ridges. Fine intentional pixel-art texture, subtle limited-palette dithering and coherent readable rock formations; substantially finer detail than the 256x128 reference, no giant pixel blocks and no blurry upscale. Landscape 2:1 panorama, preferably 1536x768. Compose as a seamless horizontally repeating sky strip: left and right image edges must meet with matching terrain heights, lighting, colors and continuous cloud/rock detail. Flat game texture, no frame, no UI, no text, no sun disk, no buildings, no people, no snow, no dramatic colorful lighting. Opaque full image.

The image tool returned RGB PNG, rejected by the packaged Engine loader as `UnsupportedPng`. ImageMagick re-encoded it as 8-bit non-interlaced RGBA with opaque alpha. Every RGB pixel was compared and is unchanged; alpha is 255 everywhere. The raw wrap output is retained as `mountain-sky-wrap-rgb.png`. The provenance check now verifies the Engine-required PNG encoding too.


## Live validation

Native Wolf session `8ab6fac3-3a4b-46a4-82d9-a2a0efb2d7d7`, slot-1, loaded the study and traversed to the courtyard with read-only pose-assisted W/E/controller input. At `(20.138,-0.830,-17.893)`, four quarter-turns inspected the full horizontal panorama. The directly inspected originals showed fine mountain detail and no obvious vertical wrap cut. The existing equirectangular projection stretches clouds near the zenith; this asset change does not alter projection. The first route attempt met the blue recess's higher lip, then used its quarter-unit rim/perimeter and north door successfully. No teleportation or A/B trial was used.

Original captures:
- courtyard: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/8ab6fac3-3a4b-46a4-82d9-a2a0efb2d7d7/5dd4905b-6f26-4356-a6ad-567e0bb75692.png`
- west: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/8ab6fac3-3a4b-46a4-82d9-a2a0efb2d7d7/0dc8004e-a5c2-4e7e-955b-89a18f4a9cc2.png`
- north: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/8ab6fac3-3a4b-46a4-82d9-a2a0efb2d7d7/45dab527-cccc-49c9-bac9-3beddd144f80.png`
- east: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/8ab6fac3-3a4b-46a4-82d9-a2a0efb2d7d7/0d597dd8-0927-466d-b050-3dde784f2845.png`
- full-turn: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/8ab6fac3-3a4b-46a4-82d9-a2a0efb2d7d7/0d86cd8b-3996-401f-adc2-1ee123af1243.png`

The live readout selected `loading-bay/sky/mountain-panorama.png`, 2,540,675 bytes, with matching exact hash and both skyResource/skySelected true. Session cleanup released=true/errors=[]. Receipts are retained in `.runtime/evidence/sky/`. An earlier launch attempt found the server unavailable while the RGB encoding failure was being corrected; it allocated no session and provides no visual evidence.

Final checks passed: C# semantic catalog/build/lifecycle/CoreCLR/NativeAOT (`/tmp/sky-spine-rgba.log`), Angular build (`/tmp/sky-shell.log`), boundary audit (`/tmp/sky-boundary-final.log`), retained-content/provenance including RGBA encoding (`/tmp/sky-provenance-final.log`). The normal demo remains running, audit disabled, with the new sky selected.
