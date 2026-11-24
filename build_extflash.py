#!/usr/bin/env python3
"""Build the emulated D168UV SPI-NAND contents (images/extflash.pages = (u32 page addr, 2048 bytes)*).

Source: ICON package .CDI (0x116-byte entries: name, u32 addr, u32 size, u32 offset) + .CDD blob,
fonts/logo/icons at 0x20000-0x614C00. Optional --base: a pages file saved from the emulator after the
firmware's first-boot NAND format (nand SaveOverlay), so later boots skip it.
"""
import argparse, os, struct

HERE = os.path.dirname(os.path.abspath(__file__))
PKG = os.path.join(os.path.dirname(HERE), "pkg", "D168UV_ICON_V1.34")
PAGE = 0x800


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base")
    ap.add_argument("--no-icon", action="store_true")
    ap.add_argument("--out", default=os.path.join(HERE, "images", "extflash.pages"))
    a = ap.parse_args()
    pages = {}
    if a.base:
        d = open(a.base, "rb").read()
        for o in range(0, len(d), 4 + PAGE):
            pages[struct.unpack_from("<I", d, o)[0]] = bytearray(d[o + 4:o + 4 + PAGE])
    def write(addr, data):
        while data:
            pa, off = addr & ~(PAGE - 1), addr & (PAGE - 1)
            n = min(len(data), PAGE - off)
            pages.setdefault(pa, bytearray(b"\xff" * PAGE))[off:off + n] = data[:n]
            addr, data = addr + n, data[n:]
    if not a.no_icon:
        cdi = open(os.path.join(PKG, "D168UV_ICON_V1.34.CDI"), "rb").read()
        cdd = open(os.path.join(PKG, "D168UV_ICON_V1.34.CDD"), "rb").read()
        for i in range(len(cdi) // 0x116):
            addr, n, off = struct.unpack_from("<III", cdi, i * 0x116 + 0x100)
            write(addr, cdd[off:off + n])
        print("ICON: %d files" % (len(cdi) // 0x116))
    if not a.base:
        # Factory format of NAND block 0 (FUN_0809c5d0): bad-block table slot 0 (0x1F8 bytes, 0x1F4 = 0x55 used,
        # 0x1F5 = slot, 0x1F6 = bad count, 0x1F7 = 0) and the marker at 0x1FF00 that FUN_08098xxx checks.
        # Without it the firmware erases and test-writes every block on boot (wiping the ICON data).
        t = bytearray(0x1F8); t[0x1F4] = 0x55
        write(0, bytes(t))
        write(0x1FF00, b"CHECKING BLOCK FOR VALID.")
        print("block 0: format marker + empty bad-block table")
    with open(a.out, "wb") as f:
        n = 0
        for pa in sorted(pages):
            if bytes(pages[pa]) != b"\xff" * PAGE:
                f.write(struct.pack("<I", pa) + pages[pa]); n += 1
    print("%s: %d pages" % (a.out, n))


if __name__ == "__main__":
    main()
