# Releasing `SeatLayer` to NuGet

Maintainer runbook.

> **The rule that shapes everything below: a version pushed to NuGet.org can
> never be re-pushed, and never be truly deleted.** `1.0.0` is burned the instant
> it is accepted, even if you unlist it thirty seconds later. Inspect the package
> before you push, not after.

---

## 1. Accounts and credentials needed

| What | Where | Notes |
|---|---|---|
| NuGet.org account | https://www.nuget.org | Signs in with a Microsoft account (personal or Entra). |
| 2FA on that account | Microsoft account security | NuGet.org requires MFA for package management. Set it up before you need it. |
| API key | https://www.nuget.org/account/apikeys | See scope below. |
| GitHub repo `seatlayer/seatlayer-dotnet` | https://github.com/seatlayer/seatlayer-dotnet | Not required to publish, but `RepositoryUrl` in the package points at it, and Source Link embeds its commit — a 404 there makes the package look abandoned. |

### API key scope — set it narrowly

When creating the key at **Account → API Keys → Create**:

- **Key name**: `seatlayer-dotnet-publish`
- **Select scopes**: `Push` → **Push new packages and package versions**
  (The first push of `SeatLayer` *creates* the package ID, so a
  "Push only new versions of existing packages" key will fail on release one.
  Narrow it to that after the ID exists, if you like.)
- **Glob pattern**: `SeatLayer` — or `SeatLayer*` if you plan on
  `SeatLayer.AspNetCore` later. Never leave it as `*`.
- **Expires in**: 365 days max. Calendar a rotation.

Treat the key as a secret equal to your account password: it can push a package
under your identity to every consumer who trusts it. Store it in the password
manager, never in the repo, never in a shell history file. For CI, use
`NUGET_API_KEY` as a GitHub Actions secret.

**Prerequisite check (verified 2026-08-04):** `SeatLayer` currently 404s on
NuGet.org — the ID is unclaimed. The first successful push claims it permanently.
IDs are case-insensitive for resolution but the casing you first push is the
casing displayed forever.

---

## 2. Publish, in order

### 2.0 Point the tag at the release commit

The `v0.1.0` tag was created before the packaging fixes (deterministic build,
Source Link, `Copyright`). NuGet does **not** read the tag — `Version` in the
`.csproj` is the source of truth, so a stale tag will not publish the wrong
bits the way it would on Packagist. But the tag is what you will `git checkout`
to reproduce this build later, so fix it anyway:

```bash
git tag -f v0.1.0 main
git push origin main
git push --force origin v0.1.0   # plain `--tags` will not move an existing remote tag
```

### 2.1 Pre-flight (all must be green)

```bash
dotnet build -c Release     # warnings are errors; expect 0 Warning(s) 0 Error(s)
dotnet test  -c Release --no-build   # expect: Passed! … Passed: 33, Failed: 0
```

### 2.2 Pack and inspect before pushing

```bash
rm -rf ./artifacts
dotnet pack src/SeatLayer/SeatLayer.csproj -c Release -o ./artifacts
unzip -l ./artifacts/SeatLayer.0.1.0.nupkg
```

Expected contents — anything else is a bug:

```
SeatLayer.nuspec
lib/net8.0/SeatLayer.dll
lib/net8.0/SeatLayer.xml      <- XML docs; IntelliSense is blank without this
README.md                     <- rendered on the package page
_rels/.rels, [Content_Types].xml, package/services/…   (OPC plumbing)
```

Check specifically that:

- there is **no** `SeatLayer.Tests.dll` anywhere (the test project sets
  `IsPackable=false`; if that regressed, you would be shipping xunit to consumers);
- the target folder is `lib/net8.0/`, not `lib/$(TargetFramework)/` or `content/`;
- `SeatLayer.0.1.0.snupkg` was produced alongside, containing
  `lib/net8.0/SeatLayer.pdb`;
- `<dependencies>` in the nuspec is an **empty** group. This SDK deliberately has
  zero package references; a dependency appearing here means someone added one.

Read the nuspec itself if anything looks off:

```bash
unzip -p ./artifacts/SeatLayer.0.1.0.nupkg SeatLayer.nuspec
```

### 2.3 Push

```bash
dotnet nuget push ./artifacts/SeatLayer.0.1.0.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key "$NUGET_API_KEY"
```

The matching `.snupkg` in the same folder is detected and pushed automatically.
If it is not, push it explicitly with the same command against the `.snupkg`.

After the push, the package sits in **validation** for roughly 5–15 minutes
before it is indexed and installable. A `200 OK` from the push means *accepted*,
not *live*. Do not push again because it "did not work" — that is how you burn a
version number.

---

## 3. Verify a clean install from NuGet

In an **empty** directory, and explicitly against nuget.org only, so a local
package cache or a `~/.nuget` fallback source cannot fake a success:

