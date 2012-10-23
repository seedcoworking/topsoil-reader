using System;
using System.Text;
using System.Threading;
using System.IO;
//using System.IO.Ports;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

/*
 * Copyright 2011-2012 Stefan Thoolen (http://www.netmftoolbox.com/)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
/*
 * Modified by John Zalewski to handle the SPI interface on the sparkfun shield
 * changes loosely based on HttpWiflyImpl from Quiche31
 * /
/*
 * HttpLibrary implementation for Sparkfun's WiFly shield
 *      
 * Use this code for whatever you want. Modify it, redistribute it, at will
 * Just keep this header intact, however, and add your own modifications to it!
 * 
 * 29 Jan 2011  -- Quiche31 - Repackaged to fit the driver independent HttpLibrary
 * 20 Jan 2011  -- Quiche31 - fixed baudrate bug with quartz at 12.288MHz, and insufficient debug output with TraceAll
 * 14 Jan 2011  -- Quiche31 - SPI-UART at 38400 bauds to avoid garbled Tx, allow UART in parrallel to normal operation, support http post
 * 10 Jan 2011  -- Quiche31 - Addition of HTTP helper code
 * 10 Jan 2011  -- Quiche31 - Slim port from Azalea Galaxy code
 * 17 Dec 2010 -- "Phillip" / "Azalea Galaxy" Original code for Arduino (https://github.com/sparkfun/WiFly-Shield)
 * 
 * */
namespace Toolbox.NETMF.Hardware.GSXSPI
{
    /// <summary>
    /// Roving Networks WiFly GSX module driver
    /// </summary>
    /// <remarks>
    /// This class is based on the Roving Networks manual found at http://www.rovingnetworks.com/files/resources/WiFly-RN-UM-2.31-v-0.1r.pdf
    /// The class has been tested with a RN-XV board containing an RN-171 module containing the 2.27 firmware.
    /// The implementation is not 100% perfect, but it works for most applications.
    /// As mentioned in the Apache 2.0 license note above, no guarantees can be given, I just hope this code might be of help.
    /// </remarks>
    public class WiFlyGSX_SPI : IDisposable
    {
        #region "SPI enums"
        public enum DeviceType
        {
            crystal_12_288_MHz,
            crystal_14_MHz,
        }

        private enum WiflyRegister
        {
            THR = 0x00 << 3,
            RHR = 0x00 << 3,
            IER = 0x01 << 3,
            FCR = 0x02 << 3,
            IIR = 0x02 << 3,
            LCR = 0x03 << 3,
            MCR = 0x04 << 3,
            LSR = 0x05 << 3,
            MSR = 0x06 << 3,
            SPR = 0x07 << 3,
            DLL = 0x00 << 3,
            DLM = 0x01 << 3,
            EFR = 0x02 << 3,
        }
        #endregion


        public DeviceType m_deviceType { get; set; }
        public int LocalPort { get; set; }
        private SPI m_uart;
        private SPI.SPI_module m_spiModule;
        private Cpu.Pin m_chipSelect;
        private Boolean m_initialized = false;
        
