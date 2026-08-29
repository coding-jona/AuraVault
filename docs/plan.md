# AuraVault — Windows-Passwort-Manager (Avalonia / .NET 10)

## Context

Aus dem heute wiederhergestellten iPhone‑5‑Schlüsselbund liegen ~1.400 entschlüsselte Einträge als
CSV/XLSX unter `C:\Users\jonag\Apple\MobileSync\Backup\entschluesselt\` vor. Der Nutzer will daraus
keinen Wegwerf‑Export, sondern eine dauerhafte, eigenständige Windows‑Anwendung: ein moderner,
sehr flüssiger, optisch aufwändiger („Aura") Passwort‑Manager mit dem Funktionsumfang und der
Menü­tiefe einer über Jahre gereiften Desktop‑App — **nativ GPU‑gerendert, kein Web‑Wrapper,
unabhängig von WinUI/WPF/UWP**. Die heute geborgenen Passwörter sollen **nicht hardcodet**,
sondern zur Laufzeit über einen Import‑Assistenten in den verschlüsselten Vault gelesen werden.

Ergebnis: eine signierte, installierbare App (`AuraVault`) mit KeePass‑kompatiblem Vault, vollem
Feature‑Set (Einträge/Ordner/Tags/Historie/Anhänge, Generator, TOTP, Suche, Security‑Dashboard,
Tray + Global‑Hotkey + Auto‑Type, Windows Hello, viele Importer, Backups, DE/EN), und einer
stark themebaren Aura‑Optik mit 120‑fps‑Ziel und Reduced‑Motion‑Fallback.

### Bestätigte Rahurbedingungen (Nutzerentscheidungen)
- **UI‑Stack:** Avalonia UI 11.3.x auf **.NET 10**, C#, MVVM (CommunityToolkit.Mvvm). Eigener
  Skia/GPU‑Renderer, keine WinUI/WPF/UWP/WebView‑Abhängigkeit. Auf diesem Rechner ohne Setup lauffähig
  (.NET SDK 10.0.301 vorhanden; Qt/CMake/MSVC nicht vorhanden).
- **Vault‑Format:** KDBX 4.1 (KeePass‑kompatibel) — Argon2id + ChaCha20/AES‑256 + HMAC‑SHA‑256.
- **Netzwerk:** offline per Default; jede Online‑Funktion (HIBP‑Leak‑Check via k‑Anonymität, Favicons,
  Browser‑Erweiterung) strikt opt‑in.
- **Umfang:** voller Funktionsumfang, aber **jeder Sektor unten ist als eigenständiger Teilplan**
  formuliert (eigener Zweck, eigene Dateien, eigene Abnahme). Die Baureihenfolge (Phasen P0–P5) bündelt
  die Sektoren nur.
- **Versionen:** immer die aktuelle Stable‑Version jedes Pakets zum Implementierungszeitpunkt
  (Nutzer­vorgabe „keine veralteten Versionen"). Die unten genannten Versionen sind Mindest­stände
  (Stand 08/2026).
- Alternativer Ausstieg, falls die Aura‑Optik in Avalonia die 120‑fps nicht hält: nur
  `AuraVault.App` wird auf **Qt 6.9 + QML** neu gebaut; `Core`/`Platform` bleiben. Nicht der Startpfad.

---

## Globale Architektur (gilt für alle Sektoren)

### Solution‑Layout
```
AuraVault.sln
├─ src/
│  ├─ AuraVault.Core/                    net10.0   — keine UI, keine OS-APIs, keine I/O-Policy
│  ├─ AuraVault.Platform.Abstractions/   net10.0   — Interfaces, die App konsumiert
│  ├─ AuraVault.Platform.Windows/        net10.0-windows10.0.19041.0 — Win32 + WinRT
│  ├─ AuraVault.App/                     net10.0-windows10.0.19041.0 — Avalonia, Composition Root
│  └─ AuraVault.BrowserBridge/           net10.0-windows — optional (Sektor 14)
├─ tests/  AuraVault.Core.Tests | .Integration.Tests | .App.HeadlessTests
├─ fixtures/  kdbx/ · importers/ · iphone/
├─ build/   velopack · msix · inno
└─ docs/    ADRs · manual-e2e.md · format-notes
```

### Abhängigkeitsregel (per Architektur‑Test erzwungen — NetArchTest)
`Core` → nur BCL + Krypto‑Pakete. `App` → `Core` + `Platform.Abstractions` + Avalonia.
`Platform.Windows` → `Core` + `Platform.Abstractions`. `App` referenziert `Platform.Windows` **nur**
im Composition Root für die DI‑Registrierung. `Core` darf **kein** Avalonia/Windows/WPF/WinUI laden.

### `Directory.Build.props`
`LangVersion=latest`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-Recommended`,
deterministische Builds, `InvariantGlobalization=false` (de‑DE nötig), `EnableTrimAnalyzer` auf `Core`.

