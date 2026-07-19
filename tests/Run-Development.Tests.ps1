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
}

Describe "Run-Development.ps1" {
    It "prints command output and advances to the next stage when commands succeed" {
        $script:ObservedCommands = [System.Collections.Generic.List[string]]::new()

        Invoke-RunDevelopmentScript -DotnetCommand {
            param([string[]]$Arguments)

            $script:ObservedCommands.Add(($Arguments -join " "))
            "representative output from $($Arguments[0])" | Out-Host
            return 0
        }

        $script:ObservedCommands | Should -HaveCount 4
        $script:ObservedCommands[0] | Should -Be "restore .\PalworldServerManager.slnx"
        $script:ObservedCommands[1] | Should -Match "^list .* --vulnerable --include-transitive --no-restore$"
        $script:ObservedCommands[2] | Should -Be "build .\PalworldServerManager.slnx -c Debug --no-restore"
        $script:ObservedCommands[3] | Should -Match "^run --project .* --no-build$"
    }

    It "stops before audit, build, and run when restore fails" {
        $script:ObservedCommands = [System.Collections.Generic.List[string]]::new()

        {
            Invoke-RunDevelopmentScript -DotnetCommand {
                param([string[]]$Arguments)

                $script:ObservedCommands.Add(($Arguments -join " "))
                if ($Arguments[0] -eq "restore") { return 1 }
                return 0
            }
        } | Should -Throw "*restore*failed with exit code 1*"

        $script:ObservedCommands | Should -HaveCount 1
        $script:ObservedCommands[0] | Should -Be "restore .\PalworldServerManager.slnx"
    }

    It "stops before build and run when vulnerability audit fails" {
        $script:ObservedCommands = [System.Collections.Generic.List[string]]::new()

        {
            Invoke-RunDevelopmentScript -DotnetCommand {
                param([string[]]$Arguments)

                $script:ObservedCommands.Add(($Arguments -join " "))
                if ($Arguments[0] -eq "list") { return 1 }
                return 0
            }
        } | Should -Throw "*list*failed with exit code 1*"

        $script:ObservedCommands | Should -HaveCount 2
        $script:ObservedCommands[1] | Should -Match "^list .* --vulnerable --include-transitive --no-restore$"
    }

    It "stops before run when build fails" {
        $script:ObservedCommands = [System.Collections.Generic.List[string]]::new()

        {
            Invoke-RunDevelopmentScript -DotnetCommand {
                param([string[]]$Arguments)

                $script:ObservedCommands.Add(($Arguments -join " "))
                if ($Arguments[0] -eq "build") { return 1 }
                return 0
            }
        } | Should -Throw "*build*failed with exit code 1*"

        $script:ObservedCommands | Should -HaveCount 3
        $script:ObservedCommands[2] | Should -Be "build .\PalworldServerManager.slnx -c Debug --no-restore"
    }

    It "does not interpret host output emitted by the adapter as the exit code" {
        $script:ObservedCommands = [System.Collections.Generic.List[string]]::new()

        Invoke-RunDevelopmentScript -DotnetCommand {
            param([string[]]$Arguments)

            $script:ObservedCommands.Add(($Arguments -join " "))
            "adapter output is visible but not returned" | Out-Host
            return 0
        }

        $script:ObservedCommands | Should -HaveCount 4
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
            Invoke-RunDevelopmentScript -DotnetCommand {
                param([string[]]$Arguments)

                if ($Arguments[0] -eq "run") {
                    $env:ASPNETCORE_ENVIRONMENT | Should -Be "Development"
                    $env:DOTNET_ENVIRONMENT | Should -Be "Development"
                }

                return 0
            }

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
