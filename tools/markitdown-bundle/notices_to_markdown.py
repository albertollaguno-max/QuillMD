"""Transform pip-licenses JSON output into a readable Markdown notices file.

Usage:
    python notices_to_markdown.py <input.json> <output.md>

The pip-licenses JSON contains local filesystem paths (LicenseFile field)
which are build-machine artifacts and must not be redistributed; this
transformer drops them.
"""
import json
import sys


def is_present(value: str) -> bool:
    stripped = value.strip() if value else ""
    return stripped != "" and stripped != "UNKNOWN"


def render(packages: list[dict]) -> str:
    lines: list[str] = [
        "# Third-Party Notices",
        "",
        "QuillMD bundles `markitdown.exe` (Microsoft, MIT) and its Python "
        "dependencies for the document import feature. The following are "
        "their licenses and copyright notices.",
        "",
        f"Total packages: {len(packages)}",
        "",
        "---",
        "",
    ]

    for pkg in sorted(packages, key=lambda p: p["Name"].lower()):
        name = pkg.get("Name", "?")
        version = pkg.get("Version", "?")
        lines.append(f"## {name} {version}")
        lines.append("")

        license_name = pkg.get("License", "")
        if is_present(license_name):
            lines.append(f"**License:** {license_name}")

        url = pkg.get("URL", "")
        if is_present(url):
            lines.append(f"**Project URL:** <{url}>")

        lines.append("")

        license_text = pkg.get("LicenseText", "")
        if is_present(license_text):
            lines.append("```")
            lines.append(license_text.strip())
            lines.append("```")
            lines.append("")

        notice_text = pkg.get("NoticeText", "")
        if is_present(notice_text):
            lines.append("### NOTICE")
            lines.append("")
            lines.append("```")
            lines.append(notice_text.strip())
            lines.append("```")
            lines.append("")

        lines.append("---")
        lines.append("")

    return "\n".join(lines)


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: notices_to_markdown.py <input.json> <output.md>", file=sys.stderr)
        return 2

    with open(sys.argv[1], encoding="utf-8") as f:
        packages = json.load(f)

    markdown = render(packages)

    with open(sys.argv[2], "w", encoding="utf-8", newline="\n") as f:
        f.write(markdown)

    print(f"Wrote {len(packages)} packages to {sys.argv[2]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
