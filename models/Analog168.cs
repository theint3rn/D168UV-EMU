// D168UV analog front end: AT32F403A ADC1 (F1 register layout) + the DMA1 channel 1 transfer into RAM, plus an
// audio sample-stream injector. Port of the D890UV model (D890UV_src/emu/models/Analog.cs) to the F1 ADC/DMA.
// cfw overlay model (emu/cfw168.repl, run.py --cfw); also serves the stock firmware's scan.
//
// Stock V1.07 FUN_0803c8a4: ch10 (PC0), ch11 (PC1 battery), ch12 (PC2) continuous scan, DMA1 ch1 -> 0x200017cc.
// cfw (D168UV/cfw/drivers/adc.c): the same three channels, one scan per TMR3 TRGO at 48 kHz (CTRL2 OCTEN, OCTESEL
// 100), DMA1 ch1 circular ring; the firmware reads its write position from CNDTR1, which this model keeps.
// The DMA controller is Renode's plain model and the ADC raises no request, so this model writes the samples
// itself: it finds the DMA1 channel whose CPAR = ADC1 ODT (0x4001244C), writes into CMAR + 2*pos and updates CNDTR.
//
// Values: slot of ch10 = discriminator / audio (stream or Set), ch11 = battery, ch12 = aux.
// Stream: .raw int16 LE (rate given) / .wav PCM16 / .adc16 (12-bit counts as the ADC reads them, e.g. a TX
// capture from dac Capture or the D890UV cfw streams). value = 2048 + s*gain/16 (.raw/.wav) or 2048 + (c-2048)*gain.
// Monitor (device "analog"):
//   analog Set 10 2600   (ADC channel, raw)   analog Battery 7.6   analog Values   analog Status
//   analog Stream @file.adc16 48000   analog StreamLoop true   analog StreamStop   analog StreamGain 0.5
//   analog StreamGate true   (stream only while rf0 sees a carrier on its tuned frequency, else noise)
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class D168Analog : IDoubleWordPeripheral
    {
        public D168Analog(IMachine machine)
        {
            this.machine = machine;
            values[10] = 2048; values[11] = 2400; values[12] = 2048;     // battery ~7.7 V (cal 100)
            values[16] = 1750; values[17] = 1490;                          // temperature sensor / VINTRV
            machine.ClockSource.AddClockEntry(new ClockEntry(1, 1000, Tick, this, "analog168"));
        }

        public long Size => 0x400;
        public double TimerClockHz { get; set; } = 120000000;
        public uint TriggerTimer { get; set; } = 0;
        public double StreamGain { get; set; } = 1.0;
        public bool StreamLoop { get; set; }
        public bool Loop { get { return StreamLoop; } set { StreamLoop = value; } }
        public bool StreamGate { get; set; }
        public int DiscChannel { get; set; } = 10;

        public void Reset() { regs.Clear(); }

        public uint ReadDoubleWord(long offset)
        {
            uint v; regs.TryGetValue(offset, out v);
            switch(offset)
            {
            case 0x00: return v | 0x12;                                  // OCCE + OCCS: conversions always done
            case 0x08: return v & ~0xCu;                                 // ADCAL / ADCALINIT finish at once
            case 0x4C: return values[FirstChannel()];
            default: return v;
            }
        }

        public void WriteDoubleWord(long offset, uint value) { regs[offset] = value; }

        public void Set(int channel, int raw) { if(channel >= 0 && channel < 18) { values[channel] = (ushort)(raw & 0xFFF); } }
        public void Battery(double volts) { values[11] = (ushort)Math.Min(4095, Math.Round(volts * 100 * 4096 / 0x528)); }
        public string Values() { return string.Join(" ", Enumerable.Range(10, 3).Select(c => "ch" + c + "=" + values[c])); }

        public void Stream(string path, int rate)
        {
            var b = File.ReadAllBytes(path);
            counts = path.EndsWith(".adc16");
            int start = 0, step = 2;
            if(b.Length > 44 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F')
            {
                int p = 12, ch = 1; start = -1;
                while(p + 8 <= b.Length)
                {
                    string id = System.Text.Encoding.ASCII.GetString(b, p, 4);
                    int len = BitConverter.ToInt32(b, p + 4);
                    if(id == "fmt ") { ch = BitConverter.ToInt16(b, p + 10); rate = BitConverter.ToInt32(b, p + 12); }
                    else if(id == "data") { start = p + 8; break; }
                    p += 8 + len + (len & 1);
                }
                if(start < 0) { throw new ArgumentException("wav: no data chunk"); }
                step = 2 * ch;
            }
            int n = (b.Length - start) / step;
            stream = new short[n];
            for(int i = 0; i < n; i++) { stream[i] = BitConverter.ToInt16(b, start + i * step); }
            srcRate = Math.Max(1, rate);
            t0 = Now();
            frame0 = fed;
            streaming = n > 0;
        }

        public void StreamStop() { streaming = false; }

        public string Status() { return StreamStatus(); }
        public string StreamStatus()
        {
            return string.Format("{0}: {1} samples at {2} Hz, loop {3}, gain {4}, gate {5}; mode {6}, rate {7:F1} Hz, scan [{8}], " +
                "dma ch {9} buf 0x{10:X8} {11} halfwords, frames {12}; {13}",
                streaming ? "playing" : "idle", stream.Length, srcRate, StreamLoop, StreamGain, StreamGate,
                triggered ? "timer-triggered" : "free-running", fwRate, string.Join(",", scan), dmaCh, bufAddr, bufLen, fed, Values());
        }

        // ---------------- internals ----------------
        private readonly Random rng = new Random(7);
        private double Now() { return machine.ElapsedVirtualTime.TimeElapsed.TotalSeconds; }
        private uint Reg(long o) { uint v; return regs.TryGetValue(o, out v) ? v : 0; }

        private int FirstChannel() { int c = (int)(Reg(0x34) & 0x1F); return c < 18 ? c : 0; }

        private bool RfCarrier()
        {
            IPeripheral p;
            if(!machine.TryGetByName("sysbus.rf0", out p)) { return true; }
            var rf = p as D890RfTransceiver;
            return rf == null || rf.Carrier;
        }

        private ushort Disc(double t)
        {
            if(!streaming) { return values[DiscChannel]; }
            if(StreamGate && !RfCarrier()) { return (ushort)(2048 + rng.Next(-400, 401)); }
            double x = (t - t0) * srcRate;
            if(x < 0) { x = 0; }
            int n = stream.Length;
            if(x >= n - 1)
            {
                if(!StreamLoop) { streaming = false; return values[DiscChannel]; }
                x = x % n;
            }
            int i = (int)x;
            double f = x - i;
            double s = stream[i] * (1 - f) + stream[(i + 1) % n] * f;
            double v = counts ? 2048 + (s - 2048) * StreamGain : 2048 + s * StreamGain / 16;
            return (ushort)Math.Max(0, Math.Min(4095, Math.Round(v)));
        }

        private void ReadScan()
        {
            int len = (int)((Reg(0x2C) >> 20) & 0xF) + 1;
            ulong sq = Reg(0x34) | ((ulong)Reg(0x30) << 30) | ((ulong)(Reg(0x2C) & 0xFFFFF) << 60);
            var l = new List<int>();
            for(int r = 0; r < len; r++)
            {
                uint w = r < 6 ? Reg(0x34) >> (5 * r) : r < 12 ? Reg(0x30) >> (5 * (r - 6)) : Reg(0x2C) >> (5 * (r - 12));
                l.Add((int)(w & 0x1F));
            }
            scan = l.ToArray();
        }

        private int FindDma(out ulong cmar, out int cndtr)
        {
            var bus = machine.SystemBus;
            cmar = 0; cndtr = 0;
            for(int ch = 1; ch <= 7; ch++)
            {
                ulong b = 0x40020008 + 0x14 * (ulong)(ch - 1);
                if(bus.ReadDoubleWord(b + 8) != 0x4001244C) { continue; }
                // (Renode's DMA model does not read back CCR.EN, so the channel is matched by CPAR alone)
                cmar = bus.ReadDoubleWord(b + 0xC);
                cndtr = (int)(bus.ReadDoubleWord(b + 4) & 0xFFFF);
                return ch;
            }
            return -1;
        }

        // 1 ms: configuration; free-running scans
        private void Tick()
        {
            var bus = machine.SystemBus;
            uint ctrl2 = Reg(0x08);
            ReadScan();
            bool on = (ctrl2 & 1) != 0 && (ctrl2 & (1u << 8)) != 0;
            ulong cmar = 0; int cndtr = 0;
            int ch = on ? FindDma(out cmar, out cndtr) : -1;
            bool wasTrig = triggered;
            double oldRate = fwRate;
            triggered = false; fwRate = 0;
            if(ch > 0 && (ctrl2 & (1u << 20)) != 0)
            {
                uint tim = TriggerTimer;
                if(tim == 0 && ((ctrl2 >> 17) & 7) == 4 && (ctrl2 & (1u << 25)) == 0) { tim = 0x40000400; }   // TMR3 TRGO
                if(tim != 0 && (bus.ReadDoubleWord(tim) & 1) != 0)
                {
                    double div = (bus.ReadDoubleWord(tim + 0x28) & 0xFFFF) + 1;
                    double pr = (bus.ReadDoubleWord(tim + 0x2C) & 0xFFFF) + 1;
                    fwRate = TimerClockHz / (div * pr);
                    triggered = fwRate > 0 && fwRate <= 1e6;
                }
            }
            if(triggered && (!wasTrig || ch != dmaCh || cmar != bufAddr)) { dmaCh = ch; bufAddr = cmar; bufLen = Math.Max(scan.Length, cndtr); pos = 0; }
            if(triggered != wasTrig || Math.Abs(oldRate - fwRate) > 0.01)
            {
                if(entryHz > 0) { machine.ClockSource.TryRemoveClockEntry(Sample); entryHz = 0; }
                if(triggered) { entryHz = (long)Math.Round(fwRate); machine.ClockSource.AddClockEntry(new ClockEntry(1, (ulong)entryHz, Sample, this, "analog168-trg")); }
            }
            if(triggered || ch <= 0) { return; }
            // free-running (stock): current values into the DMA buffer, one scan
            for(int i = 0; i < scan.Length; i++)
            {
                int c = scan[i];
                bus.WriteWord(cmar + (ulong)(2 * i), c == DiscChannel ? Disc(Now()) : values[c]);
            }
        }

        // one scan frame per trigger; the stream is played by frame count (exact firmware rate)
        private void Sample()
        {
            var bus = machine.SystemBus;
            if(dmaCh <= 0) { return; }
            ushort d = Disc(t0 + (fed - frame0) / fwRate);
            for(int i = 0; i < scan.Length; i++)
            {
                int c = scan[i];
                bus.WriteWord(bufAddr + (ulong)(2 * pos), c == DiscChannel ? d : values[c]);
                pos++;
                if(pos >= bufLen) { pos = 0; }
            }
            fed++;
            ulong b = 0x40020008 + 0x14 * (ulong)(dmaCh - 1);
            bus.WriteDoubleWord(b + 4, (uint)(bufLen - pos));            // CNDTR = halfwords left
        }

        private readonly IMachine machine;
        private readonly Dictionary<long, uint> regs = new Dictionary<long, uint>();
        private readonly ushort[] values = new ushort[18];
        private int[] scan = new int[0];
        private short[] stream = new short[0];
        private double srcRate = 48000, t0, fwRate;
        private bool streaming, triggered, counts;
        private long frame0, fed, entryHz;
        private ulong bufAddr;
        private int dmaCh = -1, bufLen, pos;
    }
}
