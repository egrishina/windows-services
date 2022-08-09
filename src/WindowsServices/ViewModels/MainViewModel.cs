using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WindowsServices.Models;

namespace WindowsServices.ViewModels;

public class MainViewModel : ObservableRecipient
{
    private const int TimeoutMs = 3000;

    private readonly ILogger _logger = Log.ForContext(typeof(MainViewModel));

    private RelayCommand _startServiceCommand;
    private RelayCommand _stopServiceCommand;
    private WindowsServiceModel _selectedService;

    public MainViewModel()
    {
        WindowsServices = new ObservableCollection<WindowsServiceModel>();
        GetServices();
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

    public RelayCommand StartServiceCommand => GetCommand(ref _startServiceCommand, StartService, () => true);
    public RelayCommand StopServiceCommand => GetCommand(ref _stopServiceCommand, StopService, () => true);

    private static RelayCommand GetCommand(ref RelayCommand existingCommand, Action execute, Func<bool> canExecute)
    {
        return existingCommand ??= new RelayCommand(execute, canExecute);
    }
    
    private void GetServices()
    {
        try
        {
            WindowsServices.Clear();

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
                WindowsServices.Add(item);
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
                var timeout = TimeSpan.FromMilliseconds(TimeoutMs);
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
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
                var timeout = TimeSpan.FromMilliseconds(TimeoutMs);
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
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
}