```bash
mkdir /tmp/sl-verify && cd /tmp/sl-verify
dotnet new console
dotnet nuget locals http-cache --clear
dotnet add package SeatLayer --version 0.1.0 \
  --source https://api.nuget.org/v3/index.json
```

Then prove it actually works:

```csharp
// Program.cs
using SeatLayer;

var client = new SeatLayerClient("sk_test_placeholder");
Console.WriteLine(client.Mode);            // expect: test

try { _ = new SeatLayerClient("pk_test_x"); Console.WriteLine("BAD: accepted pk_"); }
catch (Exception e) { Console.WriteLine($"OK: {e.Message}"); }
```

```bash
dotnet run
```

Also confirm the consumer-facing extras landed:

- **IntelliSense**: hover `SeatLayerClient` in an IDE — the XML summary should
  appear. If it does not, `SeatLayer.xml` is missing from the package.
- **Package page**: README renders, license shows MIT, repository link resolves.

A live smoke test needs a real `sk_test_…` key:

```csharp
var client = new SeatLayerClient(Environment.GetEnvironmentVariable("SEATLAYER_SECRET_KEY")!);
var page = await client.Events.ListAsync(new EventListRequest { Limit = 1 });
Console.WriteLine(page.Items.Count);
```

---

## 4. If it goes wrong

| Situation | What you can do | Irreversible? |
|---|---|---|
| Broken build pushed | **Unlist** it, push a fixed `0.1.1`. | The `0.1.0` number is gone forever. |
| Wrong package ID | Unlist every version; push under the correct ID. | The wrong ID stays claimed by you, permanently. |
| Secret embedded in the package | Unlist immediately, **rotate the secret**, then contact NuGet support for a hard delete. | Assume full disclosure — the package was mirrored the moment it indexed. |
| Pushed to the wrong feed | Nothing to undo on nuget.org if it never got there. | — |

The mechanics you need to have internalised **before** the first push:

- **There is no delete.** NuGet.org offers *unlist*, not delete. Package page →
  **Manage package → Listing** → untick the version.
- **Unlisting does not remove the package.** It hides it from search and from
  floating ranges, but `dotnet add package SeatLayer --version 0.1.0` still
  restores it, forever. This is deliberate: it keeps other people's builds
  reproducible. Unlist is damage *limitation*, not removal.
- **A pushed version number can never be re-pushed**, even after unlisting. Fix
  forward with a new version. Always.
- Hard deletion happens only via NuGet support, only for genuine emergencies
  (leaked secrets, malware, licence violation), and is not guaranteed or fast.

Because none of this can be walked back, §2.2 — inspecting the `.nupkg` before
pushing — is the actual safety mechanism. Treat it as mandatory.

---

## 5. Target framework: what `net8.0` costs

`<TargetFramework>net8.0</TargetFramework>` is a single-target package. The
consequences, stated plainly:

- **Reaches**: .NET 8, 9, 10 and later. Newer runtimes happily consume a
  `net8.0` library, so nobody on a current runtime is excluded.
- **Excludes**: .NET Framework 4.x, Mono, Unity, and .NET 6/7. A .NET Framework
  4.8 shop — still common in enterprise ticketing back-offices, which is exactly
  this SDK's market — **cannot install this package at all**. They get
  `NU1202: Package SeatLayer 0.1.0 is not compatible with net48`.
- **Support horizon**: .NET 8 LTS support ends 2026-11-10. That does not break
  the package (a `net8.0` library keeps working on .NET 10), but it is worth
  knowing that the floor is an about-to-be-unsupported runtime.

If .NET Framework reach is ever wanted, the fix is multi-targeting rather than
lowering the floor:

```xml
<TargetFrameworks>net8.0;netstandard2.0</TargetFrameworks>
```

That is not free — `netstandard2.0` has no `System.Text.Json` in the box, so it
would add the first-ever package dependency and cost the "no dependencies"
property the README advertises. It is a product decision, not a packaging one,
and should be made deliberately rather than during a release.

---

## 6. Next version

Bump `<Version>` in `src/SeatLayer/SeatLayer.csproj`, update `CHANGELOG.md`,
commit, tag, then repeat §2.1–2.3.

Keep this SDK's number aligned with the other **server** SDKs
(`@seatlayer/server`, `seatlayer` on PyPI/RubyGems, `seatlayer-java`,
`seatlayer-go`, `seatlayer/seatlayer-php`), which are all on the 0.1.x line. The
0.4x.x line belongs to the **browser** SDK family (`@seatlayer/js`, `/react`,
`/vue`, `/angular`) and is a different product with its own cadence — do not
align to it.

For a prerelease, use a suffix — these are pushable alongside and are not
shown by default in the UI:

```bash
dotnet pack -c Release -o ./artifacts -p:Version=0.2.0-rc.1
```
