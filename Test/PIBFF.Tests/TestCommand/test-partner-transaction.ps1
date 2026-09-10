# =====================================================================
# Test script for PIBFF
# Run this AFTER starting the API with:
#   cd src/PIBFF.Api
#   dotnet run --launch-profile http
#
# Usage:
#   .\test-partner-transaction.ps1                 -> runs Test 1 (single valid request)
#   .\test-partner-transaction.ps1 -Test All        -> runs every scenario below
# =====================================================================

param(
    [ValidateSet("Valid", "InvalidAmount", "MissingFields", "MockEndpointRatio", "RetryBehavior", "All")]
    [string]$Test = "Valid",
    [string]$BaseUrl = "http://localhost:5085"
)

$ErrorActionPreference = "Stop"
function Invoke-Transaction {
    param([hashtable]$Payload)

    $body = $Payload | ConvertTo-Json
    Write-Host "`n--- Request ---" -ForegroundColor Cyan
    Write-Host $body

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/partner/transactions" `
            -Method Post `
            -ContentType "application/json" `
            -Body $body

        Write-Host "--- Response (Success) ---" -ForegroundColor Green
        $response | ConvertTo-Json
    }
    catch {
        Write-Host "--- Response (Error) ---" -ForegroundColor Yellow
        Write-Host "Status code: $($_.Exception.Response.StatusCode.value__)"

        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $errorBody = $reader.ReadToEnd()
        Write-Host $errorBody
    }
}

function Test-Valid {
    Write-Host "`n===== Test: Valid transaction =====" -ForegroundColor Magenta
    Invoke-Transaction -Payload @{
        partnerId             = "P-1001"
        transactionReference  = "TXN-99823"
        amount                = 250.00
        currency              = "USD"
        timestamp             = (Get-Date).ToUniversalTime().ToString("o")
    }
}

function Test-InvalidAmount {
    Write-Host "`n===== Test: Invalid amount (should 400) =====" -ForegroundColor Magenta
    Invoke-Transaction -Payload @{
        partnerId             = "P-1001"
        transactionReference  = "TXN-99824"
        amount                = -5.00
        currency              = "USD"
        timestamp             = (Get-Date).ToUniversalTime().ToString("o")
    }
}

function Test-MissingFields {
    Write-Host "`n===== Test: Missing required fields (should 400) =====" -ForegroundColor Magenta
    Invoke-Transaction -Payload @{
        amount = 10.00
    }
}

function Test-MockEndpointRatio {
    Write-Host "`n===== Test: Mock verification endpoint failure ratio (expect ~30% failures) =====" -ForegroundColor Magenta
    $success = 0
    $timeout = 0
    for ($i = 1; $i -le 20; $i++) {
        try {
            Invoke-RestMethod -Uri "$BaseUrl/api/mock/partner-verification/P-1001" -Method Get | Out-Null
            $success++
        }
        catch {
            $timeout++
        }
    }
    Write-Host "200 OK: $success / 20"
    Write-Host "504 Timeout: $timeout / 20"
}

function Test-RetryBehavior {
    Write-Host "`n===== Test: Full pipeline retry behavior (expect mostly 202, rare 502) =====" -ForegroundColor Magenta
    $accepted = 0
    $unavailable = 0

    for ($i = 1; $i -le 15; $i++) {
        $body = @{
            partnerId             = "P-1001"
            transactionReference  = "TXN-LOOP-$i"
            amount                = 100.00
            currency              = "USD"
            timestamp             = (Get-Date).ToUniversalTime().ToString("o")
        } | ConvertTo-Json

        try {
            Invoke-RestMethod -Uri "$BaseUrl/api/v1/partner/transactions" `
                -Method Post -ContentType "application/json" -Body $body | Out-Null
            $accepted++
        }
        catch {
            $unavailable++
        }
    }

    Write-Host "202 Accepted: $accepted / 15"
    Write-Host "502 Unavailable: $unavailable / 15"
}

switch ($Test) {
    "Valid"             { Test-Valid }
    "InvalidAmount"     { Test-InvalidAmount }
    "MissingFields"     { Test-MissingFields }
    "MockEndpointRatio" { Test-MockEndpointRatio }
    "RetryBehavior"     { Test-RetryBehavior }
    "All" {
        Test-Valid
        Test-InvalidAmount
        Test-MissingFields
        Test-MockEndpointRatio
        Test-RetryBehavior
    }
}