        #region "Construction and destruction"
        /// <summary>
        /// Initializes the WiFly GSX Module
        /// </summary>
        /// <param name="localPort">The setup speed of the serial port</param>
        /// <param name="deviceType">Which version of the shell</param>
        /// <param name="CommandChar">The character used to enter command mode</param>
        /// <param name="DebugMode">Enables debug mode</param>
        public WiFlyGSX_SPI(DeviceType deviceType, SPI.SPI_module spiModule, Cpu.Pin chipSelect, 
            string CommandChar = "$", bool DebugMode = false, uint BufferSize = 2000)
        {
            _SPI_StreamBuffer_Max = BufferSize;
            m_deviceType = deviceType;
            this.m_spiModule = spiModule;
            this.m_chipSelect = chipSelect;
            
            // Configures this client
            this._CommandMode_InitString = CommandChar + CommandChar + CommandChar;
            this.DebugMode = DebugMode;
            this._Mode = Modes.Idle;

            if (!m_initialized)
            {
                m_uart = new SPI(new SPI.Configuration(m_chipSelect, false, 10, 10, false, true, 8000, m_spiModule));
                WriteRegister(WiflyRegister.LCR, 0x80); // 0x80 to program baudrate

                if (m_deviceType == DeviceType.crystal_12_288_MHz)
                    // value = (12.288*1024*1024) / (baudrate*16)
                    WriteRegister(WiflyRegister.DLL, 21);       // 4800=167, 9600=83, 19200=42, 38400=21
                else
                    // value = (14.7456*10^6) / (baudrate*16)
                    WriteRegister(WiflyRegister.DLL, 1);     // 4800=192, 9600=96, 19200=48, 38400=24
                WriteRegister(WiflyRegister.DLM, 0);
                WriteRegister(WiflyRegister.LCR, 0xbf); // access EFR register
                WriteRegister(WiflyRegister.EFR, 0xd0); // enable enhanced registers and enable RTS/CTS on the SPI UART
                WriteRegister(WiflyRegister.LCR, 3);    // 8 data bit, 1 stop bit, no parity
                WriteRegister(WiflyRegister.FCR, 0x06); // reset TXFIFO, reset RXFIFO, non FIFO mode
                WriteRegister(WiflyRegister.FCR, 0x01); // enable FIFO mode
                WriteRegister(WiflyRegister.SPR, 0x55);

                if (ReadRegister(WiflyRegister.SPR) != 0x55)
                    throw new Exception("Failed to init SPI<->UART chip");

                new Thread(_SPI_Listen).Start();
                m_initialized = true;
            }

            // Configures and opens the port
            //this._SerialPort = new SerialPort(PortName, BaudRate, Parity.None, 8, StopBits.One);
            //this._SerialPort.DataReceived += new SerialDataReceivedEventHandler(_SerialPort_DataReceived);
            //this._SerialPort.Open();
           
            // Makes sure we're not in a command mode or something
            _SPI_Write("exit\r");

            // Starts command mode
            this._CommandMode_Start();
            //enterCommandMode();

            //set up time sync
            //if (!this._CommandMode_Exec("set t z 5")) throw new SystemException(this._CommandMode_Response);
            //if (!this._CommandMode_Exec("set time port 123")) throw new SystemException(this._CommandMode_Response);
            //if (!this._CommandMode_Exec("set time enable 1440")) throw new SystemException(this._CommandMode_Response);
            //if (!this._CommandMode_Exec("show time")) throw new SystemException(this._CommandMode_Response);
            //if (!this._CommandMode_Exec("time")) throw new SystemException(this._CommandMode_Response);
            //if (!this._CommandMode_Exec("show t t")) throw new SystemException(this._CommandMode_Response);
            

            if (!this._CommandMode_Exec("set uart mode 0x10")) throw new SystemException(this._CommandMode_Response);
            // Disables system output; we only want to receive data when we request it, so we won't get bogus data through our streams
            if (!this._CommandMode_Exec("set sys printlvl 0")) throw new SystemException(this._CommandMode_Response);
            //if (SendCommand("set sys printlvl 0")=="") throw new SystemException(this._CommandMode_Response);
            // Disables the welcome greeting sent when we open a connection
            if (!this._CommandMode_Exec("set comm remote 0")) throw new SystemException(this._CommandMode_Response);
            //if (SendCommand("set comm remote 0")=="") throw new SystemException(this._CommandMode_Response);
            // Reads out the module version
            this.ModuleVersion = this._CommandMode_GetInfo("ver");
            //his.ModuleVersion = SendCommand("ver");
            // Reads out the MAC address
            this.MacAddress = this._CommandMode_GetInfo("get mac").Substring(9);
            //this.MacAddress = SendCommand("get mac").Substring(9);
            // Requests the communication options
            this._SocketOpenString = this._CommandMode_ReadValue("comm", "open");
            this._SocketCloseString = this._CommandMode_ReadValue("comm", "close");

            // Leaves command mode
            this._CommandMode_Stop();
            //leaveCommandMode();
        }

