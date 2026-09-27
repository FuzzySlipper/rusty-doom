# Engine bone attachment consumer

Independent product using SDK/runtime `0.1.0-dev.feec788503fe` and ordinary
Engine APIs. It loads the Workbench-exported descriptor and two GLBs, publishes
`MeshJointAttachment` on `RightHand`, and samples the body's `run` clip at 50%.
There is no Workbench dependency, descriptor parser or bone-follow loop.

From the repository root:

```sh
workbench --workspace /home/agent/dev/asset-pipeline job fetch 9f99d7fff511 --to tools/attachment-example/content
dotnet build tools/attachment-example/AttachmentExample.csproj -t:StageRustyEngineCoreClrProduct -p:RestoreAdditionalProjectSources=/home/agent/dev/asset-pipeline/.runtime/sdk-feed
```

Run the staged product with the matching packaged Engine runtime. Set
`PORTABLE_REPORT=/absolute/report.json` to write the native attachment facts.
The verified run admitted `RightHand`, body/child, local position 0.08,0.02,0 and scale 0.01,0.01,0.01
and pose 0.5, then disposed and exited 0. Content and local runtime reports under
`obj/` are ignored by Git; fetch supplies the source files.
