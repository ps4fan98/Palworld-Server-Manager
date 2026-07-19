$script:RepositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$script:WebProject = Join-Path $script:RepositoryRoot "src\PalworldServerManager.Web\PalworldServerManager.Web.csproj"

function Get-FreeLoopbackPort {
    $listener = [System.Net.Sockets.TcpListener]::new(
        [System.Net.IPAddress]::Loopback,
        0)

    try {
        $listener.Start()
        return $listener.LocalEndpoint.Port
    }
    finally {
        $listener.Stop()
    }
}

function Wait-ForHealthyManager {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseAddress,

        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
    $healthUri = "$BaseAddress/api/v1/health"

    do {
        if ($Process.HasExited) {
            throw "Web process exited before health check passed. Exit code: $($Process.ExitCode)."
        }

        try {
            $response = Invoke-WebRequest -Uri $healthUri -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) {
                return
            }
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Timed out waiting for $healthUri to return HTTP 200."
}

function Assert-StaticAssetResponses {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseAddress
    )

    $expectations = @(
        @{ Path = "/app.css"; ContentType = "text/css" },
        @{ Path = "/PalworldServerManager.Web.styles.css"; ContentType = "text/css" },
        @{ Path = "/_framework/blazor.web.js"; ContentType = "javascript" },
        @{ Path = "/api/v1/health"; ContentType = "application/json" }
    )

    foreach ($expectation in $expectations) {
        $response = Invoke-WebRequest `
            -Uri "$BaseAddress$($expectation.Path)" `
            -UseBasicParsing `
            -TimeoutSec 10

        $response.StatusCode | Should -Be 200
        $response.Headers["Content-Type"] | Should -Match $expectation.ContentType
    }
}

function Stop-TestProcess {
    param(
        [System.Diagnostics.Process]$Process
    )

    if ($Process -and -not $Process.HasExited) {
        $Process.Kill($true)
        $Process.WaitForExit(10000) | Out-Null
    }

    if ($Process) {
        $Process.Dispose()
    }
}

Describe "Static web assets" {
    It "serves development static assets from dotnet run on loopback" {
        $port = Get-FreeLoopbackPort
        $baseAddress = "http://127.0.0.1:$port"
        $process = $null

        try {
            $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
            $startInfo.FileName = "dotnet"
            $startInfo.WorkingDirectory = $script:RepositoryRoot
            $startInfo.ArgumentList.Add("run")
            $startInfo.ArgumentList.Add("--project")
            $startInfo.ArgumentList.Add($script:WebProject)
            $startInfo.ArgumentList.Add("-c")
            $startInfo.ArgumentList.Add("Debug")
            $startInfo.ArgumentList.Add("--no-build")
            $startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"
            $startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development"
            $startInfo.Environment["Manager__ListenUrl"] = $baseAddress
            $startInfo.Environment["ASPNETCORE_URLS"] = $baseAddress
            $startInfo.RedirectStandardOutput = $true
            $startInfo.RedirectStandardError = $true
            $startInfo.UseShellExecute = $false

            $process = [System.Diagnostics.Process]::Start($startInfo)
            $process | Should -Not -BeNullOrEmpty

            Wait-ForHealthyManager -BaseAddress $baseAddress -Process $process
            Assert-StaticAssetResponses -BaseAddress $baseAddress
        }
        finally {
            Stop-TestProcess -Process $process
        }
    }

    It "serves production static assets from published output on loopback" {
        $port = Get-FreeLoopbackPort
        $baseAddress = "http://127.0.0.1:$port"
        $publishDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString("N"))
        $process = $null

        try {
            dotnet publish $script:WebProject -c Release --no-restore --no-build -o $publishDirectory
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet publish failed with exit code $LASTEXITCODE."
            }

            $executablePath = if ($IsWindows) {
                Join-Path $publishDirectory "PalworldServerManager.Web.exe"
            }
            else {
                Join-Path $publishDirectory "PalworldServerManager.Web"
            }

            $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
            $startInfo.FileName = $executablePath
            $startInfo.WorkingDirectory = $publishDirectory
            $startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production"
            $startInfo.Environment["DOTNET_ENVIRONMENT"] = "Production"
            $startInfo.Environment["Manager__ListenUrl"] = $baseAddress
            $startInfo.Environment["ASPNETCORE_URLS"] = $baseAddress
            $startInfo.RedirectStandardOutput = $true
            $startInfo.RedirectStandardError = $true
            $startInfo.UseShellExecute = $false

            $process = [System.Diagnostics.Process]::Start($startInfo)
            $process | Should -Not -BeNullOrEmpty

            Wait-ForHealthyManager -BaseAddress $baseAddress -Process $process
            Assert-StaticAssetResponses -BaseAddress $baseAddress
        }
        finally {
            Stop-TestProcess -Process $process

            if (Test-Path $publishDirectory) {
                Remove-Item $publishDirectory -Recurse -Force
            }
        }
    }
}