### Schlüssel‑Pakete (Mindeststände; zur Implzeit auf aktuell heben)
Avalonia `11.3.x` (`Avalonia.Desktop`, `.Themes.Fluent`, `.Diagnostics` dev) · SkiaSharp `3.x`
(transitiv) · `CommunityToolkit.Mvvm 8.4.x` · `Microsoft.Extensions.{DependencyInjection,Hosting,
Configuration}* 10.x` · `Dock.Avalonia` + `Dock.Model.Mvvm 11.3.x` · `Avalonia.Controls.TreeDataGrid` ·
`Serilog 4.x` (+File/Debug‑Sinks) · `Konscious.Security.Cryptography.Argon2 1.3.x` ·
`BouncyCastle.Cryptography 2.5.x` · `Otp.NET 1.4.x` · `QRCoder 1.6.x` · `ZXing.Net 0.16.x` ·
`CsvHelper 33.x` · `Sylvan.Data.Excel 0.4.x` (Fallback `ClosedXML`) · `Microsoft.Data.Sqlite 9.x` +
`SQLitePCLRaw.bundle_e_sqlcipher 2.1.x` (optionaler Such‑Cache) · `Microsoft.Windows.CsWin32 0.3.x` ·
`Microsoft.Windows.CsWinRT 2.2.x` (Windows‑Hello‑Projektionen, **nicht** Windows App SDK) ·
`Quickenshtein 1.5.x` · `System.Text.Json 10.x` · `Velopack` (Update/Installer) ·
Tests: `xunit.v3`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`, `Verify.Xunit`, `AwesomeAssertions`
(freier FluentAssertions‑Fork — FA v8 ist kommerziell), `Avalonia.Headless.XUnit`, `NetArchTest`.

**Verboten als (auch transitive) Abhängigkeit:** WPF, WinUI, UWP, WebView2, Windows App SDK‑Runtime.

### Daten‑Ablage (nicht‑geheim = außerhalb der KDBX)
- Vault (einzige Stelle für Geheimnisse): `%USERPROFILE%\Documents\AuraVault\Personal.kdbx`
- `%APPDATA%\AuraVault\`: `settings.json` (schema‑versioniert, atomarer Write), `keybindings.json`,
  `layout.json` (Dock), `recent.json`, `logs\aura-YYYYMMDD.log` (Serilog + Redaction‑Enricher).
- `%LOCALAPPDATA%\AuraVault\`: `hello.bin` (DPAPI‑CurrentUser‑geschützter 32‑B‑Zufall, per Hello
  entsperrt, optionaler Faktor), `Backups\<vaultId>\<UTC>.kdbx` + `BackupCatalog.json`,
  `Cache\<vaultId>.search.db` (optional, SQLCipher, Key via HKDF aus Master‑Key, **default aus**),
  `probe.json` (GPU‑Frame‑Pacing‑Ergebnis). `vaultId` = SHA‑256 des KDBX‑Header‑Seeds.
- **Kein** Geheimnis wird je außerhalb der KDBX geschrieben.

---

## SEKTOR 1 — Fundament, Krypto‑Kern & KDBX‑Codec

**Zweck:** Ein korrekter, mit KeePassXC/KeePass 2.x interoperabler verschlüsselter Vault — Basis für alles.

**Scope:** Solution‑Skelett, CI (Build+Test+Format+Architektur‑Test), Serilog, DI/Composition‑Root,
leeres Avalonia‑Fenster. Krypto‑Primitive. **KDBX‑4.1‑Read+Write** (Clean‑Room), KDBX‑3.1 read‑only.
Vault‑Modell. `VaultManager`/`VaultSession`, Auto‑Lock (nur Idle‑Timer in diesem Sektor).

**Kernelemente & Dateien**
- `AuraVault.Core.Cryptography`: `IKdf` + `Argon2idKdf` + `AesKdf`(read); `SecureBuffer : IDisposable`
  (gepinnt, `VirtualLock` via Platform, `CryptographicOperations.ZeroMemory` beim Dispose);
  `readonly struct ProtectedValue` (Ciphertext + Inner‑Stream‑Key‑Ref; `Reveal()→SecureBuffer`);
  `MasterKeyComposer` (Passwort‑`SecureBuffer` + optional Keyfile‑Hash + optional Hello‑Secret →
  Composite‑Key nach KDBX‑Regeln); `CryptoRandom`; dünne Wrapper `Aes256Cbc`, `ChaCha20` (BouncyCastle),
  `HmacSha256`, `Sha512`.
- `AuraVault.Core.Kdbx`: **`src/AuraVault.Core/Kdbx/Kdbx4Codec.cs`** (Keystein der Korrektheit),
  `IKdbxCodec`, `KdbxHeaderReader/Writer`, `InnerHeaderReader/Writer`, `IInnerRandomStream` →
  `ChaCha20InnerStream` / `Salsa20InnerStream`, `KdbxDatabase`, `KdbxIntegrityException`,
  `KdbxFormatException`.
- `AuraVault.Core.Model`: `Vault` (Root‑`Group`, `RecycleBinUuid`, `VaultMetadata`, `DeletedObject[]`);
  `Group`; `Entry` (`Fields: IDictionary<string,ProtectedValue|string>`, `Tags`, `Attachments`,
  `History: Entry[]`, `Times`, `AutoType`, `Totp`, `IsFavorite`); Standardfelder
  `Title/UserName/Password/URL/Notes`; `EntryTimes`; `Attachment` (Ref in Binary‑Pool);
  `TotpSettings`; `DeletedObject`.
- `AuraVault.Core.Vaults`: `VaultManager`, `VaultSession` (entschlüsselter `Vault` + Dirty‑Tracking +
  `SaveAsync`), `RecentVaultsList`, `AutoLockController` (Idle).
- Minimal‑UI: Vault anlegen (nur Passwort), öffnen, Baum + flache Liste (unvirtualisiert ok),
  Eintrag ansehen/bearbeiten/speichern.
- **ADR „KDBX‑Bibliothek"**: Clean‑Room `Kdbx4Codec` vs. `KpcLib`‑Fallback — Entscheidung hier
  festhalten (KeePassLib ist GPL‑2.0 + WinForms‑verwoben → nicht in `Core`).

**Abnahme:** In der App `Personal.kdbx` anlegen, 3 Einträge, speichern, schließen, wieder öffnen;
dieselbe Datei unverändert in **KeePassXC** und **KeePass 2.x** öffnen; eine KeePassXC‑erstellte Datei
in AuraVault öffnen. Alle Krypto‑Pfade mit Known‑Answer‑Vektoren getestet.

**Verifikation:** Krypto‑KATs (Argon2id, ChaCha20/Salsa20, HMAC‑SHA‑256, AES‑256‑CBC);
`MasterKeyComposer`‑Kombis; `SecureBuffer`‑Zeroing (Backing‑Array nach Dispose prüfen); korrupte HMAC →
`KdbxIntegrityException`; falscher Key → sauberer Fehler, **kein** Teil‑Klartext; Architektur‑Test:
`Core` lädt kein Avalonia/Windows.

---

## SEKTOR 2 — Import‑Pipeline & iPhone‑Preset (heute geborgene Passwörter, nicht hardcodet)

**Zweck:** Die vorhandenen Recovery‑Dateien zur Laufzeit sauber in den Vault bringen; generische
CSV/XLSX‑Import‑Maschine für spätere Quellen.

**Datenlage (bestätigt, UTF‑8 mit BOM):** `1_logins_web.csv` `domain,benutzer,passwort,pfad,geaendert,erstellt`
(268) · `2_wlan.csv` `wlan_name,passwort,geaendert` (26) · `3_app_passwoerter.csv`
`dienst,benutzer,passwort,gruppe,geaendert` (64) · `4_tokens_und_system.csv`
`kategorie,dienst,benutzer,gruppe,wert_laenge` (797, **keine Geheimspalte**) · `5_veraltet_geloescht.csv`
`kategorie,dienst,benutzer,passwort,geloescht_am` (95) · `passwoerter.csv` (Voll‑Dump, 1475) ·
`iPhone5_Passwoerter.xlsx` (Sheets Web‑Logins/WLAN/App‑Passwoerter/Veraltet‑geloescht/Tokens‑System).

**Kernelemente & Dateien**
- `AuraVault.Core.Import`: `IImporter`, **`ImportPipeline`** (Quelle → Row‑Modell → `ColumnMap` →
  Transform → `DedupeResolver` → `ImportPreview` → Commit in `Vault`); `ITabularSource` → `CsvSource`
  (BOM/Delimiter/Encoding‑Erkennung) / `XlsxSource` (pro Sheet); `ColumnMap` mit Transforms
  `TrimUrlScheme` / `DomainToTitle` / `ParseTimestamp(format,culture)` / `ConstantGroup` / `SplitTags`;
  `ColumnMappingProfile` (serialisierbar); **`src/AuraVault.Core/Import/IPhoneRecoveryPreset.cs`**;
  `DedupeResolver` (`IEqualityComparer<Entry>` über normalisierten URL‑Host + Username + Passwort‑Hash;
  Strategien `Skip`/`Merge`/`KeepBoth`); `ImportPreview` (`New|Duplicate|Updated|Skipped`, Zielgruppe
  pro Zeile editierbar).
- **iPhone‑Preset‑Mapping:**
  - `1_logins_web` / Sheet `Web-Logins`: `domain`→`Title` (Registrable Domain) **und** `URL`
    (`https://`+domain); `benutzer`→`UserName`; `passwort`→`Password`; `pfad`→URL‑Pfad + in `Notes`;
    `geaendert`→`Times.LastModified`; `erstellt`→`Times.Created`. Gruppe `Import/iPhone/Web`.
  - `2_wlan` / `WLAN`: `wlan_name`→`Title`; `passwort`→`Password`; `geaendert`→`LastModified`;
    Gruppe `Import/iPhone/Wi-Fi`; Tag `wifi`.
  - `3_app_passwoerter` / `App-Passwoerter`: `dienst`→`Title`; `benutzer`→`UserName`;
    `passwort`→`Password`; `gruppe`→Unterordner `Import/iPhone/Apps/<gruppe>` **und** Tag;
    `geaendert`→`LastModified`.
  - `5_veraltet_geloescht` / `Veraltet-geloescht`: Zeilen → Vault‑**Papierkorb**, Tag `legacy`,
    `geloescht_am`→`DeletionTime`.
  - `4_tokens_und_system` / `Tokens-System`: **Metadaten‑only** unter `Import/iPhone/Reference`,
    `wert_laenge`→Custom‑Feld `value-length`, `Notes` erklärt „kein Geheimnis vorhanden"; aus
    Health/Reuse‑Statistik ausgeschlossen.
  - Advanced: `passwoerter.csv` Voll‑Dump (`kategorie`/`typ`→Gruppe+Tag, `server`+`port`+`pfad`→`URL`).
