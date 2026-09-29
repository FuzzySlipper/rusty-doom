# Engine bone attachment consumer

Independent product using the repository's pinned Engine pair and ordinary
Engine APIs. It loads the Workbench-exported descriptor and two GLBs, publishes
`MeshJointAttachment` on `RightHand`, and samples the body's `run` clip at 50%.
There is no Workbench dependency, descriptor parser or bone-follow loop.

From the repository root:

```sh
workbench --workspace /home/agent/dev/asset-pipeline job fetch f5af8e42eb1e --to tools/attachment-example/content
rusty build --project tools/attachment-example/AttachmentExample.csproj
```

Run the staged product with the matching packaged Engine runtime. Set
`PORTABLE_REPORT=/absolute/report.json` to write the native attachment facts.
The verified run admitted `RightHand`, body/child, local position 0.0008,0.0002,0 and scale 0.01,0.01,0.01
and pose 0.5, then disposed and exited 0. Content and local runtime reports under
`obj/` are ignored by Git; fetch supplies the source files.
