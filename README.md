<div align="center">

# AuraVault

**A native, GPU-rendered password manager for Windows — KeePass-compatible, offline-first, and heavy on visual polish.**

[![CI](https://github.com/OWNER/AuraVault/actions/workflows/ci.yml/badge.svg)](https://github.com/OWNER/AuraVault/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![UI: Avalonia 11](https://img.shields.io/badge/UI-Avalonia%2011-782CED)](https://avaloniaui.net/)
[![Vault: KDBX 4.1](https://img.shields.io/badge/Vault-KDBX%204.1-2E7D32)](https://keepass.info/help/kb/kdbx_4.html)
[![License](https://img.shields.io/badge/License-TBD-lightgrey)](#license)

</div>

> **Status: early development (P0 complete).**
> The cryptographic core and a clean-room **KDBX 4.1 reader/writer** are implemented and tested,
> and round-trip against KeePassXC / KeePass 2.x. The Avalonia UI, importers, and OS integration
> are being built sector by sector — see the [roadmap](#roadmap).

---

## What it is

AuraVault stores your logins in a standard **KDBX 4.1** file (Argon2id + ChaCha20 / AES-256 +
HMAC-SHA-256), so the crypto is the same battle-tested format KeePass and its mobile clients use —
nothing home-rolled. On top of that sits a fully native, Skia/GPU-rendered desktop app:

- **Not a web wrapper.** No Electron, no WebView, no Chromium. Avalonia renders its own scene graph.
- **Independent of Windows' own UI stacks.** No WinUI 3, no UWP, no WPF, no Windows App SDK runtime.
- **Offline by default.** Every network feature (breach checks, favicons, browser extension) is
  strictly opt-in.
- **Feels like a mature app.** Full menu bar, `Ctrl+K` command palette, dockable/detachable panels,
  context menus everywhere, a large preferences dialog, keyboard-first navigation.
- **"Aura" visual layer.** A GPU shader mesh-gradient background, glass/blur, glow, 120 fps target,
  with an intensity slider and a proper reduced-motion fallback.

### Planned feature set

Entries with folders, tags, custom fields, attachments and per-save history · password generator
(character classes, diceware passphrases, entropy meter) · built-in TOTP / HOTP / Steam ·
instant fuzzy search with saved filters · security dashboard (weak / **reused** / old / expiring /
optionally breached) · many importers (KDBX, Bitwarden, 1Password, LastPass, Chrome/Edge/Firefox,
Apple Passwords, generic CSV/XLSX) · tray icon + global hotkey + auto-type · Windows Hello unlock ·
multi-vault + KDBX merge · automatic rotating backups · German + English.

The first-run wizard can import an existing password export (CSV / XLSX) straight into a fresh,
encrypted vault via a column-mapping step — nothing is baked into the binary.

---

## Repository layout

```
src/
  AuraVault.Core/                  UI-independent: crypto, KDBX 4.1 codec, vault model,
                                   generator, TOTP, import, search   (net10.0, no OS APIs)
  AuraVault.Platform.Abstractions/ interfaces the app consumes         (planned)
  AuraVault.Platform.Windows/      Win32 + WinRT: Hello, tray, hotkey, auto-type, session hooks (planned)
  AuraVault.App/                   Avalonia UI + composition root      (planned)
tests/
  AuraVault.Core.Tests/            xUnit v3 — crypto KATs, KDBX round-trip, architecture rules
  AuraVault.Integration.Tests/     KeePassXC interop fixtures          (planned)
fixtures/                          sample KDBX / importer inputs
docs/                              ADRs, manual E2E script, format notes
```

A NetArchTest rule fails the build if `AuraVault.Core` ever takes a dependency on Avalonia,
WinUI, WPF, WinForms or MAUI.

---

## Build & test

Requires the **.NET 10 SDK** (10.0.301 or newer). No other tooling needed for `Core` + tests.

```bash
dotnet build
dotnet test
```

Tests run on the **Microsoft.Testing.Platform** runner (opted in via `global.json`), which the
.NET 10 SDK requires for xUnit v3.

### What's verified today

- ChaCha20 against the RFC 8439 §2.3.2 keystream vector
- KDBX 4.1 write → read round-trip across **ChaCha20 / AES-256-CBC × GZip / none**, preserving
  groups, protected & custom fields, tags, history and Unicode
- A wrong master key raises `KdbxIntegrityException` and yields **no** partial plaintext
- A single flipped ciphertext byte is rejected (HMAC block verification)
- `SecureBuffer` zeroes its pinned backing array on dispose
- `VariantDictionary` round-trips every KDBX type

---

## Design notes

- **Clean-room KDBX.** KeePass's own `KeePassLib` is GPL-2.0 and WinForms-entangled, so it can't
  live in `Core`. The KDBX *file format* is unencumbered; the codec here is built on documented
  format notes plus audited primitives (`Konscious` Argon2, `BouncyCastle` ChaCha20/Salsa20, BCL
  AES/HMAC/GZip) and validated against real KeePassXC output. See
  [`docs/adr/0001-kdbx-codec.md`](docs/adr/0001-kdbx-codec.md).
- **Secrets never touch `string`.** Plaintext lives in pinned, zero-on-dispose `SecureBuffer`s;
  protected fields stay obfuscated until the moment of use. Full in-process protection on a managed
  runtime is impossible — the residual risk is documented, not hidden.
- **Offline-first.** No network code paths ship enabled. Breach checks use HIBP k-anonymity (only a
  5-character SHA-1 prefix ever leaves the machine) and are opt-in.

The full plan — 17 self-contained sectors, phased P0–P5 — lives in
[`docs/plan.md`](docs/plan.md).

---

## Roadmap

| Phase | Scope | State |
|------:|-------|:-----:|
| **P0** | Crypto core + KDBX 4.1 read/write + vault model + tests | ✅ done |
| **P1** | CSV/XLSX import pipeline · entry management UI · generator · instant search | ▫ next |
| **P2** | Aura render system · menu bar + command palette · docking · preferences · DE/EN · onboarding | ▫ |
| **P3** | Security runtime · TOTP · security dashboard · tray + hotkey + auto-type · Windows Hello · backups | ▫ |
| **P4** | More importers/exporters · multi-vault · KDBX merge | ▫ |
| **P5** | Packaging (Velopack / MSIX / portable) · signing · hardening · optional browser extension | ▫ |

---

## Security

This is pre-release software and has **not** been independently audited. Do not trust it with your
only copy of a credential yet. If you find a vulnerability, please open a private security advisory
rather than a public issue.

## License

Not yet chosen. Until a `LICENSE` file is added, all rights are reserved by the author.

## Acknowledgements

The KDBX 4 format is designed by the KeePass project. Interop is verified against
[KeePassXC](https://keepassxc.org/). Argon2 via
[Konscious.Security.Cryptography](https://github.com/kmaragon/Konscious.Security.Cryptography),
stream ciphers via [Bouncy Castle](https://www.bouncycastle.org/).