- **Import‑Assistent‑UI** (`AuraVault.App`): Preset oder „benutzerdefiniert" wählen →
  **Spalten‑Mapping‑Grid** mit Live‑Beispielzeilen → Zeitstempel‑Format/Kultur (de‑DE default,
  `dd.MM.yyyy`/ISO auto) → Dedupe‑Strategie → **Preview + Bestätigen** (Zähler + Zielgruppe pro Zeile) →
  Commit → **„Quelldateien shreddern?"** (Sektor 11 `IFileShredder`, listet exakt welche Dateien,
  **Default aus**, explizite Bestätigung; Alternative „in Papierkorb verschieben").
- Dateien werden **zur Laufzeit von der Platte gelesen**; nichts wird einkompiliert. Fehlt der Ordner,
  startet der Assistent auf „Datei wählen".

**Abnahme:** Auf sauberem Rechner: Onboarding → „Aus iPhone‑Wiederherstellung importieren" → alle drei
kuratierten Dateien (oder XLSX) → korrekte Preview → ~358 Einträge in den richtigen Gruppen mit
Zeitstempeln → Re‑Run erkennt Duplikate → Vault speichert und **round‑trippt durch KeePassXC**.

**Verifikation:** BOM/Delimiter/Encoding‑Erkennung; deutsche Zeitstempel (`dd.MM.yyyy`, ISO, Epoch);
`ColumnMap`‑Transforms; `DedupeResolver`‑Äquivalenzklassen; Golden‑File‑Snapshot der resultierenden
Gruppen/Eintragsbäume (`Verify`); Re‑Import → alle Zeilen `Duplicate`.

---

## SEKTOR 3 — Eintragsverwaltung (Baum, Liste, Editor, Historie, Papierkorb)

**Zweck:** Der tägliche Arbeitsbereich; muss mit 1.500+ importierten Zeilen flüssig bleiben.

**Kernelemente & Dateien**
- `AuraVault.App.Views/ViewModels`: `VaultTreeView` (virtualisiert via `TreeDataGrid`),
  `EntryListView` (virtualisiert via `ItemsRepeater` + `RecyclingElementFactory`, feste Zeilenhöhe),
  `EntryDetailView`, `EntryEditorView`.
- Editor: Standardfelder + **Custom Fields** (String/Protected) + Tags + **Anhänge** (add/extract/
  preview, Binary‑Pool) + **Historie/Versionierung** (Snapshot bei jedem Save, Diff‑Ansicht) +
  Favoriten + Ablaufdatum.
- **Papierkorb**: Soft‑Delete in Recycle‑Bin‑Gruppe, Wiederherstellen, Leeren (mit Tombstones für Merge).
- Feld‑Aktionen: kopieren (Auto‑Clear, Sektor 4), anzeigen/verbergen, öffnen‑URL, per‑Feld‑Kontextmenü.

**Abhängt von:** Sektor 1 (Modell), Sektor 4 (Clipboard), Sektor 6 (Command/Kontextmenü).

**Abnahme:** Baum/Liste scrollen mit 1.500 Einträgen ruckelfrei; Eintrag anlegen/bearbeiten/löschen/
wiederherstellen; jeder Save erzeugt einen History‑Snapshot mit funktionierender Diff‑Ansicht;
Anhang hinzufügen/entpacken.

**Verifikation:** Headless‑VM‑Tests (Command‑Enable/Disable, Editor‑Flows); Perf‑Test 5k Einträge
Scroll ≥ 110 fps; History‑Union/Restore Unit‑getestet.

---

## SEKTOR 4 — Security‑Runtime (Auto‑Lock, Speicher, Clipboard)

**Zweck:** Geheimnisse so kurz und so geschützt wie möglich im Klartext halten.

**Kernelemente & Dateien**
- `AuraVault.Platform.Abstractions`: `ISecureMemory` (`Lock/Unlock/Zero`), `ISessionMonitor`
  (`SessionLock/Unlock/Suspend/Resume`), `IIdleMonitor` (`IdleFor`), `IClipboardService`
  (`SetWithAutoClear(text,ttl,isSecret)`), `IDpapiProtector`.
- `AuraVault.Platform.Windows`: `SecureMemoryWindows` (`VirtualLock/Unlock`, `CryptProtectMemory`);
  `SessionMonitor` (`WTSRegisterSessionNotification` + `WM_WTSSESSION_CHANGE`; Power via
  `RegisterPowerSettingNotification` + `WM_POWERBROADCAST`; via `Win32Properties.AddWndProcHookCallback`
  in Avalonias Message‑Loop); `IdleMonitor` (`GetLastInputInfo`); `ClipboardService` (Avalonia‑Clipboard
  + `AddClipboardFormatListener`; Clear nach TTL **12 s**, außer schon extern überschrieben; setzt
  Cloud‑Clipboard/Verlauf‑Ausschlussformat); `DpapiProtector` (`ProtectedData`, `CurrentUser`).
- `AutoLockController` erweitert: Trigger Idle / Session‑Lock / Suspend / manuell / (optional) Minimize;
  beim Lock **alle** `SecureBuffer` zeroen, `ProtectedValue` bleiben verschlüsselt.
- Regel: Geheimnisse nie in `string` — direkt in gepinnte `byte[]`/`SecureBuffer` parsen; `ZeroMemory`
  auf jedem Unlock/Close/Exception‑Pfad; `ProtectedValue` erst im Moment der Nutzung entschlüsseln,
  danach re‑encrypten.

**Abnahme:** Aus gesperrtem Zustand: Idle‑Timeout, Windows‑Sperre und Standby sperren jeweils; nach
Lock zeigt die Debug‑Ansicht genullte Buffer; kopiertes Passwort verschwindet nach 12 s aus der
Zwischenablage; nichts landet im Cloud‑Clipboard‑Verlauf.

**Verifikation:** Unit: `SecureBuffer` doppel‑Dispose/Span‑Safety; Integration: simulierte
Session/Power‑Nachrichten lösen Lock aus; Clipboard‑TTL mit Fake‑Clock.

