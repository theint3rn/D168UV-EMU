// Bluetooth module of the D168UV on UART4 (behavioural; AT command set, lines "AT+...\r\n").
// MCU side (stock V1.07): sender FUN_0800fd30 ("AT+" cmd CR LF), command + wait FUN_0800fe3c(id, cmd, len, want)
// (5 tries x 50 x 5 ms, waits for reply table index `want`), UART4 RX ring 0x200149f8 (IRQ52_Handler) drained by
// FUN_08012c00 -> line parser FUN_0800456c / FUN_08004d88 -> dispatcher FUN_0800b024. Reply table (0x0801c548):
//   1 OK  2 ERR  3 +CONN  4 +SCAN  5 +AUDIO  6 +DISC  7 +PTTKEY  8 +PIN  9 +VER  10 +DNAME  11 +PTTBATT
//   12 +HFREQ:1  13 +HFREQ:0  14 +ADATA:  15 +DATA:00,  16 +AGHF:  17 +READY
// Boot probe FUN_08005b94: AT+VER? (want +VER; no answer = module absent), then AGHF? PIN? DNAME? CONN? SPKEN=0.
// Field offsets used by the dispatcher: +VER:<ver> (text from offset 5), +PIN:<pin> (6 bytes from 5),
// +DNAME:<name> (from 7), +CONN:<12 hex MAC>,<name> (MAC at 6..17, all '0' = not connected; name at 0x13),
// +SCAN:<12 hex MAC>,<rssi>,<name> (one line per device, name after the last ','), +AGHF:<0|1> (offset 6).
// Monitor: bt Present false   bt Line "+CONN:112233445566,Headset"   bt Devices "112233445566,Headset;..."
// Lines both ways are logged to $D168_OUT_DIR/bt.log.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class D168Bluetooth : IDoubleWordPeripheral
    {
        public D168Bluetooth(IMachine machine, string uart = "sysbus.usart4")
        {
            this.machine = machine;
            uartName = uart;
            Present = true;
            Version = "JL_V1.0.7";
            Pin = "0000";
            Name = "D168UV";
            Devices = "A1B2C3D4E5F6,BT-Headset;0C1D2E3F4A5B,BT-PTT";
            var dir = Environment.GetEnvironmentVariable("D168_OUT_DIR");
            logPath = string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "bt.log");
            machine.ClockSource.AddClockEntry(new ClockEntry(1, 1000, Tick, this, "bt168"));
        }

        public long Size => 0x10;
        public bool Present { get; set; }
        public string Version { get; set; }
        public string Pin { get; set; }
        public string Name { get; set; }
        public string Devices { get; set; }       // "MAC,name;MAC,name" reported after AT+SCAN=1
        public bool Connected => connMac != null;

        public void Reset() { rx.Clear(); pending.Clear(); connMac = null; }
        public uint ReadDoubleWord(long offset) { return 0; }
        public void WriteDoubleWord(long offset, uint value) { }

        // raw line to the MCU (CR LF added)
        public void Line(string s) { Queue(s, 0); }

        private void Tick()
        {
            now++;
            if(uart == null)
            {
                IPeripheral p;
                if(machine.TryGetByName(uartName, out p)) { uart = p as IUART; if(uart != null) { uart.CharReceived += OnByte; } }
                return;
            }
            while(pending.Count > 0 && pending[0].Item1 <= now)
            {
                var l = pending[0].Item2;
                pending.RemoveAt(0);
                Log("BT < " + l);
                foreach(var b in Encoding.ASCII.GetBytes(l + "\r\n")) { uart.WriteChar(b); }
            }
        }

        private void Queue(string line, int delayMs)
        {
            long t = now + Math.Max(2, delayMs);
            if(pending.Count > 0 && pending[pending.Count - 1].Item1 > t) { t = pending[pending.Count - 1].Item1; }
            pending.Add(Tuple.Create(t, line));
        }

        private void OnByte(byte b)
        {
            if(b == '\n')
            {
                var line = Encoding.ASCII.GetString(rx.ToArray()).TrimEnd('\r');
                rx.Clear();
                if(line.Length > 0) { Command(line); }
                return;
            }
            if(rx.Count < 512) { rx.Add(b); }
        }

        private void Command(string line)
        {
            Log("MCU> " + line);
            if(!Present || !line.StartsWith("AT+")) { return; }
            var c = line.Substring(3);
            if(c == "VER?") { Queue("+VER:" + Version, 5); return; }
            if(c == "PIN?") { Queue("+PIN:" + Pin, 5); return; }
            if(c == "DNAME?") { Queue("+DNAME:" + Name, 5); return; }
            if(c == "AGHF?") { Queue("+AGHF:0", 5); return; }
            if(c == "CONN?") { Queue("+CONN:" + (connMac ?? "000000000000") + (connMac != null ? "," + connName : ""), 5); return; }
            if(c == "PTTBATT?") { Queue("+PTTBATT:100", 5); return; }
            if(c.StartsWith("PIN=")) { Pin = c.Substring(4); Queue("OK", 5); return; }
            if(c.StartsWith("DNAME=")) { Name = c.Substring(6); Queue("OK", 5); return; }
            Queue("OK", 5);
            if(c == "SCAN=1" || c == "SCAN=2")
            {
                int d = 300;
                foreach(var dev in (Devices ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = dev.Split(',');
                    Queue("+SCAN:" + f[0] + ",-60," + (f.Length > 1 ? f[1] : "BT"), d);
                    d += 300;
                }
            }
            else if(c.StartsWith("CONN=") && c.Length >= 17)
            {
                connMac = c.Substring(5, 12);
                connName = Lookup(connMac);
                Queue("+CONN:" + connMac + "," + connName, 500);
            }
            else if(c.StartsWith("DISC="))
            {
                connMac = null;
                Queue("+DISC:000000000000", 200);
            }
        }

        private string Lookup(string mac)
        {
            foreach(var dev in (Devices ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = dev.Split(',');
                if(f[0].Equals(mac, StringComparison.OrdinalIgnoreCase) && f.Length > 1) { return f[1]; }
            }
            return "BT";
        }

        private void Log(string s)
        {
            if(string.IsNullOrEmpty(logPath)) { return; }
            try { File.AppendAllText(logPath, string.Format("{0,8} ms {1}\n", now, s)); } catch(Exception) { }
        }

        private readonly IMachine machine;
        private readonly string uartName;
        private readonly string logPath;
        private readonly List<byte> rx = new List<byte>();
        private readonly List<Tuple<long, string>> pending = new List<Tuple<long, string>>();
        private IUART uart;
        private long now;
        private string connMac, connName;
    }
}
