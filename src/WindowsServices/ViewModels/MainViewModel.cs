using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WindowsServices.Models;

namespace WindowsServices.ViewModels;

public class MainViewModel : ObservableRecipient
{
    private const int TimerIntervalMs = 1000;
    
    private readonly ILogger _logger = Log.ForContext(typeof(MainViewModel));
    private readonly DispatcherTimer _timer;

    private AsyncRelayCommand _getServicesCommand;
    private AsyncRelayCommand _startServiceCommand;
    private AsyncRelayCommand _stopServiceCommand;
    private WindowsServiceModel _selectedService;

    public MainViewModel()
    {
        WindowsServices = new ObservableCollection<WindowsServiceModel>();
        
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(TimerIntervalMs);
        _timer.Tick += DispatcherTimer_Tick;
        _timer.Start();
    }

    public WindowsServiceModel SelectedService
    {
        get => _selectedService;
        set
        {
            _selectedService = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<WindowsServiceModel> WindowsServices { get; }

    public AsyncRelayCommand GetServicesCommand => GetAsyncCommand(ref _getServicesCommand, GetServices, () => true);
    public AsyncRelayCommand StartServiceCommand => GetAsyncCommand(ref _startServiceCommand, StartService, () => true);
    public AsyncRelayCommand StopServiceCommand => GetAsyncCommand(ref _stopServiceCommand, StopService, () => true);

    private static RelayCommand GetCommand(ref RelayCommand existingCommand, Action execute, Func<bool> canExecute)
    {
        return existingCommand ??= new RelayCommand(execute, canExecute);
    }

    private static AsyncRelayCommand GetAsyncCommand(ref AsyncRelayCommand existingCommand, Action execute, Func<bool> canExecute)
    {
        return existingCommand ??= new AsyncRelayCommand(() => Task.Run(execute), canExecute);
    }
    
    private void GetServices()
    {
        try
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => WindowsServices.Clear()));

            ServiceController[] services = ServiceController.GetServices();
            _logger.Information("{@count} Windows Services found.", services.Length);

            foreach (ServiceController service in services)
            {
                var userName = string.Empty;
                var query = new SelectQuery(
                    $"select name, startname from Win32_Service where name = '{service.ServiceName}'");

                using (var searcher = new ManagementObjectSearcher(query))
                {
                    var collection = searcher.Get();
                    foreach (var obj in collection)
                    {
                        userName = obj["startname"]?.ToString();
                    }
                }

                var item = new WindowsServiceModel
                {
                    Name = service.ServiceName,
                    DisplayName = service.DisplayName,
                    Status = service.Status.ToString(),
                    Account = userName
                };
                Application.Current.Dispatcher.BeginInvoke(new Action(() => WindowsServices.Add(item)));
            }

            SelectedService = WindowsServices.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occured while retrieving Windows Services.");
        }
    }

    private void StartService()
    {
        if (SelectedService is null)
        {
            return;
        }

        var service = new ServiceController(SelectedService.Name);
        if (service.Status == ServiceControllerStatus.Stopped)
        {
            try
            {
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running);
                SelectedService.Status = service.Status.ToString();
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error(ex, "Could not start the service {@name}", SelectedService.Name);
                MessageBox.Show($"Administrator rights are required to perform the action.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            MessageBox.Show($"Service {SelectedService.Name} is already running.", "Information", MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void StopService()
    {
        if (SelectedService is null)
        {
            return;
        }

        var service = new ServiceController(SelectedService.Name);
        if (service.Status == ServiceControllerStatus.Running)
        {
            try
            {
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped);
                SelectedService.Status = service.Status.ToString();
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error(ex, "Could not stop the service {@name}", SelectedService.Name);
                MessageBox.Show($"Administrator rights are required to perform the action.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            MessageBox.Show($"Service {SelectedService.Name} is already stopped.", "Information", MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void DispatcherTimer_Tick(object sender, EventArgs e)
    {
        foreach (var windowsService in WindowsServices)
        {
            var serviceController = new ServiceController(windowsService.Name);
            var status = serviceController.Status.ToString();
            if (status != windowsService.Status)
            {
                SelectedService.Status = status;
            }
        }
    }
}