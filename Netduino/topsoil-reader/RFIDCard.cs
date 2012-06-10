using System;
using System.Collections;
using System.Text;

namespace seedcoworking.topsoilreader
{
    public class RFIDCard
    {
        public string number { get; set; }
        public bool valid { get; set; }
        public Schedule schedule { get; set; }
    }

    public class Schedule
    {
        string name { get; set; }
        public Hashtable days { get; set; }
    }

    public class Day
    {
        public string name { get; set; }
        public DateTime start{ get; set; }
        public DateTime end { get; set; }
    }

    public class Trigger
    {
        public DateTime time{ get; set;}
        public ArrayList enable { get; set; }//of cards to enable
        public ArrayList disable { get; set; }// of cards to disable
    }
}