---

## SEKTOR 5 — Aura‑Rendersystem (Optik, 120 fps, Reduced Motion)

**Zweck:** Die namensgebende Optik — Glas/Blur, Glow, animierter Mesh‑Gradient — ohne die UI‑Thread‑
Latenz zu opfern.

**Layermodell (hinten→vorn)**
1. Fenster‑Material: `Window.TransparencyLevelHint = {Mica, AcrylicBlur, Blur}`;
   `ExperimentalAcrylicBorder` für Titelleiste/Seitenpaneele; `Win32PlatformOptions.CompositionMode =
   [WinUIComposition, DirectComposition]`.
2. Animierter Aura‑Hintergrund: ein `AuraLayer`‑Control mit
   **`src/AuraVault.App/Aura/AuraShaderVisual.cs` : `CompositionCustomVisual`**; ein **SKSL**‑Shader,
   einmalig via `SKRuntimeEffect.CreateShader` kompiliert; Uniforms `uTime,uResolution,uIntensity,
   uAccent,uMotion`; zeichnet Flow‑Noise‑Mesh‑Gradient (2–3 Oktaven Simplex‑Noise, 3–5 radiale
   Farbstops). Render **auf dem Render‑Thread** in `OnRender(ImmediateDrawingContext)` via
   `ISkiaSharpApiLease`.
3. `AuraClock` abonniert `TopLevel.RequestAnimationFrame` (Compositor‑Takt, Refresh‑paced), schiebt
   `uTime`, invalidiert das Custom‑Visual. **Kein** `DispatcherTimer` für Visuals. Bei
   verdeckt/minimiert/deaktiviert → abmelden → 0 % GPU.
4. Widget‑Glow/Tiefe: `Visual.Effect = DropShadowEffect` (farbig, groß, niedrige Opazität) für
   Card‑Glow; `BlurEffect` auf Modal‑Scrims; Hover/Selection‑Glow via Compositor Implicit Animations
   (`Compositor.CreateImplicitAnimationCollection`).
5. Mikrointeraktionen: `CompositePageTransition` (Slide+Fade); Listen‑Add/Remove via `ItemsRepeater` +
   Implicit‑Offset‑Animationen. Dauer = `baseMs * AnimationSpeed`, gated durch `MotionScale`.

**120‑fps‑Strategie:** alles Listenförmige virtualisieren; ein Shader/ein Custom‑Visual, keine
Per‑Frame‑Allokationen (wiederverwendetes `float[]`, kein LINQ/Boxing in `OnRender`/Clock);
Aura in **0,5–0,75×** Auflösung rendern und hochskalieren; Anzahl aktiver Glow‑Effekte begrenzen
(nur fokussierte Card animiert Glow); **Startup‑Frame‑Pacing‑Probe** (~1 s Volllast) → bei < 90 fps
`EnableAnimatedBackground`‑Default aus + Hinweis in den Einstellungen; versteckter `Ctrl+Alt+F`
FPS‑Overlay.

**Reduced Motion:** `AuraSettings.ReducedMotion` ODER OS‑Setting (`UISettings.AnimationsEnabled` /
`SPI_GETCLIENTAREAANIMATION`) → `MotionScale = 0`: `AuraClock` stoppt (Hintergrund = statischer
Einzelframe), alle `PageTransition` sofort, Implicit‑Animationen aus, Glow statisch; **Glas/Blur
bleibt** (keine Bewegung). Intensität 0 → Layer wird gar nicht erzeugt (flaches Fluent‑Theme).

**Kernelemente & Dateien:** `AuraVault.App.Aura`: `AuraLayer`, `AuraShaderVisual`,
`MeshGradientRenderer`, `AuraProfile`, `AuraClock`, `MotionScale`, `GlassBorder`; `<...>/Aura/aura.sksl`.

**Abnahme:** Auf diesem Rechner hält die Aura ≥ 110 fps bei Intensität 0,7 während eine 1.500‑Einträge‑
Liste scrollt; Reduced‑Motion liefert einen statischen, aber ansehnlichen Fallback; Intensität 0..1
und Animationsgeschwindigkeit greifen live ohne Neustart.

**Verifikation:** Headless‑Render‑Smoke (Intensität 0/0,5/1 ohne Exception; Reduced‑Motion = 1 Frame);
Perf‑Harness misst fps beim Scrollen; Allokations‑Profiler bestätigt 0 B/Frame im Aura‑Pfad.

---

## SEKTOR 6 — App‑Shell: Menüs, Command‑Palette, Docking, Statusleiste, Tastaturbedienung

**Zweck:** Die „aus 20 Jahren Coding"‑Reife — eine App, die vollständig per Tastatur bedienbar ist.

**Kernelemente & Dateien**
- **`src/AuraVault.App/Commands/CommandRegistry.cs`** — *eine* Quelle für Menüleiste, Command‑Palette,
  Kontextmenüs, Keybindings: `AppCommand { Id, Title, Category, Gesture, CanExecute, Execute,
  Keywords }`; `CommandPaletteViewModel`; `KeyBindingMap` (`keybindings.json`); `MenuModel`‑Builder.
- `AuraVault.App.Shell`: `MainWindow`, `ShellViewModel`, `MenuBarViewModel` (File/Edit/Entry/View/
  Tools/Window/Help — generiert aus Registry), `StatusBarViewModel` (Vault‑Name, Eintragszahl,
  Lock‑Countdown, Caps‑Lock, Task‑Spinner, Dirty/Sync‑Indikator).
- `AuraVault.App.Docking`: `DockFactory` (Dock.Avalonia), Layout‑Persistenz `layout.json`, abgedockte
  Fenster; „Window"‑Menü verwaltet Paneele + Detached Windows; mehrere Eintrags‑Tabs.
