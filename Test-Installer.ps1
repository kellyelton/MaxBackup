param(
    [Parameter(Mandatory)]
    [string]$Path
)

$ErrorActionPreference = 'Stop'

function Assert-Installer($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Read-MsiTable([string]$Query, [int]$Columns) {
    $view = $database.OpenView($Query)
    try {
        [void]$view.Execute()
        while ($record = $view.Fetch()) {
            try {
                $values = for ($i = 1; $i -le $Columns; $i++) { $record.StringData($i) }
                ,$values
            } finally {
                [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
            }
        }
    } finally {
        [void]$view.Close()
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
    }
}

$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $null
$session = $null
try {
    $msiPath = (Resolve-Path -LiteralPath $Path).Path
    $database = $installer.OpenDatabase($msiPath, 0)
    $properties = @{}
    Read-MsiTable 'SELECT `Property`, `Value` FROM `Property`' 2 | ForEach-Object { $properties[$_[0]] = $_[1] }
    foreach ($flag in @('REMOVECONFIG', 'REMOVELOGS')) {
        Assert-Installer ($properties[$flag] -eq '0') "$flag must exist and default to off."
    }
    $removals = @(Read-MsiTable 'SELECT `FileKey`, `Component_`, `FileName`, `DirProperty`, `InstallMode` FROM `RemoveFile`' 5)
    $actions = @(Read-MsiTable 'SELECT `Action`, `Type`, `Source`, `Target` FROM `CustomAction`' 4)
    $sequence = @(Read-MsiTable 'SELECT `Action`, `Condition`, `Sequence` FROM `InstallExecuteSequence`' 3)

    # Open a session only to evaluate MSI conditions. Do not execute installer actions.
    $session = $installer.OpenPackage($msiPath, 1)
    $targets = @(
        @('REMOVECONFIG', 'ConfigCleanupDirectory', 'MAXBACKUPDATAFOLDER', 'config.json'),
        @('REMOVELOGS', 'LogsCleanupDirectory', 'MAXBACKUPLOGSFOLDER', 'service*.log')
    )
    $dataRemovals = @($removals | Where-Object { $_[1] -eq 'ProgramDataFolderComponent' })
    Assert-Installer ($dataRemovals.Count -eq 2) 'Expected exactly two gated data-removal rules.'

    foreach ($target in $targets) {
        $flag, $directoryProperty, $directory, $fileName = $target
        Assert-Installer ($flag -cin ($properties['SecureCustomProperties'] -split ';')) "$flag must reach the elevated installer."
        Assert-Installer (-not $properties.ContainsKey($directoryProperty)) "$directoryProperty must be unset by default."

        $rule = @($dataRemovals | Where-Object { $_[3] -ceq $directoryProperty })
        Assert-Installer ($rule.Count -eq 1) "Expected one removal rule for $flag."
        Assert-Installer (($rule[0][2] -split '\|')[-1] -ceq $fileName) "$flag targets the wrong files."
        Assert-Installer ($rule[0][4] -eq '2') "$flag must apply only to uninstall."

        $setter = @($actions | Where-Object { $_[2] -ceq $directoryProperty })
        Assert-Installer ($setter.Count -eq 1) "Expected one conditional directory setter for $flag."
        Assert-Installer ($setter[0][1] -eq '51' -and $setter[0][3] -ceq "[$directory]") "$flag must use the known data directory."
        $scheduled = @($sequence | Where-Object { $_[0] -eq $setter[0][0] })
        Assert-Installer ($scheduled.Count -eq 1) "$flag must be gated in the execute sequence."
        $validate = @($sequence | Where-Object { $_[0] -eq 'InstallValidate' })[0]
        $removeFiles = @($sequence | Where-Object { $_[0] -eq 'RemoveFiles' })[0]
        Assert-Installer ([int]$scheduled[0][2] -gt [int]$validate[2] -and [int]$scheduled[0][2] -lt [int]$removeFiles[2]) "$flag must be set after validation and before file removal."

        # Exercise the packaged condition with Windows Installer's own evaluator.
        foreach ($installed in @('', '1')) {
            foreach ($remove in @('', 'BackupServiceFeature', 'ALL')) {
                foreach ($upgrade in @('', '{11111111-1111-1111-1111-111111111111}')) {
                    foreach ($configFlag in @('', '0', '1', '2')) {
                        foreach ($logsFlag in @('', '0', '1', '2')) {
                            $session.Property('Installed') = $installed
                            $session.Property('REMOVE') = $remove
                            $session.Property('UPGRADINGPRODUCTCODE') = $upgrade
                            $session.Property('REMOVECONFIG') = $configFlag
                            $session.Property('REMOVELOGS') = $logsFlag
                            $selectedFlag = if ($flag -eq 'REMOVECONFIG') { $configFlag } else { $logsFlag }
                            $expected = [int]($installed -eq '1' -and $remove -eq 'ALL' -and $upgrade -eq '' -and $selectedFlag -eq '1')
                            $actual = $session.EvaluateCondition($scheduled[0][1])
                            Assert-Installer ($actual -eq $expected) "$flag condition failed: Installed=$installed REMOVE=$remove Upgrade=$upgrade REMOVECONFIG=$configFlag REMOVELOGS=$logsFlag"
                        }
                    }
                }
            }
        }
    }

    # No additional rule may directly target the persistent data directories.
    Assert-Installer (@($removals | Where-Object { $_[3] -in @('MAXBACKUPDATAFOLDER', 'MAXBACKUPLOGSFOLDER') }).Count -eq 0) 'Found an ungated data-removal rule.'
    Write-Output 'Installer retention checks passed (384 native condition evaluations).'
} finally {
    foreach ($com in @($session, $database, $installer)) {
        if ($null -ne $com) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($com) }
    }
}
