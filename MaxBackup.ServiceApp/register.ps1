#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

Write-Information "Registering..."

Write-Information "Setting Environment Variables"

[System.Environment]::SetEnvironmentVariable('MAXBACKUP_DOTNET_ENVIRONMENT', 'Production', 'User')

$configpath = Resolve-Path ~
$configpath = "$configpath\maxbackupconfig.json"

Write-Information "Checking for config file $configpath"

if (Test-Path -Path $configpath) {
	Write-Information "Config file already exists"
} else {
	Write-Information "Creating config file"

	Copy-Item -Path 'Default_maxbackupconfig.json' -Destination $configpath
}

Write-Information "Checking if service exists"

$ServiceName = "MaxBackup"

$service = Get-Service -Name "$ServiceName" -ErrorAction SilentlyContinue

if ($service -eq $null) {
	Write-Information "Service does not exist"
} else {
	Write-Information "Service Exists"

	if ($service.Status -ne "Stopped") {
		Write-Information "Service state is $($service.Status)"
		Write-Information "Stopping service"

		$stopcount = 0
		while ((Get-Service "$ServiceName").Status -ne 'Stopped')
		{
			if ($stopcount -gt 5) {
				throw "Unable to stop service"
			}

			Stop-Service "$ServiceName" -ErrorAction SilentlyContinue
			Start-Sleep 3
			$stopcount = $stopcount + 1
		}

		Write-Information "Service stopped"

		# give the FS time to cool down
		Start-Sleep 5
	}
}

Write-Information "Copying files"

$installpathfoldername = "MaxBackup"
$installpathroot = "$env:LocalAppData\\Programs"
$installpath = "$installpathroot\\$installpathfoldername"

if (Test-Path -Path $installpath) {
	Write-Information "Deleting old files..."

	try {
		Remove-Item "$installpath\*" -Recurse -Force
	} catch {
		Start-Sleep 5
		Remove-Item "$installpath\*" -Recurse -Force
	}
} else {
	Write-Information "Creating directory $installpath"

	New-Item -Path "$installpathroot" -Name "$installpathfoldername" -ItemType Directory
}

$sourcefolder = (Resolve-Path ".\\bin\\Release\\net6.0-windows")

Get-ChildItem "$sourcefolder" | Copy-Item -Destination "$installpath" -Recurse

if ($service -eq $null) {
	Write-Information "Configuring service"

	# register credentials of current user
	#$cred = get-credentials

	$binpath = "$installpath\\MaxBackup.ServiceApp.exe"

	New-Service -name $ServiceName -binaryPathName $binpath -displayName $ServiceName -startupType Automatic # -credential $cred
}

Write-Information "Starting service..."

$startcount = 0
while ((Get-Service "$ServiceName").Status -ne 'Running')
{
	if ($startcount -gt 5) {
		throw "Unable to start service"
	}

	Start-Service "$ServiceName" -ErrorAction SilentlyContinue
	Start-Sleep 3
	$startcount = $startcount + 1
}

Write-Information "Service started"

Write-Information "Done"
