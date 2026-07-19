BeforeAll {
    $script:RepositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
    $script:WebProject = Join-Path $script:RepositoryRoot "src\PalworldServerManager.Web\PalworldServerManager.Web.csproj"
    $script:DebugAssembly = Join-Path `
        $script:RepositoryRoot `
        "src\PalworldServerManager.Web\bin\Debug\net10.0\PalworldServerManager.Web.dll"

    function ConvertTo-CommandLineArgument {
        param(
            [Parameter(Mandatory = $true)]
            [string]$Value
        )

        return '"' + $Value.Replace('"', '\"') + '"'
    }

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
            [System.Diagnostics.Process]$Process,

            [Parameter(Mandatory = $true)]
            [System.Text.StringBuilder]$StandardOutput,

            [Parameter(Mandatory = $true)]
            [System.Text.StringBuilder]$StandardError
        )

        $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
        $healthUri = "$BaseAddress/api/v1/health"

        do {
            if ($Process.HasExited) {
                throw "Web process exited before health check passed. Exit code: $($Process.ExitCode). Stdout: $StandardOutput Stderr: $StandardError"
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

        throw "Timed out waiting for $healthUri to return HTTP 200. Stdout: $StandardOutput Stderr: $StandardError"
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

    function Start-TestProcess {
        param(
            [Parameter(Mandatory = $true)]
            [System.Diagnostics.ProcessStartInfo]$StartInfo,

            [Parameter(Mandatory = $true)]
            [System.Text.StringBuilder]$StandardOutput,

            [Parameter(Mandatory = $true)]
            [System.Text.StringBuilder]$StandardError
        )

        $process = [System.Diagnostics.Process]::new()
        $process.StartInfo = $StartInfo
        $process.EnableRaisingEvents = $true

        $outputHandler = {
            param($Sender, $EventArgs)

            if ($null -ne $EventArgs.Data) {
                [void]$StandardOutput.AppendLine($EventArgs.Data)
            }
        }.GetNewClosure()

        $errorHandler = {
            param($Sender, $EventArgs)

            if ($null -ne $EventArgs.Data) {
                [void]$StandardError.AppendLine($EventArgs.Data)
            }
        }.GetNewClosure()

        $process.add_OutputDataReceived($outputHandler)
        $process.add_ErrorDataReceived($errorHandler)

        if (-not $process.Start()) {
            $process.Dispose()
            throw "Failed to start test process."
        }

        $process.BeginOutputReadLine()
        $process.BeginErrorReadLine()

        return $process
    }

    function Stop-TestProcess {
        param(
            [System.Diagnostics.Process]$Process
        )

        if (-not $Process) {
            return
        }

        try {
            if (-not $Process.HasExited) {
                $Process.Kill()
                $Process.WaitForExit(10000) | Out-Null
            }
        }
        catch [InvalidOperationException] {
            # The process exited between inspection and termination.
        }
        finally {
            $Process.Dispose()
        }
    }
}

Describe "Static web assets" {
    It "serves development static assets from the built Debug assembly on loopback" {
        $port = Get-FreeLoopbackPort
        $baseAddress = "http://127.0.0.1:$port"
        $process = $null
        $standardOutput = [System.Text.StringBuilder]::new()
        $standardError = [System.Text.StringBuilder]::new()

        try {
            Test-Path $script:DebugAssembly | Should -BeTrue
            $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
            $startInfo.FileName = "dotnet"
            $startInfo.WorkingDirectory = Split-Path -Parent $script:WebProject
            $startInfo.Arguments = ConvertTo-CommandLineArgument -Value $script:DebugAssembly
            $startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development"
            $startInfo.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "Development"
            $startInfo.EnvironmentVariables["Manager__ListenUrl"] = $baseAddress
            $startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = $baseAddress
            $startInfo.RedirectStandardOutput = $true
            $startInfo.RedirectStandardError = $true
            $startInfo.UseShellExecute = $false

            $process = Start-TestProcess `
                -StartInfo $startInfo `
                -StandardOutput $standardOutput `
                -StandardError $standardError

            Wait-ForHealthyManager `
                -BaseAddress $baseAddress `
                -Process $process `
                -StandardOutput $standardOutput `
                -StandardError $standardError

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
        $standardOutput = [System.Text.StringBuilder]::new()
        $standardError = [System.Text.StringBuilder]::new()

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
            $startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production"
            $startInfo.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "Production"
            $startInfo.EnvironmentVariables["Manager__ListenUrl"] = $baseAddress
            $startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = $baseAddress
            $startInfo.RedirectStandardOutput = $true
            $startInfo.RedirectStandardError = $true
            $startInfo.UseShellExecute = $false

            $process = Start-TestProcess `
                -StartInfo $startInfo `
                -StandardOutput $standardOutput `
                -StandardError $standardError

            Wait-ForHealthyManager `
                -BaseAddress $baseAddress `
                -Process $process `
                -StandardOutput $standardOutput `
                -StandardError $standardError

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
