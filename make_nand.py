#!/usr/bin/env python3
"""Synthetic D168UV NAND image for the emulator: images/synth.pages, made from scratch (no AnyTone files).

The published emulator cannot ship images/extflash.pages / extflash_formatted.pages (stock first-boot format +
AnyTone ICON fonts/logo). This writes a stand-in with only our own content:

  * block 0: empty bad-block table (slot 0 used, 0 bad blocks) + marker 0x1FF00 "CHECKING BLOCK FOR VALID."
    (without it the stock firmware erases and test-writes every block on boot);
  * 0x3E0000: the 10-byte check value the stock firmware compares (FUN_08028760: 10 bytes against its own
    constant, else "UPDATE MODE"); the bytes are 0x55..0x5E ("UVWXYZ[\\]^");
  * 0x02FE0000 "CHECKING DATA FOR D868S!": data-area format marker (stock re-initialises the data areas without it);
  * 0x02500000: stock settings block (128 KB block, valid marker at +0x1FFFC), first 2 KB all zero. Blank (0xFF)
    settings + the data marker leave the stock firmware in a boot loop (FUN_080284e8, settings byte 7); with zeros it
    gets past it, loads the calibration (cfw/tools/apc_test168.py reads its RAM copy) and then stops at "Program Error
    Please Reset The Radio!" (no codeplug). Zeros are our choice, not stock defaults;
  * default RF calibration block 0x02F60000 (copy A, 128 KB block, valid marker 55 55 AA AA at +0x1FFFC): the tables
    of cfw/drivers/cal_default.c (made by cfw/tools/make_cal_default.py), in the layout cfw/drivers/dac.c reads.

No channels: the cfw168 does not read the stock NAND channel layout (cfw/include/board_cfg.h CFW_NAND_CODEPLUG 0);
it shows its built-in channels or the cfw codeplug. No ICON data: the stock firmware boots but draws no text.

Pages file format (as emu/build_extflash.py): sequence of (u32 page address LE, 2048 bytes) for non-blank pages.

  python3 emu/make_nand.py [--out images/synth.pages]
"""
import argparse
import os
import re
import struct

HERE = os.path.dirname(os.path.abspath(__file__))
PAGE = 0x800
VALID = b"\x55\x55\xaa\xaa"
CAL = 0x02F60000
SETTINGS = 0x02500000


class Flash:
    def __init__(self):
        self.pages = {}

    def write(self, addr, data):
        data = bytes(data)
        while data:
            pa, off = addr & ~(PAGE - 1), addr & (PAGE - 1)
            n = min(len(data), PAGE - off)
            self.pages.setdefault(pa, bytearray(b"\xff" * PAGE))[off:off + n] = data[:n]
            addr, data = addr + n, data[n:]

    def save(self, path):
        n = 0
        with open(path, "wb") as f:
            for pa in sorted(self.pages):
                p = bytes(self.pages[pa])
                if p != b"\xff" * PAGE:
                    f.write(struct.pack("<I", pa) + p)
                    n += 1
        return n


CAL_C = os.path.join(os.path.dirname(HERE), "cfw", "drivers", "cal_default.c")


def cal_default(path=CAL_C):
    """bytes of cal_default[] in cfw/drivers/cal_default.c (None when the file is missing)"""
    if not os.path.exists(path):
        return None
    s = open(path).read()
    s = re.sub(r"/\*.*?\*/", "", s[s.index("cal_default[CAL_DEFAULT_LEN]"):], flags=re.S)
    return bytes(int(x, 16) for x in re.findall(r"0x([0-9A-Fa-f]{2})\b", s))


def rf_cal(fl):
    cal = cal_default()
    if cal is None:
        print("note: %s missing, no RF calibration block (cfw refuses TX)" % CAL_C)
        return
    fl.write(CAL, cal)
    fl.write(CAL + 0x1FFFC, VALID)


def build():
    fl = Flash()
    table = bytearray(0x1F8)
    table[0x1F4] = 0x55                                      # slot 0 used, index 0, 0 bad blocks
    fl.write(0x00000, table)
    fl.write(0x1FF00, b"CHECKING BLOCK FOR VALID.")
    fl.write(0x3E0000, bytes(range(0x55, 0x5F)))
    fl.write(0x02FE0000, b"CHECKING DATA FOR D868S!")
    fl.write(SETTINGS, bytes(0x800))
    fl.write(SETTINGS + 0x1FFFC, VALID)
    rf_cal(fl)
    return fl


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--out", default=os.path.join(HERE, "images", "synth.pages"))
    a = ap.parse_args()
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    n = build().save(a.out)
    print("%s: %d pages" % (a.out, n))


if __name__ == "__main__":
    main()