        /// <summary>Disposes this object</summary>
        public void Dispose()
        {
            //this._SerialPort.Dispose();
        }
        #endregion

        #region "SPI WriteRead code"


       
       
        #endregion



        #region "Network tools"
        /// <summary>
        /// Looks up the IP address of a hostname
        /// </summary>
        /// <param name="Hostname">The hostname</param>
        /// <returns>The IP address</returns>
        public string DnsLookup(string Hostname)
        {
            this._CommandMode_Start();
            string RetValue = this._CommandMode_GetInfo("lookup " + Hostname);
            this._CommandMode_Stop();
            return RetValue.Substring(RetValue.IndexOf("=") + 1);
        }

        /// <summary>
        /// Returns the amount of seconds passed since 1 jan. 1900
        /// </summary>
        /// <param name="IpAddress">The IP address of an NTP server</param>
        /// <param name="Port">The UDP port of the NTP server</param>
        /// <returns>The amount of seconds passed since 1 jan. 1900</returns>
        public double NtpLookup(string IpAddress, ushort Port = 123)
        {
            this._CommandMode_Start();
            if (!this._CommandMode_Exec("set time address " + IpAddress)) throw new SystemException(this._CommandMode_Response);
            if (!this._CommandMode_Exec("set time port " + Port.ToString())) throw new SystemException(this._CommandMode_Response);
            this._SPI_Write("time\r");
            string RetValue = this._CommandMode_ReadValue("t", "t", "rtc");
            this._CommandMode_Stop();

            // We get the number of seconds since 1 jan. 1970
            double Value1970 = double.Parse(RetValue);
            // But we need the number of seconds since 1 jan. 1900!!
            return (Value1970 + 2208988800);
        }

        #endregion

        #region "Network configuration"
        /// <summary>Returns the MAC address of the WiFly module</summary>
        public string MacAddress { get; protected set; }

        /// <summary>Supported wifi authentication modes</summary>
        public enum AuthMode
        {
            /// <summary>No encryption at all. Are you insane?!?!?</summary>
            Open = 0,
            /// <summary>128-bit Wired Equivalent Privacy (WEP)</summary>
            WEP_128 = 1,
            /// <summary>Wi-Fi Protected Access (WPA)</summary>
            WPA1 = 2,
            /// <summary>Mixed WPA1 &amp; WPA2-PSK</summary>
            MixedWPA1_WPA2 = 3,
            /// <summary>Wi-Fi Protected Access (WPA) II with preshared key</summary>
            WPA2_PSK = 4
        }

        /// <summary>Returns the local IP</summary>
        public string LocalIP
        {
            get
            {
                this._CommandMode_Start();
                string RetValue = this._CommandMode_ReadValue("ip", "ip");
                this._CommandMode_Stop();
                return RetValue.Substring(0, RetValue.IndexOf(":"));
            }
        }

        /// <summary>
        /// Enables DHCP
        /// </summary>
        public void EnableDHCP()
        {
            // Enterring command mode
            this._CommandMode_Start();
            // Enables DHCP
            if (!this._CommandMode_Exec("set ip dhcp 1")) throw new SystemException(this._CommandMode_Response);
            // Leaves command mode
            this._CommandMode_Stop();
        }

        /// <summary>
        /// Enables and configures a static IP address
        /// </summary>
        /// <param name="IPAddress">The IP address</param>
        /// <param name="SubnetMask">The subnet mask</param>
        /// <param name="Gateway">The gateway</param>
        /// <param name="DNS">The DNS server</param>
        public void EnableStaticIP(string IPAddress, string SubnetMask, string Gateway, string DNS)
        {
            // Enterring command mode
            this._CommandMode_Start();
            // Configures the IP
            if (!this._CommandMode_Exec("set ip dhcp 0")) throw new SystemException(this._CommandMode_Response);
            if (!this._CommandMode_Exec("set ip address " + IPAddress)) throw new SystemException(this._CommandMode_Response);
            if (!this._CommandMode_Exec("set ip gateway " + Gateway)) throw new SystemException(this._CommandMode_Response);
            if (!this._CommandMode_Exec("set ip netmask " + SubnetMask)) throw new SystemException(this._CommandMode_Response);
            if (!this._CommandMode_Exec("set dns address " + DNS)) throw new SystemException(this._CommandMode_Response);
            // Closes down command mode
            this._CommandMode_Stop();
        }

