BeforeAll {
    $script:RunDevelopmentScript = Join-Path `
        $PSScriptRoot `
        "..\scripts\Run-Development.ps1"

    function Invoke-RunDevelopmentScript {
        param(
            [Parameter(Mandatory = $true)]
            [scriptblock]$DotnetCommand
        )

        & $script:RunDevelopmentScript `
            -DotnetVersionCommand { "10.0.100"; 0 } `
            -DotnetCommand $DotnetCommand
    }

    function New-TestDotnetAdapter {
        param(
            [Parameter(Mandatory = $true)]
            [System.Collections.Generic.List[string]]$ObservedCommands,

            [string]$FailCommand,

            [switch]$WriteRepresentativeOutput
        )

        $adapter = {
            param([string[]]$Arguments)

            $ObservedCommands.Add(($Arguments -join " "))

            if ($WriteRepresentativeOutput) {
                "representative output from $($Arguments[0])" | Out-Host
            }

            if ($Arguments[0] -eq $FailCommand) {
                return 1
            }

            return 0
        }

        return $adapter.GetNewClosure()
    }
}

Describe "Run-Development.ps1" {
    It "prints command output and advances to the next stage when commands succeed" {
        $observedCommands = [System.Collections.Generic.List[string]]::new()
        $adapter = New-TestDotnetAdapter `
            -ObservedCommands $observedCommands `
            -WriteRepresentativeOutput

        Invoke-RunDevelopmentScript -DotnetCommand $adapter

        $observedCommands | Should -HaveCount 4
        $observedCommands[0] | Should -Be "restore .\PalworldServerManager.slnx"
        $observedCommands[1] | Should -Match "^list .* --vulnerable --include-transitive --no-restore$"
        $observedCommands[2] | Should -Be "build .\PalworldServerManager.slnx -c Debug --no-restore"
        $observedCommands[3] | Should -Match "^run --project .* --no-build$"
    }

    It "stops before audit, build, and run when restore fails" {
        $observedCommands = [System.Collections.Generic.List[string]]::new()
        $adapter = New-TestDotnetAdapter `
            -ObservedCommands $observedCommands `
            -FailCommand "restore"

        { Invoke-RunDevelopmentScript -DotnetCommand $adapter } |
            Should -Throw "*restore*failed with exit code 1*"

        $observedCommands | Should -HaveCount 1
        $observedCommands[0] | Should -Be "restore .\PalworldServerManager.slnx"
    }

    It "stops before build and run when vulnerability audit fails" {
        $observedCommands = [System.Collections.Generic.List[string]]::new()
        $adapter = New-TestDotnetAdapter `
            -ObservedCommands $observedCommands `
            -FailCommand "list"

        { Invoke-RunDevelopmentScript -DotnetCommand $adapter } |
            Should -Throw "*list*failed with exit code 1*"

        $observedCommands | Should -HaveCount 2
        $observedCommands[1] | Should -Match "^list .* --vulnerable --include-transitive --no-restore$"
    }

    It "stops before run when build fails" {
        $observedCommands = [System.Collections.Generic.List[string]]::new()
        $adapter = New-TestDotnetAdapter `
            -ObservedCommands $observedCommands `
            -FailCommand "build"

        { Invoke-RunDevelopmentScript -DotnetCommand $adapter } |
            Should -Throw "*build*failed with exit code 1*"

        $observedCommands | Should -HaveCount 3
        $observedCommands[2] | Should -Be "build .\PalworldServerManager.slnx -c Debug --no-restore"
    }

    It "does not interpret host output emitted by the adapter as the exit code" {
        $observedCommands = [System.Collections.Generic.List[string]]::new()
        $adapter = New-TestDotnetAdapter `
            -ObservedCommands $observedCommands `
            -WriteRepresentativeOutput

        Invoke-RunDevelopmentScript -DotnetCommand $adapter

        $observedCommands | Should -HaveCount 4
    }

    It "fails clearly when an adapter returns output on the success stream" {
        {
            Invoke-RunDevelopmentScript -DotnetCommand {
                param([string[]]$Arguments)

                Write-Output "unexpected success-stream output"
                return 0
            }
        } | Should -Throw "*must return exactly one integer exit code*"
    }

    It "sets and restores Development environment variables while running" {
        $previousAspNetEnvironment = $env:ASPNETCORE_ENVIRONMENT
        $previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
        $env:ASPNETCORE_ENVIRONMENT = "Staging"
        Remove-Item Env:DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue

        try {
            $observedCommands = [System.Collections.Generic.List[string]]::new()
            $adapter = {
                param([string[]]$Arguments)

                $observedCommands.Add(($Arguments -join " "))

                if ($Arguments[0] -eq "run") {
                    $env:ASPNETCORE_ENVIRONMENT | Should -Be "Development"
                    $env:DOTNET_ENVIRONMENT | Should -Be "Development"
                }

                return 0
            }.GetNewClosure()

            Invoke-RunDevelopmentScript -DotnetCommand $adapter

            $observedCommands | Should -HaveCount 4
            $env:ASPNETCORE_ENVIRONMENT | Should -Be "Staging"
            Test-Path Env:DOTNET_ENVIRONMENT | Should -BeFalse
        }
        finally {
            if ($null -eq $previousAspNetEnvironment) {
                Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
            }
            else {
                $env:ASPNETCORE_ENVIRONMENT = $previousAspNetEnvironment
            }

            if ($null -eq $previousDotnetEnvironment) {
                Remove-Item Env:DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue
            }
            else {
                $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
            }
        }
    }

    It "keeps the localhost-only development URL" {
        $content = Get-Content -Raw -Path $script:RunDevelopmentScript

        $content | Should -Match "http://127\.0\.0\.1:8213"
    }
}
