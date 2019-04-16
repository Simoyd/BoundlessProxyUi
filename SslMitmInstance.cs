using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BoundlessProxyUi
{
    public class SslMitmInstance
    {
        private static readonly byte[] ipAddrBytes = Encoding.UTF8.GetBytes("ipAddr");

        public SslMitmInstance(Stream client, Stream server, ConnectionInstance connectionInstance)
        {
            m_client = client;
            m_server = server;
            m_connectionInstance = connectionInstance;
            connectionInstance.SslMitmInstance = this;

            ForwardStream(client, server, new byte[10240], CommPacketDirection.ClientToServer);
            ForwardStream(server, client, new byte[10240], CommPacketDirection.ServerToClient);
        }

        private Stream m_client;
        private Stream m_server;
        private ConnectionInstance m_connectionInstance;

        public bool ReplaceIpaddr { get; set; } = false;

        public void Kill()
        {
            m_connectionInstance.IsConnectionOpen = false;

            try
            {
                m_client.Close();
            }
            catch { }

            try
            {
                m_server.Close();
            }
            catch { }
        }

        private void ForwardStream(Stream source, Stream destination, byte[] buffer, CommPacketDirection direction)
        {
            try
            {
                source.BeginRead(buffer, 0, buffer.Length, r => Forward(source, destination, r, buffer), direction);
            }
            catch
            {
                Kill();
            }
        }

        private void Forward(Stream source, Stream destination, IAsyncResult asyncResult, byte[] buffer)
        {
            try
            {
                CommPacketDirection direction = (CommPacketDirection)asyncResult.AsyncState;

                var bytesRead = source.EndRead(asyncResult);
                if (bytesRead == 0)
                {
                    Kill();
                    return;
                }

                if (ReplaceIpaddr && buffer.Search(bytesRead, ipAddrBytes) > 0)
                {
                    string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    Regex ipSubPattern = new Regex("\\,\"ipAddr\":\"[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3}\"\\,");
                    Match ipSubMatch = ipSubPattern.Match(request);

                    if (ipSubMatch.Success)
                    {
                        string org = ipSubMatch.Groups[0].Value;
                        string rep = ",\"ipAddr\":\"127.0.0.1\",";

                        Regex reg = new Regex("Content-Length\\: ([0-9]*)");
                        Match m = reg.Match(request);

                        if (!m.Success)
                        {
                        }

                        int length = Convert.ToInt32(m.Groups[1].Value);
                        int newLength = length + rep.Length - org.Length;

                        request = request.Replace(org, rep);
                        request = request.Replace($"Content-Length: {length}", $"Content-Length: {newLength}");

                        byte[] sendBytes = Encoding.UTF8.GetBytes(request);
                        DestinationWrite(destination, sendBytes, sendBytes.Length, direction);
                    }
                    else
                    {
                        DestinationWrite(destination, buffer, bytesRead, direction);
                    }
                }
                else
                {
                    DestinationWrite(destination, buffer, bytesRead, direction);
                }

                ForwardStream(source, destination, buffer, direction);
            }
            catch
            {
                Kill();
            }
        }

        private void DestinationWrite(Stream destination, byte[] buffer, int count, CommPacketDirection direction)
        {
            if (m_connectionInstance.Parent.Model.CaptureEnabled)
            {
                byte[] saveData = buffer.Take(count).ToArray();

                MainWindow.Instance.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var packet = new CommPacket
                    {
                        Data = saveData,
                        Direction = direction,
                        HostName = m_connectionInstance.HostName,
                        Id = Guid.NewGuid(),
                        Parent = m_connectionInstance,
                    };

                    m_connectionInstance.Packets.Add(packet);

                    while (m_connectionInstance.Packets.Count > 1000)
                    {
                        var curPacket = m_connectionInstance.Packets[0];

                        m_connectionInstance.Packets.RemoveAt(0);

                        curPacket.Searches.ToList().ForEach(cur => cur.Packets.Remove(curPacket));
                    }

                    foreach (var curSearch in m_connectionInstance.Parent.Model.Searches)
                    {
                        if (saveData.Search(count, curSearch.searchBytes) > -1)
                        {
                            packet.Searches.Add(curSearch);
                            curSearch.Packets.Add(packet);
                        }
                    }
                }));
            }

            destination.Write(buffer, 0, count);
        }
    }
}