        /// <summary>
        /// Joins a wireless network
        /// </summary>
        /// <param name="SSID">The name of the wireless network</param>
        /// <param name="Channel">The channel the AP is listening on (0 for autodetect)</param>
        /// <param name="Authentication">The method for authentication</param>
        /// <param name="Key">The shared key required to join the network (WEP / WPA)</param>
        /// <param name="KeyIndex">The index of the key (WEP only)</param>
        public void JoinNetwork(string SSID, int Channel = 0, AuthMode Authentication = AuthMode.Open, string Key = "", int KeyIndex = 1)
        {
            // Enterring command mode
            this._CommandMode_Start();
            // Configures the network
            if (!this._CommandMode_Exec("set wlan ssid " + SSID)) throw new SystemException(this._CommandMode_Response);
            if (!this._CommandMode_Exec("set wlan channel " + Channel)) throw new SystemException(this._CommandMode_Response);
            if (!this._CommandMode_Exec("set wlan auth " + Authentication.ToString())) throw new SystemException(this._CommandMode_Response);
            if (Authentication == AuthMode.WEP_128)
            {
                if (!this._CommandMode_Exec("set wlan key " + Key)) throw new SystemException(this._CommandMode_Response);
                if (!this._CommandMode_Exec("set wlan num " + KeyIndex.ToString())) throw new SystemException(this._CommandMode_Response);
            }
            else if (Authentication != AuthMode.Open)
            {
                if (!this._CommandMode_Exec("set wlan phrase " + Key)) throw new SystemException(this._CommandMode_Response);
            }
            // Actually joins the network
            this._SPI_Write("join\r");
            // Closes down command mode
            this._CommandMode_Stop();
        }
        #endregion

        #region "Streaming mode"
        /// <summary>Identifier for the beginning of a stream</summary>
        private string _SocketOpenString = "";
        /// <summary>Identifier for the end of a stream</summary>
        private string _SocketCloseString = "";

        /// <summary>Returns the remote hostname</summary>
        public string RemoteHostname { get; protected set; }
        /// <summary>Returns the remote port</summary>
        public ushort RemotePort { get; protected set; }

        /// <summary>Returns wether we're connected to a remote host</summary>
        public bool SocketConnected { get { return this._Mode == Modes.StreamingMode; } }

        /// <summary>Returns the length of the socket buffer</summary>
        public uint SocketBufferLength { get { return (uint)this._SPI_StreamBuffer.Length; } }

        /// <summary>
        /// Opens a TCP socket
        /// </summary>
        /// <param name="Hostname">Remote hostname</param>
        /// <param name="Port">Remote port</param>
        /// <param name="Timeout">Socket timeout in ms</param>
        public void OpenSocket(string Hostname, ushort Port, int Timeout = 5000)
        {
            if (this._SocketOpenString == "" || this._SocketCloseString == "") throw new ApplicationException("WTF?!");

            // Copies values locally
            this.RemoteHostname = Hostname;
            this.RemotePort = Port;
            // Lets start command mode first
            this._CommandMode_Start();
            // Now we open the connection
            this._Mode = Modes.Connecting;
            this._SPI_Write("open " + Hostname + " " + Port.ToString() + "\r");

            // Wait till we're connected
            while (Timeout > 0)
            {
                if (this.SocketConnected) break;
                Thread.Sleep(1);
                --Timeout;
            }

            // Are we timed out?
            if (!this.SocketConnected) throw new ApplicationException("Connection timed out");
        }

