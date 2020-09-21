using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoundlessProxyUi.ProxyManager.Components
{
    class CertComponent : ComponentBase
    {
        public override string Title => "Cert";

        public override string Start()
        {
            try
            {
                ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsEnabled = true);
                ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsFullyOff = false);

                if (IsFullyOn) return null;

                if (!ManagerWindowViewModel.UserAuthorizedCert)
                {
                    throw new Exception("User has not yet authorized modifications.");
                }

                string bundlePath;
                string bundle;
                string path;

                try
                {
                    path = Path.GetDirectoryName(ManagerWindowViewModel.GamePath);
                    bundlePath = Path.Combine(path, "ca-bundle.crt");
                    bundle = File.ReadAllText(bundlePath);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to read contents of ca-bundle.crt.", ex);
                }

                string boundlessCrt;

                try
                {
                    boundlessCrt = File.ReadAllText("playboundless.crt");
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to read contents of playboundless.crt.", ex);
                }

                string cloudfrontsCrt;

                try
                {
                    cloudfrontsCrt = File.ReadAllText("cloudfront.crt");
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to read contents of cloudfront.crt.", ex);
                }

                bool modified = false;

                if (!bundle.Contains(boundlessCrt))
                {
                    modified = true;
                    bundle += boundlessCrt;
                }

                if (!bundle.Contains(cloudfrontsCrt))
                {
                    modified = true;
                    bundle += cloudfrontsCrt;
                }

                if (modified)
                {
                    try
                    {
                        File.WriteAllText(bundlePath, bundle);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Failed to write contents of ca-bundle.crt.", ex);
                    }
                }

                ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsFullyOn = true);
            }
            catch (Exception ex)
            {
                ProxyManagerWindow.Instance.Dispatcher.Invoke(() =>
                {
                    ProxyManagerWindow.Instance.FadeControl(new CheckCert(ex.Message));
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
                if (!ManagerWindowViewModel.UserAuthorizedCert)
                {
                    ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsFullyOff = true);
                    return errors;
                }

                string bundlePath;
                string bundle;
                string path;

                try
                {
                    path = Path.GetDirectoryName(ManagerWindowViewModel.GamePath);
                    bundlePath = Path.Combine(path, "ca-bundle.crt");
                    bundle = File.ReadAllText(bundlePath);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to read contents of ca-bundle.crt.", ex);
                }

                string boundlessCrt;

                try
                {
                    boundlessCrt = File.ReadAllText("playboundless.crt");
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to read contents of playboundless.crt.", ex);
                }

                string cloudfrontsCrt;

                try
                {
                    cloudfrontsCrt = File.ReadAllText("cloudfront.crt");
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to read contents of cloudfront.crt.", ex);
                }

                bool modified = false;

                if (bundle.Contains(boundlessCrt))
                {
                    modified = true;
                    bundle = bundle.Replace(boundlessCrt, string.Empty);
                }

                if (bundle.Contains(cloudfrontsCrt))
                {
                    modified = true;
                    bundle = bundle.Replace(cloudfrontsCrt, string.Empty);
                }

                if (modified)
                {
                    try
                    {
                        File.WriteAllText(bundlePath, bundle);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Failed to write contents of ca-bundle.crt.", ex);
                    }
                }
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
