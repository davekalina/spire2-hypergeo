# Developing

## Build

```powershell
dotnet build .\Hypergeo.csproj
dotnet test .\Hypergeo.Tests\Hypergeo.Tests.csproj
```

`Sts2PathDiscovery.props` finds the game through the Steam registry keys. If it
cannot, copy `Directory.Build.props.example` to `Directory.Build.props` and set
`Sts2Path`.

Building copies `Hypergeo.json`, `Hypergeo.dll`, and `Hypergeo.pdb` into
`<game>/mods/Hypergeo/`. Pass `-p:SkipModInstall=true` to build without
installing. The game must not be running, or the DLL will be locked.

A local copy and a Workshop copy of the same mod id conflict, and the loader
disables one. It compares semantic versions and prefers the local copy when they
are equal, so a development build shadows the published one without renaming
anything.

## Publish to the Steam Workshop

```powershell
.\scripts\package-workshop.ps1
```

That stages `workshop/content/` and prints the `ModUploader.exe upload -w …`
command to run next. See `docs/sts2-modding.md` for the full pipeline and
`workshop/README.md` for the `workshop.json` field reference.

Bump `version` in `Hypergeo.json` and `Version` in `HypergeoCode/MainFile.cs`
together, and write `changeNote` in `workshop/workshop.json` before uploading —
it is the changelog subscribers see. `workshop/workshop-description.txt` is the
source for the page text; copy it into the `description` field.

`workshop/mod_id.txt` appears after the first upload. **Commit it** — it is the
only link between this repository and the published Workshop item.

### Artwork

`workshop/image.png` is the item's thumbnail and must carry that exact name.
It does not appear in the page's gallery, which comes only from
`workshop/previews/`, so a screenshot that should be in both needs a copy in
each. Every image must be under 1 MB.

Gallery order is upload order, not filename order: the uploader matches existing
previews by name and appends new ones. To reorder or insert, upload once with an
empty `previews/` directory — which clears the gallery — then again with the
full set. Deleting the directory instead leaves the gallery untouched.

### Not done yet

- The in-game mod-list image (`res://Hypergeo/mod_image.png`) requires
  `has_pck: true` and a Godot project that exports a `.pck`. This mod has
  neither, so the mod list shows no image. Not required for publishing.
