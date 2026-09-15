# ApexSenseBridge 0.6.3

ApexSenseBridge 0.6.3 is a defensive installer and update-integrity release.
It follows a report of keyboard and mouse input remaining available in BIOS but
not in Windows after ApexSenseBridge and USBip were uninstalled together. The
exact prior failure cannot be reconstructed from the wiped system, so this
release removes the risky coupling instead of assuming a single root cause.

## Uninstall safety

- ApexSenseBridge never invokes the USBip uninstaller. USBip is a shared kernel
  prerequisite whose filter removal restarts USB hubs; users who intentionally
  want to remove it must do so separately in Windows Settings.
- The Control Panel no longer passes the legacy `/REMOVEDEPENDENCIES` option,
  and the installer no longer implements that option.
- HidHide is kept by default. It can be removed only from an interactive,
  default-No prompt after exact product version, service state and a provenance
  marker written after a successful ApexSenseBridge installation are verified.
- Silent uninstall keeps USBip and HidHide with no driver-removal override.
- Settings, logs, learned executable bindings and Playnite profiles are kept by
  default. They have a separate default-No prompt; `/REMOVEUSERDATA` is the
  only silent opt-in and affects data only.

## Safer prerequisite handling

- Setup rejects unknown, mismatched, incomplete or orphaned HidHide state
  instead of overwriting it with the bundled version.
- HidHide provenance is recorded only after its product registration, pinned
  version and Windows service are confirmed.
- Existing USBip defensive checks remain in place: only the pinned 0.9.8.0
  registration with both expected services is accepted, and in-place upgrades
  are not attempted.

## Update and release integrity

- Tray, Playnite and PowerShell updaters select only the exact
  `ApexSenseBridge-Setup.exe` asset from this repository's HTTPS GitHub release
  path.
- A downloaded installer is rejected before elevation if its product/version
  metadata is wrong, its Authenticode signature is missing or untrusted, or its
  publisher differs from the installed ApexSenseBridge binary.
- A single version contract covers CMake, installer, native resources, Tray,
  Playnite and updater fallbacks. GitHub Actions verifies the tag before build,
  runs CTest, checks artifact names and SHA-256 values, and requires one trusted
  publisher across every executable release payload before publishing.
- A narrow Tray shutdown race is fixed: a newly learned executable binding is
  now marked dirty before readers can observe it, so immediate shutdown cannot
  skip its synchronous cache flush.

## Upgrade

Install 0.6.3 over the existing application. Setup reuses an exact supported
USBip/HidHide installation and preserves application data. Restart Windows only
when setup requests it. If USBip 0.9.7.x or incomplete driver state is detected,
follow the setup guidance: remove that package from Windows Settings, restart,
then run the 0.6.3 setup again.

The full release checklist is in [`RELEASE_CHECKLIST.md`](RELEASE_CHECKLIST.md).
