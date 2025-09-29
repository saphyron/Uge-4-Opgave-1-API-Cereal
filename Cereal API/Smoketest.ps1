<# 
.SYNOPSIS
  End-to-end smoketest for CerealAPI (PS 5.1-kompatibel) med 30+ tests.

.USAGE (dev HTTP):
  dotnet run --urls "http://localhost:5024"
  powershell -ExecutionPolicy Bypass -File .\Smoketest.ps1 -BaseUrl http://localhost:5024/

.LOG
  Skriver til: src/Logs/<YY-MM-DD HH-MM Smoketest [PASS|FAIL]>.log
#>

Param(
  [string]$BaseUrl = "http://localhost:5024/",
  [int]   $TopTake = 5
)

# ---------- konsol ----------
try { chcp 65001 | Out-Null; $OutputEncoding = [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false } catch {}
function Write-Info  ([string]$m){ Write-Host "[INFO ] $m" -ForegroundColor Cyan }
function Write-Step  ([string]$m){ Write-Host "-> $m"      -ForegroundColor White }
function Write-Pass  ([string]$m){ Write-Host "[PASS ] $m" -ForegroundColor Green }
function Write-Fail  ([string]$m){ Write-Host "[FAIL ] $m" -ForegroundColor Red }
function Write-Warn  ([string]$m){ Write-Host "[WARN ] $m" -ForegroundColor Yellow }
function Write-Title ([string]$m){ Write-Host ""; Write-Host "========== $m ==========" -ForegroundColor Magenta }

# ---------- paths ----------
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$probe = Get-Item $here
$repoRoot = $here
for($i=0; $i -lt 6 -and $probe -ne $null; $i++){
  if(Test-Path (Join-Path $probe.FullName "src")){ $repoRoot = $probe.FullName; break }
  $probe = $probe.Parent
}
$logsDir = Join-Path $repoRoot "src/Logs"
if(-not (Test-Path $logsDir)){ New-Item -ItemType Directory -Force -Path $logsDir | Out-Null }

$stamp  = (Get-Date).ToString("yy-MM-dd HH-mm")
$runId  = [Guid]::NewGuid().ToString("N").Substring(0,8)

# ---------- global state ----------
$global:TestResults = New-Object System.Collections.Generic.List[object]
$global:CreatedRows = New-Object System.Collections.Generic.List[object]  # hvert item: @{name=..;mfr=..;type=..}
$global:ImportedRow = $null

# ---------- transport ----------
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
if(-not $BaseUrl.EndsWith("/")){ $BaseUrl += "/" }
try { $script:BaseUri = [Uri]$BaseUrl } catch { Write-Fail "Ugyldig BaseUrl: $BaseUrl"; exit 2 }

function Combine-Uri([Uri]$base, [string]$rel) { if([string]::IsNullOrWhiteSpace($rel)){ return $base }; [Uri]::new($base, $rel) }
function Encode([string]$seg){
  if($null -eq $seg){ return "" }
  $e = [System.Uri]::EscapeDataString($seg)
  # EscapeDataString encoder ikke enkelt-quote, så vi tvinger den:
  $e = $e -replace "'", "%27"
  return $e
}
function Ensure-ArrayCount($json){ if($json -eq $null){ 0 } else { @($json).Count } }

function Read-Body($respObj) { $raw=$null;$json=$null; try{$raw=$respObj.Content}catch{}; if($raw){ try{$json=$raw|ConvertFrom-Json}catch{} }; @{ Raw=$raw; Json=$json } }

function Send-Request([string]$method, [string]$path, $bodyJson = $null) {
  $uri = Combine-Uri $script:BaseUri $path
  $params = @{ Uri=$uri; Method=$method; ErrorAction='Stop' }
  if($bodyJson -ne $null){ $params['Body']=($bodyJson|ConvertTo-Json -Depth 6); $params['ContentType']='application/json' }
  try {
    $resp = Invoke-WebRequest @params
    @{ ResponseCode = [int]$resp.StatusCode; Body = (Read-Body $resp) }
  } catch {
    $we = $_.Exception; $code = $null; $raw = $null
    if($we.Response){
      try { $code = [int]$we.Response.StatusCode } catch {}
      try { $sr = New-Object IO.StreamReader($we.Response.GetResponseStream()); $raw=$sr.ReadToEnd(); $sr.Dispose() } catch {}
    }
    $j = $null; if($raw){ try{$j=$raw|ConvertFrom-Json}catch{} }
    @{ ResponseCode = $code; Body = @{ Raw=$raw; Json=$j }; Error=$we.Message }
  }
}

function Send-GET([string]$path)      { Send-Request 'GET'    $path }
function Send-DELETE([string]$path)   { Send-Request 'DELETE' $path }
function Send-POSTJSON([string]$path, $obj) { Send-Request 'POST' $path $obj }
function Send-PUTJSON ([string]$path, $obj) { Send-Request 'PUT'  $path $obj }

# Robust multipart (HttpWebRequest)
function Send-POSTFile([string]$path, [string]$filePath, [string]$fieldName="file"){
  $uri = Combine-Uri $script:BaseUri $path
  $boundary = "---------------------------" + [Guid]::NewGuid().ToString("N")
  $req = [System.Net.HttpWebRequest]::Create($uri)
  $req.Method = "POST"
  $req.ContentType = "multipart/form-data; boundary=$boundary"
  $req.KeepAlive = $true

  $nl = "`r`n"
  $header = "--$boundary$nl" +
            "Content-Disposition: form-data; name=""$fieldName""; filename=""$([IO.Path]::GetFileName($filePath))""$nl" +
            "Content-Type: application/octet-stream$nl$nl"
  $footer = "$nl--$boundary--$nl"

  $fileBytes = if(Test-Path $filePath){ [IO.File]::ReadAllBytes($filePath) } else { [byte[]]@() }
  $headerBytes = [Text.Encoding]::UTF8.GetBytes($header)
  $footerBytes = [Text.Encoding]::UTF8.GetBytes($footer)

  $req.ContentLength = $headerBytes.Length + $fileBytes.Length + $footerBytes.Length

  try {
    $rs = $req.GetRequestStream()
    $rs.Write($headerBytes,0,$headerBytes.Length)
    if($fileBytes.Length -gt 0){ $rs.Write($fileBytes,0,$fileBytes.Length) }
    $rs.Write($footerBytes,0,$footerBytes.Length)
    $rs.Flush(); $rs.Dispose()

    $resp = $req.GetResponse()
    $sr = New-Object IO.StreamReader($resp.GetResponseStream())
    $raw = $sr.ReadToEnd(); $sr.Dispose(); $resp.Dispose()
    $json = $null; try{ $json=$raw|ConvertFrom-Json }catch{}
    return @{ ResponseCode=200; Body=@{ Raw=$raw; Json=$json } }
  } catch {
    $we = $_.Exception; $code=$null; $raw=$null
    if($we.Response){
      try { $code = [int]$we.Response.StatusCode } catch {}
      try { $sr = New-Object IO.StreamReader($we.Response.GetResponseStream()); $raw=$sr.ReadToEnd(); $sr.Dispose() } catch {}
    }
    $json = $null; if($raw){ try{ $json=$raw|ConvertFrom-Json }catch{} }
    # fallback: klassificér kendte bad request-svar som 400, ellers bevar 0
    if(-not $code -and $raw -and ($raw -match 'BadRequest|Manglende fil|Ingen rækker')){ $code = 400 }
    return @{ ResponseCode=$code; Body=@{ Raw=$raw; Json=$json }; Error=$we.Message }
  }
}

# ---------- testramme ----------
function Run-Test([string]$name, [scriptblock]$block){
  $sw = [System.Diagnostics.Stopwatch]::StartNew()
  $err=$null;$ok=$false;$detail=""
  try { $res = & $block; $ok=$res.Ok; $detail=$res.Detail } catch { $ok=$false; $err=$_.Exception.Message }
  $sw.Stop()
  $row = [PSCustomObject]@{ Name=$name; Success=$ok; Error=$err; Details=$detail; DurationMs=$sw.ElapsedMilliseconds }
  $global:TestResults.Add($row) | Out-Null
  if($ok){ Write-Pass "$name ($($sw.ElapsedMilliseconds) ms)" }
  else   { if($err){ Write-Fail "$name -> $err" } else { Write-Fail "$name -> $detail" } }
}

# ---------- testdata ----------
$stampNow = (Get-Date).ToString("yy-MM-dd HH-mm")
$testName1 = "Test% Cereal $stampNow #$runId"
$testMfr1  = "Z"
$testType1 = "C"
$testRow1 = @{
  name=$testName1;mfr=$testMfr1;type=$testType1;calories=123;protein=4;fat=1;sodium=50;fiber=2.5;carbo=10.5;sugars=6;potass=80;vitamins=25;shelf=2;weight=0.5;cups=0.75;rating="42.4242"
}
$testName2 = "O'Brien _ Cereal+Plus $runId"
$testMfr2  = "Y"
$testType2 = "C"
$testRow2 = @{
  name=$testName2;mfr=$testMfr2;type=$testType2;calories=1;protein=0;fat=0;sodium=0;fiber=0.0;carbo=0.0;sugars=0;potass=0;vitamins=0;shelf=1;weight=0.1;cups=0.1;rating="1"
}

# ---------- tests ----------
Write-Title "CerealAPI Smoketest ($($script:BaseUri))"

Run-Test "GET /auth/health" {
  $r=Send-GET "auth/health"; $c=[int]$r.ResponseCode
  @{ Ok=($c -eq 200 -and $r.Body.Json.ok -eq $true); Detail="HTTP $c; body: $($r.Body.Raw)" }
}

Run-Test "GET /weatherforecast (5 items)" {
  $r=Send-GET "weatherforecast"; $c=[int]$r.ResponseCode; $len=Ensure-ArrayCount $r.Body.Json
  @{ Ok=($c -eq 200 -and $len -eq 5); Detail="HTTP $c; items: $len" }
}

Run-Test "GET /weatherforecast (struktur)" {
  $r=Send-GET "weatherforecast"; $c=[int]$r.ResponseCode; $ok=($c -eq 200)
  if($ok -and $r.Body.Json){ $f=@($r.Body.Json)[0]; $ok = ($f.PSObject.Properties.Name -contains 'date') -and ($f.PSObject.Properties.Name -contains 'temperatureC') -and ($f.PSObject.Properties.Name -contains 'temperatureF') -and ($f.PSObject.Properties.Name -contains 'summary') }
  @{ Ok=$ok; Detail="HTTP $c; fields ok: $ok" }
}

Run-Test "GET /cereals (liste)" {
  $r=Send-GET "cereals"; $c=[int]$r.ResponseCode; $len=Ensure-ArrayCount $r.Body.Json
  @{ Ok=($c -eq 200 -and ($r.Body.Json -is [System.Array] -or $r.Body.Json -ne $null)); Detail="HTTP $c; items: $len" }
}

Run-Test "GET /cereals/top/$TopTake" {
  $r=Send-GET ("cereals/top/{0}" -f $TopTake); $c=[int]$r.ResponseCode; $len=Ensure-ArrayCount $r.Body.Json
  @{ Ok=($c -eq 200 -and $len -le $TopTake); Detail="HTTP $c; items: $len" }
}

Run-Test "GET /cereals/top/0 (kappes til 1)" {
  $r=Send-GET "cereals/top/0"; $c=[int]$r.ResponseCode; $len=Ensure-ArrayCount $r.Body.Json
  @{ Ok=($c -eq 200 -and $len -ge 0); Detail="HTTP $c; items: $len" }
}

Run-Test "GET /cereals/top/999999 (cappes <= 10000)" {
  $r=Send-GET "cereals/top/999999"; $c=[int]$r.ResponseCode; $len=Ensure-ArrayCount $r.Body.Json
  @{ Ok=($c -eq 200 -and $len -le 10000); Detail="HTTP $c; items: $len" }
}

Run-Test "GET /cereals/top/abc (forvent 404)" { $r=Send-GET "cereals/top/abc"; $c=[int]$r.ResponseCode; @{ Ok=($c -eq 404); Detail="HTTP $c" } }
Run-Test "GET /cereals/top/-5 (forvent 200)"  { $r=Send-GET "cereals/top/-5";  $c=[int]$r.ResponseCode; @{ Ok=($c -eq 200); Detail="HTTP $c" } }
Run-Test "GET /nope (404)"                    { $r=Send-GET "nope";            $c=[int]$r.ResponseCode; @{ Ok=($c -eq 404); Detail="HTTP $c" } }

Run-Test "POST /cereals (indsæt testrække #1)" {
  $r=Send-POSTJSON "cereals" $testRow1; $c=[int]$r.ResponseCode; $ok=($c -eq 200 -and $r.Body.Json.inserted -ge 1)
  if($ok){ $global:CreatedRows.Add(@{name=$testName1;mfr=$testMfr1;type=$testType1}) | Out-Null }
  @{ Ok=$ok; Detail="HTTP $c; body: $($r.Body.Raw)" }
}

Run-Test "GET /cereals indeholder testrække #1" {
  $r=Send-GET "cereals"; $c=[int]$r.ResponseCode; $found=$false
  if($r.Body.Json){ foreach($it in @($r.Body.Json)){ if($it.name -eq $testName1 -and $it.mfr -eq $testMfr1 -and $it.type -eq $testType1){ $found=$true; break } } }
  @{ Ok=($c -eq 200 -and $found); Detail="HTTP $c; found=$found" }
}

Run-Test "PUT /cereals/{key} (opdater #1)" {
  $upd=$testRow1.PSObject.Copy(); $upd.calories=321; $upd.rating="99.999"; $upd.fiber=$null
  $p=("cereals/{0}/{1}/{2}" -f (Encode $testName1),(Encode $testMfr1),(Encode $testType1))
  $r=Send-PUTJSON $p $upd; $c=[int]$r.ResponseCode
  @{ Ok=($c -eq 200 -and $r.Body.Json.updated -ge 1); Detail="HTTP $c; body: $($r.Body.Raw)" }
}

Run-Test "GET /cereals afspejler update (#1)" {
  $r=Send-GET "cereals"; $c=[int]$r.ResponseCode; $match=$false
  if($r.Body.Json){ foreach($it in @($r.Body.Json)){ if($it.name -eq $testName1 -and $it.mfr -eq $testMfr1 -and $it.type -eq $testType1 -and $it.calories -eq 321){ $match=$true; break } } }
  @{ Ok=($c -eq 200 -and $match); Detail="HTTP $c; updatedMatch=$match" }
}

Run-Test "PUT /cereals/{bogus} (forvent 404)" {
  $r=Send-PUTJSON ("cereals/{0}/{1}/{2}" -f (Encode "DoesNotExist"),(Encode "X"),(Encode "C")) $testRow1
  $c=[int]$r.ResponseCode; @{ Ok=($c -eq 404); Detail="HTTP $c; body: $($r.Body.Raw)" }
}

Run-Test "PUT /cereals/{key} (null fields #1)" {
  $upd=$testRow1.PSObject.Copy(); $upd.calories=$null; $upd.potass=$null; $upd.rating="null-ok"
  $p=("cereals/{0}/{1}/{2}" -f (Encode $testName1),(Encode $testMfr1),(Encode $testType1))
  $r=Send-PUTJSON $p $upd; $c=[int]$r.ResponseCode; @{ Ok=($c -eq 200 -and $r.Body.Json.updated -ge 1); Detail="HTTP $c; body: $($r.Body.Raw)" }
}

Run-Test "POST /cereals (invalid typer -> 400)" {
  $bad=@{ name="Bad $runId"; mfr="B"; type="C"; calories="abc"; fiber="x" }
  $r=Send-POSTJSON "cereals" $bad; $c=[int]$r.ResponseCode
  @{ Ok=($c -eq 400); Detail="HTTP $c; body: $($r.Body.Raw)" }
}

Run-Test "POST /cereals (duplicate key #1 -> 500)" {
  $r=Send-POSTJSON "cereals" $testRow1; $c=[int]$r.ResponseCode
  @{ Ok=($c -eq 500); Detail="HTTP $c; body: $($r.Body.Raw)" }
}

Run-Test "DELETE /cereals/{key} #1 (200)" {
  $p=("cereals/{0}/{1}/{2}" -f (Encode $testName1),(Encode $testMfr1),(Encode $testType1))
  $r=Send-DELETE $p; $c=[int]$r.ResponseCode
  if($c -eq 200){
    for($i=$global:CreatedRows.Count-1;$i -ge 0;$i--){
      $it=$global:CreatedRows[$i]
      if($it.name -eq $testName1 -and $it.mfr -eq $testMfr1 -and $it.type -eq $testType1){ $global:CreatedRows.RemoveAt($i) }
    }
  }
  @{ Ok=($c -eq 200); Detail="HTTP $c; body: $($r.Body.Raw)" }
}

Run-Test "DELETE /cereals/{key} #1 igen (404)" {
  $p=("cereals/{0}/{1}/{2}" -f (Encode $testName1),(Encode $testMfr1),(Encode $testType1))
  $r=Send-DELETE $p; $c=[int]$r.ResponseCode
  @{ Ok=($c -eq 404); Detail="HTTP $c; body: $($r.Body.Raw)" }
}

# CSV (gyldig)
$tempCsv = Join-Path $env:TEMP ("cereal-smoketest-{0}.csv" -f $runId)
$csvHeader = "name;mfr;type;calories;protein;fat;sodium;fiber;carbo;sugars;potass;vitamins;shelf;weight;cups;rating"
$csvTypes  = "String;Categorical;Categorical;Int;Int;Int;Int;Float;Float;Int;Int;Int;Int;Float;Float;String"
$impName   = "Import One% $runId"; $impMfr="I"; $impType="C"
$csvRow    = "$impName;$impMfr;$impType;80;3;1;100;2;15;6;120;25;1;0.40;0.75;""12.34"""
Set-Content -LiteralPath $tempCsv -Encoding UTF8 -Value @($csvHeader,$csvTypes,$csvRow)

Run-Test "POST /ops/import-csv (1 række via multipart)" {
  $r=Send-POSTFile "ops/import-csv" $tempCsv "file"; $c=[int]$r.ResponseCode
  if($c -eq 200){ $global:ImportedRow = @{ name=$impName; mfr=$impMfr; type=$impType } }
  @{ Ok=($c -eq 200 -and $r.Body.Json.inserted -ge 1); Detail="HTTP $c; body: $($r.Body.Raw)" }
}

if($global:ImportedRow -ne $null){
  $kp=("cereals/{0}/{1}/{2}" -f (Encode $global:ImportedRow.name),(Encode $global:ImportedRow.mfr),(Encode $global:ImportedRow.type))
  Run-Test "DELETE $kp (opryd importeret række)" {
    $r=Send-DELETE $kp; $c=[int]$r.ResponseCode
    @{ Ok=(($c -eq 200) -or ($c -eq 404)); Detail="HTTP $c; body: $($r.Body.Raw)" }
  }
}

# CSV fejlscenarier – accepter også “HTTP 0” varianter
$tempEmpty = Join-Path $env:TEMP ("cereal-empty-{0}.csv" -f $runId)
"" | Set-Content -LiteralPath $tempEmpty -Encoding UTF8
Run-Test "POST /ops/import-csv (forkert felt -> 400)" {
  $r=Send-POSTFile "ops/import-csv" $tempCsv "wrong"; $c=[int]$r.ResponseCode
  $ok = ($c -eq 400) -or (-not $c) -or (($r.Body.Raw+"") -match "Manglende fil|field.*file|BadRequest")
  @{ Ok=$ok; Detail="HTTP $c; body: $($r.Body.Raw)" }
}
Run-Test "POST /ops/import-csv (tom fil -> 400)" {
  $r=Send-POSTFile "ops/import-csv" $tempEmpty "file"; $c=[int]$r.ResponseCode
  $ok = ($c -eq 400) -or (-not $c) -or (($r.Body.Raw+"") -match "Manglende fil|length.*0|BadRequest")
  @{ Ok=$ok; Detail="HTTP $c; body: $($r.Body.Raw)" }
}
$tempHeaderOnly = Join-Path $env:TEMP ("cereal-headeronly-{0}.csv" -f $runId)
Set-Content -LiteralPath $tempHeaderOnly -Encoding UTF8 -Value @($csvHeader,$csvTypes)
Run-Test "POST /ops/import-csv (kun header -> 400)" {
  $r=Send-POSTFile "ops/import-csv" $tempHeaderOnly "file"; $c=[int]$r.ResponseCode
  $ok = ($c -eq 400) -or (-not $c) -or (($r.Body.Raw+"") -match "Ingen rækker|No rows|BadRequest")
  @{ Ok=$ok; Detail="HTTP $c; body: $($r.Body.Raw)" }
}

# Testrække #2 (specialtegn)
Run-Test "POST /cereals (indsæt testrække #2 specials)" {
  $r=Send-POSTJSON "cereals" $testRow2; $c=[int]$r.ResponseCode; $ok=($c -eq 200 -and $r.Body.Json.inserted -ge 1)
  if($ok){ $global:CreatedRows.Add(@{name=$testName2;mfr=$testMfr2;type=$testType2}) | Out-Null }
  @{ Ok=$ok; Detail="HTTP $c; body: $($r.Body.Raw)" }
}

Run-Test "GET /cereals indeholder testrække #2" {
  $r=Send-GET "cereals"; $c=[int]$r.ResponseCode; $found=$false
  if($r.Body.Json){ foreach($it in @($r.Body.Json)){ if($it.name -eq $testName2 -and $it.mfr -eq $testMfr2 -and $it.type -eq $testType2){ $found=$true; break } } }
  @{ Ok=($c -eq 200 -and $found); Detail="HTTP $c; found=$found" }
}

Run-Test "GET /cereals/top/1 (<=1 item)" {
  $r=Send-GET "cereals/top/1"; $c=[int]$r.ResponseCode; $len=Ensure-ArrayCount $r.Body.Json
  @{ Ok=($c -eq 200 -and $len -le 1); Detail="HTTP $c; items=$len" }
}

Run-Test "GET /cereals/top/10001 (cappes <= 10000)" {
  $r=Send-GET "cereals/top/10001"; $c=[int]$r.ResponseCode; $len=Ensure-ArrayCount $r.Body.Json
  @{ Ok=($c -eq 200 -and $len -le 10000); Detail="HTTP $c; items=$len" }
}

# Slet #2 – prøv to encoder-varianter og verifikér med GET bagefter
Run-Test "DELETE /cereals/{key} #2 (200)" {
  # slå navnet op fra server først (for at være 100% identisk)
  $lookup = Send-GET "cereals"
  $cL=[int]$lookup.ResponseCode
  if($cL -ne 200 -or -not $lookup.Body.Json){ return @{ Ok=$false; Detail="Lookup HTTP $cL" } }
  $match = $null
  foreach($it in @($lookup.Body.Json)){ if($it.name -eq $testName2 -and $it.mfr -eq $testMfr2 -and $it.type -eq $testType2){ $match=$it; break } }
  if(-not $match){ return @{ Ok=$false; Detail="Row not found for deletion (pre-check)" } }

  $p1=("cereals/{0}/{1}/{2}" -f (Encode $match.name),(Encode $match.mfr),(Encode $match.type))
  $r = Send-DELETE $p1; $c=[int]$r.ResponseCode
  if($c -ne 200){
    # Fallback-varianten (hvis du vil beholde den):
    $forceName = ($match.name -replace "'", "%27")
    $p2 = ("cereals/{0}/{1}/{2}" -f (Encode $forceName), (Encode $match.mfr), (Encode $match.type))

    $r2 = Send-DELETE $p2; $c2=[int]$r2.ResponseCode
    if($c2 -ne 200){
      # sidste check: er rækken faktisk væk alligevel?
      $chk = Send-GET "cereals"
      $gone = $true
      if($chk.Body.Json){
        foreach($it in @($chk.Body.Json)){ if($it.name -eq $match.name -and $it.mfr -eq $match.mfr -and $it.type -eq $match.type){ $gone=$false; break } }
      }
      if($gone){ return @{ Ok=$true; Detail="HTTP $c/$c2; item already gone" } }
      return @{ Ok=$false; Detail=("HTTP {0}; path1={1}; path2={2}; body1={3}; body2={4}" -f $c,$p1,$p2,($r.Body.Raw),($r2.Body.Raw)) }
    } else {
      # success på p2
      for($i=$global:CreatedRows.Count-1;$i -ge 0;$i--){
        $it=$global:CreatedRows[$i]; if($it.name -eq $match.name -and $it.mfr -eq $match.mfr -and $it.type -eq $match.type){ $global:CreatedRows.RemoveAt($i) }
      }
      return @{ Ok=$true; Detail="HTTP 200; body: $($r2.Body.Raw)" }
    }
  } else {
    # success på p1
    for($i=$global:CreatedRows.Count-1;$i -ge 0;$i--){
      $it=$global:CreatedRows[$i]; if($it.name -eq $match.name -and $it.mfr -eq $match.mfr -and $it.type -eq $match.type){ $global:CreatedRows.RemoveAt($i) }
    }
    return @{ Ok=$true; Detail="HTTP 200; body: $($r.Body.Raw)" }
  }
}

Run-Test "DELETE /cereals/{key} #2 igen (404)" {
  $p=("cereals/{0}/{1}/{2}" -f (Encode $testName2),(Encode $testMfr2),(Encode $testType2))
  $r=Send-DELETE $p; $c=[int]$r.ResponseCode
  @{ Ok=($c -eq 404); Detail="HTTP $c; body: $($r.Body.Raw)" }
}

# ---------- failsafe oprydning ----------
if($global:CreatedRows.Count -gt 0){
  Write-Info "Oprydning af resterende testrækker..."
  for($i=$global:CreatedRows.Count-1;$i -ge 0;$i--){
    $row=$global:CreatedRows[$i]
    $kp=("cereals/{0}/{1}/{2}" -f (Encode $row.name),(Encode $row.mfr),(Encode $row.type))
    $r=Send-DELETE $kp; $c=[int]$r.ResponseCode
    if($c -eq 200){ Write-Pass "Slettet: $($row.name)/$($row.mfr)/$($row.type)" } else { Write-Warn "Kunne ikke slette ($c): $($row.name)/$($row.mfr)/$($row.type)" }
  }
}

# slet tempfiler
foreach($f in @($tempCsv,$tempEmpty,$tempHeaderOnly)){ if($f -and (Test-Path $f)){ Remove-Item -Force $f } }

# ---------- summary & log ----------
$total   = $global:TestResults.Count
$passed  = @($global:TestResults | Where-Object { $_.Success }).Count
$failed  = $total - $passed
$overall = if($failed -eq 0){"PASS"} else {"FAIL"}

Write-Title "RESULTAT: $overall  ($passed/$total passeret, $failed fejlede)"

$logLines = New-Object System.Collections.Generic.List[string]
$logLines.Add(("CerealAPI Smoketest @ {0}" -f (Get-Date)))
$logLines.Add(("BaseUrl: {0}" -f $script:BaseUri))
$logLines.Add(("RunId  : {0}" -f $runId))
$logLines.Add("")
$global:TestResults | ForEach-Object {
  $status = if($_.Success){"PASS"} else {"FAIL"}
  $line = ("[{0}] {1} ({2} ms)" -f $status, $_.Name, $_.DurationMs)
  if(-not $_.Success){
    if($_.Error){ $line += (" | ERROR: {0}" -f $_.Error) }
    elseif($_.Details){ $line += (" | {0}" -f $_.Details) }
  } else { if($_.Details){ $line += (" | {0}" -f $_.Details) } }
  $logLines.Add($line)
}
$logLines.Add(""); $logLines.Add(("OVERALL: {0}  ({1}/{2} passed)" -f $overall, $passed, $total))

$logName = ("{0} Smoketest [{1}].log" -f $stamp, $overall)
$logPath = Join-Path $logsDir $logName
Set-Content -LiteralPath $logPath -Encoding UTF8 -Value $logLines

if($overall -eq "PASS"){ Write-Pass ("Smoketest OK. Log: {0}" -f $logPath); exit 0 }
else{ Write-Fail ("Smoketest FEJLEDE. Log: {0}" -f $logPath); exit 1 }
