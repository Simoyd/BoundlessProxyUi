using Newtonsoft.Json.Linq;
using BoundlessProxyUi.ProxyManager.Components;
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BoundlessProxyUi.ProxyManager
{
    /// <summary>
    /// Interaction logic for HostsFileLookups.xaml
    /// </summary>
    public partial class HostsFileLookups : UserControl
    {
        public HostsFileLookups()
        {
            InitializeComponent();
        }

        private void UpdateProcessingText(string text)
        {
            Dispatcher.Invoke(() => txtProcessingText.Text = text);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            new Thread(() =>
            {
                try
                {
                    if (Process.GetProcessesByName("Boundless").FirstOrDefault() != null)
                    {
                        Dispatcher.Invoke(() => ProxyManagerWindow.Instance.FadeControl(new HostsFileType("Boundless is running and must be closed to continue setup.")));
                        return;
                    }

                    UpdateProcessingText("Checking hosts file existance.");

                    var hostsFilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\drivers\etc\hosts");
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

                    var badEntries = hostsFileContents.Where(cur => !cur.StartsWith("#") && (cur.Contains("playboundless.com") || cur.Contains("cloudfront.net")));

                    if (badEntries.Any())
                    {
                        UpdateProcessingText("Clearing hosts file.");
                        Dispatcher.Invoke(() => ProxyManagerWindow.Instance.FadeControl(new HostsFileClear(badEntries.ToArray())));
                        return;
                    }

                    UpdateProcessingText($"Getting planet server names from discovery server.");

                    var curDsIp = Dns.GetHostAddresses(Constants.DiscoveryServer).First(curAddress => curAddress.AddressFamily == AddressFamily.InterNetwork).ToString();

                    if (curDsIp == "127.0.0.1")
                    {
                        Dispatcher.Invoke(() => ProxyManagerWindow.Instance.FadeControl(new HostsFileClear(new string[] { $"127.0.0.1 {Constants.DiscoveryServer}" })));
                        return;
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
                        throw new Exception($"Failed to query discovery server: {ex.Message}", ex);
                    }

                    List<string> errors = new List<string>();

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
                            Dispatcher.Invoke(() => ProxyManagerWindow.Instance.FadeControl(new HostsFileClear(new string[] { $"127.0.0.1 {Constants.AccountServer}" })));
                            return;
                        }

                        ComponentEngine.Instance.ServerList.Add(new ServerList { Hostname = Constants.AccountServer, Ip = dsIp.ToString() });

                        // Discovery server
                        UpdateProcessingText($"Performing lookup: {Constants.DiscoveryServer}.");

                        hostsFileContents.Add($"127.0.0.1 {Constants.DiscoveryServer}");
                        dsIp = Dns.GetHostAddresses(Constants.DiscoveryServer).First(curAddress => curAddress.AddressFamily == AddressFamily.InterNetwork);

                        if (dsIp.ToString() == "127.0.0.1")
                        {
                            Dispatcher.Invoke(() => ProxyManagerWindow.Instance.FadeControl(new HostsFileClear(new string[] { $"127.0.0.1 {Constants.DiscoveryServer}" })));
                            return;
                        }

                        ComponentEngine.Instance.ServerList.Add(new ServerList { Hostname = Constants.DiscoveryServer, Ip = dsIp.ToString() });

                        serverListJson.Select(cur => cur["addr"].Value<string>()).Distinct().ToList().ForEach(cur =>
                        {
                            UpdateProcessingText($"Performing lookup: {cur}.");

                            IPAddress entry = Dns.GetHostAddresses(cur).First(curAddress => curAddress.AddressFamily == AddressFamily.InterNetwork);

                            if (entry.ToString() == "127.0.0.1")
                            {
                                errors.Add($"127.0.0.1 {cur}");
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
                                        errors.Add($"127.0.0.1 {curHost}");
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
                                errors.Add($"127.0.0.1 {cur}");
                            }

                            ComponentEngine.Instance.ServerList.Add(new ServerList { Hostname = cur, Ip = entry.ToString() });
                        });
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Failed to lookup one or more boundless server IPs.", ex);
                    }

                    if (errors.Count > 0)
                    {
                        Dispatcher.Invoke(() => ProxyManagerWindow.Instance.FadeControl(new HostsFileClear(errors.ToArray())));
                        return;
                    }

                    if (Process.GetProcessesByName("Boundless").FirstOrDefault() != null)
                    {
                        Dispatcher.Invoke(() => ProxyManagerWindow.Instance.FadeControl(new HostsFileType("Boundless is running and must be closed to continue setup.")));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => ProxyManagerWindow.Instance.FadeControl(new HostsFileType(ex.Message)));
                    return;
                }

                Dispatcher.Invoke(() => ProxyManagerWindow.Instance.FadeControl(new HostsFileUpdate(string.Empty, ComponentEngine.Instance.ServerList.Select(cur => $"127.0.0.1 {cur.Hostname}").ToArray())));
            }).Start();
        }
    }
}
