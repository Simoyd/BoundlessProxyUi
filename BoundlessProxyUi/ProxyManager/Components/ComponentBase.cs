using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace BoundlessProxyUi.ProxyManager.Components
{
    abstract class ComponentBase
    {
        public abstract string Title { get; }
        public int ComponentIndex { get; set; }

        private bool _isEnabled = false;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                UpdateBackgroundColor();
            }
        }

        private bool _isFullyOn = false;
        public bool IsFullyOn
        {
            get => _isFullyOn;
            set
            {
                _isFullyOn = value;
                UpdateBackgroundColor();
            }
        }

        private bool _isFullyOff = true;
        public bool IsFullyOff
        {
            get => _isFullyOff;
            set
            {
                _isFullyOff = value;
                UpdateBackgroundColor();
            }
        }

        public ManagerWindowViewModel ManagerWindowViewModel { get; set; }

        public void UpdateBackgroundColor()
        {
            SolidColorBrush result = Brushes.Yellow;

            if (IsEnabled && IsFullyOn)
            {
                result = Brushes.Green;
            }

            if (!IsEnabled && IsFullyOff)
            {
                result = Brushes.Red;
            }

            ((Control)ProxyManagerWindow.Instance.stackMain.Children[ComponentIndex]).Background = result;
        }

        public static bool IsAdministrator()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void UpdateProcessingText(string text)
        {
            ProxyManagerWindow.Instance.Dispatcher.Invoke(() => ((ManagerWindowViewModel)ProxyManagerWindow.Instance.DataContext).ProcessingText = text);
        }

        public abstract string Start();

        public abstract List<string> Stop();
    }
}
