using CommunityToolkit.Mvvm.ComponentModel;

namespace WindowsServices.Models;

public class WindowsServiceModel : ObservableObject
{
    private string _name;
    private string _displayName;
    private string _status;
    private string _account;

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            _displayName = value;
            OnPropertyChanged();
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
        }
    }

    public string Account
    {
        get => _account;
        set
        {
            _account = value;
            OnPropertyChanged();
        }
    }
}