        /// <summary>Closes a TCP socket</summary>
        public void CloseSocket()
        {
            if (this._Mode != Modes.StreamingMode) return;

            Thread.Sleep(250);
            this._SPI_Write(this._CommandMode_InitString);
            Thread.Sleep(250);
            this._SPI_Write("close\r");
            this._SPI_Write("exit\r");
            while (this.SocketConnected) { }
        }

        /// <summary>
        /// Writes data to the socket
        /// </summary>
        /// <param name="WriteBuffer">Data to write</param>
        public void SocketWrite(string WriteBuffer)
        {
            if (this._Mode != Modes.StreamingMode) throw new InvalidOperationException("Can't write data when not connected");
            this._SPI_Write(WriteBuffer);
        }

        /// <summary>
        /// Reads data from the socket
        /// </summary>
        /// <param name="Length">The amount of bytes to read (-1 is everything)</param>
        /// <param name="UntilReached">Read until this string is reached (empty is no ending)</param>
        /// <returns>The data</returns>
        public string SocketRead(int Length = -1, string UntilReached = "")
        {
            lock (_SPI_StreamBuffer_Lock)
            {
                if (this._SPI_StreamBuffer.Length == 0) return "";

                int SendLength = Length == -1 ? this._SPI_StreamBuffer.Length : Length;
                if (UntilReached != "")
                {
                    int Pos = this._SPI_StreamBuffer.IndexOf(UntilReached);
                    if (Pos < 0 || Pos >= SendLength) return "";
                    SendLength = Pos + UntilReached.Length;
                }

                string RetVal = this._SPI_StreamBuffer.Substring(0, SendLength);
                this._SPI_StreamBuffer = this._SPI_StreamBuffer.Substring(RetVal.Length);
                return RetVal;
            }
        }
        #endregion

        #region "SPI code"

        /// <summary>Current state of the serial connection</summary>
        private Modes _Mode = Modes.Idle;
        /// <summary>Possible states of the WiFly connection</summary>
        private enum Modes
        {
            /// <summary>Not in Command Mode nor connected</summary>
            Idle = 0,
            /// <summary>In Command Mode</summary>
            CommandMode = 1,
            /// <summary>Trying to connect</summary>
            Connecting = 2,
            /// <summary>Connected</summary>
            StreamingMode = 3
        }

        /// <summary>Buffer while in Idle or Command Mode</summary>
        private string _SPI_TextBuffer = "";
        /// <summary>Buffer while in Stream mode</summary>
        private string _SPI_StreamBuffer = "";
        /// <summary>Maximum Buffer Size while in Stream mode</summary>
        private uint _SPI_StreamBuffer_Max = 2000;
        /// <summary>Object used to Lock() StreamBuffer threading</summary>
        private Object _SPI_StreamBuffer_Lock = new Object();

        /// <summary>Buffer that will contain the last _SocketCloseString.length bytes</summary>
        private string _SPI_EndStreamCheck = "";


        private void WriteRegister(WiflyRegister reg, byte b)
        {
            m_uart.Write(new byte[] { (byte)reg, b });
        }

        private byte ReadRegister(WiflyRegister reg)
        {
            byte[] buffer = new byte[] { (byte)((byte)reg | 0x80), 0 };
            m_uart.WriteRead(buffer, buffer);
            return buffer[1];
        }

        private void WriteBytes(byte[] ba)
        {
            for (int i = 0; i < ba.Length; i++)
                WriteRegister(WiflyRegister.THR, ba[i]);
        }

