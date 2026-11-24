"""Walk the D168UV stock menus in a running emulator (run.py --serve) and record every screen.
Port of D890UV_src/emu/crawl.py.

Depth-first: at each list, ENTER (Menu key 13) each item, record the screen it opens, recurse, go back
(Exit key 14), move DOWN (16) until the list wraps. Screens are identified by a hash of the LCD frame
(live screens with a running clock: by their drawn text). Text comes from CPU hooks on the stock V1.07 text
renderers (GBK byte strings):
  FUN_0804f4d4(x, y, ch, font, mode)          one ASCII glyph       (r0 x, r1 y, r2 char)
  FUN_0804fc4c(x, y, byte *gbk, font, mode)   one GBK double glyph  (r2 -> 2 bytes)
  FUN_08051328(x0, y0, x1, y1, tx, ty, char *s, scroll)   list row text (own glyph loop; s at [SP+8])
(FUN_08050e30 is the plain string renderer; it ends up in the two glyph functions, so it is not hooked.)
Glyphs drawn left to right on one line are joined into strings.
If Exit does not return to the expected screen, the crawler goes home and replays the key path.

  python3 emu/crawl.py --port 1335 --depth 4 --out emu/out/crawl [--only 2]
Output: tree.json / tree_N.json (menu tree with each screen's text), shots/<path>.png, crawl.log, text.log
Keys: 13 Menu/OK, 14 Exit/Back, 15 Up, 16 Down.
"""
import argparse, hashlib, json, os, re, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ctl import talk

HERE = os.path.dirname(os.path.abspath(__file__))
CHAR_FN, GBK_FN, ROW_FN = 0x0804F4D4, 0x0804FC4C, 0x08051328
MENU, EXIT, UP, DOWN = 13, 14, 15, 16


def hooks(path):
    w = "System.IO.File.AppendAllText('%s', " % path
    xy = "str(self.GetRegister(0).RawValue) + ' ' + str(self.GetRegister(1).RawValue) + ' ' + "
    return [
        "cpu AddHook 0x%08X \"import System; %s'C ' + %s('%%02x' %% (self.GetRegister(2).RawValue & 0xFF)) + chr(10))\""
        % (CHAR_FN, w, xy),
        "cpu AddHook 0x%08X \"import System; p = self.GetRegister(2).RawValue; %s'C ' + %s('%%02x%%02x' %% "
        "(self.Bus.ReadByte(p), self.Bus.ReadByte(p + 1))) + chr(10))\"" % (GBK_FN, w, xy),
        "cpu AddHook 0x%08X \"import System; sp = self.GetRegister(13).RawValue; p = self.Bus.ReadDoubleWord(sp + 8); "
        "b = [self.Bus.ReadByte(p + i) for i in range(48)]; n = b.index(0) if 0 in b else 48; "
        "%s'S ' + str(self.Bus.ReadDoubleWord(sp)) + ' ' + str(self.Bus.ReadDoubleWord(sp + 4)) + ' ' + "
        "''.join(['%%02x' %% x for x in b[:n]]) + chr(10))\"" % (ROW_FN, w),
    ]


def parse_text(lines):
    """Hook log lines -> [(y, x, text)]. Glyphs on one line drawn left to right are joined."""
    out, cur = [], None
    def flush():
        if cur and cur[3].strip():
            out.append((cur[1], cur[0], cur[3].strip()))
    for l in lines:
        f = l.split()
        if len(f) < 3:
            continue
        kind, x, y = f[0], int(f[1]), int(f[2])
        s = bytes.fromhex(f[3]).decode("gbk", "replace") if len(f) > 3 else ""
        if kind == "S":
            flush(); cur = None
            if s.strip():
                out.append((y, x, s.strip()))
            continue
        if cur and y == cur[1] and cur[2] <= x <= cur[2] + 20:
            cur[2] = x; cur[3] += s
        else:
            flush(); cur = [x, y, x, s]
    flush()
    return out


