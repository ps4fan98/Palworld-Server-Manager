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

Describe "Static web assets" {
    It "serves CSS, Blazor framework assets, and health from loopback" {
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
            $startInfo.ArgumentList.Add("Release")
            $startInfo.ArgumentList.Add("--no-build")
            $startInfo.Environment["Manager__ListenUrl"] = $baseAddress
            $startInfo.Environment["ASPNETCORE_URLS"] = $baseAddress
            $startInfo.RedirectStandardOutput = $true
            $startInfo.RedirectStandardError = $true
            $startInfo.UseShellExecute = $false

            $process = [System.Diagnostics.Process]::Start($startInfo)
            $process | Should -Not -BeNullOrEmpty

            Wait-ForHealthyManager -BaseAddress $baseAddress -Process $process

            $expectations = @(
                @{ Path = "/app.css"; ContentType = "text/css" },
                @{ Path = "/PalworldServerManager.Web.styles.css"; ContentType = "text/css" },
                @{ Path = "/_framework/blazor.web.js"; ContentType = "javascript" },
                @{ Path = "/api/v1/health"; ContentType = "application/json" }
            )

            foreach ($expectation in $expectations) {
                $response = Invoke-WebRequest `
                    -Uri "$baseAddress$($expectation.Path)" `
                    -UseBasicParsing `
                    -TimeoutSec 10

                $response.StatusCode | Should -Be 200
                $response.Headers["Content-Type"] | Should -Match $expectation.ContentType
            }
        }
        finally {
            if ($process -and -not $process.HasExited) {
                $process.Kill($true)
                $process.WaitForExit(10000) | Out-Null
            }

            if ($process) {
                $process.Dispose()
            }
        }
    }
}
