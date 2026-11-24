#!/usr/bin/env python3
"""Crawl every main-menu item of the stock D168UV firmware, each in a FRESH emulator (the crawler presses
Select on option lists, which changes settings, so one long crawl drifts). Port of D890UV_src/emu/crawl_all.py.

  python3 emu/crawl_all.py [--items 0-12] [--jobs 3] [--depth 4] [--out emu/out/crawl]
Per item: run.py --serve on its own port (from a queue) -> crawl.py --only N --out OUT/item_NN -> quit.
Then: python3 emu/crawl_report.py emu/out/crawl  (tree.md + emu/MENUS.md)
"""
import argparse, os, queue, subprocess, sys, time
from concurrent.futures import ThreadPoolExecutor
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from ctl import talk


def broker_up(port):
    try:
        talk(port, ["version"], timeout=900)
        return True
    except (OSError, ValueError):
        return False


def one(i, port, a):
    out = os.path.join(a.out, "item_%02d" % i)
    os.makedirs(out, exist_ok=True)
    log = open(os.path.join(out, "driver.txt"), "w")
    subprocess.call([sys.executable, os.path.join(HERE, "run.py"), "--seconds", "16",
                     "--name", "crawl_item%02d" % i, "--serve", str(port)], stdout=log, stderr=subprocess.STDOUT)
    t0 = time.time()
    while not broker_up(port):
        if time.time() - t0 > 900:
            log.write("broker never came up\n"); return i, "no emulator"
        time.sleep(3)
    rc = subprocess.call([sys.executable, os.path.join(HERE, "crawl.py"), "--port", str(port), "--only", str(i),
                          "--depth", str(a.depth), "--out", out], stdout=log, stderr=subprocess.STDOUT)
    try:
        talk(port, ["quit"], timeout=20)
    except Exception:
        pass
    return i, "ok" if rc == 0 else "crawl rc=%d" % rc


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--items", default="0-12")
    ap.add_argument("--jobs", type=int, default=3)
    ap.add_argument("--depth", type=int, default=4)
    ap.add_argument("--base-port", type=int, default=1400)
    ap.add_argument("--out", default=os.path.join(HERE, "out", "crawl"))
    a = ap.parse_args()
    a.out = os.path.abspath(a.out)
    items = []
    for part in a.items.split(","):
        lo, _, hi = part.partition("-")
        items += list(range(int(lo), int(hi or lo) + 1))
    ports = queue.Queue()
    for k in range(a.jobs):
        ports.put(a.base_port + 10 * k)

    def job(i):
        p = ports.get()                     # a port is reused only after its emulator has quit
        try:
            return one(i, p, a)
        finally:
            time.sleep(3); ports.put(p)
    with ThreadPoolExecutor(a.jobs) as ex:
        for f in [ex.submit(job, i) for i in items]:
            i, r = f.result()
            print("item %d: %s" % (i, r), flush=True)


if __name__ == "__main__":
    main()