class Radio:
    def __init__(self, port, out, settle):
        self.port, self.out, self.settle = port, out, settle
        self.txt = os.path.join(out, "text.log")
        self.ppm = os.path.join(out, "cur.ppm")
        self.steps = 0
        self.hl_y = None
        open(self.txt, "w").close()
        talk(port, ["cpu RemoveHooksAt 0x%08X" % a for a in (CHAR_FN, GBK_FN, ROW_FN)] + hooks(self.txt))

    def _texts(self, pos):
        with open(self.txt, "rb") as f:
            f.seek(pos)
            return parse_text(f.read().decode("ascii", "replace").splitlines())

    def key(self, code, settle=None):
        """Press a key; return (screen id, texts drawn meanwhile)."""
        pos = os.path.getsize(self.txt)
        self.steps += 1
        talk(self.port, ["lcd Press %d 120" % code, 'emulation RunFor "00:00:%09.6f"' % (settle or self.settle),
                         "lcd Screenshot @%s" % self.ppm])
        texts = self._texts(pos)
        self.hl_y = self.highlight_y()
        return self.sig(self.frame_hash(), texts), texts

    @staticmethod
    def sig(h, texts):
        """Screen identity. Live screens (a running clock) change every frame: use their text with digits masked."""
        if any(re.search(r"\d\d:\d\d:\d\d", t[2]) for t in texts):
            return "t" + hashlib.sha1("|".join(re.sub(r"\d", "#", t[2]) for t in sorted(texts)).encode()).hexdigest()[:11]
        return h

    def hold(self, code, secs):
        """Hold a key (e.g. Exit in a text editor clears the text), then release and settle."""
        self.steps += 1
        talk(self.port, ["lcd Hold %d true" % code, 'emulation RunFor "00:00:%09.6f"' % secs,
                         "lcd Hold %d false" % code, 'emulation RunFor "00:00:%09.6f"' % self.settle,
                         "lcd Screenshot @%s" % self.ppm])
        return self.frame_hash()

    def wait(self, secs):
        """Let the screen settle without a key press; return its hash."""
        talk(self.port, ['emulation RunFor "00:00:%09.6f"' % secs, "lcd Screenshot @%s" % self.ppm])
        return self.frame_hash()

    def highlight_y(self):
        """Top y (panel) of the list's highlight bar (blue, between title and soft keys), or None."""
        try:
            d = open(self.ppm, "rb").read()
            parts = d.split(b"\n", 3); w = int(parts[1].split()[0]); px = parts[3]
            for y in range(14, 138):
                for x in (40, 60, 90, 110):
                    o = (x * w + y) * 3
                    r, g, b = px[o], px[o + 1], px[o + 2]
                    if b > 120 and r < 60:
                        return y
        except (OSError, ValueError, IndexError):
            pass
        return None

    def frame_hash(self):
        return hashlib.sha1(open(self.ppm, "rb").read()).hexdigest()[:12]

    def save_shot(self, name):
        dst = os.path.join(self.out, "shots", name + ".png")
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        os.system("%s -I %s %s %s" % (sys.executable, os.path.join(HERE, "ppm2png.py"), self.ppm, dst))
        return os.path.relpath(dst, self.out)


def screen_text(texts):
    """Unique texts in screen order (top-to-bottom, left-to-right)."""
    seen, out = set(), []
    for y, x, s in sorted(texts):
        if s not in seen:
            seen.add(s); out.append(s)
    return out


def item_label(texts, hl_y=None):
    """Label of the newly highlighted list row: the text drawn on the highlight bar (from the screenshot);
    else the last text drawn between title and soft keys (wrong on scrolled pages)."""
    if hl_y is not None:
        on = [s for y, x, s in texts if hl_y - 2 <= y <= hl_y + 12]
        if on:
            return on[-1]
    rows = [s for y, x, s in texts if 14 < y < 135]
    return rows[-1] if rows else (texts[-1][2] if texts else "?")


def is_home(texts):
    s = [t[2] for t in texts]
    return "MENU" in s and "LIST" in s


def is_popup(text):
    """Message popup (1-2 lines, no soft keys): closes by itself after ~1 s."""
    return 0 < len(text) <= 2 and not any(t in ("Select", "Back", "Confirm", "Exit", "Option") for t in text)


def is_toast(text, rows=()):
    """Popup after selecting an option value: "<value> | Selected", "Record On", "Digi Moni | Off":
    it says "Selected" or names one of the list's rows."""
    if len(text) <= 3 and "Selected" in text:
        return True
    if not is_popup(text) or any(("Empty" in t or "!" in t or "Fail" in t) for t in text):
        return False
    words = set(" ".join(text).lower().replace(":", " ").split())
    joined = " ".join(text).lower()
    rows = [r.lower() for r in rows if r not in ("Select", "Back", "Exit", "Confirm")]
    return any(r in words or (" " in r and r in joined) for r in rows)


NO_SELECT = ("Language", "Reset", "Factory Reset", "Init")   # lists whose values must not be selected
EDITOR_HINTS = ("AB", "ab", "Ab", "123", "Text", "abc", "ABC")


