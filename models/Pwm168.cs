// AT32F403A TMR4 @0x40000800 as a PWM TX-audio source with capture, D168UV cfw overlay (cfw168.repl).
// Stock V1.07 drives TX audio as TMR4_CH4 PWM on PB9 (FUN_08091bd4 AF, FUN_08095c70 -> CCR4, paced by TMR5); the cfw
// (D168UV/cfw/drivers/dac.c) runs TMR4 at 48 kHz (DIV 0, PR 2499) and reloads CCR4 every period from DMA1 ch7
// (TMR4 overflow request, OVFDEN = IDEN bit 8).
// Model: register file of a general-purpose timer (no interrupts, no output pin). While CTRL1.CEN is set a clock entry
// at TimerClockHz / ((DIV+1)(PR+1)) runs one PWM period: with OVFDEN the DMA1 channel whose CPAR is CCR4 (0x40000840)
// delivers the next halfword from CMAR (circular, CNDTR = halfwords left, as the Renode DMA model does not read back
// CCR.EN); then, if C4EN, the duty is recorded as a 12-bit sample: CCR4 * 4096 / (PR+1) (= the RC-filtered PWM level).
// Capture: one uint16 LE per period, .adc16 = what `analog Stream` plays into another radio. Automatic while rf0
// transmits: $D168_OUT_DIR/tx_<n>.adc16 + .json (dac AutoCapture is off in cfw168.repl).
// Monitor (device "pwm"): pwm Capture @f.adc16 / pwm CaptureStop / pwm Status
using System;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class D168Pwm : IDoubleWordPeripheral
    {
        public D168Pwm(IMachine machine)
        {
            this.machine = machine;
            machine.ClockSource.AddClockEntry(new ClockEntry(1, 1000, Tick, this, "pwm168"));
        }

        public long Size => 0x400;
        public double TimerClockHz { get; set; } = 120000000;
        public bool AutoCapture { get; set; } = true;
        public uint Ccr4 => Reg(0x40);

        public void Reset() { regs.Clear(); }

        public uint ReadDoubleWord(long offset) { return Reg(offset); }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch(offset)
            {
            case 0x10: regs[0x10] = Reg(0x10) & value; break;        // STS: write 0 to clear
            case 0x14: regs[0x14] = 0; break;                          // SWEVT: update event, self-clearing
            default: regs[offset] = value; break;
            }
        }

        public void Capture(string path) { Open(path, false); }
        public void CaptureStop() { Close(); }
        public void Stop() { Close(); }
        public string Status()
        {
            return string.Format("cen {0} div {1} pr {2} rate {3:F1} c4en {4} ccr4 {5} ovfden {6} dma ch {7} ring 0x{8:X8} {9}; " +
                "capture {10} ({11} samples), rf tx {12}, captures {13}",
                (Reg(0) & 1) != 0, Reg(0x28) & 0xFFFF, Reg(0x2C) & 0xFFFF, rate, (Reg(0x20) & 0x1000) != 0, Reg(0x40),
                (Reg(0x0C) & 0x100) != 0, dmaCh, ringAddr, ringLen, capFile ?? "off", capCount, RfTx(), txIndex);
        }

        private uint Reg(long o) { uint v; return regs.TryGetValue(o, out v) ? v : 0; }

        private void Tick()
        {
            double r = 0;
            if((Reg(0) & 1) != 0)
            {
                double div = (Reg(0x28) & 0xFFFF) + 1.0, pr = (Reg(0x2C) & 0xFFFF) + 1.0;
                r = TimerClockHz / (div * pr);
                if(r > 2e6) { r = 0; }
            }
            if(Math.Abs(r - rate) > 0.01)
            {
                if(rate > 0) { machine.ClockSource.TryRemoveClockEntry(Period); }
                rate = r;
                dmaCh = -1;
                if(r > 0) { machine.ClockSource.AddClockEntry(new ClockEntry(1, (ulong)Math.Round(r), Period, this, "pwm168-period")); }
            }
            if(AutoCapture)
            {
                bool tx = RfTx();
                if(tx && !txOn) { txOn = true; txIndex++; StartAuto(); }
                else if(!tx && txOn) { txOn = false; if(autoFile) { Close(); } }
            }
        }

        private void Period()
        {
            if((Reg(0x0C) & 0x100) != 0) { DmaFetch(); }
            regs[0x10] = Reg(0x10) | 1;                                // overflow flag
            if((Reg(0x20) & 0x1000) == 0 || cap == null) { return; }
            double pr = (Reg(0x2C) & 0xFFFF) + 1.0;
            double v = Math.Min(Reg(0x40), pr) * 4096.0 / pr;
            ushort sample = (ushort)Math.Min(4095.0, Math.Round(v));
            cap.Write(sample);
            capCount++;
        }

        private void DmaFetch()
        {
            var bus = machine.SystemBus;
            if(dmaCh < 0) { FindDma(); if(dmaCh < 0) { return; } }
            ulong b = 0x40020008 + 0x14 * (ulong)(dmaCh - 1);
            if(bus.ReadDoubleWord(b + 8) != 0x40000840) { dmaCh = -1; return; }
            uint left = bus.ReadDoubleWord(b + 4) & 0xFFFF;              // firmware re-armed the channel: resync
            if(left == 0) { return; }
            if(left != (uint)(ringLen - ringPos))
            {
                ringAddr = bus.ReadDoubleWord(b + 0xC);
                if(left > ringLen) { ringLen = (int)left; }
                ringPos = ringLen - (int)left;
            }
            regs[0x40] = bus.ReadWord(ringAddr + (ulong)(2 * ringPos));
            ringPos++;
            if(ringPos >= ringLen) { ringPos = 0; }
            bus.WriteDoubleWord(b + 4, (uint)(ringLen - ringPos));
        }

        private void FindDma()
        {
            var bus = machine.SystemBus;
            for(int ch = 1; ch <= 7; ch++)
            {
                ulong b = 0x40020008 + 0x14 * (ulong)(ch - 1);
                if(bus.ReadDoubleWord(b + 8) != 0x40000840) { continue; }
                int n = (int)(bus.ReadDoubleWord(b + 4) & 0xFFFF);
                if(n == 0) { continue; }
                dmaCh = ch;
                ringAddr = bus.ReadDoubleWord(b + 0xC);
                ringLen = n;
                ringPos = 0;
                return;
            }
        }

        private bool RfTx()
        {
            IPeripheral p;
            if(!machine.TryGetByName("sysbus.rf0", out p)) { return false; }
            var rf = p as D890RfTransceiver;
            return rf != null && rf.Transmitting;
        }

        private void StartAuto()
        {
            string dir = Environment.GetEnvironmentVariable("D168_OUT_DIR");
            if(string.IsNullOrEmpty(dir)) { return; }
            Open(Path.Combine(dir, string.Format("tx_{0}.adc16", txIndex)), true);
        }

        private void Open(string path, bool auto)
        {
            Close();
            capFile = path; autoFile = auto; capCount = 0; capStart = Now();
            cap = new BinaryWriter(File.Open(path, FileMode.Create));
            IPeripheral p;
            if(machine.TryGetByName("sysbus.rf0", out p)) { var rf = p as D890RfTransceiver; if(rf != null) { capHz = rf.TunedHz; } }
        }

        private void Close()
        {
            if(cap == null) { return; }
            cap.Close();
            cap = null;
            string json = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{{\"file\": \"{0}\", \"samples\": {1}, \"rate\": {2:F1}, \"duration_s\": {3:F4}, \"start_s\": {4:F4}, " +
                "\"rf_tx_reg_hz\": {5}, \"source\": \"TMR4_CH4 PWM PB9\", \"auto\": {6}}}\n",
                Path.GetFileName(capFile), capCount, rate, Now() - capStart, capStart, capHz, autoFile ? "true" : "false");
            File.WriteAllText(Path.ChangeExtension(capFile, ".json"), json);
            capFile = null;
        }

        private double Now() { return machine.ElapsedVirtualTime.TimeElapsed.TotalSeconds; }

        private readonly IMachine machine;
        private readonly Dictionary<long, uint> regs = new Dictionary<long, uint>();
        private double rate, capStart;
        private int dmaCh = -1, ringLen, ringPos, txIndex;
        private ulong ringAddr;
        private BinaryWriter cap;
        private string capFile;
        private bool autoFile, txOn;
        private long capCount, capHz;
    }
}
