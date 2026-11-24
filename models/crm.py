# AT32F403A CRM @0x40021000 (STM32F1-like RCC): clocks become ready as soon as they are enabled.
# Test knob (cfw pre-flash test F9): write 1 to offset 0x3FC (sysbus WriteDoubleWord 0x400213FC 1) = HEXT never stable.
if request.IsInit:
    regs = {0x00: 0x00000083, 0x24: 0x0C000000}
    def ctrl(v):
        for on, rdy in ((0, 1), (16, 17), (24, 25)):
            v = (v | (1 << rdy)) if v & (1 << on) else (v & ~(1 << rdy))
        if regs.get(0x3FC, 0):
            v &= ~(1 << 17)
        return v
    def cfg(v):
        return (v & ~0xC) | ((v & 0x3) << 2)
    def bpdc(v):                     # LEXT enable bit0 -> stable bit1
        return (v | 2) if v & 1 else v
    def ctrlsts(v):                  # LICK enable bit0 -> stable bit1
        return (v | 2) if v & 1 else v
    rules = {0x00: ctrl, 0x04: cfg, 0x20: bpdc, 0x24: ctrlsts}
elif request.IsWrite:
    regs[request.Offset] = request.Value
else:
    v = regs.get(request.Offset, 0)
    f = rules.get(request.Offset)
    request.Value = f(v) if f else v
