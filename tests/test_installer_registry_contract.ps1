$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$installerPath = Join-Path $projectRoot "installer\ApexSenseBridge.iss"
$manifestPath = Join-Path $projectRoot "installer\driver-manifest.json"
$controlPanelPath = Join-Path $projectRoot "src\control\ControlPanel.cpp"
$installer = Get-Content -LiteralPath $installerPath -Raw
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$controlPanel = Get-Content -LiteralPath $controlPanelPath -Raw

function Assert-Contract([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "Installer registry contract failed: $Message"
    }
}

function Get-QuotedDefine([string]$Name) {
    $pattern = '(?m)^\s*#define\s+' + [Regex]::Escape($Name) +
               '\s+"([^"]+)"\s*$'
    $match = [Regex]::Match($installer, $pattern)
    Assert-Contract $match.Success "missing quoted define $Name"
    return $match.Groups[1].Value
}

$usbipProductCode = [string]$manifest.'usbip-win2'.productCode
$hidHideProductCode = [string]$manifest.HidHide.productCode

Assert-Contract (
    (Get-QuotedDefine "UsbipProductKeyPascal") -ceq $usbipProductCode
) "USBip Pascal key differs from driver-manifest.json"
Assert-Contract (
    (Get-QuotedDefine "UsbipProductKey") -ceq ('{' + $usbipProductCode)
) "USBip section key is not escaped exactly once for Inno Setup"
Assert-Contract (
    (Get-QuotedDefine "HidHideProductCodePascal") -ceq $hidHideProductCode
) "HidHide Pascal key differs from driver-manifest.json"
Assert-Contract (
    (Get-QuotedDefine "HidHideProductCode") -ceq ('{' + $hidHideProductCode)
) "HidHide section key is not escaped exactly once for Inno Setup"

Assert-Contract ([Regex]::IsMatch(
    $installer,
    '(?s)function\s+UsbipUninstallKey:\s*String;.*?' +
    '\{#UsbipProductKeyPascal\}.*?end;'
)) "UsbipUninstallKey does not use the verbatim Pascal key"
Assert-Contract ([Regex]::IsMatch(
    $installer,
    '(?s)function\s+HidHideUninstallKey:\s*String;.*?' +
    '\{#HidHideProductCodePascal\}.*?end;'
)) "HidHideUninstallKey does not use the verbatim Pascal key"

# USBip's filter installer/remover restarts the USB hubs. ApexSenseBridge must
# therefore never initiate USBip removal, including through a legacy switch or
# the Control Panel launcher.
Assert-Contract (-not $installer.Contains('UsbipOwnerProof')) `
    "obsolete USBip removal provenance remains in the installer"
Assert-Contract (-not $installer.Contains('ShouldRemoveUsbip')) `
    "the installer still contains an automated USBip removal path"
Assert-Contract (-not $installer.Contains('GetUsbipUninstaller')) `
    "the installer still resolves the USBip uninstaller"
Assert-Contract (-not $installer.Contains('AuditUsbipRemoval')) `
    "the installer still contains USBip post-removal machinery"
Assert-Contract (-not $installer.Contains('/REMOVEDEPENDENCIES')) `
    "the installer still accepts the unsafe legacy dependency-removal switch"
Assert-Contract (-not $controlPanel.Contains('/REMOVEDEPENDENCIES')) `
    "Control Panel still requests automatic dependency removal"
Assert-Contract (-not [Regex]::IsMatch(
    $installer,
    '(?im)^Filename:.*(?:usbip|Usbip).*Check:\s*ShouldRemove'
)) "an UninstallRun entry can still remove USBip"

# HidHide may only be removed after a successful fresh install has written an
# immutable proof, exact registration/service checks still pass, and the user
# separately opts in. Silent removal remains preserve-only.
Assert-Contract ([Regex]::IsMatch(
    $installer,
    '(?m)^Filename:.*HidHide_1\.5\.230_x64\.exe.*AfterInstall:\s*VerifyHidHideInstall'
)) "HidHide installation is not followed by verification"
Assert-Contract ([Regex]::IsMatch(
    $installer,
    '(?s)procedure\s+VerifyHidHideInstall;.*?' +
    "DisplayVersion.*?Services\\HidHide.*?RegWriteStringValue.*?'HidHideOwnerProof'.*?end;"
)) "HidHide provenance is written before exact registration/service verification"
Assert-Contract ([Regex]::IsMatch(
    $installer,
    '(?s)function\s+ShouldRemoveHidHide:\s*Boolean;.*?' +
    'RemoveHidHideApproved\s+and\s+IsSafelyOwnedHidHide'
)) "HidHide can be removed without explicit approval and a final safety check"
Assert-Contract ([Regex]::IsMatch(
    $installer,
    '(?s)function\s+InitializeUninstall:\s*Boolean;.*?' +
    'if\s+UninstallSilent\s+then\s+Exit;.*?' +
    'MB_YESNO\s+or\s+MB_DEFBUTTON2.*?' +
    'MB_YESNO\s+or\s+MB_DEFBUTTON2'
)) "silent uninstall is not preserve-only or an interactive prompt defaults to Yes"
Assert-Contract ($installer.Contains("HasCommandLineParameter('/REMOVEUSERDATA')")) `
    "silent user-data removal has no explicit switch"
Assert-Contract ([Regex]::Matches(
    $installer,
    '(?m)^Type:\s*filesandordirs;\s*Name:.*ExtensionsData.*Check:\s*ShouldDeleteUserData'
).Count -eq 2) "Playnite profile data is deleted without explicit consent"
Assert-Contract ([Regex]::IsMatch(
    $installer,
    '(?m)^Type:\s*filesandordirs;\s*Name:\s*"\{localappdata\}\\ApexSenseBridge";\s*Check:\s*ShouldDeleteUserData'
)) "local settings/logs are deleted without explicit consent"
Assert-Contract ($installer.Contains('AfterInstall: AuditHidHideRemoval')) `
    "HidHide removal has no post-operation audit"
Assert-Contract ([Regex]::IsMatch(
    $installer,
    '(?s)function\s+UninstallNeedRestart:\s*Boolean;.*?' +
    'Result\s*:=\s*RemoveHidHideApproved'
)) "driver removal does not require a Windows restart"

Write-Output "Installer safety contract passed."
