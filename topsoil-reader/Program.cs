using System;
using System.Collections;
using System.IO;
using System.Threading;
using Controller;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using Microsoft.SPOT.Net.NetworkInformation;
using SecretLabs.NETMF.Hardware.Netduino;
using Toolbox.NETMF;
using Toolbox.NETMF.Hardware.GSXSPI;
using Toolbox.NETMF.NET;
using System.Text;

namespace seedcoworking.topsoilreader
{
    public class Program
    {
        private static FileStream DebugLogFile;
        //GPIO
        private static OutputPort DoorStrike = new OutputPort(Pins.GPIO_PIN_D8, false);
        //Locks
        private static Object Cards_Lock = new Object();
        private static Object WData_Lock = new Object();
        private static Object UpdateUsers_Lock = new Object();
        //
        private static int rfidBitIndex = 0;
        private static int rfidBitCount = 0;
        private static int rfidLastBitCount = 0;
        private static bool[] rfidBits = new bool[83];//80 + parity[2] + overflow[1]
        private static DateTime startBits = new DateTime();
        private static DateTime stopBits = new DateTime();
        public static WiFlyGSX_SPI WifiModule;
        private static TimerCallback UpdateUsersDelegate = new TimerCallback(UpdateUsers_Callback);

        private static Hashtable Hashes = new Hashtable();
        private static Hashtable Cards = new Hashtable();
        private static Hashtable Plans = new Hashtable();
        private static Hashtable DaysOfWeek = new Hashtable();

