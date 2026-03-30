$ErrorActionPreference = "Stop"

$racineProjet = Split-Path -Parent $MyInvocation.MyCommand.Path
$cheminProjetTests = Join-Path $racineProjet "RA.Compagnon.Tests\RA.Compagnon.Tests.csproj"

& dotnet run --project $cheminProjetTests

if ($LASTEXITCODE -ne 0) {
    throw "Les tests cibles ont echoue."
}
