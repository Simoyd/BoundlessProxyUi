using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BoundlessProxyUi
{
    public class SslMitm
    {
        public SslMitm(IPAddress localIp, int port, X509Certificate2 certificate, Dictionary<string, string> hostLookup, ConnectionGrouping connectionGrouping)
        {
            m_localIp = localIp;
            m_port = port;
            m_certificate = certificate;
            m_hostLookup = hostLookup;
            m_connectionGrouping = connectionGrouping;
            connectionGrouping.SslMitm = this;

            m_listener = new TcpListener(localIp, port);
            m_listener.Start();
            m_listener.BeginAcceptTcpClient(OnBeginAcceptTcpClient, null);
        }

        private IPAddress m_localIp;
        private int m_port;
        private X509Certificate2 m_certificate;
        private Dictionary<string, string> m_hostLookup;
        private ConnectionGrouping m_connectionGrouping;

        private TcpListener m_listener;

        public bool ReplaceIpaddr { get; set; } = false;

        private void OnBeginAcceptTcpClient(IAsyncResult ar)
        {
            var clientConnection = m_listener.EndAcceptTcpClient(ar);
            m_listener.BeginAcceptTcpClient(OnBeginAcceptTcpClient, ar.AsyncState);

            try
            {
                // SSL the client
                var clientStream = clientConnection.GetStream();

                var inputSplitStream = new SplitStream.SplitStream(clientStream);
                var splitStream1 = inputSplitStream.GetReader();
                var splitStream2 = inputSplitStream.GetReader();
                inputSplitStream.StartReadAhead();

                StringBuilder sb = new StringBuilder();

                int prev = -1;

                int byt;
                while ((byt = splitStream2.ReadByte()) != -1)
                {
                    sb.Append((char)byt);

                    if ((char)byt == 'm' && (char)prev == 'o')
                    {
                        break;
                    }

                    prev = byt;
                }

                var host = sb.ToString();
                Regex hostreg = new Regex(@"[\.\-a-zA-Z0-9]*.playboundless.com$");
                Match q = hostreg.Match(host);
                var remoteHost = q.Groups[0].Value;

                inputSplitStream.TerminateReader(splitStream2);
                splitStream2.Dispose();

                var clientSecureStream = new SslStream(splitStream1, false);
                clientSecureStream.AuthenticateAsServer(m_certificate);

                // Connect to server
                var serverConnection = new TcpClient(m_hostLookup[remoteHost], m_port);

                // SSL the server
                var serverStream = serverConnection.GetStream();
                var serverSecureStream = new SslStream(serverStream, false, (a, b, c, d) => true);
                serverSecureStream.AuthenticateAsClient(remoteHost);

                MainWindow.Instance.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ConnectionInstance instanceModel = new ConnectionInstance()
                    {
                        HostName = remoteHost,
                        Id = Guid.NewGuid(),
                        Parent = m_connectionGrouping,
                    };
                    SslMitmInstance instance = new SslMitmInstance(clientSecureStream, serverSecureStream, instanceModel)
                    {
                        ReplaceIpaddr = ReplaceIpaddr,
                    };

                    m_connectionGrouping.Instances.Add(instanceModel);
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