class Crawler:
    def __init__(self, r, depth, log):
        self.r, self.depth, self.log = r, depth, log
        self.home = None
        self.titles = ["Main Menu"]

    def go_home(self):
        """Exit until the main screen (soft keys MENU / LIST). Exit on the main screen opens the TG list."""
        for i in range(20):
            if i in (5, 9, 13):
                self.r.hold(EXIT, 6.0)          # text editor: Exit deletes one char, long press clears
            h, texts = self.r.key(EXIT)
            if is_home(texts):
                return True
        return False

    def walk(self, path, here_hash, node, level):
        """Enumerate the list currently shown (hash here_hash, reached by `path` + ENTER)."""
        self.titles.append((node.get("text") or ["?"])[0])
        try:
            self._walk(path, here_hash, node, level)
        finally:
            self.titles.pop()

    def _walk(self, path, here_hash, node, level):
        t0 = (node.get("text") or ["?"])[0]
        if t0 in NO_SELECT:                     # selecting would break the crawl (UI language): read rows only
            node["options"] = [x for x in node.get("text", [])[1:] if x not in ("Select", "Back", "Exit")]
            node["not_selected"] = True
            self.log("%s= %s (not selected)" % ("  " * level, ", ".join(node["options"])))
            return
        first = here_hash
        cur = here_hash
        seen = {here_hash}
        t = node.get("text", [])
        labels = {t[1]} if len(t) > 2 else set()       # first row of the list
        cur_label = t[1] if len(t) > 2 else None
        idx = 0
        # Data lists ("TG 1/69", channels, messages): sample the first few entries only.
        limit = 3 if any(re.search(r"\d+/\d+$", t) for t in node.get("text", [])[:2]) else 60
        while idx < limit:
            h, texts = self.r.key(MENU)
            child = {"index": idx, "text": screen_text(texts), "hash": h, "items": []}
            name = "_".join(str(p) for p in path + [idx])
            if h != cur:
                child["shot"] = self.r.save_shot(name)
                self.log("%s%s -> %s" % ("  " * level, name, " | ".join(child["text"][:6])))
                editor = any(t in EDITOR_HINTS for t in child["text"]) or \
                    ("Confirm" in child["text"] and "Delete" in child["text"])
                if editor:
                    child["editor"] = True
                pop = is_popup(child["text"])
                if is_home(texts):
                    child["home"] = True            # item acted and went back to the main screen
                elif pop:
                    child["popup"] = True           # closes by itself (back to this list or one level up)
                elif level < self.depth and not editor:
                    self.walk(path + [idx], h, child, level + 1)
                    if child.get("at_parent"):       # option list closed itself: already back here
                        child["home_back"] = True
                if child.get("editor"):
                    self.r.hold(EXIT, 3.0)
                if child.get("home"):
                    hb = None
                elif child.get("home_back"):
                    hb = self.r.wait(0.1)
                elif pop:
                    pos = os.path.getsize(self.r.txt)
                    hb = self.r.wait(2.0)
                    tw = self.r._texts(pos)
                    tt = screen_text(tw)
                    # popup followed by a new screen of its own (e.g. "No GPS Position" -> Location list)
                    if (hb != cur and tt and not is_home(tw) and tt[0] not in self.titles
                            and any(x in ("Select", "Back") for x in tt)):
                        nxt = {"index": 0, "text": tt, "hash": hb, "items": [], "shot": self.r.save_shot(name + "_then")}
                        child["then"] = nxt
                        self.log("%s%s then -> %s" % ("  " * level, name, " | ".join(tt[:6])))
                        if level < self.depth:
                            self.walk(path + [idx], hb, nxt, level + 1)
                        hb, _ = self.r.key(EXIT)
                else:
                    hb, tx = self.r.key(EXIT)
                    # back in this list, but the page re-rendered (scrolled): same title, bar on the same row
                    if (hb != cur and cur_label and item_label(tx, self.r.hl_y) == cur_label
                            and (node.get("text") or ["?"])[0] in [t[2] for t in tx]):
                        cur = hb
                        seen.add(hb)
                soft = ("Select", "Back", "Exit", "Confirm")
                rows = [x for x in node.get("text", [])[1:] if x not in soft]
                if hb != cur and pop and idx == 0 and is_toast(child["text"], rows) and len(rows) < 5:
                    # whole option list visible, and the popup already took us back to the parent list
                    node["items"].append(child)
                    node["options"], node["count"], node["at_parent"] = rows, len(rows), True
                    self.log("%s= %s" % ("  " * level, ", ".join(rows)))
                    return
                if hb != cur and pop and idx == 0 and is_toast(child["text"], rows):
                    # value popups drop back to the parent list: re-enter this list with one Menu press
                    hm, tm = self.r.key(MENU)
                    if node.get("text") and node["text"][0] in [t[2] for t in tm]:
                        hb = cur = hm
                        seen.add(hm)
                if hb != cur:
                    hb = self.replay_to(path, idx)
                    if hb != cur:
                        self.log("%s!! lost at %s (got %s want %s)" % ("  " * level, name, hb, cur))
                        cur = hb
            else:
                child["leaf"] = "no screen change"
            node["items"].append(child)
            if idx == 0 and is_toast(child.get("text", []), node.get("text", [])[1:]):
                self.options(node, child, level)
                return
            sig = [c.get("text", [])[:6] for c in node["items"][-3:]]
            if len(sig) == 3 and sig[0] == sig[1] == sig[2] and sig[0]:
                node["sampled"] = True
                break
            h, texts = self.r.key(DOWN)
            lab = item_label(texts, self.r.hl_y)
            # wrap: same frame as before, or (scrolling lists redraw differently) a row label seen already
            if h in seen or h == first or lab in labels:
                self.log("%s. end of list after %d (%s)" % ("  " * level, idx + 1,
                         "label %r again" % lab if lab in labels else "frame seen"))
                break
            labels.add(lab)
            cur_label = lab
            child["label_next"] = lab
            seen.add(h)
            cur = h
            idx += 1
        node["count"] = idx + 1

    def options(self, node, first, level):
        """Option list (Select shows a "Selected" toast): read the values by moving DOWN, no more selecting.
        The first value got selected by the probe (fresh emulator per main item, so that only affects siblings)."""
        soft = ("Select", "Back", "Exit", "Confirm")
        vals = [x for x in node.get("text", [])[1:] if x not in soft]   # rows visible when the list opened
        down = []
        for _ in range(120):                    # the cursor may start on the current value, not the top
            h, texts = self.r.key(DOWN)
            lab = item_label(texts, self.r.hl_y)
            if lab in down:
                break
            down.append(lab)
            if lab not in vals:
                vals.append(lab)
        node["options"] = vals
        node["count"] = len(vals)
        self.log("%s= %s" % ("  " * level, ", ".join(vals)))

    def replay_to(self, path, idx):
        """Go home, open the main menu, follow `path` (downs then ENTER per level), cursor down idx times."""
        if not self.go_home():
            raise RuntimeError("cannot get home")
        h, _ = self.r.key(MENU)
        for downs in path:
            for _ in range(downs):
                h, _ = self.r.key(DOWN)
            h, _ = self.r.key(MENU)
        for _ in range(idx):
            h, _ = self.r.key(DOWN)
        return h


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", type=int, default=1335)
    ap.add_argument("--out", default=os.path.join(HERE, "out", "crawl"))
    ap.add_argument("--depth", type=int, default=4)
    ap.add_argument("--settle", type=float, default=0.6)
    ap.add_argument("--only", type=int, default=-1, help="main-menu item index to crawl (default all)")
    a = ap.parse_args()
    a.out = os.path.abspath(a.out)
    os.makedirs(a.out, exist_ok=True)
    logf = open(os.path.join(a.out, "crawl.log"), "a")
    def log(s):
        print(s, flush=True); logf.write(s + "\n"); logf.flush()
    r = Radio(a.port, a.out, a.settle)
    c = Crawler(r, a.depth, log)
    if not c.go_home():
        raise SystemExit("main screen not found")
    c.home = r.frame_hash()
    r.save_shot("home")
    t0 = time.time()
    h, texts = r.key(MENU)
    root = {"text": screen_text(texts), "hash": h, "items": [], "shot": r.save_shot("menu")}
    log("menu -> " + " | ".join(root["text"]))
    if a.only >= 0:
        for _ in range(a.only):
            h, texts = r.key(DOWN)
        label = item_label(texts, r.hl_y) if a.only else ""
        hh, texts = r.key(MENU)
        child = {"index": a.only, "label": label, "text": screen_text(texts), "hash": hh, "items": [],
                 "shot": r.save_shot(str(a.only))}
        log("%d -> %s" % (a.only, " | ".join(child["text"][:6])))
        root["items"].append(child)
        try:
            if hh != h:
                c.walk([a.only], hh, child, 2)
        except Exception as e:                  # keep what was crawled
            child["error"] = repr(e)
            log("!! crawl stopped: %r" % e)
    else:
        c.walk([], h, root, 1)
    json.dump(root, open(os.path.join(a.out, "tree%s.json" % ("" if a.only < 0 else "_%d" % a.only)), "w"),
              indent=1, ensure_ascii=False)
    log("done: %d key presses, %.0f s" % (r.steps, time.time() - t0))


if __name__ == "__main__":
    main()
