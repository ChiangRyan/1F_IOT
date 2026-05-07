using SANJET.Core.ViewModels;
using System.Windows;

namespace SANJET.UI.Views.Windows
{
    public partial class AddDisplayAreaDeviceWindow : Window
    {
        public AddDisplayAreaDeviceWindow()
        {
            InitializeComponent();
        }

        public AddDisplayAreaDeviceWindow(AddDisplayAreaDeviceViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