        public byte[] getBytes(String str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        /// <summary>Writes raw data to the WiFly module</summary>
        /// <param name="Text">Data to write</param>
        private void _SPI_Write(string Text)
        {
            _DebugPrint('O', Text);
            WriteBytes(Encoding.UTF8.GetBytes(Text));
        }

        /// <summary>The WiFly module sent us data</summary>
        /// <param name="sender">Reference to this._Port</param>
        /// <param name="e">Event data</param>
        public void _SPI_Listen()
        {
            String ReadBuffer; 
            while (true)
            {
                ReadBuffer = "";
                int max = 2000;
                if (this._Mode == Modes.StreamingMode)max = (int)_SPI_StreamBuffer_Max - _SPI_StreamBuffer.Length;
                bool end_of_packet = true;
                for (int i = 0; i < max && !(end_of_packet = ((ReadRegister(WiflyRegister.LSR) & 0x01) == 0)); i++)
                {
                    char c = (char)ReadRegister(WiflyRegister.RHR);
                    ReadBuffer += c;
                }
                _SPI_DataReceived(ReadBuffer, end_of_packet);
                if (!end_of_packet) Thread.Sleep(1);
                else Thread.Sleep(4);
            }
        }

        private void _SPI_DataReceived(String ReadBuffer,bool end_of_packet)
        {
            if (ReadBuffer.Length == 0)
            {
                if (end_of_packet && this._Mode == Modes.CommandMode && this._SPI_TextBuffer != "") ReadBuffer += "\r\n";
                else return;
            }

            // Reads out the data and converts it to a string
            
            // While in Streaming mode, we need to handle the data differently
            if (this._Mode == Modes.StreamingMode)
            {
                lock(_SPI_StreamBuffer_Lock)
                {
                    _DebugPrint('I', ReadBuffer);
                    //string NewBuffer = _SPI_StreamBuffer;

                    // Fixes the "*CLOS*" issues as described below
                    if(_SPI_EndStreamCheck != _SocketCloseString)
                    {
                        _SPI_EndStreamCheck += ReadBuffer;
                        // Does it contain a closing string?
                        int CheckPos = _SPI_EndStreamCheck.IndexOf(this._SocketCloseString);
                        if (CheckPos >= 0)
                        {
                            // jtz - sometimes the closing string (default: "*CLOS*") is WITHIN the string.
                            // might be just in SPI mode with the UART
                            //so SPI_TextBuffer has to be appended back on
                            this._SPI_TextBuffer = _SPI_EndStreamCheck.Substring(CheckPos + this._SocketCloseString.Length);
                            _SPI_StreamBuffer = _SPI_EndStreamCheck.Substring(0, CheckPos);
                            _SPI_StreamBuffer += this._SPI_TextBuffer;
                            _SPI_TextBuffer = "";
                            _SPI_EndStreamCheck = _SocketCloseString;
                        }
                        else 
                        {
                            //extract partial string and save for next receive
                            int len = _SPI_EndStreamCheck.Length;
                            if (len > _SocketCloseString.Length)
                            {
                                len = this._SPI_EndStreamCheck.Length - this._SocketCloseString.Length;
                                _SPI_StreamBuffer += _SPI_EndStreamCheck.Substring(0,len);
                                _SPI_EndStreamCheck = _SPI_EndStreamCheck.Substring(len);
                            }
                        }
                    }
                    else
                    {
                        _SPI_StreamBuffer += ReadBuffer;
                    }
                    //{
                    //    this._SPI_EndStreamCheck += ReadBuffer;
                    //    if (this._SPI_EndStreamCheck.Length > this._SocketCloseString.Length)
                    //    this._SPI_EndStreamCheck = this._SPI_EndStreamCheck.Substring(this._SPI_EndStreamCheck.Length - this._SocketCloseString.Length);
                    //}
                    
                    // The closing string (default: "*CLOS*") is many times sent in multiple packets.
                    // This causes annoying issues of connections not shutting down well.
                    // This fixes that issue, but it's possible the last few bytes of the stream contain something like "*CL" or something.
                    if (end_of_packet && _SPI_EndStreamCheck == this._SocketCloseString)
                    {
                        _SPI_EndStreamCheck = ""; ;
                        this._Mode = Modes.Idle;
                        _DebugPrint('D', "Left streaming mode");
                    }
                    return;
                }
            }

            // When not in Streaming Mode we check if we need to parse text line by line
            this._SPI_TextBuffer += ReadBuffer;

            // Do we can/need to enter streaming mode?
            if (this._Mode == Modes.Connecting)
            {
                int CheckPos = this._SPI_TextBuffer.IndexOf(this._SocketOpenString);
                if (CheckPos >= 0)
                {
                    this._SPI_StreamBuffer = this._SPI_TextBuffer.Substring(CheckPos + this._SocketOpenString.Length);
                    this._SPI_TextBuffer = this._SPI_TextBuffer.Substring(0, CheckPos);
                    this._Mode = Modes.StreamingMode;
                    _DebugPrint('D', "Enterred streaming mode");
                }
            }

            // Parses all lines
            while (true)
            {
                int CheckPos = this._SPI_TextBuffer.IndexOf("\r\n");
                if (CheckPos < 0) break;

                string Line = this._SPI_TextBuffer.Substring(0, CheckPos);
                this._SPI_TextBuffer = this._SPI_TextBuffer.Substring(CheckPos + 2);
                this._SPI_LineReceived(Line);
            }
        }


        /// <summary>The WiFly module sent us a line of text</summary>
        /// <param name="Text">The text</param>
        private void _SPI_LineReceived(string Text)
        {
            _DebugPrint('I', Text + "\r\n");

            // Did we enter command mode?
            if (Text == "CMD" && this._Mode == Modes.Idle)
            {
                this._Mode = Modes.CommandMode;
                _DebugPrint('D', "Successfully enterred the command mode");
                return;
            }

            // Is this line for Command Mode?
            if (this._Mode == Modes.CommandMode) _CommandMode_LineReceived(Text);
        }

        #endregion

        #region "Command Mode support"
        /// <summary>Returns the version of the WiFly module</summary>
        public string ModuleVersion { get; protected set; }

        /// <summary>Contains the init string to enter command mode</summary>
        private string _CommandMode_InitString = "";

        /// <summary>The WiFly module sent us a line of text during Command Mode</summary>
        /// <param name="Text">The text</param>
        private void _CommandMode_LineReceived(string Text)
        {
            // Is this an echo on Command Mode?
            if (Text.Substring(Text.Length - 1) == "\r")
            {
                // Yes it is, lets ignore it!
                _DebugPrint('D', "Last line was an echo");
                return;
            }

            // Did we left command mode?
            if (Text == "EXIT")
            {
                this._Mode = Modes.Idle;
                _DebugPrint('D', "Successfully left the command mode");
            }

            // Are we waiting for a command to complete?
            if (!this._CommandMode_ResponseComplete)
            {
                this._CommandMode_Response += Text + "\r\n";
                if (Text == "AOK" || Text == "<2.31> " || Text.Substring(0, 3) == "ERR")
                {
                    if (Text == "<2.31> ") this._CommandMode_Response += "AOK\r\n";
                    this._CommandMode_Response = this._CommandMode_Response.TrimEnd();
                    this._CommandMode_ResponseComplete = true;
                }
            }

            // Are we waiting for info?
            if (this._CommandMode_GetInfoLines > 0)
            {
                this._CommandMode_GetInfoResponse += Text + "\r\n";
                this._CommandMode_GetInfoLines--;
            }

            // Are we waiting for just a value?
            if (this._CommandMode_ReadKey != "")
            {
                string[] Values = Text.Split("=".ToCharArray(), 2);
                if (Values[0].ToLower() == this._CommandMode_ReadKey) this._CommandMode_ReadKeyValue = Values[1];
            }
        }

        // This is the amount of info lines we still require
        private int _CommandMode_GetInfoLines = 0;
        // Info lines will be temporarily stored here
        private string _CommandMode_GetInfoResponse = "";

        /// <summary>Executes a command and returns its answer</summary>
        /// <param name="Command">The command</param>
        /// <param name="Answers">The amount of lines to be expected</param>
        /// <returns>The answer</returns>
        private string _CommandMode_GetInfo(string Command, int Answers = 1)
        {
            this._CommandMode_GetInfoLines = Answers;
            this._CommandMode_GetInfoResponse = "";
            this._SPI_Write(Command + "\r");

            // Wait until the command is completed
            while (this._CommandMode_GetInfoLines > 0) Thread.Sleep(1);

            this._CommandMode_GetInfoResponse = this._CommandMode_GetInfoResponse.TrimEnd();

            _DebugPrint('D', "Answer: " + this._CommandMode_GetInfoResponse);

            return this._CommandMode_GetInfoResponse;
        }

        private string _CommandMode_Response = "";
        private bool _CommandMode_ResponseComplete = true;

        /// <summary>Executes a command and wait for it's response</summary>
        /// <param name="Command">The command</param>
        /// <returns>True if it returned AOK</returns>
        private bool _CommandMode_Exec(string Command)
        {
            this._CommandMode_ResponseComplete = false;
            this._CommandMode_Response = "";
            this._SPI_Write(Command + "\r");

            // Wait until the command is completed
            while (!this._CommandMode_ResponseComplete) Thread.Sleep(1);

            if (this._CommandMode_Response.Substring(this._CommandMode_Response.Length - 3) == "AOK")
            {
                _DebugPrint('D', "Last command is completed with success");
                return true;
            }
            else
            {
                _DebugPrint('D', "Last command is completed with an error");
                return false;
            }
        }

        private string _CommandMode_ReadKey = "";
        private string _CommandMode_ReadKeyValue = "";

        /// <summary>Reads a value from the config</summary>
        /// <param name="List">The config chapter</param>
        /// <param name="Key">The config name</param>
        /// <returns>The value</returns>
        private string _CommandMode_ReadValue(string List, string Key)
        {
            this._CommandMode_ReadKey = Key.ToLower();
            this._CommandMode_ReadKeyValue = "";
            this._SPI_Write("get " + List + "\r");

            while (this._CommandMode_ReadKeyValue == "") Thread.Sleep(1);

            this._CommandMode_ReadKey = "";
            return this._CommandMode_ReadKeyValue;
        }

        /// <summary>Reads a value from the config</summary>
        /// <param name="List">The config chapter</param>
        /// <param name="SubList">The config subchapter</param>
        /// <param name="Key">The config name</param>
        /// <returns>The value</returns>
        private string _CommandMode_ReadValue(string List, string SubList, string Key)
        {
            this._CommandMode_ReadKey = Key.ToLower();
            this._CommandMode_ReadKeyValue = "";
            this._SPI_Write("show " + List + " " + SubList + "\r");

            while (this._CommandMode_ReadKeyValue == "") Thread.Sleep(1);

            this._CommandMode_ReadKey = "";
            return this._CommandMode_ReadKeyValue;
        }

        /// <summary>
        /// Enters the command mode
        /// </summary>
        private void _CommandMode_Start()
        {
            // Can/need we to enter the command mode?
            if (this._Mode == Modes.CommandMode) return;
            if (this._Mode == Modes.StreamingMode) throw new InvalidOperationException("Can't open Command Mode while a stream is active");

            // Enterring command mode
            Thread.Sleep(300);
            _SPI_Write(this._CommandMode_InitString);
            Thread.Sleep(300);

            // Wait until we actually enterred the command mode
            while (this._Mode != Modes.CommandMode)
                Thread.Sleep(1);
        }

        /// <summary>
        /// Leaves the command mode
        /// </summary>
        private void _CommandMode_Stop()
        {
            // Can we leave command mode?
            if (this._Mode != Modes.CommandMode) throw new InvalidOperationException("Can't stop Command Mode when not in Command Mode");

            // Exits command mode
            this._SPI_Write("exit\r");

            // Wait until we actually left the command mode
            while (this._Mode == Modes.CommandMode)
                Thread.Sleep(1);
        }
        #endregion

        #region "Debugging"
        /// <summary>When true, debugging is enabled</summary>
        public bool DebugMode { get; set; }

        /// <summary>Does some logging</summary>
        /// <param name="Flag">Type of data: (I)ncoming / (O)utgoing / (D)ebug</param>
        /// <param name="Text">Text to debug</param>
        private void _DebugPrint(char Flag, string Text)
        {
            if (!this.DebugMode) return;
            //Debug.GC(true);

            Debug.Print(Flag + ": " + Tools.Escape(Text));
        }
        #endregion

    }
}
