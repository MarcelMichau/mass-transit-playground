$testProject = "WeatherApi.Tests\WeatherApi.Tests.csproj"

$counter = 0

do {
  $counter++

  dotnet test $testProject
  $exitCode = $?

  Write-Host "Iteration: $counter, Exit code: $exitCode"

} while ($exitCode -eq $True -and $counter -lt 100)
