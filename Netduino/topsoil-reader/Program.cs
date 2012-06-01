using System;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using astra.http;

namespace seedcoworking.topsoilreader
{
    public class Program
    {
        //private static OutputPort led = new OutputPort((Cpu.Pin)Pins.GPIO_PIN_A0, true);
        private static DateTime LDRtime = new DateTime(1980, 1, 1);
        private static Object LDR_Lock = new Object();
        //private static OutputPort Wout0 = new OutputPort((Cpu.Pin)Pins.GPIO_PIN_D0, true);
        //private static OutputPort Wout1 = new OutputPort((Cpu.Pin)Pins.GPIO_PIN_D1, true);
        private static Object WData_Lock = new Object();
        private static int rfidBits = 0;
        private static int SendBits = 0;
        private static byte[] rfidBytes = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private static DateTime startBits = new DateTime();
        private static DateTime stopBits = new DateTime();
        private static HttpImplementation webServer;
        
        public static void Main()
        {
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

            new Thread(webServerThread).Start();

            // wait forever...
            Thread.Sleep(Timeout.Infinite);
        }

        private static void webServerThread()
        {
            webServer = new HttpWiflyImpl(processRequest, 80, HttpWiflyImpl.DeviceType.crystal_14_MHz, SPI.SPI_module.SPI1, SecretLabs.NETMF.Hardware.Netduino.Pins.GPIO_PIN_D10);

            //Debug.Print("Listening on " + webServer.getIP());
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

            rfidBits = 0;
        }

    }
}
