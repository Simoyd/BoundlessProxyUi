using BoundlessProxyUi.ProxyManager;
using BoundlessProxyUi.ProxyManager.Components;
using BoundlessProxyUi.ProxyUi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace BoundlessProxyUi.Mitm
{
    class SslMitm
    {
        private static readonly Regex s_hostreg = new Regex(@"[\.\-a-zA-Z0-9]*(.playboundless.com|.cloudfront.net)");

        public SslMitm(IPAddress localIp, int port, Dictionary<string, string> hostLookup, bool replaceIpaddr)
        {
            m_localIp = localIp;
            m_port = port;
            m_certificates["playboundless.com"] = new X509Certificate2("playboundless.pfx", "");
            m_certificates["cloudfront.net"] = new X509Certificate2("cloudfront.pfx", "");
            m_hostLookup = hostLookup;

            m_listener = new TcpListener(m_localIp, port);
            m_listener.Start();
            m_listener.BeginAcceptTcpClient(OnBeginAcceptTcpClient, null);

            m_replaceIpaddr = replaceIpaddr;
        }

        private readonly IPAddress m_localIp;
        private readonly int m_port;
        private readonly Dictionary<string, X509Certificate2> m_certificates = new Dictionary<string, X509Certificate2>();
        private readonly Dictionary<string, string> m_hostLookup;
        private readonly TcpListener m_listener;
        private readonly bool m_replaceIpaddr;

        public void Kill()
        {
            try
            {
                m_listener.Stop();
            }
            catch { }
        }

        private void OnBeginAcceptTcpClient(IAsyncResult ar)
        {
            TcpClient clientConnection;

            try
            {
                clientConnection = m_listener.EndAcceptTcpClient(ar);
            }
            catch
            {
                Kill();
                return;
            }

            m_listener.BeginAcceptTcpClient(OnBeginAcceptTcpClient, ar.AsyncState);

            string remoteHost = null;

            try
            {
                // SSL the client
                var clientStream = clientConnection.GetStream();
                var inputSplitStream = new SplitStream.SplitStream(clientStream);
                var splitStream1 = inputSplitStream.GetReader();
                splitStream1.IsMaster = true;
                var splitStream2 = inputSplitStream.GetReader();
                inputSplitStream.StartReadAhead();

                StringBuilder sb = new StringBuilder();
                int byt;
                byte[] buffer = new byte[1024];
                remoteHost = null;

                while (true)
                {
                    if ((byt = splitStream2.ReadByte()) == -1)
                    {
                        break;
                    }

                    sb.Append((char)byt);
                    int av = 0;
                    while ((av = splitStream2.GetBufferAvailable()) > 0)
                    {
                        splitStream2.Read(buffer, 0, Math.Min(buffer.Length, av));
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, av));
                    }

                    var sbstring = sb.ToString();
                    Match q = s_hostreg.Match(sbstring);

                    if (q.Success)
                    {
                        remoteHost = q.Groups[0].Value;
                        break;
                    }
                }

                inputSplitStream.TerminateReader(splitStream2);
                splitStream2.Dispose();

                var clientSecureStream = new SslStream(splitStream1, false);
                clientSecureStream.AuthenticateAsServer(m_certificates.Where(cur => remoteHost.EndsWith(cur.Key)).First().Value);

                // Connect to server
                var serverConnection = new TcpClient(m_hostLookup[remoteHost], m_port);

                // SSL the server
                var serverStream = serverConnection.GetStream();
                var serverSecureStream = new SslStream(serverStream, false, (a, b, c, d) => true);
                serverSecureStream.AuthenticateAsClient(remoteHost);

                ProxyManagerWindow.Instance.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var connectionGrouping = ComponentEngine.Instance.GetComponent<TcpComponent>().GetGrouping(remoteHost, m_port);

                    ConnectionInstance instanceModel = new ConnectionInstance()
                    {
                        HostName = remoteHost,
                        Id = Guid.NewGuid(),
                        Parent = connectionGrouping,
                    };
                    SslMitmInstance instance = new SslMitmInstance(clientSecureStream, serverSecureStream, instanceModel)
                    {
                        ReplaceIpaddr = m_replaceIpaddr,
                    };

                    connectionGrouping.Instances.Add(instanceModel);
                }));
            }
            catch (Exception)
            {
                //MainWindow.Instance.Dispatcher.Invoke(new Action(() =>
                //{
                //    MessageBox.Show($"5 Fatal error (did you prep?) ({remoteHost ?? "nohost"}): {ex.Message}");
                //}));
            }
        }
    }
}
