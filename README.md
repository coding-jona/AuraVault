<div align="center">

# AuraVault

**A native, GPU-rendered password manager for Windows — KeePass-compatible, offline-first, and heavy on visual polish.**

[![CI](https://github.com/coding-jona/AuraVault/actions/workflows/ci.yml/badge.svg)](https://github.com/coding-jona/AuraVault/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![UI: Avalonia 12](https://img.shields.io/badge/UI-Avalonia%2012-782CED)](https://avaloniaui.net/)
[![Vault: KDBX 4.1](https://img.shields.io/badge/Vault-KDBX%204.1-2E7D32)](https://keepass.info/help/kb/kdbx_4.html)
[![License: GPL-3.0](https://img.shields.io/badge/License-GPL--3.0-blue)](LICENSE)

</div>

> **Status: early development — P0–P2.1 complete.**
> The crypto core, a clean-room **KDBX 4.1 reader/writer**, the CSV import pipeline, the password
> generator and search all work headless (driven by the [`auravault` CLI](#cli)). The **Avalonia
> desktop app** now runs: unlock/create, a 3-pane vault browser, **entry editing**, an **in-app
> import wizard**, the aura background, a `Ctrl+K` command palette, live-applied theming, DE/EN.
> Remaining work is tracked in the [roadmap](#roadmap).

---

## What it is

AuraVault stores your logins in a standard **KDBX 4.1** file (Argon2id + ChaCha20 / AES-256 +
HMAC-SHA-256), so the crypto is the same battle-tested format KeePass and its mobile clients use —
nothing home-rolled. On top of that sits a fully native, Skia/GPU-rendered desktop app:

- **Not a web wrapper.** No Electron, no WebView, no Chromium. Avalonia renders its own scene graph.
- **Independent of Windows' own UI stacks.** No WinUI 3, no UWP, no WPF, no Windows App SDK runtime.
- **Offline by default.** Every network feature (breach checks, favicons, browser extension) is
  strictly opt-in.
- **Keyboard-first & menu-driven.** Full menu bar, `Ctrl+K` command palette, shortcut for every action.
- **"Aura" visual layer.** A drifting gradient background, glass surfaces, an intensity slider and a
  real reduced-motion fallback.

### Planned feature set

Entries with folders, tags, custom fields, attachments and per-save history · password generator
(character classes, diceware passphrases, entropy meter) · built-in TOTP / HOTP / Steam ·
instant fuzzy search with saved filters · security dashboard (weak / **reused** / old / expiring /
optionally breached) · many importers (KDBX, Bitwarden, 1Password, LastPass, Chrome/Edge/Firefox,
Apple Passwords, generic CSV/XLSX) · tray icon + global hotkey + auto-type · Windows Hello unlock ·
multi-vault + KDBX merge · automatic rotating backups · German + English.

Imports run through a column-mapping wizard into an encrypted vault — nothing is baked into the binary.

---

## Repository layout

```
src/
  AuraVault.Core/                  UI-independent: crypto, KDBX 4.1 codec, vault model,
                                   generator, TOTP, import, search, backup   (net10.0, no OS APIs)  ✅
  AuraVault.Cli/                   headless CLI: create / import / ls / gen                          ✅
  AuraVault.Platform.Abstractions/ interfaces the app consumes                                       ✅
  AuraVault.Platform.Windows/      Win32: VirtualLock, idle monitor, app paths                       ✅
                                   (Hello, tray, hotkey, auto-type land in P3)
  AuraVault.App/                   Avalonia 12 UI + DI composition root                              ✅
tests/
  AuraVault.Core.Tests/            xUnit v3 — crypto KATs, KDBX round-trip, import, architecture rules
  AuraVault.App.Tests/             Avalonia.Headless — shell flow + aura render smoke
  AuraVault.Integration.Tests/     KeePassXC interop fixtures   (planned)
fixtures/                          sample KDBX / importer inputs
docs/                              plan, ADRs, format notes
```

A NetArchTest rule fails the build if `AuraVault.Core` ever takes a dependency on Avalonia,
WinUI, WPF, WinForms or MAUI.

---

## Build, run & test

Requires the **.NET 10 SDK** (10.0.301 or newer).

```bash
dotnet build
dotnet test                              # 53 tests: Core + headless app
dotnet run --project src/AuraVault.App   # the desktop app
```

Tests run on the **Microsoft.Testing.Platform** runner (opted in via `global.json`).

<a id="cli"></a>
### CLI quickstart

```bash
dotnet run --project src/AuraVault.Cli -- create Personal.kdbx
dotnet run --project src/AuraVault.Cli -- import Personal.kdbx --dir path\to\csvs        # dry run
dotnet run --project src/AuraVault.Cli -- import Personal.kdbx --dir path\to\csvs --commit
dotnet run --project src/AuraVault.Cli -- ls Personal.kdbx paypal
dotnet run --project src/AuraVault.Cli -- gen --passphrase --words 6
```

`import` is a dry run until `--commit`; it prints New / Duplicate / Updated / Skipped counts per file.
`ls` hides passwords unless `--show-passwords`. The master password comes from a masked prompt,
`--password-env NAME`, or `--password-stdin`.

### What's verified today

- ChaCha20 against the RFC 8439 §2.3.2 keystream vector
- KDBX 4.1 write → read round-trip across **ChaCha20 / AES-256-CBC × GZip / none**, preserving
  groups, protected & custom fields, tags, history and Unicode
- A wrong master key raises `KdbxIntegrityException` and yields **no** partial plaintext; a single
  flipped ciphertext byte is rejected (HMAC block verification)
- `SecureBuffer` zeroes its pinned backing array on dispose; `VariantDictionary` round-trips every type
- Import pipeline: dedupe strategies, timestamp parsing, idempotent re-runs, the iPhone-recovery presets
- Generator: class constraints, look-alike exclusion, diceware entropy
- Headless app: creating a vault completes on the UI thread without crashing; `MainWindow` renders
  across aura intensity 0 / 0.7 / 1 and reduced-motion

---

## Design notes

- **Clean-room KDBX.** KeePass's own `KeePassLib` is GPL-2.0-only and WinForms-entangled, so it can't
  live in a UI-independent `Core`. The KDBX *file format* is unencumbered; the codec here is built on
  documented format notes plus audited primitives (`Konscious` Argon2, `BouncyCastle` ChaCha20/Salsa20,
  BCL AES/HMAC/GZip) and validated against real KeePassXC output. See
  [`docs/adr/0001-kdbx-codec.md`](docs/adr/0001-kdbx-codec.md).
- **Secrets never touch `string`.** Plaintext lives in pinned, zero-on-dispose `SecureBuffer`s
  (VirtualLock-backed on Windows); protected fields stay obfuscated until the moment of use. Full
  in-process protection on a managed runtime is impossible — the residual risk is documented, not hidden.
- **Offline-first.** No network code paths ship enabled.

The full plan — 17 self-contained sectors, phased P0–P5 — lives in [`docs/plan.md`](docs/plan.md).

---

## Roadmap

| Phase | Scope | State |
|------:|-------|:-----:|
| **P0** | Crypto core + KDBX 4.1 read/write + vault model + tests | ✅ done |
| **P1** | CSV import pipeline · generator · instant search · backup · `auravault` CLI | ✅ done |
| **P2** | Avalonia shell · aura layer · theming · command palette · unlock + vault browser · DE/EN | ✅ done |
| **P2.1** | Entry editing (fields / custom fields / tags / history) · in-app import wizard · headless tests | ✅ done |
| **P2.2** | Dockable panels · onboarding wizard · full Preferences pages · KeePassXC interop test in CI | ▫ next |
| **P3** | Security runtime (auto-lock, clipboard clear) · TOTP · security dashboard · tray + hotkey + auto-type · Windows Hello | ▫ |
| **P4** | More importers/exporters · multi-vault · KDBX merge | ▫ |
| **P5** | Packaging (Velopack / MSIX / portable) · signing · hardening · optional browser extension | ▫ |

---

## Security

Pre-release software, **not** independently audited. Don't trust it with your only copy of a
credential yet. Found a vulnerability? Open a private security advisory rather than a public issue.

## License

**GPL-3.0** — see [`LICENSE`](LICENSE).

## Acknowledgements

The KDBX 4 format is designed by the KeePass project; interop is verified against
[KeePassXC](https://keepassxc.org/). Argon2 via
[Konscious.Security.Cryptography](https://github.com/kmaragon/Konscious.Security.Cryptography),
stream ciphers via [Bouncy Castle](https://www.bouncycastle.org/), UI by
[Avalonia](https://avaloniaui.net/), diceware wordlist by the
[EFF](https://www.eff.org/dice) (CC-BY-3.0-US).
