using System;
using System.Text;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
//using astra.http;
//using netduino.helpers.Helpers;
using System.Collections;
using Controller;
//using Toolbox.NETMF.Hardware;
using Toolbox.NETMF.NET;
using Toolbox.NETMF.Hardware.GSXSPI;

namespace seedcoworking.topsoilreader
{
    public class Program
    {
        private static OutputPort DoorStrike = new OutputPort(Pins.GPIO_PIN_D8, false);
        private static Object Cards_Lock = new Object();
        private static Object WData_Lock = new Object();
        private static Object UdateUsers_Lock = new Object();
        private static int rfidBits = 0;
        private static byte[] rfidBytes = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private static DateTime startBits = new DateTime();
        private static DateTime stopBits = new DateTime();
        //private static HttpImplementation webServer;
        // Declares the WiFly module, configures the IP address and joins a wireless network
        public static WiFlyGSX_SPI WifiModule;
        private static TimerCallback UpdateUsersDelegate = new TimerCallback(UpdateUsers_Callback);
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
            //webServer = new HttpWiflyImpl(processRequest, 80, HttpWiflyImpl.DeviceType.crystal_14_MHz, SPI.SPI_module.SPI1, SecretLabs.NETMF.Hardware.Netduino.Pins.GPIO_PIN_D10);
            WifiModule = new WiFlyGSX_SPI(WiFlyGSX_SPI.DeviceType.crystal_14_MHz, SPI.SPI_module.SPI1, SecretLabs.NETMF.Hardware.Netduino.Pins.GPIO_PIN_D10, "$", true);
            Timer WDataTimer = new Timer(UpdateUsersDelegate, null, 0, 900000);
            
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

        //private static void webServerThread()
        //{
        //    //webServer = new HttpWiflyImpl(processRequest, 80, HttpWiflyImpl.DeviceType.crystal_14_MHz, SPI.SPI_module.SPI1, SecretLabs.NETMF.Hardware.Netduino.Pins.GPIO_PIN_D10);

        //    //webServer.Listen();
        //}

        //private static void processRequest(HttpContext context)
        //{
        //}

        private static void WData_OnInterrupt(uint port, uint data, DateTime time)
        {
            lock (WData_Lock)
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
        }

        private static void WData_Callback(Object StateInfo)
        {
            lock (WData_Lock)
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
                Debug.Print("Start: " + startBits.Second + ":" + startBits.Millisecond);
                Debug.Print("Recieved: " + rfid);
                Debug.Print("Recieved: " + rfidBits + " bits");
                Debug.Print("Stop: " + stopBits.Second + ":" + stopBits.Millisecond);
                Debug.Print("Testing : 123456789");
                rfid = "123456789";
                lock (Cards_Lock)
                {
                    if (Cards == null)
                    {
                        Debug.Print("No Cards on File");
                        Timer WDataTimer = new Timer(UpdateUsersDelegate, null, 150, 0);
                    }
                    else if (Cards.Contains(rfid))
                    {
                        var n = DateTime.Now;
                        var c = (RFIDCard)Cards[rfid];
                        if (c.schedule.name == "Unlimited")
                        {
                            Debug.Print("OPEN");
                            UnlockDoor();
                        }
                        else if (c.schedule.days.Contains(n.DayOfWeek))
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
        }

        private static void UpdateUsers_Callback(Object StateInfo)
        {
            lock (UdateUsers_Lock)
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
                // Creates a socket
                SimpleSocket Socket = new WiFlySocket("seed-api.herokuapp.com", 80, WifiModule);
                //SimpleSocket Socket = new WiFlySocket("api.seedcoworking.com", 80, WifiModule);

                // Connects to the socket
                Socket.Connect();

                // Does a plain HTTP request
                Socket.Send("GET /users/ HTTP/1.1\r\n");
                Socket.Send("Host: " + Socket.Hostname + "\r\n");
                Socket.Send("Connection: Close\r\n");
                Socket.Send("\r\n");

                // Prints all received data to the debug window, until the connection is terminated and there's no data left anymore
                while (Socket.IsConnected || Socket.BytesAvailable > 0)
                {
                    json = Socket.Receive().ToString();
                    if (json != "")

                        Debug.Print(json);
                }

                // Closes down the socket
                Socket.Close();

                //WifiModule.EnableDHCP();
                //WifiModule.JoinNetwork("Netduino");
                //var response = webServer.SendRequest("seed-api.herokuapp.com/users", 80, "");
                //var response = webServer.SendRequest("tm107.com", 80, "GET // HTTP/1.1\r\n");
                //SimpleSocket sc = (SimpleSocket)(new ));
                //HTTP_Client hc = new HTTP_Client();
                //hc.Get("");
                //var parser = new JSONParser();
                //var results = parser.Parse(json);

                //short status;
                ArrayList users = new ArrayList();
                users = (ArrayList)JSON.JsonDecode(json);
                if (users == null || users.Count == 0)
                {
                    Timer WDataTimer = new Timer(UpdateUsersDelegate, null, 150, 0);
                    return;
                }
                Hashtable cards = new Hashtable();
                foreach (Hashtable u in users)
                {
                    Debug.Print(u["name"].ToString());
                    Hashtable card;
                    if (!JSON.Find("card", u, out card)) continue;
                    string n;
                    if (!JSON.Find("identifier", card, out n)) continue;
                    RFIDCard c = new RFIDCard();
                    c.number = n;
                    Debug.Print(c.number);
                    Hashtable p;
                    if (!JSON.Find("plan", u, out p)) continue;
                    Debug.Print(p["name"].ToString());
                    c.schedule = new Schedule();
                    c.schedule.name = p["name"].ToString();
                    ArrayList days;
                    if (JSON.Find("schedule", p, out days) && days.Count > 0)
                    {
                        c.schedule.days = new Hashtable();
                        foreach (Hashtable dy in days)
                        {
                            Hashtable d;
                            if (!JSON.Find("day", dy, out d)) continue;
                            Day day = new Day();
                            string s;
                            if (!JSON.Find("name", d, out s)) continue;
                            day.name = s;
                            if (!DaysOfWeek.Contains(s)) continue;
                            if (!JSON.Find("start", d, out s)) continue;
                            day.start = day.ParseDateTime(s, (int)DaysOfWeek[day.name]);
                            var dow = day.start.DayOfWeek;
                            if (!JSON.Find("end", d, out s)) continue;
                            day.end = day.ParseDateTime(s, (int)DaysOfWeek[day.name]);
                            Debug.Print(day.name + " " + day.start.ToString() + " " + day.end.ToString());
                            c.schedule.days.Add(day.start.DayOfWeek, day);
                        }
                    }
                    cards.Add(c.number, c);
                }
                lock (Cards_Lock)
                {
                    if (cards != null && cards.Count > 0)Cards = cards;
                    else new Timer(UpdateUsersDelegate, null, 150, 0);
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
