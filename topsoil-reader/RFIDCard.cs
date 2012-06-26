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
        public string name { get; set; }
        public Hashtable days { get; set; }
    }

    public class Day
    {
        public string name { get; set; }
        public DateTime start{ get; set;}
        public DateTime end { get; set; }

        public DateTime ParseDateTime(String str, int DayOfWeek)
        {
            int h=0, m=0, s=0;
            string []sp = str.Split(':');
            if (sp.Length > 0)
            {
                h = int.Parse(sp[0]);
                if (h >= 24)
                {
                    h = 23;
                    m = 59;
                    s = 59;
                }
                else if (sp.Length > 1)
                {
                    m = int.Parse(sp[1]);
                    if (m >= 60)
                    {
                        m = 59;
                        s = 59;
                    }
                    else if (sp.Length > 2)
                    {
                        s = int.Parse(sp[2]);
                        if (s >= 60) s = 59;
                    }
                }
            }
            return new DateTime(2012, 4, DayOfWeek+1, h, m, s);
        }
    }

    public class Trigger
    {
        public DateTime time{ get; set;}
        public ArrayList enable { get; set; }//of cards to enable
        public ArrayList disable { get; set; }// of cards to disable
    }
}
