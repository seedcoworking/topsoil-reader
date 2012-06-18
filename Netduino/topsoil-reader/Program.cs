using System;
using System.Text;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using astra.http;
using netduino.helpers.Helpers;
using System.Collections;
using Controller;

namespace seedcoworking.topsoilreader
{
    public class Program
    {
        private static OutputPort DoorStrike = new OutputPort(Pins.GPIO_PIN_D4, false);
        private static OutputPort DoorStrikeLOW = new OutputPort(Pins.GPIO_PIN_D3, false);
        private static Object Cards_Lock = new Object();
        private static Object WData_Lock = new Object();
        private static int rfidBits = 0;
        private static byte[] rfidBytes = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private static DateTime startBits = new DateTime();
        private static DateTime stopBits = new DateTime();
        private static HttpImplementation webServer;

        public static Hashtable DaysOfWeek;
        
        private static Hashtable Cards;
        //private static Hashtable Schedules;
        //private static Queue Triggers;

        public static void Main()
        {
            DaysOfWeek = new Hashtable();
            DaysOfWeek.Add("Sunday",0);
            DaysOfWeek.Add("Monday",1);
            DaysOfWeek.Add("Tuesday",2);
            DaysOfWeek.Add("Wednesday",3);
            DaysOfWeek.Add("Thursday",4);
            DaysOfWeek.Add("Friday",5);
            DaysOfWeek.Add("Saturday",6);
            TimerCallback UpdateUsersDelegate = new TimerCallback(UpdateUsers_Callback);
            Timer WDataTimer = new Timer(UpdateUsersDelegate, null,0, 900000);
            
            // initialize the Data0 input
            InterruptPort Data0 = new InterruptPort(Pins.GPIO_PIN_D0, true,
                                                         Port.ResistorMode.PullUp,
                                                         Port.InterruptMode.InterruptEdgeHigh);
            Data0.OnInterrupt += new NativeEventHandler(WData_OnInterrupt);
            // initialize the Data1 input
            InterruptPort Data1 = new InterruptPort(Pins.GPIO_PIN_D1, true,
                                                         Port.ResistorMode.PullUp,
                                                         Port.InterruptMode.InterruptEdgeHigh);
            Data1.OnInterrupt += new NativeEventHandler(WData_OnInterrupt);


            //new Thread(webServerThread).Start();

            // wait forever...
            Thread.Sleep(Timeout.Infinite);
        }

        private static void webServerThread()
        {
            webServer = new HttpWiflyImpl(processRequest, 80, HttpWiflyImpl.DeviceType.crystal_14_MHz, SPI.SPI_module.SPI1, SecretLabs.NETMF.Hardware.Netduino.Pins.GPIO_PIN_D10);

            webServer.Listen();
        }

        private static void processRequest(HttpContext context)
        {
        }

        private static void WData_OnInterrupt(uint port, uint data, DateTime time)
        {
            if (rfidBits == 0)
            {
                
                rfidBits++;
                startBits = DateTime.Now;
                TimerCallback WDataDelegate = new TimerCallback(WData_Callback);
                Timer WDataTimer = new Timer(WDataDelegate, null, 100, 0);
            }
            else
            //lock (WData_Lock)
            {
                int rb = rfidBits - 1;
                if (port == (uint)Pins.GPIO_PIN_D1) rfidBytes[rb / 8] |= (byte)(128 >> (rb % 8));
                else rfidBytes[rb / 8] &= (byte)~(128 >> (rb % 8));
                rfidBits++;
                stopBits = DateTime.Now;
                //led.Write(true);
                //Debug.Print("port:" + port.ToString() + "  " + time.Millisecond.ToString() + "  " + rfidBits);
                //led.Write(false);
            }
        }

        private static void WData_Callback(Object StateInfo)
        {
            char[] hex = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
            string rfid = "";
            int maxNib = rfidBits / 4;
            for (int i = 0; i < maxNib; i += 2)
            {
                int hi = (rfidBytes[i / 2] & 0xF0) >> 4;
                int lo = rfidBytes[i / 2] & 0x0F;
                rfid += hex[hi].ToString();
                if (i < maxNib - 1) rfid += hex[lo].ToString();
            }
            Debug.Print("Start: " + startBits.Second+ ":" + startBits.Millisecond);
            Debug.Print("Recieved: " + rfid);
            Debug.Print("Recieved: " + rfidBits + " bits");
            Debug.Print("Stop: " + stopBits.Second + ":" + stopBits.Millisecond);

            lock (Cards_Lock)
            {
                if (Cards.Contains(rfid))
                {
                    var n = DateTime.Now;
                    var c = (RFIDCard)Cards[rfid];
                    if (c.schedule.days.Contains(n.DayOfWeek))
                    {
                        var d = (Day)c.schedule.days[n.DayOfWeek];
                        if (n.TimeOfDay > d.start.TimeOfDay && n.TimeOfDay < d.end.TimeOfDay)
                        {
                            Debug.Print("OPEN");
                            UnlockDoor();
                        }
                        else Debug.Print("Not Scheduled");
                    }
                    else Debug.Print("Not Scheduled");
                }
                else Debug.Print("Not Valid");
            }
            rfidBits = 0;
        }

