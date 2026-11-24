# AT32F403A flash controller @0x40022000 (cfw overlay, emu/cfw168.repl): bank 1 regs at +0x00, bank 2 at +0x40.
# Programming itself is a plain write to the MappedMemory flash; this model handles unlock / lock (OPLK), the
# busy flag (never busy) and sector erase (SECERS + ADDR + ERSTR: 2 KB filled with 0xFF). Bootloader sectors
# (0x08000000-0x08003FFF) are never erased here either.
if request.IsInit:
    regs = {0x10: 0x80, 0x50: 0x80}
    unl = {0: 0, 1: 0}
elif request.IsWrite:
    o = request.Offset
    v = request.Value
    b = 1 if o >= 0x40 else 0
    r = o - 0x40 * b
    if r == 0x04:
        if v == 0x45670123:
            unl[b] = 1
        elif v == 0xCDEF89AB and unl[b] == 1:
            regs[0x10 + 0x40 * b] = regs.get(0x10 + 0x40 * b, 0x80) & ~0x80
            unl[b] = 0
    elif r == 0x0C:
        regs[o] = regs.get(o, 0) & ~(v & 0x34)
    elif r == 0x10:
        if (regs.get(o, 0x80) & 0x80) == 0 or (v & 0x80):
            regs[o] = v
            if (v & 0x42) == 0x42:
                a = regs.get(0x14 + 0x40 * b, 0) & ~0x7FF
                if 0x08004000 <= a < 0x08100000:
                    from System import Array, Byte
                    self.GetMachine().SystemBus.WriteBytes(Array[Byte]([0xFF] * 0x800), a)
                regs[o] = v & ~0x40
                regs[0x0C + 0x40 * b] = regs.get(0x0C + 0x40 * b, 0) | 0x20
    else:
        regs[o] = v
else:
    request.Value = regs.get(request.Offset, 0) & (~1 if (request.Offset & 0x3F) == 0x0C else 0xFFFFFFFF)
