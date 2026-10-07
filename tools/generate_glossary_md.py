#!/usr/bin/env python3
"""Turn data/glossary/*.json into GLOSSARY.md, a readable copy of the dictionary for GitHub/phones.

Spoilers are wrapped in collapsible <details> blocks, and entries whose very name is a spoiler
("hideUntilSeen") are listed in a separate collapsed section at the end.

    python3 tools/generate_glossary_md.py
"""
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent.parent
CATEGORIES = [
    "Skills", "Game Mechanics", "People", "Places", "World", "Factions",
    "Politics & Ideas", "History", "Slang & Jargon", "Vocabulary",
]


def strip_comments(text: str) -> str:
    """Remove // comments that are outside of JSON strings (the glossary files allow them)."""
    out, in_string, escaped, i = [], False, False, 0
    while i < len(text):
        c = text[i]
        if in_string:
            out.append(c)
            if escaped:
                escaped = False
            elif c == "\\":
                escaped = True
            elif c == '"':
                in_string = False
        elif c == '"':
            in_string = True
            out.append(c)
        elif text.startswith("//", i):
            while i < len(text) and text[i] != "\n":
                i += 1
            continue
        else:
            out.append(c)
        i += 1
    return re.sub(r",(\s*[\]}])", r"\1", "".join(out))  # trailing commas


def slug(term: str) -> str:
    """GitHub's heading anchor: lower-case, punctuation dropped, spaces to hyphens."""
    return re.sub(r"[^\w\- ]", "", term.lower()).replace(" ", "-")


def load():
    entries = []
    for path in sorted((ROOT / "data" / "glossary").glob("*.json")):
        data = json.loads(strip_comments(path.read_text(encoding="utf-8")))
        for e in data.get("entries", []):
            e.setdefault("category", data.get("category", ""))
            e.setdefault("id", slug(e["term"]))
            entries.append(e)
    return entries


def render_entry(e, by_id):
    lines = [f"### {e['term']}", ""]
    aliases = e.get("aliases") or []
    if aliases:
        lines += [f"*Also: {', '.join(aliases)}*", ""]
    lines += [f"**{e['short']}**", ""]
    if e.get("details"):
        lines += [p.strip() + "\n" for p in e["details"].split("\n") if p.strip()]
    if e.get("inspiredBy"):
        lines += [f"*Real-world inspiration:* {e['inspiredBy']}", ""]
    see = [by_id[s] for s in e.get("seeAlso") or [] if s in by_id and not by_id[s].get("hideUntilSeen")]
    if see:
        lines += ["See also: " + ", ".join(f"[{s['term']}](#{slug(s['term'])})" for s in see), ""]
    if e.get("spoiler"):
        lines += ["<details><summary>Spoiler</summary>", "", e["spoiler"], "", "</details>", ""]
    return lines


def main():
    entries = load()
    by_id = {e["id"]: e for e in entries}
    visible = [e for e in entries if not e.get("hideUntilSeen")]
    hidden = [e for e in entries if e.get("hideUntilSeen")]

    out = [
        "# Disco Dictionary: the glossary",
        "",
        "Everything the in-game dictionary knows, readable without the game. Generated from "
        "`data/glossary/*.json` by `tools/generate_glossary_md.py`; edit those files, not this one.",
        "",
        "Explanations are spoiler-free. Plot details are folded away under **Spoiler**, and a few "
        "names that would give the story away are collected in the last section.",
        "",
        "## Contents",
        "",
    ]
    for cat in CATEGORIES:
        count = sum(1 for e in visible if e["category"] == cat)
        out.append(f"- [{cat}](#{slug(cat)}) ({count})")
    out.append("- [Names that are spoilers](#names-that-are-spoilers)")
    out.append("")

    for cat in CATEGORIES:
        out += [f"## {cat}", ""]
        for e in sorted((e for e in visible if e["category"] == cat), key=lambda e: e["term"].lower()):
            out += render_entry(e, by_id)

    out += ["## Names that are spoilers", "",
            "<details><summary>Only open this if you've already met these characters in the game.</summary>", ""]
    for e in sorted(hidden, key=lambda e: e["term"].lower()):
        out += render_entry(e, by_id)
    out += ["</details>", ""]

    (ROOT / "GLOSSARY.md").write_text("\n".join(out), encoding="utf-8")
    print(f"Wrote GLOSSARY.md with {len(entries)} entries.")


if __name__ == "__main__":
    main()