        private static Config Cfg = new Config();
        public int iii;
        private static bool connected = true;
        public static void Main()
        {
            try
            {
                if (!Directory.Exists(@"\SD\logs\")) Directory.CreateDirectory(@"\SD\logs\");
                if (!Directory.Exists(@"\SD\logs\debug\")) Directory.CreateDirectory(@"\SD\logs\debug\");
                var now = DateTime.Now;
                string debug_file_name = now.Year.ToString() + '_' + now.Month + '_' + now.Day + '_' + now.Hour + '_' + now.Minute + ".log";
                DebugLogFile = File.Create(@"\SD\logs\debug\" + debug_file_name);
                //throw new Exception("Main() Exception ");

                //Because enum.Parse() does not exist in NETMF... yet
                DaysOfWeek.Add("Sunday", 0);
                DaysOfWeek.Add("Monday", 1);
                DaysOfWeek.Add("Tuesday", 2);
                DaysOfWeek.Add("Wednesday", 3);
                DaysOfWeek.Add("Thursday", 4);
                DaysOfWeek.Add("Friday", 5);
                DaysOfWeek.Add("Saturday", 6);

                //Eventually this line will return an interface to the WiFly
                //var ni = NetworkInterface.GetAllNetworkInterfaces();
                WifiModule = new WiFlyGSX_SPI(WiFlyGSX_SPI.DeviceType.crystal_14_MHz, SPI.SPI_module.SPI1,
                                                SecretLabs.NETMF.Hardware.Netduino.Pins.GPIO_PIN_D10, "$", true, 500);
                //need to add this to raspi api
                string ip = WifiModule.DnsLookup("nist1-pa.ustiming.org");
                DateTime n = new DateTime(1900, 1, 1).AddSeconds(WifiModule.NtpLookup(ip) - 14400);
                Utility.SetLocalTime(n.ToLocalTime());
                now = DateTime.Now;
                WriteDebug(now.ToString());
                //open nelog file with correct datetime
                debug_file_name = now.Year.ToString() + '_' + now.Month + '_' + now.Day + '_' + now.Hour + '_' + now.Minute + ".log";
                var tempfile = DebugLogFile;
                DebugLogFile = File.Create(@"\SD\logs\debug\" + debug_file_name);
                tempfile.Seek(0, SeekOrigin.Begin);
                byte[] b = new byte[tempfile.Length];
                tempfile.Read(b,0,(int)tempfile.Length);
                tempfile.Dispose();
                DebugLogFile.Write(b,0,b.Length);
               



                Timer WDataTimer = new Timer(UpdateUsersDelegate, null, 0, Timeout.Infinite);

                // initialize the Data0 input
                InterruptPort Data0 = new InterruptPort(Pins.GPIO_PIN_D2, true,
                                                             Port.ResistorMode.PullUp,
                                                             Port.InterruptMode.InterruptEdgeHigh);
                Data0.OnInterrupt += new NativeEventHandler(WData_OnInterrupt);
                // initialize the Data1 input
                InterruptPort Data1 = new InterruptPort(Pins.GPIO_PIN_D3, true,
                                                             Port.ResistorMode.PullUp,
                                                             Port.InterruptMode.InterruptEdgeHigh);
                Data1.OnInterrupt += new NativeEventHandler(WData_OnInterrupt);

                // wait forever...
                Thread.Sleep(Timeout.Infinite);
            }
            catch(Exception ex)
            {
                WriteDebug("Global Exception");
                WriteDebug("Message:" + ex.Message + "\n"
                        + "InnerException: " + ex.InnerException + "\n"
                        + "Stack Trace:\n" + ex.StackTrace);
                DebugLogFile.Flush();
                DebugLogFile.Close();
                DebugLogFile.Dispose();
                PowerState.RebootDevice(false);
            }
    }

        
        private static void WData_OnInterrupt(uint port, uint data, DateTime time)
        {
            lock (WData_Lock)
            {
                if (rfidBitIndex <= 0)
                {
                    rfidLastBitCount = rfidBitCount = rfidBitIndex = 1;
                    startBits = DateTime.Now;
                    TimerCallback WDataDelegate = new TimerCallback(WData_Callback);
                    Timer WDataTimer = new Timer(WDataDelegate, null, 50, 0);
                }
                else if (rfidBitIndex < rfidBits.Length)
                {
                    if (port == (uint)Pins.GPIO_PIN_D3) rfidBits[rfidBitIndex] = true;
                    else rfidBits[rfidBitIndex] = false;
                    rfidBitCount++;
                    rfidBitIndex++;
                    stopBits = DateTime.Now;
                }
                else rfidBitCount++;
            }
        }

        private static void WData_Callback(Object StateInfo)
        {
            lock (WData_Lock)
            {
                //WriteDebug("Bits: " + rfidBitIndex + "   LastBitCount: " + rfidLastBitCount);
                if (rfidBitCount <= 1 || rfidBitCount != rfidLastBitCount)
                {
                    rfidLastBitCount = rfidBitCount;
                    TimerCallback WDataDelegate = new TimerCallback(WData_Callback);
                    Timer WDataTimer = new Timer(WDataDelegate, null, 20, 0);
                    return;
                }
                //for (int i = 0; i < rfidBitIndex; i++)
                //{
                    //WriteDebug(rfidBits[i].ToString());
                //}
                Int64 rfidNum = 0;
                byte[] rfidBytes = new byte[(rfidBitIndex - 2 + 7) / 8];
                for(int i =0; i<rfidBytes.Length;i++){rfidBytes[i]=0;}
                int notBits = (8-(rfidBitIndex-2)%8)%8;
                for (int i = 1; i < rfidBitIndex-1; i ++)
                {
                    int b = (i-1 + notBits) / 8;
                    rfidNum <<= 1;
                    rfidBytes[b] <<= 1;
                    if (rfidBits[i])
                    {
                        rfidNum++;
                        rfidBytes[b]++;
                    }

                }
                string rfid = rfidNum.ToString();
                char[] hex = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
                rfid = "";
                int maxNib = rfidBitIndex / 4;
                for (int i = 0; i < maxNib; i += 2)
                {
                    int hi = (rfidBytes[i / 2] & 0xF0) >> 4;
                    int lo = rfidBytes[i / 2] & 0x0F;
                    rfid += hex[hi].ToString();
                    if (i < maxNib - 1) rfid += hex[lo].ToString();
                }
                WriteDebug("Start: " + startBits.Second + ":" + startBits.Millisecond);
                WriteDebug("Recieved: " + rfid);
                WriteDebug("Recieved: " + rfidBitIndex + " bits");
                WriteDebug("Stop: " + stopBits.Second + ":" + stopBits.Millisecond);
                lock (Cards_Lock)
                {
                    if (rfid == Cfg.hardcode) UnlockDoor();
                    else if (Cards != null && Cards.Contains(rfid))
                    {
                        var n = DateTime.Now;
                        var c = (RFIDCard)Cards[rfid];
                        if (c.plan != null && c.plan.days != null && c.plan.days.Contains(n.DayOfWeek))
                        {
                            var d = (Day)c.plan.days[n.DayOfWeek];
                            if (n.TimeOfDay > d.start.TimeOfDay && n.TimeOfDay < d.end.TimeOfDay)
                            {
                                WriteDebug("OPEN");
                                UnlockDoor();
                            }
                            else WriteDebug("Not Scheduled");
                        }
                        else WriteDebug("Not Scheduled");
                    }
                    else
                    {
                        WriteDebug("Not Valid\r\nKeys:{");
                        foreach (var k in Cards.Keys) WriteDebug(k.ToString());
                        WriteDebug("}");
                    }
                    rfidBitIndex = 0;
                }
            }
        }

        private static void UpdateUsers_Callback(Object StateInfo)
        {
            //return;
            lock (UpdateUsers_Lock)
            {
                string src = Cfg.users_door_root;
                string dst = Cfg.datapath;
                string auth = Cfg.users_door_login + ":" + Cfg.users_door_password;
                if (!Directory.Exists(dst)) Directory.CreateDirectory(dst);

                SimpleSocket Socket = new WiFlySocket(Cfg.users_host, Cfg.users_host_port, WifiModule);
                
                if(connected) connected = DownloadJson(Socket, src + Cfg.users_door_get, dst + "hashes.json", auth);
                if(!File.Exists(dst + "hashes.json"))return;
                
                WriteDebug("Memory: " + Debug.GC(false).ToString());
                Hashtable hashes = null;
                using (FileStream jfs = new FileStream(dst + "hashes.json", FileMode.Open, FileAccess.Read, FileShare.None, 2048))
                {
                    hashes = (Hashtable)JSON.JsonDecode(jfs);
                }
                WriteDebug("Memory: " + Debug.GC(true).ToString());
                if (hashes == null || hashes.Count == 0) return;
                Hashtable cards = null;
                if (hashes.Contains("cards") && (!Hashes.Contains("cards") || ((double)Hashes["cards"]) != (double)hashes["cards"]))
                {
                    if (connected) connected = DownloadJson(Socket, src + "cards", dst + "cards.json", auth);
                    if (File.Exists(dst + "cards.json"))
                    {
                        Hashes["cards"] = hashes["cards"];
                    }
                    ArrayList cardarr = null;
                    using (FileStream jfs = new FileStream(dst + "cards.json", FileMode.Open, FileAccess.Read, FileShare.None, 2048))
                    {
                        cardarr = (ArrayList)JSON.JsonDecode(jfs);
                    }
                    WriteDebug("Memory: " + Debug.GC(true).ToString());
                    if (cardarr == null)cardarr = new ArrayList();
                    cards = new Hashtable();
                    if (cardarr.Count > 0)
                    {
                        Plan no_plan = new Plan();
                        for (int i = 0; i < cardarr.Count; i++)
                        {
                            var c = new RFIDCard();
                            c.number = ((Hashtable)cardarr[i])["identifier"].ToString();
                            c.plan_id = (int)System.Math.Round((double)((Hashtable)cardarr[i])["plan_id"]);
                            cards[c.number] = c;
                        }
                    }
                }
                Hashtable plans = null;
                if(hashes.Contains("plans"))
                {
                    plans = new Hashtable();
                    if (!Hashes.Contains("plans"))Hashes["plans"] = new Hashtable();
                    Hashtable skippedKeys = new Hashtable();
                    foreach (var h in ((Hashtable)hashes["plans"]).Keys)
                    {
                        if (((Hashtable)Hashes["plans"]).Contains(h)
                            && (double)((Hashtable)Hashes["plans"])[h] == (double)((Hashtable)hashes["plans"])[h]
                            && Plans.Contains(h))
                        {
                            plans.Add(h, Plans[h]);
                            continue;
                        }
                        //download
                        if(connected) connected=DownloadJson(Socket, src + h, dst + h + ".json", auth);
                        if (!File.Exists(dst + h + ".json"))
                        {
                            //download failed - keep existing if it exists.
                            if (Plans.Contains(h)) plans.Add(h, Plans[h]);
                            else skippedKeys.Add(h, h);
                            continue;
                        }
                        //decode
                        Hashtable plantbl = null;
                        using (FileStream jfs = new FileStream(dst + h + ".json", FileMode.Open, FileAccess.Read, FileShare.None, 2048))
                        {
                            plantbl = (Hashtable)JSON.JsonDecode(jfs);
                        }
                        WriteDebug("Memory: " + Debug.GC(true).ToString());
                        if (plantbl == null)
                        {
                            //json decode failed - keep existing if it exists.
                            if (Plans.Contains(h)) plans.Add(h, Plans[h]);
                            else skippedKeys.Add(h, h);
                            continue;
                        }
                        //convert to Plan class
                        Plan p = new Plan();
                        p.id = (int)System.Math.Round((double)plantbl["id"]);
                        p.name = (string)plantbl["name"];
                        p.days = new Hashtable();
                        //add to plans
                        ArrayList days = null;
                        if (plantbl.Contains("schedule")) days = (ArrayList)plantbl["schedule"];
                        else days = new ArrayList();
                        if (days != null)
                        {
                            for (int d = 0, k = days.Count; d < k; d++)
                            {
                                Hashtable dy = (Hashtable)days[d];
                                Day day = new Day();
                                string s;
                                if (!dy.Contains("name")) continue;
                                day.name = (string)dy["name"];
                                if (!DaysOfWeek.Contains(day.name)) continue;
                                if (!dy.Contains("start_time")) continue;
                                s = (string)dy["start_time"];
                                day.start = day.ParseDateTime(s, (int)DaysOfWeek[day.name]);
                                var dow = day.start.DayOfWeek;
                                if (!dy.Contains("end_time")) continue;
                                s = (string)dy["end_time"];
                                day.end = day.ParseDateTime(s, (int)DaysOfWeek[day.name]);
                                WriteDebug(day.name + " " + day.start.ToString() + " " + day.end.ToString());
                                p.days.Add(day.start.DayOfWeek, day);
                            }
                        }
                        plans.Add(h, p);
                    }
                    //remove skipped keys from new hashes
                    foreach (var k in skippedKeys)
                    {
                        ((Hashtable)hashes["plans"]).Remove(k);
                    }
                    Hashes["plans"] = hashes["plans"];
                }
                lock (Cards_Lock)
                {
                    if(cards != null)Cards = cards;
                    if(plans != null)Plans = plans;
                    foreach (RFIDCard c in Cards.Values)
                    {
                        //Need to try microLinq if its not a resource pig
                        foreach (Plan p in Plans.Values)
                        {
                            Plan pl = p;
                            RFIDCard cd = c;
                            if (pl.id == cd.plan_id)
                            {
                                cd.plan = (Plan)pl;
                                break;
                            }
                        }
                    }
                }
            }
        }

        private static bool DownloadJson(SimpleSocket Socket, string src, string dst, string auth)
        {
            try { Socket.Connect(); }
            catch
            {
                //Timer WDataTimer = new Timer(UpdateUsersDelegate, null, 150, 0);
                return false;
            }
            // Does a plain HTTP request
            string hdr = Tools.Base64Encode(auth);
            Socket.Send("GET /" + src + " HTTP/1.1\r\n");
            Socket.Send("Authorization: Basic " + hdr + "\r\n");
            Socket.Send("Host: " + Socket.Hostname + "\r\n");
            Socket.Send("Connection: Close\r\n");
            Socket.Send("\r\n");

            WriteDebug("Memory: " + Debug.GC(false).ToString());

            //Writes all received data to dst, until the connection is terminated and there's no data left anymore
            using (FileStream outf = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None, 2048))
            {
                int j = -1;
                while (Socket.IsConnected || Socket.BytesAvailable > 0)
                {
                    string json = Socket.Receive();
                    if (json != "")
                    {
                        if (j < 0 && (j = json.IndexOf("\r\n\r\n")) >= 0)j += ("\r\n\r\n").Length;
                        if (j >= 0)
                        {
                            if (j > 0)
                            {
                                int i = json.IndexOf('[', j);
                                int k = json.IndexOf('{', j);
                                if (k < i || i < 0) i = k;
                                if (i > j) j = i;
                            }
                            byte[] b = WifiModule.getBytes(json.Substring(j));
                            outf.Write(b, 0, b.Length);
                            j = 0;
                        }
                    }
                }
            }

            return true;
        }

        private static void UnlockDoor()
        {
            DoorStrike.Write(true);
            Thread.Sleep(5000);
            DoorStrike.Write(false);
        }

        public static void WriteDebug(string s = "")
        {
            Debug.Print(s);
            byte[] b = Encoding.UTF8.GetBytes(DateTime.Now.ToString() + DateTime.Now.Millisecond + " - ");
            DebugLogFile.Write(b, 0, b.Length);
            b = Encoding.UTF8.GetBytes(s);
            DebugLogFile.Write(b, 0, b.Length);
            b = Encoding.UTF8.GetBytes("\r\n");
            DebugLogFile.Write(b, 0, b.Length);
            DebugLogFile.Flush();
        }

    }
}

