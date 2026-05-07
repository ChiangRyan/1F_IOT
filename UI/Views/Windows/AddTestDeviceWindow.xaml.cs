using SANJET.Core.ViewModels;
using System.Windows;

namespace SANJET.UI.Views.Windows
{
    public partial class AddTestDeviceWindow : Window
    {
        public AddTestDeviceWindow()
        {
            InitializeComponent();
        }

        public AddTestDeviceWindow(AddTestDeviceViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
