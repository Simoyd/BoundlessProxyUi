using Newtonsoft.Json.Linq;
using BoundlessProxyUi.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BoundlessProxyUi.ProxyManager.Components
{
    class HostsComponent : ComponentBase
    {
        public override string Title => "Hosts";

        public override string Start()
        {
            try
            {
                ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsEnabled = true);
                ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsFullyOff = false);

                if (IsFullyOn) return null;

                if (Process.GetProcessesByName("Boundless").FirstOrDefault() != null)
                {
                    throw new Exception("Boundless is running and must be closed to continue setup.");
                }

                UpdateProcessingText("Checking user auth.");

                if (!ManagerWindowViewModel.UserAuthorizedHostFile)
                {
                    throw new Exception("User has not yet authorized modifications.");
                }

                UpdateProcessingText("Checking admin.");

                if (!IsAdministrator())
                {
                    throw new Exception("Not running as administrator.");
                }

                UpdateProcessingText("Checking hosts file existance.");

                var hostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\drivers\etc\hosts");
                List<string> hostsFileContents = new List<string>();

                if (File.Exists(hostsFilePath))
                {
                    UpdateProcessingText("Reading hosts file content.");

                    try
                    {
                        hostsFileContents = File.ReadAllLines(hostsFilePath).ToList();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Failed to read contents of hosts file.", ex);
                    }
                }

                if (hostsFileContents.Where(cur => !cur.StartsWith("#") && (cur.Contains("playboundless.com") || cur.Contains("cloudfront.net"))).Any())
                {
                    UpdateProcessingText("Clearing hosts file.");

                    hostsFileContents = hostsFileContents.Where(cur => cur.StartsWith("#") || (!cur.Contains("playboundless.com") && !cur.Contains("cloudfront.net"))).ToList();

                    try
                    {
                        File.WriteAllLines(hostsFilePath, hostsFileContents.ToArray());
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Failed to write to hosts file.", ex);
                    }
                }

                UpdateProcessingText($"Getting planet server names from discovery server.");

                var curDsIp = Dns.GetHostAddresses(Constants.DiscoveryServer).First(curAddress => curAddress.AddressFamily == AddressFamily.InterNetwork).ToString();

                if (curDsIp == "127.0.0.1")
                {
                    throw new Exception("Failed to remove boundless related entries from hosts file.");
                }

                // Run IP addresses
                HttpClient client = new HttpClient();
                JArray serverListJson;

                try
                {
                    serverListJson = JArray.Parse(client.GetAsync($"https://{Constants.DiscoveryServer}:8902/list-gameservers").Result.Content.ReadAsStringAsync().Result);
                }
                catch (Exception ex)
                {
                    var bork = ex;
                    StringBuilder output = new StringBuilder();

                    while (bork != null)
                    {
                        output.AppendLine(bork.Message);
                        bork = bork.InnerException;
                    }

                    throw new Exception($"Failed to query discovery server: {output}", ex);
                }

                // Persist em
                try
                {
                    ComponentEngine.Instance.ServerList.Clear();

                    IPAddress dsIp;

                    // Account server
                    UpdateProcessingText($"Performing lookup: {Constants.AccountServer}.");

                    hostsFileContents.Add($"127.0.0.1 {Constants.AccountServer}");
                    dsIp = Dns.GetHostAddresses(Constants.AccountServer).First(curAddress => curAddress.AddressFamily == AddressFamily.InterNetwork);

                    if (dsIp.ToString() == "127.0.0.1")
                    {
                        throw new Exception("Hosts file not clear!");
                    }

                    ComponentEngine.Instance.ServerList.Add(new ServerList { Hostname = Constants.AccountServer, Ip = dsIp.ToString() });

                    // Discovery server
                    UpdateProcessingText($"Performing lookup: {Constants.DiscoveryServer}.");

                    hostsFileContents.Add($"127.0.0.1 {Constants.DiscoveryServer}");
                    dsIp = Dns.GetHostAddresses(Constants.DiscoveryServer).First(curAddress => curAddress.AddressFamily == AddressFamily.InterNetwork);

                    if (dsIp.ToString() == "127.0.0.1")
                    {
                        throw new Exception("Hosts file not clear!");
                    }

                    ComponentEngine.Instance.ServerList.Add(new ServerList { Hostname = Constants.DiscoveryServer, Ip = dsIp.ToString() });

                    // Planet servers
                    serverListJson.Select(cur => cur["addr"].Value<string>()).Distinct().ToList().ForEach(cur =>
                    {
                        UpdateProcessingText($"Performing lookup: {cur}.");

                        IPAddress entry = Dns.GetHostAddresses(cur).First(curAddress => curAddress.AddressFamily == AddressFamily.InterNetwork);

                        if (entry.ToString() == "127.0.0.1")
                        {
                            throw new Exception("Hosts file not clear!");
                        }

                        hostsFileContents.Add($"127.0.0.1 {cur}");
                        ComponentEngine.Instance.ServerList.Add(new ServerList { Hostname = cur, Ip = entry.ToString() });
                    });

                    string[] hosts = new string[]{
                            "gs-live-usw{0}.playboundless.com",
                            "gs-live-use{0}.playboundless.com",
                            "gs-live-euc{0}.playboundless.com",
                            "gs-live-aus{0}.playboundless.com",
                        };

                    foreach (string curHostPattern in hosts)
                    {
                        int curNum = -1;
                        int failures = 0;

                        while (failures < 3 && curNum <= 100)
                        {
                            ++curNum;

                            string curHost = string.Format(curHostPattern, curNum);

                            if (!ComponentEngine.Instance.ServerList.Where(cur => cur.Hostname == curHost).Any())
                            {
                                UpdateProcessingText($"Performing lookup: {curHost}.");
                                IPAddress entry = null;

                                try
                                {
                                    entry = Dns.GetHostAddresses(curHost).First(curAddress => curAddress.AddressFamily == AddressFamily.InterNetwork);
                                }
                                catch { }

                                if (entry == null)
                                {
                                    ++failures;
                                    continue;
                                }

                                if (entry.ToString() == "127.0.0.1")
                                {
                                    throw new Exception("Hosts file not clear!");
                                }

                                hostsFileContents.Add($"127.0.0.1 {curHost}");
                                ComponentEngine.Instance.ServerList.Add(new ServerList { Hostname = curHost, Ip = entry.ToString() });
                            }
                        }
                    }

                    // TODO: EXO HERE

                    Regex chunkReg = new Regex("^https\\://([^/]+)/");

                    serverListJson.Select(cur =>
                    {
                        var fullUrl = cur["chunksURL"].Value<string>();
                        Match m = chunkReg.Match(fullUrl);
                        return m.Groups[1].Value;
                    }).Distinct().ToList().ForEach(cur =>
                    {
                        UpdateProcessingText($"Performing lookup: {cur}.");

                        hostsFileContents.Add($"127.0.0.1 {cur}");
                        IPAddress entry = Dns.GetHostAddresses(cur).First(curAddress => curAddress.AddressFamily == AddressFamily.InterNetwork);

                        if (entry.ToString() == "127.0.0.1")
                        {
                            throw new Exception("Hosts file not clear!");
                        }

                        ComponentEngine.Instance.ServerList.Add(new ServerList { Hostname = cur, Ip = entry.ToString() });
                    });
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to lookup one or more boundless server IPs.", ex);
                }

                UpdateProcessingText($"Updating hosts file.");

                try
                {
                    File.WriteAllLines(hostsFilePath, hostsFileContents.ToArray());
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to write to hosts file.", ex);
                }

                ComponentEngine.Instance.ServerList.ForEach(cur =>
                {
                    UpdateProcessingText($"Confirming lookup: {cur.Hostname}.");

                    IPAddress entry = Dns.GetHostAddresses(cur.Hostname).First(curAddress => curAddress.AddressFamily == AddressFamily.InterNetwork);

                    if (entry.ToString() != "127.0.0.1")
                    {
                        throw new Exception("Hosts file did not apply.");
                    }
                });

                if (Process.GetProcessesByName("Boundless").FirstOrDefault() != null)
                {
                    throw new Exception("Boundless is running and must be closed to continue setup.");
                }

                ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsFullyOn = true);
            }
            catch (Exception ex)
            {
                ProxyManagerWindow.Instance.Dispatcher.Invoke(() =>
                {
                    ProxyManagerWindow.Instance.FadeControl(new HostsFileType(ex.Message));
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
                UpdateProcessingText("Checking user auth.");

                if (!ManagerWindowViewModel.UserAuthorizedHostFile)
                {
                    ProxyManagerWindow.Instance.Dispatcher.Invoke(() => IsFullyOff = true);
                    return errors;
                }

                UpdateProcessingText("Checking hosts file existance.");

                var hostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\drivers\etc\hosts");
                List<string> hostsFileContents = new List<string>();

                if (File.Exists(hostsFilePath))
                {
                    UpdateProcessingText("Reading hosts file content.");

                    try
                    {
                        hostsFileContents = File.ReadAllLines(hostsFilePath).ToList();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Failed to read contents of hosts file.", ex);
                    }
                }

                if (hostsFileContents.Where(cur => !cur.StartsWith("#") && (cur.Contains("playboundless.com") || cur.Contains("cloudfront.net"))).Any())
                {
                    UpdateProcessingText("Checking admin.");

                    if (!IsAdministrator())
                    {
                        throw new Exception("Not running as administrator. Hosts file must be cleared manually.");
                    }

                    UpdateProcessingText("Clearing hosts file.");

                    hostsFileContents = hostsFileContents.Where(cur => cur.StartsWith("#") || (!cur.Contains("playboundless.com") && !cur.Contains("cloudfront.net"))).ToList();

                    try
                    {
                        File.WriteAllLines(hostsFilePath, hostsFileContents.ToArray());
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Failed to write to hosts file.", ex);
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
