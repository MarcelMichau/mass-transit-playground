for ($i = 1; $i -le 100; $i++) {
  if ($i % 2 -eq 0) {
    $url = "https://localhost:7001/weatherforecast?location=Test%20City%201"
  }
  else {
    $url = "https://localhost:7002/weatherforecast?location=Test%20City%202"
  }
  Invoke-WebRequest -Uri $url -Method Post -UseBasicParsing
}
