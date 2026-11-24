// AT32F403A / STM32F1-style RTC @0x40002800: 32-bit seconds counter (CNTH/CNTL), second flag + interrupt
// (CRH.SECIE -> RTC IRQ 3, stock IRQ3_Handler 0x08077d4c). Counter starts at host local time (seconds since
// 1970) on first reset and keeps running across firmware resets; firmware writes to CNT set it.
// Registers: 0x00 CRH, 0x04 CRL (SECF 0, ALRF 1, OWF 2, RSF 3, CNF 4, RTOFF 5), 0x08/0x0C PRL, 0x10/0x14 DIV,
// 0x18/0x1C CNT, 0x20/0x24 ALR.
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class D168Rtc : IDoubleWordPeripheral, IWordPeripheral
    {
        public D168Rtc(IMachine machine)
        {
            this.machine = machine;
            IRQ = new GPIO();
            machine.ClockSource.AddClockEntry(new ClockEntry(1, 1, Second, this, "rtc168"));
            Reset();
        }

        public GPIO IRQ { get; }
        public long Size => 0x400;

        public void Reset()
        {
            if(!started)
            {
                started = true;
                var t = DateTime.Now;
                counter = (uint)(t - new DateTime(1970, 1, 1)).TotalSeconds;
            }
            crh = 0; secf = false; alrf = false;
            IRQ.Unset();
        }

        public ushort ReadWord(long offset) { return (ushort)ReadDoubleWord(offset); }
        public void WriteWord(long offset, ushort value) { WriteDoubleWord(offset, value); }

        public uint ReadDoubleWord(long offset)
        {
            switch(offset)
            {
            case 0x00: return crh;
            case 0x04: return (secf ? 1u : 0) | (alrf ? 2u : 0) | 0x08 | (cnf ? 0x10u : 0) | 0x20;
            case 0x08: return prl >> 16;
            case 0x0C: return prl & 0xFFFF;
            case 0x10: return 0;
            case 0x14: return 0x7FFF;
            case 0x18: return counter >> 16;
            case 0x1C: return counter & 0xFFFF;
            case 0x20: return alarm >> 16;
            case 0x24: return alarm & 0xFFFF;
            }
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch(offset)
            {
            case 0x00: crh = value & 7; break;
            case 0x04:
                cnf = (value & 0x10) != 0;
                if((value & 1) == 0) { secf = false; }
                if((value & 2) == 0) { alrf = false; }
                break;
            case 0x08: prl = (prl & 0xFFFF) | ((value & 0xF) << 16); break;
            case 0x0C: prl = (prl & 0xF0000) | (value & 0xFFFF); break;
            case 0x18: counter = (counter & 0xFFFF) | (value << 16); break;
            case 0x1C: counter = (counter & 0xFFFF0000) | (value & 0xFFFF); break;
            case 0x20: alarm = (alarm & 0xFFFF) | (value << 16); break;
            case 0x24: alarm = (alarm & 0xFFFF0000) | (value & 0xFFFF); break;
            }
            Update();
        }

        private void Second()
        {
            counter++;
            secf = true;
            if(counter == alarm) { alrf = true; }
            Update();
        }

        private void Update()
        {
            IRQ.Set(((crh & 1) != 0 && secf) || ((crh & 2) != 0 && alrf));
        }

        private readonly IMachine machine;
        private bool started, secf, alrf, cnf;
        private uint crh, prl = 0x7FFF, counter, alarm = 0xFFFFFFFF;
    }
}
