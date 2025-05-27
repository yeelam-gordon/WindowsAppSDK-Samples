Param(
    [Parameter(Mandatory=$false)]
    [string]$Platform = "x64",
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Debug",
    [switch]$Clean
)

if ($Clean) {
    $CleanTargets = @(
      'bin'
      'obj'
    )

    $ProjectRoot = (Join-Path $PSScriptRoot "WinUI")
    foreach ($CleanTarget in $CleanTargets)
    {
      $CleanTargetPath = (Join-Path $ProjectRoot $CleanTarget)
      if (Test-Path ($CleanTargetPath)) {
        Remove-Item $CleanTargetPath -recurse
      }
    }
    Get-AppxPackage -Name "WindowsAISampleForWinUISparse" | Remove-AppxPackage
}

function Get-UserPath
{
    $root = Join-Path (Get-Item $PSScriptRoot).FullName "WinUI"
    $user = Join-Path $root '.user'
    if (-not(Test-Path -Path $user -PathType Container))
    {
        $null = New-Item -ItemType Directory -Path $user
    }
    if (-not(Test-Path -Path $user -PathType Container))
    {
        throw ".user directory creation failed"
    }
    return $user
}

function New-CertificateIfNeeded
{
    param (
        [string]$user
    )
    $cert = Join-Path $user 'SampleCompanionAppTest.pfx'
    if (-not(Test-Path -Path $cert))
    {
        $signer = Join-Path $user 'SampleCompanionAppTest.pvk'
        $signerPub = Join-Path $user 'SampleCompanionAppTest.cer'
        $makecertPath = (Get-Command -CommandType Application makecert.exe | Select-Object -first 1).Source
        & $makecertPath "-sv" $signer "-r" "-n" "CN=Fabrikam Corporation, O=Fabrikam Corporation, L=Redmond, S=Washington, C=US" "-b" "01/01/2000" "-e" "01/01/2040" "-sky" "exchange" $signerPub
        $pvk2pfxPath = (Get-Command -CommandType Application pvk2pfx.exe | Select-Object -first 1).Source
        & $pvk2pfxPath "-pvk" $signer "-spc" $signerPub "-pfx" $cert
    }
    return $cert
}

function Install-Certificate
{
    param (
        [string]$cert
    )
    # Make sure the cert isn't installed
    $installed = Get-ChildItem -path Cert:\CurrentUser\TrustedPeople | Where-Object {$_.Subject -match "Fabrikam" }
    if ($installed -eq $null)
    {
        Import-PfxCertificate $cert -CertStoreLocation Cert:\CurrentUser\TrustedPeople
    }
    else 
    {
        # Already installed
    }
}

function MakeSparsePackage
{
    $Solution = Join-Path (Get-Item $PSScriptRoot).FullName 'WindowsAISampleForWinUI.sln'
    $MSBuildPath = Get-Command MSBuild.exe | Select-Object -ExpandProperty Definition
    & $MSBuildPath $Solution -m -p:Configuration=$Configuration -p:Platform=$Platform -p:BuildProjectReferences=true -p:RestorePackagesConfig=true -p:WindowsAppSDKSelfContained=true -Restore -t:rebuild

    $manifests = Join-Path (Get-Item $PSScriptRoot).FullName "WinUI\AppxManifest.xml"
    $outputdir = Join-Path (Get-Item $PSScriptRoot).FullName "WinUI\bin\$Platform\$Configuration\net8.0-windows10.0.22621.0"
    $outputpath = Join-Path $outputdir "WindowsAISampleForWinUISparse.msix"
    $makeappxPath = (Get-Command -CommandType Application makeappx.exe | Select-Object -first 1).Source
    $outputargsInit = @(
        'pack',
        '/v',
        '/o',
        '/d',
        (Join-Path (Get-Item $PSScriptRoot).FullName "WinUI"),
        '/p',
        $outputpath,
        '/nv'
    )
    Write-Host "Creating msix: $makeappxPath $outputargsInit"
    & $makeappxPath $outputargsInit
    $user = Get-UserPath
    $cert = New-CertificateIfNeeded -user $user
    Install-Certificate -cert $cert
    $signToolPath = (Get-Command -CommandType Application signtool.exe | Select-Object -first 1).Source
    & $signToolPath "sign" "/a" "/v" "/fd" "SHA256" "/f" $cert $outputpath
}

MakeSparsePackage