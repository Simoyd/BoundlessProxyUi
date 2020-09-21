using BoundlessProxyUi.Mitm;
using BoundlessProxyUi.ProxyUi;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;

namespace BoundlessProxyUi.ProxyManager.Components
{
    class TcpComponent : ComponentBase
    {
        public override string Title => "Tcp";

        public ObservableCollection<ConnectionGrouping> groups = new ObservableCollection<ConnectionGrouping>();
        public List<SslMitm> mitms = new List<SslMitm>();

        public ConnectionGrouping accountGroup;
        public ConnectionGrouping discoveryGroup;
        public ConnectionGrouping planetApiGroup;
        public ConnectionGrouping websocketGroup;
        public ConnectionGrouping udpGroup;
        public ConnectionGrouping chunkGroup;

        public ConnectionGrouping GetGrouping(string remoteHost, int remotePort)
        {
            if (remoteHost == "account.playboundless.com")
            {
                return accountGroup;
            }
            else if (remoteHost.EndsWith(".cloudfront.net"))
            {
                return chunkGroup;
            }
            else if (remotePort == 8902)
            {
                return discoveryGroup;
            }
            else
            {
                return planetApiGroup;
            }
        }

        public override string Start()
        {
            try
            {
                ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsEnabled = true);
                ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsFullyOff = false);

                if (IsFullyOn) return null;

                mitms.ToList().ForEach(cur => cur.Kill());
                groups.ToList().ForEach(curGroup =>
                {
                    curGroup.Instances.ToList().ForEach(cur =>
                    {
                        cur.SslMitmInstance.Kill(true);
                        cur.SslMitmInstance.Kill(false);
                    });
                });

                mitms.Clear();
                groups.Clear();

                ProxyManagerWindow.Instance.Dispatcher.Invoke(() =>
                {
                    groups.Add(accountGroup = new ConnectionGrouping
                    {
                        GroupingName = "Account API",
                    });

                    groups.Add(discoveryGroup = new ConnectionGrouping
                    {
                        GroupingName = "Discovery API",
                    });

                    groups.Add(planetApiGroup = new ConnectionGrouping
                    {
                        GroupingName = "Planet API",
                    });

                    groups.Add(websocketGroup = new ConnectionGrouping
                    {
                        GroupingName = "Planet Websocket",
                    });

                    groups.Add(chunkGroup = new ConnectionGrouping
                    {
                        GroupingName = "Planet Chunk",
                    });

                    groups.Add(udpGroup = new ConnectionGrouping
                    {
                        GroupingName = "Planet UDP",
                    });
                });

                SslMitm discoveryMitm = null;

                try
                {
                    discoveryMitm = new SslMitm(IPAddress.Loopback, 8902, ComponentEngine.Instance.ServerLookup, true);

                }
                catch (Exception ex)
                {
                    discoveryMitm?.Kill();

                    throw new Exception("Failed to open port 127.0.0.1:8902.", ex);
                }

                SslMitm websocketMitm = null;

                try
                {
                    websocketMitm = new SslMitm(IPAddress.Loopback, 443, ComponentEngine.Instance.ServerLookup, true);

                }
                catch (Exception ex)
                {
                    discoveryMitm.Kill();
                    websocketMitm?.Kill();

                    throw new Exception("Failed to open port 127.0.0.1:443.", ex);
                }

                mitms.Add(discoveryMitm);
                mitms.Add(websocketMitm);

                ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsFullyOn = true);
            }
            catch (Exception ex)
            {
                ProxyManagerWindow.Instance.Dispatcher.Invoke(() =>
                {
                    ProxyManagerWindow.Instance.FadeControl(new TcpIssue(ex.Message));
                });

                return ex.Message;
            }

            return null;
        }

        public override List<string> Stop()
        {
            List<string> errors = new List<string>();

            ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsEnabled = false);
            ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsFullyOn = false);

            if (IsFullyOff)
            {
                return errors;
            }

            try
            {
                mitms.ToList().ForEach(cur => cur.Kill());
                groups.ToList().ForEach(curGroup =>
                {
                    curGroup.Instances.ToList().ForEach(cur =>
                    {
                        cur?.SslMitmInstance?.Kill(true);
                        cur?.SslMitmInstance?.Kill(false);
                    });
                });
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }

            ProxyManagerWindow.Instance.Dispatcher.Invoke(() => mitms.Clear());
            ProxyManagerWindow.Instance.Dispatcher.Invoke(() => groups.Clear());
            ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsFullyOff = true);

            return errors;
        }
    }
}
