// AT32F403A DAC @0x40007400 (F1 register layout) with TX capture, D168UV. Port of D890UV_src/emu/models/Dac.cs.
// Stock V1.07: ch1 (PA4) = APC level (FUN_08004364, D1DTH12R = v << 4) switched by the TX-slot EXTI5 handler;
// ch2 (PA5) = RX tune voltage per frequency (FUN_0809099c). cfw (D168UV/cfw/drivers/dac.c): ch2 carries the TX
// baseband test stream at 48 kHz (TEN2, TSEL2 100 = TMR2 TRGO, DMA2 ch4 circular; CNDTR4 kept here).
// Registers: CTRL 0x00 (EN1 b0, TEN1 b2, TSEL1 b5:3, DMAEN1 b12; ch2 +16), SWTRG 0x04, D1DTH12R 0x08, D1DTH12L 0x0C,
// D1DTH8R 0x10, D2DTH12R 0x14, D2DTH12L 0x18, D2DTH8R 0x1C, DDTH12R 0x20, DDTH12L 0x24, DDTH8R 0x28, D1ODT 0x2C, D2ODT 0x30.
// Output: without trigger ODT = DTH at once; with TENx the ODT loads on the trigger (SWTRG or the timer).
// DMA: the DMA2 channel whose CPAR is this channel's DTH register (DAC ch1 -> DMA2 ch3, ch2 -> ch4 on the F1 map;
// any channel is accepted), one halfword per trigger from CMAR, CNDTR = halfwords left.
// Capture (CaptureChannel, default 2): one uint16 LE 12-bit sample per ODT update, .adc16 = what `analog Stream`
// plays into another radio. Automatic while rf0 transmits: $D168_OUT_DIR/tx_<n>.adc16 + .json.
// Monitor (device "dac"): dac Capture @f.adc16 / dac Stop / dac Status
using System;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class D168Dac : IDoubleWordPeripheral
    {
        public D168Dac(IMachine machine)
        {
            this.machine = machine;
            machine.ClockSource.AddClockEntry(new ClockEntry(1, 1000, Tick, this, "dac168"));
        }

        public long Size => 0x400;
        public double TimerClockHz { get; set; } = 120000000;
        public int CaptureChannel { get; set; } = 2;
        public bool AutoCapture { get; set; } = true;
        public uint Odt1 => dor[0];
        public uint Odt2 => dor[1];

        public void Reset() { regs.Clear(); dor[0] = dor[1] = 0; }

        public uint ReadDoubleWord(long offset)
        {
            switch(offset)
            {
            case 0x2C: return dor[0];
            case 0x30: return dor[1];
            default: return Reg(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            regs[offset] = value;
            switch(offset)
            {
            case 0x04:
                if((value & 1) != 0) { Trigger(0); }
                if((value & 2) != 0) { Trigger(1); }
                regs[0x04] = 0;
                break;
            case 0x08: case 0x0C: case 0x10: dhr[0] = Dhr(offset, value); Loaded(0); break;
            case 0x14: case 0x18: case 0x1C: dhr[1] = Dhr(offset - 0x0C, value); Loaded(1); break;
            case 0x20: dhr[0] = value & 0xFFF; dhr[1] = (value >> 16) & 0xFFF; Loaded(0); Loaded(1); break;
            case 0x24: dhr[0] = (value >> 4) & 0xFFF; dhr[1] = (value >> 20) & 0xFFF; Loaded(0); Loaded(1); break;
            case 0x28: dhr[0] = (value & 0xFF) << 4; dhr[1] = ((value >> 8) & 0xFF) << 4; Loaded(0); Loaded(1); break;
            }
        }

        public void Capture(string path) { Open(path, false); }
        public void Stop() { Close(); }
        public void CaptureStop() { Close(); }
        public string Status()
        {
            return string.Format("ch1: en {0} ten {1} tsel {2} dma {3} odt 0x{4:X3} rate {5:F1}; ch2: en {6} ten {7} tsel {8} dma {9} odt 0x{10:X3} rate {11:F1}; " +
                "dma ch {12} ring 0x{13:X8} {14}; capture {15} ({16} samples), rf tx {17}, captures {18}",
                En(0), Ten(0), Tsel(0), DmaEn(0), dor[0], rate[0], En(1), Ten(1), Tsel(1), DmaEn(1), dor[1], rate[1],
                dmaCh[1], ringAddr[1], ringLen[1], capFile ?? "off", capCount, RfTx(), txIndex);
        }

        private uint Reg(long o) { uint v; return regs.TryGetValue(o, out v) ? v : 0; }
        private static uint Dhr(long off, uint v)
        {
            if(off == 0x08) { return v & 0xFFF; }
            if(off == 0x0C) { return (v >> 4) & 0xFFF; }
            return (v & 0xFF) << 4;
        }
        private bool En(int c) { return (Reg(0) & (1u << (16 * c))) != 0; }
        private bool Ten(int c) { return (Reg(0) & (4u << (16 * c))) != 0; }
        private uint Tsel(int c) { return (Reg(0) >> (3 + 16 * c)) & 7; }
        private bool DmaEn(int c) { return (Reg(0) & (1u << (12 + 16 * c))) != 0; }

        private void Loaded(int c)
        {
            if(!Ten(c) || !En(c))
            {
                dor[c] = dhr[c];
                if(c == CaptureChannel - 1) { Record(dor[c]); }
            }
        }

        private void Trigger(int c)
        {
            if(DmaEn(c)) { DmaFetch(c); }
            dor[c] = dhr[c];
            if(c == CaptureChannel - 1) { Record(dor[c]); }
        }

        private uint TimerFor(int c)
        {
            switch(Tsel(c))
            {
            case 0: return 0x40001000;   // TMR6
            case 1: return 0x40013400;   // TMR8
            case 2: return 0x40001400;   // TMR7
            case 3: return 0x40000C00;   // TMR5
            case 4: return 0x40000000;   // TMR2
            case 5: return 0x40000800;   // TMR4
            default: return 0;
            }
        }

        private void Tick()
        {
            var bus = machine.SystemBus;
            for(int c = 0; c < 2; c++)
            {
                double r = 0;
                uint tim = TimerFor(c);
                if(En(c) && Ten(c) && tim != 0 && (bus.ReadDoubleWord(tim) & 1) != 0)
                {
                    double div = (bus.ReadDoubleWord(tim + 0x28) & 0xFFFF) + 1;
                    double pr = (bus.ReadDoubleWord(tim + 0x2C) & 0xFFFF) + 1.0;
                    r = TimerClockHz / (div * pr);
                    if(r > 2e6) { r = 0; }
                }
                if(Math.Abs(r - rate[c]) > 0.01)
                {
                    if(rate[c] > 0) { machine.ClockSource.TryRemoveClockEntry(c == 0 ? (Action)Trig0 : Trig1); }
                    rate[c] = r;
                    dmaCh[c] = -1;
                    if(r > 0) { machine.ClockSource.AddClockEntry(new ClockEntry(1, (ulong)Math.Round(r), c == 0 ? (Action)Trig0 : Trig1, this, "dac168-trg" + c)); }
                }
            }
            if(AutoCapture)
            {
                bool tx = RfTx();
                if(tx && !txOn) { txOn = true; txIndex++; StartAuto(); }
                else if(!tx && txOn) { txOn = false; if(autoFile) { Close(); } }
            }
        }

        private void Trig0() { Trigger(0); }
        private void Trig1() { Trigger(1); }

        private void DmaFetch(int c)
        {
            var bus = machine.SystemBus;
            if(dmaCh[c] < 0) { FindDma(c); if(dmaCh[c] < 0) { return; } }
            ulong b = 0x40020408 + 0x14 * (ulong)(dmaCh[c] - 1);
            uint v = bus.ReadWord(ringAddr[c] + (ulong)(2 * ringPos[c]));
            ringPos[c]++;
            if(ringPos[c] >= ringLen[c]) { ringPos[c] = 0; }
            uint par = bus.ReadDoubleWord(b + 8);
            dhr[c] = Dhr(par - 0x40007400 - (c == 1 ? 0x0Cu : 0u), v);
            bus.WriteDoubleWord(b + 4, (uint)(ringLen[c] - ringPos[c]));
        }

        private void FindDma(int c)
        {
            var bus = machine.SystemBus;
            uint lo = c == 0 ? 0x40007408u : 0x40007414u;
            for(int ch = 1; ch <= 5; ch++)
            {
                ulong b = 0x40020408 + 0x14 * (ulong)(ch - 1);
                uint par = bus.ReadDoubleWord(b + 8);
                if(par < lo || par > lo + 8) { continue; }     // CCR.EN does not read back in Renode's DMA model
                dmaCh[c] = ch;
                ringAddr[c] = bus.ReadDoubleWord(b + 0xC);
                ringLen[c] = Math.Max(1, (int)(bus.ReadDoubleWord(b + 4) & 0xFFFF));
                ringPos[c] = 0;
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

        private void Record(uint v)
        {
            if(cap == null) { return; }
            cap.Write((ushort)v);
            capCount++;
        }

        private void Close()
        {
            if(cap == null) { return; }
            cap.Close();
            cap = null;
            string json = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{{\"file\": \"{0}\", \"samples\": {1}, \"rate\": {2:F1}, \"duration_s\": {3:F4}, \"start_s\": {4:F4}, " +
                "\"rf_tx_reg_hz\": {5}, \"apc_odt1\": \"0x{6:X3}\", \"capture_channel\": {7}, \"auto\": {8}}}\n",
                Path.GetFileName(capFile), capCount, rate[CaptureChannel - 1], Now() - capStart, capStart, capHz, dor[0],
                CaptureChannel, autoFile ? "true" : "false");
            File.WriteAllText(Path.ChangeExtension(capFile, ".json"), json);
            capFile = null;
        }

        private double Now() { return machine.ElapsedVirtualTime.TimeElapsed.TotalSeconds; }

        private readonly IMachine machine;
        private readonly Dictionary<long, uint> regs = new Dictionary<long, uint>();
        private readonly uint[] dhr = new uint[2], dor = new uint[2];
        private readonly double[] rate = new double[2];
        private readonly int[] dmaCh = { -1, -1 }, ringLen = new int[2], ringPos = new int[2];
        private readonly ulong[] ringAddr = new ulong[2];
        private BinaryWriter cap;
        private string capFile;
        private bool autoFile, txOn;
        private long capCount, capHz;
        private int txIndex;
        private double capStart;
    }
}
