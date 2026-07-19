from pathlib import Path
import re

SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "Run-Development.ps1"


def _script_text() -> str:
    return SCRIPT.read_text(encoding="utf-8")


def test_dotnet_pipeline_uses_checked_invocations_in_order():
    script = _script_text()
    calls = re.findall(
        r'Invoke-CheckedDotnetCommand\s+`\s+-Description\s+"([^"]+)"\s+`\s+-Arguments\s+@\((.*?)\)',
        script,
        flags=re.DOTALL,
    )

    descriptions = [description for description, _ in calls]

    assert descriptions == [
        "Restoring packages...",
        "Auditing packages for known vulnerabilities...",
        "Building...",
        "Running...",
    ]


def test_checked_invocation_throws_before_later_stages_can_run():
    script = _script_text()

    assert "function Invoke-CheckedDotnetCommand" in script
    assert "if ($exitCode -ne 0)" in script
    assert "throw \"dotnet $($Arguments -join ' ') failed with exit code $exitCode.\"" in script


def test_vulnerability_audit_runs_before_build_and_run():
    script = _script_text()

    audit_index = script.index('"Auditing packages for known vulnerabilities..."')
    build_index = script.index('"Building..."')
    run_index = script.index('"Running..."')

    assert audit_index < build_index < run_index
    assert '"--vulnerable"' in script
    assert '"--include-transitive"' in script
    assert '"--no-restore"' in script


def test_localhost_development_url_is_preserved():
    script = _script_text()

    assert "http://127.0.0.1:8213" in script
