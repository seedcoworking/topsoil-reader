using System.Collections;
using System.IO;
using System.Text;
using Controller;
using SecretLabs.NETMF.Hardware.Netduino;
using System;

namespace seedcoworking.topsoilreader
{
    class Config
    {
        public const string root = @"\SD\";
        public string name {get;set;}
        protected string file;
        public string datapath { get; set; }
        public string hardcode { get; set; }
        public string users_host { get; set; }
        public ushort users_host_port { get; set; }
        public string users_door_login { get; set; }
        public string users_door_password { get; set; }
        public string users_door_root { get; set; }
        public string users_door_get { get; set; }

        public Config(string n = "config")
        {
            name = n.Trim();
            file = name + ".conf";
            Load();
        }

        
        public void Load()
        {
            var f = new FileInfo(root + file);
            if (f.Exists)
            {
                Hashtable cfg = null;
                using (FileStream jfs = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.None, 2048))
                {
                    cfg = (Hashtable)JSON.JsonDecode(jfs);
                }
                if (cfg != null)
                {
                    if (cfg.Contains("datapath")) datapath = (string)cfg["datapath"];
                    if (cfg.Contains("hardcode")) hardcode = (string)cfg["hardcode"];
                    if (cfg.Contains("users_host")) users_host = (string)cfg["users_host"];
                    if (cfg.Contains("users_host_port")) users_host_port = (ushort)Math.Abs(Math.Round((double)cfg["users_host_port"]));
                    if (cfg.Contains("users_door_login")) users_door_login = (string)cfg["users_door_login"];
                    if (cfg.Contains("users_door_password")) users_door_password = (string)cfg["users_door_password"];
                    if (cfg.Contains("users_door_root")) users_door_root = (string)cfg["users_door_root"];
                    if (cfg.Contains("users_door_get")) users_door_get = (string)cfg["users_door_get"];
                }
            }
            
        }
        
        public void Save()
        {
            Hashtable cfg = new Hashtable();
            cfg.Add("datapath", datapath);
            cfg.Add("hardcode", hardcode);
            cfg.Add("users_host", users_host);
            cfg.Add("users_host_port", users_host_port);
            cfg.Add("users_door_login", users_door_login);
            cfg.Add("users_door_password", users_door_password);
            cfg.Add("users_door_root", users_door_root);
            cfg.Add("users_door_get", users_door_get);
            string json = JSON.JsonEncode(cfg);
            using (FileStream f=File.Create(root + file)) 
            {
                byte[] b = Encoding.UTF8.GetBytes(json);
                f.Write(b,0,b.Length);
            }
        }
    }
}
