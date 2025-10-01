using System.Configuration;
using System.Data;
using System.Windows;

namespace EvoDesigner;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Test LinkedImage functionality
        TestLinkedImage.TestCreateLinkedImageElement();
    }
}

