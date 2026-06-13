using Sallimni.DriverApp.Views;

namespace Sallimni.DriverApp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("taskdetail", typeof(TaskDetailPage));
	}
}
