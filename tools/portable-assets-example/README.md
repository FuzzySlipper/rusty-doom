# Portable asset consumer

Small independent Engine product using SDK/runtime `0.1.0-dev.feec788503fe`.
It loads a directional sprite atlas and animated GLB through
`PortableAssetContent`, without a Workbench dependency or descriptor parser.

From this repository root, fetch the verified example jobs:

```sh
workbench --workspace /home/agent/dev/asset-pipeline job fetch 68ff501f38ef --to tools/portable-assets-example/content/sprite
workbench --workspace /home/agent/dev/asset-pipeline job fetch 8edb89227534 --to tools/portable-assets-example/content/model
dotnet build tools/portable-assets-example/PortableExample.csproj -t:StageRustyEngineCoreClrProduct -p:RestoreAdditionalProjectSources=/home/agent/dev/asset-pipeline/.runtime/sdk-feed
```

Run the staged `product.json` using that exact Engine runtime pair. Content is
ignored by Git and can be replaced with your own descriptor/member folders.
`PORTABLE_REPORT=/absolute/file.json` writes consumption facts for headless runs.
The verified run reports 32 frames, four directional walk animations and model
clips `idle` and `walk`; normal Engine sprite playback drives the displayed atlas.
