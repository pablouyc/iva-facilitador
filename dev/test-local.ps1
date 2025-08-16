param([switch]$Clean)
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
if($Clean){ dotnet clean $Root\IvaFacilitador.sln }
dotnet restore $Root\IvaFacilitador.sln
dotnet build   $Root\IvaFacilitador.sln -c Release
dotnet test    $Root\IvaFacilitador.sln -c Release --no-build
