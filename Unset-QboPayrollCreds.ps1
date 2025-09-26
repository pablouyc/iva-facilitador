# === Unset-QboPayrollCreds.ps1 ===
[Environment]::SetEnvironmentVariable("IntuitAuth__ClientId",     $null, "User")
[Environment]::SetEnvironmentVariable("IntuitAuth__ClientSecret", $null, "User")
[Environment]::SetEnvironmentVariable("IntuitAuth__RedirectUri",  $null, "User")
[Environment]::SetEnvironmentVariable("IntuitAuth__Environment",  $null, "User")
[Environment]::SetEnvironmentVariable("IntuitAuth__Scopes",       $null, "User")
Write-Host "Variables de QBO Payroll eliminadas del perfil de usuario." -ForegroundColor Yellow
