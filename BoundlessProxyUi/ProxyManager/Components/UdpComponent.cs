using BoundlessProxyUi.Mitm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoundlessProxyUi.ProxyManager.Components
{
    class UdpComponent : ComponentBase
    {
        public override string Title => "Udp";

        public override string Start()
        {
            try
            {
                ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsEnabled = true);
                ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsFullyOff = false);

                if (IsFullyOn) return null;

                SslMitmInstance.KillUdp();

                try
                {
                    SslMitmInstance.InitPlanets(ComponentEngine.Instance.ServerLookup).Wait();
                }
                catch (Exception ex)
                {
                    SslMitmInstance.KillUdp();
                    throw new Exception("Failed to bind to one or more UDP ports.", ex);
                }

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
                SslMitmInstance.KillUdp();
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }

            ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsFullyOff = true);

            return errors;
        }
    }
}
