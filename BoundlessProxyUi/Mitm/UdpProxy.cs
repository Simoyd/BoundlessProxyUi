using BoundlessProxyUi.ProxyManager;
using BoundlessProxyUi.ProxyManager.Components;
using BoundlessProxyUi.ProxyUi;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Timers;

namespace BoundlessProxyUi.Mitm
{
    /// <summary>
    /// Class used to MITM a UDP connection
    /// </summary>
    class UdpProxy
    {
        /// <summary>
        /// Creates a new instance of UdpProxy
        /// </summary>
        /// <param name="localPort">The local port number to open for the proxy
        /// (remote port is set later as it is unknown until just before connection)</param>
        /// <param name="remoteHost">The remote host to route the incoming data to</param>
        /// <param name="displayHost">The remote host dns for user display</param>
        public UdpProxy(int localPort, string remoteHost, string displayHost)
        {
            LocalPort = localPort;
            RemoteHost = remoteHost;

            // Reserve the port so we don't need to pick new ports later
            m_listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, localPort));

            var udpGrouping = ComponentEngine.Instance.GetComponent<TcpComponent>().udpGroup;
            m_connectionInstance = new ConnectionInstance
            {
                HostName = displayHost,
                Id = Guid.NewGuid(),
                Parent = udpGrouping,
            };
        }

        private void closeTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (this)
            {
                m_closeTimer.Stop();
                m_closeTimer = null;
                m_connectionInstance.IsConnectionOpen = false;
            }
        }

        private void deleteTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            InstanceAdded = false;
        }

        /// <summary>
        /// The local port number to open for the proxy
        /// </summary>
        public int LocalPort { get; set; }

        /// <summary>
        /// The remote host to route the incoming data to
        /// </summary>
        public string RemoteHost { get; set; }

        /// <summary>
        /// The remote port to route the incoming data to
        /// </summary>
        public int RemotePort { get; set; } = 0;

        /// <summary>
        /// Client used to listen for incoming data
        /// </summary>
        private UdpClient m_listener;

        /// <summary>
        /// Signal for failure or termination
        /// </summary>
        private TaskCompletionSource<object> m_failureSource = new TaskCompletionSource<object>();

        /// <summary>
        /// The connection instance this UDP connection is for (for the UI)
        /// </summary>
        private ConnectionInstance m_connectionInstance;

        private Timer m_closeTimer;

        private Timer m_deleteTimer;

        /// <summary>
        /// Bool indicating if this instance is being tracked on the UI
        /// </summary>
        private bool m_instanceAdded = false;

        private bool InstanceAdded
        {
            get
            {
                return m_instanceAdded;
            }
            set
            {
                lock (this)
                {
                    if (value)
                    {
                        m_closeTimer?.Stop();
                        m_deleteTimer?.Stop();

                        if (!m_instanceAdded)
                        {
                            var udpGrouping = ComponentEngine.Instance.GetComponent<TcpComponent>().udpGroup;

                            ProxyManagerWindow.Instance.Dispatcher.Invoke(new Action(() =>
                            {
                                udpGrouping.Instances.Add(m_connectionInstance);
                            }));
                        }

                        m_connectionInstance.IsConnectionOpen = true;

                        m_closeTimer = new Timer
                        {
                            Interval = 5000,
                        };

                        m_closeTimer.Elapsed += closeTimer_Elapsed;
                        m_closeTimer.Start();

                        m_deleteTimer = new Timer
                        {
                            Interval = Math.Max(ProxyUiWindow.Instance.GetDataContext<ProxyUiWindowViewModel>().DeathTimeout * 1000, 10000),
                        };

                        m_deleteTimer.Elapsed += deleteTimer_Elapsed;
                        m_deleteTimer.Start();
                    }
                    else if (!value && m_instanceAdded)
                    {
                        m_deleteTimer.Stop();
                        m_deleteTimer = null;

                        m_connectionInstance.IsConnectionOpen = false;

                        // Remove the instance from the UI
                        ProxyManagerWindow.Instance.Dispatcher.Invoke(new Action(() =>
                        {
                            if (m_connectionInstance.Parent.Instances.Contains(m_connectionInstance))
                            {
                                m_connectionInstance.Parent.Instances.Remove(m_connectionInstance);
                            }
                        }));
                    }

                    m_instanceAdded = value;
                }
            }
        }

        /// <summary>
        /// Begins the UDP MITM. RemotePort must be set before calling this
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            // Client to send data to the server
            UdpClient sender = new UdpClient();

            // Endpoint representing the local port
            var endpoint = new IPEndPoint(IPAddress.Any, LocalPort);

            try
            {
                // Local function called when data is received
                void requestCallback(IAsyncResult ar)
                {
                    try
                    {
                        // Complete the receive and get the bytes
                        var bytes = m_listener.EndReceive(ar, ref endpoint);

                        // Send the bytes to the server
                        if (RemotePort != 0)
                        {
                            InstanceAdded = true;

                            if (ProxyUiWindow.Instance.GetDataContext<ProxyUiWindowViewModel>().CaptureEnabled)
                            {
                                ProxyUiWindow.Instance.GetDataContext<ProxyUiWindowViewModel>().SendBytesToUi(m_connectionInstance, new CommPacket
                                {
                                    Data = bytes,
                                    Direction = CommPacketDirection.ClientToServer,
                                    Id = Guid.NewGuid(),
                                    Instance = m_connectionInstance,
                                    ParentPacket = null,
                                    Header = "UDP data",
                                });
                            }

                            if (sender.Send(bytes, bytes.Length, RemoteHost, RemotePort) != bytes.Length)
                            {
                                throw new Exception("UDP send failed");
                            }
                        }

                        // Listen for more bytes
                        m_listener.BeginReceive(requestCallback, null);
                    }
                    catch (Exception ex)
                    {
                        // Terminate the port on failure
                        m_failureSource.TrySetException(ex);
                    }
                }

                // Perform the first listen
                m_listener.BeginReceive(requestCallback, null);
            }
            catch (Exception ex)
            {
                // Terminate the port on failure
                m_failureSource.TrySetException(ex);
            }

            try
            {
                // Wait until failure or termination
                await m_failureSource.Task;
            }
            catch { }
        }

        /// <summary>
        /// Terminate the MITM
        /// </summary>
        public void Kill()
        {
            try
            {
                // Close the incoming port
                m_listener.Close();

                // Signal the MITM to shutdown
                m_failureSource.TrySetResult(null);
            }
            catch { }
        }
    }
}
