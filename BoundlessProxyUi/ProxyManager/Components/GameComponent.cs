using System;
using System.Collections.Generic;
using System.IO;

namespace BoundlessProxyUi.ProxyManager.Components
{
    class GameComponent : ComponentBase
    {
        public override string Title => "Game";

        public override string Start()
        {
            try
            {
                ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsEnabled = true);
                ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsFullyOff = false);

                if (IsFullyOn) return null;

                if (!File.Exists(ManagerWindowViewModel.GamePath))
                {
                    ManagerWindowViewModel.GamePath = string.Empty;
                    throw new Exception($"Unable to find boundless.exe at the specified location.");
                }

                if (!ManagerWindowViewModel.UserVerifiedGamePath)
                {
                    throw new Exception($"User has not yet verified the game path.");
                }

                if (!File.Exists(Path.Combine(Path.GetDirectoryName(ManagerWindowViewModel.GamePath), "ca-bundle.crt")))
                {
                    ManagerWindowViewModel.GamePath = string.Empty;
                    throw new Exception($"Unable to find ca-bundle.crt at the specified location.");
                }

                ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsFullyOn = true);
            }
            catch (Exception ex)
            {
                ProxyManagerWindow.Instance.Dispatcher.Invoke(() =>
                {
                    ProxyManagerWindow.Instance.FadeControl(new GamePath(ex.Message));
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
