#!/usr/bin/env python3
"""Menu crawl report: OUT/item_NN/tree_N.json (crawl_all.py) -> OUT/tree.md (full tree with screenshots) and
emu/MENUS.md (summary).

  python3 emu/crawl_report.py [emu/out/crawl]
"""
import glob, json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
MAIN = ["Talk Group", "SMS", "Call Log", "Zone", "Scan", "Roaming", "Settings", "Record", "GPS", "Satellite",
        "Digi Moni", "BT", "APRS"]


def title(n):
    t = n.get("text") or ["?"]
    return t[0]


def body(n, k=8):
    return " | ".join((n.get("text") or [])[1:k])


def labels(n):
    """Row label of each child: first row of the list for item 0, then the label read after each Down."""
    t = n.get("text") or []
    out, prev = [], (t[1] if len(t) > 1 else "?")
    for c in n.get("items", []):
        out.append(prev)
        prev = c.get("label_next", "?")
    return out


def name(lab, c):
    t = title(c)
    return lab if lab == t or popup(c) else "%s → %s" % (lab, t)


def popup(n):
    """Short message / confirmation popup (1-2 texts, no soft keys): it closes by itself, so the crawler's
    "children" of it are screens of the list behind it - not shown."""
    t = n.get("text") or []
    return 0 < len(t) <= 2 and not any(x in ("Select", "Back", "Confirm", "Exit") for x in t)


def walk_md(n, depth, rel, out, stats):
    if popup(n):
        return
    for lab, c in zip(labels(n), n.get("items", [])):
        stats["screens"] += 1
        shot = c.get("shot")
        link = " ([png](%s))" % os.path.join(rel, shot) if shot else ""
        if c.get("leaf"):
            out.append("%s- *(no screen change)*" % ("  " * depth))
            continue
        extra = ""
        if c.get("editor"):
            extra = " — text editor"
        if c.get("home"):
            extra = " — acts, back to main screen"
        out.append("%s- **%s**%s: %s%s" % ("  " * depth, name(lab, c), extra, body(c), link))
        if c.get("options"):
            stats["option_lists"] += 1
            out.append("%s  - options (%d): %s" % ("  " * depth, len(c["options"]), ", ".join(c["options"])))
        walk_md(c, depth + 1, rel, out, stats)


def summary(n, depth, out, maxdepth=2):
    """Short tree: item titles and option values, two levels below the main item."""
    if popup(n):
        return
    for lab, c in zip(labels(n), n.get("items", [])):
        if c.get("leaf"):
            continue
        line = "%s- %s" % ("  " * depth, name(lab, c))
        kids = [k for k in c.get("items", []) if not k.get("leaf")]
        if not c.get("options") and len(kids) > 1 and all(popup(k) for k in kids):
            c = dict(c, options=[l for l, k in zip(labels(c), c["items"]) if not k.get("leaf")], items=[])
        if c.get("options"):
            o = c["options"]
            line += ": " + ", ".join(o[:12]) + (" … (%d)" % len(o) if len(o) > 12 else "")
        elif c.get("editor"):
            line += " (text entry)"
        elif popup(c):
            line += ": popup \"%s\"" % " ".join(c.get("text", []))
        elif not c.get("items"):
            b = body(c, 4)
            if b:
                line += " — " + b
        out.append(line)
        if depth < maxdepth and not c.get("options"):
            summary(c, depth + 1, out, maxdepth)


def normalise(n):
    """A popup followed by its own screen ("then"): show the follow-on screen in the popup's place."""
    for c in n.get("items", []):
        if c.get("then"):
            t = c["then"]
            c["popup_text"] = c.get("text")
            c["text"], c["items"], c["shot"] = t.get("text"), t.get("items", []), t.get("shot", c.get("shot"))
            c.pop("popup", None)
        normalise(c)


def main():
    out_dir = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "out", "crawl"))
    md, sm = ["# D168UV stock V1.07 menu tree (emulator crawl)", ""], []
    stats = {"screens": 0, "option_lists": 0}
    for f in sorted(glob.glob(os.path.join(out_dir, "item_*", "tree_*.json"))):
        root = json.load(open(f))
        normalise(root)
        rel = os.path.relpath(os.path.dirname(f), out_dir)
        for it in root.get("items", []):
            name = MAIN[it["index"]] if it["index"] < len(MAIN) else title(it)
            shot = it.get("shot")
            md += ["## %d. %s" % (it["index"], name), "",
                   "Opens: %s%s" % (" | ".join(it.get("text", [])[:8]),
                                     " ([png](%s))" % os.path.join(rel, shot) if shot else ""), ""]
            if it.get("options"):
                md.append("- options: " + ", ".join(it["options"]))
            walk_md(it, 0, rel, md, stats)
            md.append("")
            sm += ["### %d. %s" % (it["index"], name), ""]
            if it.get("options"):
                sm.append("- options: " + ", ".join(it["options"]))
            if not it.get("items"):
                sm.append("- " + (" | ".join(it.get("text", [])[:6]) or "(no screen)"))
            summary(it, 0, sm)
            sm.append("")
    open(os.path.join(out_dir, "tree.md"), "w").write("\n".join(md) + "\n")
    print("%s: %d screens, %d option lists" % (os.path.join(out_dir, "tree.md"), stats["screens"], stats["option_lists"]))
    open(os.path.join(out_dir, "summary.md"), "w").write("\n".join(sm) + "\n")


if __name__ == "__main__":
    main()