        private static void UpdateUsers_Callback(Object StateInfo)
        {
            var json = "["
                        + "{\"created_at\":\"2012-06-06T00:00:00Z\",\"email\":\"robert.plant@example.com\","
                        + "\"id\":2,\"name\":\"Robert Plant\",\"updated_at\":\"2012-06-06T00:00:00Z\"},"

                        + "{\"created_at\":\"2012-06-06T00:00:00Z\",\"email\":\"jimmy.page@example.com\","
                        + "\"id\":1,\"name\":\"Jimmy Page\",\"updated_at\":\"2012-06-06T00:00:00Z\","
                        + "\"card\":{\"number\":\"76537489\"},\"plan\":{\"name\":\"Medium\","
                        + "\"schedule\":[{\"day\":{\"name\":\"Monday\",\"start\":\"08:00:00\",\"end\":\"17:30:00\"}},"
                        + "{\"day\":{\"name\":\"Tuesday\",\"start\":\"08:00:00\",\"end\":\"17:30:00\"}}]}},"

                        + "{\"created_at\":\"2012-06-09T00:00:00Z\",\"email\":\"john@tm107.com\","
                        + "\"id\":3,\"name\":\"john zalewski\",\"updated_at\":\"2012-06-06T00:00:00Z\","
                        + "\"card\":{\"number\":\"FEF4E3\"},\"plan\":{\"name\":\"Full\","
                        + "\"schedule\":[{\"day\":{\"name\":\"Sunday\",\"start\":\"00:00:00\",\"end\":\"24:00:00\"}},"
                        + "{\"day\":{\"name\":\"Monday\",\"start\":\"00:00:00\",\"end\":\"24:00:00\"}},"
                        + "{\"day\":{\"name\":\"Tuesday\",\"start\":\"00:00:00\",\"end\":\"24:00:00\"}},"
                        + "{\"day\":{\"name\":\"Wednesday\",\"start\":\"00:00:00\",\"end\":\"24:00:00\"}},"
                        + "{\"day\":{\"name\":\"Thursday\",\"start\":\"00:00:00\",\"end\":\"24:00:00\"}},"
                        + "{\"day\":{\"name\":\"Friday\",\"start\":\"00:00:00\",\"end\":\"24:00:00\"}},"
                        + "{\"day\":{\"name\":\"Saturday\",\"start\":\"00:00:00\",\"end\":\"24:00:00\"}}]}}"
                        
                        + "]";
            var parser = new JSONParser();
            //var results = parser.Parse(json);

            //short status;
            ArrayList users=new ArrayList();
            users = (ArrayList)JSON.JsonDecode(json);
            if (users == null || users.Count == 0) return;
            Hashtable cards = new Hashtable();
            foreach (Hashtable u in users)
            {
                Debug.Print(u["name"].ToString());
                Hashtable card;
                if (!parser.Find("card", u, out card)) continue;
                string n;
                if(!parser.Find("number", card, out n))continue;
                RFIDCard c = new RFIDCard();
                c.number = n;
                Debug.Print(c.number);
                Hashtable p;
                if(!parser.Find("plan", u, out p))continue;
                Debug.Print(p["name"].ToString());
                ArrayList days;
                if (!parser.Find("schedule", p, out days) || days.Count==0) continue;
                c.schedule = new Schedule();
                c.schedule.name = p["name"].ToString();
                c.schedule.days = new Hashtable();
                foreach (Hashtable dy in days)
                {
                    Hashtable d;
                    if(!parser.Find("day",dy, out d))continue;
                    Day day = new Day();
                    string s;
                    if (!parser.Find("name", d, out s)) continue;
                    day.name = s;
                    if (!DaysOfWeek.Contains(s)) continue;
                    if (!parser.Find("start", d, out s)) continue;
                    day.start = day.ParseDateTime(s,(int)DaysOfWeek[day.name]);
                    var dow = day.start.DayOfWeek;
                    if (!parser.Find("end", d, out s)) continue;
                    day.end = day.ParseDateTime(s, (int)DaysOfWeek[day.name]);
                    Debug.Print(day.name + " " + day.start.ToString() + " " + day.end.ToString());
                    c.schedule.days.Add(day.start.DayOfWeek, day);
                }
                cards.Add(c.number, c);
            }
            if (cards != null && cards.Count > 0)
            {
                lock (Cards_Lock)
                {
                    Cards = cards;

                    //string rfid = "FEF4E3";
                    //if (Cards.Contains(rfid))
                    //{
                    //    var n = DateTime.Now;
                    //    var c = (RFIDCard)Cards[rfid];
                    //    if (c.schedule.days.Contains(n.DayOfWeek))
                    //    {
                    //        var d = (Day)c.schedule.days[n.DayOfWeek];
                    //        if (n.TimeOfDay > d.start.TimeOfDay && n.TimeOfDay < d.end.TimeOfDay)
                    //        {
                    //            Debug.Print("OPEN");
                    //            UnlockDoor();
                    //        }
                    //        else Debug.Print("Not Scheduled");
                    //    }
                    //    else Debug.Print("Not Scheduled");
                    //}
                    //else Debug.Print("Not Valid");
                }
            }
        }

        private static void UnlockDoor()
        {
            DoorStrike.Write(true);
            Thread.Sleep(5000);
            DoorStrike.Write(false);
        }
    }
}
