using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoundlessProxyUi
{
    public class ServerList
    {
        public string Hostname;
        public string Ip;
    }

    class ServerListAbstraction : PersistanceFileAbstraction<List<ServerList>>
    {
        public ServerListAbstraction() : base("ServerList.xml") { }

        public List<ServerList> Data
        {
            get
            {
                lock (persistanceFile)
                {
                    return persistanceFile.Data.ToList();
                }
            }
        }

        public void AddServer(ServerList server)
        {
            lock (persistanceFile)
            {
                persistanceFile.Data.Add(server);
                persistanceFile.Save();
            }
        }

        public void Clear()
        {
            lock (persistanceFile)
            {
                persistanceFile.Data.Clear();
                persistanceFile.Save();
            }
        }
    }
}