- **Command‑Palette** `Ctrl+K` — Fuzzy über Registry + Einträge („zu Eintrag springen") + Settings.
- **Kontextmenüs überall** (Baum, Liste, Felder, Tabs, Statusleiste) aus derselben Registry.
- Tastatur‑First: jede Aktion mit Geste; Focus‑Visible überall; Type‑Ahead in Baum/Liste; `F2` rename;
  `Esc` schließt/sperrt.

**Abhängt von:** Sektor 5 (Transitions/Glass), Sektor 1 (Vault‑Aktionen).

**Abnahme:** Menüleiste + Palette + Kontextmenüs bedienen die App mausfrei; Paneele docken/floaten/
tabben und stellen sich nach Neustart wieder her.

**Verifikation:** Headless: Command‑Enable/Disable, Keybinding‑Auflösung, Menü‑Modell aus Registry,
Wizard‑Step‑Flow.

---

## SEKTOR 7 — Einstellungen (großer Preferences‑Dialog)

**Zweck:** Tiefe Konfigurierbarkeit; alles live, kein Neustart.

**Kernelemente & Dateien**
- `AuraVault.Core.Settings`: `AppSettings` (POCO‑Baum: `General, Security, Appearance, Aura, Hotkeys,
  AutoType, Backup, Import, Network`); `ISettingsStore` → `JsonSettingsStore` (atomarer Write,
  Schema‑Version, Migrations‑Hooks); `AuraSettings` (`Intensity 0..1`, `ReducedMotion`,
  `AnimationSpeed 0.25..2.0`, `EnableAnimatedBackground`, `EnableGlass`, `ThemeVariant`, `AccentColor`,
  `TrueBlackAmoled`).
- `AuraVault.App.Views`: `PreferencesWindow` mit Seiten **General · Security** (Auto‑Lock,
  Clipboard‑TTL, Hello, Wipe‑Cache‑on‑Lock, Argon2‑Parameter mit Benchmark) **· Appearance**
  (Dark/Light/AMOLED, Accent, Theme‑Variant) **· Aura** (Intensität, animierter Hintergrund an/aus,
  Glas an/aus, Animationsgeschwindigkeit, Reduced‑Motion mit OS‑gekoppeltem Default) **· Hotkeys**
  (Rebind‑Grid → `keybindings.json`) **· Auto‑Type · Backup · Import‑Defaults · Network** (alles aus)
  **· Advanced · About**.

**Abnahme:** Alle Seiten persistieren und wirken live; Theme/Accent/Aura/Animationstempo ohne Neustart;
Argon2‑Benchmark schlägt sinnvolle Presets vor.

**Verifikation:** Unit: Settings‑Round‑Trip, Schema‑Migration v1→v2; Headless: Live‑Apply von
Theme/Aura.

---

## SEKTOR 8 — Passwort‑Generator

**Zweck:** Starke Passwörter/Passphrasen mit glaubwürdiger Entropie‑Anzeige.

**Kernelemente & Dateien**
- `AuraVault.Core.Generator`: `PasswordGenerator`; `GeneratorProfile` (Länge, Klassen‑Toggles,
  Min‑pro‑Klasse, Exclude‑Similar, Exclude‑Set, Require‑every‑class); `CharacterPoolBuilder`;
  `DicewareGenerator` (gebündelte EFF‑Long‑List + deutsche Wortliste); `PronounceableGenerator`
  (Silbenmodell); `PassphraseProfile` (Wortzahl, Separator, Groß/Klein, Ziffern‑Injektion);
  `EntropyEstimator` (Pool‑basiert + zxcvbn‑artige Muster‑Strafen für Nutzereingaben).
- UI: `GeneratorPanel` (Slider/Toggles, Live‑Vorschau, **Entropie‑Meter**, „übernehmen" auf Eintrag →
  History‑Snapshot), per‑Eintrag‑Policy.
- Wortlisten als eingebettete Ressourcen.

**Abnahme:** 20‑Zeichen‑Passwort und 6‑Wort‑Diceware‑Passphrase erzeugen; Klassen‑Constraints erfüllt;
Ausschlussmengen greifen; Entropie‑Wert plausibel und monoton.

**Verifikation:** Unit: Constraints, Diceware‑Wortzahl/Entropie‑Mathematik, RNG‑Gleichverteilung
(Chi‑Quadrat‑Smoke), Estimator‑Monotonie.

---

## SEKTOR 9 — TOTP / HOTP / Steam

**Zweck:** 2FA‑Codes direkt im Manager.

**Kernelemente & Dateien**
- `AuraVault.Core.Otp`: `TotpGenerator`, `HotpGenerator`, `SteamGuardGenerator`; `OtpAuthUri`
  (parse/format `otpauth://`); `OtpProvisioning` (aus URI, aus Base32, aus QR‑Payload).
- Import: `otpauth://` einfügen; **QR aus Bilddatei ODER Bildschirmausschnitt** (`ZXing.Net`); manuell
  Base32. `TotpSettings.Secret` als `ProtectedValue`.
- UI: `TotpPanel` — Inline‑Code + Fortschrittsring + Kopieren; `{TOTP}` im Auto‑Type (Sektor 12).

**Abnahme:** TOTP via `otpauth://` und via QR‑Screenshot hinzufügen; Code stimmt mit einem
Referenz‑Authenticator überein; Steam‑Alphabet korrekt.

**Verifikation:** RFC‑6238/4226‑Vektoren, Steam‑Alphabet, `otpauth://`‑Round‑Trip, Clock‑Skew‑Fenster.

---

## SEKTOR 10 — Suche, Filter, Spalten

**Zweck:** Sofortfund bei tausenden Einträgen.

**Kernelemente & Dateien**
- `AuraVault.Core.Search`: `SearchIndex` (In‑Memory‑Invertindex über Titel/User/URL/Tags/Notizen +
  optional Custom Fields; inkrementelles Update bei Eintragsänderung); `FuzzyMatcher` (fzf‑Stil:
  Prefix/Wortgrenze/Konsekutiv‑Boni, `Quickenshtein` für Distanz‑Tiebreak); `QueryParser` (Freitext +
  `field:value`, `tag:`, `group:`, `is:weak`, `is:reused`, `expires:<30d`); `SavedFilter`;
  `ColumnDefinition` (Id, Header, Value‑Selector, Breite, Sort); `ColumnLayout`.
- UI: `SearchBarView` (Instant‑Suche), gespeicherte Filter, anpassbare Spalten + Layout‑Persistenz.
- Optionaler verschlüsselter Such‑Cache (SQLCipher, Key via HKDF aus Master‑Key, **default aus**,
  Purge bei Lock).

**Abnahme:** Tippen filtert sofort (fuzzy); `is:reused` als gespeicherter Filter; Custom‑Spalte
hinzufügen bleibt nach Neustart.

**Verifikation:** Fuzzy‑Ranking‑Snapshots (`Verify`); Query‑Parser‑Grammatik; inkrementelle
Index‑Korrektheit.

---

## SEKTOR 11 — Security‑Dashboard (schwach / mehrfach / alt / ablaufend / geleakt)

**Zweck:** Aufräum‑Werkzeug — nutzt direkt das heute schon berechnete `pw_wie_oft_genutzt`‑Konzept.

**Kernelemente & Dateien**
- `AuraVault.Core.Security`: `HealthAnalyzer` → `HealthReport` (Buckets `Weak/Reused/Old/Expiring/
  Compromised`); **`ReuseMetric`** (`IReadOnlyDictionary<PasswordHash,int>` über den Vault, pro Eintrag
  `TimesUsed`); `WeaknessRule`‑Set (Länge, Entropie, Wörterbuch, Keyboard‑Walk, Datums‑Muster);
  `AgePolicy` (Default 365 d); `HibpClient` (k‑Anonymität `range/{prefix5}`, **opt‑in**, nur
  SHA‑1‑Prefix verlässt den Rechner, Antwort gecacht).
- `AuraVault.Platform.Abstractions`: `IFileShredder` (`ShredAsync(path, passes)`) — hier definiert,
  von Sektor 2 genutzt; Windows‑Impl: 3× Zufall + Zero, dann Delete; „Best‑Effort auf SSD/CoW"‑Label.
- UI: `SecurityDashboardView` — Klick‑Durchstieg zu gefilterter Liste; „jetzt beheben" öffnet Generator
  auf dem Eintrag; Report‑Export.

**Abnahme:** Dashboard markiert die Mehrfach‑Passwort‑Cluster aus dem iPhone‑Import; Alt/Ablauf‑Grenzen
stimmen; HIBP‑Request enthält nur den 5‑Zeichen‑Prefix (gegen Mock geprüft).

**Verifikation:** Unit: `ReuseMetric`‑Zählung über gebauten Vault; Weakness‑Regeln; Age/Expiry‑Grenzen;
HIBP‑Mock‑Handler‑Assertion.

---

## SEKTOR 12 — System‑Integration: Tray, Global‑Hotkey, Auto‑Type, Windows Hello, Autostart

**Zweck:** Aus der App ein Betriebssystem‑Bürger machen.

**Kernelemente & Dateien**
- `AuraVault.Platform.Abstractions`: `IHelloAuthenticator`, `IGlobalHotkey`, `ISingleInstance`,
  `ITrayIcon`, `IJumpList`, `IStartupManager`, `IInputEmitter`, `IWindowEnumerator`.
- `AuraVault.Platform.Windows`:
  - `WindowsHelloAuthenticator` — `Windows.Security.Credentials.KeyCredentialManager`; freigegebene
    Credential entschlüsselt den **DPAPI‑CurrentUser**‑geschützten 32‑B‑Zufall in
    `%LOCALAPPDATA%\AuraVault\hello.bin`; dieser geht als Keyfile‑äquivalenter Faktor in
    `MasterKeyComposer`. Löschen von `hello.bin` deaktiviert Hello; Master‑Passwort funktioniert weiter.
  - `GlobalHotkey` — `RegisterHotKey` auf verstecktem Message‑Window (Default `Ctrl+Alt+K` Auto‑Type
    fürs Vordergrundfenster; zweiter Hotkey = Quick‑Search‑Popup).
  - `SingleInstance` — Named `Mutex` + Named‑Pipe‑Server; leitet CLI‑Args / „show+search" an die
    laufende Instanz.
  - `TrayIcon` — Avalonia `TrayIcon` für Icon/Menü; Quick‑Unlock + Quick‑Search als kleines
    randloses Avalonia‑Fenster (Tippen → Treffer → Enter kopiert Passwort mit Auto‑Clear oder löst
    Auto‑Type aus).
  - `JumpList` — `ICustomDestinationList` (COM via CsWin32): „Neuer Eintrag", „Vault öffnen…", Recents.
  - `StartupManager` — per‑User Scheduled Task (`--tray`), bevorzugt vor `Run`‑Key.
  - `InputEmitter` — `SendInput` mit Scan‑Codes + Unicode‑Fallback, Per‑Token‑Delay,
    Vordergrundfenster‑Guard.
  - `WindowEnumerator` — `EnumWindows` + `GetWindowText` + `GetWindowThreadProcessId` + UIA
    (`CUIAutomation`) für Browser‑URL des fokussierten Dokuments.
- `AuraVault.Core.Autotype`: `AutoTypeSequence`, `AutoTypeParser`
  (`{USERNAME}{TAB}{PASSWORD}{ENTER}`, `{DELAY x}`, `{TOTP}`, `{CLEARFIELD}`, Key‑Namen);
  `IWindowTargetMatcher` (Titel‑Regex + Prozessname + URL‑Host); `AutoTypeController` (Eintrag per
  Vordergrundfenster wählen, Disambiguierungs‑Picker, Abbruch bei Fokuswechsel); per‑Eintrag Sequenzen
  + Fenster‑Assoziationen; „Quick‑Paste" (User/Pass ohne Enter).

**Abnahme:** Aus gesperrtem Zustand entsperrt Hello; Tray‑Quick‑Search kopiert ein Passwort, das nach
12 s gelöscht wird; Global‑Hotkey tippt User+Passwort+TOTP in ein echtes Browser‑Login‑Formular, das
per URL gematcht wurde; nie Auto‑Type in ein ungematchtes Fenster; elevated Zielfenster → klare
Meldung statt stillem Fehler.

**Verifikation:** Testmatrix Auto‑Type über Chrome/Edge/Firefox‑Login + eine Win32/WinForms‑App;
Integration: Session/Power‑Nachrichten; Hello‑Faktor Unit‑getestet über Fake‑Authenticator.

---

## SEKTOR 13 — Multi‑Vault, Merge/Sync, Backups

**Zweck:** Mehrere Tresore, deterministisches Zusammenführen zweier KDBX, rotierende Sicherungen.

**Kernelemente & Dateien**
- `AuraVault.Core.Vaults`: mehrere offene Vaults (Tabs/Workspaces), Recent‑Liste, per‑Vault‑Settings,
  „alle schließen / alle sperren".
- **`MergeEngine`** — UUID‑basierter 3‑Wege‑Merge zweier `KdbxDatabase`: `LastModified` gewinnt,
  Tombstone‑bewusste Deletes (`DeletedObject`), per‑Eintrag‑History‑Union, **Konfliktliste** mit
  manueller Auflösung; Dry‑Run‑Preview; genutzt für „meine zwei Dateien syncen" und „KDBX importieren";
  Re‑Merge = No‑Op.
- `AuraVault.Core.Backup`: `BackupService` (bei Save + geplant), `RetentionPolicy` (N täglich /
  M wöchentlich / K monatlich; Größenlimit), `BackupCatalog`; Restore‑Dialog.
- UI: `MergeView`, Backup‑Seite in den Einstellungen.

**Abnahme:** Zwei absichtlich divergierte Kopien von `Personal.kdbx` mergen deterministisch mit dem
erwarteten Konfliktset; erneutes Mergen ändert nichts; Backups rotieren gemäß Policy; ein Backup
stellt sauber wieder her.

**Verifikation:** Unit: add/edit/delete/move‑Permutationen, Tombstone‑gewinnt, History‑Union,
idempotenter Re‑Merge, Konflikterkennung; Retention‑Policy mit Fake‑Clock.

---

## SEKTOR 14 — Weitere Importer/Exporter + Anhänge‑UI

**Zweck:** Umzug von anderen Managern; Export mit Warnungen.

**Kernelemente & Dateien**
- `AuraVault.Core.Import` (konkret, mit Fixture‑Tests): `KdbxImporter` (als Merge oder Kopie),
  `BitwardenJsonImporter`, `OnePuxImporter`, `LastPassCsvImporter`, `ChromiumCsvImporter`,
  `FirefoxCsvImporter`, `ApplePasswordsCsvImporter`, `GenericDelimitedImporter` (nutzt das
  Mapping‑UI aus Sektor 2).
- `AuraVault.Core.Export`: `IExporter`, `CsvExporter`, `Kdbx4Exporter`, `ExportRiskReport`
  (Klartext‑Warnung + getippte Bestätigung; Feldverlust‑Warnung); Scope Auswahl/Gruppe/ganzer Vault.
- Anhänge‑UI: add/extract/preview, Größenwarnungen.

**Abnahme:** Jeder Importer liest einen echten (bereinigten) Export seiner Quelle in die korrekte
Struktur (Test‑asserted); CSV‑Export verlangt explizite getippte Bestätigung und re‑importiert
verlustfrei für unterstützte Felder.

**Verifikation:** `fixtures/importers/<quelle>/` Golden‑File‑Snapshots (`Verify`); Re‑Import → alle
Zeilen `Duplicate`.

---

## SEKTOR 15 — Lokalisierung & Onboarding

**Zweck:** DE + EN vollständig; ruhiger Erststart.

**Kernelemente & Dateien**
- `AuraVault.App.Localization`: `Strings.resx` / `Strings.de.resx`, `ILocalizer`, `LocalizeExtension`
  (Markup); Laufzeit‑Umschaltung; kultur‑bewusste Daten/Sortierung; **Deutsch ist Default bei OS de‑\***.
- `AuraVault.App.Views`: `OnboardingWizardView` — Willkommen → Vault anlegen/öffnen →
  Master‑Passwort‑Stärke → optional Hello → optional Import (Sektor 2) → Aura‑Kostprobe
  (Intensitäts‑Vorschau) → fertig.

**Abnahme:** DE/EN‑Umschaltung wirkt sofort; `resx`‑Vollständigkeitstest bricht CI bei fehlendem
DE‑Key; Onboarding führt vom Nullzustand bis zum importierten, entsperrten Vault.

**Verifikation:** Unit: `resx`‑Key‑Parität; Headless: Wizard‑Step‑Flow inkl. „Import überspringen".

---

## SEKTOR 16 — Packaging, Signierung, Auto‑Update, Härtung

**Zweck:** Auslieferbar auf einem sauberen Windows 11.

| Format | Tooling | Zweck |
|---|---|---|
| **Velopack** (primär) | `vpk pack` auf `dotnet publish -r win-x64 --self-contained` | 1‑Klick‑Install nach `%LOCALAPPDATA%`, kein Admin, **Delta‑Auto‑Update**, Channels |
| **Portable ZIP** (primär) | self‑contained Single‑Folder + `portable.ini` → Config neben der EXE | USB / ohne Install |
| **MSIX** (sekundär) | `dotnet publish` → `MakeAppx` + `SignTool` (Windows SDK), handgeschriebenes `AppxManifest.xml` (`runFullTrust`), **kein** Windows App SDK | Store / Intune |
| **Inno Setup 6** (Alternative) | `build/installer.iss` | klassischer EXE‑Installer |

- Ziel `win-x64` jetzt; `win-arm64` in P5 (Avalonia + SkiaSharp unterstützen es).
- **Code‑Signing:** Authenticode auf jeder `.exe`/`.dll` und jedem Installer, RFC‑3161‑Timestamp;
  bevorzugt **Azure Trusted Signing** (oder EV‑Cert auf HSM) für stabile SmartScreen‑Reputation;
  signieren **vor** Velopack‑Pack; CI hält keinen privaten Schlüssel (Signing in gesperrter Stage /
  Trusted Signing via OIDC).
- Versionsschema SemVer, `MinVer`/GitVersion aus Tags; `AssemblyInformationalVersion` = Commit.
- Härtung: Threat‑Model‑Review, Secure‑Memory‑Audit, SBOM (`dotnet` SBOM), Fuzz auf KDBX‑ und
  CSV‑Parser (`SharpFuzz`), Accessibility‑Audit (Screenreader‑Labels, Kontrast, keine Keyboard‑Traps),
  High‑DPI + Multi‑Monitor (Per‑Monitor‑v2), lokaler Crash‑Reporter (opt‑in Upload).
- MSIX + Portable werden ab P2 in **jedem** CI‑Lauf gebaut, damit Packaging nicht verrottet.

**Abnahme:** Signierter Velopack‑Installer und signierte Portable‑ZIP installieren/laufen auf sauberer
Win‑11‑VM; Auto‑Update vN→vN+1 funktioniert; MSIX + Inno bauen in CI; SBOM veröffentlicht;
Accessibility‑Checkliste bestanden.

---

## SEKTOR 17 — Optional: Browser‑Erweiterung + Native‑Messaging‑Host

**Zweck:** Ausfüllen/Speichern/Generieren im Browser. Erst nach P5, standardmäßig deaktiviert.

**Kernelemente & Dateien**
- `AuraVault.BrowserBridge` — Native‑Messaging‑Host (Chromium + Firefox): fill/save/generate,
  per‑Origin‑Freigabe, **kein** Vault‑Unlock ohne Nutzer.
- Extension (MV3, Chromium + Firefox) unter `src/extension/` (Node 20 nur hier).

**Abnahme:** Erweiterung füllt ein Login im Browser über den Host aus, ohne dass der Vault ohne
Nutzeraktion entsperrt wird; jede neue Origin verlangt explizite Freigabe.

**Verifikation:** Host‑Protokoll‑Contract‑Tests; E2E in Chrome + Firefox gegen eine Test‑Login‑Seite.

---

## Baureihenfolge (Phasen bündeln die Sektoren)

| Phase | Enthält | „Fertig" heißt |
|---|---|---|
| **P0** Fundament | Sektor 1 | Vault anlegen/öffnen/bearbeiten; Round‑Trip mit KeePassXC & KeePass 2.x; Krypto‑KATs grün; ADR KDBX‑Lib |
| **P1** Erststart‑Story | Sektor 2 + Sektor 3 + Sektor 8 + Sektor 10 (v1) + Backup‑on‑Save | iPhone‑Import (~358 Einträge) mit Preview & Dedupe; virtualisierte Baum/Liste; Generator + Entropie; Instant‑Suche; Vault round‑trippt durch KeePassXC |
| **P2** Aura + Shell‑Reife | Sektor 5 + Sektor 6 + Sektor 7 + Sektor 15 | mausfreie Bedienung (Menü/Palette/Kontext); Docking persistiert; Preferences live; DE/EN; Aura ≥ 110 fps @ Intensität 0,7 beim Scrollen von 1.500 Einträgen; Reduced‑Motion‑Fallback |
| **P3** Sicherheit, 2FA, OS | Sektor 4 (voll) + Sektor 9 + Sektor 11 + Sektor 12 + Sektor 13 (Backups/Retention) + Sektor 10 (v2) | Hello‑Unlock; Tray‑Quick‑Search mit 12‑s‑Auto‑Clear; Global‑Hotkey Auto‑Type inkl. `{TOTP}` per URL‑Match; Dashboard markiert Reuse‑Cluster; Backups rotieren, Restore ok |
| **P4** Breite | Sektor 13 (Merge/Multi‑Vault) + Sektor 14 | jeder Importer schluckt einen echten Fixture‑Export; CSV‑Export nur mit getippter Bestätigung, verlustfreier Re‑Import; deterministischer Merge zweier divergierter Kopien |
| **P5** Auslieferung + Härtung | Sektor 16 (+ Sektor 17 optional) | signierter Velopack + Portable laufen auf sauberer VM; Auto‑Update; MSIX/Inno in CI; SBOM; Accessibility bestanden |

---

## Globale Verifikation

**CI (`windows-latest`):** restore → build (warnings‑as‑errors) → format‑check → Unit + Integration +
Headless → Coverage‑Gate (`Core` ≥ 85 %) → Artefakte. Nightly: Fuzz + Large‑Vault‑Perf +
`keepassxc-cli`‑Interop‑Diff.

**KDBX‑Interop:** `fixtures/kdbx/` mit von **KeePassXC** und **KeePass 2.x** erzeugten Dateien (AES‑,
ChaCha20‑, Argon2‑, Keyfile‑Vault; Eintrag mit Historie + Anhängen + Custom Fields + Ablauf + TOTP;
5k‑Einträge‑Vault; Recycle Bin). Test: lesen → Modell prüfen → schreiben → erneut lesen →
Struktur‑Deep‑Equal; CI‑Schritt öffnet AuraVaults Output mit `keepassxc-cli` und difft einen Dump.

**Manuelles E2E (`docs/manual-e2e.md`, ab P1 pro Phase):**
1. Frisches Profil → Onboarding → `E2E.kdbx` mit starkem Master‑Passwort.
2. Import‑Assistent → iPhone‑Preset → Ordner `...\entschluesselt\` → Preview zeigt ~268 + 26 + 64 in
   Web / Wi‑Fi / Apps → bestätigen → Shred **ablehnen**.
3. Gruppen prüfen; Zeitstempel einiger Einträge; `5_*` im Papierkorb; `4_*` metadaten‑only.
4. Manuell sperren → Debug‑Ansicht: Buffer genullt → mit Passwort entsperren → mit Hello entsperren.
5. Generator: 20‑Zeichen + 6‑Wort‑Diceware; Entropie prüfen; auf Eintrag anwenden → History‑Snapshot.
6. TOTP via `otpauth://` und via QR‑Screenshot; Code gegen Referenz‑Authenticator.
7. „amazon" suchen (fuzzy); Filter `is:reused` speichern; Custom‑Spalte hinzufügen.
8. Tray‑Quick‑Search → Passwort kopieren → Zwischenablage nach 12 s leer.
9. Global‑Hotkey → Auto‑Type inkl. `{TOTP}` in ein per URL gematchtes Browser‑Login.
10. Backup auslösen (Save + geplant) → aus Backup in neue Datei wiederherstellen.
11. Vault in KeePassXC öffnen → alles intakt.
12. Aura‑Intensität 0→1 und Reduced‑Motion umschalten; FPS‑Overlay bleibt ≥ 110 beim Scrollen.

---

## Hauptrisiken & Gegenmaßnahmen

1. **KDBX‑Bibliotheksreife in C#.** KeePassLib ist GPL‑2.0 + WinForms‑verwoben. → **Clean‑Room
   `Kdbx4Codec`** auf dokumentiertem Format + auditierten Primitiven (`Konscious` Argon2, `BouncyCastle`
   ChaCha20, BCL AES/HMAC); das KDBX‑**Format** ist frei, nur KeePass‑*Quellcode* ist GPL. Gegen
   KeePassXC/KeePass‑Fixtures + `keepassxc-cli` in CI validieren. `IKdbxCodec` hält `KpcLib` als
   Drop‑in‑Fallback. Entscheidung als ADR in P0.
2. **Sicherer Speicher auf verwalteter Runtime.** GC kopiert Buffer; `string` ist unveränderlich.
   → nie Geheimnisse in `string`; direkt in gepinnte `byte[]`/`SecureBuffer`; `GCHandle.Alloc(Pinned)`
   + `VirtualLock` + `CryptProtectMemory`; `ZeroMemory` auf jedem Pfad; `ProtectedValue` bleibt bis zur
   Nutzung verschlüsselt; Clipboard‑Auto‑Clear + Cloud‑Clipboard‑Ausschluss; Auto‑Lock nullt alles.
   Restrisiko ehrlich dokumentieren. Option: Krypto‑Kern später als kleine Rust‑`cdylib` hinter
   `IKdbxCodec` (Toolchain vorhanden).
3. **Auto‑Type‑Zuverlässigkeit.** `SendInput` rennt gegen Fokuswechsel, Layouts, UIPI (elevated Ziele),
   Browser‑Eigenheiten, IME. → Vordergrund‑`HWND` vor dem Tippen fixieren, bei Fokuswechsel abbrechen;
   Scan‑Code + Unicode; `{CLEARFIELD}` + Tab‑Navigation; UIA‑URL/Titel‑Match + Disambiguierungs‑Picker;
   elevated Ziel erkennen → klare Meldung; „Quick‑Paste" ohne Enter; Testmatrix; nie in ungematchtes
   Fenster tippen.
4. **Shader/Aura‑Performance bei 120 fps.** Skia‑Blur/Drop‑Shadow sind teuer; Avalonias
   Custom‑Shader‑Pfad ist weniger turnkey als Qt. → ein kompilierter Shader, ein `CompositionCustomVisual`,
   nur Render‑Thread, 0 Allokationen/Frame; Aura in 0,5–0,75× rendern + hochskalieren; aktive
   Glow‑Effekte begrenzen; **Startup‑Frame‑Pacing‑Probe** deaktiviert animierten Hintergrund auf zu
   schwachen GPUs; `RequestAnimationFrame` statt `DispatcherTimer`; alles virtualisieren; `AuraClock`
   bei verdeckt/minimiert stoppen; Reduced‑Motion = statischer Frame; FPS‑Overlay für Support.
   **Ausstieg:** wenn P2 das Ziel verfehlt, wird nur `AuraVault.App` auf **Qt 6.9 + QML** neu gebaut
   (MSVC/CMake/Qt‑Install + Qt‑Lizenz‑Review akzeptiert) — `Core`/`Platform` bleiben unangetastet.

**Sekundär:** File‑Shredding auf SSD/CoW/BitLocker nur Best‑Effort (→ Default „in Papierkorb", Option
klar labeln, auch Backups leeren empfehlen); Argon2‑Defaults auf RAM‑armen Rechnern (→ Wizard‑Benchmark
+ Presets); XLSX‑Parser auf kaputten Dateien (→ Guarded Parse + Fixtures, Fallback „Sheet als CSV
exportieren"); Lokalisierungs‑Drift (→ `resx`‑Vollständigkeitstest bricht CI).

---

## Zu erstellende Schlüsseldateien

- `src/AuraVault.Core/Kdbx/Kdbx4Codec.cs` — Clean‑Room KDBX 4.1 Reader/Writer (Korrektheits‑Keystein;
  dazu `IKdbxCodec`, `KdbxHeaderReader/Writer`, Inner‑Random‑Stream‑Typen).
- `src/AuraVault.Core/Cryptography/{MasterKeyComposer,SecureBuffer,ProtectedValue}.cs` —
  Schlüssel­komposition und Secure‑Memory‑Disziplin, von der alles abhängt.
- `src/AuraVault.Core/Import/{ImportPipeline,IPhoneRecoveryPreset}.cs` — Laufzeit‑CSV/XLSX‑Ingestion +
  eingebautes Erststart‑Mapping für `C:\Users\jonag\Apple\MobileSync\Backup\entschluesselt\`.
- `src/AuraVault.App/Aura/{AuraLayer,AuraShaderVisual,AuraClock}.cs` + `Aura/aura.sksl` —
  Render‑Thread‑Aura‑Layer und Reduced‑Motion‑Fallback.
- `src/AuraVault.App/Commands/CommandRegistry.cs` — einzige Quelle für Menüleiste, Command‑Palette,
  Kontextmenüs, Keybindings.
- `src/AuraVault.Platform.Windows/{WindowsHelloAuthenticator,SessionMonitor,GlobalHotkey,InputEmitter,
  SingleInstance,ClipboardService,FileShredder}.cs` — OS‑Integration hinter
  `Platform.Abstractions`‑Interfaces.
- `tests/AuraVault.Integration.Tests/KdbxRoundTripTests.cs` + `fixtures/kdbx/` — KeePassXC‑erzeugte
  Interop‑Fixtures und das Round‑Trip‑Gate.
